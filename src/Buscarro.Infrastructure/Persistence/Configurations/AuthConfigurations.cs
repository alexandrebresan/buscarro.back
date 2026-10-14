using Buscarro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Buscarro.Infrastructure.Persistence.Configurations;
internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> e)
    {
        e.ToTable("UserSessions");
        e.HasKey(x => x.Id);
        e.Property(x => x.AuthenticationMethod).HasMaxLength(32).IsRequired();
        e.Property(x => x.RevocationReason).HasMaxLength(64);
        e.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> e)
    {
        e.ToTable("RefreshTokens");
        e.HasKey(x => x.Id);
        e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        e.HasIndex(x => x.TokenHash).IsUnique();
        e.HasOne<UserSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
    }
}
internal sealed class UserExternalLoginConfiguration : IEntityTypeConfiguration<UserExternalLogin>
{
    public void Configure(EntityTypeBuilder<UserExternalLogin> e)
    {
        e.ToTable("UserExternalLogins");
        e.HasKey(x => x.Id);
        e.Property(x => x.Provider).HasMaxLength(32).IsRequired();
        e.Property(x => x.Subject).HasMaxLength(255).IsRequired();
        e.HasIndex(x => new { x.Provider, x.Subject }).IsUnique();
        e.HasIndex(x => new { x.UserId, x.Provider }).IsUnique();
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
internal sealed class GoogleAuthChallengeConfiguration : IEntityTypeConfiguration<GoogleAuthChallenge>
{
    public void Configure(EntityTypeBuilder<GoogleAuthChallenge> e)
    {
        e.ToTable("GoogleAuthChallenges");
        e.HasKey(x => x.Id);
        e.Property(x => x.NonceHash).HasMaxLength(64).IsRequired();
        e.Property(x => x.BrowserBindingHash).HasMaxLength(64).IsRequired();
        e.HasIndex(x => x.ExpiresAtUtc);
    }
}
