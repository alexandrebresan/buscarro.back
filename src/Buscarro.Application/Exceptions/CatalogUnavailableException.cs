namespace Buscarro.Application.Exceptions;

public sealed class CatalogUnavailableException : Exception
{
    public const string PublicMessage = "O catalogo de veiculos esta temporariamente indisponivel.";

    public CatalogUnavailableException() : base(PublicMessage) { }
    public CatalogUnavailableException(Exception innerException) : base(PublicMessage, innerException) { }
}
