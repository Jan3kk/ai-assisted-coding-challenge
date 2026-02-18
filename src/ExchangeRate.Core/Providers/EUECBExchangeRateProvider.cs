using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Interfaces.Providers;
using ExchangeRate.Core.Models;
using ExchangeRateEntity = ExchangeRate.Core.Entities.ExchangeRate;

namespace ExchangeRate.Core.Providers
{
    /// <summary>
    /// European Central Bank (ECB) exchange rate provider.
    /// Provides both daily and monthly EUR exchange rates.
    /// Delegates to shared base class methods to avoid duplicating fetch/chunking logic.
    /// </summary>
    public class EUECBExchangeRateProvider : ExternalApiExchangeRateProvider, IDailyExchangeRateProvider, IMonthlyExchangeRateProvider
    {
        public EUECBExchangeRateProvider(HttpClient httpClient, ExternalExchangeRateApiConfig externalExchangeRateApiConfig)
            : base(httpClient, externalExchangeRateApiConfig)
        {
        }

        public override CurrencyTypes Currency => CurrencyTypes.EUR;
        public override QuoteTypes QuoteType => QuoteTypes.Indirect;
        public override ExchangeRateSources Source => ExchangeRateSources.ECB;
        public override string BankId => "EUECB";

        public async Task<IEnumerable<ExchangeRateEntity>> GetHistoricalDailyFxRatesAsync(DateTime from, DateTime to)
        {
            if (to < from)
                throw new ArgumentException("to must be later than or equal to from");

            var results = new List<ExchangeRateEntity>();
            foreach (var period in DailyExternalApiExchangeRateProvider.GetDateRange(from, to, DailyExternalApiExchangeRateProvider.MaxQueryIntervalInDays))
            {
                var rates = await GetDailyRatesAsync(BankId, (period.StartDate, period.EndDate));
                results.AddRange(rates);
            }
            return results;
        }

        public async Task<IEnumerable<ExchangeRateEntity>> GetDailyFxRatesAsync()
        {
            return await GetHistoricalDailyFxRatesAsync(DateTime.UtcNow.Date.AddDays(-4), DateTime.UtcNow.Date);
        }

        public async Task<IEnumerable<ExchangeRateEntity>> GetHistoricalMonthlyFxRatesAsync(DateTime from, DateTime to)
        {
            if (to < from)
                throw new ArgumentException("to must be later than or equal to from");

            var start = new DateTime(from.Year, from.Month, 1);
            var end = new DateTime(to.Year, to.Month, 1);
            var results = new List<ExchangeRateEntity>();

            var date = start;
            while (date <= end)
            {
                var rates = await GetMonthlyRatesAsync(BankId, (date.Year, date.Month));
                results.AddRange(rates);
                date = date.AddMonths(1);
            }
            return results;
        }

        public async Task<IEnumerable<ExchangeRateEntity>> GetMonthlyFxRatesAsync()
        {
            return await GetMonthlyRatesAsync(BankId);
        }
    }
}
