using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Radish.Model;

namespace Radish.IRepository;

public sealed record ExperienceGovernanceActionPageQuery(
    long TenantId,
    long TargetUserId,
    int PageIndex,
    int PageSize);

public sealed record ExperienceLevelRecalculationAuditPageQuery(int PageIndex, int PageSize);

public sealed record ExperienceAdjustmentMutationCommand(
    long TenantId,
    long UserId,
    int ExpectedVersion,
    int CurrentLevel,
    long CurrentExp,
    long TotalExp,
    DateTime? LevelUpAt,
    ExpTransaction Transaction,
    long ActorUserId,
    string ActorName,
    DateTime NowUtc);

public sealed record ExperienceGovernanceMutationCommand(
    long TenantId,
    long UserId,
    int ExpectedVersion,
    bool ExpFrozen,
    DateTime? FrozenUntil,
    string FrozenReason,
    UserExperienceGovernanceAction Action,
    long ActorUserId,
    string ActorName,
    DateTime NowUtc);

public sealed record ExperienceAdjustmentMutationResult(
    UserExperience Experience,
    ExpTransaction Transaction);

public sealed record ExperienceGovernanceMutationResult(
    UserExperience Experience,
    UserExperienceGovernanceAction Action);

public sealed record ExperienceLevelSnapshot(int Level, long ExpRequired, long ExpCumulative);

public sealed record ExperienceLevelRecalculationTarget(int Level, long ExpRequired, long ExpCumulative);

public sealed record ExperienceLevelRecalculationCommand(
    string FormulaType,
    string FormulaSummary,
    string ExpectedFingerprint,
    IReadOnlyList<ExperienceLevelRecalculationTarget> Targets,
    string Reason,
    long ActorUserId,
    string ActorName,
    DateTime NowUtc);

public sealed record ExperienceLevelRecalculationMutationResult(
    IReadOnlyList<LevelConfig> LevelConfigs,
    ExperienceLevelRecalculationAudit Audit);

public sealed class ExperienceGovernanceTargetUnavailableException : Exception;

public sealed class ExperienceGovernanceStateConflictException : Exception;

public sealed class ExperienceLevelRecalculationPreviewConflictException : Exception;

public sealed class ExperienceLevelRecalculationNoChangesException : Exception;

public static class ExperienceLevelRecalculationFingerprint
{
    public static string Compute(
        string formulaType,
        string formulaSummary,
        IEnumerable<ExperienceLevelSnapshot> current,
        IEnumerable<ExperienceLevelRecalculationTarget> targets)
    {
        var canonical = new StringBuilder()
            .Append("formula=").Append(formulaType.Trim()).Append('\n')
            .Append("summary=").Append(formulaSummary.Trim()).Append('\n');

        foreach (var item in current.OrderBy(item => item.Level))
        {
            canonical
                .Append("before=")
                .Append(item.Level.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(item.ExpRequired.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(item.ExpCumulative.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        foreach (var item in targets.OrderBy(item => item.Level))
        {
            canonical
                .Append("after=")
                .Append(item.Level.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(item.ExpRequired.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(item.ExpCumulative.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }
}

public interface IExperienceGovernanceRepository
{
    Task<(IReadOnlyList<UserExperienceGovernanceAction> Items, int Total)> QueryActionsPageAsync(
        ExperienceGovernanceActionPageQuery query);

    Task<(IReadOnlyList<ExperienceLevelRecalculationAudit> Items, int Total)> QueryLevelRecalculationAuditsAsync(
        ExperienceLevelRecalculationAuditPageQuery query);

    Task<IReadOnlyList<LevelConfig>> QueryLevelConfigsAsync();

    Task<ExperienceAdjustmentMutationResult> ApplyAdjustmentAsync(ExperienceAdjustmentMutationCommand command);

    Task<ExperienceGovernanceMutationResult> ApplyGovernanceActionAsync(
        ExperienceGovernanceMutationCommand command);

    Task<ExperienceLevelRecalculationMutationResult> ApplyLevelRecalculationAsync(
        ExperienceLevelRecalculationCommand command);
}
