/**
 * 用户相关的 API 调用
 * 直接使用后端 Vo 字段名，不做映射
 */

import { apiGet, configureApiClient } from '@radish/http';
import type { TFunction } from 'i18next';
import { getApiBaseUrl } from '@/config/env';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

/**
 * 用户提及选项（使用 vo 前缀，与后端 VO 保持一致）
 */
export interface UserMentionOption {
  voId: number | string;  // 后端返回long类型会被序列化为字符串
  voUserName: string;
  voDisplayName?: string | null;
  voAvatar?: string | null;
}

export interface VoPagedResult<T> {
  voItems: T[];
  voTotal: number;
  voPageIndex: number;
  voPageSize: number;
}

export type LongId = number | string;

export interface PageModel<T> {
  page: number;
  pageSize: number;
  dataCount: number;
  pageCount: number;
  data: T[];
}

export interface UserBrowseHistoryItem {
  voId: LongId;
  voTargetType: 'Post' | 'Product' | 'Wiki' | string;
  voTargetTypeDisplay: string;
  voTargetId: LongId;
  voTargetSlug?: string | null;
  voTitle: string;
  voSummary?: string | null;
  voCoverImage?: string | null;
  voRoutePath?: string | null;
  voViewCount: number;
  voLastViewTime: string;
}

export interface PublicUserProfile {
  voUserId: LongId;
  voUserName: string;
  voDisplayName?: string | null;
  voCreateTime: string;
  voAvatarUrl?: string | null;
  voAvatarThumbnailUrl?: string | null;
}

export interface PublicUserStats {
  voPostCount: number;
  voCommentCount: number;
  voTotalLikeCount: number;
  voPostLikeCount: number;
  voCommentLikeCount: number;
}

export interface PublicUserPost {
  voId: LongId;
  voTitle: string;
  voSummary?: string | null;
  voContent: string;
  voCategoryName?: string | null;
  voViewCount: number;
  voLikeCount: number;
  voCommentCount: number;
  voCreateTime: string;
}

export interface PublicUserComment {
  voId: LongId;
  voPostId: LongId;
  voContent: string;
  voLikeCount: number;
  voCreateTime: string;
  voReplyToUserName?: string | null;
  voReplyToCommentSnapshot?: string | null;
}

/**
 * 搜索用户（用于@提及功能）
 * @param keyword 搜索关键词
 * @param limit 返回结果数量限制（默认10）
 * @param t i18n 翻译函数
 * @returns 用户列表
 */
export async function searchUsersForMention(
  keyword: string,
  t: TFunction,
  limit: number = 10
): Promise<UserMentionOption[]> {
  void t;

  if (!keyword.trim()) {
    return [];
  }

  const response = await apiGet<UserMentionOption[]>(
    `/api/v1/User/SearchForMention?keyword=${encodeURIComponent(keyword)}&limit=${limit}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '搜索用户失败');
  }

  // 直接返回后端数据，不做字段映射
  return response.data;
}

export async function getMyBrowseHistory(
  pageIndex: number = 1,
  pageSize: number = 10
): Promise<VoPagedResult<UserBrowseHistoryItem>> {
  const response = await apiGet<VoPagedResult<UserBrowseHistoryItem>>(
    `/api/v1/User/GetMyBrowseHistory?pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载浏览记录失败');
  }

  return response.data;
}

export async function getPublicProfile(userId: LongId): Promise<PublicUserProfile> {
  const response = await apiGet<PublicUserProfile>(
    `/api/v1/User/GetPublicProfile?userId=${encodeURIComponent(String(userId))}`
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载公开资料失败');
  }

  return response.data;
}

export async function getPublicUserStats(userId: LongId): Promise<PublicUserStats> {
  const response = await apiGet<PublicUserStats>(
    `/api/v1/User/GetUserStats?userId=${encodeURIComponent(String(userId))}`
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载用户统计失败');
  }

  return response.data;
}

export async function getPublicUserPosts(
  userId: LongId,
  pageIndex: number = 1,
  pageSize: number = 10
): Promise<PageModel<PublicUserPost>> {
  const response = await apiGet<PageModel<PublicUserPost>>(
    `/api/v1/Post/GetUserPosts?userId=${encodeURIComponent(String(userId))}&pageIndex=${pageIndex}&pageSize=${pageSize}`
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载用户帖子失败');
  }

  return response.data;
}

export async function getPublicUserComments(
  userId: LongId,
  pageIndex: number = 1,
  pageSize: number = 10
): Promise<PageModel<PublicUserComment>> {
  const response = await apiGet<PageModel<PublicUserComment>>(
    `/api/v1/Comment/GetUserComments?userId=${encodeURIComponent(String(userId))}&pageIndex=${pageIndex}&pageSize=${pageSize}`
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '加载用户评论失败');
  }

  return response.data;
}
