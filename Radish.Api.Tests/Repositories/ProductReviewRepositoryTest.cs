using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed class ProductReviewRepositoryTest
{
    private static readonly DateTime NowUtc =
        new(2026, 8, 9, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task UpsertAndDelete_ShouldKeepOneRelationshipAndAdvanceCasVersion()
    {
        var path = CreatePath("lifecycle");
        using var db = CreateClient(path);
        try
        {
            InitializeSchema(db);
            SeedProductAndCompletedOrder(db);
            var repository = CreateRepository(db);

            var created = await repository.UpsertAsync(Command(0, 5, "很好用", NowUtc));
            var updated = await repository.UpsertAsync(Command(1, 4, "调整后的评价", NowUtc.AddMinutes(1)));
            var deleted = await repository.DeleteAsync(new ProductReviewDeleteCommand(
                9,
                updated.Review.Id,
                1001,
                2,
                "alice",
                NowUtc.AddMinutes(2)));
            var restored = await repository.UpsertAsync(Command(
                3,
                5,
                "重新评价",
                NowUtc.AddMinutes(3)));

            Assert.True(created.Created);
            Assert.Equal(1, created.Review.Version);
            Assert.False(updated.Created);
            Assert.Equal(2, updated.Review.Version);
            Assert.True(deleted.IsDeleted);
            Assert.Equal(3, deleted.Version);
            Assert.True(restored.Restored);
            Assert.Equal(4, restored.Review.Version);
            Assert.False(restored.Review.IsDeleted);

            var persisted = Assert.Single(db.Queryable<ProductReview>().ToList());
            Assert.Equal(created.Review.Id, persisted.Id);
            Assert.Equal(4, persisted.Version);
            Assert.Equal("重新评价", persisted.Comment);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task UpsertAsync_ShouldRejectStaleVersionAndMissingCompletedOrder()
    {
        var path = CreatePath("guards");
        using var db = CreateClient(path);
        try
        {
            InitializeSchema(db);
            SeedProductAndCompletedOrder(db);
            var repository = CreateRepository(db);
            await repository.UpsertAsync(Command(0, 5, null, NowUtc));

            await Assert.ThrowsAsync<ProductReviewVersionConflictException>(() =>
                repository.UpsertAsync(Command(0, 4, null, NowUtc.AddMinutes(1))));

            await Assert.ThrowsAsync<ProductReviewPurchaseRequiredException>(() =>
                repository.UpsertAsync(new ProductReviewWriteCommand(
                    9,
                    7001,
                    1002,
                    "bob",
                    5,
                    null,
                    0,
                    NowUtc)));
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task QueryPageAndSummary_ShouldExcludeDeletedReviewsAndReturnDistribution()
    {
        var path = CreatePath("summary");
        using var db = CreateClient(path);
        try
        {
            InitializeSchema(db);
            SeedProductAndCompletedOrder(db);
            SeedReview(db, 8001, 1001, 5, NowUtc.AddMinutes(-2));
            SeedReview(db, 8002, 1002, 4, NowUtc.AddMinutes(-1));
            SeedReview(db, 8003, 1003, 1, NowUtc, isDeleted: true);
            var repository = CreateRepository(db);

            var (items, total) = await repository.QueryPageAsync(9, 7001, 1, 20);
            var summary = await repository.QuerySummaryAsync(9, 7001);

            Assert.Equal(2, total);
            Assert.Equal([8002L, 8001L], items.Select(item => item.Id).ToArray());
            Assert.Equal(2, summary.ReviewCount);
            Assert.Equal(4.5m, summary.AverageRating);
            Assert.Equal(1, summary.FiveStarCount);
            Assert.Equal(1, summary.FourStarCount);
            Assert.Equal(0, summary.OneStarCount);
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    [Fact]
    public async Task UpsertAsync_ShouldSerializeConcurrentFirstReviewOnSqlite()
    {
        var path = CreatePath("concurrent");
        using var setupDb = CreateClient(path);
        using var firstDb = CreateClient(path);
        using var secondDb = CreateClient(path);
        try
        {
            InitializeSchema(setupDb);
            SeedProductAndCompletedOrder(setupDb);

            var tasks = new[]
            {
                CaptureWriteAsync(CreateRepository(firstDb), Command(0, 5, "first", NowUtc)),
                CaptureWriteAsync(CreateRepository(secondDb), Command(0, 4, "second", NowUtc))
            };
            var results = await Task.WhenAll(tasks);

            Assert.Single(results, result => result.Result != null);
            Assert.Single(results, result => result.Error is ProductReviewVersionConflictException);
            Assert.Single(setupDb.Queryable<ProductReview>().ToList());
        }
        finally
        {
            DeleteIfExists(path);
        }
    }

    private static async Task<(ProductReviewWriteResult? Result, Exception? Error)> CaptureWriteAsync(
        IProductReviewRepository repository,
        ProductReviewWriteCommand command)
    {
        try
        {
            return (await repository.UpsertAsync(command), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    private static ProductReviewWriteCommand Command(
        int expectedVersion,
        int rating,
        string? comment,
        DateTime nowUtc) =>
        new(9, 7001, 1001, "alice", rating, comment, expectedVersion, nowUtc);

    private static ProductReviewRepository CreateRepository(SqlSugarScope db) =>
        new(new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));

    private static void InitializeSchema(SqlSugarScope db)
    {
        db.CodeFirst.InitTables<Product>();
        db.CodeFirst.InitTables<Order>();
        db.CodeFirst.InitTables<ProductReview>();
    }

    private static void SeedProductAndCompletedOrder(SqlSugarScope db)
    {
        db.Insertable(new Product
        {
            Id = 7001,
            TenantId = 9,
            Name = "青玉头像框",
            CategoryId = "appearance",
            ProductType = ProductType.Benefit,
            Price = 120,
            IsEnabled = true,
            IsOnSale = true,
            CreateTime = NowUtc.AddDays(-2)
        }).ExecuteCommand();
        db.Insertable(new Order
        {
            Id = 7101,
            TenantId = 9,
            OrderNo = "ORD_7101",
            UserId = 1001,
            ProductId = 7001,
            ProductName = "青玉头像框",
            ProductType = ProductType.Benefit,
            Status = OrderStatus.Completed,
            CompletedTime = NowUtc.AddDays(-1),
            CreateTime = NowUtc.AddDays(-1)
        }).ExecuteCommand();
    }

    private static void SeedReview(
        SqlSugarScope db,
        long id,
        long userId,
        int rating,
        DateTime createTime,
        bool isDeleted = false)
    {
        db.Insertable(new ProductReview
        {
            Id = id,
            TenantId = 9,
            ProductId = 7001,
            UserId = userId,
            EligibleOrderId = 7101,
            AuthorName = $"user-{userId}",
            Rating = rating,
            Version = 1,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? createTime.AddMinutes(1) : null,
            DeletedBy = isDeleted ? "user" : null,
            CreateTime = createTime,
            CreateBy = "test",
            CreateId = userId
        }).ExecuteCommand();
    }

    private static SqlSugarScope CreateClient(string path) =>
        new(new ConnectionConfig
        {
            ConfigId = "main",
            ConnectionString = $"Data Source={path};Cache=Shared",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });

    private static string CreatePath(string scope) =>
        Path.Combine(Path.GetTempPath(), $"radish-product-review-{scope}-{Guid.NewGuid():N}.db");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
