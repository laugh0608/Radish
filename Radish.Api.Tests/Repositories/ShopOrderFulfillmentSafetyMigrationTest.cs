using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ShopOrderFulfillmentSafetyMigrationTest
{
    [Fact]
    public void Apply_ShouldAddColumnsAndBackfillHistoricalOrderSemantics()
    {
        var path = CreateTemporaryDatabasePath();
        using var db = CreateClient(path);
        using var services = new ServiceCollection().BuildServiceProvider();

        try
        {
            CreateLegacyOrderTable(db);
            db.Ado.ExecuteCommand(
                "INSERT INTO \"ShopOrder\" " +
                "(\"Id\", \"Status\", \"PaidTime\", \"CoinTransactionId\", \"ProductType\", " +
                "\"DurationType\", \"BenefitExpiresAt\", \"UserBenefitId\") VALUES " +
                "(1, 5, NULL, NULL, 2, 0, NULL, NULL), " +
                "(2, 5, '2026-07-13 08:00:00', 2002, 2, 0, NULL, 9002), " +
                "(3, 2, '2026-07-13 08:00:00', 2003, 1, 2, '2026-08-01 00:00:00', 9003)");

            var migration = ShopOrderFulfillmentSafetyMigration.Instance;
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            Assert.Equal((int)OrderFailureStage.Payment, ReadInt(db, 1, "FailureStage"));
            Assert.Equal((int)OrderFailureStage.Fulfillment, ReadInt(db, 2, "FailureStage"));
            Assert.Equal(9002, ReadLong(db, 2, "GrantedInventoryId"));
            Assert.Equal(9003, ReadLong(db, 3, "GrantedBenefitId"));
            Assert.StartsWith(
                "2026-08-01",
                db.Ado.GetString("SELECT \"FixedExpiresAt\" FROM \"ShopOrder\" WHERE \"Id\" = 3"),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteTemporaryDatabase(path);
        }
    }

    [Fact]
    public void Verify_ShouldReportFulfillmentFailureWithoutPaymentEvidence()
    {
        var path = CreateTemporaryDatabasePath();
        using var db = CreateClient(path);
        using var services = new ServiceCollection().BuildServiceProvider();

        try
        {
            CreateLegacyOrderTable(db);
            var migration = ShopOrderFulfillmentSafetyMigration.Instance;
            migration.Apply(db, services);
            db.Ado.ExecuteCommand(
                "INSERT INTO \"ShopOrder\" " +
                "(\"Id\", \"Status\", \"FailureStage\", \"PaidTime\", \"CoinTransactionId\", " +
                "\"ProductType\", \"DurationType\") VALUES (10, 5, 2, NULL, NULL, 2, 0)");

            var issues = migration.Verify(db, services);

            Assert.Contains(issues, issue => issue.Contains("履约失败订单缺少基础支付证据", StringComparison.Ordinal));
        }
        finally
        {
            DeleteTemporaryDatabase(path);
        }
    }

    private static void CreateLegacyOrderTable(ISqlSugarClient db)
    {
        db.Ado.ExecuteCommand(
            """
            CREATE TABLE "ShopOrder" (
                "Id" INTEGER NOT NULL PRIMARY KEY,
                "Status" INTEGER NOT NULL,
                "PaidTime" TEXT NULL,
                "CoinTransactionId" INTEGER NULL,
                "ProductType" INTEGER NOT NULL,
                "DurationType" INTEGER NOT NULL,
                "BenefitExpiresAt" TEXT NULL,
                "UserBenefitId" INTEGER NULL
            )
            """);
    }

    private static int ReadInt(ISqlSugarClient db, long orderId, string columnName)
    {
        return Convert.ToInt32(
            db.Ado.GetScalar($"SELECT \"{columnName}\" FROM \"ShopOrder\" WHERE \"Id\" = {orderId}"));
    }

    private static long ReadLong(ISqlSugarClient db, long orderId, string columnName)
    {
        return Convert.ToInt64(
            db.Ado.GetScalar($"SELECT \"{columnName}\" FROM \"ShopOrder\" WHERE \"Id\" = {orderId}"));
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

    private static string CreateTemporaryDatabasePath()
    {
        return Path.Combine(Path.GetTempPath(), $"radish-shop-order-migration-{Guid.NewGuid():N}.db");
    }

    private static void DeleteTemporaryDatabase(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
