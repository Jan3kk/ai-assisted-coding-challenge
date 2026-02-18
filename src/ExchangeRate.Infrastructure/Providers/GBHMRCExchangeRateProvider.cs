using ExchangeRate.Application.Configuration;
using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Infrastructure.Providers;

public sealed class GBHMRCExchangeRateProvider : MonthlyExternalApiExchangeRateProvider
{
    public GBHMRCExchangeRateProvider(HttpClient httpClient, ExternalExchangeRateApiConfig config)
        : base(httpClient, config) { }

    public override CurrencyTypes Currency => CurrencyTypes.GBP;
    public override QuoteTypes QuoteType => QuoteTypes.Indirect;
    public override ExchangeRateSources Source => ExchangeRateSources.HMRC;
    public override string BankId => "GBHMRC";
}
