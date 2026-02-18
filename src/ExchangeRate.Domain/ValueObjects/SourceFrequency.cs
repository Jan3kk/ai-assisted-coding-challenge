using ExchangeRate.Domain.Enums;

namespace ExchangeRate.Domain.ValueObjects;

public readonly record struct SourceFrequency(ExchangeRateSources Source, ExchangeRateFrequencies Frequency);
