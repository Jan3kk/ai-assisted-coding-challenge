using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Application.Ports;

public interface IExchangeRateProvider
{
    CurrencyTypes Currency { get; }
    QuoteTypes QuoteType { get; }
    ExchangeRateSources Source { get; }
}
