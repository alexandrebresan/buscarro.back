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
    public DbSet<CatalogGeneration> CatalogGenerations => Set<CatalogGeneration>();
    public DbSet<VehicleCatalogItem> VehicleCatalog => Set<VehicleCatalogItem>();
    public DbSet<ProviderCatalogCurrent> ProviderCatalogCurrents => Set<ProviderCatalogCurrent>();
    public DbSet<CatalogPublication> CatalogPublications => Set<CatalogPublication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BuscarroDbContext).Assembly);

        // The search vector is a PostgreSQL-only, infrastructure shadow property.
        // Keeping it out of non-relational test models avoids leaking provider types into Domain.
        if (!Database.IsNpgsql())
        {
            modelBuilder.Entity<VehicleCatalogItem>().Ignore("SearchVector");
        }
    }
}
