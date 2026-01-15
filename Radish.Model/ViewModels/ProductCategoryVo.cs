namespace Radish.Model.ViewModels;

/// <summary>商品分类视图模型</summary>
public class ProductCategoryVo
{
    /// <summary>分类 ID</summary>
    /// <remarks>语义化标识，如 badge, card, boost</remarks>
    public string Id { get; set; } = string.Empty;

    /// <summary>分类名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>分类图标</summary>
    public string? Icon { get; set; }

    /// <summary>分类描述</summary>
    public string? Description { get; set; }

    /// <summary>排序权重</summary>
    public int SortOrder { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }

    /// <summary>该分类下的商品数量</summary>
    /// <remarks>运行时计算，非数据库字段</remarks>
    public int ProductCount { get; set; }
}
