using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

/// <summary>
/// 评论编辑历史记录
/// </summary>
[SugarTable("CommentEditHistory")]
[SugarIndex("idx_comment_edit_history_comment", nameof(CommentId), OrderByType.Asc)]
[SugarIndex("idx_comment_edit_history_comment_seq", nameof(CommentId), OrderByType.Asc, nameof(EditSequence), OrderByType.Desc)]
[SugarIndex("idx_comment_edit_history_tenant", nameof(TenantId), OrderByType.Asc)]
[SugarIndex("idx_comment_edit_history_time", nameof(EditedAt), OrderByType.Desc)]
public class CommentEditHistory : RootEntityTKey<long>, ITenantEntity
{
    /// <summary>评论 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long CommentId { get; set; }

    /// <summary>所属帖子 Id（冗余字段，便于查询）</summary>
    [SugarColumn(IsNullable = false)]
    public long PostId { get; set; }

    /// <summary>编辑序号（从 1 开始）</summary>
    [SugarColumn(IsNullable = false)]
    public int EditSequence { get; set; }

    /// <summary>编辑前内容</summary>
    [SugarColumn(Length = 2000, IsNullable = false)]
    public string OldContent { get; set; } = string.Empty;

    /// <summary>编辑后内容</summary>
    [SugarColumn(Length = 2000, IsNullable = false)]
    public string NewContent { get; set; } = string.Empty;

    /// <summary>编辑人 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long EditorId { get; set; }

    /// <summary>编辑人名称</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string EditorName { get; set; } = string.Empty;

    /// <summary>编辑时间</summary>
    [SugarColumn(IsNullable = false)]
    public DateTime EditedAt { get; set; } = DateTime.Now;

    /// <summary>租户 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建者</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建者 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }
}

