namespace Buscarro.Application.Exceptions;

public sealed class CurrentUserRequiredException : Exception
{
    public CurrentUserRequiredException()
        : base("Usuario autenticado nao encontrado.")
    {
    }
}
