namespace Radish.Model.DtoModels;

/// <summary>记录浏览历史 DTO</summary>
public class RecordBrowseHistoryDto
{
    /// <summary>浏览用户 ID</summary>
    public long UserId { get; set; }

    /// <summary>租户 ID</summary>
    public long TenantId { get; set; }

    /// <summary>目标类型（Post/Product/Wiki）</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>目标 ID</summary>
    public long TargetId { get; set; }

    /// <summary>目标 Slug</summary>
    public string? TargetSlug { get; set; }

    /// <summary>标题快照</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>摘要快照</summary>
    public string? Summary { get; set; }

    /// <summary>封面快照</summary>
    public string? CoverImage { get; set; }

    /// <summary>路由快照</summary>
    public string? RoutePath { get; set; }

    /// <summary>操作者名称</summary>
    public string OperatorName { get; set; } = "System";
}
