/**
 * 论坛相关的 API 调用
 * 直接使用后端 Vo 字段名，不进行映射
 */

import {
  apiGet,
  apiPost,
  apiPut,
  apiDelete,
  configureApiClient,
} from '@radish/http';
import type { TFunction } from 'i18next';
import type {
  Category,
  Tag,
  PostItem,
  PostDetail,
  CommentNode,
  CommentReplyTarget,
  PostQuickReply,
  PostQuickReplyWall,
  CommentHighlight,
  PostEditHistory,
  CommentEditHistory,
  ForumPostViewMode,
  QuestionStatusFilter,
  PollStatusFilter,
  ForumPostSortBy,
  QuestionAnswerSort,
  QuestionAnswerFilter,
  VoPagedResult,
  PageModel,
  PublishPostRequest,
  CreatePollRequest,
  CreateLotteryRequest,
  CreatePollOptionRequest,
  VotePollRequest,
  ClosePollRequest,
  CreateAnswerRequest,
  AcceptAnswerRequest,
  PostPoll,
  PostPollOption,
  PollVoteResult,
  PostLottery,
  PostLotteryWinner,
  LotteryResult,
  PostQuestion,
  PostAnswer,
  CreateCommentRequest,
  CreatePostQuickReplyRequest,
  CommentLikeResult,
  PostLikeResult,
  SetPostTopRequest,
  UpdatePostRequest,
  ReactionSummaryVo,
  ToggleReactionRequest,
  BatchGetReactionSummaryRequest
} from '@/types/forum';
import { getApiBaseUrl } from '@/config/env';
import { tokenService } from '@/services/tokenService';

const FORUM_READ_TIMEOUT_MS = 60_000;

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

// ==================== 类型重导出 ====================

export type {
  Category,
  Tag,
  PostItem,
  PostDetail,
  CommentNode,
  CommentReplyTarget,
  PostQuickReply,
  PostQuickReplyWall,
  CommentHighlight,
  PostEditHistory,
  CommentEditHistory,
  ForumPostViewMode,
  QuestionStatusFilter,
  PollStatusFilter,
  ForumPostSortBy,
  QuestionAnswerSort,
  QuestionAnswerFilter,
  VoPagedResult,
  PageModel,
  PublishPostRequest,
  CreatePollRequest,
  CreateLotteryRequest,
  CreatePollOptionRequest,
  VotePollRequest,
  ClosePollRequest,
  CreateAnswerRequest,
  AcceptAnswerRequest,
  PostPoll,
  PostPollOption,
  PollVoteResult,
  PostLottery,
  PostLotteryWinner,
  LotteryResult,
  PostQuestion,
  PostAnswer,
  CreateCommentRequest,
  CreatePostQuickReplyRequest,
  CommentLikeResult,
  PostLikeResult,
  SetPostTopRequest,
  UpdatePostRequest,
  ReactionSummaryVo,
  ToggleReactionRequest,
  BatchGetReactionSummaryRequest
};

/**
 * 获取所有标签
 */
export async function getAllTags(t: TFunction): Promise<Tag[]> {
  void t;
  const response = await apiGet<Tag[]>('/api/v1/Tag/GetAll', { timeout: FORUM_READ_TIMEOUT_MS });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载标签失败');
  }

  return response.data;
}

/**
 * 获取固定标签
 */
export async function getFixedTags(t: TFunction): Promise<Tag[]> {
  void t;
  const response = await apiGet<Tag[]>('/api/v1/Tag/GetFixedTags', { timeout: FORUM_READ_TIMEOUT_MS });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载固定标签失败');
  }

  return response.data;
}

/**
 * 获取热门标签
 * @param topCount 返回数量（默认 20）
 */
export async function getHotTags(t: TFunction, topCount: number = 20): Promise<Tag[]> {
  void t;
  const response = await apiGet<Tag[]>(`/api/v1/Tag/GetHotTags?topCount=${topCount}`, { timeout: FORUM_READ_TIMEOUT_MS });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载热门标签失败');
  }

  return response.data;
}

/**
 * 获取顶级分类列表
 */
export async function getTopCategories(t: TFunction): Promise<Category[]> {
  void t;
  const response = await apiGet<Category[]>('/api/v1/Category/GetTopCategories', { timeout: FORUM_READ_TIMEOUT_MS });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载分类失败');
  }

  return response.data;
}

/**
 * 获取帖子列表（支持分页、排序和搜索）
 * @param categoryId 可选的分类 ID，不传则获取所有帖子
 * @param pageIndex 页码（从 1 开始）
 * @param pageSize 每页数量（默认 20）
 * @param sortBy 排序方式：newest（最新）、hottest（最热）、essence（精华）、pending（待解决优先）、answers（回答数）、votes（票数优先）、deadline（即将截止）
 * @param keyword 搜索关键词（搜索标题和内容）
 */
export async function getPostList(
  categoryId: number | null,
  t: TFunction,
  pageIndex: number = 1,
  pageSize: number = 20,
  sortBy: ForumPostSortBy = 'newest',
  keyword: string = '',
  startTime?: string,
  endTime?: string,
  postType: ForumPostViewMode = 'all',
  questionStatus: QuestionStatusFilter = 'all',
  pollStatus: PollStatusFilter = 'all'
): Promise<PageModel<PostItem>> {
  void t;
  const params = new URLSearchParams();
  if (categoryId) params.set('categoryId', categoryId.toString());
  params.set('pageIndex', pageIndex.toString());
  params.set('pageSize', pageSize.toString());
  params.set('sortBy', sortBy);
  params.set('postType', postType);
  params.set('questionStatus', questionStatus);
  params.set('pollStatus', pollStatus);
  if (keyword.trim()) params.set('keyword', keyword.trim());
  if (startTime) params.set('startTime', startTime);
  if (endTime) params.set('endTime', endTime);

  const response = await apiGet<PageModel<PostItem>>(
    `/api/v1/Post/GetList?${params.toString()}`,
    { timeout: FORUM_READ_TIMEOUT_MS }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载帖子失败');
  }

  return response.data;
}

/**
 * 获取帖子详情
 */
export async function getPostById(
  postId: number,
  t: TFunction,
  answerSort: QuestionAnswerSort = 'default'
): Promise<PostDetail> {
  void t;
  const response = await apiGet<PostDetail>(
    `/api/v1/Post/GetById/${postId}?answerSort=${answerSort}`,
    { timeout: FORUM_READ_TIMEOUT_MS, withAuth: true }
  );

  if (!response.ok || !response.data) {
    // 针对帖子不存在的情况给出友好提示
    if (!response.ok && (response.statusCode === 404 || response.statusCode === 410)) {
      throw new Error(response.message || '帖子不存在或已被删除');
    }
    throw new Error(response.message || '加载帖子详情失败');
  }

  return response.data;
}

/**
 * 按帖子获取投票详情
 */
export async function getPollByPostId(postId: number, t: TFunction): Promise<PollVoteResult> {
  void t;
  const response = await apiGet<PollVoteResult>(
    `/api/v1/Poll/GetByPostId?postId=${postId}`,
    { timeout: FORUM_READ_TIMEOUT_MS }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载投票详情失败');
  }

  return response.data;
}

/**
 * 按帖子获取抽奖详情
 */
export async function getLotteryByPostId(postId: number, t: TFunction): Promise<LotteryResult> {
  void t;
  const response = await apiGet<LotteryResult>(
    `/api/v1/Lottery/GetByPostId?postId=${postId}`,
    { timeout: FORUM_READ_TIMEOUT_MS }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载抽奖详情失败');
  }

  return response.data;
}

/**
 * 手动开奖
 */
export async function drawLottery(postId: number, t: TFunction): Promise<PostLottery> {
  void t;
  const response = await apiPost<PostLottery>(
    '/api/v1/Lottery/Draw',
    { postId },
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '开奖失败');
  }

  return response.data;
}

/**
 * 提交投票
 */
export async function votePoll(request: VotePollRequest, t: TFunction): Promise<PostPoll> {
  void t;
  const response = await apiPost<PostPoll>('/api/v1/Poll/Vote', request, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '投票失败');
  }

  return response.data;
}

/**
 * 手动结束投票
 */
export async function closePoll(request: ClosePollRequest, t: TFunction): Promise<PostPoll> {
  void t;
  const response = await apiPost<PostPoll>('/api/v1/Poll/Close', request, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '结束投票失败');
  }

  return response.data;
}

/**
 * 提交问答回答
 */
export async function answerQuestion(request: CreateAnswerRequest, t: TFunction): Promise<PostQuestion> {
  void t;
  const response = await apiPost<PostQuestion>('/api/v1/Question/Answer', request, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '提交回答失败');
  }

  return response.data;
}

/**
 * 采纳问答回答
 */
export async function acceptQuestionAnswer(request: AcceptAnswerRequest, t: TFunction): Promise<PostQuestion> {
  void t;
  const response = await apiPost<PostQuestion>('/api/v1/Question/Accept', request, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '采纳回答失败');
  }

  return response.data;
}

/**
 * 分页获取帖子的根评论（自动包含当前用户的点赞状态）
 */
export async function getRootCommentsPage(
  postId: number,
  pageIndex: number,
  pageSize: number,
  sortBy: 'newest' | 'hottest' | 'default',
  t: TFunction
): Promise<VoPagedResult<CommentNode>> {
  void t;
  const hasToken = Boolean(tokenService.getAccessToken());

  const response = await apiGet<VoPagedResult<CommentNode>>(
    `/api/v1/Comment/GetRootComments?postId=${postId}&pageIndex=${pageIndex}&pageSize=${pageSize}&sortBy=${sortBy}`,
    { withAuth: !!hasToken, timeout: FORUM_READ_TIMEOUT_MS }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载评论失败');
  }

  return response.data;
}

/**
 * 发布新帖子
 * @returns 新帖子的 ID
 */
export async function publishPost(request: PublishPostRequest, t: TFunction): Promise<number> {
  void t;
  const response = await apiPost<number>('/api/v1/Post/Publish', request, { withAuth: true });

  if (!response.ok || response.data === undefined) {
    throw new Error(response.message || '发布帖子失败');
  }

  return response.data;
}

/**
 * 创建评论
 * @returns 新评论的 ID
 */
export async function createComment(request: CreateCommentRequest, t: TFunction): Promise<number> {
  void t;
  const response = await apiPost<number>('/api/v1/Comment/Create', request, { withAuth: true });

  if (!response.ok || response.data === undefined) {
    throw new Error(response.message || '发表评论失败');
  }

  return response.data;
}

/**
 * 获取帖子轻回应墙
 */
export async function getPostQuickReplyWall(
  postId: number,
  t: TFunction,
  take: number = 30
): Promise<PostQuickReplyWall> {
  void t;
  const response = await apiGet<PostQuickReplyWall>(
    `/api/v1/PostQuickReply/GetRecentByPostId?postId=${postId}&take=${take}`,
    { timeout: FORUM_READ_TIMEOUT_MS }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载轻回应失败');
  }

  return response.data;
}

/**
 * 创建帖子轻回应
 */
export async function createPostQuickReply(
  request: CreatePostQuickReplyRequest,
  t: TFunction
): Promise<PostQuickReply> {
  void t;
  const response = await apiPost<PostQuickReply>(
    '/api/v1/PostQuickReply/Create',
    request,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '发布轻回应失败');
  }

  return response.data;
}

/**
 * 删除帖子轻回应
 */
export async function deletePostQuickReply(quickReplyId: number, t: TFunction): Promise<void> {
  void t;
  const response = await apiDelete<null>(
    `/api/v1/PostQuickReply/Delete?quickReplyId=${quickReplyId}`,
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '删除轻回应失败');
  }
}

/**
 * 点赞/取消点赞帖子
 * @param postId 帖子 ID
 * @returns 点赞操作结果（新的点赞状态和点赞总数）
 */
export async function likePost(postId: number, t: TFunction): Promise<PostLikeResult> {
  void t;
  const response = await apiPost<PostLikeResult>(
    `/api/v1/Post/Like?postId=${postId}`,
    undefined,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '点赞操作失败');
  }

  return response.data;
}

/**
 * 切换评论点赞状态（智能切换：已点赞则取消，未点赞则点赞）
 * @param commentId 评论 ID
 * @returns 点赞操作结果（新的点赞状态和点赞总数）
 */
export async function toggleCommentLike(commentId: number, t: TFunction): Promise<CommentLikeResult> {
  void t;
  const response = await apiPost<CommentLikeResult>(
    `/api/v1/Comment/ToggleLike?commentId=${commentId}`,
    undefined,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '点赞操作失败');
  }

  return response.data;
}

/**
 * 编辑帖子
 * @param request 编辑请求
 */
export async function updatePost(request: UpdatePostRequest, t: TFunction): Promise<void> {
  void t;
  const response = await apiPut<null>('/api/v1/Post/Update', request, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '编辑帖子失败');
  }
}

/**
 * 设置帖子置顶状态
 * @param request 置顶请求
 */
export async function setPostTop(request: SetPostTopRequest, t: TFunction): Promise<void> {
  void t;
  const response = await apiPost<null>('/api/v1/Post/SetTop', request, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '设置帖子置顶状态失败');
  }
}

/**
 * 删除帖子（软删除）
 * @param postId 帖子 ID
 */
export async function deletePost(postId: number, t: TFunction): Promise<void> {
  void t;
  const response = await apiDelete<null>(`/api/v1/Post/Delete?postId=${postId}`, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '删除帖子失败');
  }
}

/**
 * 编辑评论
 * @param request 编辑评论请求参数
 */
export async function updateComment(
  request: { commentId: number; content: string },
  t: TFunction
): Promise<void> {
  void t;
  const response = await apiPut<null>(
    '/api/v1/Comment/Update',
    {
      commentId: request.commentId,
      content: request.content
    },
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '编辑评论失败');
  }
}

/**
 * 获取帖子编辑历史
 */
export async function getPostEditHistory(
  postId: number,
  pageIndex: number,
  pageSize: number,
  t: TFunction
): Promise<VoPagedResult<PostEditHistory>> {
  void t;
  const hasToken = Boolean(tokenService.getAccessToken());
  const response = await apiGet<VoPagedResult<PostEditHistory>>(
    `/api/v1/Post/GetEditHistory?postId=${postId}&pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { withAuth: hasToken, timeout: FORUM_READ_TIMEOUT_MS }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取帖子编辑历史失败');
  }

  return response.data;
}

/**
 * 获取评论编辑历史
 */
export async function getCommentEditHistory(
  commentId: number,
  pageIndex: number,
  pageSize: number,
  t: TFunction
): Promise<VoPagedResult<CommentEditHistory>> {
  void t;
  const hasToken = Boolean(tokenService.getAccessToken());
  const response = await apiGet<VoPagedResult<CommentEditHistory>>(
    `/api/v1/Comment/GetEditHistory?commentId=${commentId}&pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { withAuth: hasToken, timeout: FORUM_READ_TIMEOUT_MS }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取评论编辑历史失败');
  }

  return response.data;
}

/**
 * 删除评论（软删除）
 * @param commentId 评论 ID
 */
export async function deleteComment(commentId: number, t: TFunction): Promise<void> {
  void t;
  const response = await apiDelete<null>(`/api/v1/Comment/Delete?commentId=${commentId}`, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '删除评论失败');
  }
}

/**
 * 分页获取子评论（按点赞数降序）
 * @param parentId 父评论 ID
 * @param pageIndex 页码（从 1 开始）
 * @param pageSize 每页数量
 * @param t i18n 翻译函数
 * @returns 子评论列表、总数、页码信息
 */
export async function getChildComments(
  parentId: number | string,
  pageIndex: number,
  pageSize: number,
  t: TFunction
): Promise<{ voItems: CommentNode[]; voTotal: number; voPageIndex: number; voPageSize: number }> {
  void t;
  const response = await apiGet<{
    voItems: CommentNode[];
    voTotal: number;
    voPageIndex: number;
    voPageSize: number;
  }>(
    `/api/v1/Comment/GetChildComments?parentId=${parentId}&pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { timeout: FORUM_READ_TIMEOUT_MS }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取子评论失败');
  }

  return response.data;
}

/**
 * 获取帖子的当前神评列表
 * @param postId 帖子 ID
 * @param t i18n 翻译函数
 * @returns 神评列表（按排名升序）
 */
export async function getCurrentGodComments(
  postId: number,
  t: TFunction
): Promise<CommentHighlight[]> {
  void t;
  const response = await apiGet<CommentHighlight[]>(
    `/api/v1/CommentHighlight/GetCurrentGodComments?postId=${postId}`,
    { timeout: FORUM_READ_TIMEOUT_MS }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取神评列表失败');
  }

  return response.data;
}

/**
 * 批量获取帖子当前神评（每帖 Top1）
 * @param postIds 帖子 ID 列表
 * @param t i18n 翻译函数
 */
export async function getCurrentGodCommentsBatch(
  postIds: number[],
  t: TFunction
): Promise<Record<number, CommentHighlight>> {
  void t;
  if (!postIds.length) {
    return {};
  }

  const maxAttempts = 3;
  let lastError: Error | null = null;

  for (let attempt = 1; attempt <= maxAttempts; attempt += 1) {
    const response = await apiPost<Record<number, CommentHighlight>>(
      '/api/v1/CommentHighlight/GetCurrentGodCommentsBatch',
      postIds,
      { timeout: FORUM_READ_TIMEOUT_MS }
    );

    if (response.ok && response.data) {
      return response.data;
    }

    lastError = new Error(response.message || '批量获取神评失败');
    if (attempt < maxAttempts) {
      await new Promise((resolve) => {
        setTimeout(resolve, attempt * 250);
      });
    }
  }

  throw lastError || new Error('批量获取神评失败');
}

/**
 * 获取父评论的当前沙发列表
 * @param parentCommentId 父评论 ID
 * @param t i18n 翻译函数
 * @returns 沙发列表（按排名升序）
 */
export async function getCurrentSofas(
  parentCommentId: number,
  t: TFunction
): Promise<CommentHighlight[]> {
  void t;
  const response = await apiGet<CommentHighlight[]>(
    `/api/v1/CommentHighlight/GetCurrentSofas?parentCommentId=${parentCommentId}`,
    { timeout: FORUM_READ_TIMEOUT_MS }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取沙发列表失败');
  }

  return response.data;
}

/**
 * 获取单目标 Reaction 汇总
 */
export async function getReactionSummary(
  targetType: 'Post' | 'Comment' | 'ChatMessage',
  targetId: number
): Promise<ReactionSummaryVo[]> {
  const response = await apiGet<ReactionSummaryVo[]>(
    `/api/v1/Reaction/GetSummary?targetType=${encodeURIComponent(targetType)}&targetId=${targetId}`,
    { timeout: FORUM_READ_TIMEOUT_MS }
  );

  if (!response.ok) {
    throw new Error(response.message || '加载回应失败');
  }

  return response.data || [];
}

/**
 * 批量获取多个目标的 Reaction 汇总
 */
export async function batchGetReactionSummary(
  request: BatchGetReactionSummaryRequest
): Promise<Record<string, ReactionSummaryVo[]>> {
  const response = await apiPost<Record<string, ReactionSummaryVo[]>>(
    '/api/v1/Reaction/BatchGetSummary',
    request,
    { timeout: FORUM_READ_TIMEOUT_MS }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '批量加载回应失败');
  }

  return response.data;
}

/**
 * 切换 Reaction（添加/取消）
 */
export async function toggleReaction(
  request: ToggleReactionRequest
): Promise<ReactionSummaryVo[]> {
  const response = await apiPost<ReactionSummaryVo[]>(
    '/api/v1/Reaction/Toggle',
    request,
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '回应操作失败');
  }

  return response.data || [];
}

/**
 * 生成 OIDC 登录 URL
 */
export function getOidcLoginUrl(): string {
  if (typeof window === 'undefined') return '';
  const currentOrigin = window.location.origin;
  const redirectUri = `${currentOrigin}/oidc/callback`;
  const apiBaseUrl = getApiBaseUrl();
  const authorizeUrl = new URL(`${apiBaseUrl}/connect/authorize`);
  authorizeUrl.searchParams.set('client_id', 'radish-client');
  authorizeUrl.searchParams.set('response_type', 'code');
  authorizeUrl.searchParams.set('redirect_uri', redirectUri);
  authorizeUrl.searchParams.set('scope', 'openid profile offline_access radish-api');
  return authorizeUrl.toString();
}
