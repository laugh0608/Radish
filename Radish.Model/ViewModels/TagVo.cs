namespace Radish.Model.ViewModels;

/// <summary>
/// 标签视图模型
/// </summary>
public class TagVo
{
    /// <summary>
    /// 标签 Id
    /// </summary>
    public long VoId { get; set; }

    /// <summary>
    /// 标签名称
    /// </summary>
    public string VoName { get; set; } = string.Empty;

    /// <summary>
    /// URL 友好的标识符
    /// </summary>
    public string VoSlug { get; set; } = string.Empty;

    /// <summary>
    /// 标签描述
    /// </summary>
    public string? VoDescription { get; set; }

    /// <summary>
    /// 标签颜色
    /// </summary>
    public string? VoColor { get; set; }

    /// <summary>
    /// 排序值
    /// </summary>
    public int VoSortOrder { get; set; }

    /// <summary>
    /// 帖子数量
    /// </summary>
    public int VoPostCount { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool VoIsEnabled { get; set; }

    /// <summary>
    /// 是否固定标签
    /// </summary>
    public bool VoIsFixed { get; set; }

    /// <summary>
    /// 是否已删除
    /// </summary>
    public bool VoIsDeleted { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime VoCreateTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime? VoModifyTime { get; set; }
}
