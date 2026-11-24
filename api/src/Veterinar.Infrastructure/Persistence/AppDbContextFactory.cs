using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Veterinar.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("DATABASE_PROVIDER")?.ToLowerInvariant() ?? "sqlite";
        var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING") ?? "Data Source=veterinar.design.db";

        var options = new DbContextOptionsBuilder<AppDbContext>();
        if (provider != "sqlite") throw new InvalidOperationException("SQLite is the supported provider.");
        options.UseSqlite(connectionString);

        return new AppDbContext(options.Options);
    }
}
