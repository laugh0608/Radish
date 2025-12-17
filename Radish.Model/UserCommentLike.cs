using System;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>用户评论点赞关系实体</summary>
/// <remarks>记录用户对评论的点赞关系，支持查询点赞状态和点赞用户列表</remarks>
public class UserCommentLike : RootEntityTKey<long>
{
    /// <summary>
    /// 用户ID
    /// </summary>
    /// <remarks>不可为空</remarks>
    [SugarColumn(ColumnDescription = "用户ID", IsNullable = false)]
    public long UserId { get; set; }

    /// <summary>
    /// 评论ID
    /// </summary>
    /// <remarks>不可为空</remarks>
    [SugarColumn(ColumnDescription = "评论ID", IsNullable = false)]
    public long CommentId { get; set; }

    /// <summary>
    /// 帖子ID
    /// </summary>
    /// <remarks>冗余字段，便于统计分析，不可为空</remarks>
    [SugarColumn(ColumnDescription = "帖子ID", IsNullable = false)]
    public long PostId { get; set; }

    /// <summary>
    /// 点赞时间
    /// </summary>
    /// <remarks>不可为空</remarks>
    [SugarColumn(ColumnDescription = "点赞时间", IsNullable = false)]
    public DateTime LikedAt { get; set; } = DateTime.UtcNow;
}
