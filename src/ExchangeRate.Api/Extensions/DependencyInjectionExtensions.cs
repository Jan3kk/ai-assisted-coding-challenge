using ExchangeRate.Application;
using ExchangeRate.Application.Configuration;
using ExchangeRate.Application.Ports;
using ExchangeRate.Domain;
using ExchangeRate.Domain.Services;
using ExchangeRate.Infrastructure.Persistence;
using ExchangeRate.Infrastructure.Providers;
using ExchangeRate.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace ExchangeRate.Api.Extensions;

/// <summary>
/// Extension methods for registering exchange rate services (composition root for DI).
/// </summary>
public static class DependencyInjectionExtensions
{
    /// <summary>
    /// Registers exchange rate API configuration using the Options pattern.
    /// </summary>
    public static IServiceCollection AddExchangeRateConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ExternalExchangeRateApiConfig>(configuration.GetSection("ExchangeRateApi"));
        services.AddSingleton<ExternalExchangeRateApiConfig>(sp =>
            sp.GetRequiredService<IOptions<ExternalExchangeRateApiConfig>>().Value);
        return services;
    }

    /// <summary>
    /// Registers HTTP clients and exchange rate providers (ECB, MXCB, etc.).
    /// </summary>
    public static IServiceCollection AddExchangeRateProviders(this IServiceCollection services)
    {
        services.AddHttpClient<EUECBExchangeRateProvider>();
        services.AddHttpClient<MXCBExchangeRateProvider>();

        services.AddSingleton<IExchangeRateProvider>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(EUECBExchangeRateProvider));
            var config = sp.GetRequiredService<ExternalExchangeRateApiConfig>();
            return new EUECBExchangeRateProvider(httpClient, config);
        });

        services.AddSingleton<IExchangeRateProvider>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(MXCBExchangeRateProvider));
            var config = sp.GetRequiredService<ExternalExchangeRateApiConfig>();
            return new MXCBExchangeRateProvider(httpClient, config);
        });

        services.AddSingleton<IExchangeRateProviderFactory, ExchangeRateProviderFactory>();
        return services;
    }

    /// <summary>
    /// Registers exchange rate infrastructure and application (data store, repositories, facade).
    /// </summary>
    public static IServiceCollection AddExchangeRateInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IExchangeRateDataStore, InMemoryExchangeRateDataStore>();
        services.AddSingleton<IStoredExchangeRateRepository, StoredExchangeRateRepository>();
        services.AddSingleton<IPeggedCurrencyRepository, PeggedCurrencyRepository>();
        services.AddSingleton<IRateQuoteService, RateQuoteService>();
        services.AddSingleton<IExchangeRateRepository, ExchangeRateRepositoryFacade>();
        return services;
    }
}
