using ExchangeRate.Domain;
using ExchangeRate.Domain.Entities;

namespace ExchangeRate.Api.Infrastructure;

/// <summary>
/// In-memory implementation of IExchangeRateDataStore.
/// </summary>
public class InMemoryExchangeRateDataStore : IExchangeRateDataStore
{
    private readonly List<ExchangeRate.Domain.Entities.ExchangeRate> _exchangeRates = new();
    private readonly List<PeggedCurrency> _peggedCurrencies = new();

    public Task<List<ExchangeRate.Domain.Entities.ExchangeRate>> GetExchangeRatesAsync(DateTime minDate, DateTime maxDate)
    {
        var rates = _exchangeRates
            .Where(r => r.Date >= minDate && r.Date < maxDate)
            .ToList();

        return Task.FromResult(rates);
    }

    public Task SaveExchangeRatesAsync(IEnumerable<ExchangeRate.Domain.Entities.ExchangeRate> rates)
    {
        foreach (var rate in rates)
        {
            var existingIndex = _exchangeRates.FindIndex(r =>
                r.Date == rate.Date &&
                r.CurrencyId == rate.CurrencyId &&
                r.Source == rate.Source &&
                r.Frequency == rate.Frequency);

            if (existingIndex >= 0)
                _exchangeRates[existingIndex] = rate;
            else
                _exchangeRates.Add(rate);
        }

        return Task.CompletedTask;
    }

    public List<PeggedCurrency> GetPeggedCurrencies()
    {
        return _peggedCurrencies.ToList();
    }

    public void AddPeggedCurrency(PeggedCurrency peggedCurrency)
    {
        _peggedCurrencies.Add(peggedCurrency);
    }
}
