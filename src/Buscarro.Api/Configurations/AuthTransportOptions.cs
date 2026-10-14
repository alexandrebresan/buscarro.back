namespace Buscarro.Api.Configurations;

public sealed class AuthTransportOptions
{
    public string[] AllowedOrigins { get; set; } = [];
    public string DataProtectionCertificatePath { get; set; } = string.Empty;
    public string DataProtectionCertificatePassword { get; set; } = string.Empty;
}
