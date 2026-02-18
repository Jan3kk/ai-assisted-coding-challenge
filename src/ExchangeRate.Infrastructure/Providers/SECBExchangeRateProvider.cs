using ExchangeRate.Application.Configuration;
using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Infrastructure.Providers;

public sealed class SECBExchangeRateProvider : DailyExternalApiExchangeRateProvider
{
    public SECBExchangeRateProvider(HttpClient httpClient, ExternalExchangeRateApiConfig config)
        : base(httpClient, config) { }

    public override CurrencyTypes Currency => CurrencyTypes.SEK;
    public override QuoteTypes QuoteType => QuoteTypes.Direct;
    public override ExchangeRateSources Source => ExchangeRateSources.SECB;
    public override string BankId => "SECB";
}
