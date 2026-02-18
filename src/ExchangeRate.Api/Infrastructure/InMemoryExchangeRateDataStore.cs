using ExchangeRate.Core.Infrastructure;
using ExchangeRateEntity = ExchangeRate.Core.Entities.ExchangeRate;
using PeggedCurrency = ExchangeRate.Core.Entities.PeggedCurrency;

namespace ExchangeRate.Api.Infrastructure;

/// <summary>
/// In-memory implementation of IExchangeRateDataStore.
/// Candidates can replace this with a real database implementation (e.g., EF Core).
/// </summary>
public class InMemoryExchangeRateDataStore : IExchangeRateDataStore
{
    private readonly List<ExchangeRateEntity> _exchangeRates = new();
    private readonly List<PeggedCurrency> _peggedCurrencies = new();

    public Task<List<ExchangeRateEntity>> GetExchangeRatesAsync(DateTime minDate, DateTime maxDate)
    {
        var rates = _exchangeRates
            .Where(r => r.Date >= minDate && r.Date < maxDate)
            .ToList();

        return Task.FromResult(rates);
    }

    public Task SaveExchangeRatesAsync(IEnumerable<ExchangeRateEntity> rates)
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
