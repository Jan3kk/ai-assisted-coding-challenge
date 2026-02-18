using ExchangeRate.Domain.Entities;
using ExchangeRate.Domain.Enums;
using FluentResults;

namespace ExchangeRate.Domain.Services;

public interface IRateQuoteService
{
    /// <summary>
    /// Computes the exchange rate from fromCurrency to toCurrency for the given date,
    /// using the rate surface, provider base currency and quote type, and pegged currencies.
    /// </summary>
    Result<decimal> GetQuote(
        IReadOnlyDictionary<CurrencyTypes, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrencyAndDate,
        DateTime date,
        DateTime minFxDate,
        CurrencyTypes providerCurrency,
        QuoteTypes quoteType,
        IReadOnlyDictionary<CurrencyTypes, PeggedCurrency> peggedCurrencies,
        CurrencyTypes fromCurrency,
        CurrencyTypes toCurrency,
        out CurrencyTypes lookupCurrency);
}
