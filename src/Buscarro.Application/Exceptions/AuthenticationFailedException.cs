namespace Buscarro.Application.Exceptions;

public sealed class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException()
        : base("E-mail ou senha invalidos.")
    {
    }
}
