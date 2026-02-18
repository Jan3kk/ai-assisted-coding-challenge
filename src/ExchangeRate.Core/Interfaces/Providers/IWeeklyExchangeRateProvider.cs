using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExchangeRateEntity = ExchangeRate.Core.Entities.ExchangeRate;

namespace ExchangeRate.Core.Interfaces.Providers
{
    public interface IWeeklyExchangeRateProvider : IExchangeRateProvider
    {
        Task<IEnumerable<ExchangeRateEntity>> GetWeeklyFxRatesAsync();

        Task<IEnumerable<ExchangeRateEntity>> GetHistoricalWeeklyFxRatesAsync(DateTime from, DateTime to);
    }
}
