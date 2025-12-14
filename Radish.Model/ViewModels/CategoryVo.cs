namespace Radish.Model.ViewModels;

/// <summary>
/// 分类视图模型
/// </summary>
public class CategoryVo
{
    /// <summary>
    /// 分类 Id
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 分类名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL 友好的标识符
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// 分类描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 分类图标
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 分类封面图
    /// </summary>
    public string? CoverImage { get; set; }

    /// <summary>
    /// 父分类 Id
    /// </summary>
    public long? ParentId { get; set; }

    /// <summary>
    /// 层级深度
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 排序权重
    /// </summary>
    public int OrderSort { get; set; }

    /// <summary>
    /// 帖子数量
    /// </summary>
    public int PostCount { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreateTime { get; set; }

    /// <summary>
    /// 创建者名称
    /// </summary>
    public string CreateBy { get; set; } = string.Empty;
}
