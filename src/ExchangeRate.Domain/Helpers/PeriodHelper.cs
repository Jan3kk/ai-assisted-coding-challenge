#nullable enable
namespace ExchangeRate.Domain.Helpers;

public static class PeriodHelper
{
    /// <summary>
    /// Returns a date with the first day of the year and month of the given date.
    /// Used for monthly exchange rate lookups and period alignment.
    /// </summary>
    public static DateTime GetStartOfMonth(DateTime date)
    {
        return new DateTime(date.Year, date.Month, 1);
    }
}
