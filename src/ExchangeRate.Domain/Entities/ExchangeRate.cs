using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Domain.Entities;

public record ExchangeRate
{
    public required DateTime Date { get; init; }

    public required CurrencyTypes CurrencyId { get; init; }

    public required ExchangeRateSources Source { get; init; }

    public required ExchangeRateFrequencies Frequency { get; init; }

    public required decimal Rate { get; set; }

    public override string ToString() => $"{CurrencyId} - {Date:yyyy-MM-dd}: {Rate}";

    public const int Precision = 10;
}
