import { apiGet, apiPost, configureApiClient } from '@radish/http';
import { getApiBaseUrl } from '@/config/env';

const STICKER_READ_TIMEOUT_MS = 30_000;

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

export interface StickerVo {
  voId: number;
  voGroupId: number;
  voCode: string;
  voName: string;
  voImageUrl: string;
  voThumbnailUrl?: string | null;
  voIsAnimated: boolean;
  voAllowInline: boolean;
  voUseCount: number;
  voSort: number;
}

export interface StickerGroupVo {
  voId: number;
  voCode: string;
  voName: string;
  voDescription?: string | null;
  voCoverImageUrl?: string | null;
  voSort: number;
  voStickers: StickerVo[];
}

export interface RecordStickerUseRequest {
  emojiType: 'unicode' | 'sticker';
  emojiValue: string;
}

let groupsCache: StickerGroupVo[] | null = null;
let groupsRequestPromise: Promise<StickerGroupVo[]> | null = null;

export function clearStickerGroupsCache(): void {
  groupsCache = null;
  groupsRequestPromise = null;
}

export async function getStickerGroups(forceRefresh: boolean = false): Promise<StickerGroupVo[]> {
  if (forceRefresh) {
    clearStickerGroupsCache();
  }

  if (groupsCache) {
    return groupsCache;
  }

  if (groupsRequestPromise) {
    return groupsRequestPromise;
  }

  groupsRequestPromise = (async () => {
    const response = await apiGet<StickerGroupVo[]>('/api/v1/Sticker/GetGroups', {
      timeout: STICKER_READ_TIMEOUT_MS,
    });
    if (!response.ok || !response.data) {
      throw new Error(response.message || '加载表情包分组失败');
    }

    groupsCache = response.data;
    return response.data;
  })();

  try {
    return await groupsRequestPromise;
  } finally {
    groupsRequestPromise = null;
  }
}

export async function recordStickerUse(request: RecordStickerUseRequest): Promise<boolean> {
  const response = await apiPost<boolean>('/api/v1/Sticker/RecordUse', request, {
    withAuth: true,
  });
  if (!response.ok) {
    throw new Error(response.message || '记录表情使用失败');
  }

  return response.data ?? true;
}
