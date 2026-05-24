using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.Model;
using Radish.Shared.CustomEnum;
using Serilog;

namespace Radish.Service;

public partial class ExperienceService
{
    /// <summary>
    /// 冻结用户经验值
    /// </summary>
    public async Task<bool> FreezeExperienceAsync(long userId, DateTime? frozenUntil, string reason, long operatorId, string operatorName)
    {
        if (userId <= 0)
        {
            Log.Warning("冻结用户经验失败：userId 无效（{UserId}）", userId);
            return false;
        }

        if (frozenUntil.HasValue && frozenUntil.Value <= DateTime.Now)
        {
            Log.Warning("冻结用户经验失败：冻结到期时间早于当前时间，userId={UserId}, frozenUntil={FrozenUntil}", userId, frozenUntil);
            return false;
        }

        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "管理员冻结经验" : reason.Trim();

        try
        {
            return await ExecuteWithRetryAsync(async () =>
                await FreezeExperienceInternalAsync(userId, frozenUntil, normalizedReason, operatorId, operatorName));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "冻结用户 {UserId} 经验失败", userId);
            return false;
        }
    }

    /// <summary>
    /// 解冻用户经验值
    /// </summary>
    public async Task<bool> UnfreezeExperienceAsync(long userId, long operatorId, string operatorName)
    {
        if (userId <= 0)
        {
            Log.Warning("解冻用户经验失败：userId 无效（{UserId}）", userId);
            return false;
        }

        try
        {
            return await ExecuteWithRetryAsync(async () =>
                await UnfreezeExperienceInternalAsync(userId, operatorId, operatorName));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "解冻用户 {UserId} 经验失败", userId);
            return false;
        }
    }

    [UseTran(Propagation = Propagation.Required)]
    private async Task<bool> FreezeExperienceInternalAsync(
        long userId,
        DateTime? frozenUntil,
        string reason,
        long operatorId,
        string operatorName)
    {
        var userExp = await _userExpRepository.QueryFirstAsync(e => e.UserId == userId && !e.IsDeleted);
        if (userExp == null)
        {
            userExp = await InitializeUserExperienceAsync(userId);
            if (userExp == null)
            {
                Log.Error("冻结经验失败：用户 {UserId} 经验值记录初始化失败", userId);
                return false;
            }
        }

        var now = DateTime.Now;
        var updatedRows = await _userExpRepository.UpdateColumnsAsync(
            e => new UserExperience
            {
                ExpFrozen = true,
                FrozenUntil = frozenUntil,
                FrozenReason = reason,
                Version = e.Version + 1,
                ModifyTime = now,
                ModifyBy = operatorName,
                ModifyId = operatorId
            },
            e => e.UserId == userId && e.Version == userExp.Version && !e.IsDeleted
        );

        if (updatedRows == 0)
        {
            throw new ConcurrencyException("乐观锁冲突：经验冻结状态已被其他操作修改");
        }

        var targetInfo = await ResolveGovernanceTargetAsync(userId, userExp.TenantId);
        await AddGovernanceActionAsync(
            targetUserId: userId,
            targetUserName: targetInfo.UserName,
            tenantId: targetInfo.TenantId,
            actionType: ExperienceGovernanceActionTypeEnum.Freeze,
            remark: NormalizeRequiredSnapshotText(reason, "管理员冻结经验"),
            operatorId: operatorId,
            operatorName: operatorName,
            frozenUntil: frozenUntil);

        Log.Information("用户 {UserId} 经验已冻结，frozenUntil={FrozenUntil}, reason={Reason}", userId, frozenUntil, reason);
        return true;
    }

    [UseTran(Propagation = Propagation.Required)]
    private async Task<bool> UnfreezeExperienceInternalAsync(long userId, long operatorId, string operatorName)
    {
        var userExp = await _userExpRepository.QueryFirstAsync(e => e.UserId == userId && !e.IsDeleted);
        if (userExp == null)
        {
            userExp = await InitializeUserExperienceAsync(userId);
            if (userExp == null)
            {
                Log.Error("解冻经验失败：用户 {UserId} 经验值记录初始化失败", userId);
                return false;
            }
        }

        var needsReset = userExp.ExpFrozen || userExp.FrozenUntil.HasValue || !string.IsNullOrWhiteSpace(userExp.FrozenReason);
        if (!needsReset)
        {
            return true;
        }

        var unfreezeRemark = BuildUnfreezeGovernanceRemark(userExp.FrozenReason);
        var now = DateTime.Now;
        var updatedRows = await _userExpRepository.UpdateColumnsAsync(
            e => new UserExperience
            {
                ExpFrozen = false,
                FrozenUntil = null,
                FrozenReason = string.Empty,
                Version = e.Version + 1,
                ModifyTime = now,
                ModifyBy = operatorName,
                ModifyId = operatorId
            },
            e => e.UserId == userId && e.Version == userExp.Version && !e.IsDeleted
        );

        if (updatedRows == 0)
        {
            throw new ConcurrencyException("乐观锁冲突：经验解冻状态已被其他操作修改");
        }

        var targetInfo = await ResolveGovernanceTargetAsync(userId, userExp.TenantId);
        await AddGovernanceActionAsync(
            targetUserId: userId,
            targetUserName: targetInfo.UserName,
            tenantId: targetInfo.TenantId,
            actionType: ExperienceGovernanceActionTypeEnum.Unfreeze,
            remark: unfreezeRemark,
            operatorId: operatorId,
            operatorName: operatorName);

        Log.Information("用户 {UserId} 经验已解冻", userId);
        return true;
    }

    private async Task<UserExperience> NormalizeFreezeStateAsync(UserExperience userExp)
    {
        var now = DateTime.Now;
        if (!IsFreezeExpired(userExp, now))
        {
            return userExp;
        }

        var updatedRows = await _userExpRepository.UpdateColumnsAsync(
            e => new UserExperience
            {
                ExpFrozen = false,
                FrozenUntil = null,
                FrozenReason = string.Empty,
                Version = e.Version + 1,
                ModifyTime = now,
                ModifyBy = "System",
                ModifyId = 0
            },
            e => e.Id == userExp.Id && e.Version == userExp.Version && !e.IsDeleted
        );

        if (updatedRows > 0)
        {
            userExp.ExpFrozen = false;
            userExp.FrozenUntil = null;
            userExp.FrozenReason = string.Empty;
            userExp.Version += 1;
            userExp.ModifyTime = now;
            userExp.ModifyBy = "System";
            userExp.ModifyId = 0;
            Log.Information("用户 {UserId} 的临时经验冻结已到期，自动释放", userExp.UserId);
            return userExp;
        }

        var refreshed = await _userExpRepository.QueryFirstAsync(e => e.Id == userExp.Id && !e.IsDeleted);
        return refreshed ?? userExp;
    }

    private static bool IsFreezeActive(UserExperience userExp, DateTime referenceTime)
        => userExp.ExpFrozen && (!userExp.FrozenUntil.HasValue || userExp.FrozenUntil > referenceTime);

    private static bool IsFreezeExpired(UserExperience userExp, DateTime referenceTime)
        => userExp.ExpFrozen && userExp.FrozenUntil.HasValue && userExp.FrozenUntil.Value <= referenceTime;
}
