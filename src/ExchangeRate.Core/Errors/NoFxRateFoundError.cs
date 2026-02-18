using FluentResults;

namespace ExchangeRate.Core.Errors;

public class NoFxRateFoundError : Error
{
    public NoFxRateFoundError()
        : base("No fx rate found") { }
}
