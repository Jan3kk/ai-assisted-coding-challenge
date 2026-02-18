using ExchangeRateEntity = ExchangeRate.Domain.Entities.ExchangeRate;

namespace ExchangeRate.Application.Ports;

public interface IBiWeeklyExchangeRateProvider : IExchangeRateProvider
{
    Task<IEnumerable<ExchangeRateEntity>> GetBiWeeklyFxRatesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ExchangeRateEntity>> GetHistoricalBiWeeklyFxRatesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
