namespace Buscarro.Infrastructure.Providers;

public sealed class CarzinOptions
{
    public string BaseUrl { get; set; } = "https://carzin.com.br";
    public string Token { get; set; } = string.Empty;
}
