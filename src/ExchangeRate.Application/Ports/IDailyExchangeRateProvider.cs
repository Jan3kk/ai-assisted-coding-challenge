using ExchangeRateEntity = ExchangeRate.Domain.Entities.ExchangeRate;

namespace ExchangeRate.Application.Ports;

public interface IDailyExchangeRateProvider : IExchangeRateProvider
{
    Task<IEnumerable<ExchangeRateEntity>> GetDailyFxRatesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ExchangeRateEntity>> GetHistoricalDailyFxRatesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
