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
} from '@radish/ui';
import type { TFunction } from 'i18next';
import type {
  Category,
  PostItem,
  PostDetail,
  CommentNode,
  CommentHighlight,
  PageModel,
  PublishPostRequest,
  CreateCommentRequest,
  CommentLikeResult,
  PostLikeResult,
  UpdatePostRequest
} from '@/types/forum';
import { getApiBaseUrl } from '@/config/env';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

// ==================== 类型重导出 ====================

export type {
  Category,
  PostItem,
  PostDetail,
  CommentNode,
  CommentHighlight,
  PageModel,
  PublishPostRequest,
  CreateCommentRequest,
  CommentLikeResult,
  PostLikeResult,
  UpdatePostRequest
};

/**
 * 获取顶级分类列表
 */
export async function getTopCategories(t: TFunction): Promise<Category[]> {
  const response = await apiGet<Category[]>('/api/v1/Category/GetTopCategories');

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
 * @param sortBy 排序方式：newest（最新）、hottest（最热）、essence（精华）
 * @param keyword 搜索关键词（搜索标题和内容）
 */
export async function getPostList(
  categoryId: number | null,
  t: TFunction,
  pageIndex: number = 1,
  pageSize: number = 20,
  sortBy: string = 'newest',
  keyword: string = ''
): Promise<PageModel<PostItem>> {
  const params = new URLSearchParams();
  if (categoryId) params.set('categoryId', categoryId.toString());
  params.set('pageIndex', pageIndex.toString());
  params.set('pageSize', pageSize.toString());
  params.set('sortBy', sortBy);
  if (keyword.trim()) params.set('keyword', keyword.trim());

  const response = await apiGet<PageModel<PostItem>>(
    `/api/v1/Post/GetList?${params.toString()}`
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载帖子失败');
  }

  return response.data;
}

/**
 * 获取帖子详情
 */
export async function getPostById(postId: number, t: TFunction): Promise<PostDetail> {
  const response = await apiGet<PostDetail>(`/api/v1/Post/GetById/${postId}`);

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
 * 获取帖子的评论树（自动包含当前用户的点赞状态）
 */
export async function getCommentTree(postId: number, sortBy: 'newest' | 'hottest' | 'default', t: TFunction): Promise<CommentNode[]> {
  // 如果用户已登录，自动发送token以获取点赞状态
  const hasToken = typeof window !== 'undefined' && window.localStorage.getItem('access_token');

  const response = await apiGet<CommentNode[]>(
    `/api/v1/Comment/GetCommentTree?postId=${postId}&sortBy=${sortBy}`,
    { withAuth: !!hasToken }
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
  const response = await apiPost<number>('/api/v1/Comment/Create', request, { withAuth: true });

  if (!response.ok || response.data === undefined) {
    throw new Error(response.message || '发表评论失败');
  }

  return response.data;
}

/**
 * 点赞/取消点赞帖子
 * @param postId 帖子 ID
 * @returns 点赞操作结果（新的点赞状态和点赞总数）
 */
export async function likePost(postId: number, t: TFunction): Promise<PostLikeResult> {
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
  const response = await apiPut<null>('/api/v1/Post/Update', request, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '编辑帖子失败');
  }
}

/**
 * 删除帖子（软删除）
 * @param postId 帖子 ID
 */
export async function deletePost(postId: number, t: TFunction): Promise<void> {
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
 * 删除评论（软删除）
 * @param commentId 评论 ID
 */
export async function deleteComment(commentId: number, t: TFunction): Promise<void> {
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
  const response = await apiGet<{
    voItems: CommentNode[];
    voTotal: number;
    voPageIndex: number;
    voPageSize: number;
  }>(
    `/api/v1/Comment/GetChildComments?parentId=${parentId}&pageIndex=${pageIndex}&pageSize=${pageSize}`
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取子评论失败');
  }

  // 兼容后端 VoPagedResult 与历史 PageModel 结构
  const raw = response.data;
  const items = raw.items ?? raw.voItems ?? raw.data ?? [];
  const total = raw.total ?? raw.voTotal ?? raw.dataCount ?? items.length;
  const resolvedPageIndex = raw.pageIndex ?? raw.voPageIndex ?? raw.page ?? pageIndex;
  const resolvedPageSize = raw.pageSize ?? raw.voPageSize ?? pageSize;

  return {
    items,
    total,
    pageIndex: resolvedPageIndex,
    pageSize: resolvedPageSize
  };
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
  const response = await apiGet<CommentHighlight[]>(`/api/v1/CommentHighlight/GetCurrentGodComments?postId=${postId}`);

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
  if (!postIds.length) {
    return {};
  }

  const response = await apiPost<Record<number, CommentHighlight>>(
    '/api/v1/CommentHighlight/GetCurrentGodCommentsBatch',
    postIds
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '批量获取神评失败');
  }

  return response.data;
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
  const response = await apiGet<CommentHighlight[]>(`/api/v1/CommentHighlight/GetCurrentSofas?parentCommentId=${parentCommentId}`);

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取沙发列表失败');
  }

  return response.data;
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
