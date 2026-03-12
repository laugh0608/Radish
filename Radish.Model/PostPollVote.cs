using System.ComponentModel.DataAnnotations;
using Radish.Model.Root;
using SqlSugar;

namespace Radish.Model;

/// <summary>帖子投票记录</summary>
[SugarTable("PostPollVote")]
[SugarIndex("idx_postpollvote_poll_user", nameof(PollId), OrderByType.Asc, nameof(UserId), OrderByType.Asc, IsUnique = true)]
[SugarIndex("idx_postpollvote_post", nameof(PostId), OrderByType.Asc)]
public class PostPollVote : RootEntityTKey<long>, ITenantEntity
{
    /// <summary>投票主体 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long PollId { get; set; }

    /// <summary>帖子 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long PostId { get; set; }

    /// <summary>投票选项 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long OptionId { get; set; }

    /// <summary>投票用户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long UserId { get; set; }

    /// <summary>投票用户名称</summary>
    [SugarColumn(Length = 100, IsNullable = false)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>租户 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long TenantId { get; set; } = 0;

    /// <summary>投票时间</summary>
    [SugarColumn(IsNullable = false)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>创建人</summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string CreateBy { get; set; } = "System";

    /// <summary>创建人 ID</summary>
    [SugarColumn(IsNullable = false)]
    public long CreateId { get; set; }
}
