using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Buscarro.Infrastructure.Persistence;

// Model construction does not contact PostgreSQL or require runtime authentication secrets.
public sealed class BuscarroDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BuscarroDbContext>
{
    public BuscarroDbContext CreateDbContext(string[] args)
    {
        var connectionArgument = Array.IndexOf(args, "--connection");
        var connectionString = connectionArgument >= 0 && connectionArgument + 1 < args.Length
            ? args[connectionArgument + 1]
            : Environment.GetEnvironmentVariable("DATABASE_URL")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__BuscarroDb");

        return new BuscarroDbContext(new DbContextOptionsBuilder<BuscarroDbContext>()
            .UseNpgsql(PostgresConnectionString.Normalize(connectionString ?? string.Empty))
            .Options);
    }
}
