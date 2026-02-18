using ExchangeRate.Core.Enums;
using FluentResults;

namespace ExchangeRate.Core.Errors;

public class NotSupportedCurrencyError : Error
{
    public NotSupportedCurrencyError(CurrencyTypes currency)
        : base($"Not supported currency: {currency}") { }
}
