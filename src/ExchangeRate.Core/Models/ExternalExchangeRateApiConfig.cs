namespace ExchangeRate.Core.Models
{
    public class ExternalExchangeRateApiConfig
    {
        public required string BaseAddress { get; set; }
        public required string TokenEndpoint { get; set; }
        public required string ClientId { get; set; }
        public required string ClientSecret { get; set; }
    }
}
