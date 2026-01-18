namespace Radish.Model.ViewModels;

/// <summary>
/// 分页结果ViewModel
/// </summary>
/// <typeparam name="T">数据项类型</typeparam>
public class VoPagedResult<T>
{
    /// <summary>
    /// 数据项列表
    /// </summary>
    public List<T> VoItems { get; set; } = new();

    /// <summary>
    /// 总记录数
    /// </summary>
    public int VoTotal { get; set; }

    /// <summary>
    /// 页码（从1开始）
    /// </summary>
    public int VoPageIndex { get; set; }

    /// <summary>
    /// 每页大小
    /// </summary>
    public int VoPageSize { get; set; }
}