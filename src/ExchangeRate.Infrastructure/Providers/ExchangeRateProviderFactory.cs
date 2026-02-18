using ExchangeRate.Application.Ports;
using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Infrastructure.Providers;

public sealed class ExchangeRateProviderFactory : IExchangeRateProviderFactory
{
    private readonly Dictionary<ExchangeRateSources, IExchangeRateProvider> _providersBySource;

    public ExchangeRateProviderFactory(IEnumerable<IExchangeRateProvider> exchangeRateProviders)
    {
        ArgumentNullException.ThrowIfNull(exchangeRateProviders);
        _providersBySource = exchangeRateProviders.ToDictionary(p => p.Source);
    }

    public IExchangeRateProvider GetExchangeRateProvider(ExchangeRateSources source)
    {
        if (!_providersBySource.TryGetValue(source, out var provider))
            throw new NotSupportedException($"Source {source} is not supported.");
        return provider;
    }

    public IEnumerable<ExchangeRateSources> ListExchangeRateSources() => _providersBySource.Keys;
}
