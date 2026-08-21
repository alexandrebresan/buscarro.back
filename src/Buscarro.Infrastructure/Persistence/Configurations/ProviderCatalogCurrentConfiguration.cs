using Buscarro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Buscarro.Infrastructure.Persistence.Configurations;

internal sealed class ProviderCatalogCurrentConfiguration : IEntityTypeConfiguration<ProviderCatalogCurrent>
{
    public void Configure(EntityTypeBuilder<ProviderCatalogCurrent> entity)
    {
        entity.ToTable("provider_catalog_current", table =>
        {
            table.HasCheckConstraint("ck_provider_catalog_current_distinct", "previous_generation_id IS NULL OR current_generation_id <> previous_generation_id");
            table.HasCheckConstraint("ck_provider_catalog_current_provider_not_blank", "btrim(provider) <> ''");
        });
        entity.HasKey(value => value.Provider);
        entity.Property(value => value.Provider).HasColumnName("provider").HasMaxLength(80);
        entity.Property(value => value.CurrentGenerationId).HasColumnName("current_generation_id");
        entity.Property(value => value.PreviousGenerationId).HasColumnName("previous_generation_id");
        entity.Property(value => value.LastPublicationId).HasColumnName("last_publication_id");
        entity.Property(value => value.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        entity.HasOne(value => value.CurrentGeneration).WithMany()
            .HasForeignKey(value => new { value.CurrentGenerationId, value.Provider })
            .HasPrincipalKey(value => new { value.Id, value.Provider })
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(value => value.PreviousGeneration).WithMany()
            .HasForeignKey(value => new { value.PreviousGenerationId, value.Provider })
            .HasPrincipalKey(value => new { value.Id, value.Provider })
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(value => value.LastPublication).WithMany()
            .HasForeignKey(value => value.LastPublicationId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(value => value.CurrentGenerationId);
        entity.HasIndex(value => value.PreviousGenerationId);
        entity.HasIndex(value => value.LastPublicationId);
    }
}
