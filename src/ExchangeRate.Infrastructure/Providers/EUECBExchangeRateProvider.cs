using ExchangeRate.Application.Configuration;
using ExchangeRate.Application.Ports;
using ExchangeRate.Domain.Entities;
using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Infrastructure.Providers;

public sealed class EUECBExchangeRateProvider : ExternalApiExchangeRateProvider, IDailyExchangeRateProvider, IMonthlyExchangeRateProvider
{
    public EUECBExchangeRateProvider(HttpClient httpClient, ExternalExchangeRateApiConfig config)
        : base(httpClient, config) { }

    public override CurrencyTypes Currency => CurrencyTypes.EUR;
    public override QuoteTypes QuoteType => QuoteTypes.Indirect;
    public override ExchangeRateSources Source => ExchangeRateSources.ECB;
    public override string BankId => "EUECB";

    public async Task<IEnumerable<Domain.Entities.ExchangeRate>> GetHistoricalDailyFxRatesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        if (to < from)
            throw new ArgumentException("to must be later than or equal to from");

        var results = new List<Domain.Entities.ExchangeRate>();
        foreach (var (start, end) in DailyExternalApiExchangeRateProvider.GetDateRange(from, to, DailyExternalApiExchangeRateProvider.MaxQueryIntervalInDays))
        {
            var rates = await GetDailyRatesAsync(BankId, (start, end), cancellationToken).ConfigureAwait(false);
            results.AddRange(rates);
        }
        return results;
    }

    public async Task<IEnumerable<Domain.Entities.ExchangeRate>> GetDailyFxRatesAsync(CancellationToken cancellationToken = default)
    {
        return await GetHistoricalDailyFxRatesAsync(DateTime.UtcNow.Date.AddDays(-4), DateTime.UtcNow.Date, cancellationToken).ConfigureAwait(false);
    }

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
