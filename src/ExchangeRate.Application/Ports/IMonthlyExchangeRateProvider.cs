using ExchangeRateEntity = ExchangeRate.Domain.Entities.ExchangeRate;

namespace ExchangeRate.Application.Ports;

public interface IMonthlyExchangeRateProvider : IExchangeRateProvider
{
    Task<IEnumerable<ExchangeRateEntity>> GetMonthlyFxRatesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ExchangeRateEntity>> GetHistoricalMonthlyFxRatesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
