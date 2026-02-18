using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ExchangeRate.Core.Interfaces.Providers;
using ExchangeRate.Core.Models;
using ExchangeRateEntity = ExchangeRate.Core.Entities.ExchangeRate;

namespace ExchangeRate.Core.Providers
{
    public abstract class DailyExternalApiExchangeRateProvider : ExternalApiExchangeRateProvider, IDailyExchangeRateProvider
    {
        public const int MaxQueryIntervalInDays = 180;

        protected DailyExternalApiExchangeRateProvider(HttpClient httpClient, ExternalExchangeRateApiConfig externalExchangeRateApiConfig)
            : base(httpClient, externalExchangeRateApiConfig)
        {
        }

        public virtual async Task<IEnumerable<ExchangeRateEntity>> GetHistoricalDailyFxRatesAsync(DateTime from, DateTime to)
        {
            if (to < from)
                throw new ArgumentException("to must be later than or equal to from");

            var results = new List<ExchangeRateEntity>();
            foreach (var period in GetDateRange(from, to, MaxQueryIntervalInDays))
            {
                var rates = await GetDailyRatesAsync(BankId, (period.StartDate, period.EndDate));
                results.AddRange(rates);
            }
            return results;
        }

        public virtual async Task<IEnumerable<ExchangeRateEntity>> GetDailyFxRatesAsync()
        {
            return await GetHistoricalDailyFxRatesAsync(DateTime.UtcNow.Date.AddDays(-4), DateTime.UtcNow.Date);
        }

        public static IEnumerable<(DateTime StartDate, DateTime EndDate)> GetDateRange(DateTime startDate, DateTime endDate, int daysChunkSize)
        {
            DateTime markerDate;

            while ((markerDate = startDate.AddDays(daysChunkSize)) < endDate)
            {
                yield return (StartDate: startDate, EndDate: markerDate.AddDays(-1));
                startDate = markerDate;
            }

            yield return (StartDate: startDate, EndDate: endDate);
        }
    }
}
