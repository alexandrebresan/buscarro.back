using Buscarro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Buscarro.Infrastructure.Persistence.Configurations;

internal sealed class VehicleProviderVersionConfiguration : IEntityTypeConfiguration<VehicleProviderVersion>
{
    public void Configure(EntityTypeBuilder<VehicleProviderVersion> entity)
    {
        entity.ToTable("VehicleProviderVersions");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.ProviderName).HasMaxLength(80).IsRequired();
        entity.Property(value => value.ProviderCode).HasMaxLength(80).IsRequired();
        entity.Property(value => value.Name).HasMaxLength(200).IsRequired();
        entity.HasIndex(value => new { value.ProviderName, value.VehicleType, value.ModelId, value.ProviderCode }).IsUnique();
        entity.HasOne(value => value.Model).WithMany(value => value.Versions)
            .HasForeignKey(value => value.ModelId).OnDelete(DeleteBehavior.Cascade);
    }
}
