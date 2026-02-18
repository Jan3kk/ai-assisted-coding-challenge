using ExchangeRate.Application.Configuration;
using ExchangeRate.Application.Ports;
using ExchangeRate.Domain.Entities;

namespace ExchangeRate.Infrastructure.Providers;

public abstract class MonthlyExternalApiExchangeRateProvider : ExternalApiExchangeRateProvider, IMonthlyExchangeRateProvider
{
    protected MonthlyExternalApiExchangeRateProvider(HttpClient httpClient, ExternalExchangeRateApiConfig config)
        : base(httpClient, config) { }

    public async Task<IEnumerable<Domain.Entities.ExchangeRate>> GetHistoricalMonthlyFxRatesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        if (to < from)
            throw new ArgumentException("to must be later than or equal to from");

        var start = new DateTime(from.Year, from.Month, 1);
        var end = new DateTime(to.Year, to.Month, 1);
        var results = new List<Domain.Entities.ExchangeRate>();

        for (var date = start; date <= end; date = date.AddMonths(1))
        {
            var rates = await GetMonthlyRatesAsync(BankId, (date.Year, date.Month), cancellationToken).ConfigureAwait(false);
            results.AddRange(rates);
        }
        return results;
    }

    public async Task<IEnumerable<Domain.Entities.ExchangeRate>> GetMonthlyFxRatesAsync(CancellationToken cancellationToken = default)
    {
        return await GetMonthlyRatesAsync(BankId, default, cancellationToken).ConfigureAwait(false);
    }
}
