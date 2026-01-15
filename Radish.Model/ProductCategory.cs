using SqlSugar;

namespace Radish.Model;

/// <summary>商品分类实体</summary>
/// <remarks>
/// 用于组织和展示商城商品
/// 使用语义化字符串主键（如 badge, card, boost）
/// </remarks>
[SugarTable("ShopProductCategory")]
public class ProductCategory
{
    /// <summary>分类 ID</summary>
    /// <remarks>语义化标识，如 badge, card, boost</remarks>
    [SugarColumn(Length = 50, IsNullable = false, IsPrimaryKey = true)]
    public string Id { get; set; } = string.Empty;

    /// <summary>分类名称</summary>
    /// <remarks>显示名称，如 徽章装饰、功能卡片</remarks>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>分类图标</summary>
    /// <remarks>图标类名或图标 URL</remarks>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string? Icon { get; set; }

    /// <summary>分类描述</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Description { get; set; }

    /// <summary>排序权重</summary>
    /// <remarks>数值越小越靠前</remarks>
    [SugarColumn(IsNullable = false)]
    public int SortOrder { get; set; } = 0;

    /// <summary>是否启用</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>修改时间</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? ModifyTime { get; set; }
}
