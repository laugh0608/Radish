using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ShopEntitlementOperationSchemaMigrationTest
{
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
}
