/**
 * 论坛相关的 API 调用
 */

import { parseApiResponse, type ApiResponse } from '@/api/client';
import type { TFunction } from 'i18next';
import type {
  Category,
  PostItem,
  PostDetail,
  CommentNode,
  PublishPostRequest,
  CreateCommentRequest,
  CommentLikeResult,
  CommentHighlight
} from '@/types/forum';

const defaultApiBase = 'https://localhost:5000';

/**
 * 获取 API Base URL
 */
function getApiBaseUrl(): string {
  const configured = import.meta.env.VITE_API_BASE_URL as string | undefined;
  return (configured ?? defaultApiBase).replace(/\/$/, '');
}

/**
 * 带认证的 fetch 封装
 */
interface ApiFetchOptions extends RequestInit {
  withAuth?: boolean;
}

function apiFetch(input: RequestInfo | URL, options: ApiFetchOptions = {}) {
  const { withAuth, headers, ...rest } = options;

  const finalHeaders: HeadersInit = {
    Accept: 'application/json',
    ...headers
  };

  if (withAuth && typeof window !== 'undefined') {
    const token = window.localStorage.getItem('access_token');
    if (token) {
      (finalHeaders as Record<string, string>).Authorization = `Bearer ${token}`;
    }
  }

  return fetch(input, {
    ...rest,
    headers: finalHeaders
  });
}

/**
 * 获取顶级分类列表
 */
export async function getTopCategories(t: TFunction): Promise<Category[]> {
  const url = `${getApiBaseUrl()}/api/v1/Category/GetTopCategories`;
  const response = await apiFetch(url);
  const json = await response.json() as ApiResponse<Category[]>;
  const parsed = parseApiResponse<Category[]>(json, t);

  if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || '加载分类失败');
  }

  return parsed.data;
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
): Promise<import('@/types/forum').PageModel<PostItem>> {
  const baseUrl = `${getApiBaseUrl()}/api/v1/Post/GetList`;
  const params = new URLSearchParams();
  if (categoryId) params.set('categoryId', categoryId.toString());
  params.set('pageIndex', pageIndex.toString());
  params.set('pageSize', pageSize.toString());
  params.set('sortBy', sortBy);
  if (keyword.trim()) params.set('keyword', keyword.trim());

  const url = `${baseUrl}?${params.toString()}`;
  const response = await apiFetch(url);
  const json = await response.json() as ApiResponse<import('@/types/forum').PageModel<PostItem>>;
  const parsed = parseApiResponse<import('@/types/forum').PageModel<PostItem>>(json, t);

  if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || '加载帖子失败');
  }

  return parsed.data;
}

/**
 * 获取帖子详情
 */
export async function getPostById(postId: number, t: TFunction): Promise<PostDetail> {
  const url = `${getApiBaseUrl()}/api/v1/Post/GetById/${postId}`;
  const response = await apiFetch(url);
  const json = await response.json() as ApiResponse<PostDetail>;
  const parsed = parseApiResponse<PostDetail>(json, t);

  if (!parsed.ok || !parsed.data) {
    // 针对帖子不存在的情况给出友好提示
    if (!parsed.ok && (json.statusCode === 404 || json.statusCode === 410)) {
      throw new Error(parsed.message || '帖子不存在或已被删除');
    }
    throw new Error(parsed.message || '加载帖子详情失败');
  }

  return parsed.data;
}

/**
 * 获取帖子的评论树（自动包含当前用户的点赞状态）
 */
export async function getCommentTree(postId: number, sortBy: 'newest' | 'hottest' | 'default', t: TFunction): Promise<CommentNode[]> {
  const url = `${getApiBaseUrl()}/api/v1/Comment/GetCommentTree?postId=${postId}&sortBy=${sortBy}`;
  // 如果用户已登录，自动发送token以获取点赞状态
  const hasToken = typeof window !== 'undefined' && window.localStorage.getItem('access_token');
  const response = await apiFetch(url, { withAuth: !!hasToken });
  const json = await response.json() as ApiResponse<CommentNode[]>;
  const parsed = parseApiResponse<CommentNode[]>(json, t);

  if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || '加载评论失败');
  }

  return parsed.data;
}

/**
 * 发布新帖子
 * @returns 新帖子的 ID
 */
export async function publishPost(request: PublishPostRequest, t: TFunction): Promise<number> {
  const url = `${getApiBaseUrl()}/api/v1/Post/Publish`;
  const response = await apiFetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(request),
    withAuth: true
  });
  const json = await response.json() as ApiResponse<number>;
  const parsed = parseApiResponse<number>(json, t);

  if (!parsed.ok || parsed.data === undefined) {
    throw new Error(parsed.message || '发布帖子失败');
  }

  return parsed.data;
}

/**
 * 创建评论
 * @returns 新评论的 ID
 */
export async function createComment(request: CreateCommentRequest, t: TFunction): Promise<number> {
  const url = `${getApiBaseUrl()}/api/v1/Comment/Create`;
  const response = await apiFetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(request),
    withAuth: true
  });
  const json = await response.json() as ApiResponse<number>;
  const parsed = parseApiResponse<number>(json, t);

  if (!parsed.ok || parsed.data === undefined) {
    throw new Error(parsed.message || '发表评论失败');
  }

  return parsed.data;
}

/**
 * 点赞/取消点赞帖子
 * @param postId 帖子 ID
 * @param isLike true 为点赞，false 为取消点赞
 */
export async function likePost(postId: number, isLike: boolean, t: TFunction): Promise<void> {
  const url = `${getApiBaseUrl()}/api/v1/Post/Like?postId=${postId}&isLike=${isLike}`;
  const response = await apiFetch(url, {
    method: 'POST',
    withAuth: true
  });
  const json = await response.json() as ApiResponse<null>;
  const parsed = parseApiResponse<null>(json, t);

  if (!parsed.ok) {
    throw new Error(parsed.message || '点赞操作失败');
  }
}

/**
 * 切换评论点赞状态（智能切换：已点赞则取消，未点赞则点赞）
 * @param commentId 评论 ID
 * @returns 点赞操作结果（新的点赞状态和点赞总数）
 */
export async function toggleCommentLike(commentId: number, t: TFunction): Promise<CommentLikeResult> {
  const url = `${getApiBaseUrl()}/api/v1/Comment/ToggleLike?commentId=${commentId}`;
  const response = await apiFetch(url, {
    method: 'POST',
    withAuth: true
  });
  const json = await response.json() as ApiResponse<CommentLikeResult>;
  const parsed = parseApiResponse<CommentLikeResult>(json, t);

  if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || '点赞操作失败');
  }

  return parsed.data;
}

/**
 * 编辑帖子
 * @param request 编辑请求
 */
export async function updatePost(request: import('@/types/forum').UpdatePostRequest, t: TFunction): Promise<void> {
  const url = `${getApiBaseUrl()}/api/v1/Post/Update`;
  const response = await apiFetch(url, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(request),
    withAuth: true
  });
  const json = await response.json() as ApiResponse<null>;
  const parsed = parseApiResponse<null>(json, t);

  if (!parsed.ok) {
    throw new Error(parsed.message || '编辑帖子失败');
  }
}

/**
 * 删除帖子（软删除）
 * @param postId 帖子 ID
 */
export async function deletePost(postId: number, t: TFunction): Promise<void> {
  const url = `${getApiBaseUrl()}/api/v1/Post/Delete?postId=${postId}`;
  const response = await apiFetch(url, {
    method: 'DELETE',
    withAuth: true
  });
  const json = await response.json() as ApiResponse<null>;
  const parsed = parseApiResponse<null>(json, t);

  if (!parsed.ok) {
    throw new Error(parsed.message || '删除帖子失败');
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
  const url = `${getApiBaseUrl()}/api/v1/Comment/Update`;
  const response = await apiFetch(url, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      commentId: request.commentId,
      content: request.content
    }),
    withAuth: true
  });
  const json = await response.json() as ApiResponse<null>;
  const parsed = parseApiResponse<null>(json, t);

  if (!parsed.ok) {
    throw new Error(parsed.message || '编辑评论失败');
  }
}

/**
 * 删除评论（软删除）
 * @param commentId 评论 ID
 */
export async function deleteComment(commentId: number, t: TFunction): Promise<void> {
  const url = `${getApiBaseUrl()}/api/v1/Comment/Delete?commentId=${commentId}`;
  const response = await apiFetch(url, {
    method: 'DELETE',
    withAuth: true
  });
  const json = await response.json() as ApiResponse<null>;
  const parsed = parseApiResponse<null>(json, t);

  if (!parsed.ok) {
    throw new Error(parsed.message || '删除评论失败');
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
): Promise<{ comments: CommentNode[]; total: number; pageIndex: number; pageSize: number }> {
  const url = `${getApiBaseUrl()}/api/v1/Comment/GetChildComments?parentId=${parentId}&pageIndex=${pageIndex}&pageSize=${pageSize}`;
  const response = await apiFetch(url); // 匿名访问

  if (!response.ok) {
    throw new Error(`获取子评论失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<{
    comments: CommentNode[];
    total: number;
    pageIndex: number;
    pageSize: number;
  }>;
  const parsed = parseApiResponse(json, t);

  if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || '获取子评论失败');
  }

  return parsed.data;
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
  const url = `${getApiBaseUrl()}/api/v1/CommentHighlight/GetCurrentGodComments?postId=${postId}`;
  const response = await apiFetch(url);
  const json = await response.json() as ApiResponse<CommentHighlight[]>;
  const parsed = parseApiResponse<CommentHighlight[]>(json, t);

  if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || '获取神评列表失败');
  }

  return parsed.data;
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
  const url = `${getApiBaseUrl()}/api/v1/CommentHighlight/GetCurrentSofas?parentCommentId=${parentCommentId}`;
  const response = await apiFetch(url);
  const json = await response.json() as ApiResponse<CommentHighlight[]>;
  const parsed = parseApiResponse<CommentHighlight[]>(json, t);

  if (!parsed.ok || !parsed.data) {
    throw new Error(parsed.message || '获取沙发列表失败');
  }

  return parsed.data;
}

/**
 * 生成 OIDC 登录 URL
 */
export function getOidcLoginUrl(): string {
  if (typeof window === 'undefined') return '';
  const currentOrigin = window.location.origin;
  const redirectUri = `${currentOrigin}/oidc/callback`;
  const authorizeUrl = new URL(`${getApiBaseUrl()}/connect/authorize`);
  authorizeUrl.searchParams.set('client_id', 'radish-client');
  authorizeUrl.searchParams.set('response_type', 'code');
  authorizeUrl.searchParams.set('redirect_uri', redirectUri);
  authorizeUrl.searchParams.set('scope', 'radish-api');
  return authorizeUrl.toString();
}
