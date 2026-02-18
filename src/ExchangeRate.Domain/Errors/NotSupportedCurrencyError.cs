using ExchangeRate.Domain.Enums;
using FluentResults;

namespace ExchangeRate.Domain.Errors;

public class NotSupportedCurrencyError : Error
{
    public NotSupportedCurrencyError(CurrencyTypes currency)
        : base($"Not supported currency: {currency}") { }
}
