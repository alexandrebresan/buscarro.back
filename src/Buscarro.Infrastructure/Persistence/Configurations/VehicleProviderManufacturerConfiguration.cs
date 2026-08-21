using Buscarro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Buscarro.Infrastructure.Persistence.Configurations;

internal sealed class VehicleProviderManufacturerConfiguration : IEntityTypeConfiguration<VehicleProviderManufacturer>
{
    public void Configure(EntityTypeBuilder<VehicleProviderManufacturer> entity)
    {
        entity.ToTable("VehicleProviderManufacturers");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.ProviderName).HasMaxLength(80).IsRequired();
        entity.Property(value => value.ProviderCode).HasMaxLength(80).IsRequired();
        entity.Property(value => value.Name).HasMaxLength(200).IsRequired();
        entity.HasIndex(value => new { value.ProviderName, value.VehicleType, value.ProviderCode }).IsUnique();
    }
}
