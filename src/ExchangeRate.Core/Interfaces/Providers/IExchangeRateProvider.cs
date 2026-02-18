using ExchangeRate.Core.Enums;

namespace ExchangeRate.Core.Interfaces.Providers;

public interface IExchangeRateProvider
{
    CurrencyTypes Currency { get; }

    QuoteTypes QuoteType { get; }

    ExchangeRateSources Source { get; }
}
