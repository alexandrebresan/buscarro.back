namespace Buscarro.Application.DTOs;

public sealed record ExchangeRateQuote(decimal UsdToBrlRate, DateOnly QuotedOn);
