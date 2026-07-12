using Radish.Common;
using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.Model;
using Serilog;
using SqlSugar;

namespace Radish.Service;

public partial class ExperienceService
{
    /// <summary>
    /// 管理员调整用户经验值
    /// </summary>
    public async Task<bool> AdminAdjustExperienceAsync(
        long userId,
        int deltaExp,
        string reason,
        long operatorId,
        string operatorName)
    {
        if (userId <= 0)
        {
            Log.Warning("管理员调整经验失败：userId 无效（{UserId}）", userId);
            return false;
        }

        if (deltaExp == 0)
        {
            Log.Warning("管理员调整经验失败：变动量不能为 0，userId={UserId}", userId);
            return false;
        }

        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "管理员调整" : reason.Trim();

        try
        {
            return await ExecuteWithRetryAsync(async () =>
                await AdminAdjustExperienceInternalAsync(userId, deltaExp, normalizedReason, operatorId, operatorName));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "管理员调整用户 {UserId} 经验失败，deltaExp={DeltaExp}", userId, deltaExp);
            return false;
        }
    }

    [UseTran(Propagation = Propagation.Required)]
    private async Task<bool> AdminAdjustExperienceInternalAsync(
        long userId,
        int deltaExp,
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
                Log.Error("管理员调整经验失败：用户 {UserId} 经验值记录初始化失败", userId);
                return false;
            }
        }

        userExp = await NormalizeFreezeStateAsync(userExp);

        var levelConfigs = await GetLevelConfigsCacheAsync();
        var oldTotalExp = userExp.TotalExp;
        var oldLevel = userExp.CurrentLevel;
        var newTotalExp = oldTotalExp + deltaExp;
        var actualDelta = deltaExp;

        if (newTotalExp < 0)
        {
            actualDelta = -(int)oldTotalExp;
            newTotalExp = 0;
        }

        if (actualDelta == 0)
        {
            Log.Warning("管理员调整经验未生效：userId={UserId}, oldTotalExp={OldTotalExp}, deltaExp={DeltaExp}",
                userId, oldTotalExp, deltaExp);
            return false;
        }

        var (newLevel, newCurrentExp) = CalculateLevel(newTotalExp, levelConfigs);
        var now = GetUtcNow();
        var businessDate = _businessCalendar.GetDate(new DateTimeOffset(now));

        var updatedRows = await _userExpRepository.UpdateColumnsAsync(
            e => new UserExperience
            {
                CurrentLevel = newLevel,
                CurrentExp = newCurrentExp,
                TotalExp = newTotalExp,
                LevelUpAt = newLevel > oldLevel ? now : e.LevelUpAt,
                Version = e.Version + 1,
                ModifyTime = now,
                ModifyBy = operatorName,
                ModifyId = operatorId
            },
            e => e.UserId == userId && e.Version == userExp.Version && !e.IsDeleted
        );

        if (updatedRows == 0)
        {
            throw new ConcurrencyException("乐观锁冲突：管理员调整经验时记录已被其他操作修改");
        }

        var transaction = new ExpTransaction
        {
            Id = SnowFlakeSingle.instance.getID(),
            UserId = userId,
            ExpType = actualDelta > 0 ? "ADMIN_ADJUST" : "PENALTY",
            ExpAmount = actualDelta,
            BusinessType = "User",
            BusinessId = userId,
            Remark = reason,
            ExpBefore = oldTotalExp,
            ExpAfter = newTotalExp,
            LevelBefore = oldLevel,
            LevelAfter = newLevel,
            CreatedDate = GetBusinessDateStorageValue(businessDate),
            TenantId = userExp.TenantId,
            CreateTime = now,
            CreateBy = operatorName,
            CreateId = operatorId
        };
        await _expTransactionRepository.AddAsync(transaction);

        Log.Information(
            "管理员 {OperatorName}({OperatorId}) 调整用户 {UserId} 经验成功，实际变动 {ActualDelta}，总经验 {OldTotalExp} -> {NewTotalExp}",
            operatorName,
            operatorId,
            userId,
            actualDelta,
            oldTotalExp,
            newTotalExp);

        if (newLevel > oldLevel)
        {
            Log.Information("管理员调整触发升级：用户 {UserId} 从 Lv.{OldLevel} 升级到 Lv.{NewLevel}", userId, oldLevel, newLevel);
            var reliableOutboxService = _reliableOutboxService
                ?? throw new InvalidOperationException("可靠 Outbox 服务未注册");
            await reliableOutboxService.AddAsync(
                ReliableOutboxSources.Main,
                transaction.TenantId,
                ReliableTaskTypes.ExperienceLevelChanged,
                $"task:level-change:exp-transaction:{transaction.Id}",
                "ExpTransaction",
                transaction.Id.ToString(),
                new ExperienceLevelChangedTaskPayload(
                    transaction.Id,
                    userId,
                    oldLevel,
                    newLevel,
                    newLevel * 100L,
                    SnowFlakeSingle.Instance.NextId()),
                now);
        }

        return true;
    }
}
