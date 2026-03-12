namespace Radish.Model.ViewModels;

/// <summary>
/// 帖子投票视图模型
/// </summary>
public class PostPollVo
{
    /// <summary>投票 ID</summary>
    public long VoPollId { get; set; }

    /// <summary>帖子 ID</summary>
    public long VoPostId { get; set; }

    /// <summary>投票问题</summary>
    public string VoQuestion { get; set; } = string.Empty;

    /// <summary>截止时间</summary>
    public DateTime? VoEndTime { get; set; }

    /// <summary>是否关闭</summary>
    public bool VoIsClosed { get; set; }

    /// <summary>总票数</summary>
    public int VoTotalVoteCount { get; set; }

    /// <summary>当前用户是否已投票</summary>
    public bool VoHasVoted { get; set; }

    /// <summary>当前用户所选选项 ID</summary>
    public long? VoSelectedOptionId { get; set; }

    /// <summary>投票选项</summary>
    public List<PostPollOptionVo> VoOptions { get; set; } = new();
}

/// <summary>
/// 帖子投票选项视图模型
/// </summary>
public class PostPollOptionVo
{
    /// <summary>选项 ID</summary>
    public long VoOptionId { get; set; }

    /// <summary>选项文本</summary>
    public string VoOptionText { get; set; } = string.Empty;

    /// <summary>排序号</summary>
    public int VoSortOrder { get; set; }

    /// <summary>票数</summary>
    public int VoVoteCount { get; set; }

    /// <summary>票数占比</summary>
    public decimal VoVotePercent { get; set; }
}

/// <summary>
/// 投票结果视图模型
/// </summary>
public class PollVoteResultVo
{
    /// <summary>帖子 ID</summary>
    public long VoPostId { get; set; }

    /// <summary>投票详情</summary>
    public PostPollVo? VoPoll { get; set; }
}
