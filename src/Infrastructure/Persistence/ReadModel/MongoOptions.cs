namespace KartOfferService.Infrastructure.Persistence.ReadModel;

/// <summary>
/// Binds the "Mongo" configuration section - this service's CQRS read side (sharded MongoDB,
/// per the user's explicit architecture requirement). PostgreSQL remains the sole write-side
/// source of truth; nothing here is ever written to except by the read-model projection consumer.
/// </summary>
public sealed class MongoOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    public string Database { get; set; } = "kart_offer_read";
}
