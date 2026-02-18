using ExchangeRate.Api.Infrastructure;
using ExchangeRate.Core;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Infrastructure;
using ExchangeRate.Core.Interfaces;
using ExchangeRate.Core.Interfaces.Providers;
using ExchangeRate.Core.Models;
using ExchangeRate.Core.Providers;

var builder = WebApplication.CreateBuilder(args);

// Configure ExchangeRate services
builder.Services.AddSingleton<ExternalExchangeRateApiConfig>(sp =>
{
    var config = builder.Configuration.GetSection("ExchangeRateApi").Get<ExternalExchangeRateApiConfig>();
    return config ?? new ExternalExchangeRateApiConfig
    {
        BaseAddress = builder.Configuration["ExchangeRateApi:BaseAddress"] ?? "http://localhost",
        TokenEndpoint = builder.Configuration["ExchangeRateApi:TokenEndpoint"] ?? "/connect/token",
        ClientId = builder.Configuration["ExchangeRateApi:ClientId"] ?? "client",
        ClientSecret = builder.Configuration["ExchangeRateApi:ClientSecret"] ?? "secret"
    };
});

builder.Services.AddHttpClient<EUECBExchangeRateProvider>();
builder.Services.AddHttpClient<MXCBExchangeRateProvider>();

builder.Services.AddSingleton<IExchangeRateProvider>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(EUECBExchangeRateProvider));
    var config = sp.GetRequiredService<ExternalExchangeRateApiConfig>();
    return new EUECBExchangeRateProvider(httpClient, config);
});

builder.Services.AddSingleton<IExchangeRateProvider>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(MXCBExchangeRateProvider));
    var config = sp.GetRequiredService<ExternalExchangeRateApiConfig>();
    return new MXCBExchangeRateProvider(httpClient, config);
});

// Register the provider factory
builder.Services.AddSingleton<IExchangeRateProviderFactory, ExchangeRateProviderFactory>();

// Register the data store - this can be replaced by candidates with a real DB implementation
builder.Services.AddSingleton<IExchangeRateDataStore, InMemoryExchangeRateDataStore>();

// Register the repository
builder.Services.AddSingleton<IExchangeRateRepository, ExchangeRateRepository>();

var app = builder.Build();

app.MapGet("/api/rates", async (
    string from,
    string to,
    DateTime date,
    ExchangeRateSources source,
    ExchangeRateFrequencies frequency,
    IExchangeRateRepository repository) =>
{
    var rate = await repository.GetRateAsync(from, to, date, source, frequency);

    if (rate == null)
    {
        return Results.NotFound(new { error = $"No exchange rate found for {from} to {to} on {date:yyyy-MM-dd}" });
    }

    return Results.Ok(new ExchangeRateResponse(from, to, date, source.ToString(), frequency.ToString(), rate.Value));
});

app.Run();

/// <summary>
/// Response model for the exchange rate API endpoint.
/// </summary>
public record ExchangeRateResponse(
    string FromCurrency,
    string ToCurrency,
    DateTime Date,
    string Source,
    string Frequency,
    decimal Rate);

// Make Program accessible to test project
public partial class Program { }

namespace ExchangeRate.Api.Infrastructure
{
    /// <summary>
    /// In-memory implementation of IExchangeRateDataStore.
    /// Candidates can replace this with a real database implementation (e.g., EF Core).
    /// </summary>
    public class InMemoryExchangeRateDataStore : IExchangeRateDataStore
    {
        private readonly List<ExchangeRate.Core.Entities.ExchangeRate> _exchangeRates = new();
        private readonly List<ExchangeRate.Core.Entities.PeggedCurrency> _peggedCurrencies = new();

        public Task<List<ExchangeRate.Core.Entities.ExchangeRate>> GetExchangeRatesAsync(DateTime minDate, DateTime maxDate)
        {
            var rates = _exchangeRates
                .Where(r => r.Date >= minDate && r.Date < maxDate)
                .ToList();

            return Task.FromResult(rates);
        }

        public Task SaveExchangeRatesAsync(IEnumerable<ExchangeRate.Core.Entities.ExchangeRate> rates)
        {
            foreach (var rate in rates)
            {
                var existingIndex = _exchangeRates.FindIndex(r =>
                    r.Date == rate.Date &&
                    r.CurrencyId == rate.CurrencyId &&
                    r.Source == rate.Source &&
                    r.Frequency == rate.Frequency);

                if (existingIndex >= 0)
                    _exchangeRates[existingIndex] = rate;
                else
                    _exchangeRates.Add(rate);
            }

            return Task.CompletedTask;
        }

        public List<ExchangeRate.Core.Entities.PeggedCurrency> GetPeggedCurrencies()
        {
            return _peggedCurrencies.ToList();
        }

        public void AddPeggedCurrency(ExchangeRate.Core.Entities.PeggedCurrency peggedCurrency)
        {
            _peggedCurrencies.Add(peggedCurrency);
        }
    }
}
