using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using System.Text.Json;

namespace Radish.Service;

public partial class ExperienceService
{
    private static int NormalizeGovernanceActionTake(int take)
    {
        if (take <= 0)
        {
            return DefaultGovernanceActionTake;
        }

        return Math.Min(take, MaxGovernanceActionTake);
    }

    private static int? NormalizeGovernanceWindowDays(int? windowDays)
    {
        if (!windowDays.HasValue || windowDays.Value <= 0)
        {
            return null;
        }

        return Math.Min(windowDays.Value, 30);
    }

    private static string NormalizeRequiredSnapshotText(string? value, string fallbackValue, int maxLength = 500)
    {
        var normalizedValue = NormalizeOptionalSnapshotText(value, maxLength);
        return string.IsNullOrWhiteSpace(normalizedValue)
            ? fallbackValue
            : normalizedValue;
    }

    private static string? NormalizeOptionalSnapshotText(string? value, int maxLength = 500)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? NormalizeRecommendationLevel(string? value)
    {
        return value?.Trim() switch
        {
            "normal" => "normal",
            "review" => "review",
            "freeze-suggest" => "freeze-suggest",
            _ => null
        };
    }

    private static string? SerializeSnapshotItems(IEnumerable<string>? values)
    {
        if (values == null)
        {
            return null;
        }

        var normalizedItems = values
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return normalizedItems.Count == 0 ? null : JsonSerializer.Serialize(normalizedItems, GovernanceSnapshotJsonOptions);
    }

    private static List<string> DeserializeSnapshotItems(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(value)
                ?.Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList()
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static ExperienceGovernanceReviewResultEnum ParseGovernanceReviewResult(string? value)
    {
        return value?.Trim() switch
        {
            "NoIssue" => ExperienceGovernanceReviewResultEnum.NoIssue,
            "Observe" => ExperienceGovernanceReviewResultEnum.Observe,
            "FreezeSuggest" => ExperienceGovernanceReviewResultEnum.FreezeSuggest,
            _ => ExperienceGovernanceReviewResultEnum.Unknown
        };
    }

    private static string GetGovernanceActionTypeCode(int actionType)
    {
        return (ExperienceGovernanceActionTypeEnum)actionType switch
        {
            ExperienceGovernanceActionTypeEnum.Review => "Review",
            ExperienceGovernanceActionTypeEnum.Freeze => "Freeze",
            ExperienceGovernanceActionTypeEnum.Unfreeze => "Unfreeze",
            _ => "Unknown"
        };
    }

    private static string GetGovernanceActionTypeDisplay(int actionType)
    {
        return (ExperienceGovernanceActionTypeEnum)actionType switch
        {
            ExperienceGovernanceActionTypeEnum.Review => "人工复核",
            ExperienceGovernanceActionTypeEnum.Freeze => "冻结经验",
            ExperienceGovernanceActionTypeEnum.Unfreeze => "解除冻结",
            _ => "未知动作"
        };
    }

    private static string? GetGovernanceReviewResultCode(int? reviewResult)
    {
        return reviewResult switch
        {
            (int)ExperienceGovernanceReviewResultEnum.NoIssue => "NoIssue",
            (int)ExperienceGovernanceReviewResultEnum.Observe => "Observe",
            (int)ExperienceGovernanceReviewResultEnum.FreezeSuggest => "FreezeSuggest",
            _ => null
        };
    }

    private static string? GetGovernanceReviewResultDisplay(int? reviewResult)
    {
        return reviewResult switch
        {
            (int)ExperienceGovernanceReviewResultEnum.NoIssue => "已复核，未见异常",
            (int)ExperienceGovernanceReviewResultEnum.Observe => "已复核，继续观察",
            (int)ExperienceGovernanceReviewResultEnum.FreezeSuggest => "已复核，可考虑冻结",
            _ => null
        };
    }

    private static string? GetGovernanceRecommendationTitle(string? recommendationLevel)
    {
        return recommendationLevel switch
        {
            "normal" => "正常观察",
            "review" => "建议人工复核",
            "freeze-suggest" => "可考虑临时冻结",
            _ => null
        };
    }

    private static string? BuildGovernanceEvidenceSummary(
        UserExperienceGovernanceAction action,
        IReadOnlyCollection<string> ruleLabels)
    {
        var segments = new List<string>();
        if (action.WindowDays.HasValue)
        {
            segments.Add($"窗口 {action.WindowDays.Value} 天");
        }

        if (action.StatDate.HasValue)
        {
            segments.Add($"命中日期 {action.StatDate.Value:yyyy-MM-dd}");
        }

        if (ruleLabels.Count > 0)
        {
            segments.Add($"规则 {string.Join("、", ruleLabels)}");
        }

        var recommendationTitle = GetGovernanceRecommendationTitle(action.RecommendationLevel);
        if (!string.IsNullOrWhiteSpace(recommendationTitle))
        {
            segments.Add($"建议 {recommendationTitle}");
        }

        return segments.Count == 0 ? null : string.Join("；", segments);
    }

    private static string BuildUnfreezeGovernanceRemark(string? frozenReason)
    {
        var normalizedReason = NormalizeOptionalSnapshotText(frozenReason);
        return string.IsNullOrWhiteSpace(normalizedReason)
            ? "管理员解除经验冻结"
            : NormalizeRequiredSnapshotText($"管理员解除经验冻结；原冻结原因：{normalizedReason}", "管理员解除经验冻结");
    }

    private UserExperienceGovernanceActionVo MapGovernanceAction(UserExperienceGovernanceAction action)
    {
        var ruleCodes = DeserializeSnapshotItems(action.RuleCodes);
        var ruleLabels = DeserializeSnapshotItems(action.RuleLabels);

        return new UserExperienceGovernanceActionVo
        {
            VoActionId = action.Id,
            VoTargetUserId = action.TargetUserId,
            VoTargetUserName = action.TargetUserName,
            VoActionType = GetGovernanceActionTypeCode(action.ActionType),
            VoActionTypeDisplay = GetGovernanceActionTypeDisplay(action.ActionType),
            VoReviewResult = GetGovernanceReviewResultCode(action.ReviewResult),
            VoReviewResultDisplay = GetGovernanceReviewResultDisplay(action.ReviewResult),
            VoRemark = action.Remark,
            VoEvidenceSummary = BuildGovernanceEvidenceSummary(action, ruleLabels),
            VoWindowDays = action.WindowDays,
            VoStatDate = action.StatDate,
            VoRuleCodes = ruleCodes,
            VoRuleLabels = ruleLabels,
            VoRecommendationLevel = action.RecommendationLevel,
            VoRecommendationTitle = GetGovernanceRecommendationTitle(action.RecommendationLevel),
            VoRecommendationReason = action.RecommendationReason,
            VoFrozenUntil = action.FrozenUntil,
            VoOperatorId = action.CreateId,
            VoOperatorName = action.CreateBy,
            VoCreateTime = action.CreateTime
        };
    }

    private async Task<ExperienceGovernanceTargetSnapshot> ResolveGovernanceTargetAsync(
        long userId,
        long fallbackTenantId = 0,
        string? fallbackUserName = null)
    {
        var targetUser = await _userRepository.QueryFirstAsync(user => user.Id == userId);
        var tenantId = fallbackTenantId > 0 ? fallbackTenantId : 0;
        if (tenantId <= 0)
        {
            var userExp = await _userExpRepository.QueryFirstAsync(exp => exp.UserId == userId && !exp.IsDeleted);
            tenantId = userExp?.TenantId ?? 0;
        }

        if (tenantId <= 0)
        {
            tenantId = targetUser?.TenantId ?? 0;
        }

        var userName = !string.IsNullOrWhiteSpace(targetUser?.UserName)
            ? targetUser!.UserName!.Trim()
            : !string.IsNullOrWhiteSpace(fallbackUserName)
                ? fallbackUserName.Trim()
                : $"User-{userId}";

        return new ExperienceGovernanceTargetSnapshot(tenantId, userName);
    }

    private async Task AddGovernanceActionAsync(
        long targetUserId,
        string targetUserName,
        long tenantId,
        ExperienceGovernanceActionTypeEnum actionType,
        string remark,
        long operatorId,
        string operatorName,
        ExperienceGovernanceReviewResultEnum? reviewResult = null,
        int? windowDays = null,
        DateTime? statDate = null,
        IEnumerable<string>? ruleCodes = null,
        IEnumerable<string>? ruleLabels = null,
        string? recommendationLevel = null,
        string? recommendationReason = null,
        DateTime? frozenUntil = null)
    {
        var now = DateTime.Now;
        var action = new UserExperienceGovernanceAction
        {
            TargetUserId = targetUserId,
            TargetUserName = NormalizeOptionalSnapshotText(targetUserName, 100) ?? $"User-{targetUserId}",
            TenantId = tenantId > 0 ? tenantId : 0,
            ActionType = (int)actionType,
            ReviewResult = reviewResult.HasValue && reviewResult.Value != ExperienceGovernanceReviewResultEnum.Unknown
                ? (int)reviewResult.Value
                : null,
            Remark = NormalizeRequiredSnapshotText(remark, "经验治理动作"),
            WindowDays = NormalizeGovernanceWindowDays(windowDays),
            StatDate = statDate?.Date,
            RuleCodes = SerializeSnapshotItems(ruleCodes),
            RuleLabels = SerializeSnapshotItems(ruleLabels),
            RecommendationLevel = NormalizeRecommendationLevel(recommendationLevel),
            RecommendationReason = NormalizeOptionalSnapshotText(recommendationReason),
            FrozenUntil = frozenUntil,
            CreateTime = now,
            CreateBy = operatorName,
            CreateId = operatorId
        };

        await _governanceActionRepository.AddAsync(action);
    }
}
