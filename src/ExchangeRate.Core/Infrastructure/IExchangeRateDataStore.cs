#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExchangeRate.Core.Entities;

namespace ExchangeRate.Core.Infrastructure;

/// <summary>
/// Abstraction for exchange rate data persistence.
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
    List<PeggedCurrency> GetPeggedCurrencies();
}
