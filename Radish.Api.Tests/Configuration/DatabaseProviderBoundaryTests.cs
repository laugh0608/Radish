using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Radish.Api.Tests.TestCollections;
using Radish.Common;
using Radish.Common.CoreTool;
using Radish.Common.DbTool;
using Radish.DbMigrate;
using Radish.Infrastructure.Tenant;
using Xunit;

namespace Radish.Api.Tests.Configuration;

[Collection(AppSettingsStateCollection.CollectionName)]
public sealed class DatabaseProviderBoundaryTests : IDisposable
{
    private const string SecretConnection = "Host=private.invalid;Password=must-not-appear";
    private readonly IConfiguration _previousConfiguration = AppSettingsTool.Configuration;
    private readonly IConfiguration _previousRuntimeConfiguration = App.Configuration;
    private readonly string _previousMainDb = MainDb.CurrentDbConnId;

    public static TheoryData<int, string, int> UnsupportedBusinessProviders
    {
        get
        {
            var rows = new TheoryData<int, string, int>();
            var connections = new[] { "Main", "Log", "Message", "Chat" };
            for (var index = 0; index < connections.Length; index++)
            {
                foreach (var provider in new[] { 0, 1, 3, 5, 6, -1, 999 })
                {
                    rows.Add(index, connections[index], provider);
                }
            }

            return rows;
        }
    }

    [Theory]
    [MemberData(nameof(UnsupportedBusinessProviders))]
    public void BusinessConnections_RejectUnsupportedProviderBeforeProcessingAnySqlitePath(
        int index, string connectionId, int provider)
    {
        var values = new Dictionary<string, string?> { ["MainDb"] = "Main" };
        var connections = new[] { "Main", "Log", "Message", "Chat" };
        for (var current = 0; current < connections.Length; current++)
        {
            values[$"Databases:{current}:ConnId"] = connections[current];
            values[$"Databases:{current}:Enabled"] = "true";
            values[$"Databases:{current}:DbType"] = "2";
            // 如果校验前处理了任一 SQLite 路径，Path.Combine 将先抛出路径异常。
            values[$"Databases:{current}:ConnectionString"] = "invalid\0path.db";
        }

        values[$"Databases:{index}:DbType"] = provider.ToString();
        values[$"Databases:{index}:ConnectionString"] = SecretConnection;
        AppSettingsTool.Configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        Assert.Equal(connectionId, AppSettingsTool.Configuration[$"Databases:{index}:ConnId"]);

        var exception = Assert.Throws<InvalidOperationException>(() => BaseDbConfig.MutiInitConn());

        Assert.Contains($"Databases:{index}:DbType=", exception.Message, StringComparison.Ordinal);
        Assert.Contains("仅支持 Sqlite (2) 和 PostgreSql (4)", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("private.invalid", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("must-not-appear", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BusinessConnections_IgnoreDisabledLegacyProviderAndKeepMainFirst()
    {
        AppSettingsTool.Configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["MainDb"] = "Main",
                ["Databases:0:ConnId"] = "Log",
                ["Databases:0:Enabled"] = "true",
                ["Databases:0:DbType"] = "PostgreSql",
                ["Databases:0:ConnectionString"] = SecretConnection,
                ["Databases:1:ConnId"] = "Legacy",
                ["Databases:1:Enabled"] = "false",
                ["Databases:1:DbType"] = "Oracle",
                ["Databases:1:ConnectionString"] = SecretConnection,
                ["Databases:2:ConnId"] = "Main",
                ["Databases:2:Enabled"] = "true",
                ["Databases:2:DbType"] = "4",
                ["Databases:2:ConnectionString"] = SecretConnection
            }).Build();

        var (connections, slaves) = BaseDbConfig.MutiInitConn();

        Assert.Collection(connections,
            main => Assert.Equal("Main", main.ConnId),
            log => Assert.Equal("Log", log.ConnId));
        Assert.All(connections, connection =>
        {
            Assert.Equal(DataBaseType.PostgreSql, connection.DbType);
            Assert.Equal(SecretConnection, connection.ConnectionString);
        });
        Assert.Empty(slaves);
    }

    [Fact]
    public void DbMigrateRegistration_RejectsUnsupportedProviderBeforeRegisteringConnections()
    {
        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MainDb"] = "Main",
            ["Databases:0:ConnId"] = "Main",
            ["Databases:0:Enabled"] = "true",
            ["Databases:0:DbType"] = "1",
            ["Databases:0:ConnectionString"] = SecretConnection
        });
        var connectionCount = BaseDbConfig.AllConfigs.Count;
        InternalApp.ConfigureApplication(builder.Configuration);

        var exception = Assert.Throws<InvalidOperationException>(() => DbMigrateBootstrap.ConfigureServices(builder));

        Assert.Contains("Databases:0:DbType=SqlServer", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("must-not-appear", exception.Message, StringComparison.Ordinal);
        Assert.Equal(connectionCount, BaseDbConfig.AllConfigs.Count);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("3")]
    [InlineData("5")]
    [InlineData("6")]
    [InlineData("Oracle")]
    [InlineData("-1")]
    [InlineData("999")]
    [InlineData("MySql, PostgreSql")]
    [InlineData("private.invalid;Password=must-not-appear")]
    public void RuntimeStores_RejectUnsupportedOrInvalidProviderBeforeUsingConnectionString(string provider)
    {
        foreach (var section in new[] { "OpenIddict:Database", "Hangfire" })
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{section}:DbType"] = provider,
                [$"{section}:ConnectionString"] = SecretConnection
            }).Build();

            var exception = Assert.Throws<InvalidOperationException>(() =>
                RuntimeDatabaseConfigResolver.Resolve(configuration, section, null, "invalid\0path.db"));

            Assert.Contains($"{section}:DbType", exception.Message, StringComparison.Ordinal);
            Assert.Contains("仅支持 Sqlite (2) 和 PostgreSql (4)", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("private.invalid", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("must-not-appear", exception.Message, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("2", DataBaseType.Sqlite)]
    [InlineData("sqlite", DataBaseType.Sqlite)]
    [InlineData("4", DataBaseType.PostgreSql)]
    [InlineData("pOsTgReSqL", DataBaseType.PostgreSql)]
    public void RuntimeStores_KeepSupportedProviderAndConnection(string provider, DataBaseType expected)
    {
        var filePath = Path.Combine(Path.GetTempPath(), "radish-provider-boundary-unused.db");
        var connection = expected == DataBaseType.Sqlite ? $"Data Source={filePath}" : SecretConnection;
        foreach (var section in new[] { "OpenIddict:Database", "Hangfire" })
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{section}:DbType"] = provider,
                [$"{section}:ConnectionString"] = connection
            }).Build();

            var result = RuntimeDatabaseConfigResolver.Resolve(configuration, section, null, "unused.db");

            Assert.Equal(expected, result.DbType);
            Assert.Equal(connection, result.ConnectionString);
        }
    }

    [Fact]
    public void RuntimeStores_KeepLegacyProviderInferenceAndDefaultSqlite()
    {
        var configuration = new ConfigurationBuilder().Build();
        var pg = RuntimeDatabaseConfigResolver.Resolve(configuration, "Hangfire", SecretConnection, "unused.db");
        var sqlite = RuntimeDatabaseConfigResolver.Resolve(configuration, "OpenIddict:Database", null, "unused.db");

        Assert.Equal(DataBaseType.PostgreSql, pg.DbType);
        Assert.Equal(SecretConnection, pg.ConnectionString);
        Assert.Equal(DataBaseType.Sqlite, sqlite.DbType);
        Assert.EndsWith(Path.Combine("DataBases", "unused.db"), sqlite.ConnectionString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(-1)]
    [InlineData(999)]
    public void SeparateTenant_RejectsUnsupportedProviderWithoutExposingDatabaseDetails(int provider)
    {
        var tenant = new Radish.Model.Tenant
        {
            DbType = (SqlSugar.DbType)provider,
            TenantConfigId = "private-tenant-id",
            DbConnectionStr = SecretConnection
        };

        var exception = Assert.Throws<InvalidOperationException>(() => tenant.GetConnectionConfig());

        Assert.Contains("Tenant:DbType=", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("private-tenant-id", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("must-not-appear", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SqlSugar.DbType.Sqlite)]
    [InlineData(SqlSugar.DbType.PostgreSQL)]
    public void SeparateTenant_KeepsSupportedProviderIdentityAndConnection(SqlSugar.DbType provider)
    {
        var tenant = new Radish.Model.Tenant
        {
            DbType = provider,
            TenantConfigId = "isolated-tenant",
            DbConnectionStr = SecretConnection
        };

        var config = tenant.GetConnectionConfig();

        Assert.Equal(provider, config.DbType);
        Assert.Equal("isolated-tenant", config.ConfigId);
        Assert.Equal(SecretConnection, config.ConnectionString);
        Assert.True(config.MoreSettings.PgSqlIsAutoToLower);
        Assert.True(config.MoreSettings.PgSqlIsAutoToLowerCodeFirst);
    }

    [Fact]
    public void SeparateTenant_MissingProviderRemainsAnExplicitFailure()
    {
        Assert.Throws<ArgumentException>(() => new Radish.Model.Tenant { DbType = null }.GetConnectionConfig());
    }

    public void Dispose()
    {
        AppSettingsTool.Configuration = _previousConfiguration;
        InternalApp.ConfigureApplication(_previousRuntimeConfiguration);
        MainDb.CurrentDbConnId = _previousMainDb;
    }
}
