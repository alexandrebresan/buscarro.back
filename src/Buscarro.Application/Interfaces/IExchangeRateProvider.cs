using Buscarro.Application.DTOs;

namespace Buscarro.Application.Interfaces;

public interface IExchangeRateProvider
{
    Task<ExchangeRateQuote> GetUsdToBrlQuoteAsync(CancellationToken cancellationToken);
}
