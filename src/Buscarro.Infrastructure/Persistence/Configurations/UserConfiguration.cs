using Buscarro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Buscarro.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("Users");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Name).HasMaxLength(120).IsRequired();
        entity.Property(value => value.Email).HasMaxLength(256).IsRequired();
        entity.Property(value => value.PasswordHash).HasMaxLength(512).IsRequired();
        entity.HasIndex(value => value.Email).IsUnique();
    }
}
