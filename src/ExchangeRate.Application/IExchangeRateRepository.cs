using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Application;

public interface IExchangeRateRepository
{
    /// <summary>
    /// Returns the exchange rate from <paramref name="fromCurrency"/> to <paramref name="toCurrency"/> on the given <paramref name="date"/>.
    /// Falls back to the most recent available rate if the exact date is not available.
    /// Returns null if no rate exists at all.
    /// </summary>
    Task<decimal?> GetRateAsync(CurrencyTypes fromCurrency, CurrencyTypes toCurrency, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency);

    /// <summary>
    /// Returns the exchange rate from <paramref name="fromCurrencyCode"/> to <paramref name="toCurrencyCode"/> on the given <paramref name="date"/>.
    /// Falls back to the most recent available rate if the exact date is not available.
    /// Returns null if no rate exists at all.
    /// </summary>
    Task<decimal?> GetRateAsync(string fromCurrencyCode, string toCurrencyCode, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency);

    /// <summary>
    /// Updates the exchange rates for the last available day/month from all registered providers.
    /// </summary>
    Task UpdateRatesAsync();

    /// <summary>
    /// Ensures that the database contains all exchange rates after <paramref name="minDate"/>.
    /// </summary>
    Task<bool> EnsureMinimumDateRangeAsync(DateTime minDate, IEnumerable<ExchangeRateSources>? exchangeRateSources = null);
}
