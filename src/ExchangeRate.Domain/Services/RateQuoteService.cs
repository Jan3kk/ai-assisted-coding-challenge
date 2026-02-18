using ExchangeRate.Domain.Entities;
using ExchangeRate.Domain.Enums;
using ExchangeRate.Domain.Errors;
using FluentResults;

namespace ExchangeRate.Domain.Services;

public sealed class RateQuoteService : IRateQuoteService
{
    public Result<decimal> GetQuote(
        IReadOnlyDictionary<CurrencyTypes, IReadOnlyDictionary<DateTime, decimal>> ratesByCurrencyAndDate,
        DateTime date,
        DateTime minFxDate,
        CurrencyTypes providerCurrency,
        QuoteTypes quoteType,
        IReadOnlyDictionary<CurrencyTypes, PeggedCurrency> peggedCurrencies,
        CurrencyTypes fromCurrency,
        CurrencyTypes toCurrency,
        out CurrencyTypes lookupCurrency)
    {
        if (fromCurrency == toCurrency)
        {
            lookupCurrency = fromCurrency;
            return Result.Ok(1m);
        }

        lookupCurrency = toCurrency == providerCurrency ? fromCurrency : toCurrency;
        var nonLookupCurrency = toCurrency == providerCurrency ? toCurrency : fromCurrency;

        if (!ratesByCurrencyAndDate.TryGetValue(lookupCurrency, out var currencyDict))
        {
            if (!peggedCurrencies.TryGetValue(lookupCurrency, out var peggedCurrency))
                return Result.Fail(new NotSupportedCurrencyError(lookupCurrency));

            var peggedResult = GetQuote(ratesByCurrencyAndDate, date, minFxDate, providerCurrency, quoteType,
                peggedCurrencies, nonLookupCurrency, peggedCurrency.PeggedTo, out _);
            if (peggedResult.IsFailed)
                return peggedResult;

            var peggedRate = peggedCurrency.Rate;
            return Result.Ok(toCurrency == providerCurrency
                ? peggedRate / peggedResult.Value
                : peggedResult.Value / peggedRate);
        }

        for (var d = date; d >= minFxDate; d = d.AddDays(-1))
        {
            if (currencyDict.TryGetValue(d, out var fxRate))
            {
                return quoteType switch
                {
                    QuoteTypes.Direct when toCurrency == providerCurrency => Result.Ok(fxRate),
                    QuoteTypes.Direct when fromCurrency == providerCurrency => Result.Ok(1 / fxRate),
                    QuoteTypes.Indirect when fromCurrency == providerCurrency => Result.Ok(fxRate),
                    QuoteTypes.Indirect when toCurrency == providerCurrency => Result.Ok(1 / fxRate),
                    _ => throw new InvalidOperationException("Unsupported QuoteType")
                };
            }
        }

        return Result.Fail(new NoFxRateFoundError());
    }
}
