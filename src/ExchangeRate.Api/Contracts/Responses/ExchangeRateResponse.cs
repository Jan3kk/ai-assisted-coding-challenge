namespace ExchangeRate.Api.Contracts.Responses;

/// <summary>
/// Response model for the exchange rate API endpoint.
/// </summary>
public record ExchangeRateResponse(
    string FromCurrency,
    string ToCurrency,
    DateTime Date,
    string Source,
    string Frequency,
    decimal Rate);
