using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

/// <summary>
/// 帖子编辑历史记录
/// </summary>
[SugarTable("PostEditHistory")]
[SugarIndex("idx_post_edit_history_post", nameof(PostId), OrderByType.Asc)]
[SugarIndex("idx_post_edit_history_post_seq", nameof(PostId), OrderByType.Asc, nameof(EditSequence), OrderByType.Desc)]
[SugarIndex("idx_post_edit_history_tenant", nameof(TenantId), OrderByType.Asc)]
[SugarIndex("idx_post_edit_history_time", nameof(EditedAt), OrderByType.Desc)]
public class PostEditHistory : RootEntityTKey<long>, ITenantEntity
{
    /// <summary>帖子 Id</summary>
    [SugarColumn(IsNullable = false)]
    public long PostId { get; set; }

    /// <summary>编辑序号（从 1 开始）</summary>
    [SugarColumn(IsNullable = false)]
    public int EditSequence { get; set; }

    /// <summary>编辑前标题</summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string OldTitle { get; set; } = string.Empty;

    /// <summary>编辑后标题</summary>
    [SugarColumn(Length = 200, IsNullable = false)]
    public string NewTitle { get; set; } = string.Empty;

    /// <summary>编辑前内容</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
    public string OldContent { get; set; } = string.Empty;

    /// <summary>编辑后内容</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = false)]
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

