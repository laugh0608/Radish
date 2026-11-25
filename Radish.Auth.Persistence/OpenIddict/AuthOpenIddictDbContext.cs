using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace Radish.Auth.OpenIddict;

/// <summary>
/// EF Core DbContext 承载 OpenIddict 数据与普通 Web 的服务端会话。
/// 仅负责身份/授权相关数据，业务数据仍由 SqlSugar 负责。
/// </summary>
public class AuthOpenIddictDbContext : DbContext
{
    public AuthOpenIddictDbContext(DbContextOptions<AuthOpenIddictDbContext> options)
        : base(options)
    {
    }

    public DbSet<WebAuthSession> WebAuthSessions => Set<WebAuthSession>();
    public DbSet<WebAuthLogin> WebAuthLogins => Set<WebAuthLogin>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // 注册 OpenIddict 默认实体模型（使用内置实体类型和映射）
        builder.UseOpenIddict();

        builder.Entity<WebAuthSession>(session =>
        {
            session.Property(item => item.Subject).HasMaxLength(400);
            session.Property(item => item.TenantId).HasMaxLength(64);
            session.Property(item => item.AuthorizationId).HasMaxLength(100);
            session.Property(item => item.CookieDigest).HasMaxLength(64);
            session.Property(item => item.Version).IsConcurrencyToken();
            session.HasIndex(item => item.AuthorizationId).IsUnique();
            session.HasIndex(item => item.CookieDigest).IsUnique();
        });

        builder.Entity<WebAuthLogin>(login =>
        {
            login.HasKey(item => item.StateDigest);
            login.Property(item => item.StateDigest).HasMaxLength(64);
            login.Property(item => item.BrowserDigest).HasMaxLength(64);
            login.HasIndex(item => item.ExpiresAtUtc);
        });

        if (Database.IsNpgsql())
        {
            ConfigurePostgreSqlTimestamps(builder);
        }
    }

    private static void ConfigurePostgreSqlTimestamps(ModelBuilder builder)
    {
        const string timestampWithTimeZone = "timestamp with time zone";

        builder.Entity<OpenIddictEntityFrameworkCoreAuthorization>()
            .Property(authorization => authorization.CreationDate)
            .HasColumnType(timestampWithTimeZone);

        builder.Entity<OpenIddictEntityFrameworkCoreToken>()
            .Property(token => token.CreationDate)
            .HasColumnType(timestampWithTimeZone);
        builder.Entity<OpenIddictEntityFrameworkCoreToken>()
            .Property(token => token.ExpirationDate)
            .HasColumnType(timestampWithTimeZone);
        builder.Entity<OpenIddictEntityFrameworkCoreToken>()
            .Property(token => token.RedemptionDate)
            .HasColumnType(timestampWithTimeZone);

        var session = builder.Entity<WebAuthSession>();
        session.Property(item => item.CreatedAtUtc).HasColumnType(timestampWithTimeZone);
        session.Property(item => item.LastActiveAtUtc).HasColumnType(timestampWithTimeZone);
        session.Property(item => item.ActivatedAtUtc).HasColumnType(timestampWithTimeZone);
        session.Property(item => item.RevokedAtUtc).HasColumnType(timestampWithTimeZone);
        session.Property(item => item.AccessTokenExpiresAtUtc).HasColumnType(timestampWithTimeZone);
        session.Property(item => item.RefreshLeaseUntilUtc).HasColumnType(timestampWithTimeZone);
        session.Property(item => item.RefreshRetryAtUtc).HasColumnType(timestampWithTimeZone);

        builder.Entity<WebAuthLogin>()
            .Property(login => login.ExpiresAtUtc)
            .HasColumnType(timestampWithTimeZone);
    }
}
