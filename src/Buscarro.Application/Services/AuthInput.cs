using Buscarro.Application.Exceptions;
namespace Buscarro.Application.Services;
internal static class AuthInput
{
    internal static string Required(string? value, string field, int max = 512)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max)
            throw new ValidationException($"{field} invalido.");
        return value.Trim();
    }
    internal static string Email(string? value)
    {
        var email = Required(value, "E-mail", 256).ToLowerInvariant();
        if (!System.Net.Mail.MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw new ValidationException("E-mail invalido.");
        return email;
    }
}
