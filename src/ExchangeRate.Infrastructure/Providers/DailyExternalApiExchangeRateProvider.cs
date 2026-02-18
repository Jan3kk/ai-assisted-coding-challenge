using ExchangeRate.Application.Configuration;
using ExchangeRate.Application.Ports;
using ExchangeRate.Domain.Entities;

namespace ExchangeRate.Infrastructure.Providers;

public abstract class DailyExternalApiExchangeRateProvider : ExternalApiExchangeRateProvider, IDailyExchangeRateProvider
{
    public const int MaxQueryIntervalInDays = 180;

    protected DailyExternalApiExchangeRateProvider(HttpClient httpClient, ExternalExchangeRateApiConfig config)
        : base(httpClient, config) { }

    public virtual async Task<IEnumerable<Domain.Entities.ExchangeRate>> GetHistoricalDailyFxRatesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        if (to < from)
            throw new ArgumentException("to must be later than or equal to from");

        var results = new List<Domain.Entities.ExchangeRate>();
        foreach (var (start, end) in GetDateRange(from, to, MaxQueryIntervalInDays))
        {
            var rates = await GetDailyRatesAsync(BankId, (start, end), cancellationToken).ConfigureAwait(false);
            results.AddRange(rates);
        }
        return results;
    }

    public virtual async Task<IEnumerable<Domain.Entities.ExchangeRate>> GetDailyFxRatesAsync(CancellationToken cancellationToken = default)
    {
        return await GetHistoricalDailyFxRatesAsync(DateTime.UtcNow.Date.AddDays(-4), DateTime.UtcNow.Date, cancellationToken).ConfigureAwait(false);
    }

    public static IEnumerable<(DateTime StartDate, DateTime EndDate)> GetDateRange(DateTime startDate, DateTime endDate, int daysChunkSize)
    {
        DateTime markerDate;
        while ((markerDate = startDate.AddDays(daysChunkSize)) < endDate)
        {
            yield return (startDate, markerDate.AddDays(-1));
            startDate = markerDate;
        }
        yield return (startDate, endDate);
    }
}
