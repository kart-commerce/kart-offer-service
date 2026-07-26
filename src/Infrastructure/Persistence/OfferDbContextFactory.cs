using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KartOfferService.Infrastructure.Persistence;

/// <summary>
/// Design-time-only factory `dotnet ef migrations add`/`database update` use to build
/// <see cref="OfferDbContext"/> without spinning up the full Api host. Never used at runtime -
/// the app's own DI registration (Infrastructure/DependencyInjection.cs) takes over there.
/// </summary>
public sealed class OfferDbContextFactory : IDesignTimeDbContextFactory<OfferDbContext>
{
    public OfferDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("OFFER_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=kart_offer;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<OfferDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new OfferDbContext(optionsBuilder.Options);
    }
}
