namespace Radish.Model.ViewModels;

/// <summary>
/// 用户时间偏好视图模型
/// </summary>
public class UserTimePreferenceVo
{
    /// <summary>
    /// 用户 ID
    /// </summary>
    public long VoUserId { get; set; }

    /// <summary>
    /// 用户时区（IANA）
    /// </summary>
    public string VoTimeZoneId { get; set; } = string.Empty;

    /// <summary>
    /// 是否已自定义
    /// </summary>
    public bool VoIsCustomized { get; set; }

    /// <summary>
    /// 系统默认时区（IANA）
    /// </summary>
    public string VoSystemDefaultTimeZoneId { get; set; } = "Asia/Shanghai";

    /// <summary>
    /// 展示格式
    /// </summary>
    public string VoDisplayFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime? VoModifyTime { get; set; }
}
