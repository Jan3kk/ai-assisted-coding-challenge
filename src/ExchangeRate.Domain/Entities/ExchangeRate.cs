using ExchangeRate.Domain.Enums;
using ExchangeRate.Domain.ValueObjects;

namespace ExchangeRate.Domain.Entities;

public record ExchangeRate
{
    public required DateTime Date { get; init; }

    public required CurrencyTypes CurrencyId { get; init; }

    public required ExchangeRateSources Source { get; init; }

    public required ExchangeRateFrequencies Frequency { get; init; }

    public required RateValue Rate { get; init; }

    public override string ToString() => $"{CurrencyId} - {Date:yyyy-MM-dd}: {Rate.Rounded}";
}
