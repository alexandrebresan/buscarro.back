namespace Buscarro.Application.Exceptions;
public sealed class AuthFlowException(string code, int statusCode = 401) : Exception("Authentication request could not be completed.")
{
    public string Code { get; } = code; public int StatusCode { get; } = statusCode;
}
