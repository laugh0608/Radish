using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Radish.Auth.OpenIddict;

namespace Radish.Auth.Migrations.PostgreSql;

public sealed class AuthOpenIddictPostgreSqlDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<AuthOpenIddictDbContext>
{
    public AuthOpenIddictDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("RADISH_EF_CONNECTION_STRING")?.Trim();
        var options = new DbContextOptionsBuilder<AuthOpenIddictDbContext>();
        options.UseNpgsql(
            string.IsNullOrWhiteSpace(connectionString)
                ? "Host=127.0.0.1;Port=5432;Database=radish_openiddict_design;Username=postgres;Password=postgres"
                : connectionString,
            provider => provider.MigrationsAssembly(AuthOpenIddictPersistence.PostgreSqlMigrationsAssembly));
        return new AuthOpenIddictDbContext(options.Options);
    }
}
