using Buscarro.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Buscarro.Infrastructure.Persistence;

public sealed class BuscarroDbContext : DbContext
{
    public BuscarroDbContext(DbContextOptions<BuscarroDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<FavoriteVehicle> FavoriteVehicles => Set<FavoriteVehicle>();
    public DbSet<VehicleProviderManufacturer> VehicleProviderManufacturers => Set<VehicleProviderManufacturer>();
    public DbSet<VehicleProviderModel> VehicleProviderModels => Set<VehicleProviderModel>();
    public DbSet<VehicleProviderVersion> VehicleProviderVersions => Set<VehicleProviderVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Name).HasMaxLength(120).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(256).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<FavoriteVehicle>(entity =>
        {
            entity.ToTable("FavoriteVehicles");
            entity.HasKey(favorite => favorite.Id);
            entity.Property(favorite => favorite.Provider).HasMaxLength(80).IsRequired();
            entity.Property(favorite => favorite.ExternalId).HasMaxLength(160).IsRequired();
            entity.Property(favorite => favorite.Title).HasMaxLength(300);
            entity.Property(favorite => favorite.Brand).HasMaxLength(120);
            entity.Property(favorite => favorite.Model).HasMaxLength(120);
            entity.Property(favorite => favorite.Version).HasMaxLength(160);
            entity.Property(favorite => favorite.Fuel).HasMaxLength(80);
            entity.Property(favorite => favorite.Transmission).HasMaxLength(80);
            entity.Property(favorite => favorite.Color).HasMaxLength(80);
            entity.Property(favorite => favorite.City).HasMaxLength(120);
            entity.Property(favorite => favorite.State).HasMaxLength(80);
            entity.Property(favorite => favorite.Url).HasMaxLength(1000);
            entity.Property(favorite => favorite.ImageUrl).HasMaxLength(1000);
            entity.Property(favorite => favorite.Price).HasPrecision(18, 2);
            entity.HasIndex(favorite => new
            {
                favorite.UserId,
                favorite.Provider,
                favorite.ExternalId
            }).IsUnique();
            entity.HasOne(favorite => favorite.User)
                .WithMany(user => user.Favorites)
                .HasForeignKey(favorite => favorite.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VehicleProviderManufacturer>(entity =>
        {
            entity.ToTable("VehicleProviderManufacturers");
            entity.HasKey(manufacturer => manufacturer.Id);
            entity.Property(manufacturer => manufacturer.ProviderName).HasMaxLength(80).IsRequired();
            entity.Property(manufacturer => manufacturer.ProviderCode).HasMaxLength(80).IsRequired();
            entity.Property(manufacturer => manufacturer.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(manufacturer => new
            {
                manufacturer.ProviderName,
                manufacturer.VehicleType,
                manufacturer.ProviderCode
            }).IsUnique();
        });

        modelBuilder.Entity<VehicleProviderModel>(entity =>
        {
            entity.ToTable("VehicleProviderModels");
            entity.HasKey(model => model.Id);
            entity.Property(model => model.ProviderName).HasMaxLength(80).IsRequired();
            entity.Property(model => model.ProviderCode).HasMaxLength(80).IsRequired();
            entity.Property(model => model.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(model => new
            {
                model.ProviderName,
                model.VehicleType,
                model.ManufacturerId,
                model.ProviderCode
            }).IsUnique();
            entity.HasOne(model => model.Manufacturer)
                .WithMany(manufacturer => manufacturer.Models)
                .HasForeignKey(model => model.ManufacturerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VehicleProviderVersion>(entity =>
        {
            entity.ToTable("VehicleProviderVersions");
            entity.HasKey(version => version.Id);
            entity.Property(version => version.ProviderName).HasMaxLength(80).IsRequired();
            entity.Property(version => version.ProviderCode).HasMaxLength(80).IsRequired();
            entity.Property(version => version.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(version => new
            {
                version.ProviderName,
                version.VehicleType,
                version.ModelId,
                version.ProviderCode
            }).IsUnique();
            entity.HasOne(version => version.Model)
                .WithMany(model => model.Versions)
                .HasForeignKey(version => version.ModelId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
