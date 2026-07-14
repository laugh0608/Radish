using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ShopEntitlementOperationSchemaMigrationTest
{
    private const string PostgreSqlConnectionStringEnvironmentVariable = "RADISH_TEST_POSTGRES_CONNECTION_STRING";

    [Fact]
    public void Apply_ShouldCreateIdempotentOperationLedgerAndIndexes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-shop-operation-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);
        using var services = new ServiceCollection().BuildServiceProvider();

        try
        {
            var migration = ShopEntitlementOperationSchemaMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.True(db.DbMaintenance.IsAnyTable("ShopEntitlementOperation", false));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_shop_entitlement_operation_idempotency"));

            InsertOperation(db, 1);
            Assert.ThrowsAny<Exception>(() => InsertOperation(db, 2));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SubjectNullabilityApply_ShouldPreserveConsumableRowsAndAllowBenefitRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-shop-operation-nullable-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);
        using var services = CreateServices();

        try
        {
            ShopEntitlementOperationSchemaMigration.Instance.Apply(db, services);
            UserActiveBenefitSchemaMigration.Instance.Apply(db, services);
            InsertOperation(db, 1);
            Assert.False(IsSqliteColumnNullable(db, "InventoryId"));
            Assert.False(IsSqliteColumnNullable(db, "ConsumableType"));
            Assert.False(IsSqliteColumnNullable(db, "Quantity"));
            Assert.False(IsSqliteColumnNullable(db, "ItemValue"));

            var migration = ShopEntitlementOperationSubjectNullabilityMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(*) FROM \"ShopEntitlementOperation\" WHERE \"Id\" = 1"));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_shop_entitlement_operation_idempotency"));
            Assert.True(db.DbMaintenance.IsAnyIndex("idx_shop_entitlement_operation_benefit_time"));
            InsertBenefitOperation(db, 3);
            Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(*) FROM \"ShopEntitlementOperation\" WHERE \"Id\" = 3"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    [Trait("Database", "PostgreSQL")]
    public async Task SubjectNullabilityApply_ShouldSupportPostgreSql()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable(PostgreSqlConnectionStringEnvironmentVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(adminConnectionString),
            $"未配置 {PostgreSqlConnectionStringEnvironmentVariable}，跳过商城通用流水 PostgreSQL 迁移测试");

        var schema = $"shop_operation_nullable_{Guid.NewGuid():N}";
        using var adminDb = CreatePostgreSqlClient(adminConnectionString!);
        await adminDb.Ado.ExecuteCommandAsync($"CREATE SCHEMA {QuoteIdentifier(schema)}");
        try
        {
            var connectionString = $"{adminConnectionString!.Trim().TrimEnd(';')};Search Path={schema};Pooling=false";
            using var db = CreatePostgreSqlClient(connectionString);
            using var services = CreateServices();
            ShopEntitlementOperationSchemaMigration.Instance.Apply(db, services);
            UserActiveBenefitSchemaMigration.Instance.Apply(db, services);
            InsertOperation(db, 1);

            var migration = ShopEntitlementOperationSubjectNullabilityMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(*) FROM \"ShopEntitlementOperation\" WHERE \"Id\" = 1"));
            InsertBenefitOperation(db, 3);
            Assert.Equal(1, db.Ado.GetInt("SELECT COUNT(*) FROM \"ShopEntitlementOperation\" WHERE \"Id\" = 3"));
        }
        finally
        {
            await adminDb.Ado.ExecuteCommandAsync($"DROP SCHEMA IF EXISTS {QuoteIdentifier(schema)} CASCADE");
        }
    }

    private static void InsertOperation(ISqlSugarClient db, long id)
    {
        db.Ado.ExecuteCommand(
            "INSERT INTO \"ShopEntitlementOperation\" " +
            "(\"Id\", \"TenantId\", \"UserId\", \"InventoryId\", \"OperationType\", " +
            "\"ConsumableType\", \"Quantity\", \"ItemValue\", \"IdempotencyKey\", \"RequestHash\", " +
            "\"EffectType\", \"EffectValue\", \"ResultPayload\", \"CreateTime\", \"CreateBy\", \"CreateId\") VALUES " +
            "(@Id, 1, 100, 200, 'Use', 4, 1, '100', 'same-key', 'request-hash', " +
            "'ExperienceGrant', '100', '{}', '2026-07-13 08:00:00', 'test', 100)",
            new SugarParameter("@Id", id));
    }

    private static void InsertBenefitOperation(ISqlSugarClient db, long id)
    {
        db.Ado.ExecuteCommand(
            "INSERT INTO \"ShopEntitlementOperation\" " +
            "(\"Id\", \"TenantId\", \"UserId\", \"BenefitId\", \"OperationType\", " +
            "\"BenefitType\", \"BenefitValue\", \"IdempotencyKey\", \"RequestHash\", " +
            "\"EffectType\", \"EffectValue\", \"ResultPayload\", \"CreateTime\", \"CreateBy\", \"CreateId\") VALUES " +
            "(@Id, 1, 100, 300, 'Activate', 1, 'badge-veteran', 'benefit-key', 'request-hash', " +
            "'BenefitSelection', 'badge-veteran', '{}', '2026-07-14 08:00:00', 'test', 100)",
            new SugarParameter("@Id", id));
    }

    private static bool IsSqliteColumnNullable(ISqlSugarClient db, string columnName)
    {
        var table = db.Ado.GetDataTable("PRAGMA table_info(\"ShopEntitlementOperation\")");
        var row = table.Rows.Cast<System.Data.DataRow>().Single(item =>
            string.Equals(item["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase));
        return Convert.ToInt32(row["notnull"]) == 0;
    }

    private static ServiceProvider CreateServices()
    {
        return new ServiceCollection()
            .AddSingleton(TimeProvider.System)
            .BuildServiceProvider();
    }

    private static SqlSugarScope CreateClient(string path)
    {
        return new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "Main",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static SqlSugarClient CreatePostgreSqlClient(string connectionString)
    {
        return PostgreSqlIntegrationSqlSugarFactory.CreateClient(new ConnectionConfig
        {
            ConfigId = "Main",
            ConnectionString = connectionString,
            DbType = DbType.PostgreSQL,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
