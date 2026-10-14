using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Buscarro.Application.DTOs;
using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Buscarro.Tests;

public sealed class PostgresAuthHttpTests
{
    [PostgresIntegrationFact]
    [Trait("Category", "PostgreSqlIntegration")]
    public async Task Encrypted_keys_and_csrf_work_across_replicas_and_restart()
    {
        await using var database = new PostgresTestDatabaseFixture();
        await database.InitializeAsync();
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=BuscarroTests", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var path = Path.Combine(Path.GetTempPath(), $"buscarro-auth-test-{Guid.NewGuid():N}.pfx");
        await File.WriteAllBytesAsync(path, cert.Export(X509ContentType.Pfx));
        try
        {
            await using var first = await AuthHttpFixture.StartAsync(database.ConnectionString, path);
            await first.BootstrapCsrfAsync();
            using var registered = await first.PostAsync("/api/auth/register", new
            {
                name = "Alex",
                email = "alex@example.com",
                password = "secret"
            });
            Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
            var auth = (await registered.Content.ReadFromJsonAsync<AuthResponse>())!;
            await using (var scope = first.App.Services.CreateAsyncScope())
            {
                var keys = await scope.ServiceProvider.GetRequiredService<BuscarroDbContext>().DataProtectionKeys.ToListAsync();
                Assert.NotEmpty(keys);
                Assert.All(keys, key => Assert.Contains("encryptedSecret", key.Xml));
            }
            await using var second = await AuthHttpFixture.StartAsync(database.ConnectionString, path);
            second.CopyBrowserFrom(first);
            using var refresh = await second.PostAsync("/api/auth/refresh", new
            {
            }, first.CreateExpiredToken(auth));
            Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
            var renewed = (await refresh.Content.ReadFromJsonAsync<AuthResponse>())!;
            Assert.Equal(auth.User.Id, renewed.User.Id);
            await first.App.StopAsync();
            await using var restarted = await AuthHttpFixture.StartAsync(database.ConnectionString, path);
            restarted.CopyBrowserFrom(second);
            using var logout = await restarted.PostAsync("/api/auth/logout", new
            {
            });
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
            using var denied = await second.GetAsync("/api/favorites", renewed.Token);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }
        finally { File.Delete(path); }
    }
}
