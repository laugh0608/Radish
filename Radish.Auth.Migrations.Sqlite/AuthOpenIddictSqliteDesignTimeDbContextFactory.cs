using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Radish.Auth.OpenIddict;

namespace Radish.Auth.Migrations.Sqlite;

public sealed class AuthOpenIddictSqliteDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<AuthOpenIddictDbContext>
{
    public AuthOpenIddictDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("RADISH_EF_CONNECTION_STRING")?.Trim();
        var options = new DbContextOptionsBuilder<AuthOpenIddictDbContext>();
        options.UseSqlite(
            string.IsNullOrWhiteSpace(connectionString)
                ? "Data Source=radish-openiddict-design.db"
                : connectionString,
            provider => provider.MigrationsAssembly(AuthOpenIddictPersistence.SqliteMigrationsAssembly));
        return new AuthOpenIddictDbContext(options.Options);
    }
}
