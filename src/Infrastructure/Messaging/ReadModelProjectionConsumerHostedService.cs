using System.Text;
using System.Text.Json;
using Kart.Shared.Messaging;
using KartOfferService.Infrastructure.Persistence.ReadModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KartOfferService.Infrastructure.Messaging;

/// <summary>
/// The CQRS sync mechanism the user's requirements explicitly call for: self-consumes every
/// `Coupon*`/`Promotion*` event this service just published on its own `offer.exchange`
/// (`offer.read-model-projection.queue`, bound with wildcard routing keys) and applies the
/// equivalent change to the MongoDB denormalized read model via <see cref="ReadModelProjectionWriter"/>.
/// This is the only path that ever writes to the read side - PostgreSQL (via the transactional
/// outbox) remains the sole source of truth, and the read model is always rebuildable by replaying
/// the outbox/event log, never a second place business logic writes to directly. The same
/// "self-consumed event, own internal pipeline" shape kart-delivery-tracking-service uses for its
/// own ingestion buffer, applied here to CQRS projection instead.
/// </summary>
public sealed class ReadModelProjectionConsumerHostedService : BackgroundService
{
    private const string QueueName = "offer.read-model-projection.queue";
    private const string RetryCountHeader = "x-offer-read-model-projection-retry-count";

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionFactory _connectionFactory;
    private readonly MessageBusManifest _manifest;
    private readonly ILogger<ReadModelProjectionConsumerHostedService> _logger;

    public ReadModelProjectionConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        IConnectionFactory connectionFactory,
        MessageBusManifest manifest,
        ILogger<ReadModelProjectionConsumerHostedService> logger)
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

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, deliverEventArgs) => await OnMessageReceivedAsync(channel, deliverEventArgs, stoppingToken);
                channel.BasicConsume(QueueName, autoAck: false, consumer);

                while (!stoppingToken.IsCancellationRequested && connection.IsOpen)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Read-model-projection consumer lost its RabbitMQ connection; reconnecting in {Delay}.", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task OnMessageReceivedAsync(IModel channel, BasicDeliverEventArgs deliverEventArgs, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var writer = scope.ServiceProvider.GetRequiredService<ReadModelProjectionWriter>();
            var json = Encoding.UTF8.GetString(deliverEventArgs.Body.Span);
            var eventType = _manifest.EventTypeForRoutingKey(deliverEventArgs.RoutingKey);

            await ProjectAsync(writer, eventType, json, stoppingToken);

            channel.BasicAck(deliverEventArgs.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            HandleFailure(channel, deliverEventArgs, ex);
        }
    }

    private static async Task ProjectAsync(ReadModelProjectionWriter writer, string eventType, string json, CancellationToken cancellationToken)
    {
        switch (eventType)
        {
            case "CouponIssued":
                {
                    var payload = Deserialize<CouponIssuedPayload>(json);
                    await writer.UpsertCouponAsync(payload.Code, payload.PerUserCap, payload.GlobalCap, payload.ValidFrom, payload.ValidUntil, DateTime.UtcNow, cancellationToken);
                    break;
                }
            case "CouponRedeemed":
                {
                    var payload = Deserialize<CouponRedeemedPayload>(json);
                    await writer.IncrementCouponRedemptionsAsync(payload.Code, +1, DateTime.UtcNow, cancellationToken);
                    break;
                }
            case "CouponRedemptionVoided":
                {
                    var payload = Deserialize<CouponRedemptionVoidedPayload>(json);
                    await writer.IncrementCouponRedemptionsAsync(payload.Code, -1, DateTime.UtcNow, cancellationToken);
                    break;
                }
            case "CouponDeactivated":
                {
                    var payload = Deserialize<CouponDeactivatedPayload>(json);
                    await writer.UpdateCouponValidUntilAsync(payload.Code, payload.DeactivatedAt, DateTime.UtcNow, cancellationToken);
                    break;
                }
            case "PromotionActivated":
                {
                    var payload = Deserialize<PromotionActivatedPayload>(json);
                    await writer.UpsertPromotionAsync(payload.CampaignId.ToString(), payload.Window.StartsAt, payload.Window.EndsAt, DateTime.UtcNow, cancellationToken);
                    break;
                }
            case "PromotionDeactivated":
                {
                    var payload = Deserialize<PromotionDeactivatedPayload>(json);
                    await writer.UpdatePromotionEndsAtAsync(payload.CampaignId.ToString(), payload.DeactivatedAt, DateTime.UtcNow, cancellationToken);
                    break;
                }
            default:
                throw new InvalidOperationException($"Read-model-projection consumer has no handling for event type '{eventType}'.");
        }
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, SerializerOptions) ?? throw new InvalidOperationException($"{typeof(T).Name} payload deserialized to null.");

    private void HandleFailure(IModel channel, BasicDeliverEventArgs deliverEventArgs, Exception ex)
    {
        var retryCount = RetryHeaders.GetRetryCount(deliverEventArgs.BasicProperties, RetryCountHeader);
        var tiers = _manifest.GetQueue(QueueName).RetryLadder?.Tiers ?? Array.Empty<RetryTierDefinition>();

        if (retryCount < tiers.Count)
        {
            var tier = tiers[retryCount];
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.Headers = new Dictionary<string, object> { [RetryCountHeader] = retryCount + 1 };

            channel.BasicPublish(exchange: string.Empty, routingKey: tier.Name, basicProperties: properties, body: deliverEventArgs.Body);
            channel.BasicAck(deliverEventArgs.DeliveryTag, multiple: false);

            _logger.LogWarning(ex, "Read-model projection failed for routing key {RoutingKey}; routed to retry tier {Tier} (attempt {Attempt}).", deliverEventArgs.RoutingKey, tier.Name, retryCount + 1);
        }
        else
        {
            _logger.LogCritical(ex, "Read-model projection failed after exhausting all retry tiers for routing key {RoutingKey}; dead-lettering. The read model will lag until this is replayed from the DLQ.", deliverEventArgs.RoutingKey);
            channel.BasicNack(deliverEventArgs.DeliveryTag, multiple: false, requeue: false);
        }
    }

    private sealed record CouponIssuedPayload(string Code, int? PerUserCap, int? GlobalCap, DateTime ValidFrom, DateTime ValidUntil);
    private sealed record CouponRedeemedPayload(string Code, string UserId, string OrderId);
    private sealed record CouponRedemptionVoidedPayload(string Code, string OrderId, DateTime VoidedAt);
    private sealed record CouponDeactivatedPayload(string Code, DateTime DeactivatedAt);
    private sealed record PromotionActivatedPayload(Guid CampaignId, PromotionWindowPayload Window);
    private sealed record PromotionWindowPayload(DateTime StartsAt, DateTime EndsAt);
    private sealed record PromotionDeactivatedPayload(Guid CampaignId, DateTime DeactivatedAt);
}
