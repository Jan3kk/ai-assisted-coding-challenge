using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExchangeRate.Application.Configuration;
using ExchangeRate.Application.Ports;
using ExchangeRate.Domain.Entities;
using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Infrastructure.Providers;

public abstract class ExternalApiExchangeRateProvider : IExchangeRateProvider
{
    private static readonly Dictionary<string, CurrencyTypes> CurrencyMapping;
    private readonly HttpClient _httpClient;
    private readonly ExternalExchangeRateApiConfig _config;
    private readonly JsonSerializerOptions _jsonOptions;

    public abstract CurrencyTypes Currency { get; }
    public abstract QuoteTypes QuoteType { get; }
    public abstract ExchangeRateSources Source { get; }
    public abstract string BankId { get; }

    static ExternalApiExchangeRateProvider()
    {
        var currencies = Enum.GetValues(typeof(CurrencyTypes)).Cast<CurrencyTypes>().ToList();
        CurrencyMapping = currencies.ToDictionary(x => x.ToString().ToUpperInvariant());
    }

    protected ExternalApiExchangeRateProvider(HttpClient httpClient, ExternalExchangeRateApiConfig config)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.BaseAddress = new Uri(config.BaseAddress);
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    protected async Task<IEnumerable<Domain.Entities.ExchangeRate>> GetDailyRatesAsync(string bankId, (DateTime startDate, DateTime endDate) period = default, CancellationToken cancellationToken = default)
    {
        var path = period == default ? $"/v1/Banks/{bankId}/DailyRates/Latest" : $"/v1/Banks/{bankId}/DailyRates/TimeSeries?startDate={period.startDate:yyyy-MM-dd}&endDate={period.endDate:yyyy-MM-dd}";
        var data = await GetExchangeRatesAsync(path, cancellationToken).ConfigureAwait(false);
        return MapRates(data, Source, ExchangeRateFrequencies.Daily);
    }

    protected async Task<IEnumerable<Domain.Entities.ExchangeRate>> GetMonthlyRatesAsync(string bankId, (int year, int month) period = default, CancellationToken cancellationToken = default)
    {
        var path = period == default ? $"/v1/Banks/{bankId}/MonthlyRates/Latest" : $"/v1/Banks/{bankId}/MonthlyRates/{period.year}/{period.month}";
        var data = await GetExchangeRatesAsync(path, cancellationToken).ConfigureAwait(false);
        return MapRates(data, Source, ExchangeRateFrequencies.Monthly);
    }

    protected async Task<IEnumerable<Domain.Entities.ExchangeRate>> GetWeeklyRatesAsync(string bankId, (int year, int month) period = default, CancellationToken cancellationToken = default)
    {
        var path = period == default ? $"/v1/Banks/{bankId}/WeeklyRates/Latest" : $"/v1/Banks/{bankId}/WeeklyRates/{period.year}/{period.month}";
        var data = await GetExchangeRatesAsync(path, cancellationToken).ConfigureAwait(false);
        return MapRates(data, Source, ExchangeRateFrequencies.Weekly);
    }

    protected async Task<IEnumerable<Domain.Entities.ExchangeRate>> GetBiWeeklyRatesAsync(string bankId, (int year, int month) period = default, CancellationToken cancellationToken = default)
    {
        var path = period == default ? $"/v1/Banks/{bankId}/BiweeklyRates/Latest" : $"/v1/Banks/{bankId}/BiweeklyRates/{period.year}/{period.month}";
        var data = await GetExchangeRatesAsync(path, cancellationToken).ConfigureAwait(false);
        return MapRates(data, Source, ExchangeRateFrequencies.BiWeekly);
    }

    private static IEnumerable<Domain.Entities.ExchangeRate> MapRates(ExchangeRatesDto dto, ExchangeRateSources source, ExchangeRateFrequencies frequency)
    {
        foreach (var (dateStr, currencies) in dto.Rates)
        {
            if (!DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var date))
                continue;
            foreach (var (code, rateDto) in currencies.Where(kv => CurrencyMapping.ContainsKey(kv.Key)))
            {
                yield return new Domain.Entities.ExchangeRate
                {
                    Date = date,
                    Frequency = frequency,
                    Rate = rateDto.GetAbsoluteRate(),
                    Source = source,
                    CurrencyId = CurrencyMapping[code]
                };
            }
        }
    }

    private async Task<ExchangeRatesDto> GetExchangeRatesAsync(string requestUri, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"Exchange rate API request failed. BankId: {BankId}, StatusCode: {(int)response.StatusCode}, ResponseBody: {body}");
        }
        return (await response.Content.ReadFromJsonAsync<ExchangeRatesDto>(_jsonOptions, cancellationToken).ConfigureAwait(false))!;
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", _config.ClientId },
            { "client_secret", _config.ClientSecret },
            { "scope", "fx_api" }
        });
        using var response = await _httpClient.PostAsync(_config.TokenEndpoint, content, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"Token request failed. StatusCode: {(int)response.StatusCode}, ResponseBody: {body}");
        }
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
        return tokenResponse!.access_token;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string access_token { get; set; } = "";
    }

    private sealed class ExchangeRatesDto
    {
        public Dictionary<string, Dictionary<string, RateDto>> Rates { get; set; } = new();
    }

    private sealed class RateDto
    {
        [JsonPropertyName("rate")]
        public decimal Value { get; set; }
        public int? UnitMultiplier { get; set; }
        public decimal GetAbsoluteRate() => Value / (decimal)Math.Pow(10, UnitMultiplier ?? 0);
    }
}
