using System.Collections.Generic;
using ExchangeRate.Core.Enums;

namespace ExchangeRate.Core.Entities;

public class PeggedCurrency
{
    public required CurrencyTypes CurrencyId { get; init; }

    public required CurrencyTypes PeggedTo { get; init; }

    public required decimal Rate { get; init; }

    internal static IEnumerable<PeggedCurrency> InitialData =>
        new List<PeggedCurrency>()
        {
            new() { CurrencyId = CurrencyTypes.XCD, PeggedTo = CurrencyTypes.USD, Rate = 0.37007m },
            new() { CurrencyId = CurrencyTypes.DJF, PeggedTo = CurrencyTypes.USD, Rate = 0.00562m },
            new() { CurrencyId = CurrencyTypes.HKD, PeggedTo = CurrencyTypes.USD, Rate = 0.12850m },
            new() { CurrencyId = CurrencyTypes.BAM, PeggedTo = CurrencyTypes.EUR, Rate = 0.60000m },
            new() { CurrencyId = CurrencyTypes.XPF, PeggedTo = CurrencyTypes.EUR, Rate = 0.00838m },
            new() { CurrencyId = CurrencyTypes.BND, PeggedTo = CurrencyTypes.SGD, Rate = 1.00000m },
            new() { CurrencyId = CurrencyTypes.MOP, PeggedTo = CurrencyTypes.HKD, Rate = 0.16890m },
            new() { CurrencyId = CurrencyTypes.AWG, PeggedTo = CurrencyTypes.USD, Rate = 0.55866m },
            new() { CurrencyId = CurrencyTypes.BSD, PeggedTo = CurrencyTypes.USD, Rate = 1.00000m },
            new() { CurrencyId = CurrencyTypes.BHD, PeggedTo = CurrencyTypes.USD, Rate = 2.65957m },
            new() { CurrencyId = CurrencyTypes.BBD, PeggedTo = CurrencyTypes.USD, Rate = 0.50000m },
            new() { CurrencyId = CurrencyTypes.BZD, PeggedTo = CurrencyTypes.USD, Rate = 0.49600m },
            new() { CurrencyId = CurrencyTypes.ANG, PeggedTo = CurrencyTypes.USD, Rate = 0.55900m },
            new() { CurrencyId = CurrencyTypes.ERN, PeggedTo = CurrencyTypes.USD, Rate = 0.06667m },
            new() { CurrencyId = CurrencyTypes.JOD, PeggedTo = CurrencyTypes.USD, Rate = 1.41044m },
            new() { CurrencyId = CurrencyTypes.OMR, PeggedTo = CurrencyTypes.USD, Rate = 2.60078m },
            new() { CurrencyId = CurrencyTypes.PAB, PeggedTo = CurrencyTypes.USD, Rate = 1.00000m },
            new() { CurrencyId = CurrencyTypes.QAR, PeggedTo = CurrencyTypes.USD, Rate = 0.27473m },
            new() { CurrencyId = CurrencyTypes.SAR, PeggedTo = CurrencyTypes.USD, Rate = 0.26667m },
            new() { CurrencyId = CurrencyTypes.TMT, PeggedTo = CurrencyTypes.USD, Rate = 0.29777m },
            new() { CurrencyId = CurrencyTypes.AED, PeggedTo = CurrencyTypes.USD, Rate = 0.27229m },
            new() { CurrencyId = CurrencyTypes.XOF, PeggedTo = CurrencyTypes.EUR, Rate = 0.00152m },
            new() { CurrencyId = CurrencyTypes.CVE, PeggedTo = CurrencyTypes.EUR, Rate = 0.00907m },
            new() { CurrencyId = CurrencyTypes.XAF, PeggedTo = CurrencyTypes.EUR, Rate = 0.00152m },
            new() { CurrencyId = CurrencyTypes.KMF, PeggedTo = CurrencyTypes.EUR, Rate = 0.00203m },
        };
}
