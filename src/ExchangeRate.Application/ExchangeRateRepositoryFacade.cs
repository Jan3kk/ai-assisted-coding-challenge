using ExchangeRate.Domain;
using ExchangeRate.Domain.Enums;
using ExchangeRate.Domain.Errors;
using ExchangeRate.Domain.Exceptions;
using ExchangeRate.Domain.Services;
using ExchangeRate.Application.Ports;
using Microsoft.Extensions.Logging;
using ExchangeRateEntity = ExchangeRate.Domain.Entities.ExchangeRate;

namespace ExchangeRate.Application;

public sealed class ExchangeRateRepositoryFacade : IExchangeRateRepository
{
    private static readonly Dictionary<string, CurrencyTypes> CurrencyMapping;

    private readonly IStoredExchangeRateRepository _storedRepository;
    private readonly IPeggedCurrencyRepository _peggedCurrencyRepository;
    private readonly IExchangeRateProviderFactory _providerFactory;
    private readonly IRateQuoteService _rateQuoteService;
    private readonly IExchangeRateSynchronizer _synchronizer;
    private readonly ILogger<ExchangeRateRepositoryFacade>? _logger;

    static ExchangeRateRepositoryFacade()
    {
        var currencies = Enum.GetValues(typeof(CurrencyTypes)).Cast<CurrencyTypes>().ToList();
        CurrencyMapping = currencies.ToDictionary(x => x.ToString().ToUpperInvariant());
    }

    public ExchangeRateRepositoryFacade(
        IStoredExchangeRateRepository storedRepository,
        IPeggedCurrencyRepository peggedCurrencyRepository,
        IExchangeRateProviderFactory providerFactory,
        IRateQuoteService rateQuoteService,
        IExchangeRateSynchronizer synchronizer,
        ILogger<ExchangeRateRepositoryFacade>? logger = null)
    {
        _storedRepository = storedRepository ?? throw new ArgumentNullException(nameof(storedRepository));
        _peggedCurrencyRepository = peggedCurrencyRepository ?? throw new ArgumentNullException(nameof(peggedCurrencyRepository));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _rateQuoteService = rateQuoteService ?? throw new ArgumentNullException(nameof(rateQuoteService));
        _synchronizer = synchronizer ?? throw new ArgumentNullException(nameof(synchronizer));
        _logger = logger;
    }

    public async Task<decimal?> GetRateAsync(CurrencyTypes fromCurrency, CurrencyTypes toCurrency, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
    {
        var provider = _providerFactory.GetExchangeRateProvider(source);

        if (toCurrency == fromCurrency)
            return 1m;

        date = date.Date;
        await _synchronizer.EnsureRatesLoadedAsync(date, source, frequency).ConfigureAwait(false);

        if (fromCurrency != provider.Currency && toCurrency != provider.Currency)
        {
            var leg1 = await GetRateAsync(fromCurrency, provider.Currency, date, source, frequency).ConfigureAwait(false);
            var leg2 = await GetRateAsync(provider.Currency, toCurrency, date, source, frequency).ConfigureAwait(false);
            return leg1 * leg2;
        }

        var rates = await _storedRepository.GetAsync(DateTime.MinValue, DateTime.UtcNow.Date.AddDays(1), source, frequency).ConfigureAwait(false);
        var (ratesByCurrencyAndDate, minFxDate) = BuildRateSurface(rates);
        var peggedCurrencies = _peggedCurrencyRepository.GetAll().ToDictionary(x => x.CurrencyId);

        var result = _rateQuoteService.GetQuote(
            ratesByCurrencyAndDate,
            date,
            minFxDate,
            provider.Currency,
            provider.QuoteType,
            peggedCurrencies,
            fromCurrency,
            toCurrency,
            out _);

        if (result.IsSuccess)
            return result.Value;

        if (result.Errors.FirstOrDefault() is NoFxRateFoundError)
        {
            await _synchronizer.EnsureRatesForDateAsync(date, source, frequency).ConfigureAwait(false);

            rates = await _storedRepository.GetAsync(DateTime.MinValue, DateTime.UtcNow.Date.AddDays(1), source, frequency).ConfigureAwait(false);
            (ratesByCurrencyAndDate, minFxDate) = BuildRateSurface(rates);
            result = _rateQuoteService.GetQuote(
                ratesByCurrencyAndDate,
                date,
                minFxDate,
                provider.Currency,
                provider.QuoteType,
                peggedCurrencies,
                fromCurrency,
                toCurrency,
                out var lookupCurrency);

            if (result.IsSuccess)
                return result.Value;

            _logger?.LogError(
                "No {Source} {Frequency} exchange rate found for {LookupCurrency} on {Date:yyyy-MM-dd}. FromCurrency: {FromCurrency}, ToCurrency: {ToCurrency}",
                source, frequency, lookupCurrency, date, fromCurrency, toCurrency);
        }

        return null;
    }

    public async Task<decimal?> GetRateAsync(string fromCurrencyCode, string toCurrencyCode, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
    {
        var fromCurrency = ParseCurrencyCode(fromCurrencyCode);
        var toCurrency = ParseCurrencyCode(toCurrencyCode);
        return await GetRateAsync(fromCurrency, toCurrency, date, source, frequency).ConfigureAwait(false);
    }

    public Task UpdateRatesAsync() => _synchronizer.UpdateRatesAsync();

    public Task<bool> EnsureMinimumDateRangeAsync(DateTime minDate, IEnumerable<ExchangeRateSources>? exchangeRateSources = null) =>
        _synchronizer.EnsureMinimumDateRangeAsync(minDate, exchangeRateSources);

    private static (IReadOnlyDictionary<CurrencyTypes, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrencyAndDate, DateTime minFxDate) BuildRateSurface(IReadOnlyList<ExchangeRateEntity> rates)
    {
        var byCurrencyAndDate = new Dictionary<CurrencyTypes, Dictionary<DateTime, decimal>>();
        var minFxDate = DateTime.MaxValue;

        foreach (var r in rates)
        {
            if (!byCurrencyAndDate.TryGetValue(r.CurrencyId, out var byDate))
                byCurrencyAndDate[r.CurrencyId] = byDate = new Dictionary<DateTime, decimal>();
            byDate[r.Date] = r.Rate.Rounded;
            if (r.Date < minFxDate)
                minFxDate = r.Date;
        }

        var readOnly = byCurrencyAndDate.ToDictionary(
            x => x.Key,
            x => (IReadOnlyDictionary<DateTime, decimal>)x.Value);
        return (readOnly, rates.Count > 0 ? minFxDate : DateTime.MaxValue);
    }

    private static CurrencyTypes ParseCurrencyCode(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            throw new ExchangeRateException("Null or empty currency code.");

        if (!CurrencyMapping.TryGetValue(currencyCode.ToUpperInvariant(), out var currency))
            throw new ExchangeRateException("Not supported currency code: " + currencyCode);

        return currency;
    }
}
