using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>商品购买者对单个商品的唯一评价。</summary>
[SugarTable("ShopProductReview")]
[SugarIndex(
    "idx_product_review_user_product",
    nameof(TenantId), OrderByType.Asc,
    nameof(ProductId), OrderByType.Asc,
    nameof(UserId), OrderByType.Asc,
    IsUnique = true)]
[SugarIndex(
    "idx_product_review_product_page",
    nameof(TenantId), OrderByType.Asc,
    nameof(ProductId), OrderByType.Asc,
    nameof(IsDeleted), OrderByType.Asc,
    nameof(CreateTime), OrderByType.Desc,
    nameof(Id), OrderByType.Desc)]
[SugarIndex(
    "idx_product_review_product_rating",
    nameof(TenantId), OrderByType.Asc,
    nameof(ProductId), OrderByType.Asc,
    nameof(IsDeleted), OrderByType.Asc,
    nameof(Rating), OrderByType.Asc)]
public sealed class ProductReview : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long ProductId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; }

    /// <summary>首次建立评价资格时绑定的已完成订单证据。</summary>
    [SugarColumn(IsNullable = false)]
    public long EligibleOrderId { get; set; }

    /// <summary>评价者公开展示名快照；公开身份存在时由查询投影覆盖。</summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string AuthorName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = false)]
    public int Rating { get; set; }

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Comment { get; set; }

    /// <summary>写入版本；创建、编辑、删除与恢复均单调递增。</summary>
    [SugarColumn(IsNullable = false)]
    public int Version { get; set; }

    /// <summary>治理限制该评价时对应的目标动作。</summary>
    [SugarColumn(IsNullable = true)]
    public long? ModerationTargetActionId { get; set; }

    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; }

    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; }

    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }

    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }
}
