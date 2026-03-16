namespace Radish.Model.ViewModels;

/// <summary>帖子抽奖视图模型</summary>
public class PostLotteryVo
{
    /// <summary>抽奖 ID</summary>
    public long VoLotteryId { get; set; }

    /// <summary>帖子 ID</summary>
    public long VoPostId { get; set; }

    /// <summary>奖品名称</summary>
    public string VoPrizeName { get; set; } = string.Empty;

    /// <summary>奖品说明</summary>
    public string? VoPrizeDescription { get; set; }

    /// <summary>可开奖时间</summary>
    public DateTime? VoDrawTime { get; set; }

    /// <summary>实际开奖时间</summary>
    public DateTime? VoDrawnAt { get; set; }

    /// <summary>中奖人数</summary>
    public int VoWinnerCount { get; set; }

    /// <summary>参与人数</summary>
    public int VoParticipantCount { get; set; }

    /// <summary>是否已开奖</summary>
    public bool VoIsDrawn { get; set; }

    /// <summary>中奖名单</summary>
    public List<PostLotteryWinnerVo> VoWinners { get; set; } = new();
}

/// <summary>帖子抽奖中奖人视图模型</summary>
public class PostLotteryWinnerVo
{
    /// <summary>中奖记录 ID</summary>
    public long VoId { get; set; }

    /// <summary>抽奖 ID</summary>
    public long VoLotteryId { get; set; }

    /// <summary>用户 ID</summary>
    public long VoUserId { get; set; }

    /// <summary>用户名</summary>
    public string VoUserName { get; set; } = string.Empty;

    /// <summary>参与父评论 ID</summary>
    public long? VoCommentId { get; set; }

    /// <summary>评论内容快照</summary>
    public string? VoCommentContentSnapshot { get; set; }

    /// <summary>开奖时间</summary>
    public DateTime VoDrawnAt { get; set; }
}

/// <summary>抽奖详情结果视图模型</summary>
public class LotteryResultVo
{
    /// <summary>帖子 ID</summary>
    public long VoPostId { get; set; }

    /// <summary>抽奖详情</summary>
    public PostLotteryVo? VoLottery { get; set; }
}
