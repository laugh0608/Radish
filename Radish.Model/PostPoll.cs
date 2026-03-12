using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>帖子投票主体</summary>
[SugarTable("PostPoll")]
[SugarIndex("idx_postpoll_post", nameof(PostId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_postpoll_tenant_status", nameof(TenantId), OrderByType.Asc, nameof(IsClosed), OrderByType.Asc)]
public class PostPoll : RootEntityTKey<long>, ITenantEntity, IDeleteFilter
{
    /// <summary>帖子 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long PostId { get; set; }

    /// <summary>投票问题</summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string Question { get; set; } = string.Empty;

    /// <summary>截止时间</summary>
    [SugarColumn(IsNullable = true)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime? EndTime { get; set; }

    /// <summary>是否关闭</summary>
    [SugarColumn(IsNullable = false)]
    public bool IsClosed { get; set; } = false;

    /// <summary>总票数</summary>
    [SugarColumn(IsNullable = false)]
    public int TotalVoteCount { get; set; } = 0;

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
