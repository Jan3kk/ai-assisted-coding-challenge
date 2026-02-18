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
    public abstract class MonthlyExternalApiExchangeRateProvider : ExternalApiExchangeRateProvider, IMonthlyExchangeRateProvider
    {
        protected MonthlyExternalApiExchangeRateProvider(HttpClient httpClient, ExternalExchangeRateApiConfig externalExchangeRateApiConfig)
            : base(httpClient, externalExchangeRateApiConfig)
        {
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
