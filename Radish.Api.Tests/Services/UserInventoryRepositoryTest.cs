using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Radish.Common.CoreTool;
using Radish.Common.HttpContextTool;
using Radish.Common.OptionTool;
using Radish.Model;
using Radish.Repository;
using Radish.Repository.UnitOfWorks;
using Radish.Shared.CustomEnum;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Services;

public sealed class UserInventoryRepositoryTest
{
    [Fact]
    public async Task GrantConsumableForOrderAsync_ShouldNotIncreaseQuantity_WhenOrderAlreadyGranted()
    {
        using var harness = UserInventoryRepositoryHarness.Create();

        var first = await harness.Repository.GrantConsumableForOrderAsync(
            tenantId: 0,
            userId: 1001,
            consumableType: ConsumableType.ExpCard,
            itemValue: "100",
            itemName: "经验卡",
            itemIconAttachmentId: null,
            quantity: 3,
            sourceOrderId: 2001,
            sourceProductId: 3001);
        var replay = await harness.Repository.GrantConsumableForOrderAsync(
            tenantId: 0,
            userId: 1001,
            consumableType: ConsumableType.ExpCard,
            itemValue: "100",
            itemName: "经验卡",
            itemIconAttachmentId: null,
            quantity: 3,
            sourceOrderId: 2001,
            sourceProductId: 3001);

        var inventory = harness.Db.Queryable<UserInventory>().First(item => item.Id == first.InventoryId);
        var grantRecords = harness.Db.Queryable<UserInventoryGrantRecord>().ToList();

        Assert.True(first.CreatedGrantRecord);
        Assert.False(replay.CreatedGrantRecord);
        Assert.Equal(first.InventoryId, replay.InventoryId);
        Assert.Equal(3, inventory.Quantity);
        Assert.Single(grantRecords);
    }

    [Fact]
    public async Task TryDeductItemAsync_ShouldRejectDeduct_WhenQuantityInsufficient()
    {
        using var harness = UserInventoryRepositoryHarness.Create();
        harness.Db.Insertable(new UserInventory
        {
            Id = 4001,
            TenantId = 0,
            UserId = 1001,
            ConsumableType = ConsumableType.CoinCard,
            ItemValue = "50",
            Quantity = 2,
            CreateTime = DateTime.UtcNow,
            CreateBy = "System",
            CreateId = 1001
        }).ExecuteCommand();

        var first = await harness.Repository.TryDeductItemAsync(1001, 4001, 2);
        var second = await harness.Repository.TryDeductItemAsync(1001, 4001, 1);

        var inventory = harness.Db.Queryable<UserInventory>().First(item => item.Id == 4001);

        Assert.True(first.Success);
        Assert.Equal(0, first.RemainingQuantity);
        Assert.False(second.Success);
        Assert.Equal(0, inventory.Quantity);
    }

    private sealed class UserInventoryRepositoryHarness : IDisposable
    {
        private static readonly object AppServicesLock = new();
        private static bool appServicesConfigured;
        private readonly string _dbPath;

        private UserInventoryRepositoryHarness(string dbPath, SqlSugarScope db)
        {
            _dbPath = dbPath;
            Db = db;
            var unitOfWork = new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance);
            Repository = new UserInventoryRepository(unitOfWork);
        }

        public SqlSugarScope Db { get; }

        public UserInventoryRepository Repository { get; }

        public static UserInventoryRepositoryHarness Create()
        {
            EnsureAppServices();
            var dbPath = Path.Combine(Path.GetTempPath(), $"radish-inventory-{Guid.NewGuid():N}.db");
            var db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "Main",
                DbType = DbType.Sqlite,
                ConnectionString = $"Data Source={dbPath}",
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });

            db.CodeFirst.InitTables<UserInventory, UserInventoryGrantRecord>();
            return new UserInventoryRepositoryHarness(dbPath, db);
        }

        private static void EnsureAppServices()
        {
            if (appServicesConfigured)
            {
                return;
            }

            lock (AppServicesLock)
            {
                if (appServicesConfigured)
                {
                    return;
                }

                var currentUserAccessor = new Mock<ICurrentUserAccessor>(MockBehavior.Strict);
                currentUserAccessor
                    .Setup(accessor => accessor.Current)
                    .Returns(CurrentUser.Anonymous);

                var services = new ServiceCollection();
                services.AddSingleton(currentUserAccessor.Object);
                services.ConfigureApplication();
                appServicesConfigured = true;
            }
        }

        public void Dispose()
        {
            Db.Dispose();
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
    }
}
