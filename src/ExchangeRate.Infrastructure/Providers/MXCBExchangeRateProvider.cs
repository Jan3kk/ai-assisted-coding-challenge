using ExchangeRate.Application.Configuration;
using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Infrastructure.Providers;

public sealed class MXCBExchangeRateProvider : MonthlyExternalApiExchangeRateProvider
{
    public MXCBExchangeRateProvider(HttpClient httpClient, ExternalExchangeRateApiConfig config)
        : base(httpClient, config) { }

    public override CurrencyTypes Currency => CurrencyTypes.MXN;
    public override QuoteTypes QuoteType => QuoteTypes.Direct;
    public override ExchangeRateSources Source => ExchangeRateSources.MXCB;
    public override string BankId => "MXCB";
}
