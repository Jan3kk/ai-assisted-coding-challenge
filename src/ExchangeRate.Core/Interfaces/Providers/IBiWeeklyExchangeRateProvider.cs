using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExchangeRateEntity = ExchangeRate.Core.Entities.ExchangeRate;

namespace ExchangeRate.Core.Interfaces.Providers
{
    public interface IBiWeeklyExchangeRateProvider : IExchangeRateProvider
    {
        Task<IEnumerable<ExchangeRateEntity>> GetBiWeeklyFxRatesAsync();

        Task<IEnumerable<ExchangeRateEntity>> GetHistoricalBiWeeklyFxRatesAsync(DateTime from, DateTime to);
    }
}
