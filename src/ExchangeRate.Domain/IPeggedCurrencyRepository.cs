namespace ExchangeRate.Domain;

public interface IPeggedCurrencyRepository
{
    IReadOnlyList<Entities.PeggedCurrency> GetAll();
}
