using Radish.Common;
using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Serilog;
using SqlSugar;

namespace Radish.Service;

public partial class ExperienceService
{
    /// <summary>管理员按已加载目标的权威版本调整经验。</summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<AdminExperienceAdjustmentResultVo> AdminAdjustExperienceAsync(
        long userId,
        int deltaExp,
        string reason,
        long operatorId,
        string operatorName,
        int expectedVersion,
        string idempotencyKey)
    {
        ValidateExperienceMutationIdentity(userId, operatorId, expectedVersion);
        if (deltaExp == 0)
        {
            throw new BusinessException(
                "经验变动量不能为 0",
                400,
                ExperienceGovernanceErrorCodes.AdjustmentWouldNotChange,
                "error.experience.adjustment_no_changes");
        }

        var normalizedReason = NormalizeExperienceMutationReason(reason, "调整原因");
        var normalizedOperatorName = NormalizeExperienceOperatorName(operatorId, operatorName);
        var current = await RequireExperienceTargetAsync(userId);
        var idempotency = await BeginAdjustmentIdempotencyAsync(
            current.TenantId,
            operatorId,
            userId,
            deltaExp,
            normalizedReason,
            expectedVersion,
            idempotencyKey);
        var replay = ResolveAdjustmentReplay(idempotency);
        if (replay != null)
        {
            replay.VoReplayed = true;
            return replay;
        }

        if (current.Version != expectedVersion)
        {
            throw CreateExperienceVersionConflictException();
        }

        var newTotalExp = checked(current.TotalExp + (long)deltaExp);
        if (newTotalExp < 0)
        {
            newTotalExp = 0;
        }

        var actualDelta = checked((int)(newTotalExp - current.TotalExp));
        if (actualDelta == 0)
        {
            throw new BusinessException(
                "经验调整不会改变目标状态",
                409,
                ExperienceGovernanceErrorCodes.AdjustmentWouldNotChange,
                "error.experience.adjustment_no_changes");
        }

        var levelConfigs = await GetLevelConfigsCacheAsync();
        var (newLevel, newCurrentExp) = CalculateLevel(newTotalExp, levelConfigs);
        var now = GetUtcNow();
        var transaction = new ExpTransaction
        {
            Id = SnowFlakeSingle.Instance.NextId(),
            UserId = userId,
            ExpType = actualDelta > 0 ? "ADMIN_ADJUST" : "PENALTY",
            ExpAmount = actualDelta,
            BusinessType = "User",
            BusinessId = userId,
            Remark = normalizedReason,
            ExpBefore = current.TotalExp,
            ExpAfter = newTotalExp,
            LevelBefore = current.CurrentLevel,
            LevelAfter = newLevel,
            CreatedDate = GetBusinessDateStorageValue(_businessCalendar.GetDate(new DateTimeOffset(now))),
            TenantId = current.TenantId,
            CreateTime = now,
            CreateBy = normalizedOperatorName,
            CreateId = operatorId
        };

        ExperienceAdjustmentMutationResult writeResult;
        try
        {
            writeResult = await _experienceGovernanceRepository.ApplyAdjustmentAsync(
                new ExperienceAdjustmentMutationCommand(
                    current.TenantId,
                    userId,
                    expectedVersion,
                    newLevel,
                    newCurrentExp,
                    newTotalExp,
                    newLevel > current.CurrentLevel ? now : current.LevelUpAt,
                    transaction,
                    operatorId,
                    normalizedOperatorName,
                    now));
        }
        catch (ExperienceGovernanceTargetUnavailableException)
        {
            throw CreateExperienceTargetUnavailableException();
        }
        catch (ExperienceGovernanceStateConflictException)
        {
            throw CreateExperienceVersionConflictException();
        }

        if (newLevel > current.CurrentLevel)
        {
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
                    current.CurrentLevel,
                    newLevel,
                    newLevel * 100L,
                    SnowFlakeSingle.Instance.NextId()),
                now);
        }

        var result = new AdminExperienceAdjustmentResultVo
        {
            VoExperience = await MapToVoAsync(writeResult.Experience)
                ?? throw new InvalidOperationException("经验调整结果映射失败"),
            VoTransaction = Mapper.Map<ExpTransactionVo>(writeResult.Transaction)
        };
        await FillTransactionUserNamesAsync([writeResult.Transaction], [result.VoTransaction]);
        await CompleteExperienceIdempotencyAsync(
            idempotency,
            OperationIdempotencyResourceTypes.ExpTransaction,
            transaction.Id,
            result);

        Log.Information(
            "管理员 {OperatorName}({OperatorId}) 调整用户 {UserId} 经验成功，经验版本 {ExpectedVersion} -> {ResultVersion}",
            normalizedOperatorName,
            operatorId,
            userId,
            expectedVersion,
            writeResult.Experience.Version);
        return result;
    }

    private async Task<OperationIdempotencyBeginResult> BeginAdjustmentIdempotencyAsync(
        long tenantId,
        long operatorId,
        long userId,
        int deltaExp,
        string reason,
        int expectedVersion,
        string idempotencyKey)
    {
        var service = _operationIdempotencyService
            ?? throw new InvalidOperationException("经验治理幂等服务未注册");
        var snapshot = service.CreateRequestSnapshot(new Dictionary<string, object?>
        {
            ["targetUserId"] = userId,
            ["deltaExp"] = deltaExp,
            ["reason"] = reason,
            ["expectedVersion"] = expectedVersion
        });
        return await service.BeginAsync(new OperationIdempotencyBeginRequest
        {
            TenantId = tenantId,
            UserId = operatorId,
            OperationType = OperationIdempotencyOperationTypes.ExperienceAdminAdjustment,
            IdempotencyKey = service.NormalizeKey(idempotencyKey),
            RequestHash = snapshot.RequestHash,
            RequestSummary = snapshot.RequestSummary,
            AllowExpiredProcessingReset = false
        });
    }

    private AdminExperienceAdjustmentResultVo? ResolveAdjustmentReplay(
        OperationIdempotencyBeginResult idempotency)
    {
        return idempotency.Status switch
        {
            OperationIdempotencyBeginStatus.Started => null,
            OperationIdempotencyBeginStatus.Succeeded =>
                _operationIdempotencyService!.DeserializeResponse<AdminExperienceAdjustmentResultVo>(
                    idempotency.ResponsePayload)
                ?? throw new BusinessException(
                    "幂等记录缺少经验调整结果，请刷新目标经验与流水",
                    409,
                    ExperienceGovernanceErrorCodes.AdjustmentReplayUnavailable,
                    "error.experience.adjustment_replay_unavailable"),
            OperationIdempotencyBeginStatus.Processing => throw new BusinessException(
                idempotency.Message ?? "相同经验调整仍在处理中",
                409,
                ExperienceGovernanceErrorCodes.AdjustmentProcessing,
                "error.experience.adjustment_processing"),
            OperationIdempotencyBeginStatus.Conflict => throw new BusinessException(
                idempotency.Message ?? "幂等键已用于其他经验调整内容",
                409,
                ExperienceGovernanceErrorCodes.AdjustmentIdempotencyConflict,
                "error.experience.adjustment_idempotency_conflict"),
            OperationIdempotencyBeginStatus.InvalidKey => throw new BusinessException(
                idempotency.Message ?? "经验调整请求标识格式无效",
                400,
                ExperienceGovernanceErrorCodes.AdjustmentIdempotencyInvalid,
                "error.experience.adjustment_idempotency_invalid"),
            _ => throw new InvalidOperationException("经验调整幂等记录状态无效")
        };
    }

    private async Task CompleteExperienceIdempotencyAsync<T>(
        OperationIdempotencyBeginResult idempotency,
        string resourceType,
        long resourceId,
        T response)
    {
        if (idempotency.Status != OperationIdempotencyBeginStatus.Started || !idempotency.RecordId.HasValue)
        {
            return;
        }

        await _operationIdempotencyService!.CompleteSuccessAsync(new OperationIdempotencyCompletionRequest
        {
            RecordId = idempotency.RecordId.Value,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ResourceNo = resourceId.ToString(),
            ResponsePayload = _operationIdempotencyService.SerializeResponse(response)
        });
    }
}
