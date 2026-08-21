using Buscarro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Buscarro.Infrastructure.Persistence.Configurations;

internal sealed class FavoriteVehicleConfiguration : IEntityTypeConfiguration<FavoriteVehicle>
{
    public void Configure(EntityTypeBuilder<FavoriteVehicle> entity)
    {
        entity.ToTable("FavoriteVehicles");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Provider).HasMaxLength(80).IsRequired();
        entity.Property(value => value.ExternalId).HasMaxLength(160).IsRequired();
        entity.Property(value => value.Title).HasMaxLength(300);
        entity.Property(value => value.Brand).HasMaxLength(120);
        entity.Property(value => value.Model).HasMaxLength(120);
        entity.Property(value => value.Version).HasMaxLength(160);
        entity.Property(value => value.Fuel).HasMaxLength(80);
        entity.Property(value => value.Transmission).HasMaxLength(80);
        entity.Property(value => value.Color).HasMaxLength(80);
        entity.Property(value => value.City).HasMaxLength(120);
        entity.Property(value => value.State).HasMaxLength(80);
        entity.Property(value => value.Url).HasMaxLength(1000);
        entity.Property(value => value.ImageUrl).HasMaxLength(1000);
        entity.Property(value => value.Price).HasPrecision(18, 2);
        entity.HasIndex(value => new { value.UserId, value.Provider, value.ExternalId }).IsUnique();
        entity.HasOne(value => value.User).WithMany(value => value.Favorites)
            .HasForeignKey(value => value.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
