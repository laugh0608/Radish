import type { LongId } from '@/api/user';

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

export type ForumPostViewMode = 'all' | 'question' | 'poll' | 'lottery';

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
  voUserId: LongId;
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
  voPollId: LongId;
  voPostId: LongId;
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
  voId: LongId;
  voLotteryId: LongId;
  voUserId: LongId;
  voUserName: string;
  voCommentId?: LongId | null;
  voCommentContentSnapshot?: string | null;
  voDrawnAt: string;
}

/**
 * 帖子抽奖 Vo
 */
export interface PostLottery {
  voLotteryId: LongId;
  voPostId: LongId;
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
  voPostId: LongId;
  voLottery?: PostLottery | null;
}

/**
 * 投票结果 Vo
 */
export interface PollVoteResult {
  voPostId: LongId;
  voPoll?: PostPoll | null;
}

/**
 * 问答回答 Vo
 */
export interface PostAnswer {
  voAnswerId: LongId;
  voPostId: LongId;
  voAuthorId: LongId;
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
  voPostId: LongId;
  voIsSolved: boolean;
  voAcceptedAnswerId?: LongId | null;
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
  postId: LongId;
  optionId: number;
}

/**
 * 结束投票请求
 */
export interface ClosePollRequest {
  postId: LongId;
}

/**
 * 提交回答请求
 */
export interface CreateAnswerRequest {
  postId: LongId;
  content: string;
}

/**
 * 采纳回答请求
 */
export interface AcceptAnswerRequest {
  postId: LongId;
  answerId: LongId;
}

/**
 * 帖子列表项 Vo
 */
export interface PostItem {
  voId: LongId;
  voPublicId?: string | null;
  voTitle: string;
  voSlug?: string;
  voSummary?: string | null;
  voTags?: string;
  voTagSlugs?: string[];
  voCategoryId: number;
  voCategoryName?: string | null;
  voAuthorId: LongId;
  voAuthorName?: string | null;
  voAuthorAvatarUrl?: string | null;
  voLatestInteractors?: PostInteractor[];
  voGodCommentId?: LongId | null;
  voGodCommentAuthorName?: string | null;
  voGodCommentContentSnapshot?: string | null;
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
  voId: LongId;
  voPublicId?: string | null;
  voTitle: string;
  voSlug?: string;
  voSummary?: string | null;
  voContent: string;
  voContentType?: string;
  voCoverImage?: string | null;
  voCategoryId: number;
  voCategoryName?: string | null;
  voAuthorId: LongId;
  voAuthorName?: string | null;
  voAuthorAvatarUrl?: string | null;
  voTags?: string;
  voTagNames?: string[];
  voTagSlugs?: string[];
  voGodCommentId?: LongId | null;
  voGodCommentAuthorName?: string | null;
  voGodCommentContentSnapshot?: string | null;
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
  voId: LongId;
  voPostId: LongId;
  voContent: string;
  voAuthorId: LongId;
  voAuthorName: string;
  voAuthorAvatarUrl?: string | null;
  voParentId?: LongId | null;
  voRootId?: LongId | null;
  voReplyToCommentId?: LongId | null;
  voReplyToCommentSnapshot?: string | null;
  voReplyToUserId?: LongId | null;
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
 * 评论回复目标
 * 当前论坛 UI 仅展示两级评论，因此提交时始终挂到根评论下，
 * 但仍保留实际点击的回复目标，用于“回复 @某人”的展示。
 */
export interface CommentReplyTarget {
  parentCommentId: LongId;
  targetCommentId: LongId;
  authorName: string;
  contentSnapshot?: string | null;
}

/**
 * 帖子轻回应 Vo
 */
export interface PostQuickReply {
  voId: LongId;
  voPostId: LongId;
  voAuthorId: LongId;
  voAuthorName: string;
  voAuthorAvatarUrl?: string | null;
  voContent: string;
  voStatus: number;
  voCreateTime: string;
}

/**
 * 帖子轻回应墙 Vo
 */
export interface PostQuickReplyWall {
  voItems: PostQuickReply[];
  voTotal: number;
}

/**
 * 个人主页轻回应列表项
 */
export interface UserPostQuickReply {
  voId: LongId;
  voPostId: LongId;
  voPostPublicId?: string | null;
  voPostTitle: string;
  voContent: string;
  voCreateTime: string;
}

/**
 * 帖子编辑历史
 */
export interface PostEditHistory {
  voId: LongId;
  voPostId: LongId;
  voEditSequence: number;
  voOldTitle: string;
  voNewTitle: string;
  voOldContent: string;
  voNewContent: string;
  voEditorId: LongId;
  voEditorName: string;
  voEditedAt: string;
  voCreateTime: string;
}

/**
 * 评论编辑历史
 */
export interface CommentEditHistory {
  voId: LongId;
  voCommentId: LongId;
  voPostId: LongId;
  voEditSequence: number;
  voOldContent: string;
  voNewContent: string;
  voEditorId: LongId;
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
  voId: LongId;
  voPostId: LongId;
  voCommentId: LongId;
  voParentCommentId: LongId | null;
  voHighlightType: number;
  voStatDate: string;
  voLikeCount: number;
  voRank: number;
  voContentSnapshot: string | null;
  voAuthorId: LongId;
  voAuthorName: string;
  voIsCurrent: boolean;
  voCreateTime: string;
}

/**
 * 评论精确定位结果
 */
export interface CommentNavigationLocation {
  voCommentId: LongId;
  voPostId: LongId;
  voRootCommentId: LongId;
  voParentCommentId?: LongId | null;
  voIsRootComment: boolean;
  voRootPageIndex: number;
  voChildPageIndex?: number | null;
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
  targetId: LongId;
  emojiType: 'unicode' | 'sticker';
  emojiValue: string;
}

/**
 * 批量获取 Reaction 汇总请求
 */
export interface BatchGetReactionSummaryRequest {
  targetType: 'Post' | 'Comment' | 'ChatMessage';
  targetIds: LongId[];
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
  postId: LongId;
  title: string;
  content: string;
  categoryId?: number;
  tagNames: string[];
}

/**
 * 设置帖子置顶状态请求
 */
export interface SetPostTopRequest {
  postId: LongId;
  isTop: boolean;
}

/**
 * 创建评论请求
 */
export interface CreateCommentRequest {
  postId: LongId;
  content: string;
  parentId?: LongId | null;
  replyToCommentId?: LongId | null;
  replyToCommentSnapshot?: string | null;
  replyToUserId?: LongId | null;
  replyToUserName?: string | null;
}

/**
 * 创建轻回应请求
 */
export interface CreatePostQuickReplyRequest {
  postId: LongId;
  content: string;
}
