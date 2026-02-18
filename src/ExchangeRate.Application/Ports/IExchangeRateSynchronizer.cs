using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Application.Ports;

/// <summary>
/// Responsible for ensuring exchange rate data is available (on-demand and bulk synchronization).
/// Fetches from external providers and persists via the stored repository.
/// </summary>
public interface IExchangeRateSynchronizer
{
    /// <summary>
    /// Ensures rates are loaded so that a quote for the given date/source/frequency can be resolved
    /// (e.g. by backfilling historical data if the stored minimum date is after the requested date).
    /// </summary>
    Task EnsureRatesLoadedAsync(DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency);

    /// <summary>
    /// Ensures the stored repository has rates from all (or specified) sources and frequencies
    /// covering at least from <paramref name="minDate"/> onward.
    /// </summary>
    Task<bool> EnsureMinimumDateRangeAsync(DateTime minDate, IEnumerable<ExchangeRateSources>? exchangeRateSources = null);

    /// <summary>
    /// Fetches and saves rates for a specific date (on-demand when a quote was not found).
    /// </summary>
    Task EnsureRatesForDateAsync(DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency);

    /// <summary>
    /// Fetches the latest rates from all registered providers and saves them.
    /// </summary>
    Task UpdateRatesAsync();
}
