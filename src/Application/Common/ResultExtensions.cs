using Kart.Shared.Domain;

namespace KartOfferService.Application.Common;

/// <summary>Small `Result&lt;T&gt;` combinators `Kart.Shared.Domain.Result` doesn't itself provide.</summary>
public static class ResultExtensions
{
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map) =>
        result.IsSuccess ? Result.Success(map(result.Value)) : Result.Failure<TOut>(result.Error);
}
