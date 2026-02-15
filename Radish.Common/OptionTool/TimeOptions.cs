using Radish.Common.OptionTool.Core;

namespace Radish.Common.OptionTool;

/// <summary>时间配置选项</summary>
public sealed class TimeOptions : IConfigurableOptions
{
    /// <summary>系统默认时区 ID（IANA，例如 Asia/Shanghai）</summary>
    public string DefaultTimeZoneId { get; set; } = "Asia/Shanghai";

    /// <summary>前端默认展示格式</summary>
    public string DisplayFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
}
