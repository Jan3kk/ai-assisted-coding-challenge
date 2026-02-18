using ExchangeRate.Api.Contracts.Responses;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ExchangeRate.Api.Endpoints;

/// <summary>
/// Minimal API endpoint mappings for exchange rate operations.
/// </summary>
public static class ExchangeRateEndpoints
{
    private const string GroupName = "Exchange Rates";
    private const string RoutePrefix = "rates";

    /// <summary>
    /// Maps exchange rate endpoints with a common prefix and OpenAPI metadata.
    /// </summary>
    public static IEndpointRouteBuilder MapExchangeRateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup($"/api/{RoutePrefix}")
            .WithTags(GroupName)
            .WithName("ExchangeRates");

        group.MapGet("/", GetRate)
            .WithName("GetRate")
            .WithSummary("Get exchange rate")
            .WithDescription("Returns the exchange rate from one currency to another for a given date, source, and frequency. Returns 404 with a problem details payload when no rate is found.")
            .Produces<ExchangeRateResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> GetRate(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] DateTime date,
        [FromQuery] ExchangeRateSources source,
        [FromQuery] ExchangeRateFrequencies frequency,
        IExchangeRateRepository repository)
    {
        var rate = await repository.GetRateAsync(from, to, date, source, frequency);

        if (rate == null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Exchange rate not found",
                detail: $"No exchange rate found for {from} to {to} on {date:yyyy-MM-dd}",
                type: "https://tools.ietf.org/html/rfc7231#section-6.5.4");
        }

        return Results.Ok(new ExchangeRateResponse(
            from,
            to,
            date,
            source.ToString(),
            frequency.ToString(),
            rate.Value));
    }
}
