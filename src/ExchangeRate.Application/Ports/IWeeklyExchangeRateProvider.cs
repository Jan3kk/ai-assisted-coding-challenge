using ExchangeRateEntity = ExchangeRate.Domain.Entities.ExchangeRate;

namespace ExchangeRate.Application.Ports;

public interface IWeeklyExchangeRateProvider : IExchangeRateProvider
{
    Task<IEnumerable<ExchangeRateEntity>> GetWeeklyFxRatesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ExchangeRateEntity>> GetHistoricalWeeklyFxRatesAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
