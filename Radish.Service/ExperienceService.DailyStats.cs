using Radish.Common.AttributeTool;
using Radish.Model;
using Radish.Model.ViewModels;
using Radish.Shared.CustomEnum;
using Serilog;
using SqlSugar;

namespace Radish.Service;

public partial class ExperienceService
{
    #region 每日统计

    /// <summary>
    /// 获取用户每日经验值统计
    /// </summary>
    public async Task<UserExpDailyStatsWindowVo> GetDailyStatsAsync(long userId, int days = 7)
    {
        try
        {
            var normalizedDays = days <= 0 ? 7 : Math.Min(days, 30);
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-normalizedDays + 1);
            var dailyLimits = GetDailyLimitOptions();

            var stats = await _dailyStatsRepository.QueryAsync(
                s => s.UserId == userId && s.StatDate >= startDate && s.StatDate <= endDate
            );

            var sortedStats = stats.OrderByDescending(s => s.StatDate).ToList();
            var statVos = Mapper.Map<List<UserExpDailyStatsVo>>(sortedStats);
            var normalizedStats = NormalizeDailyStatsWindow(userId, startDate, endDate, statVos, dailyLimits);
            var ruleSummaries = BuildAnomalyRuleSummaries(normalizedStats, dailyLimits);
            var recommendation = BuildGovernanceRecommendation(normalizedStats, ruleSummaries);

            return new UserExpDailyStatsWindowVo
            {
                VoWindowDays = normalizedDays,
                VoStats = normalizedStats,
                VoSummary = BuildDailyStatsSummary(normalizedStats, dailyLimits),
                VoRuleSummaries = ruleSummaries,
                VoRecommendation = recommendation,
                VoLimits = BuildDailyLimitSnapshot(dailyLimits)
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 每日统计失败", userId);
            throw;
        }
    }

    /// <summary>
    /// 更新每日统计（内部方法，经验值发放时调用）
    /// </summary>
    [UseTran(Propagation = Propagation.Required)]
    public async Task UpdateDailyStatsAsync(long userId, string expType, int amount, DateTime statDate)
    {
        if (userId <= 0)
        {
            Log.Warning("更新每日经验统计失败：userId 无效（{UserId}）", userId);
            return;
        }

        if (amount <= 0)
        {
            Log.Warning("更新每日经验统计失败：amount 必须大于 0，userId={UserId}, amount={Amount}", userId, amount);
            return;
        }

        var stats = await GetOrCreateDailyStatsAsync(userId, statDate);
        await UpdateDailyStatsAsync(stats, amount, expType);
    }

    /// <summary>
    /// 获取用户最近的经验治理留痕
    /// </summary>
    public async Task<List<UserExperienceGovernanceActionVo>> GetGovernanceActionsAsync(long userId, int take = 20)
    {
        try
        {
            var safeTake = NormalizeGovernanceActionTake(take);
            var (actions, _) = await _governanceActionRepository.QueryPageAsync(
                action => action.TargetUserId == userId && !action.IsDeleted,
                1,
                safeTake,
                action => action.CreateTime,
                OrderByType.Desc);

            return actions.Select(MapGovernanceAction).ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取用户 {UserId} 经验治理留痕失败", userId);
            throw;
        }
    }

    private static List<UserExpDailyStatsVo> NormalizeDailyStatsWindow(
        long userId,
        DateTime startDate,
        DateTime endDate,
        List<UserExpDailyStatsVo> stats,
        ExperienceDailyLimitSettings dailyLimits)
    {
        var statMap = stats
            .GroupBy(stat => stat.VoStatDate.Date)
            .ToDictionary(group => group.Key, group => group.First());

        var normalizedStats = new List<UserExpDailyStatsVo>();
        for (var date = endDate.Date; date >= startDate.Date; date = date.AddDays(-1))
        {
            if (!statMap.TryGetValue(date, out var stat))
            {
                stat = CreateEmptyDailyStatsVo(userId, date);
            }
            else
            {
                stat.VoStatDate = date;
            }

            stat.VoObservations = BuildDailyStatObservations(stat, dailyLimits);
            normalizedStats.Add(stat);
        }

        return normalizedStats;
    }

    private static UserExpDailyStatsVo CreateEmptyDailyStatsVo(long userId, DateTime statDate)
    {
        return new UserExpDailyStatsVo
        {
            VoId = 0,
            VoUserId = userId,
            VoStatDate = statDate.Date,
            VoExpEarned = 0,
            VoExpFromPost = 0,
            VoExpFromComment = 0,
            VoExpFromLike = 0,
            VoExpFromHighlight = 0,
            VoExpFromLogin = 0,
            VoPostCount = 0,
            VoCommentCount = 0,
            VoLikeGivenCount = 0,
            VoLikeReceivedCount = 0,
            VoObservations = []
        };
    }

    private static UserExpDailyStatsSummaryVo BuildDailyStatsSummary(
        List<UserExpDailyStatsVo> stats,
        ExperienceDailyLimitSettings dailyLimits)
    {
        if (stats.Count == 0)
        {
            return new UserExpDailyStatsSummaryVo
            {
                VoReviewDays = 0,
                VoNotices = ["当前窗口暂无经验统计数据。"]
            };
        }

        long totalExp = 0;
        var zeroGainDays = 0;
        var anomalyDays = 0;
        var likeHeavyDays = 0;
        var highlightHeavyDays = 0;
        var cappedDailyLimitDays = 0;
        var nearDailyLimitDays = 0;
        var sourceLimitDays = 0;
        var peakDayExp = long.MinValue;
        DateTime? peakStatDate = null;

        foreach (var stat in stats)
        {
            totalExp += stat.VoExpEarned;
            if (HasAnomalyObservation(stat))
            {
                anomalyDays++;
            }

            if (stat.VoExpEarned <= 0)
            {
                zeroGainDays++;
            }

            if (HasObservationCode(stat, ObservationLikeShareHeavy))
            {
                likeHeavyDays++;
            }

            if (HasObservationCode(stat, ObservationHighlightRewardClustered))
            {
                highlightHeavyDays++;
            }

            if (HasObservationCode(stat, ObservationTotalLimitHit))
            {
                cappedDailyLimitDays++;
            }
            else if (HasObservationCode(stat, ObservationTotalLimitNear))
            {
                nearDailyLimitDays++;
            }

            if (
                HasObservationCode(stat, ObservationLikeLimitHit)
                || HasObservationCode(stat, ObservationLikeLimitNear)
                || HasObservationCode(stat, ObservationHighlightLimitHit)
                || HasObservationCode(stat, ObservationHighlightLimitNear)
            )
            {
                sourceLimitDays++;
            }

            if (stat.VoExpEarned > peakDayExp)
            {
                peakDayExp = stat.VoExpEarned;
                peakStatDate = stat.VoStatDate.Date;
            }
        }

        var notices = new List<string>();
        if (!dailyLimits.EnableDailyLimit)
        {
            notices.Add("当前未启用每日经验上限，观察结果仅基于经验来源结构。");
        }
        else
        {
            if (cappedDailyLimitDays > 0)
            {
                notices.Add($"最近 {stats.Count} 天中有 {cappedDailyLimitDays} 天触达当日总上限。");
            }
            else if (nearDailyLimitDays > 0)
            {
                notices.Add($"最近 {stats.Count} 天中有 {nearDailyLimitDays} 天接近当日总上限。");
            }

            if (sourceLimitDays > 0)
            {
                notices.Add($"其中 {sourceLimitDays} 天的点赞或高亮经验接近来源上限，建议结合行为计数复核。");
            }
        }

        if (zeroGainDays >= 3)
        {
            notices.Add($"最近 {stats.Count} 天中有 {zeroGainDays} 天没有经验增长。");
        }

        if (likeHeavyDays > 0)
        {
            notices.Add($"其中 {likeHeavyDays} 天经验主要来自点赞，建议结合互动来源复核。");
        }

        if (highlightHeavyDays > 0)
        {
            notices.Add($"其中 {highlightHeavyDays} 天经验主要来自高亮评论，建议确认是否集中触发奖励。");
        }

        if (notices.Count == 0)
        {
            notices.Add("最近窗口内经验分布整体平稳，暂未看到明显需要人工复核的集中模式。");
        }

        return new UserExpDailyStatsSummaryVo
        {
            VoTotalExp = totalExp,
            VoAverageExp = stats.Count == 0 ? 0 : totalExp / (double)stats.Count,
            VoPeakDayExp = peakDayExp == long.MinValue ? 0 : peakDayExp,
            VoPeakStatDate = peakStatDate,
            VoZeroGainDays = zeroGainDays,
            VoReviewDays = anomalyDays,
            VoNotices = notices
        };
    }

    private static List<UserExpDailyStatObservationVo> BuildDailyStatObservations(
        UserExpDailyStatsVo stat,
        ExperienceDailyLimitSettings dailyLimits)
    {
        if (stat.VoExpEarned <= 0)
        {
            return
            [
                CreateContextObservation("零增长", ObservationZeroGain, "default", "当日没有经验增长。")
            ];
        }

        var observations = new List<UserExpDailyStatObservationVo>();
        var dominantSource = GetDominantSourceObservation(stat);
        if (dominantSource != null)
        {
            observations.Add(dominantSource);
        }

        if (dailyLimits.EnableDailyLimit)
        {
            AddLimitObservation(
                observations,
                stat.VoExpEarned,
                dailyLimits.MaxDailyExp,
                ObservationTotalLimitNear,
                ObservationTotalLimitHit,
                "接近总上限",
                "触达总上限");
            AddLimitObservation(
                observations,
                stat.VoExpFromLike,
                dailyLimits.MaxExpFromLike,
                ObservationLikeLimitNear,
                ObservationLikeLimitHit,
                "点赞接近上限",
                "点赞触达上限");
            AddLimitObservation(
                observations,
                stat.VoExpFromHighlight,
                dailyLimits.MaxExpFromHighlight,
                ObservationHighlightLimitNear,
                ObservationHighlightLimitHit,
                "高亮接近上限",
                "高亮触达上限");
        }

        if (stat.VoExpEarned >= LikeShareHeavyMinExp && stat.VoExpFromLike / (double)stat.VoExpEarned >= LikeShareHeavyRatioThreshold)
        {
            var likeRatio = stat.VoExpFromLike / (double)stat.VoExpEarned;
            observations.Add(CreateAnomalyObservation(
                "点赞占比偏高",
                ObservationLikeShareHeavy,
                $"点赞经验 {stat.VoExpFromLike}/{stat.VoExpEarned}，占比 {likeRatio:P0}（阈值 {LikeShareHeavyRatioThreshold:P0}）。"));
        }

        if (stat.VoExpEarned >= HighlightClusterMinExp && stat.VoExpFromHighlight / (double)stat.VoExpEarned >= HighlightClusterRatioThreshold)
        {
            var highlightRatio = stat.VoExpFromHighlight / (double)stat.VoExpEarned;
            observations.Add(CreateAnomalyObservation(
                "高亮奖励集中",
                ObservationHighlightRewardClustered,
                $"高亮经验 {stat.VoExpFromHighlight}/{stat.VoExpEarned}，占比 {highlightRatio:P0}（阈值 {HighlightClusterRatioThreshold:P0}）。"));
        }

        return observations;
    }

    private static List<UserExpAnomalyRuleSummaryVo> BuildAnomalyRuleSummaries(
        List<UserExpDailyStatsVo> stats,
        ExperienceDailyLimitSettings dailyLimits)
    {
        var repeatThreshold = GetRepeatThreshold(stats.Count);
        var groupedHits = stats
            .SelectMany(stat => stat.VoObservations
                .Where(IsAnomalyObservation)
                .Select(observation => new AnomalyObservationHit(stat.VoStatDate.Date, observation)))
            .GroupBy(hit => MapObservationToRuleCode(hit.Observation.VoRuleCode));

        var summaries = new List<UserExpAnomalyRuleSummaryVo>();
        foreach (var group in groupedHits)
        {
            var ruleCode = group.Key;
            if (string.IsNullOrWhiteSpace(ruleCode))
            {
                continue;
            }

            var hitDays = group
                .Select(hit => hit.StatDate)
                .Distinct()
                .Count();
            var latestHitDate = group.Max(hit => hit.StatDate);
            var strongestSignal = GetStrongestSignal(ruleCode, group.Select(hit => hit.Observation.VoRuleCode));
            var severity = DetermineRuleSeverity(ruleCode, strongestSignal, hitDays, repeatThreshold);

            summaries.Add(new UserExpAnomalyRuleSummaryVo
            {
                VoRuleCode = ruleCode,
                VoRuleLabel = GetRuleLabel(ruleCode),
                VoThresholdDescription = GetRuleThresholdDescription(ruleCode, dailyLimits),
                VoHitDays = hitDays,
                VoLatestHitDate = latestHitDate,
                VoStrongestSignal = strongestSignal,
                VoSeverity = severity,
                VoSuggestedAction = GetRuleSuggestedAction(ruleCode, severity)
            });
        }

        return summaries
            .OrderByDescending(summary => GetSeverityRank(summary.VoSeverity))
            .ThenByDescending(summary => summary.VoHitDays)
            .ThenByDescending(summary => summary.VoLatestHitDate)
            .ToList();
    }

    private static UserExpGovernanceRecommendationVo BuildGovernanceRecommendation(
        List<UserExpDailyStatsVo> stats,
        List<UserExpAnomalyRuleSummaryVo> ruleSummaries)
    {
        var anomalyDays = stats.Count(HasAnomalyObservation);
        if (anomalyDays == 0)
        {
            return new UserExpGovernanceRecommendationVo
            {
                VoLevel = "normal",
                VoTitle = "正常观察",
                VoReason = "最近窗口未命中异常规则，当前没有需要升级处理的集中模式。",
                VoSuggestedAction = "继续观察，不建议直接冻结。"
            };
        }

        var repeatThreshold = GetRepeatThreshold(stats.Count);
        var repeatedRules = ruleSummaries
            .Where(summary => summary.VoHitDays >= repeatThreshold)
            .Select(summary => summary.VoRuleLabel)
            .ToList();
        var limitHitDays = stats.Count(stat =>
            HasObservationCode(stat, ObservationTotalLimitHit)
            || HasObservationCode(stat, ObservationLikeLimitHit)
            || HasObservationCode(stat, ObservationHighlightLimitHit));
        var multiAnomalyDays = stats.Count(stat =>
            stat.VoObservations.Count(IsAnomalyObservation) >= 2);
        var freezeComboDays = stats.Count(HasFreezeSuggestionCombo);

        if (freezeComboDays >= repeatThreshold)
        {
            return new UserExpGovernanceRecommendationVo
            {
                VoLevel = "freeze-suggest",
                VoTitle = "可考虑临时冻结",
                VoReason = $"最近 {stats.Count} 天中有 {freezeComboDays} 天同时出现来源集中与上限触达，已达到冻结建议阈值（{repeatThreshold} 天）。",
                VoSuggestedAction = "先回看经验流水、互动来源和内容记录，确认异常后再考虑临时冻结。"
            };
        }

        if (repeatedRules.Count > 0)
        {
            return new UserExpGovernanceRecommendationVo
            {
                VoLevel = "review",
                VoTitle = "建议人工复核",
                VoReason = $"规则「{string.Join("、", repeatedRules)}」在最近 {stats.Count} 天重复命中，已达到人工复核阈值（{repeatThreshold} 天）。",
                VoSuggestedAction = "结合经验流水、互动来源和目标内容做人工复核，确认后再决定是否冻结。"
            };
        }

        if (limitHitDays > 0)
        {
            return new UserExpGovernanceRecommendationVo
            {
                VoLevel = "review",
                VoTitle = "建议人工复核",
                VoReason = $"最近 {stats.Count} 天中有 {limitHitDays} 天触达经验上限，建议核对对应来源与内容记录。",
                VoSuggestedAction = "优先回看命中日期的经验流水与行为来源，确认是否属于异常集中增长。"
            };
        }

        if (multiAnomalyDays > 0)
        {
            return new UserExpGovernanceRecommendationVo
            {
                VoLevel = "review",
                VoTitle = "建议人工复核",
                VoReason = $"最近 {stats.Count} 天中有 {multiAnomalyDays} 天同时命中多条异常规则，建议做一次人工交叉复核。",
                VoSuggestedAction = "结合经验流水、互动来源和目标内容做人工复核，暂不直接处罚。"
            };
        }

        return new UserExpGovernanceRecommendationVo
        {
            VoLevel = "normal",
            VoTitle = "正常观察",
            VoReason = $"最近 {stats.Count} 天共命中 {anomalyDays} 天异常规则，但都属于零星单次观察，尚未达到人工复核阈值。",
            VoSuggestedAction = "继续观察，不建议直接冻结。"
        };
    }

    private static UserExpDailyLimitSnapshotVo BuildDailyLimitSnapshot(ExperienceDailyLimitSettings dailyLimits)
    {
        return new UserExpDailyLimitSnapshotVo
        {
            VoDailyLimitEnabled = dailyLimits.EnableDailyLimit,
            VoMaxDailyExp = dailyLimits.MaxDailyExp,
            VoMaxExpFromPost = dailyLimits.MaxExpFromPost,
            VoMaxExpFromComment = dailyLimits.MaxExpFromComment,
            VoMaxExpFromLike = dailyLimits.MaxExpFromLike,
            VoMaxExpFromHighlight = dailyLimits.MaxExpFromHighlight,
            VoMaxExpFromLogin = dailyLimits.MaxExpFromLogin
        };
    }

    private static bool HasAnomalyObservation(UserExpDailyStatsVo stat)
    {
        return stat.VoObservations.Any(observation =>
            IsAnomalyObservation(observation));
    }

    private static bool IsAnomalyObservation(UserExpDailyStatObservationVo observation)
    {
        return string.Equals(observation.VoKind, ObservationKindAnomaly, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasObservationCode(UserExpDailyStatsVo stat, string ruleCode)
    {
        return stat.VoObservations.Any(observation =>
            string.Equals(observation.VoRuleCode, ruleCode, StringComparison.Ordinal));
    }

    private static int GetRepeatThreshold(int windowDays)
    {
        return windowDays <= 7 ? 2 : 4;
    }

    private static void AddLimitObservation(
        ICollection<UserExpDailyStatObservationVo> observations,
        long currentValue,
        int limitValue,
        string nearRuleCode,
        string hitRuleCode,
        string nearLabel,
        string hitLabel)
    {
        if (limitValue <= 0 || currentValue <= 0)
        {
            return;
        }

        if (currentValue >= limitValue)
        {
            observations.Add(CreateAnomalyObservation(
                hitLabel,
                hitRuleCode,
                $"当前值 {currentValue}，已达到配置上限 {limitValue}。"));
            return;
        }

        if (currentValue >= Math.Ceiling(limitValue * NearLimitRatioThreshold))
        {
            observations.Add(CreateAnomalyObservation(
                nearLabel,
                nearRuleCode,
                $"当前值 {currentValue}，已达到配置上限 {limitValue} 的 {currentValue / (double)limitValue:P0}。"));
        }
    }

    private static UserExpDailyStatObservationVo? GetDominantSourceObservation(UserExpDailyStatsVo stat)
    {
        var sourceObservations = new[]
        {
            CreateContextObservation("发帖驱动", ObservationSourcePost, "success", "当日经验主要来自发帖奖励。"),
            CreateContextObservation("评论驱动", ObservationSourceComment, "processing", "当日经验主要来自评论奖励。"),
            CreateContextObservation("点赞驱动", ObservationSourceLike, "processing", "当日经验主要来自点赞相关奖励。"),
            CreateContextObservation("高亮驱动", ObservationSourceHighlight, "processing", "当日经验主要来自高亮奖励。"),
            CreateContextObservation("登录驱动", ObservationSourceLogin, "default", "当日经验主要来自登录奖励。")
        };

        var sourceValues = new[]
        {
            stat.VoExpFromPost,
            stat.VoExpFromComment,
            stat.VoExpFromLike,
            stat.VoExpFromHighlight,
            stat.VoExpFromLogin
        };

        var dominantIndex = 0;
        for (var i = 1; i < sourceValues.Length; i++)
        {
            if (sourceValues[i] > sourceValues[dominantIndex])
            {
                dominantIndex = i;
            }
        }

        return sourceValues[dominantIndex] > 0 ? sourceObservations[dominantIndex] : null;
    }

    private static UserExpDailyStatObservationVo CreateContextObservation(
        string label,
        string ruleCode,
        string tone,
        string description)
    {
        return new UserExpDailyStatObservationVo
        {
            VoLabel = label,
            VoTone = tone,
            VoKind = ObservationKindContext,
            VoRuleCode = ruleCode,
            VoDescription = description
        };
    }

    private static UserExpDailyStatObservationVo CreateAnomalyObservation(
        string label,
        string ruleCode,
        string description)
    {
        return new UserExpDailyStatObservationVo
        {
            VoLabel = label,
            VoTone = "warning",
            VoKind = ObservationKindAnomaly,
            VoRuleCode = ruleCode,
            VoDescription = description
        };
    }

    private static string MapObservationToRuleCode(string observationRuleCode)
    {
        return observationRuleCode switch
        {
            ObservationTotalLimitNear or ObservationTotalLimitHit => RuleTotalLimitPressure,
            ObservationLikeLimitNear or ObservationLikeLimitHit => RuleLikeLimitPressure,
            ObservationHighlightLimitNear or ObservationHighlightLimitHit => RuleHighlightLimitPressure,
            ObservationLikeShareHeavy => RuleLikeShareHeavy,
            ObservationHighlightRewardClustered => RuleHighlightRewardClustered,
            _ => string.Empty
        };
    }

    private static string GetRuleLabel(string ruleCode)
    {
        return ruleCode switch
        {
            RuleTotalLimitPressure => "总经验接近/触达上限",
            RuleLikeLimitPressure => "点赞经验接近/触达上限",
            RuleHighlightLimitPressure => "高亮经验接近/触达上限",
            RuleLikeShareHeavy => "点赞占比偏高",
            RuleHighlightRewardClustered => "高亮奖励集中",
            _ => ruleCode
        };
    }

    private static string GetRuleThresholdDescription(string ruleCode, ExperienceDailyLimitSettings dailyLimits)
    {
        return ruleCode switch
        {
            RuleTotalLimitPressure =>
                $"接近：达到总经验上限的 {NearLimitRatioThreshold:P0}；触达：达到总经验上限 100%（当前上限 {dailyLimits.MaxDailyExp}）。",
            RuleLikeLimitPressure =>
                $"接近：达到点赞经验上限的 {NearLimitRatioThreshold:P0}；触达：达到点赞经验上限 100%（当前上限 {dailyLimits.MaxExpFromLike}）。",
            RuleHighlightLimitPressure =>
                $"接近：达到高亮经验上限的 {NearLimitRatioThreshold:P0}；触达：达到高亮经验上限 100%（当前上限 {dailyLimits.MaxExpFromHighlight}）。",
            RuleLikeShareHeavy =>
                $"单日总经验 >= {LikeShareHeavyMinExp}，且点赞经验占比 >= {LikeShareHeavyRatioThreshold:P0}。",
            RuleHighlightRewardClustered =>
                $"单日总经验 >= {HighlightClusterMinExp}，且高亮经验占比 >= {HighlightClusterRatioThreshold:P0}。",
            _ => "以当前经验治理规则为准。"
        };
    }

    private static string GetStrongestSignal(string ruleCode, IEnumerable<string> observationRuleCodes)
    {
        var ruleCodeSet = observationRuleCodes.ToHashSet(StringComparer.Ordinal);
        return ruleCode switch
        {
            RuleTotalLimitPressure => ruleCodeSet.Contains(ObservationTotalLimitHit) ? "触达上限" : "接近上限",
            RuleLikeLimitPressure => ruleCodeSet.Contains(ObservationLikeLimitHit) ? "触达上限" : "接近上限",
            RuleHighlightLimitPressure => ruleCodeSet.Contains(ObservationHighlightLimitHit) ? "触达上限" : "接近上限",
            RuleLikeShareHeavy => $"点赞经验占比达到 {LikeShareHeavyRatioThreshold:P0}",
            RuleHighlightRewardClustered => $"高亮经验占比达到 {HighlightClusterRatioThreshold:P0}",
            _ => "规则命中"
        };
    }

    private static string DetermineRuleSeverity(string ruleCode, string strongestSignal, int hitDays, int repeatThreshold)
    {
        if (hitDays >= repeatThreshold)
        {
            return "review";
        }

        if ((ruleCode == RuleTotalLimitPressure || ruleCode == RuleLikeLimitPressure || ruleCode == RuleHighlightLimitPressure)
            && string.Equals(strongestSignal, "触达上限", StringComparison.Ordinal))
        {
            return "review";
        }

        return "observe";
    }

    private static string GetRuleSuggestedAction(string ruleCode, string severity)
    {
        if (string.Equals(severity, "review", StringComparison.Ordinal))
        {
            return ruleCode switch
            {
                RuleTotalLimitPressure => "回看对应日期的经验流水与主要来源，确认是否存在集中刷取。",
                RuleLikeLimitPressure => "优先核对点赞来源、互动对象与内容分布，再决定是否冻结。",
                RuleHighlightLimitPressure => "核对高亮触发链路与目标评论，确认是否存在集中奖励。",
                RuleLikeShareHeavy => "结合互动来源和目标内容做人工复核，暂不直接处罚。",
                RuleHighlightRewardClustered => "核对高亮评论来源与触发链路，确认后再决定是否冻结。",
                _ => "结合经验流水做人工复核。"
            };
        }

        return "继续观察，暂不直接冻结。";
    }

    private static int GetSeverityRank(string severity)
    {
        return severity switch
        {
            "freeze-suggest" => 3,
            "review" => 2,
            "observe" => 1,
            _ => 0
        };
    }

    private static bool HasFreezeSuggestionCombo(UserExpDailyStatsVo stat)
    {
        var hasLikeHeavy = HasObservationCode(stat, ObservationLikeShareHeavy);
        var hasHighlightCluster = HasObservationCode(stat, ObservationHighlightRewardClustered);
        var hasDailyLimitHit = HasObservationCode(stat, ObservationTotalLimitHit);
        var hasLikeLimitHit = HasObservationCode(stat, ObservationLikeLimitHit);
        var hasHighlightLimitHit = HasObservationCode(stat, ObservationHighlightLimitHit);

        return (hasLikeHeavy && (hasLikeLimitHit || hasDailyLimitHit))
            || (hasHighlightCluster && (hasHighlightLimitHit || hasDailyLimitHit));
    }

    #endregion
}
