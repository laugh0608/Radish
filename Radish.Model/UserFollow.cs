using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户关注关系实体</summary>
[SugarTable("UserFollow")]
[SugarIndex("idx_userfollow_follower", nameof(TenantId), OrderByType.Asc, nameof(FollowerUserId), OrderByType.Asc)]
[SugarIndex("idx_userfollow_following", nameof(TenantId), OrderByType.Asc, nameof(FollowingUserId), OrderByType.Asc)]
[SugarIndex("idx_userfollow_pair", nameof(TenantId), OrderByType.Asc, nameof(FollowerUserId), OrderByType.Asc, nameof(FollowingUserId), OrderByType.Asc, IsUnique = true)]
public class UserFollow : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>租户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    /// <summary>关注者用户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long FollowerUserId { get; set; }

    /// <summary>被关注用户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long FollowingUserId { get; set; }

    /// <summary>关注时间</summary>
    [SugarColumn(IsNullable = false)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime FollowTime { get; set; } = DateTime.UtcNow;

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
    public DateTime CreateTime { get; set; } = DateTime.UtcNow;

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
}
