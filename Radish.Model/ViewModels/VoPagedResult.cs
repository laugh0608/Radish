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
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// 总记录数
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// 页码（从1开始）
    /// </summary>
    public int PageIndex { get; set; }

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize { get; set; }
}