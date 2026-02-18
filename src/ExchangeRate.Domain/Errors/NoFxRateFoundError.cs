using FluentResults;

namespace ExchangeRate.Domain.Errors;

public class NoFxRateFoundError : Error
{
    public NoFxRateFoundError()
        : base("No fx rate found") { }
}
