using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户浏览记录</summary>
[SugarTable("UserBrowseHistory")]
[SugarIndex("idx_userbrowsehistory_user_target", nameof(UserId), OrderByType.Asc, nameof(TargetType), OrderByType.Asc, nameof(TargetId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_userbrowsehistory_user_lastview", nameof(UserId), OrderByType.Asc, nameof(LastViewTime), OrderByType.Desc)]
public class UserBrowseHistory : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>用户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; }

    /// <summary>目标类型（Post/Product/Wiki）</summary>
    [SugarColumn(Length = 20, IsNullable = false)]
    public string TargetType { get; set; } = string.Empty;

    /// <summary>目标 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TargetId { get; set; }

    /// <summary>目标 Slug</summary>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string? TargetSlug { get; set; }

    /// <summary>标题快照</summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string Title { get; set; } = string.Empty;

    /// <summary>摘要快照</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Summary { get; set; }

    /// <summary>封面快照</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? CoverImage { get; set; }

    /// <summary>前端跳转路径快照</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? RoutePath { get; set; }

    /// <summary>浏览次数</summary>
    [SugarColumn(IsNullable = false)]
    public int ViewCount { get; set; } = 0;

    /// <summary>最后浏览时间</summary>
    [SugarColumn(IsNullable = false)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime LastViewTime { get; set; } = DateTime.UtcNow;

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
}
