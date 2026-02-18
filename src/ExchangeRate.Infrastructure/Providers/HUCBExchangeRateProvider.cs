using ExchangeRate.Application.Configuration;
using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Infrastructure.Providers;

public sealed class HUCBExchangeRateProvider : DailyExternalApiExchangeRateProvider
{
    public HUCBExchangeRateProvider(HttpClient httpClient, ExternalExchangeRateApiConfig config)
        : base(httpClient, config) { }

    public override CurrencyTypes Currency => CurrencyTypes.HUF;
    public override QuoteTypes QuoteType => QuoteTypes.Direct;
    public override ExchangeRateSources Source => ExchangeRateSources.MNB;
    public override string BankId => "HUCB";
}
