using ExchangeRate.Domain;
using ExchangeRate.Domain.Entities;

namespace ExchangeRate.Infrastructure.Persistence;

public sealed class PeggedCurrencyRepository : IPeggedCurrencyRepository
{
    private readonly IExchangeRateDataStore _dataStore;

    public PeggedCurrencyRepository(IExchangeRateDataStore dataStore)
    {
        _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
    }

    public IReadOnlyList<PeggedCurrency> GetAll()
    {
        var fromStore = _dataStore.GetPeggedCurrencies();
        if (fromStore.Count > 0)
            return fromStore;
        return PeggedCurrency.InitialData.ToList();
    }
}
