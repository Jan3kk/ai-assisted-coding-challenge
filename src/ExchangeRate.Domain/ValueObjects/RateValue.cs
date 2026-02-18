namespace ExchangeRate.Domain.ValueObjects;

public readonly record struct RateValue(decimal Value)
{
    private const int Precision = 10;

    public decimal Rounded => decimal.Round(Value, Precision);

    public bool Equals(RateValue other) =>
        decimal.Round(Value, Precision) == decimal.Round(other.Value, Precision);

    public override int GetHashCode() => Rounded.GetHashCode();
}
