using System;
using System.Collections.Generic;
using System.Linq;
using ExchangeRate.Core.Enums;
using ExchangeRate.Core.Interfaces;
using ExchangeRate.Core.Interfaces.Providers;

namespace ExchangeRate.Core
{
    public class ExchangeRateProviderFactory : IExchangeRateProviderFactory
    {
        private readonly Dictionary<ExchangeRateSources, IExchangeRateProvider> _providersBySource;

        public ExchangeRateProviderFactory(IEnumerable<IExchangeRateProvider> exchangeRateProviders)
        {
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
}
