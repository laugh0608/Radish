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
  CreateCommentRequest
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
 * 获取帖子列表（支持分页和排序）
 * @param categoryId 可选的分类 ID，不传则获取所有帖子
 * @param pageIndex 页码（从 1 开始）
 * @param pageSize 每页数量（默认 20）
 * @param sortBy 排序方式：newest（最新）、hottest（最热）、essence（精华）
 */
export async function getPostList(
  categoryId: number | null,
  t: TFunction,
  pageIndex: number = 1,
  pageSize: number = 20,
  sortBy: string = 'newest'
): Promise<import('@/types/forum').PageModel<PostItem>> {
  const baseUrl = `${getApiBaseUrl()}/api/v1/Post/GetList`;
  const params = new URLSearchParams();
  if (categoryId) params.set('categoryId', categoryId.toString());
  params.set('pageIndex', pageIndex.toString());
  params.set('pageSize', pageSize.toString());
  params.set('sortBy', sortBy);

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
 * 获取帖子的评论树
 */
export async function getCommentTree(postId: number, t: TFunction): Promise<CommentNode[]> {
  const url = `${getApiBaseUrl()}/api/v1/Comment/GetCommentTree?postId=${postId}`;
  const response = await apiFetch(url);
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
