using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExchangeRateEntity = ExchangeRate.Core.Entities.ExchangeRate;

namespace ExchangeRate.Core.Interfaces.Providers
{
    public interface IDailyExchangeRateProvider : IExchangeRateProvider
    {
        Task<IEnumerable<ExchangeRateEntity>> GetDailyFxRatesAsync();

        Task<IEnumerable<ExchangeRateEntity>> GetHistoricalDailyFxRatesAsync(DateTime from, DateTime to);
    }
}
