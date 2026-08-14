using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Radish.Common;
using Radish.IRepository;
using Radish.Model;
using Radish.Repository.Base;
using Radish.Repository.UnitOfWorks;
using SqlSugar;

namespace Radish.Repository;

/// <summary>经验聚合、治理事件与等级配置审计的权威事务边界。</summary>
public sealed class ExperienceGovernanceRepository : BaseRepository<UserExperience>, IExperienceGovernanceRepository
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ExperienceGovernanceRepository(IUnitOfWorkManage unitOfWorkManage) : base(unitOfWorkManage)
    {
    }

    public Task<(IReadOnlyList<UserExperienceGovernanceAction> Items, int Total)> QueryActionsPageAsync(
        ExperienceGovernanceActionPageQuery query)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            var targetExists = await DbProtectedClient.Queryable<UserExperience>()
                .Where(item =>
                    item.TenantId == query.TenantId &&
                    item.UserId == query.TargetUserId &&
                    !item.IsDeleted)
                .AnyAsync();
            if (!targetExists)
            {
                throw new ExperienceGovernanceTargetUnavailableException();
            }

            RefAsync<int> total = 0;
            var items = await DbProtectedClient.Queryable<UserExperienceGovernanceAction>()
                .Where(item =>
                    item.TenantId == query.TenantId &&
                    item.TargetUserId == query.TargetUserId &&
                    !item.IsDeleted)
                .OrderByDescending(item => item.CreateTime)
                .OrderByDescending(item => item.Id)
                .ToPageListAsync(query.PageIndex, query.PageSize, total);
            return ((IReadOnlyList<UserExperienceGovernanceAction>)items, total.Value);
        });
    }

    public Task<(IReadOnlyList<ExperienceLevelRecalculationAudit> Items, int Total)>
        QueryLevelRecalculationAuditsAsync(ExperienceLevelRecalculationAuditPageQuery query)
    {
        return ExecuteDbOperationAsync(async () =>
        {
            RefAsync<int> total = 0;
            var items = await DbProtectedClient.Queryable<ExperienceLevelRecalculationAudit>()
                .OrderByDescending(item => item.CreateTime)
                .OrderByDescending(item => item.Id)
                .ToPageListAsync(query.PageIndex, query.PageSize, total);
            return ((IReadOnlyList<ExperienceLevelRecalculationAudit>)items, total.Value);
        });
    }

    public Task<IReadOnlyList<LevelConfig>> QueryLevelConfigsAsync()
    {
        return ExecuteDbOperationAsync(async () =>
            (IReadOnlyList<LevelConfig>)await DbProtectedClient.Queryable<LevelConfig>()
                .OrderBy(item => item.Level)
                .ToListAsync());
    }

    public Task<ExperienceAdjustmentMutationResult> ApplyAdjustmentAsync(
        ExperienceAdjustmentMutationCommand command)
    {
        return ExecuteDbOperationAsync(() => ExecuteInTransactionAsync(async () =>
        {
            await AcquireUserExperienceLockAsync(command.TenantId, command.UserId);
            var experience = await QueryExperienceAsync(command.TenantId, command.UserId);
            EnsureExpectedVersion(experience, command.ExpectedVersion);

            var resultVersion = command.ExpectedVersion + 1;
            var affected = await DbProtectedClient.Updateable<UserExperience>()
                .SetColumns(item => new UserExperience
                {
                    CurrentLevel = command.CurrentLevel,
                    CurrentExp = command.CurrentExp,
                    TotalExp = command.TotalExp,
                    LevelUpAt = command.LevelUpAt,
                    Version = resultVersion,
                    ModifyTime = command.NowUtc,
                    ModifyBy = command.ActorName,
                    ModifyId = command.ActorUserId
                })
                .Where(item =>
                    item.Id == experience.Id &&
                    item.TenantId == command.TenantId &&
                    item.UserId == command.UserId &&
                    item.Version == command.ExpectedVersion &&
                    !item.IsDeleted)
                .ExecuteCommandAsync();
            if (affected != 1)
            {
                throw new ExperienceGovernanceStateConflictException();
            }

            await DbProtectedClient.Insertable(command.Transaction).ExecuteCommandAsync();
            experience.CurrentLevel = command.CurrentLevel;
            experience.CurrentExp = command.CurrentExp;
            experience.TotalExp = command.TotalExp;
            experience.LevelUpAt = command.LevelUpAt;
            experience.Version = resultVersion;
            experience.ModifyTime = command.NowUtc;
            experience.ModifyBy = command.ActorName;
            experience.ModifyId = command.ActorUserId;
            return new ExperienceAdjustmentMutationResult(experience, command.Transaction);
        }));
    }

    public Task<ExperienceGovernanceMutationResult> ApplyGovernanceActionAsync(
        ExperienceGovernanceMutationCommand command)
    {
        return ExecuteDbOperationAsync(() => ExecuteInTransactionAsync(async () =>
        {
            await AcquireUserExperienceLockAsync(command.TenantId, command.UserId);
            var experience = await QueryExperienceAsync(command.TenantId, command.UserId);
            EnsureExpectedVersion(experience, command.ExpectedVersion);

            var resultVersion = command.ExpectedVersion + 1;
            var affected = await DbProtectedClient.Updateable<UserExperience>()
                .SetColumns(item => new UserExperience
                {
                    ExpFrozen = command.ExpFrozen,
                    FrozenUntil = command.FrozenUntil,
                    FrozenReason = command.FrozenReason,
                    Version = resultVersion,
                    ModifyTime = command.NowUtc,
                    ModifyBy = command.ActorName,
                    ModifyId = command.ActorUserId
                })
                .Where(item =>
                    item.Id == experience.Id &&
                    item.TenantId == command.TenantId &&
                    item.UserId == command.UserId &&
                    item.Version == command.ExpectedVersion &&
                    !item.IsDeleted)
                .ExecuteCommandAsync();
            if (affected != 1)
            {
                throw new ExperienceGovernanceStateConflictException();
            }

            command.Action.ExpectedVersion = command.ExpectedVersion;
            command.Action.ResultVersion = resultVersion;
            await DbProtectedClient.Insertable(command.Action).ExecuteCommandAsync();

            experience.ExpFrozen = command.ExpFrozen;
            experience.FrozenUntil = command.FrozenUntil;
            experience.FrozenReason = command.FrozenReason;
            experience.Version = resultVersion;
            experience.ModifyTime = command.NowUtc;
            experience.ModifyBy = command.ActorName;
            experience.ModifyId = command.ActorUserId;
            return new ExperienceGovernanceMutationResult(experience, command.Action);
        }));
    }

    public Task<ExperienceLevelRecalculationMutationResult> ApplyLevelRecalculationAsync(
        ExperienceLevelRecalculationCommand command)
    {
        return ExecuteDbOperationAsync(() => ExecuteInTransactionAsync(async () =>
        {
            await AcquireLevelRecalculationLockAsync();
            var current = await DbProtectedClient.Queryable<LevelConfig>()
                .OrderBy(item => item.Level)
                .ToListAsync();
            var currentSnapshots = current
                .Select(item => new ExperienceLevelSnapshot(item.Level, item.ExpRequired, item.ExpCumulative))
                .ToList();
            var actualFingerprint = ExperienceLevelRecalculationFingerprint.Compute(
                command.FormulaType,
                command.FormulaSummary,
                currentSnapshots,
                command.Targets);
            if (!string.Equals(actualFingerprint, command.ExpectedFingerprint, StringComparison.Ordinal))
            {
                throw new ExperienceLevelRecalculationPreviewConflictException();
            }

            var targetMap = command.Targets.ToDictionary(item => item.Level);
            if (current.Count != targetMap.Count || current.Any(item => !targetMap.ContainsKey(item.Level)))
            {
                throw new ExperienceLevelRecalculationPreviewConflictException();
            }

            var changed = current
                .Where(item =>
                    item.ExpRequired != targetMap[item.Level].ExpRequired ||
                    item.ExpCumulative != targetMap[item.Level].ExpCumulative)
                .ToList();
            if (changed.Count == 0)
            {
                throw new ExperienceLevelRecalculationNoChangesException();
            }

            foreach (var item in changed)
            {
                var target = targetMap[item.Level];
                var affected = await DbProtectedClient.Updateable<LevelConfig>()
                    .SetColumns(stored => new LevelConfig
                    {
                        ExpRequired = target.ExpRequired,
                        ExpCumulative = target.ExpCumulative,
                        ModifyTime = command.NowUtc,
                        ModifyBy = command.ActorName,
                        ModifyId = command.ActorUserId
                    })
                    .Where(stored =>
                        stored.Level == item.Level &&
                        stored.ExpRequired == item.ExpRequired &&
                        stored.ExpCumulative == item.ExpCumulative)
                    .ExecuteCommandAsync();
                if (affected != 1)
                {
                    throw new ExperienceLevelRecalculationPreviewConflictException();
                }

                item.ExpRequired = target.ExpRequired;
                item.ExpCumulative = target.ExpCumulative;
                item.ModifyTime = command.NowUtc;
                item.ModifyBy = command.ActorName;
                item.ModifyId = command.ActorUserId;
            }

            var audit = new ExperienceLevelRecalculationAudit
            {
                Id = SnowFlakeSingle.Instance.NextId(),
                FormulaType = command.FormulaType,
                FormulaSummary = command.FormulaSummary,
                PreviewFingerprint = actualFingerprint,
                ChangedLevelCount = changed.Count,
                BeforeSnapshot = JsonSerializer.Serialize(currentSnapshots, AuditJsonOptions),
                AfterSnapshot = JsonSerializer.Serialize(
                    current.Select(item => new ExperienceLevelSnapshot(item.Level, item.ExpRequired, item.ExpCumulative)),
                    AuditJsonOptions),
                Reason = command.Reason,
                CreateTime = command.NowUtc,
                CreateBy = command.ActorName,
                CreateId = command.ActorUserId
            };
            await DbProtectedClient.Insertable(audit).ExecuteCommandAsync();
            return new ExperienceLevelRecalculationMutationResult(current, audit);
        }));
    }

    private async Task<UserExperience> QueryExperienceAsync(long tenantId, long userId)
    {
        return await DbProtectedClient.Queryable<UserExperience>()
            .Where(item =>
                item.TenantId == tenantId &&
                item.UserId == userId &&
                !item.IsDeleted)
            .FirstAsync()
            ?? throw new ExperienceGovernanceTargetUnavailableException();
    }

    private static void EnsureExpectedVersion(UserExperience experience, int expectedVersion)
    {
        if (experience.Version != expectedVersion)
        {
            throw new ExperienceGovernanceStateConflictException();
        }
    }

    private async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action)
    {
        var ownsTransaction = DbProtectedClient.Ado.Transaction == null;
        if (ownsTransaction)
        {
            DbProtectedClient.Ado.BeginTran();
        }

        try
        {
            var result = await action();
            if (ownsTransaction)
            {
                DbProtectedClient.Ado.CommitTran();
            }

            return result;
        }
        catch
        {
            if (ownsTransaction)
            {
                DbProtectedClient.Ado.RollbackTran();
            }

            throw;
        }
    }

    private Task AcquireUserExperienceLockAsync(long tenantId, long userId)
        => AcquirePostgreSqlLockAsync($"radish-experience-governance:{tenantId}:{userId}");

    private Task AcquireLevelRecalculationLockAsync()
        => AcquirePostgreSqlLockAsync("radish-experience-level-recalculation");

    private async Task AcquirePostgreSqlLockAsync(string source)
    {
        if (DbProtectedClient.CurrentConnectionConfig.DbType != DbType.PostgreSQL)
        {
            return;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        var lockKey = BinaryPrimitives.ReadInt64BigEndian(hash);
        await DbProtectedClient.Ado.ExecuteCommandAsync(
            "SELECT pg_advisory_xact_lock(@LockKey)",
            new SugarParameter("@LockKey", lockKey));
    }
}
