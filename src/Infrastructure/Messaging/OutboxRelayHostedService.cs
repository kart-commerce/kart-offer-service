using System.Text;
using Kart.Shared.Messaging;
using Kart.Shared.Observability;
using KartOfferService.Application.Common;
using KartOfferService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace KartOfferService.Infrastructure.Messaging;

/// <summary>
/// Relays `offer_outbox_events` rows to whichever exchange/routing key
/// contracts/message-bus-manifest.json's `publishedEvents` maps each event type to. Declares the
/// full manifest topology idempotently on every (re)connect. Retries indefinitely until RabbitMQ
/// is reachable, rather than dead-lettering - the publish-side half of at-least-once delivery.
/// Mirrors kart-identity-service/kart-category-service's identically-shaped relay.
/// </summary>
public sealed class OutboxRelayHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private const int BatchSize = 100;

    // Maps each published event type to the business-flows.md flow it belongs to. `PriceQuoteIssued`
    // is deliberately absent - Pricing is not one of the two flows this service covers, so it gets
    // no Flow tag rather than a guessed one.
    private static readonly Dictionary<string, string> EventFlowNames = new()
    {
        ["CouponRedeemed"] = FlowNames.NormalShoppingPurchaseJourney,
        ["CouponIssued"] = FlowNames.OffersCouponsPromotionsManagementAdmin,
        ["CouponDeactivated"] = FlowNames.OffersCouponsPromotionsManagementAdmin,
        ["CouponRedemptionVoided"] = FlowNames.OffersCouponsPromotionsManagementAdmin,
        ["PromotionActivated"] = FlowNames.OffersCouponsPromotionsManagementAdmin,
        ["PromotionDeactivated"] = FlowNames.OffersCouponsPromotionsManagementAdmin,
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionFactory _connectionFactory;
    private readonly MessageBusManifest _manifest;
    private readonly ILogger<OutboxRelayHostedService> _logger;

    public OutboxRelayHostedService(
        IServiceScopeFactory scopeFactory,
        IConnectionFactory connectionFactory,
        MessageBusManifest manifest,
        ILogger<OutboxRelayHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _connectionFactory = connectionFactory;
        _manifest = manifest;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = _connectionFactory.CreateConnection();
                using var channel = connection.CreateModel();
                RabbitMqTopologyProvisioner.Declare(channel, _manifest);

                await RunRelayLoopAsync(channel, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Offer outbox relay lost its RabbitMQ connection; reconnecting in {Delay}.", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task RunRelayLoopAsync(IModel channel, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RelayPendingBatchAsync(channel, stoppingToken);
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RelayPendingBatchAsync(IModel channel, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OfferDbContext>();

        var pending = await dbContext.OutboxEvents
            .Where(e => e.PublishedAt == null)
            .OrderBy(e => e.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var outboxEvent in pending)
        {
            using var flowScope = EventFlowNames.TryGetValue(outboxEvent.EventType, out var flowName)
                ? KartFlowContext.Push(flowName)
                : null;

            var exchange = _manifest.ExchangeFor(outboxEvent.EventType);
            var routingKey = _manifest.RoutingKeyFor(outboxEvent.EventType);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = outboxEvent.Id.ToString();
            properties.ContentType = "application/json";

            channel.BasicPublish(
                exchange: exchange,
                routingKey: routingKey,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(outboxEvent.Payload));

            outboxEvent.MarkPublished(DateTimeOffset.UtcNow);

            _logger.LogInformation(
                "Stage {Stage}: outbox event {EventId} of type {EventType} published to {Exchange}/{RoutingKey}",
                "OutboxEventPublished",
                outboxEvent.Id,
                outboxEvent.EventType,
                exchange,
                routingKey);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
