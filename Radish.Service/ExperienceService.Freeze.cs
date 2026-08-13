using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Serilog;

namespace Radish.Service;

public partial class ExperienceService
{
    /// <summary>按权威经验版本冻结目标经验。</summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<AdminExperienceGovernanceResultVo> FreezeExperienceAsync(
        long userId,
        DateTime? frozenUntil,
        string reason,
        long operatorId,
        string operatorName,
        int expectedVersion)
    {
        ValidateExperienceMutationIdentity(userId, operatorId, expectedVersion);
        var now = GetUtcNow();
        if (frozenUntil.HasValue && frozenUntil.Value <= now)
        {
            throw new BusinessException(
                "冻结到期时间必须晚于当前时间",
                400,
                "Experience.FreezeUntilInvalid",
                "error.experience.freeze_until_invalid");
        }

        var normalizedReason = NormalizeExperienceMutationReason(reason, "冻结原因");
        var normalizedOperatorName = NormalizeExperienceOperatorName(operatorId, operatorName);
        var current = await RequireExperienceTargetAsync(userId);
        EnsureExperienceVersion(current.Version, expectedVersion);
        var target = await ResolveGovernanceTargetAsync(userId, current.TenantId);
        var action = CreateGovernanceAction(
            userId,
            target.UserName,
            current.TenantId,
            ExperienceGovernanceActionTypeEnum.Freeze,
            normalizedReason,
            operatorId,
            normalizedOperatorName,
            frozenUntil: frozenUntil);

        var result = await ApplyGovernanceActionAsync(
            current.TenantId,
            userId,
            expectedVersion,
            true,
            frozenUntil,
            normalizedReason,
            action,
            operatorId,
            normalizedOperatorName,
            now);
        Log.Information(
            "管理员 {OperatorName}({OperatorId}) 冻结用户 {UserId} 经验，经验版本 {ExpectedVersion} -> {ResultVersion}",
            normalizedOperatorName,
            operatorId,
            userId,
            expectedVersion,
            result.VoExperience.VoVersion);
        return result;
    }

    /// <summary>按权威经验版本解冻目标经验。</summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<AdminExperienceGovernanceResultVo> UnfreezeExperienceAsync(
        long userId,
        string reason,
        long operatorId,
        string operatorName,
        int expectedVersion)
    {
        ValidateExperienceMutationIdentity(userId, operatorId, expectedVersion);
        var normalizedReason = NormalizeExperienceMutationReason(reason, "解冻原因");
        var normalizedOperatorName = NormalizeExperienceOperatorName(operatorId, operatorName);
        var current = await RequireExperienceTargetAsync(userId);
        EnsureExperienceVersion(current.Version, expectedVersion);
        if (!current.ExpFrozen && !current.FrozenUntil.HasValue && string.IsNullOrWhiteSpace(current.FrozenReason))
        {
            throw new BusinessException(
                "目标经验当前未冻结",
                409,
                "Experience.GovernanceNoChanges",
                "error.experience.governance_no_changes");
        }

        var now = GetUtcNow();
        var target = await ResolveGovernanceTargetAsync(userId, current.TenantId);
        var action = CreateGovernanceAction(
            userId,
            target.UserName,
            current.TenantId,
            ExperienceGovernanceActionTypeEnum.Unfreeze,
            normalizedReason,
            operatorId,
            normalizedOperatorName);
        var result = await ApplyGovernanceActionAsync(
            current.TenantId,
            userId,
            expectedVersion,
            false,
            null,
            string.Empty,
            action,
            operatorId,
            normalizedOperatorName,
            now);
        Log.Information(
            "管理员 {OperatorName}({OperatorId}) 解冻用户 {UserId} 经验，经验版本 {ExpectedVersion} -> {ResultVersion}",
            normalizedOperatorName,
            operatorId,
            userId,
            expectedVersion,
            result.VoExperience.VoVersion);
        return result;
    }

    private async Task<UserExperience> NormalizeFreezeStateAsync(UserExperience userExp)
    {
        var now = GetUtcNow();
        if (!IsFreezeExpired(userExp, now))
        {
            return userExp;
        }

        var target = await ResolveGovernanceTargetAsync(userExp.UserId, userExp.TenantId);
        var action = CreateGovernanceAction(
            userExp.UserId,
            target.UserName,
            userExp.TenantId,
            ExperienceGovernanceActionTypeEnum.AutoUnfreeze,
            BuildAutomaticUnfreezeRemark(userExp.FrozenReason),
            0,
            "System");
        try
        {
            var result = await _experienceGovernanceRepository.ApplyGovernanceActionAsync(
                new ExperienceGovernanceMutationCommand(
                    userExp.TenantId,
                    userExp.UserId,
                    userExp.Version,
                    false,
                    null,
                    string.Empty,
                    action,
                    0,
                    "System",
                    now));
            Log.Information(
                "用户 {UserId} 的临时经验冻结已到期并追加自动解冻事件，经验版本 {ExpectedVersion} -> {ResultVersion}",
                userExp.UserId,
                userExp.Version,
                result.Experience.Version);
            return result.Experience;
        }
        catch (ExperienceGovernanceStateConflictException)
        {
            return await _userExpRepository.QueryFirstAsync(item => item.Id == userExp.Id && !item.IsDeleted)
                ?? userExp;
        }
    }

    private async Task<AdminExperienceGovernanceResultVo> ApplyGovernanceActionAsync(
        long tenantId,
        long userId,
        int expectedVersion,
        bool expFrozen,
        DateTime? frozenUntil,
        string frozenReason,
        UserExperienceGovernanceAction action,
        long operatorId,
        string operatorName,
        DateTime now)
    {
        try
        {
            var writeResult = await _experienceGovernanceRepository.ApplyGovernanceActionAsync(
                new ExperienceGovernanceMutationCommand(
                    tenantId,
                    userId,
                    expectedVersion,
                    expFrozen,
                    frozenUntil,
                    frozenReason,
                    action,
                    operatorId,
                    operatorName,
                    now));
            return new AdminExperienceGovernanceResultVo
            {
                VoExperience = await MapToVoAsync(writeResult.Experience)
                    ?? throw new InvalidOperationException("经验治理结果映射失败"),
                VoAction = MapGovernanceAction(writeResult.Action)
            };
        }
        catch (ExperienceGovernanceTargetUnavailableException)
        {
            throw CreateExperienceTargetUnavailableException();
        }
        catch (ExperienceGovernanceStateConflictException)
        {
            throw CreateExperienceVersionConflictException();
        }
    }

    private static string BuildAutomaticUnfreezeRemark(string? frozenReason)
    {
        var normalized = NormalizeOptionalSnapshotText(frozenReason);
        return string.IsNullOrWhiteSpace(normalized)
            ? "临时经验冻结到期，系统自动解除"
            : NormalizeRequiredSnapshotText(
                $"临时经验冻结到期，系统自动解除；原冻结原因：{normalized}",
                "临时经验冻结到期，系统自动解除");
    }

    private static bool IsFreezeActive(UserExperience userExp, DateTime referenceTime)
        => userExp.ExpFrozen && (!userExp.FrozenUntil.HasValue || userExp.FrozenUntil > referenceTime);

    private static bool IsFreezeExpired(UserExperience userExp, DateTime referenceTime)
        => userExp.ExpFrozen && userExp.FrozenUntil.HasValue && userExp.FrozenUntil.Value <= referenceTime;
}
