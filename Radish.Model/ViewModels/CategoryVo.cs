namespace Radish.Model.ViewModels;

/// <summary>
/// 分类视图模型
/// </summary>
public class CategoryVo
{
    /// <summary>
    /// 分类 Id
    /// </summary>
    public long VoId { get; set; }

    /// <summary>
    /// 分类名称
    /// </summary>
    public string VoName { get; set; } = string.Empty;

    /// <summary>
    /// URL 友好的标识符
    /// </summary>
    public string VoSlug { get; set; } = string.Empty;

    /// <summary>
    /// 分类描述
    /// </summary>
    public string? VoDescription { get; set; }

    /// <summary>
    /// 分类图标
    /// </summary>
    public string? VoIcon { get; set; }

    /// <summary>
    /// 分类封面图
    /// </summary>
    public string? VoCoverImage { get; set; }

    /// <summary>
    /// 父分类 Id
    /// </summary>
    public long? VoParentId { get; set; }

    /// <summary>
    /// 层级深度
    /// </summary>
    public int VoLevel { get; set; }

    /// <summary>
    /// 排序权重
    /// </summary>
    public int VoOrderSort { get; set; }

    /// <summary>
    /// 帖子数量
    /// </summary>
    public int VoPostCount { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool VoIsEnabled { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime VoCreateTime { get; set; }

    /// <summary>
    /// 创建者名称
    /// </summary>
    public string VoCreateBy { get; set; } = string.Empty;
}
