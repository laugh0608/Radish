using Radish.Common.AttributeTool;
using Radish.Common.Exceptions;
using Radish.IRepository;
using Radish.IService;
using Radish.Model;
using Radish.Model.DtoModels;
using Radish.Model.ViewModels;
using Radish.Shared.Constants;
using Radish.Shared.CustomEnum;
using Serilog;

namespace Radish.Service;

public partial class ExperienceService
{
    /// <summary>记录会推进经验聚合版本的幂等人工复核结论。</summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task<AdminExperienceGovernanceResultVo> RecordGovernanceReviewAsync(
        AdminRecordExperienceGovernanceReviewDto request,
        long operatorId,
        string operatorName)
    {
        ValidateExperienceMutationIdentity(request.UserId, operatorId, request.ExpectedVersion);
        var reviewResult = ParseGovernanceReviewResult(request.ReviewResult);
        if (reviewResult == ExperienceGovernanceReviewResultEnum.Unknown)
        {
            throw new BusinessException(
                "经验治理复核结论无效",
                400,
                "Experience.ReviewResultInvalid",
                "error.experience.review_result_invalid");
        }

        var normalizedRemark = NormalizeExperienceMutationReason(request.Remark, "复核备注");
        var normalizedOperatorName = NormalizeExperienceOperatorName(operatorId, operatorName);
        var current = await RequireExperienceTargetAsync(request.UserId);
        var idempotency = await BeginReviewIdempotencyAsync(
            current.TenantId,
            request,
            normalizedRemark,
            operatorId);
        var replay = ResolveReviewReplay(idempotency);
        if (replay != null)
        {
            replay.VoReplayed = true;
            return replay;
        }

        EnsureExperienceVersion(current.Version, request.ExpectedVersion);

        var target = await ResolveGovernanceTargetAsync(request.UserId, current.TenantId);
        var action = CreateGovernanceAction(
            request.UserId,
            target.UserName,
            current.TenantId,
            ExperienceGovernanceActionTypeEnum.Review,
            normalizedRemark,
            operatorId,
            normalizedOperatorName,
            reviewResult,
            request.WindowDays,
            request.StatDate,
            request.RuleCodes,
            request.RuleLabels,
            request.RecommendationLevel,
            request.RecommendationReason);
        var result = await ApplyGovernanceActionAsync(
            current.TenantId,
            request.UserId,
            request.ExpectedVersion,
            current.ExpFrozen,
            current.FrozenUntil,
            current.FrozenReason ?? string.Empty,
            action,
            operatorId,
            normalizedOperatorName,
            action.CreateTime);
        await CompleteExperienceIdempotencyAsync(
            idempotency,
            OperationIdempotencyResourceTypes.UserExperienceGovernanceAction,
            result.VoAction.VoActionId,
            result);

        Log.Information(
            "管理员 {OperatorName}({OperatorId}) 记录用户 {UserId} 经验治理复核结论，经验版本 {ExpectedVersion} -> {ResultVersion}",
            normalizedOperatorName,
            operatorId,
            request.UserId,
            request.ExpectedVersion,
            result.VoExperience.VoVersion);
        return result;
    }

    public async Task<ExperienceLevelRecalculationPreviewVo> PreviewLevelConfigRecalculationAsync()
    {
        var plan = await BuildLevelRecalculationPlanAsync();
        return plan.Preview;
    }

    public async Task<ExperienceLevelRecalculationResultVo> RecalculateLevelConfigsAsync(
        RecalculateLevelConfigsDto request,
        long operatorId,
        string operatorName)
    {
        if (operatorId <= 0)
        {
            throw new BusinessException("操作员 ID 无效", 400, "Experience.OperatorInvalid", "error.common.validation_failed");
        }

        var normalizedReason = NormalizeExperienceMutationReason(request.Reason, "重算原因");
        var normalizedOperatorName = NormalizeExperienceOperatorName(operatorId, operatorName);
        var plan = await BuildLevelRecalculationPlanAsync();
        if (plan.Preview.VoMissingLevels.Count > 0)
        {
            throw new BusinessException(
                "等级配置不完整，无法执行整批重算",
                409,
                ExperienceGovernanceErrorCodes.LevelPreviewConflict,
                "error.experience.level_preview_conflict");
        }

        if (!string.Equals(request.ExpectedFingerprint.Trim(), plan.Preview.VoFingerprint, StringComparison.Ordinal))
        {
            throw CreateLevelPreviewConflictException();
        }

        ExperienceLevelRecalculationMutationResult writeResult;
        try
        {
            writeResult = await _experienceGovernanceRepository.ApplyLevelRecalculationAsync(
                new ExperienceLevelRecalculationCommand(
                    plan.Preview.VoFormulaType,
                    plan.Preview.VoFormulaSummary,
                    plan.Preview.VoFingerprint,
                    plan.Targets,
                    normalizedReason,
                    operatorId,
                    normalizedOperatorName,
                    GetUtcNow()));
        }
        catch (ExperienceLevelRecalculationPreviewConflictException)
        {
            throw CreateLevelPreviewConflictException();
        }
        catch (ExperienceLevelRecalculationNoChangesException)
        {
            throw new BusinessException(
                "等级配置与公式结果一致，无需重算",
                409,
                ExperienceGovernanceErrorCodes.LevelNoChanges,
                "error.experience.level_no_changes");
        }

        _experienceCalculator.ClearCache();
        await InvalidateLevelConfigsCacheAsync();
        var levelVos = Mapper.Map<List<LevelConfigVo>>(writeResult.LevelConfigs);
        FillLevelConfigUrls(levelVos);
        Log.Information(
            "管理员 {OperatorName}({OperatorId}) 完成等级配置整批重算，变更 {ChangedLevelCount} 级，审计 {AuditId}",
            normalizedOperatorName,
            operatorId,
            writeResult.Audit.ChangedLevelCount,
            writeResult.Audit.Id);
        return new ExperienceLevelRecalculationResultVo
        {
            VoLevels = levelVos,
            VoAudit = MapLevelRecalculationAudit(writeResult.Audit)
        };
    }

    public async Task<PageModel<ExperienceLevelRecalculationAuditVo>> GetLevelRecalculationAuditsAsync(
        int pageIndex = 1,
        int pageSize = 20)
    {
        var safePage = Math.Max(1, pageIndex);
        var safePageSize = Math.Clamp(pageSize, 1, 50);
        var (items, total) = await _experienceGovernanceRepository.QueryLevelRecalculationAuditsAsync(
            new ExperienceLevelRecalculationAuditPageQuery(safePage, safePageSize));
        return new PageModel<ExperienceLevelRecalculationAuditVo>
        {
            Page = safePage,
            PageSize = safePageSize,
            DataCount = total,
            PageCount = (int)Math.Ceiling(total / (double)safePageSize),
            Data = items.Select(MapLevelRecalculationAudit).ToList()
        };
    }

    private async Task<OperationIdempotencyBeginResult> BeginReviewIdempotencyAsync(
        long tenantId,
        AdminRecordExperienceGovernanceReviewDto request,
        string normalizedRemark,
        long operatorId)
    {
        var service = _operationIdempotencyService
            ?? throw new InvalidOperationException("经验治理幂等服务未注册");
        var snapshot = service.CreateRequestSnapshot(new Dictionary<string, object?>
        {
            ["targetUserId"] = request.UserId,
            ["reviewResult"] = request.ReviewResult,
            ["remark"] = normalizedRemark,
            ["windowDays"] = request.WindowDays,
            ["statDate"] = request.StatDate?.ToString("yyyy-MM-dd"),
            ["ruleCodes"] = request.RuleCodes,
            ["ruleLabels"] = request.RuleLabels,
            ["recommendationLevel"] = request.RecommendationLevel,
            ["recommendationReason"] = request.RecommendationReason,
            ["expectedVersion"] = request.ExpectedVersion
        });
        return await service.BeginAsync(new OperationIdempotencyBeginRequest
        {
            TenantId = tenantId,
            UserId = operatorId,
            OperationType = OperationIdempotencyOperationTypes.ExperienceGovernanceReview,
            IdempotencyKey = service.NormalizeKey(request.IdempotencyKey),
            RequestHash = snapshot.RequestHash,
            RequestSummary = snapshot.RequestSummary,
            AllowExpiredProcessingReset = false
        });
    }

    private AdminExperienceGovernanceResultVo? ResolveReviewReplay(OperationIdempotencyBeginResult idempotency)
    {
        return idempotency.Status switch
        {
            OperationIdempotencyBeginStatus.Started => null,
            OperationIdempotencyBeginStatus.Succeeded =>
                _operationIdempotencyService!.DeserializeResponse<AdminExperienceGovernanceResultVo>(
                    idempotency.ResponsePayload)
                ?? throw new BusinessException(
                    "幂等记录缺少人工复核结果，请刷新经验与治理记录",
                    409,
                    ExperienceGovernanceErrorCodes.ReviewReplayUnavailable,
                    "error.experience.review_replay_unavailable"),
            OperationIdempotencyBeginStatus.Processing => throw new BusinessException(
                idempotency.Message ?? "相同人工复核请求仍在处理中",
                409,
                ExperienceGovernanceErrorCodes.ReviewProcessing,
                "error.experience.review_processing"),
            OperationIdempotencyBeginStatus.Conflict => throw new BusinessException(
                idempotency.Message ?? "幂等键已用于其他人工复核内容",
                409,
                ExperienceGovernanceErrorCodes.ReviewIdempotencyConflict,
                "error.experience.review_idempotency_conflict"),
            OperationIdempotencyBeginStatus.InvalidKey => throw new BusinessException(
                idempotency.Message ?? "人工复核请求标识格式无效",
                400,
                ExperienceGovernanceErrorCodes.ReviewIdempotencyInvalid,
                "error.experience.review_idempotency_invalid"),
            _ => throw new InvalidOperationException("人工复核幂等记录状态无效")
        };
    }

    private async Task<LevelRecalculationPlan> BuildLevelRecalculationPlanAsync()
    {
        var current = await _experienceGovernanceRepository.QueryLevelConfigsAsync();
        var calculated = _experienceCalculator.CalculateAllLevels();
        var targets = calculated
            .OrderBy(item => item.Key)
            .Select(item => new ExperienceLevelRecalculationTarget(
                item.Key,
                item.Value.ExpRequired,
                item.Value.ExpCumulative))
            .ToList();
        var formulaType = _experienceCalculator.GetFormulaType().Trim();
        var formulaSummary = _experienceCalculator.GetConfigSummary().Trim();
        var fingerprint = ExperienceLevelRecalculationFingerprint.Compute(
            formulaType,
            formulaSummary,
            current.Select(item => new ExperienceLevelSnapshot(item.Level, item.ExpRequired, item.ExpCumulative)),
            targets);
        var currentMap = current.ToDictionary(item => item.Level);
        var changes = targets.Select(target =>
        {
            currentMap.TryGetValue(target.Level, out var before);
            return new ExperienceLevelRecalculationChangeVo
            {
                VoLevel = target.Level,
                VoLevelName = before?.LevelName ?? $"Lv.{target.Level}",
                VoBeforeExpRequired = before?.ExpRequired ?? 0,
                VoAfterExpRequired = target.ExpRequired,
                VoBeforeExpCumulative = before?.ExpCumulative ?? 0,
                VoAfterExpCumulative = target.ExpCumulative,
                VoChanged = before == null ||
                    before.ExpRequired != target.ExpRequired ||
                    before.ExpCumulative != target.ExpCumulative
            };
        }).ToList();
        return new LevelRecalculationPlan(
            new ExperienceLevelRecalculationPreviewVo
            {
                VoFingerprint = fingerprint,
                VoFormulaType = formulaType,
                VoFormulaSummary = formulaSummary,
                VoChangedLevelCount = changes.Count(item => item.VoChanged),
                VoMissingLevels = targets.Where(item => !currentMap.ContainsKey(item.Level)).Select(item => item.Level).ToList(),
                VoChanges = changes
            },
            targets);
    }

    private static ExperienceLevelRecalculationAuditVo MapLevelRecalculationAudit(
        ExperienceLevelRecalculationAudit audit)
    {
        return new ExperienceLevelRecalculationAuditVo
        {
            VoAuditId = audit.Id,
            VoFormulaType = audit.FormulaType,
            VoFormulaSummary = audit.FormulaSummary,
            VoPreviewFingerprint = audit.PreviewFingerprint,
            VoChangedLevelCount = audit.ChangedLevelCount,
            VoReason = audit.Reason,
            VoOperatorId = audit.CreateId,
            VoOperatorName = audit.CreateBy,
            VoCreateTime = audit.CreateTime
        };
    }

    private async Task<UserExperience> RequireExperienceTargetAsync(long userId)
    {
        return await _userExpRepository.QueryFirstAsync(item => item.UserId == userId && !item.IsDeleted)
            ?? throw CreateExperienceTargetUnavailableException();
    }

    private static void ValidateExperienceMutationIdentity(long userId, long operatorId, int expectedVersion)
    {
        if (userId <= 0 || operatorId <= 0 || expectedVersion < 0)
        {
            throw new BusinessException(
                "经验治理请求参数无效",
                400,
                "Experience.MutationInvalid",
                "error.common.validation_failed");
        }
    }

    private static string NormalizeExperienceMutationReason(string? reason, string fieldName)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BusinessException($"{fieldName}不能为空", 400, "Experience.ReasonRequired", "error.common.validation_failed");
        }

        if (normalized.Length > 500)
        {
            throw new BusinessException($"{fieldName}不能超过 500 个字符", 400, "Experience.ReasonTooLong", "error.common.validation_failed");
        }

        return normalized;
    }

    private static string NormalizeExperienceOperatorName(long operatorId, string? operatorName)
        => string.IsNullOrWhiteSpace(operatorName) ? $"User_{operatorId}" : operatorName.Trim();

    private static void EnsureExperienceVersion(int actualVersion, int expectedVersion)
    {
        if (actualVersion != expectedVersion)
        {
            throw CreateExperienceVersionConflictException();
        }
    }

    private static BusinessException CreateExperienceTargetUnavailableException()
        => new(
            "经验治理目标不存在或不可用",
            404,
            ExperienceGovernanceErrorCodes.TargetUnavailable,
            "error.experience.target_unavailable");

    private static BusinessException CreateExperienceVersionConflictException()
        => new(
            "经验状态已变化，请刷新目标后重新确认",
            409,
            ExperienceGovernanceErrorCodes.VersionConflict,
            "error.experience.version_conflict");

    private static BusinessException CreateLevelPreviewConflictException()
        => new(
            "等级配置或公式已变化，请重新生成预览后确认",
            409,
            ExperienceGovernanceErrorCodes.LevelPreviewConflict,
            "error.experience.level_preview_conflict");

    private sealed record LevelRecalculationPlan(
        ExperienceLevelRecalculationPreviewVo Preview,
        IReadOnlyList<ExperienceLevelRecalculationTarget> Targets);
}
