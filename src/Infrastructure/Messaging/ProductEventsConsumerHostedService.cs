using System.Text;
using System.Text.Json;
using Kart.Shared.Messaging;
using KartOfferService.Application.Features.RecomputeCatalogPrice;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace KartOfferService.Infrastructure.Messaging;

/// <summary>
/// OFF-4: consumes `offer.product-events.queue` (bound to Product's own `product.exchange` /
/// `product.price.changed`, per contracts/message-bus-manifest.json) and dispatches to
/// <see cref="RecomputeCatalogPriceCommand"/> via MediatR. On handler failure, walks the
/// manifest's retry ladder before dead-lettering. Mirrors kart-delivery-tracking-service's
/// `ShippingEventsConsumerHostedService` shape.
/// </summary>
public sealed class ProductEventsConsumerHostedService : BackgroundService
{
    private const string QueueName = "offer.product-events.queue";
    private const string RetryCountHeader = "x-offer-product-events-retry-count";

    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionFactory _connectionFactory;
    private readonly MessageBusManifest _manifest;
    private readonly ILogger<ProductEventsConsumerHostedService> _logger;

    public ProductEventsConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        IConnectionFactory connectionFactory,
        MessageBusManifest manifest,
        ILogger<ProductEventsConsumerHostedService> logger)
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
                _logger.LogError(ex, "Product-events consumer lost its RabbitMQ connection; reconnecting in {Delay}.", ReconnectDelay);
                await Task.Delay(ReconnectDelay, stoppingToken);
            }
        }
    }

    private async Task OnMessageReceivedAsync(IModel channel, BasicDeliverEventArgs deliverEventArgs, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var json = Encoding.UTF8.GetString(deliverEventArgs.Body.Span);

            var payload = JsonSerializer.Deserialize<ProductPriceChangedEventPayload>(json, SerializerOptions)
                ?? throw new InvalidOperationException("ProductPriceChanged payload deserialized to null.");

            var result = await sender.Send(new RecomputeCatalogPriceCommand(payload.Sku, payload.NewPrice), stoppingToken);
            if (result.IsFailure)
            {
                throw new InvalidOperationException($"RecomputeCatalogPrice failed: {result.Error.Code} - {result.Error.Message}");
            }

            channel.BasicAck(deliverEventArgs.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            HandleFailure(channel, deliverEventArgs, ex);
        }
    }

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

            _logger.LogWarning(ex, "Handling ProductPriceChanged failed; routed to retry tier {Tier} (attempt {Attempt}).", tier.Name, retryCount + 1);
        }
        else
        {
            _logger.LogCritical(ex, "Handling ProductPriceChanged failed after exhausting all retry tiers; dead-lettering.");
            channel.BasicNack(deliverEventArgs.DeliveryTag, multiple: false, requeue: false);
        }
    }
}
