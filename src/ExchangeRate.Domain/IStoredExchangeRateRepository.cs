using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Domain;

public interface IStoredExchangeRateRepository
{
    Task<IReadOnlyList<Entities.ExchangeRate>> GetAsync(
        DateTime minDate,
        DateTime maxDate,
        ExchangeRateSources? source = null,
        ExchangeRateFrequencies? frequency = null,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        IEnumerable<Entities.ExchangeRate> rates,
        CancellationToken cancellationToken = default);
}
