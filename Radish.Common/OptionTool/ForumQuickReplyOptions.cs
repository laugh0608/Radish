using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>论坛轻回应墙配置</summary>
public sealed class ForumQuickReplyOptions : IConfigurableOptions
{
    /// <summary>是否启用轻回应能力</summary>
    public bool Enable { get; set; } = true;
}
