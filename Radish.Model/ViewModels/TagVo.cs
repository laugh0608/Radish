namespace Radish.Model.ViewModels;

/// <summary>
/// 标签视图模型
/// </summary>
public class TagVo
{
    /// <summary>
    /// 标签 Id
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 标签名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL 友好的标识符
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// 标签描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 标签颜色
    /// </summary>
    public string? Color { get; set; }

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
}
