using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>社区分发流配置</summary>
public sealed class FeedDistributionOptions : IConfigurableOptions
{
    /// <summary>候选内容时间窗口（天）</summary>
    public int CandidateWindowDays { get; set; } = 30;

    /// <summary>每次评分前最大候选数量</summary>
    public int MaxCandidateCount { get; set; } = 500;

    /// <summary>热门流权重配置</summary>
    public FeedHotWeightsOptions Hot { get; set; } = new();

    /// <summary>推荐流权重配置</summary>
    public FeedRecommendWeightsOptions Recommend { get; set; } = new();
}

/// <summary>热门流权重</summary>
public sealed class FeedHotWeightsOptions
{
    /// <summary>浏览权重</summary>
    public decimal ViewWeight { get; set; } = 1m;

    /// <summary>点赞权重</summary>
    public decimal LikeWeight { get; set; } = 2m;

    /// <summary>评论权重</summary>
    public decimal CommentWeight { get; set; } = 3m;
}

/// <summary>推荐流附加权重</summary>
public sealed class FeedRecommendWeightsOptions
{
    /// <summary>关注作者加权分</summary>
    public decimal FollowingAuthorBoost { get; set; } = 30m;

    /// <summary>新鲜度最大加权分</summary>
    public decimal FreshnessMaxBoost { get; set; } = 20m;

    /// <summary>新鲜度衰减半衰期（小时）</summary>
    public decimal FreshnessHalfLifeHours { get; set; } = 24m;
}
