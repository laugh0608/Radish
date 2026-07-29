import {
  configureApiClient,
  createApiResponseError,
  getMyPostBookmarks,
  removePostBookmark,
  setPostBookmarkState,
  type PostBookmarkRemoveVo,
  type PostBookmarkStateVo,
  type SetPostBookmarkStateRequest,
  type UserPostBookmarkPageVo,
  type UserPostBookmarkVo,
} from '@radish/http';
import type { TFunction } from 'i18next';
import { getApiBaseUrl } from '@/config/env';

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

export type {
  PostBookmarkRemoveVo,
  PostBookmarkStateVo,
  UserPostBookmarkPageVo,
  UserPostBookmarkVo,
};

export async function setMyPostBookmarkState(
  request: SetPostBookmarkStateRequest,
  t: TFunction,
): Promise<PostBookmarkStateVo> {
  const response = await setPostBookmarkState(request);
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, t('forum.postDetail.bookmark.error'));
  }

  return response.data;
}

export async function getMyPostBookmarkPage(
  pageIndex: number,
  pageSize: number,
  t: TFunction,
): Promise<UserPostBookmarkPageVo> {
  const response = await getMyPostBookmarks({ pageIndex, pageSize });
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, t('me.bookmarks.loadFailed'));
  }

  return response.data;
}

export async function removeMyPostBookmark(
  bookmarkIdentifier: string,
  t: TFunction,
): Promise<PostBookmarkRemoveVo> {
  const response = await removePostBookmark({ bookmarkIdentifier });
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, t('me.bookmarks.removeFailed'));
  }

  return response.data;
}
