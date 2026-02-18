using ExchangeRate.Domain;
using ExchangeRate.Domain.Enums;
using ExchangeRate.Domain.Errors;
using ExchangeRate.Domain.Exceptions;
using ExchangeRate.Domain.Helpers;
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
        ILogger<ExchangeRateRepositoryFacade>? logger = null)
    {
        _storedRepository = storedRepository ?? throw new ArgumentNullException(nameof(storedRepository));
        _peggedCurrencyRepository = peggedCurrencyRepository ?? throw new ArgumentNullException(nameof(peggedCurrencyRepository));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _rateQuoteService = rateQuoteService ?? throw new ArgumentNullException(nameof(rateQuoteService));
        _logger = logger;
    }

    public async Task<decimal?> GetRateAsync(CurrencyTypes fromCurrency, CurrencyTypes toCurrency, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
    {
        var provider = _providerFactory.GetExchangeRateProvider(source);

        if (toCurrency == fromCurrency)
            return 1m;

        date = date.Date;
        await EnsureRatesLoadedAsync(date, source, frequency).ConfigureAwait(false);

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
            await FetchRatesForDateAsync(provider, date, source, frequency).ConfigureAwait(false);

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

    public async Task UpdateRatesAsync()
    {
        foreach (var source in _providerFactory.ListExchangeRateSources())
        {
            try
            {
                var provider = _providerFactory.GetExchangeRateProvider(source);
                var rates = await FetchLatestRatesAsync(provider).ConfigureAwait(false);

                if (rates.Count > 0)
                    await SaveFetchedRatesAsync(rates).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to update rates for {Source}", source);
            }
        }
    }

    public async Task<bool> EnsureMinimumDateRangeAsync(DateTime minDate, IEnumerable<ExchangeRateSources>? exchangeRateSources = null)
    {
        var success = true;
        foreach (var source in exchangeRateSources ?? _providerFactory.ListExchangeRateSources())
        {
            var provider = _providerFactory.GetExchangeRateProvider(source);

            foreach (var f in GetSupportedFrequencies(provider))
            {
                if (!await EnsureMinimumDateRangeAsync(minDate, source, f).ConfigureAwait(false))
                    success = false;
            }
        }
        return success;
    }

    private async Task EnsureRatesLoadedAsync(DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
    {
        var rates = await _storedRepository.GetAsync(DateTime.MinValue, DateTime.UtcNow.Date.AddDays(1), source, frequency).ConfigureAwait(false);
        var minFxDate = rates.Count > 0 ? rates.Min(r => r.Date) : DateTime.MaxValue;

        if (PeriodHelper.GetStartOfMonth(minFxDate) > date)
            await EnsureMinimumDateRangeAsync(PeriodHelper.GetStartOfMonth(date.AddMonths(-1)), source, frequency).ConfigureAwait(false);
    }

    private async Task<bool> EnsureMinimumDateRangeAsync(DateTime minDate, ExchangeRateSources source, ExchangeRateFrequencies frequency)
    {
        minDate = PeriodHelper.GetStartOfMonth(minDate);

        var rates = await _storedRepository.GetAsync(DateTime.MinValue, DateTime.UtcNow.Date.AddDays(1), source, frequency).ConfigureAwait(false);
        var rawMinFxDate = rates.Count > 0 ? rates.Min(r => r.Date) : DateTime.MaxValue;

        if (PeriodHelper.GetStartOfMonth(rawMinFxDate) <= minDate)
            return true;

        var provider = _providerFactory.GetExchangeRateProvider(source);
        var fetchTo = rawMinFxDate == DateTime.MaxValue ? DateTime.UtcNow.Date : rawMinFxDate;
        return await FetchAndSaveHistoricalRatesAsync(provider, minDate, fetchTo, source, frequency).ConfigureAwait(false);
    }

    private async Task FetchRatesForDateAsync(IExchangeRateProvider provider, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
    {
        await FetchAndSaveHistoricalRatesAsync(provider, date.AddDays(-7), date, source, frequency).ConfigureAwait(false);
    }

    private async Task<bool> FetchAndSaveHistoricalRatesAsync(IExchangeRateProvider provider, DateTime from, DateTime to, ExchangeRateSources source, ExchangeRateFrequencies frequency)
    {
        var actualFrom = from <= to ? from : to;
        var actualTo = from <= to ? to : from;

        var fetchedRates = await FetchHistoricalRatesAsync(provider, frequency, actualFrom, actualTo).ConfigureAwait(false);

        if (fetchedRates.Count == 0)
        {
            _logger?.LogError(
                "No historical data found between {From:yyyy-MM-dd} and {To:yyyy-MM-dd} for source {Source} with frequency {Frequency}.",
                actualFrom, actualTo, source, frequency);
            return false;
        }

        await SaveFetchedRatesAsync(fetchedRates).ConfigureAwait(false);
        return true;
    }

    private async Task<List<ExchangeRateEntity>> FetchHistoricalRatesAsync(IExchangeRateProvider provider, ExchangeRateFrequencies frequency, DateTime from, DateTime to)
    {
        IEnumerable<ExchangeRateEntity> rates = frequency switch
        {
            ExchangeRateFrequencies.Daily when provider is IDailyExchangeRateProvider daily
                => await daily.GetHistoricalDailyFxRatesAsync(from, to).ConfigureAwait(false),
            ExchangeRateFrequencies.Monthly when provider is IMonthlyExchangeRateProvider monthly
                => await monthly.GetHistoricalMonthlyFxRatesAsync(from, to).ConfigureAwait(false),
            ExchangeRateFrequencies.Weekly when provider is IWeeklyExchangeRateProvider weekly
                => await weekly.GetHistoricalWeeklyFxRatesAsync(from, to).ConfigureAwait(false),
            ExchangeRateFrequencies.BiWeekly when provider is IBiWeeklyExchangeRateProvider biweekly
                => await biweekly.GetHistoricalBiWeeklyFxRatesAsync(from, to).ConfigureAwait(false),
            _ => throw new ExchangeRateException($"Provider {provider.Source} does not support frequency {frequency}")
        };
        return rates.ToList();
    }

    private static async Task<List<ExchangeRateEntity>> FetchLatestRatesAsync(IExchangeRateProvider provider)
    {
        var list = new List<ExchangeRateEntity>();

        if (provider is IDailyExchangeRateProvider daily)
            list.AddRange(await daily.GetDailyFxRatesAsync().ConfigureAwait(false));
        if (provider is IMonthlyExchangeRateProvider monthly)
            list.AddRange(await monthly.GetMonthlyFxRatesAsync().ConfigureAwait(false));
        if (provider is IWeeklyExchangeRateProvider weekly)
            list.AddRange(await weekly.GetWeeklyFxRatesAsync().ConfigureAwait(false));
        if (provider is IBiWeeklyExchangeRateProvider biWeekly)
            list.AddRange(await biWeekly.GetBiWeeklyFxRatesAsync().ConfigureAwait(false));

        return list;
    }

    private async Task SaveFetchedRatesAsync(List<ExchangeRateEntity> rates)
    {
        await _storedRepository.SaveAsync(rates).ConfigureAwait(false);
    }

    private static (IReadOnlyDictionary<CurrencyTypes, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrencyAndDate, DateTime minFxDate) BuildRateSurface(IReadOnlyList<ExchangeRateEntity> rates)
    {
        var byCurrencyAndDate = new Dictionary<CurrencyTypes, Dictionary<DateTime, decimal>>();
        var minFxDate = DateTime.MaxValue;

        foreach (var r in rates)
        {
            if (!byCurrencyAndDate.TryGetValue(r.CurrencyId, out var byDate))
                byCurrencyAndDate[r.CurrencyId] = byDate = new Dictionary<DateTime, decimal>();
            byDate[r.Date] = r.Rate;
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

    private static IEnumerable<ExchangeRateFrequencies> GetSupportedFrequencies(IExchangeRateProvider provider)
    {
        if (provider is IDailyExchangeRateProvider) yield return ExchangeRateFrequencies.Daily;
        if (provider is IMonthlyExchangeRateProvider) yield return ExchangeRateFrequencies.Monthly;
        if (provider is IWeeklyExchangeRateProvider) yield return ExchangeRateFrequencies.Weekly;
        if (provider is IBiWeeklyExchangeRateProvider) yield return ExchangeRateFrequencies.BiWeekly;
    }
}
