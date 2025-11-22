namespace Radish.Model.Root;

/// <summary>
/// 软删除过滤器
/// </summary>
public interface IDeleteFilter
{
    public bool IsDeleted { get; set; }
}