using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>当前用户拥有的私有帖子收藏关系。</summary>
[SugarTable("UserPostBookmark")]
[SugarIndex(
    "idx_userpostbookmark_public_id",
    nameof(PublicId),
    OrderByType.Asc,
    IsUnique = true)]
[SugarIndex(
    "idx_userpostbookmark_relation",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(UserId),
    OrderByType.Asc,
    nameof(PostId),
    OrderByType.Asc,
    IsUnique = true)]
[SugarIndex(
    "idx_userpostbookmark_mine",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(UserId),
    OrderByType.Asc,
    nameof(IsDeleted),
    OrderByType.Asc,
    nameof(BookmarkedAt),
    OrderByType.Desc,
    nameof(Id),
    OrderByType.Desc)]
[SugarIndex(
    "idx_userpostbookmark_post_active",
    nameof(TenantId),
    OrderByType.Asc,
    nameof(PostId),
    OrderByType.Asc,
    nameof(IsDeleted),
    OrderByType.Asc)]
public sealed class UserPostBookmark : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    public const string PublicIdPrefix = "bmk_";

    [SugarColumn(Length = 36, IsNullable = false)]
    public string PublicId { get; set; } = GeneratePublicId();

    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; }

    [SugarColumn(IsNullable = false)]
    public long PostId { get; set; }

    [SugarColumn(IsNullable = false)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime BookmarkedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = false)]
    public bool IsDeleted { get; set; }

    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? DeletedAt { get; set; }

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? DeletedBy { get; set; }

    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ModifyTime { get; set; }

    [SugarColumn(Length = 50, IsNullable = true)]
    public string? ModifyBy { get; set; }

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
