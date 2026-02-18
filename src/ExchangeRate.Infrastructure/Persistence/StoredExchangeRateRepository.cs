using ExchangeRate.Domain;
using ExchangeRate.Domain.Enums;
using ExchangeRate.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using ExchangeRateEntity = ExchangeRate.Domain.Entities.ExchangeRate;

namespace ExchangeRate.Infrastructure.Persistence;

public sealed class StoredExchangeRateRepository : IStoredExchangeRateRepository
{
    private readonly Dictionary<(ExchangeRateSources, ExchangeRateFrequencies), Dictionary<CurrencyTypes, Dictionary<DateTime, RateValue>>> _rateCache = new();
    private Dictionary<(ExchangeRateSources, ExchangeRateFrequencies), DateTime> _minDateBySourceFrequency = new();
    private readonly IExchangeRateDataStore _dataStore;
    private readonly ILogger<StoredExchangeRateRepository>? _logger;

    public StoredExchangeRateRepository(IExchangeRateDataStore dataStore, ILogger<StoredExchangeRateRepository>? logger = null)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
        _logger = logger;
        InitMinDates();
    }

    private void InitMinDates()
    {
        var supportedSources = Enum.GetValues(typeof(ExchangeRateSources)).Cast<ExchangeRateSources>().ToList();
        _minDateBySourceFrequency = supportedSources
            .SelectMany(s => Enum.GetValues(typeof(ExchangeRateFrequencies)).Cast<ExchangeRateFrequencies>().Select(f => (s, f)))
            .ToDictionary(x => x, _ => DateTime.MaxValue);
    }

    public async Task<IReadOnlyList<ExchangeRateEntity>> GetAsync(
        DateTime minDate,
        DateTime maxDate,
        ExchangeRateSources? source = null,
        ExchangeRateFrequencies? frequency = null,
        CancellationToken cancellationToken = default)
    {
        var minFxDate = _minDateBySourceFrequency.Values.Min();
        if (minFxDate == DateTime.MaxValue)
            minFxDate = DateTime.UtcNow.Date;

        var fromDb = await _dataStore.GetExchangeRatesAsync(minDate, maxDate).ConfigureAwait(false);
        LoadRates(fromDb);

        var sources = source.HasValue ? new[] { source.Value } : Enum.GetValues(typeof(ExchangeRateSources)).Cast<ExchangeRateSources>();
        var frequencies = frequency.HasValue ? new[] { frequency.Value } : Enum.GetValues(typeof(ExchangeRateFrequencies)).Cast<ExchangeRateFrequencies>();

        var result = new List<ExchangeRateEntity>();
        foreach (var s in sources)
        {
            foreach (var f in frequencies)
            {
                if (!_rateCache.TryGetValue((s, f), out var byCurrency))
                    continue;
                foreach (var (currency, byDate) in byCurrency)
                {
                    foreach (var (date, rateValue) in byDate)
                    {
                        if (date >= minDate && date < maxDate)
                            result.Add(new ExchangeRateEntity { Date = date, CurrencyId = currency, Source = s, Frequency = f, Rate = rateValue });
                    }
                }
            }
        }

        return result;
    }

    public async Task SaveAsync(IEnumerable<ExchangeRateEntity> rates, CancellationToken cancellationToken = default)
    {
        var itemsToSave = new List<ExchangeRateEntity>();
        foreach (var item in rates)
        {
            var currentMin = _minDateBySourceFrequency.GetValueOrDefault((item.Source, item.Frequency), DateTime.MaxValue);

            if (AddRateToCache(item))
                itemsToSave.Add(item);

            if (item.Date < currentMin)
                _minDateBySourceFrequency[(item.Source, item.Frequency)] = item.Date;
        }

        if (itemsToSave.Count > 0)
            await _dataStore.SaveExchangeRatesAsync(itemsToSave).ConfigureAwait(false);
    }

    private void LoadRates(IEnumerable<ExchangeRateEntity> rates)
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

    private bool AddRateToCache(ExchangeRateEntity item)
    {
        var currency = item.CurrencyId;
        var date = item.Date;
        var source = item.Source;
        var frequency = item.Frequency;
        var newRate = item.Rate;

        if (!_rateCache.TryGetValue((source, frequency), out var byCurrency))
            _rateCache[(source, frequency)] = byCurrency = new Dictionary<CurrencyTypes, Dictionary<DateTime, RateValue>>();

        if (!byCurrency.TryGetValue(currency, out var byDate))
            byCurrency[currency] = byDate = new Dictionary<DateTime, RateValue>();

        if (byDate.TryGetValue(date, out var savedRate))
        {
            if (newRate.Equals(savedRate))
                return false;

            _logger?.LogWarning(
                "Correcting exchange rate for {Currency} on {Date:yyyy-MM-dd}. Old: {SavedRate}, New: {NewRate}. Source: {Source}, Frequency: {Frequency}",
                currency, date, savedRate.Rounded, newRate.Rounded, source, frequency);
            byDate[date] = newRate;
            return true;
        }

        byDate[date] = newRate;
        return true;
    }
}
