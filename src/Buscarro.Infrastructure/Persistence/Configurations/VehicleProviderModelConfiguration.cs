using Buscarro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Buscarro.Infrastructure.Persistence.Configurations;

internal sealed class VehicleProviderModelConfiguration : IEntityTypeConfiguration<VehicleProviderModel>
{
    public void Configure(EntityTypeBuilder<VehicleProviderModel> entity)
    {
        entity.ToTable("VehicleProviderModels");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.ProviderName).HasMaxLength(80).IsRequired();
        entity.Property(value => value.ProviderCode).HasMaxLength(80).IsRequired();
        entity.Property(value => value.Name).HasMaxLength(200).IsRequired();
        entity.HasIndex(value => new { value.ProviderName, value.VehicleType, value.ManufacturerId, value.ProviderCode }).IsUnique();
        entity.HasOne(value => value.Manufacturer).WithMany(value => value.Models)
            .HasForeignKey(value => value.ManufacturerId).OnDelete(DeleteBehavior.Cascade);
    }
}
