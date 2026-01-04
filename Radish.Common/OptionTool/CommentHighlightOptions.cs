using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>神评/沙发统计配置选项</summary>
public sealed class CommentHighlightOptions : IConfigurableOptions
{
    /// <summary>是否启用神评/沙发统计任务</summary>
    public bool Enable { get; set; } = true;

    /// <summary>Cron 调度表达式</summary>
    /// <remarks>默认：每天凌晨 1 点执行</remarks>
    public string Schedule { get; set; } = "0 1 * * *";

    /// <summary>任务描述</summary>
    public string Description { get; set; } = "神评/沙发统计任务";

    /// <summary>是否启用增量扫描</summary>
    /// <remarks>仅扫描最近有更新的帖子/评论</remarks>
    public bool IncrementalScan { get; set; } = true;

    /// <summary>扫描时间窗口（小时）</summary>
    /// <remarks>默认扫描最近 24 小时有更新的内容</remarks>
    public int ScanWindowHours { get; set; } = 24;

    /// <summary>是否启用实时更新</summary>
    /// <remarks>点赞时触发神评/沙发检查</remarks>
    public bool RealtimeUpdate { get; set; } = true;

    /// <summary>是否启用缓存</summary>
    public bool CacheEnabled { get; set; } = true;

    /// <summary>缓存过期时间（分钟）</summary>
    public int CacheTTLMinutes { get; set; } = 60;

    /// <summary>神评触发最小父评论数</summary>
    /// <remarks>帖子的父评论数必须大于此值才会产生神评</remarks>
    public int MinParentCommentCount { get; set; } = 5;

    /// <summary>沙发触发最小子评论数</summary>
    /// <remarks>父评论的子评论数必须大于此值才会产生沙发</remarks>
    public int MinChildCommentCount { get; set; } = 3;
}
