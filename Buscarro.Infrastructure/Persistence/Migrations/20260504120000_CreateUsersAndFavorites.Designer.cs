using Buscarro.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Buscarro.Infrastructure.Persistence.Migrations;

[DbContext(typeof(BuscarroDbContext))]
[Migration("20260504120000_CreateUsersAndFavorites")]
partial class CreateUsersAndFavorites
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "9.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

        modelBuilder.Entity("Buscarro.Domain.Entities.FavoriteVehicle", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Brand")
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnType("nvarchar(120)");

            entity.Property<string>("City")
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnType("nvarchar(120)");

            entity.Property<string>("Color")
                .IsRequired()
                .HasMaxLength(80)
                .HasColumnType("nvarchar(80)");

            entity.Property<DateTime>("CreatedAtUtc")
                .HasColumnType("datetime2");

            entity.Property<string>("ExternalId")
                .IsRequired()
                .HasMaxLength(160)
                .HasColumnType("nvarchar(160)");

            entity.Property<string>("Fuel")
                .IsRequired()
                .HasMaxLength(80)
                .HasColumnType("nvarchar(80)");

            entity.Property<string>("ImageUrl")
                .IsRequired()
                .HasMaxLength(1000)
                .HasColumnType("nvarchar(1000)");

            entity.Property<int>("Mileage")
                .HasColumnType("int");

            entity.Property<string>("Model")
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnType("nvarchar(120)");

            entity.Property<decimal>("Price")
                .HasPrecision(18, 2)
                .HasColumnType("decimal(18,2)");

            entity.Property<string>("Provider")
                .IsRequired()
                .HasMaxLength(80)
                .HasColumnType("nvarchar(80)");

            entity.Property<string>("State")
                .IsRequired()
                .HasMaxLength(80)
                .HasColumnType("nvarchar(80)");

            entity.Property<string>("Title")
                .IsRequired()
                .HasMaxLength(300)
                .HasColumnType("nvarchar(300)");

            entity.Property<string>("Transmission")
                .IsRequired()
                .HasMaxLength(80)
                .HasColumnType("nvarchar(80)");

            entity.Property<string>("Url")
                .IsRequired()
                .HasMaxLength(1000)
                .HasColumnType("nvarchar(1000)");

            entity.Property<Guid>("UserId")
                .HasColumnType("uniqueidentifier");

            entity.Property<string>("Version")
                .IsRequired()
                .HasMaxLength(160)
                .HasColumnType("nvarchar(160)");

            entity.Property<int>("Year")
                .HasColumnType("int");

            entity.HasKey("Id");

            entity.HasIndex("UserId", "Provider", "ExternalId")
                .IsUnique();

            entity.ToTable("FavoriteVehicles", (string)null);
        });

        modelBuilder.Entity("Buscarro.Domain.Entities.User", entity =>
        {
            entity.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("uniqueidentifier");

            entity.Property<DateTime>("CreatedAtUtc")
                .HasColumnType("datetime2");

            entity.Property<string>("Email")
                .IsRequired()
                .HasMaxLength(256)
                .HasColumnType("nvarchar(256)");

            entity.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(120)
                .HasColumnType("nvarchar(120)");

            entity.Property<string>("PasswordHash")
                .IsRequired()
                .HasMaxLength(512)
                .HasColumnType("nvarchar(512)");

            entity.HasKey("Id");

            entity.HasIndex("Email")
                .IsUnique();

            entity.ToTable("Users", (string)null);
        });

        modelBuilder.Entity("Buscarro.Domain.Entities.FavoriteVehicle", entity =>
        {
            entity.HasOne("Buscarro.Domain.Entities.User", "User")
                .WithMany("Favorites")
                .HasForeignKey("UserId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            entity.Navigation("User");
        });

        modelBuilder.Entity("Buscarro.Domain.Entities.User", entity =>
        {
            entity.Navigation("Favorites");
        });
#pragma warning restore 612, 618
    }
}
