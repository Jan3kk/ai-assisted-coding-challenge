using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Application.Ports;

public interface IExchangeRateProviderFactory
{
    IExchangeRateProvider GetExchangeRateProvider(ExchangeRateSources source);
    IEnumerable<ExchangeRateSources> ListExchangeRateSources();
}
