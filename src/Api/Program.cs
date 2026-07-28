using Kart.Shared.Auditing;
using Kart.Shared.Configuration;
using Kart.Shared.ErrorHandling;
using Kart.Shared.Observability;
using KartOfferService.Api.Security;
using KartOfferService.Application;
using KartOfferService.Application.Common.Exceptions;
using KartOfferService.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// kart-conventions.md Configuration Management: GlobalConfig external-secrets-file bootstrap,
// shared across every service - never reimplemented per service. See appsettings.Local.json.example.
builder.AddKartGlobalConfig();

// kart-conventions.md Observability section: Serilog + OpenTelemetry SDK behind one DI call.
// Standard (not 100%) trace-sampling tier - Offer is not an Order Saga participant
// (requirement-spec.md's Observability NFR row).
builder.AddKartObservability("kart-offer-service");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOfferAuthentication();

// kart-conventions.md Error Handling section: the single global exception handler +
// ProblemDetails factory, wired once via the shared package - no local try/catch for translation
// anywhere in this service's handler/controller/domain code. The two mappings below translate
// Infrastructure's database-level race backstops (see EfUnitOfWork) into the same envelope a
// Result.Failure produces via Api/Common/ResultExtensions.
builder.Services.AddKartErrorHandling(options => options
    .Map<DuplicateKeyException>(StatusCodes.Status409Conflict, "conflict")
    .Map<ConcurrencyConflictException>(StatusCodes.Status412PreconditionFailed, "stale_version"));

// No dedicated audit sink beyond the createdBy/updatedBy columns already stamped inline at each
// PostgreSQL write site - registers the safe NullAuditLogWriter default, ready to swap in a real
// sink (e.g. an outbox-backed one) if a future requirement asks for one.
builder.Services.AddKartAuditing();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Per-HTTP-request Information log (method/path/status/elapsed) - registered outermost, wrapping
// UseKartErrorHandling below, so this always logs the *final* status code a client actually
// received.
app.UseSerilogRequestLogging();

// The single global error handler - every unhandled exception is translated to the platform's
// ProblemDetails envelope and logged here.
app.UseKartErrorHandling();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Prometheus scrape target (observability-standards.md's mandatory /metrics).
app.MapPrometheusScrapingEndpoint();

app.MapControllers();

app.Run();

// Exposed for WebApplicationFactory<Program> in IntegrationTests/ContractTests.
public partial class Program
{
}
