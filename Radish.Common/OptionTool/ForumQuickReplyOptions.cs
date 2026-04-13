using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>论坛轻回应墙配置</summary>
public sealed class ForumQuickReplyOptions : IConfigurableOptions
{
    /// <summary>是否启用轻回应能力</summary>
    public bool Enable { get; set; } = true;

    /// <summary>默认返回条数</summary>
    public int DefaultTake { get; set; } = 30;

    /// <summary>最大返回条数</summary>
    public int MaxTake { get; set; } = 60;

    /// <summary>单条最大长度</summary>
    public int MaxContentLength { get; set; } = 24;

    /// <summary>单用户单帖发送冷却秒数</summary>
    public int PerPostCooldownSeconds { get; set; } = 30;

    /// <summary>相同内容去重窗口秒数</summary>
    public int DuplicateWindowSeconds { get; set; } = 300;
}
