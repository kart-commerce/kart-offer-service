using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartOfferService.Application.Common.Behaviors;

/// <summary>
/// requirement-spec.md's Observability NFR row: every command/query gets a structured Information
/// log on completion, tagged with its own name and duration. Never logs the request/response
/// objects themselves (only the type name) so this can't leak cart contents/coupon codes by
/// construction. Exceptions are left unlogged here and rethrown as-is - logged once, at the true
/// boundary (Kart.Shared.ErrorHandling's global exception handler), never duplicated per layer.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        // Checkpoint-logging taxonomy stage 3 ("<Command>HandlerStarted", first line inside
        // Handle()) generalized here rather than duplicated in every handler - this behavior
        // already wraps every MediatR request, so it's the one place that's true by construction.
        _logger.LogInformation(
            "Stage {Stage}: {RequestName} handler started",
            $"{requestName}HandlerStarted",
            requestName);

        var response = await next();

        _logger.LogInformation(
            "Stage {Stage}: {RequestName} completed in {ElapsedMilliseconds}ms",
            $"{requestName}Completed",
            requestName,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}
