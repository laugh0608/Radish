import {
  apiGet,
  apiPost,
  configureApiClient,
  createApiResponseError,
  type ParsedApiResponse,
} from '@radish/http';
import type { TFunction } from 'i18next';
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
  voPublicId?: string | null;
  voPublicIndex?: string | number | null;
  voUserName: string;
  voDisplayName?: string | null;
  voDisplayHandle?: string | null;
  voAvatarUrl?: string | null;
  voIsMutualFollow: boolean;
  voFollowTime: string;
}

export type DistributionStreamType = 'recommend' | 'hot' | 'newest';

async function ensureOk<T>(request: Promise<ParsedApiResponse<T>>, fallbackMessage: string): Promise<T> {
  const response = await request;
  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(
      response.messageKey ? response : { ...response, message: undefined },
      fallbackMessage,
    );
  }

  return response.data;
}

export async function followUser(targetUserId: LongId, t: TFunction): Promise<UserFollowStatus> {
  return await ensureOk(apiPost<UserFollowStatus>(
    '/api/v1/UserFollow/Follow',
    { targetUserId },
    { withAuth: true }
  ), t('userFollow.error.followFailed'));
}

export async function unfollowUser(targetUserId: LongId, t: TFunction): Promise<UserFollowStatus> {
  return await ensureOk(apiPost<UserFollowStatus>(
    '/api/v1/UserFollow/Unfollow',
    { targetUserId },
    { withAuth: true }
  ), t('userFollow.error.unfollowFailed'));
}

export async function getFollowStatus(targetUserId: LongId, t: TFunction): Promise<UserFollowStatus> {
  return await ensureOk(apiGet<UserFollowStatus>(
    `/api/v1/UserFollow/GetFollowStatus?targetUserId=${encodeURIComponent(String(targetUserId))}`,
    { withAuth: true }
  ), t('userFollow.error.statusFailed'));
}

export async function getMyFollowers(pageIndex: number, pageSize: number, t: TFunction): Promise<VoPagedResult<UserFollowUser>> {
  return await ensureOk(apiGet<VoPagedResult<UserFollowUser>>(
    `/api/v1/UserFollow/GetMyFollowers?pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { withAuth: true }
  ), t('userFollow.error.followersFailed'));
}

export async function getMyFollowing(pageIndex: number, pageSize: number, t: TFunction): Promise<VoPagedResult<UserFollowUser>> {
  return await ensureOk(apiGet<VoPagedResult<UserFollowUser>>(
    `/api/v1/UserFollow/GetMyFollowing?pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { withAuth: true }
  ), t('userFollow.error.followingFailed'));
}

export async function getMyFollowingFeed(pageIndex: number, pageSize: number, t: TFunction): Promise<VoPagedResult<PostItem>> {
  return await ensureOk(apiGet<VoPagedResult<PostItem>>(
    `/api/v1/UserFollow/GetMyFollowingFeed?pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { withAuth: true }
  ), t('userFollow.error.feedFailed'));
}

export async function getMyDistributionFeed(
  streamType: DistributionStreamType,
  pageIndex: number,
  pageSize: number,
  t: TFunction,
): Promise<VoPagedResult<PostItem>> {
  return await ensureOk(apiGet<VoPagedResult<PostItem>>(
    `/api/v1/UserFollow/GetMyDistributionFeed?streamType=${streamType}&pageIndex=${pageIndex}&pageSize=${pageSize}`,
    { withAuth: true }
  ), t('userFollow.error.distributionFailed'));
}

export async function getMyFollowSummary(t: TFunction): Promise<UserFollowSummary> {
  return await ensureOk(
    apiGet<UserFollowSummary>('/api/v1/UserFollow/GetMySummary', { withAuth: true }),
    t('userFollow.error.summaryFailed'),
  );
}
