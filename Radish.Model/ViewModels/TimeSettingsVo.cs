namespace Radish.Model.ViewModels;

/// <summary>时间配置视图模型</summary>
public class TimeSettingsVo
{
    /// <summary>系统默认时区 ID（IANA）</summary>
    public string VoDefaultTimeZoneId { get; set; } = "Asia/Shanghai";

    /// <summary>时间展示格式</summary>
    public string VoDisplayFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
}
