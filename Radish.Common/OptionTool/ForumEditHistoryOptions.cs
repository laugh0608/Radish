using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>论坛帖子/评论编辑历史配置</summary>
public sealed class ForumEditHistoryOptions : IConfigurableOptions
{
    /// <summary>是否启用编辑历史功能</summary>
    public bool Enable { get; set; } = true;

    /// <summary>帖子编辑历史配置</summary>
    public ForumPostEditHistoryOptions Post { get; set; } = new();

    /// <summary>评论编辑历史配置</summary>
    public ForumCommentEditHistoryOptions Comment { get; set; } = new();

    /// <summary>管理员覆盖规则</summary>
    public ForumEditHistoryAdminOverrideOptions AdminOverride { get; set; } = new();
}

/// <summary>帖子编辑历史配置</summary>
public sealed class ForumPostEditHistoryOptions
{
    /// <summary>是否启用帖子编辑历史记录</summary>
    public bool EnableHistory { get; set; } = true;

    /// <summary>历史保存的修改次数（超过后不再新增历史）</summary>
    public int HistorySaveEditCount { get; set; } = 10;

    /// <summary>普通用户最大可编辑次数</summary>
    public int MaxEditCount { get; set; } = 20;

    /// <summary>单个帖子历史保留上限（保留最近 N 条）</summary>
    public int MaxHistoryRecords { get; set; } = 20;
}

/// <summary>评论编辑历史配置</summary>
public sealed class ForumCommentEditHistoryOptions
{
    /// <summary>是否启用评论编辑历史记录</summary>
    public bool EnableHistory { get; set; } = true;

    /// <summary>历史保存的修改次数（超过后不再新增历史）</summary>
    public int HistorySaveEditCount { get; set; } = 5;

    /// <summary>普通用户最大可编辑次数</summary>
    public int MaxEditCount { get; set; } = 10;

    /// <summary>单条评论历史保留上限（保留最近 N 条）</summary>
    public int MaxHistoryRecords { get; set; } = 10;

    /// <summary>普通用户评论编辑时间窗口（分钟）</summary>
    public int EditWindowMinutes { get; set; } = 5;
}

/// <summary>管理员覆盖规则</summary>
public sealed class ForumEditHistoryAdminOverrideOptions
{
    /// <summary>管理员是否可绕过编辑次数限制</summary>
    public bool BypassEditCountLimit { get; set; } = true;

    /// <summary>管理员是否可绕过评论编辑时间窗口限制</summary>
    public bool BypassCommentEditWindow { get; set; } = true;
}

