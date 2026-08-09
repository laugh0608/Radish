using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Radish.DbMigrate;
using Radish.Model;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ProductReviewSchemaMigrationTest
{
    [Fact]
    public void Apply_ShouldBeRepeatableAndEnforceOneReviewPerUserAndProduct()
    {
        var path = CreatePath("apply");
        using var db = CreateClient(path);
        using var services = new ServiceCollection().BuildServiceProvider();
        try
        {
            db.CodeFirst.InitTables<ContentReport>();
            ProductReviewSchemaMigration.Instance.Apply(db, services);
            ProductReviewSchemaMigration.Instance.Apply(db, services);

            Assert.Empty(ProductReviewSchemaMigration.Instance.Verify(db, services));
            Assert.NotNull(DatabaseIdentifierResolver.ResolveColumn(
                db,
                nameof(ContentReport),
                nameof(ContentReport.TargetSnapshotProductId)));

            db.Insertable(CreateReview(8001)).ExecuteCommand();
            Assert.ThrowsAny<Exception>(() => db.Insertable(CreateReview(8002)).ExecuteCommand());
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void Verify_ShouldRequireMatchingCompletedOrderEvidence()
    {
        var path = CreatePath("evidence");
        using var db = CreateClient(path);
        using var services = new ServiceCollection().BuildServiceProvider();
        try
        {
            db.CodeFirst.InitTables<Order>();
            db.CodeFirst.InitTables<Product>();
            ProductReviewSchemaMigration.Instance.Apply(db, services);
            db.Insertable(new Product
            {
                Id = 7001,
                TenantId = 9,
                Name = "青玉头像框",
                CategoryId = "appearance",
                IsEnabled = true,
                IsOnSale = true
            }).ExecuteCommand();
            db.Insertable(new Order
            {
                Id = 7101,
                TenantId = 9,
                OrderNo = "ORD_7101",
                UserId = 1001,
                ProductId = 7001,
                ProductName = "青玉头像框",
                Status = OrderStatus.Pending
            }).ExecuteCommand();
            db.Insertable(CreateReview(8001)).ExecuteCommand();

            var issues = ProductReviewSchemaMigration.Instance.Verify(db, services);

            Assert.Contains(issues, issue => issue.Contains("Completed 订单证据", StringComparison.Ordinal));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public void Registry_ShouldAppendProductReviewMigrationToMainScope()
    {
        var migration = SchemaMigrationRegistry.All.Single(item =>
            item.MigrationId == "20260809_020_product_review");

        Assert.Same(ProductReviewSchemaMigration.Instance, migration);
        Assert.Equal("Main", migration.Scope);
    }

    private static ProductReview CreateReview(long id) => new()
    {
        Id = id,
        TenantId = 9,
        ProductId = 7001,
        UserId = 1001,
        EligibleOrderId = 7101,
        AuthorName = "alice",
        Rating = 5,
        Version = 1,
        CreateTime = new DateTime(2026, 8, 9, 6, 0, 0, DateTimeKind.Utc),
        CreateBy = "alice",
        CreateId = 1001
    };

    private static SqlSugarScope CreateClient(string path) =>
        new(new ConnectionConfig
        {
            ConfigId = "Main",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

    private static string CreatePath(string scope) =>
        Path.Combine(
            Path.GetTempPath(),
            $"radish-product-review-migration-{scope}-{Guid.NewGuid():N}.db");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
