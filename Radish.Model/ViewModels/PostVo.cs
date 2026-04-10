namespace Radish.Model.ViewModels;

/// <summary>
/// 帖子视图模型
/// </summary>
public class PostVo
{
    /// <summary>
    /// 帖子 Id
    /// </summary>
    public long VoId { get; set; }

    /// <summary>
    /// 帖子标题
    /// </summary>
    public string VoTitle { get; set; } = string.Empty;

    /// <summary>
    /// URL 友好的标识符
    /// </summary>
    public string VoSlug { get; set; } = string.Empty;

    /// <summary>
    /// 帖子摘要
    /// </summary>
    public string? VoSummary { get; set; }

    /// <summary>
    /// 帖子内容
    /// </summary>
    public string VoContent { get; set; } = string.Empty;

    /// <summary>
    /// 内容类型（markdown/html/text）
    /// </summary>
    public string VoContentType { get; set; } = "markdown";

    /// <summary>
    /// 封面附件 Id
    /// </summary>
    public long? VoCoverAttachmentId { get; set; }

    /// <summary>
    /// 封面图片 URL
    /// </summary>
    public string? VoCoverImage { get; set; }

    /// <summary>
    /// 作者 Id
    /// </summary>
    public long VoAuthorId { get; set; }

    /// <summary>
    /// 作者名称
    /// </summary>
    public string VoAuthorName { get; set; } = string.Empty;

    /// <summary>
    /// 作者头像 URL
    /// </summary>
    public string? VoAuthorAvatarUrl { get; set; }

    /// <summary>
    /// 分类 Id
    /// </summary>
    public long VoCategoryId { get; set; }

    /// <summary>
    /// 分类名称（冗余字段，便于前端显示）
    /// </summary>
    public string? VoCategoryName { get; set; }

    /// <summary>
    /// 标签列表（逗号分隔的标签名）
    /// </summary>
    public string? VoTags { get; set; }

    /// <summary>
    /// 是否置顶
    /// </summary>
    public bool VoIsTop { get; set; }

    /// <summary>
    /// 是否精华
    /// </summary>
    public bool VoIsEssence { get; set; }

    /// <summary>
    /// 是否锁定
    /// </summary>
    public bool VoIsLocked { get; set; }

    /// <summary>
    /// 是否已发布
    /// </summary>
    public bool VoIsPublished { get; set; }

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime? VoPublishTime { get; set; }

    /// <summary>
    /// 浏览次数
    /// </summary>
    public int VoViewCount { get; set; }

    /// <summary>
    /// 点赞次数
    /// </summary>
    public int VoLikeCount { get; set; }

    /// <summary>
    /// 评论次数
    /// </summary>
    public int VoCommentCount { get; set; }

    /// <summary>
    /// 收藏次数
    /// </summary>
    public int VoCollectCount { get; set; }

    /// <summary>
    /// 分享次数
    /// </summary>
    public int VoShareCount { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime VoCreateTime { get; set; }

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime? VoModifyTime { get; set; }

    /// <summary>
    /// 最新互动用户（按最新评论时间倒序，最多 3 个）
    /// </summary>
    public List<PostInteractorVo> VoLatestInteractors { get; set; } = new();

    /// <summary>
    /// 当前神评评论 ID
    /// </summary>
    public long? VoGodCommentId { get; set; }

    /// <summary>
    /// 当前神评作者名
    /// </summary>
    public string? VoGodCommentAuthorName { get; set; }

    /// <summary>
    /// 当前神评内容快照
    /// </summary>
    public string? VoGodCommentContentSnapshot { get; set; }

    /// <summary>
    /// 是否问答帖
    /// </summary>
    public bool VoIsQuestion { get; set; }

    /// <summary>
    /// 是否已解决
    /// </summary>
    public bool VoIsSolved { get; set; }

    /// <summary>
    /// 回答数
    /// </summary>
    public int VoAnswerCount { get; set; }

    /// <summary>
    /// 问答详情（列表页通常为空，详情页可用）
    /// </summary>
    public PostQuestionVo? VoQuestion { get; set; }

    /// <summary>
    /// 是否附带投票
    /// </summary>
    public bool VoHasPoll { get; set; }

    /// <summary>
    /// 投票总票数
    /// </summary>
    public int VoPollTotalVoteCount { get; set; }

    /// <summary>
    /// 投票是否关闭
    /// </summary>
    public bool VoPollIsClosed { get; set; }

    /// <summary>
    /// 投票详情（列表页通常为空，详情页可用）
    /// </summary>
    public PostPollVo? VoPoll { get; set; }

    /// <summary>
    /// 是否附带抽奖
    /// </summary>
    public bool VoHasLottery { get; set; }

    /// <summary>
    /// 抽奖参与人数
    /// </summary>
    public int VoLotteryParticipantCount { get; set; }

    /// <summary>
    /// 抽奖是否已开奖
    /// </summary>
    public bool VoLotteryIsDrawn { get; set; }

    /// <summary>
    /// 抽奖详情（列表页通常为空，详情页可用）
    /// </summary>
    public PostLotteryVo? VoLottery { get; set; }
}
