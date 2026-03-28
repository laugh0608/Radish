/**
 * 论坛相关的 TypeScript 类型定义
 * 直接使用后端 Vo 字段名，不进行映射
 */

/**
 * 分页模型
 */
export interface PageModel<T> {
  /** 当前页码 */
  page: number;
  /** 每页数量 */
  pageSize: number;
  /** 总数据量 */
  dataCount: number;
  /** 总页数 */
  pageCount: number;
  /** 数据列表 */
  data: T[];
}

export type ForumPostViewMode = 'all' | 'question' | 'poll';

export type QuestionStatusFilter = 'all' | 'pending' | 'solved';

export type PollStatusFilter = 'all' | 'active' | 'closed';

export type ForumPostSortBy = 'newest' | 'hottest' | 'essence' | 'pending' | 'answers' | 'votes' | 'deadline';

export type QuestionAnswerSort = 'default' | 'latest';

export type QuestionAnswerFilter = 'all' | 'accepted';

/**
 * 分类 Vo
 */
export interface Category {
  voId: number;
  voName: string;
  voSlug: string;
  voDescription?: string | null;
  voIcon?: string | null;
  voCoverImage?: string | null;
  voParentId?: number | null;
  voLevel?: number;
  voOrderSort?: number;
  voPostCount?: number;
  voIsEnabled?: boolean;
  voIsDeleted?: boolean;
  voCreateTime?: string;
  voModifyTime?: string | null;
  voCreateBy?: string | null;
}

/**
 * 标签 Vo
 */
export interface Tag {
  voId: number;
  voName: string;
  voSlug: string;
  voDescription?: string | null;
  voColor?: string | null;
  voSortOrder?: number;
  voPostCount?: number;
  voIsEnabled?: boolean;
  voIsFixed?: boolean;
  voIsDeleted?: boolean;
  voCreateTime?: string;
  voModifyTime?: string | null;
}

/**
 * 帖子列表项 Vo
 */
export interface PostInteractor {
  voUserId: number;
  voUserName: string;
  voAvatarUrl?: string | null;
}

/**
 * 投票选项 Vo
 */
export interface PostPollOption {
  voOptionId: number;
  voOptionText: string;
  voSortOrder: number;
  voVoteCount: number;
  voVotePercent: number;
}

/**
 * 帖子投票 Vo
 */
export interface PostPoll {
  voPollId: number;
  voPostId: number;
  voQuestion: string;
  voEndTime?: string | null;
  voIsClosed: boolean;
  voTotalVoteCount: number;
  voHasVoted: boolean;
  voSelectedOptionId?: number | null;
  voOptions: PostPollOption[];
}

/**
 * 抽奖中奖人 Vo
 */
export interface PostLotteryWinner {
  voId: number;
  voLotteryId: number;
  voUserId: number;
  voUserName: string;
  voCommentId?: number | null;
  voCommentContentSnapshot?: string | null;
  voDrawnAt: string;
}

/**
 * 帖子抽奖 Vo
 */
export interface PostLottery {
  voLotteryId: number;
  voPostId: number;
  voPrizeName: string;
  voPrizeDescription?: string | null;
  voDrawTime?: string | null;
  voDrawnAt?: string | null;
  voWinnerCount: number;
  voParticipantCount: number;
  voIsDrawn: boolean;
  voWinners: PostLotteryWinner[];
}

/**
 * 抽奖结果 Vo
 */
export interface LotteryResult {
  voPostId: number;
  voLottery?: PostLottery | null;
}

/**
 * 投票结果 Vo
 */
export interface PollVoteResult {
  voPostId: number;
  voPoll?: PostPoll | null;
}

/**
 * 问答回答 Vo
 */
export interface PostAnswer {
  voAnswerId: number;
  voPostId: number;
  voAuthorId: number;
  voAuthorName: string;
  voAuthorAvatarUrl?: string | null;
  voContent: string;
  voIsAccepted: boolean;
  voCreateTime: string;
}

/**
 * 帖子问答详情 Vo
 */
export interface PostQuestion {
  voPostId: number;
  voIsSolved: boolean;
  voAcceptedAnswerId?: number | null;
  voAnswerCount: number;
  voAnswers: PostAnswer[];
}

/**
 * 创建投票请求
 */
export interface CreatePollRequest {
  question: string;
  endTime?: string | null;
  options: CreatePollOptionRequest[];
}

/**
 * 创建抽奖请求
 */
export interface CreateLotteryRequest {
  prizeName: string;
  prizeDescription?: string | null;
  drawTime?: string | null;
  winnerCount: number;
}

/**
 * 创建投票选项请求
 */
export interface CreatePollOptionRequest {
  optionText: string;
  sortOrder?: number;
}

/**
 * 提交投票请求
 */
export interface VotePollRequest {
  postId: number;
  optionId: number;
}

/**
 * 结束投票请求
 */
export interface ClosePollRequest {
  postId: number;
}

/**
 * 提交回答请求
 */
export interface CreateAnswerRequest {
  postId: number;
  content: string;
}

/**
 * 采纳回答请求
 */
export interface AcceptAnswerRequest {
  postId: number;
  answerId: number;
}

/**
 * 帖子列表项 Vo
 */
export interface PostItem {
  voId: number;
  voTitle: string;
  voSlug?: string;
  voSummary?: string | null;
  voTags?: string;
  voCategoryId: number;
  voCategoryName?: string | null;
  voAuthorId: number;
  voAuthorName?: string | null;
  voAuthorAvatarUrl?: string | null;
  voLatestInteractors?: PostInteractor[];
  voViewCount?: number;
  voLikeCount?: number;
  voCommentCount?: number;
  voIsTop?: boolean;
  voIsEssence?: boolean;
  voIsLocked?: boolean;
  voIsQuestion?: boolean;
  voIsSolved?: boolean;
  voAnswerCount?: number;
  voHasPoll?: boolean;
  voPollTotalVoteCount?: number;
  voPollIsClosed?: boolean;
  voHasLottery?: boolean;
  voLotteryParticipantCount?: number;
  voLotteryIsDrawn?: boolean;
  voCreateTime?: string;
  voUpdateTime?: string;
}

/**
 * 帖子详情 Vo
 */
export interface PostDetail {
  voId: number;
  voTitle: string;
  voSlug?: string;
  voSummary?: string | null;
  voContent: string;
  voContentType?: string;
  voCoverImage?: string | null;
  voCategoryId: number;
  voCategoryName?: string | null;
  voAuthorId: number;
  voAuthorName?: string | null;
  voAuthorAvatarUrl?: string | null;
  voTags?: string;
  voTagNames?: string[];
  voViewCount?: number;
  voLikeCount?: number;
  voCommentCount?: number;
  voIsTop?: boolean;
  voIsEssence?: boolean;
  voIsLocked?: boolean;
  voIsQuestion?: boolean;
  voIsSolved?: boolean;
  voAnswerCount?: number;
  voQuestion?: PostQuestion | null;
  voHasPoll?: boolean;
  voPollTotalVoteCount?: number;
  voPollIsClosed?: boolean;
  voPoll?: PostPoll | null;
  voHasLottery?: boolean;
  voLotteryParticipantCount?: number;
  voLotteryIsDrawn?: boolean;
  voLottery?: PostLottery | null;
  voCreateTime?: string;
  voUpdateTime?: string;
}

/**
 * 评论节点 Vo（树形结构）
 */
export interface CommentNode {
  voId: number;
  voPostId: number;
  voContent: string;
  voAuthorId: number;
  voAuthorName: string;
  voAuthorAvatarUrl?: string | null;
  voParentId?: number | null;
  voRootId?: number | null;
  voReplyToUserId?: number | null;
  voReplyToUserName?: string | null;
  voLevel?: number;
  voLikeCount?: number;
  voReplyCount?: number;
  voIsTop?: boolean;
  voIsLiked?: boolean;
  voCreateTime?: string;
  voUpdateTime?: string;
  voChildren?: CommentNode[];
  voChildrenTotal?: number;
  voIsGodComment?: boolean;
  voIsSofa?: boolean;
  voHighlightRank?: number;
}

/**
 * 帖子编辑历史
 */
export interface PostEditHistory {
  voId: number;
  voPostId: number;
  voEditSequence: number;
  voOldTitle: string;
  voNewTitle: string;
  voOldContent: string;
  voNewContent: string;
  voEditorId: number;
  voEditorName: string;
  voEditedAt: string;
  voCreateTime: string;
}

/**
 * 评论编辑历史
 */
export interface CommentEditHistory {
  voId: number;
  voCommentId: number;
  voPostId: number;
  voEditSequence: number;
  voOldContent: string;
  voNewContent: string;
  voEditorId: number;
  voEditorName: string;
  voEditedAt: string;
  voCreateTime: string;
}

/**
 * Vo 分页结果
 */
export interface VoPagedResult<T> {
  voItems: T[];
  voTotal: number;
  voPageIndex: number;
  voPageSize: number;
}

/**
 * 神评/沙发高亮记录 Vo
 */
export interface CommentHighlight {
  voId: number;
  voPostId: number;
  voCommentId: number;
  voParentCommentId: number | null;
  voHighlightType: number;
  voStatDate: string;
  voLikeCount: number;
  voRank: number;
  voContentSnapshot: string | null;
  voAuthorId: number;
  voAuthorName: string;
  voIsCurrent: boolean;
  voCreateTime: string;
}

/**
 * 评论点赞操作结果
 * 注意：后端返回字段为 isLiked / likeCount
 */
export interface CommentLikeResult {
  isLiked: boolean;
  likeCount: number;
}

/**
 * Reaction 汇总项
 */
export interface ReactionSummaryVo {
  voEmojiType: 'unicode' | 'sticker';
  voEmojiValue: string;
  voCount: number;
  voIsReacted: boolean;
  voThumbnailUrl?: string | null;
}

/**
 * 切换 Reaction 请求
 */
export interface ToggleReactionRequest {
  targetType: 'Post' | 'Comment' | 'ChatMessage';
  targetId: number;
  emojiType: 'unicode' | 'sticker';
  emojiValue: string;
}

/**
 * 批量获取 Reaction 汇总请求
 */
export interface BatchGetReactionSummaryRequest {
  targetType: 'Post' | 'Comment' | 'ChatMessage';
  targetIds: number[];
}

/**
 * 帖子点赞操作结果
 * 注意：后端返回字段为 voIsLiked / voLikeCount
 */
export interface PostLikeResult {
  voIsLiked: boolean;
  voLikeCount: number;
}

/**
 * 发布帖子请求
 */
export interface PublishPostRequest {
  title: string;
  content: string;
  categoryId: number;
  tagNames: string[];
  isQuestion?: boolean;
  poll?: CreatePollRequest | null;
  lottery?: CreateLotteryRequest | null;
}

/**
 * 编辑帖子请求
 */
export interface UpdatePostRequest {
  postId: number;
  title: string;
  content: string;
  categoryId?: number;
  tagNames: string[];
}

/**
 * 设置帖子置顶状态请求
 */
export interface SetPostTopRequest {
  postId: number;
  isTop: boolean;
}

/**
 * 创建评论请求
 */
export interface CreateCommentRequest {
  postId: number;
  content: string;
  parentId?: number | null;
  replyToUserId?: number | null;
  replyToUserName?: string | null;
}
