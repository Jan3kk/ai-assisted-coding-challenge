#nullable enable
using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Domain;

/// <summary>
/// Port for exchange rate data persistence.
/// </summary>
public interface IExchangeRateDataStore
{
    /// <summary>
    /// Gets exchange rates filtered by date range.
    /// </summary>
    Task<List<Entities.ExchangeRate>> GetExchangeRatesAsync(DateTime minDate, DateTime maxDate);

    /// <summary>
    /// Saves exchange rates to the data store with upsert semantics.
    /// If a rate already exists for the same date/currency/source/frequency, it is updated.
    /// </summary>
    Task SaveExchangeRatesAsync(IEnumerable<Entities.ExchangeRate> rates);

    /// <summary>
    /// Gets all pegged currency configurations.
    /// </summary>
    List<Entities.PeggedCurrency> GetPeggedCurrencies();
}
