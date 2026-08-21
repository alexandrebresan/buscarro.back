using Buscarro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Buscarro.Infrastructure.Persistence.Configurations;

internal sealed class CatalogPublicationConfiguration : IEntityTypeConfiguration<CatalogPublication>
{
    public void Configure(EntityTypeBuilder<CatalogPublication> entity)
    {
        entity.ToTable("catalog_publication", table =>
            table.HasCheckConstraint("ck_catalog_publication_counts", "total_catalog_count >= 0 AND provider_count >= 0"));
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(value => value.PublishedAtUtc).HasColumnName("published_at_utc").HasColumnType("timestamp with time zone");
        entity.Property(value => value.TotalCatalogCount).HasColumnName("total_catalog_count");
        entity.Property(value => value.ProviderCount).HasColumnName("provider_count");
        entity.HasIndex(value => value.PublishedAtUtc).HasDatabaseName("ix_catalog_publication_published_at");
    }
}
