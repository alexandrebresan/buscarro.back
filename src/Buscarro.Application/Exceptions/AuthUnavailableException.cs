namespace Buscarro.Application.Exceptions;
public sealed class AuthUnavailableException(string code = "authentication_unavailable", Exception? inner = null) : Exception("Authentication service is temporarily unavailable.", inner)
{
    public string Code { get; } = code;
}
