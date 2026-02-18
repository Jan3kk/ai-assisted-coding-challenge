using ExchangeRate.Application.Configuration;
using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Infrastructure.Providers;

public sealed class PLCBExchangeRateProvider : DailyExternalApiExchangeRateProvider
{
    public PLCBExchangeRateProvider(HttpClient httpClient, ExternalExchangeRateApiConfig config)
        : base(httpClient, config) { }

    public override CurrencyTypes Currency => CurrencyTypes.PLN;
    public override QuoteTypes QuoteType => QuoteTypes.Direct;
    public override ExchangeRateSources Source => ExchangeRateSources.PLCB;
    public override string BankId => "PLCB";
}
