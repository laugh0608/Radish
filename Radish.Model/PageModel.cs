namespace Radish.Model;

/// <summary>
/// 分页模型
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class PageModel<T>
{
    /// <summary>
    /// 当前页码
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每页数量
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// 总数据量
    /// </summary>
    public int DataCount { get; set; }

    /// <summary>
    /// 总页数
    /// </summary>
    public int PageCount { get; set; }

    /// <summary>
    /// 数据列表
    /// </summary>
    public List<T> Data { get; set; } = new();
}
