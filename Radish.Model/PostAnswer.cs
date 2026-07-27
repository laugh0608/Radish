using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>帖子问答回答</summary>
[SugarTable("PostAnswer")]
[SugarIndex("idx_postanswer_public_id", nameof(PublicId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_postanswer_post_create", nameof(PostId), OrderByType.Asc, nameof(CreateTime), OrderByType.Asc)]
[SugarIndex("idx_postanswer_post_accept", nameof(PostId), OrderByType.Asc, nameof(IsAccepted), OrderByType.Asc)]
[SugarIndex(
    "idx_postanswer_tenant_post_visibility",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(PostId),
    OrderByType.Asc,
    nameof(IsDeleted),
    OrderByType.Asc,
    nameof(IsEnabled),
    OrderByType.Asc,
    nameof(IsAccepted),
    OrderByType.Desc,
    nameof(CreateTime),
    OrderByType.Asc)]
public class PostAnswer : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    public const string PublicIdPrefix = "ans_";

    /// <summary>公开访问标识</summary>
    [SugarColumn(Length = 36, IsNullable = false)]
    public string PublicId { get; set; } = GeneratePublicId();

    /// <summary>帖子 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long PostId { get; set; }

    /// <summary>作者 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long AuthorId { get; set; }

    /// <summary>作者名称</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>回答内容</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string Content { get; set; } = string.Empty;

    /// <summary>是否已采纳</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsAccepted { get; set; } = false;

    /// <summary>当前正文版本</summary>
    [SugarColumn(IsNullable = false)]
    public int ContentRevision { get; set; } = 1;

    /// <summary>作者成功编辑 / 恢复次数</summary>
    [SugarColumn(IsNullable = false)]
    public int EditCount { get; set; }

    /// <summary>治理可见状态</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsEnabled { get; set; } = true;

    /// <summary>当前限制该回答的治理目标动作 ID。</summary>
    [SugarColumn(IsNullable = true)]
    public long? ModerationTargetActionId { get; set; }

    /// <summary>租户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    /// <summary>软删除标记</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; } = false;

    /// <summary>删除时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

    /// <summary>删除人</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建人</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建人 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }

    /// <summary>修改时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? ModifyTime { get; set; }

    /// <summary>修改人</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

    /// <summary>修改人 ID</summary>
    [SugarColumn(IsNullable = true)]
    public long? ModifyId { get; set; }

    public static string GeneratePublicId() => $"{PublicIdPrefix}{Guid.CreateVersion7():N}";

    public static bool HasPublicIdFormat(string? value)
    {
        var normalized = value?.Trim();
        return normalized is { Length: 36 } &&
               normalized.StartsWith(PublicIdPrefix, StringComparison.OrdinalIgnoreCase) &&
               normalized[PublicIdPrefix.Length..].All(Uri.IsHexDigit);
    }
}
