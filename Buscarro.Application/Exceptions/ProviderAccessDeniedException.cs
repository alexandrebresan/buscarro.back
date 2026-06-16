namespace Buscarro.Application.Exceptions;

public sealed class ProviderAccessDeniedException : Exception
{
    public ProviderAccessDeniedException(string provider, string message)
        : base(message)
    {
        Provider = provider;
    }

    public string Provider { get; }
}
