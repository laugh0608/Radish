namespace Radish.Model.ViewModels;

/// <summary>
/// 单日经验观察标签
/// </summary>
public class UserExpDailyStatObservationVo
{
    /// <summary>
    /// 标签文案
    /// </summary>
    public string VoLabel { get; set; } = string.Empty;

    /// <summary>
    /// 标签语义色
    /// </summary>
    /// <remarks>当前约定为 default / success / processing / warning</remarks>
    public string VoTone { get; set; } = "default";

    /// <summary>
    /// 标签分类
    /// </summary>
    /// <remarks>当前约定为 context / anomaly</remarks>
    public string VoKind { get; set; } = "context";

    /// <summary>
    /// 规则编码
    /// </summary>
    public string VoRuleCode { get; set; } = string.Empty;

    /// <summary>
    /// 判定说明
    /// </summary>
    public string? VoDescription { get; set; }
}

/// <summary>
/// 经验统计窗口摘要
/// </summary>
public class UserExpDailyStatsSummaryVo
{
    /// <summary>
    /// 窗口总经验
    /// </summary>
    public long VoTotalExp { get; set; }

    /// <summary>
    /// 日均经验
    /// </summary>
    public double VoAverageExp { get; set; }

    /// <summary>
    /// 峰值单日经验
    /// </summary>
    public long VoPeakDayExp { get; set; }

    /// <summary>
    /// 峰值对应日期
    /// </summary>
    public DateOnly? VoPeakStatDate { get; set; }

    /// <summary>
    /// 零增长天数
    /// </summary>
    public int VoZeroGainDays { get; set; }

    /// <summary>
    /// 命中异常规则的天数
    /// </summary>
    public int VoReviewDays { get; set; }

    /// <summary>
    /// 管理侧观察提示
    /// </summary>
    public List<string> VoNotices { get; set; } = [];
}

/// <summary>
/// 异常规则摘要
/// </summary>
public class UserExpAnomalyRuleSummaryVo
{
    /// <summary>
    /// 规则编码
    /// </summary>
    public string VoRuleCode { get; set; } = string.Empty;

    /// <summary>
    /// 规则名称
    /// </summary>
    public string VoRuleLabel { get; set; } = string.Empty;

    /// <summary>
    /// 阈值说明
    /// </summary>
    public string VoThresholdDescription { get; set; } = string.Empty;

    /// <summary>
    /// 窗口命中天数
    /// </summary>
    public int VoHitDays { get; set; }

    /// <summary>
    /// 最近一次命中日期
    /// </summary>
    public DateOnly? VoLatestHitDate { get; set; }

    /// <summary>
    /// 当前最强信号
    /// </summary>
    public string VoStrongestSignal { get; set; } = string.Empty;

    /// <summary>
    /// 建议级别
    /// </summary>
    /// <remarks>当前约定为 observe / review / freeze-suggest</remarks>
    public string VoSeverity { get; set; } = "observe";

    /// <summary>
    /// 建议动作
    /// </summary>
    public string VoSuggestedAction { get; set; } = string.Empty;
}

/// <summary>
/// 经验治理建议
/// </summary>
public class UserExpGovernanceRecommendationVo
{
    /// <summary>
    /// 建议级别
    /// </summary>
    /// <remarks>当前约定为 normal / review / freeze-suggest</remarks>
    public string VoLevel { get; set; } = "normal";

    /// <summary>
    /// 建议标题
    /// </summary>
    public string VoTitle { get; set; } = "正常观察";

    /// <summary>
    /// 结论原因
    /// </summary>
    public string VoReason { get; set; } = string.Empty;

    /// <summary>
    /// 建议动作
    /// </summary>
    public string VoSuggestedAction { get; set; } = string.Empty;
}

/// <summary>
/// 当前生效的每日经验上限快照
/// </summary>
public class UserExpDailyLimitSnapshotVo
{
    /// <summary>
    /// 是否启用每日上限
    /// </summary>
    public bool VoDailyLimitEnabled { get; set; }

    /// <summary>
    /// 单日总经验上限
    /// </summary>
    public int VoMaxDailyExp { get; set; }

    /// <summary>
    /// 单日发帖经验上限
    /// </summary>
    public int VoMaxExpFromPost { get; set; }

    /// <summary>
    /// 单日评论经验上限
    /// </summary>
    public int VoMaxExpFromComment { get; set; }

    /// <summary>
    /// 单日点赞经验上限
    /// </summary>
    public int VoMaxExpFromLike { get; set; }

    /// <summary>
    /// 单日高亮经验上限
    /// </summary>
    public int VoMaxExpFromHighlight { get; set; }

    /// <summary>
    /// 单日登录经验上限
    /// </summary>
    public int VoMaxExpFromLogin { get; set; }
}

/// <summary>
/// 经验统计窗口
/// </summary>
public class UserExpDailyStatsWindowVo
{
    /// <summary>
    /// 查询窗口天数
    /// </summary>
    public int VoWindowDays { get; set; }

    /// <summary>
    /// 每日统计明细
    /// </summary>
    public List<UserExpDailyStatsVo> VoStats { get; set; } = [];

    /// <summary>
    /// 窗口摘要
    /// </summary>
    public UserExpDailyStatsSummaryVo? VoSummary { get; set; }

    /// <summary>
    /// 异常规则摘要
    /// </summary>
    public List<UserExpAnomalyRuleSummaryVo> VoRuleSummaries { get; set; } = [];

    /// <summary>
    /// 当前窗口治理建议
    /// </summary>
    public UserExpGovernanceRecommendationVo? VoRecommendation { get; set; }

    /// <summary>
    /// 当前生效的每日经验上限快照
    /// </summary>
    public UserExpDailyLimitSnapshotVo? VoLimits { get; set; }
}
