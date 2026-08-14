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

public sealed class ExperienceGovernanceRepositoryTest : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(),
        $"radish-experience-governance-{Guid.NewGuid():N}.db");
    private readonly SqlSugarScope _db;
    private readonly ExperienceGovernanceRepository _repository;

    public ExperienceGovernanceRepositoryTest()
    {
        _db = CreateScope(_path);
        _db.CodeFirst.InitTables<
            UserExperience,
            ExpTransaction,
            UserExperienceGovernanceAction,
            LevelConfig,
            ExperienceLevelRecalculationAudit>();
        _repository = new ExperienceGovernanceRepository(
            new UnitOfWorkManage(_db, NullLogger<UnitOfWorkManage>.Instance));
    }

    [Fact]
    public async Task AdjustmentAndGovernanceAction_ShouldAdvanceVersionAndRejectStaleWritesAtomically()
    {
        var now = new DateTime(2026, 8, 13, 4, 0, 0, DateTimeKind.Utc);
        _db.Insertable(CreateExperience(version: 3)).ExecuteCommand();

        var adjustment = await _repository.ApplyAdjustmentAsync(
            CreateAdjustmentCommand(transactionId: 2001, expectedVersion: 3, now));

        Assert.Equal(4, adjustment.Experience.Version);
        Assert.Equal(125, adjustment.Experience.TotalExp);
        Assert.Single(_db.Queryable<ExpTransaction>().ToList());

        await Assert.ThrowsAsync<ExperienceGovernanceStateConflictException>(() =>
            _repository.ApplyAdjustmentAsync(
                CreateAdjustmentCommand(transactionId: 2002, expectedVersion: 3, now.AddMinutes(1))));
        Assert.Single(_db.Queryable<ExpTransaction>().ToList());

        var action = new UserExperienceGovernanceAction
        {
            Id = 3001,
            TenantId = 9,
            TargetUserId = 1001,
            TargetUserName = "alice",
            ActionType = (int)ExperienceGovernanceActionTypeEnum.Freeze,
            Remark = "人工复核后冻结",
            FrozenUntil = now.AddDays(1),
            CreateTime = now.AddMinutes(2),
            CreateBy = "admin",
            CreateId = 9001
        };
        var governance = await _repository.ApplyGovernanceActionAsync(
            new ExperienceGovernanceMutationCommand(
                9,
                1001,
                4,
                true,
                action.FrozenUntil,
                action.Remark,
                action,
                9001,
                "admin",
                action.CreateTime));

        Assert.Equal(5, governance.Experience.Version);
        Assert.True(governance.Experience.ExpFrozen);
        Assert.Equal(4, governance.Action.ExpectedVersion);
        Assert.Equal(5, governance.Action.ResultVersion);
        var page = await _repository.QueryActionsPageAsync(
            new ExperienceGovernanceActionPageQuery(9, 1001, 1, 20));
        Assert.Equal(1, page.Total);
        Assert.Equal(3001, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task LevelRecalculation_ShouldRevalidatePreviewAndAppendAudit()
    {
        var now = new DateTime(2026, 8, 13, 5, 0, 0, DateTimeKind.Utc);
        _db.Insertable(new[]
        {
            CreateLevel(0, 100, 0),
            CreateLevel(1, 200, 100),
            CreateLevel(2, 0, 300)
        }).ExecuteCommand();
        const string formulaType = "AuthoritativeCumulativeV1";
        const string formulaSummary = "Lv.0 起点为 0，后续累计值等于前序等级所需经验之和";
        var before = new[]
        {
            new ExperienceLevelSnapshot(0, 100, 0),
            new ExperienceLevelSnapshot(1, 200, 100),
            new ExperienceLevelSnapshot(2, 0, 300)
        };
        var targets = new[]
        {
            new ExperienceLevelRecalculationTarget(0, 120, 0),
            new ExperienceLevelRecalculationTarget(1, 240, 120),
            new ExperienceLevelRecalculationTarget(2, 0, 360)
        };
        var fingerprint = ExperienceLevelRecalculationFingerprint.Compute(
            formulaType,
            formulaSummary,
            before,
            targets);
        var command = new ExperienceLevelRecalculationCommand(
            formulaType,
            formulaSummary,
            fingerprint,
            targets,
            "统一等级累计经验口径",
            9001,
            "admin",
            now);

        var result = await _repository.ApplyLevelRecalculationAsync(command);

        Assert.Equal(3, result.Audit.ChangedLevelCount);
        Assert.Equal(fingerprint, result.Audit.PreviewFingerprint);
        Assert.Equal([0L, 120L, 360L], result.LevelConfigs.Select(item => item.ExpCumulative).ToArray());
        var auditPage = await _repository.QueryLevelRecalculationAuditsAsync(
            new ExperienceLevelRecalculationAuditPageQuery(1, 20));
        Assert.Equal(1, auditPage.Total);
        Assert.Single(auditPage.Items);

        await Assert.ThrowsAsync<ExperienceLevelRecalculationPreviewConflictException>(() =>
            _repository.ApplyLevelRecalculationAsync(command with { NowUtc = now.AddMinutes(1) }));
        Assert.Single(_db.Queryable<ExperienceLevelRecalculationAudit>().ToList());
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private static UserExperience CreateExperience(int version) => new()
    {
        Id = 10001,
        TenantId = 9,
        UserId = 1001,
        CurrentLevel = 1,
        CurrentExp = 20,
        TotalExp = 120,
        Version = version,
        CreateTime = new DateTime(2026, 8, 13, 3, 0, 0, DateTimeKind.Utc),
        CreateBy = "seed",
        CreateId = 1
    };

    private static ExperienceAdjustmentMutationCommand CreateAdjustmentCommand(
        long transactionId,
        int expectedVersion,
        DateTime now) => new(
        9,
        1001,
        expectedVersion,
        1,
        25,
        125,
        null,
        new ExpTransaction
        {
            Id = transactionId,
            TenantId = 9,
            UserId = 1001,
            ExpType = "ADMIN_ADJUST",
            ExpAmount = 5,
            BusinessType = "User",
            BusinessId = 1001,
            Remark = "测试调整",
            ExpBefore = 120,
            ExpAfter = 125,
            LevelBefore = 1,
            LevelAfter = 1,
            CreatedDate = now.Date,
            CreateTime = now,
            CreateBy = "admin",
            CreateId = 9001
        },
        9001,
        "admin",
        now);

    private static LevelConfig CreateLevel(int level, long required, long cumulative) => new()
    {
        Id = level,
        Level = level,
        LevelName = $"Lv.{level}",
        ExpRequired = required,
        ExpCumulative = cumulative,
        SortOrder = level,
        CreateTime = new DateTime(2026, 8, 13, 3, 0, 0, DateTimeKind.Utc),
        CreateBy = "seed",
        CreateId = 1
    };

    private static SqlSugarScope CreateScope(string path) => new(new ConnectionConfig
    {
        ConfigId = "Main",
        ConnectionString = $"Data Source={path}",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute
    });
}
