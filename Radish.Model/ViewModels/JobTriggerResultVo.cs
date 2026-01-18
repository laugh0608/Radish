namespace Radish.Model.ViewModels;

/// <summary>
/// 任务触发结果视图模型
/// </summary>
public class JobTriggerResultVo
{
    /// <summary>
    /// 任务ID
    /// </summary>
    public string VoJobId { get; set; } = string.Empty;

    /// <summary>
    /// 统计日期
    /// </summary>
    public DateTime VoStatDate { get; set; }
}