using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
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

            db.Insertable(CreateOperation(1)).ExecuteCommand();
            Assert.ThrowsAny<Exception>(() => db.Insertable(CreateOperation(2)).ExecuteCommand());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static ShopEntitlementOperation CreateOperation(long id)
    {
        return new ShopEntitlementOperation
        {
            Id = id,
            TenantId = 1,
            UserId = 100,
            InventoryId = 200,
            OperationType = ShopEntitlementOperationTypes.Use,
            ConsumableType = ConsumableType.ExpCard,
            Quantity = 1,
            ItemValue = "100",
            IdempotencyKey = "same-key",
            RequestHash = "request-hash",
            EffectType = ShopEntitlementEffectTypes.Experience,
            EffectValue = "100",
            ResultPayload = "{}",
            CreateTime = new DateTime(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc),
            CreateBy = "test",
            CreateId = 100
        };
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
