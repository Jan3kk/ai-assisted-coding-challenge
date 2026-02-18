using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExchangeRateEntity = ExchangeRate.Core.Entities.ExchangeRate;

namespace ExchangeRate.Core.Interfaces.Providers
{
    public interface IMonthlyExchangeRateProvider : IExchangeRateProvider
    {
        Task<IEnumerable<ExchangeRateEntity>> GetMonthlyFxRatesAsync();

        Task<IEnumerable<ExchangeRateEntity>> GetHistoricalMonthlyFxRatesAsync(DateTime from, DateTime to);
    }
}
