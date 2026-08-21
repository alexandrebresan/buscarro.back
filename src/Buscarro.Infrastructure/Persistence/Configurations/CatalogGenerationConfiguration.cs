using Buscarro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Buscarro.Infrastructure.Persistence.Configurations;

internal sealed class CatalogGenerationConfiguration : IEntityTypeConfiguration<CatalogGeneration>
{
    public void Configure(EntityTypeBuilder<CatalogGeneration> entity)
    {
        entity.ToTable("catalog_generation", table =>
        {
            table.HasCheckConstraint("ck_catalog_generation_counts", "page_count >= 0 AND source_item_count >= 0 AND valid_item_count >= 0 AND invalid_item_count >= 0 AND duplicate_item_count >= 0");
            table.HasCheckConstraint("ck_catalog_generation_checksum", "checksum_sha256 IS NULL OR length(checksum_sha256) = 64");
            table.HasCheckConstraint("ck_catalog_generation_provider_not_blank", "btrim(provider) <> ''");
        });
        entity.HasKey(value => value.Id);
        entity.HasAlternateKey(value => new { value.Id, value.Provider });
        entity.Property(value => value.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(value => value.Provider).HasColumnName("provider").HasMaxLength(80).IsRequired();
        entity.Property(value => value.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        entity.Property(value => value.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        entity.Property(value => value.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");
        entity.Property(value => value.ReadyAtUtc).HasColumnName("ready_at_utc").HasColumnType("timestamp with time zone");
        entity.Property(value => value.ActivatedAtUtc).HasColumnName("activated_at_utc").HasColumnType("timestamp with time zone");
        entity.Property(value => value.FailedAtUtc).HasColumnName("failed_at_utc").HasColumnType("timestamp with time zone");
        entity.Property(value => value.PageCount).HasColumnName("page_count");
        entity.Property(value => value.SourceItemCount).HasColumnName("source_item_count");
        entity.Property(value => value.ValidItemCount).HasColumnName("valid_item_count");
        entity.Property(value => value.InvalidItemCount).HasColumnName("invalid_item_count");
        entity.Property(value => value.DuplicateItemCount).HasColumnName("duplicate_item_count");
        entity.Property(value => value.ChecksumSha256).HasColumnName("checksum_sha256").HasColumnType("character(64)").HasMaxLength(64);
        entity.Property(value => value.ValidationSummaryJson).HasColumnName("validation_summary_json").HasColumnType("jsonb");
        entity.Property(value => value.SanitizedError).HasColumnName("sanitized_error").HasMaxLength(2000);
        entity.HasIndex(value => new { value.Provider, value.Status, value.CreatedAtUtc })
            .HasDatabaseName("ix_catalog_generation_provider_status_created_at");
    }
}
