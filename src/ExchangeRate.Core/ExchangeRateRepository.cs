using Microsoft.Extensions.Logging;
using FluentResults;
using ExchangeRate.Core.Exceptions;
using ExchangeRate.Core.Helpers;
using ExchangeRate.Core.Interfaces;
using ExchangeRate.Core.Interfaces.Providers;
using ExchangeRate.Core.Entities;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Errors;
using ExchangeRate.Core.Infrastructure;

namespace ExchangeRate.Core
{
    public class ExchangeRateRepository : IExchangeRateRepository
    {
        private static readonly IEnumerable<ExchangeRateSources> SupportedSources =
            Enum.GetValues(typeof(ExchangeRateSources)).Cast<ExchangeRateSources>().ToList();

        private static readonly Dictionary<string, CurrencyTypes> CurrencyMapping;

        private readonly Dictionary<(ExchangeRateSources, ExchangeRateFrequencies), Dictionary<CurrencyTypes, Dictionary<DateTime, decimal>>> _rateCache;
        private Dictionary<(ExchangeRateSources, ExchangeRateFrequencies), DateTime> _minDateBySourceFrequency = null!;
        private readonly Dictionary<CurrencyTypes, PeggedCurrency> _peggedCurrencies;

        private readonly IExchangeRateDataStore? _dataStore;
        private readonly ILogger<ExchangeRateRepository>? _logger;
        private readonly IExchangeRateProviderFactory _providerFactory;

        static ExchangeRateRepository()
        {
            var currencies = Enum.GetValues(typeof(CurrencyTypes)).Cast<CurrencyTypes>().ToList();
            CurrencyMapping = currencies.ToDictionary(x => x.ToString().ToUpperInvariant());
        }

        public ExchangeRateRepository(IExchangeRateDataStore dataStore, ILogger<ExchangeRateRepository> logger, IExchangeRateProviderFactory providerFactory)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));

            _rateCache = new Dictionary<(ExchangeRateSources, ExchangeRateFrequencies), Dictionary<CurrencyTypes, Dictionary<DateTime, decimal>>>();
            InitMinDates();

            _peggedCurrencies = _dataStore.GetPeggedCurrencies()
                .ToDictionary(x => x.CurrencyId);
        }

        internal ExchangeRateRepository(IEnumerable<Entities.ExchangeRate> rates, IExchangeRateProviderFactory providerFactory)
        {
            _rateCache = new Dictionary<(ExchangeRateSources, ExchangeRateFrequencies), Dictionary<CurrencyTypes, Dictionary<DateTime, decimal>>>();
            _peggedCurrencies = new Dictionary<CurrencyTypes, PeggedCurrency>();
            InitMinDates();
            LoadRates(rates);
            _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        }

        private void InitMinDates()
        {
            _minDateBySourceFrequency = SupportedSources
                .SelectMany(s => Enum.GetValues(typeof(ExchangeRateFrequencies)).Cast<ExchangeRateFrequencies>()
                    .Select(f => (s, f)))
                .ToDictionary(x => x, _ => DateTime.MaxValue);
        }

        #region Public API

        public async Task<decimal?> GetRateAsync(CurrencyTypes fromCurrency, CurrencyTypes toCurrency, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            var provider = _providerFactory.GetExchangeRateProvider(source);

            if (toCurrency == fromCurrency)
                return 1m;

            date = date.Date;
            await EnsureRatesLoadedAsync(date, source, frequency);

            if (fromCurrency != provider.Currency && toCurrency != provider.Currency)
            {
                var leg1 = await GetRateAsync(fromCurrency, provider.Currency, date, source, frequency);
                var leg2 = await GetRateAsync(provider.Currency, toCurrency, date, source, frequency);
                return leg1 * leg2;
            }

            var minFxDate = _minDateBySourceFrequency.GetValueOrDefault((source, frequency), DateTime.MaxValue);
            var rates = GetOrCreateCurrencyCache(source, frequency);
            var result = LookupFxRate(rates, date, minFxDate, provider, fromCurrency, toCurrency, out _);

            if (result.IsSuccess)
                return result.Value;

            if (result.Errors.FirstOrDefault() is NoFxRateFoundError)
            {
                await FetchRatesForDateAsync(provider, date, source, frequency);

                minFxDate = _minDateBySourceFrequency.GetValueOrDefault((source, frequency), DateTime.MaxValue);
                rates = GetOrCreateCurrencyCache(source, frequency);
                result = LookupFxRate(rates, date, minFxDate, provider, fromCurrency, toCurrency, out var lookupCurrency);

                if (result.IsSuccess)
                    return result.Value;

                _logger?.LogError("No {source} {frequency} exchange rate found for {lookupCurrency} on {date:yyyy-MM-dd}. FromCurrency: {fromCurrency}, ToCurrency: {toCurrency}",
                    source, frequency, lookupCurrency, date, fromCurrency, toCurrency);
            }

            return null;
        }

        public async Task<decimal?> GetRateAsync(string fromCurrencyCode, string toCurrencyCode, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            var fromCurrency = ParseCurrencyCode(fromCurrencyCode);
            var toCurrency = ParseCurrencyCode(toCurrencyCode);
            return await GetRateAsync(fromCurrency, toCurrency, date, source, frequency);
        }

        public async Task UpdateRatesAsync()
        {
            foreach (var source in _providerFactory.ListExchangeRateSources())
            {
                try
                {
                    var provider = _providerFactory.GetExchangeRateProvider(source);
                    var rates = await FetchLatestRatesAsync(provider);

                    if (rates.Count > 0)
                    {
                        await LoadRatesFromDbAsync(PeriodHelper.GetStartOfMonth(rates.Min(x => x.Date)));
                        await SaveFetchedRatesAsync(rates, source);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to update rates for {source}", source);
                }
            }
        }

        public async Task<bool> EnsureMinimumDateRangeAsync(DateTime minDate, IEnumerable<ExchangeRateSources>? exchangeRateSources = null)
        {
            var success = true;
            foreach (var source in exchangeRateSources ?? _providerFactory.ListExchangeRateSources())
            {
                var provider = _providerFactory.GetExchangeRateProvider(source);

                foreach (var frequency in GetSupportedFrequencies(provider))
                {
                    if (!await EnsureMinimumDateRangeAsync(minDate, source, frequency))
                        success = false;
                }
            }
            return success;
        }

        #endregion

        #region Rate Loading

        private async Task EnsureRatesLoadedAsync(DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            var minFxDate = _minDateBySourceFrequency.GetValueOrDefault((source, frequency), DateTime.MaxValue);

            if (minFxDate > date)
                await EnsureMinimumDateRangeAsync(date.AddMonths(-1), source, frequency);
        }

        private async Task<bool> EnsureMinimumDateRangeAsync(DateTime minDate, ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            minDate = PeriodHelper.GetStartOfMonth(minDate);

            var rawMinFxDate = _minDateBySourceFrequency.GetValueOrDefault((source, frequency), DateTime.MaxValue);
            if (PeriodHelper.GetStartOfMonth(rawMinFxDate) <= minDate)
                return true;

            await LoadRatesFromDbAsync(minDate);

            rawMinFxDate = _minDateBySourceFrequency.GetValueOrDefault((source, frequency), DateTime.MaxValue);
            if (PeriodHelper.GetStartOfMonth(rawMinFxDate) <= minDate)
                return true;

            var provider = _providerFactory.GetExchangeRateProvider(source);
            var fetchTo = rawMinFxDate == DateTime.MaxValue ? DateTime.UtcNow.Date : rawMinFxDate;

            return await FetchAndSaveHistoricalRatesAsync(provider, minDate, fetchTo, source, frequency);
        }

        private async Task FetchRatesForDateAsync(IExchangeRateProvider provider, DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            await FetchAndSaveHistoricalRatesAsync(provider, date.AddDays(-7), date, source, frequency);
        }

        private async Task<bool> FetchAndSaveHistoricalRatesAsync(IExchangeRateProvider provider, DateTime from, DateTime to, ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            var actualFrom = from <= to ? from : to;
            var actualTo = from <= to ? to : from;

            var fetchedRates = await FetchHistoricalRatesAsync(provider, frequency, actualFrom, actualTo);

            if (fetchedRates.Count == 0)
            {
                _logger?.LogError("No historical data found between {from:yyyy-MM-dd} and {to:yyyy-MM-dd} for source {source} with frequency {frequency}.",
                    actualFrom, actualTo, source, frequency);
                return false;
            }

            await SaveFetchedRatesAsync(fetchedRates, source);
            return true;
        }

        private async Task<List<Entities.ExchangeRate>> FetchHistoricalRatesAsync(IExchangeRateProvider provider, ExchangeRateFrequencies frequency, DateTime from, DateTime to)
        {
            IEnumerable<Entities.ExchangeRate> rates = frequency switch
            {
                ExchangeRateFrequencies.Daily when provider is IDailyExchangeRateProvider daily
                    => await daily.GetHistoricalDailyFxRatesAsync(from, to),
                ExchangeRateFrequencies.Monthly when provider is IMonthlyExchangeRateProvider monthly
                    => await monthly.GetHistoricalMonthlyFxRatesAsync(from, to),
                ExchangeRateFrequencies.Weekly when provider is IWeeklyExchangeRateProvider weekly
                    => await weekly.GetHistoricalWeeklyFxRatesAsync(from, to),
                ExchangeRateFrequencies.BiWeekly when provider is IBiWeeklyExchangeRateProvider biweekly
                    => await biweekly.GetHistoricalBiWeeklyFxRatesAsync(from, to),
                _ => throw new ExchangeRateException($"Provider {provider.Source} does not support frequency {frequency}")
            };
            return rates.ToList();
        }

        private static async Task<List<Entities.ExchangeRate>> FetchLatestRatesAsync(IExchangeRateProvider provider)
        {
            var rates = new List<Entities.ExchangeRate>();

            if (provider is IDailyExchangeRateProvider daily)
                rates.AddRange(await daily.GetDailyFxRatesAsync());
            if (provider is IMonthlyExchangeRateProvider monthly)
                rates.AddRange(await monthly.GetMonthlyFxRatesAsync());
            if (provider is IWeeklyExchangeRateProvider weekly)
                rates.AddRange(await weekly.GetWeeklyFxRatesAsync());
            if (provider is IBiWeeklyExchangeRateProvider biWeekly)
                rates.AddRange(await biWeekly.GetBiWeeklyFxRatesAsync());

            return rates;
        }

        #endregion

        #region Cache Operations

        private async Task SaveFetchedRatesAsync(List<Entities.ExchangeRate> rates, ExchangeRateSources source)
        {
            var itemsToSave = new List<Entities.ExchangeRate>();

            foreach (var item in rates)
            {
                var currentMin = _minDateBySourceFrequency.GetValueOrDefault((source, item.Frequency), DateTime.MaxValue);

                if (AddRateToCache(item))
                    itemsToSave.Add(item);

                if (item.Date < currentMin)
                    _minDateBySourceFrequency[(source, item.Frequency)] = item.Date;
            }

            if (itemsToSave.Count > 0 && _dataStore != null)
                await _dataStore.SaveExchangeRatesAsync(itemsToSave);
        }

        private async Task LoadRatesFromDbAsync(DateTime minDate)
        {
            if (_dataStore == null) return;
            var minFxDate = _minDateBySourceFrequency.Min(x => x.Value);
            var fxRatesInDb = await _dataStore.GetExchangeRatesAsync(minDate, minFxDate);
            LoadRates(fxRatesInDb);
        }

        private void LoadRates(IEnumerable<Entities.ExchangeRate> rates)
        {
            foreach (var item in rates)
            {
                AddRateToCache(item);

                var key = (item.Source, item.Frequency);

                if (!_minDateBySourceFrequency.TryGetValue(key, out var minFxDate))
                    _minDateBySourceFrequency[key] = item.Date;
                else if (item.Date < minFxDate)
                    _minDateBySourceFrequency[key] = item.Date;
            }
        }

        /// <summary>
        /// Adds or updates an exchange rate in the cache. Returns true if the rate was added or corrected.
        /// </summary>
        private bool AddRateToCache(Entities.ExchangeRate item)
        {
            var currency = item.CurrencyId;
            var date = item.Date;
            var source = item.Source;
            var frequency = item.Frequency;
            var newRate = item.Rate;

            if (!_rateCache.TryGetValue((source, frequency), out var byCurrency))
                _rateCache[(source, frequency)] = byCurrency = new Dictionary<CurrencyTypes, Dictionary<DateTime, decimal>>();

            if (!byCurrency.TryGetValue(currency, out var byDate))
                byCurrency[currency] = byDate = new Dictionary<DateTime, decimal>();

            if (byDate.TryGetValue(date, out var savedRate))
            {
                if (decimal.Round(newRate, Entities.ExchangeRate.Precision) == decimal.Round(savedRate, Entities.ExchangeRate.Precision))
                    return false;

                _logger?.LogWarning("Correcting exchange rate for {currency} on {date:yyyy-MM-dd}. Old: {savedRate}, New: {newRate}. Source: {source}, Frequency: {frequency}",
                    currency, date, savedRate, newRate, source, frequency);
                byDate[date] = newRate;
                return true;
            }

            byDate[date] = newRate;
            return true;
        }

        private IReadOnlyDictionary<CurrencyTypes, Dictionary<DateTime, decimal>> GetOrCreateCurrencyCache(ExchangeRateSources source, ExchangeRateFrequencies frequency)
        {
            if (!_rateCache.TryGetValue((source, frequency), out var cache))
            {
                cache = new Dictionary<CurrencyTypes, Dictionary<DateTime, decimal>>();
                _rateCache[(source, frequency)] = cache;
            }
            return cache;
        }

        #endregion

        #region Rate Calculation

        private Result<decimal> LookupFxRate(
            IReadOnlyDictionary<CurrencyTypes, Dictionary<DateTime, decimal>> ratesByCurrencyAndDate,
            DateTime date,
            DateTime minFxDate,
            IExchangeRateProvider provider,
            CurrencyTypes fromCurrency,
            CurrencyTypes toCurrency,
            out CurrencyTypes lookupCurrency)
        {
            if (fromCurrency == toCurrency)
            {
                lookupCurrency = fromCurrency;
                return Result.Ok(1m);
            }

            lookupCurrency = toCurrency == provider.Currency ? fromCurrency : toCurrency;
            var nonLookupCurrency = toCurrency == provider.Currency ? toCurrency : fromCurrency;

            if (!ratesByCurrencyAndDate.TryGetValue(lookupCurrency, out var currencyDict))
            {
                if (!_peggedCurrencies.TryGetValue(lookupCurrency, out var peggedCurrency))
                    return Result.Fail(new NotSupportedCurrencyError(lookupCurrency));

                var peggedResult = LookupFxRate(ratesByCurrencyAndDate, date, minFxDate, provider, nonLookupCurrency, peggedCurrency.PeggedTo, out _);
                if (peggedResult.IsFailed)
                    return peggedResult;

                var peggedRate = peggedCurrency.Rate;
                return Result.Ok(toCurrency == provider.Currency
                    ? peggedRate / peggedResult.Value
                    : peggedResult.Value / peggedRate);
            }

            for (var d = date; d >= minFxDate; d = d.AddDays(-1))
            {
                if (currencyDict.TryGetValue(d, out var fxRate))
                {
                    return provider.QuoteType switch
                    {
                        QuoteTypes.Direct when toCurrency == provider.Currency => Result.Ok(fxRate),
                        QuoteTypes.Direct when fromCurrency == provider.Currency => Result.Ok(1 / fxRate),
                        QuoteTypes.Indirect when fromCurrency == provider.Currency => Result.Ok(fxRate),
                        QuoteTypes.Indirect when toCurrency == provider.Currency => Result.Ok(1 / fxRate),
                        _ => throw new InvalidOperationException("Unsupported QuoteType")
                    };
                }
            }

            return Result.Fail(new NoFxRateFoundError());
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

        #endregion
    }

}
