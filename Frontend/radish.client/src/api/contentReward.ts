import {
  apiGet,
  apiPost,
  configureApiClient,
  createApiResponseError,
  type ContentRewardMutationVo,
  type ContentRewardTargetPageVo,
  type ContentRewardTargetRequest,
  type ContentRewardTargetStateVo,
  type CreateContentRewardRequest,
} from '@radish/http';
import type { TFunction } from 'i18next';
import { getApiBaseUrl } from '@/config/env';
import { tokenService } from '@/services/tokenService';

const CONTENT_REWARD_READ_TIMEOUT_MS = 60_000;
const TARGET_STATE_BATCH_SIZE = 100;

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

export async function createContentReward(
  request: CreateContentRewardRequest,
  t: TFunction,
): Promise<ContentRewardMutationVo> {
  const response = await apiPost<ContentRewardMutationVo>(
    '/api/v1/ContentReward/Create',
    request,
    { withAuth: true },
  );

  if (!response.ok || !response.data) {
    throw createApiResponseError(response, t('forum.contentReward.error.generic'));
  }

  return response.data;
}

export async function getContentRewardTargetRewards(
  target: ContentRewardTargetRequest,
  pageIndex: number,
  pageSize: number,
  t: TFunction,
): Promise<ContentRewardTargetPageVo> {
  const params = new URLSearchParams({
    targetType: target.targetType,
    targetId: String(target.targetId),
    pageIndex: String(pageIndex),
    pageSize: String(pageSize),
  });
  const response = await apiGet<ContentRewardTargetPageVo>(
    `/api/v1/ContentReward/GetTargetRewards?${params.toString()}`,
    {
      withAuth: Boolean(tokenService.getAccessToken()),
      timeout: CONTENT_REWARD_READ_TIMEOUT_MS,
    },
  );

  if (!response.ok || !response.data) {
    throw createApiResponseError(response, t('forum.contentReward.error.loadRecords'));
  }

  return response.data;
}

export async function getContentRewardTargetStates(
  targets: ContentRewardTargetRequest[],
  t: TFunction,
): Promise<ContentRewardTargetStateVo[]> {
  if (targets.length === 0) {
    return [];
  }

  const deduplicated = [
    ...new Map(
      targets.map((target) => [
        `${target.targetType}:${String(target.targetId)}`,
        target,
      ]),
    ).values(),
  ];
  const result: ContentRewardTargetStateVo[] = [];

  for (let index = 0; index < deduplicated.length; index += TARGET_STATE_BATCH_SIZE) {
    const batch = deduplicated.slice(index, index + TARGET_STATE_BATCH_SIZE);
    const response = await apiPost<ContentRewardTargetStateVo[]>(
      '/api/v1/ContentReward/GetTargetStates',
      { targets: batch },
      {
        withAuth: Boolean(tokenService.getAccessToken()),
        timeout: CONTENT_REWARD_READ_TIMEOUT_MS,
      },
    );

    if (!response.ok || !response.data) {
      throw createApiResponseError(response, t('forum.contentReward.error.loadState'));
    }

    result.push(...response.data);
  }

  return result;
}
