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

public sealed class UserActiveBenefitSchemaMigrationTest
{
    private static readonly DateTime NowUtc = new(2026, 7, 13, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Apply_ShouldBackfillLatestLegalSelectionAndRemainRepeatable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-active-benefit-migration-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);
        using var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(NowUtc)))
            .BuildServiceProvider();

        try
        {
            db.CodeFirst.InitTables<UserBenefit>();
            ShopEntitlementOperationSchemaMigration.Instance.Apply(db, services);
            var revokedBenefit = CreateBenefit(
                1004,
                BenefitType.Badge,
                true,
                NowUtc.AddMinutes(-30),
                null);
            revokedBenefit.RevokedAt = NowUtc.AddMinutes(-10);
            db.Insertable(new[]
            {
                CreateBenefit(1001, BenefitType.Title, true, NowUtc.AddHours(-2), null),
                CreateBenefit(1002, BenefitType.Title, true, NowUtc.AddHours(-1), null),
                CreateBenefit(1003, BenefitType.Theme, true, NowUtc.AddHours(-3), NowUtc),
                revokedBenefit
            }).ExecuteCommand();
            db.Ado.ExecuteCommand(
                "INSERT INTO \"ShopEntitlementOperation\" " +
                "(\"Id\", \"TenantId\", \"UserId\", \"InventoryId\", \"OperationType\", " +
                "\"ConsumableType\", \"Quantity\", \"ItemValue\", \"IdempotencyKey\", \"RequestHash\", " +
                "\"EffectType\", \"ResultPayload\", \"CreateTime\", \"CreateBy\", \"CreateId\") VALUES " +
                "(5001, 0, 2001, 4001, 'Use', 4, 1, '100', 'legacy-use', @RequestHash, " +
                "'ExperienceGrant', '{}', '2026-07-12 08:00:00', 'User', 2001)",
                new SugarParameter("@RequestHash", new string('a', 64)));

            var migration = UserActiveBenefitSchemaMigration.Instance;
            var diagnostics = migration.Diagnose(db, services);
            migration.Apply(db, services);
            migration.Apply(db, services);

            Assert.Contains(diagnostics, item => item.Contains("多激活权益", StringComparison.Ordinal));
            Assert.Contains(diagnostics, item => item.Contains("实时已过期", StringComparison.Ordinal));
            Assert.Empty(migration.Verify(db, services));
            var selection = Assert.Single(db.Queryable<UserActiveBenefit>().ToList());
            Assert.Equal(1002, selection.BenefitId);

            var benefits = db.Queryable<UserBenefit>().OrderBy(item => item.Id).ToList();
            Assert.False(benefits[0].IsActive);
            Assert.True(benefits[1].IsActive);
            Assert.False(benefits[2].IsActive);
            Assert.False(benefits[3].IsActive);
            Assert.Single(db.Queryable<ShopEntitlementOperation>().ToList());
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
    public void Diagnose_ShouldSupportPendingRevocationColumnsBeforeApply()
    {
        var path = Path.Combine(Path.GetTempPath(), $"radish-active-benefit-preflight-{Guid.NewGuid():N}.db");
        using var db = CreateClient(path);
        using var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FixedTimeProvider(new DateTimeOffset(NowUtc)))
            .BuildServiceProvider();

        try
        {
            db.CodeFirst.InitTables<UserBenefit>();
            db.Insertable(new[]
            {
                CreateBenefit(2001, BenefitType.Title, true, NowUtc.AddHours(-2), null),
                CreateBenefit(2002, BenefitType.Title, true, NowUtc.AddHours(-1), null)
            }).ExecuteCommand();
            foreach (var columnName in new[] { "RevokedAt", "RevokedById", "RevokedByName", "RevocationReason" })
            {
                db.Ado.ExecuteCommand($"ALTER TABLE \"ShopUserBenefit\" DROP COLUMN \"{columnName}\"");
            }

            var migration = UserActiveBenefitSchemaMigration.Instance;
            var diagnostics = migration.Diagnose(db, services);

            Assert.Contains(diagnostics, item => item.Contains("多激活权益", StringComparison.Ordinal));

            migration.Apply(db, services);

            Assert.Empty(migration.Verify(db, services));
            var selection = Assert.Single(db.Queryable<UserActiveBenefit>().ToList());
            Assert.Equal(2002, selection.BenefitId);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static UserBenefit CreateBenefit(
        long id,
        BenefitType benefitType,
        bool isActive,
        DateTime activatedAt,
        DateTime? expiresAt)
    {
        return new UserBenefit
        {
            Id = id,
            TenantId = 0,
            UserId = 2001,
            BenefitType = benefitType,
            BenefitValue = $"benefit-{id}",
            SourceType = "System",
            DurationType = expiresAt.HasValue ? DurationType.FixedDate : DurationType.Permanent,
            EffectiveAt = NowUtc.AddDays(-2),
            ExpiresAt = expiresAt,
            IsActive = isActive,
            ActivatedAt = activatedAt,
            CreateTime = NowUtc.AddDays(-2),
            CreateBy = "System",
            CreateId = 0
        };
    }

    private static SqlSugarScope CreateClient(string path)
    {
        var db = new SqlSugarScope(new ConnectionConfig
        {
            ConfigId = "Main",
            ConnectionString = $"Data Source={path}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
        return db;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
