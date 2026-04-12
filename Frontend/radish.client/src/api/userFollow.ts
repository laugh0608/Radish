import { apiGet, apiPost, configureApiClient } from '@radish/http';
import { getApiBaseUrl } from '@/config/env';
import type { PostItem, VoPagedResult } from '@/types/forum';
import type { LongId } from './user';

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

export interface UserFollowStatus {
  voTargetUserId: LongId;
  voIsFollowing: boolean;
  voIsFollower: boolean;
  voFollowerCount: number;
  voFollowingCount: number;
}

export interface UserFollowSummary {
  voFollowerCount: number;
  voFollowingCount: number;
}

export interface UserFollowUser {
  voUserId: LongId;
  voUserName: string;
  voDisplayName?: string | null;
  voAvatarUrl?: string | null;
  voIsMutualFollow: boolean;
  voFollowTime: string;
}

export type DistributionStreamType = 'recommend' | 'hot' | 'newest';

export async function followUser(targetUserId: LongId): Promise<UserFollowStatus> {
  const response = await apiPost<UserFollowStatus>(
    '/api/v1/UserFollow/Follow',
    { targetUserId },
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '关注失败');
  }

  return response.data;
}

export async function unfollowUser(targetUserId: LongId): Promise<UserFollowStatus> {
  const response = await apiPost<UserFollowStatus>(
    '/api/v1/UserFollow/Unfollow',
    { targetUserId },
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '取消关注失败');
  }

  return response.data;
}

export async function getFollowStatus(targetUserId: LongId): Promise<UserFollowStatus> {
  const response = await apiGet<UserFollowStatus>(
    `/api/v1/UserFollow/GetFollowStatus?targetUserId=${encodeURIComponent(String(targetUserId))}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取关注状态失败');
  }

  return response.data;
}

export async function getMyFollowers(pageIndex: number, pageSize: number): Promise<VoPagedResult<UserFollowUser>> {
  const response = await apiGet<VoPagedResult<UserFollowUser>>(
    `/api/v1/UserFollow/GetMyFollowers?pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取粉丝列表失败');
  }

  return response.data;
}

export async function getMyFollowing(pageIndex: number, pageSize: number): Promise<VoPagedResult<UserFollowUser>> {
  const response = await apiGet<VoPagedResult<UserFollowUser>>(
    `/api/v1/UserFollow/GetMyFollowing?pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取关注列表失败');
  }

  return response.data;
}

export async function getMyFollowingFeed(pageIndex: number, pageSize: number): Promise<VoPagedResult<PostItem>> {
  const response = await apiGet<VoPagedResult<PostItem>>(
    `/api/v1/UserFollow/GetMyFollowingFeed?pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取关系链动态失败');
  }

  return response.data;
}

export async function getMyDistributionFeed(
  streamType: DistributionStreamType,
  pageIndex: number,
  pageSize: number
): Promise<VoPagedResult<PostItem>> {
  const response = await apiGet<VoPagedResult<PostItem>>(
    `/api/v1/UserFollow/GetMyDistributionFeed?streamType=${streamType}&pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取分发流失败');
  }

  return response.data;
}

export async function getMyFollowSummary(): Promise<UserFollowSummary> {
  const response = await apiGet<UserFollowSummary>('/api/v1/UserFollow/GetMySummary', { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取关系链汇总失败');
  }

  return response.data;
}
