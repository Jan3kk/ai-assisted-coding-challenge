using ExchangeRate.Domain;
using ExchangeRate.Domain.Enums;
using ExchangeRate.Domain.Exceptions;
using ExchangeRate.Domain.Helpers;
using ExchangeRate.Application.Ports;
using Microsoft.Extensions.Logging;
using ExchangeRateEntity = ExchangeRate.Domain.Entities.ExchangeRate;

namespace ExchangeRate.Application;

public sealed class ExchangeRateSynchronizer : IExchangeRateSynchronizer
{
    private readonly IStoredExchangeRateRepository _storedRepository;
    private readonly IExchangeRateProviderFactory _providerFactory;
    private readonly ILogger<ExchangeRateSynchronizer>? _logger;

    public ExchangeRateSynchronizer(
        IStoredExchangeRateRepository storedRepository,
        IExchangeRateProviderFactory providerFactory,
        ILogger<ExchangeRateSynchronizer>? logger = null)
    {
        _storedRepository = storedRepository ?? throw new ArgumentNullException(nameof(storedRepository));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _logger = logger;
    }

    public async Task EnsureRatesLoadedAsync(DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
    {
        var rates = await _storedRepository.GetAsync(DateTime.MinValue, DateTime.UtcNow.Date.AddDays(1), source, frequency).ConfigureAwait(false);
        var minFxDate = rates.Count > 0 ? rates.Min(r => r.Date) : DateTime.MaxValue;

        if (PeriodHelper.GetStartOfMonth(minFxDate) > date)
            await EnsureMinimumDateRangeAsync(PeriodHelper.GetStartOfMonth(date.AddMonths(-1)), source, frequency).ConfigureAwait(false);
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

    public async Task EnsureRatesForDateAsync(DateTime date, ExchangeRateSources source, ExchangeRateFrequencies frequency)
    {
        var provider = _providerFactory.GetExchangeRateProvider(source);
        await FetchAndSaveHistoricalRatesAsync(provider, date.AddDays(-7), date, source, frequency).ConfigureAwait(false);
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

    private static IEnumerable<ExchangeRateFrequencies> GetSupportedFrequencies(IExchangeRateProvider provider)
    {
        if (provider is IDailyExchangeRateProvider) yield return ExchangeRateFrequencies.Daily;
        if (provider is IMonthlyExchangeRateProvider) yield return ExchangeRateFrequencies.Monthly;
        if (provider is IWeeklyExchangeRateProvider) yield return ExchangeRateFrequencies.Weekly;
        if (provider is IBiWeeklyExchangeRateProvider) yield return ExchangeRateFrequencies.BiWeekly;
    }
}
