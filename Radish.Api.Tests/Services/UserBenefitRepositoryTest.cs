using System;
using System.IO;
using System.Linq;
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

public sealed class UserBenefitRepositoryTest
{
    private static readonly DateTime NowUtc = new(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SelectionLifecycle_ShouldKeepOneCurrentBenefitAndWriteChangedOperationsOnly()
    {
        using var harness = UserBenefitRepositoryHarness.Create();
        harness.Db.Insertable(new[]
        {
            CreateBenefit(1001, "title-a"),
            CreateBenefit(1002, "title-b")
        }).ExecuteCommand();

        var first = await harness.Repository.ActivateAsync(2001, 1001, 2001, "User", NowUtc);
        var replay = await harness.Repository.ActivateAsync(2001, 1001, 2001, "User", NowUtc.AddSeconds(1));
        var switched = await harness.Repository.ActivateAsync(2001, 1002, 2001, "User", NowUtc.AddSeconds(2));
        var staleDeactivate = await harness.Repository.DeactivateAsync(2001, 1001, 2001, "User", NowUtc.AddSeconds(3));

        var selection = Assert.Single(harness.Db.Queryable<UserActiveBenefit>().ToList());
        var benefits = harness.Db.Queryable<UserBenefit>().OrderBy(item => item.Id).ToList();
        var operations = harness.Db.Queryable<ShopEntitlementOperation>().OrderBy(item => item.CreateTime).ToList();

        Assert.True(first.Changed);
        Assert.False(replay.Changed);
        Assert.True(switched.Changed);
        Assert.Equal(1001, switched.PreviousBenefitId);
        Assert.False(staleDeactivate.Changed);
        Assert.Equal(1002, staleDeactivate.CurrentBenefitId);
        Assert.Equal(1002, selection.BenefitId);
        Assert.False(benefits[0].IsActive);
        Assert.True(benefits[1].IsActive);
        Assert.Equal(2, operations.Count);
        Assert.All(operations, item => Assert.Equal("Activate", item.OperationType));
    }

    [Fact]
    public async Task ExpireAndRevoke_ShouldRemoveOnlyMatchingSelectionAndBeIdempotent()
    {
        using var harness = UserBenefitRepositoryHarness.Create();
        var expiring = CreateBenefit(1101, "title-expiring");
        expiring.ExpiresAt = NowUtc;
        var revoked = CreateBenefit(1102, "title-revoked");
        revoked.BenefitType = BenefitType.Theme;
        harness.Db.Insertable(new[] { expiring, revoked }).ExecuteCommand();
        await harness.Repository.ActivateAsync(2001, 1101, 2001, "User", NowUtc.AddSeconds(-1));
        var beforeExpiry = harness.Db.Queryable<UserBenefit>().Single(item => item.Id == 1101);
        Assert.Equal(NowUtc, beforeExpiry.ExpiresAt);
        Assert.False(beforeExpiry.IsExpired);
        Assert.True(beforeExpiry.IsActive);

        var expired = await harness.Repository.ExpireAsync(1101, NowUtc);
        var expireReplay = await harness.Repository.ExpireAsync(1101, NowUtc.AddSeconds(1));
        var revokeResult = await harness.Repository.RevokeAsync(1102, "测试撤销", 9001, "Admin", NowUtc);
        var revokeReplay = await harness.Repository.RevokeAsync(1102, "测试撤销", 9001, "Admin", NowUtc.AddSeconds(1));

        Assert.NotNull(expired);
        Assert.True(expired!.Changed);
        Assert.Null(expireReplay);
        Assert.True(revokeResult.Changed);
        Assert.False(revokeReplay.Changed);
        Assert.Empty(harness.Db.Queryable<UserActiveBenefit>().ToList());

        var storedExpired = harness.Db.Queryable<UserBenefit>().Single(item => item.Id == 1101);
        var storedRevoked = harness.Db.Queryable<UserBenefit>().Single(item => item.Id == 1102);
        Assert.True(storedExpired.IsExpired);
        Assert.False(storedExpired.IsActive);
        Assert.Equal(NowUtc, storedRevoked.RevokedAt);
        Assert.Equal("测试撤销", storedRevoked.RevocationReason);
        Assert.Equal(3, harness.Db.Queryable<ShopEntitlementOperation>().Count());
    }

    [Fact]
    public async Task GetDueBenefitIdsAsync_ShouldApplyUtcBoundaryAfterBoundedDatabaseOrdering()
    {
        using var harness = UserBenefitRepositoryHarness.Create();
        var due = CreateBenefit(1201, "title-due");
        due.TenantId = 7;
        due.ExpiresAt = NowUtc;
        var future = CreateBenefit(1202, "title-future");
        future.TenantId = 8;
        future.ExpiresAt = NowUtc.AddSeconds(1);
        harness.Db.Insertable(new[] { future, due }).ExecuteCommand();

        var result = await harness.Repository.GetDueBenefitIdsAsync(NowUtc, 100);
        var expired = await harness.Repository.ExpireAsync(1201, NowUtc);

        Assert.Equal([1201L], result);
        Assert.NotNull(expired);
        Assert.True(expired!.Changed);
        Assert.Equal(7, expired.Benefit.TenantId);
    }

    private static UserBenefit CreateBenefit(long id, string value)
    {
        return new UserBenefit
        {
            Id = id,
            TenantId = 0,
            UserId = 2001,
            BenefitType = BenefitType.Title,
            BenefitValue = value,
            SourceType = "System",
            DurationType = DurationType.Permanent,
            EffectiveAt = NowUtc.AddDays(-1),
            CreateTime = NowUtc.AddDays(-1),
            CreateBy = "System",
            CreateId = 0
        };
    }

    private sealed class UserBenefitRepositoryHarness : IDisposable
    {
        private static readonly object AppServicesLock = new();
        private static bool appServicesConfigured;
        private readonly string _dbPath;

        private UserBenefitRepositoryHarness(string dbPath, SqlSugarScope db)
        {
            _dbPath = dbPath;
            Db = db;
            Repository = new UserBenefitRepository(
                new UnitOfWorkManage(db, NullLogger<UnitOfWorkManage>.Instance));
        }

        public SqlSugarScope Db { get; }

        public UserBenefitRepository Repository { get; }

        public static UserBenefitRepositoryHarness Create()
        {
            EnsureAppServices();
            var dbPath = Path.Combine(Path.GetTempPath(), $"radish-benefit-{Guid.NewGuid():N}.db");
            var db = new SqlSugarScope(new ConnectionConfig
            {
                ConfigId = "Main",
                DbType = DbType.Sqlite,
                ConnectionString = $"Data Source={dbPath}",
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
            db.CodeFirst.InitTables<UserBenefit, UserActiveBenefit, ShopEntitlementOperation>();
            return new UserBenefitRepositoryHarness(dbPath, db);
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
                currentUserAccessor.Setup(accessor => accessor.Current).Returns(CurrentUser.Anonymous);
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
