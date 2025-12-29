using System;
using Radish.Model.Root;
using Radish.Model.Tenants;
using SqlSugar;

namespace Radish.Model;

/// <summary>
/// 神评/沙发历史记录表
/// </summary>
/// <remarks>
/// 记录每日统计的神评（父评论点赞最高）和沙发（子评论点赞最高）
/// </remarks>
[SugarIndex("idx_post_type_current", $"{nameof(PostId)},{nameof(HighlightType)},{nameof(IsCurrent)}", OrderByType.Asc)]
[SugarIndex("idx_parent_type_current", $"{nameof(ParentCommentId)},{nameof(HighlightType)},{nameof(IsCurrent)}", OrderByType.Asc)]
[SugarIndex("idx_stat_date", nameof(StatDate), OrderByType.Desc)]
[SugarIndex("idx_comment_id", nameof(CommentId), OrderByType.Asc)]
public class CommentHighlight : RootEntityTKey<long>, ITenantEntity
{
    /// <summary>帖子 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long PostId { get; set; }

    /// <summary>评论 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long CommentId { get; set; }

    /// <summary>父评论 ID（沙发专用，神评为 null）</summary>
    /// <remarks>
    /// - 神评：ParentCommentId = null（表示这是父评论的神评）
    /// - 沙发：ParentCommentId = 父评论ID（表示这是某个父评论下的沙发）
    /// </remarks>
    [SugarColumn(IsNullable = true)]
    public long? ParentCommentId { get; set; }

    /// <summary>高亮类型：1=神评，2=沙发</summary>
    [SugarColumn(IsNullable = false)]
    public int HighlightType { get; set; }

    /// <summary>统计日期（yyyy-MM-dd）</summary>
    [SugarColumn(IsNullable = false, ColumnDataType = "date")]
    public DateTime StatDate { get; set; }

    /// <summary>点赞数（快照）</summary>
    [SugarColumn(IsNullable = false)]
    public int LikeCount { get; set; }

    /// <summary>排名（同一天同一类型的排名，1=第一名）</summary>
    /// <remarks>
    /// 用于处理点赞数相同的情况，按时间最新排序
    /// </remarks>
    [SugarColumn(IsNullable = false)]
    public int Rank { get; set; }

    /// <summary>评论内容快照（冗余字段，便于历史查询）</summary>
    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? ContentSnapshot { get; set; }

    /// <summary>作者 ID（冗余字段）</summary>
    [SugarColumn(IsNullable = false)]
    public long AuthorId { get; set; }

    /// <summary>作者名称（冗余字段）</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>是否当前有效（最新一次统计）</summary>
    /// <remarks>
    /// 用于快速查询当前神评/沙发，每次统计时更新
    /// </remarks>
    [SugarColumn(IsNullable = false)]
    public bool IsCurrent { get; set; } = true;

    /// <summary>租户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; }

    /// <summary>创建时间</summary>
    [SugarColumn(IsNullable = false, IsOnlyIgnoreUpdate = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建者</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";
}
