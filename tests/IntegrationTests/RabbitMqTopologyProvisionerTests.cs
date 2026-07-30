using FluentAssertions;
using Kart.Shared.Messaging;
using KartOfferService.IntegrationTests.Fixtures;
using Xunit;

namespace KartOfferService.IntegrationTests;

/// <summary>
/// Validates this service's actual, committed contracts/message-bus-manifest.json - the single
/// source of truth for its RabbitMQ topology - against a real broker. A mocked IModel would only
/// prove the C# code calls the right methods; this proves the manifest itself produces a topology
/// RabbitMQ actually accepts.
/// </summary>
[Collection("RabbitMq")]
public class RabbitMqTopologyProvisionerTests
{
    private readonly RabbitMqContainerFixture _fixture;

    public RabbitMqTopologyProvisionerTests(RabbitMqContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private static MessageBusManifest LoadRealManifest() =>
        MessageBusManifestLoader.Load(Path.Combine(AppContext.BaseDirectory, "message-bus-manifest.json"));

    [Fact]
    public void Declare_FromTheRealCommittedManifest_ProvisionsEveryDeclaredQueueAndExchange()
    {
        var manifest = LoadRealManifest();
        using var connection = _fixture.ConnectionFactory.CreateConnection();
        using var channel = connection.CreateModel();

        RabbitMqTopologyProvisioner.Declare(channel, manifest);

        foreach (var exchange in manifest.Exchanges.Concat(manifest.ExternalExchanges))
        {
            var act = () => channel.ExchangeDeclarePassive(exchange.Name);
            act.Should().NotThrow($"exchange '{exchange.Name}' should have been declared from the manifest");
        }

        foreach (var queue in manifest.Queues)
        {
            var act = () => channel.QueueDeclarePassive(queue.Name);
            act.Should().NotThrow($"queue '{queue.Name}' should have been declared from the manifest");
        }

        foreach (var dlq in manifest.DeadLetterQueues)
        {
            var act = () => channel.QueueDeclarePassive(dlq.Name);
            act.Should().NotThrow($"dead-letter queue '{dlq.Name}' should have been declared from the manifest");
        }
    }

    [Fact]
    public void Declare_IsIdempotent_CallingTwiceOnTheSameChannelDoesNotThrow()
    {
        var manifest = LoadRealManifest();
        using var connection = _fixture.ConnectionFactory.CreateConnection();
        using var channel = connection.CreateModel();

        RabbitMqTopologyProvisioner.Declare(channel, manifest);
        var act = () => RabbitMqTopologyProvisioner.Declare(channel, manifest);

        act.Should().NotThrow();
    }

    [Fact]
    public void Declare_PublishOnOfferExchange_RoutesIntoTheSelfConsumedReadModelProjectionQueue()
    {
        // The exact self-publish/self-consume loop the CQRS read-model sync depends on:
        // CouponRedeemed publishes onto offer.exchange, and this service's own
        // offer.read-model-projection.queue (bound with a wildcard routing key) receives it.
        var manifest = LoadRealManifest();
        using var connection = _fixture.ConnectionFactory.CreateConnection();
        using var channel = connection.CreateModel();
        RabbitMqTopologyProvisioner.Declare(channel, manifest);

        channel.BasicPublish(
            exchange: manifest.ExchangeFor("CouponRedeemed"),
            routingKey: manifest.RoutingKeyFor("CouponRedeemed"),
            mandatory: false,
            basicProperties: channel.CreateBasicProperties(),
            body: "{}"u8.ToArray());

        Thread.Sleep(500);
        var messageCount = channel.MessageCount("offer.read-model-projection.queue");
        messageCount.Should().Be(1u);
    }
}
