import { apiGet, apiPut, createApiResponseError } from '@radish/http';
import type { TFunction } from 'i18next';

export type ChannelDiscoverVisibility = 0 | 1;

export interface ChannelDiscoverabilityVo {
  voChannelId: string;
  voName: string;
  voSlug: string;
  voDescription?: string | null;
  voIconEmoji?: string | null;
  voType: number;
  voIsEnabled: boolean;
  voIsDeleted: boolean;
  voDiscoverVisibility: ChannelDiscoverVisibility;
  voDiscoverVisibilityVersion: number;
  voCanEnableSummary: boolean;
  voEligibilityIssues: string[];
  voLastMessageTime?: string | null;
  voModifyTime?: string | null;
  voModifyBy?: string | null;
}

export interface ChannelDiscoverVisibilityEventVo {
  voId: string;
  voChannelId: string;
  voFromVisibility: ChannelDiscoverVisibility;
  voToVisibility: ChannelDiscoverVisibility;
  voExpectedVersion: number;
  voResultVersion: number;
  voReason: string;
  voActorUserId: string;
  voActorName: string;
  voCreateTime: string;
}

export interface ChannelDiscoverabilityPageModel<T> {
  page: number;
  pageSize: number;
  dataCount: number;
  pageCount: number;
  data: T[];
}

export type ChannelDiscoverabilityListPageModel = ChannelDiscoverabilityPageModel<ChannelDiscoverabilityVo>;
export type ChannelDiscoverVisibilityEventPageModel = ChannelDiscoverabilityPageModel<ChannelDiscoverVisibilityEventVo>;

export interface ChannelDiscoverVisibilityMutationVo {
  voChannel: ChannelDiscoverabilityVo;
  voChanged: boolean;
}

function translateFallback(t: TFunction | undefined, key: string, fallback: string): string {
  return t ? t(key) : fallback;
}

export async function getChannelDiscoverabilityPage(params: {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  discoverVisibility?: ChannelDiscoverVisibility;
  isEnabled?: boolean;
  includeDeleted?: boolean;
}, t?: TFunction): Promise<ChannelDiscoverabilityListPageModel> {
  const searchParams = new URLSearchParams();
  searchParams.set('pageIndex', String(params.pageIndex ?? 1));
  searchParams.set('pageSize', String(params.pageSize ?? 20));
  if (params.keyword?.trim()) {
    searchParams.set('keyword', params.keyword.trim());
  }
  if (params.discoverVisibility !== undefined) {
    searchParams.set('discoverVisibility', String(params.discoverVisibility));
  }
  if (params.isEnabled !== undefined) {
    searchParams.set('isEnabled', String(params.isEnabled));
  }
  if (params.includeDeleted !== undefined) {
    searchParams.set('includeDeleted', String(params.includeDeleted));
  }

  const response = await apiGet<ChannelDiscoverabilityListPageModel>(
    `/api/v1/ChannelDiscoverability/GetPage?${searchParams.toString()}`,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(
      response,
      translateFallback(t, 'channelDiscoverability.feedback.loadFailed', '获取频道公开摘要列表失败'),
    );
  }
  return response.data;
}

export async function getChannelDiscoverabilityById(
  channelId: string,
  t?: TFunction,
): Promise<ChannelDiscoverabilityVo> {
  const searchParams = new URLSearchParams({ channelId });
  const response = await apiGet<ChannelDiscoverabilityVo>(
    `/api/v1/ChannelDiscoverability/GetById?${searchParams.toString()}`,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(
      response,
      translateFallback(t, 'channelDiscoverability.feedback.targetRefreshFailed', '刷新频道状态失败'),
    );
  }
  return response.data;
}

export async function getChannelDiscoverabilityHistory(params: {
  channelId: string;
  pageIndex?: number;
  pageSize?: number;
}, t?: TFunction): Promise<ChannelDiscoverVisibilityEventPageModel> {
  const searchParams = new URLSearchParams({
    channelId: params.channelId,
    pageIndex: String(params.pageIndex ?? 1),
    pageSize: String(params.pageSize ?? 20),
  });
  const response = await apiGet<ChannelDiscoverVisibilityEventPageModel>(
    `/api/v1/ChannelDiscoverability/GetHistory?${searchParams.toString()}`,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(
      response,
      translateFallback(t, 'channelDiscoverability.feedback.historyFailed', '获取频道公开摘要历史失败'),
    );
  }
  return response.data;
}

export async function updateChannelDiscoverVisibility(
  channelId: string,
  request: {
    discoverVisibility: ChannelDiscoverVisibility;
    expectedVersion: number;
    reason: string;
  },
  t?: TFunction,
): Promise<ChannelDiscoverVisibilityMutationVo> {
  const response = await apiPut<ChannelDiscoverVisibilityMutationVo>(
    `/api/v1/ChannelDiscoverability/UpdateVisibility/${encodeURIComponent(channelId)}`,
    request,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(
      response,
      translateFallback(t, 'channelDiscoverability.feedback.updateFailed', '更新频道公开摘要资格失败'),
    );
  }
  return response.data;
}
