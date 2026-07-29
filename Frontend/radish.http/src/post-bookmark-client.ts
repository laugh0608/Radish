import { apiGet, apiPost } from './client';
import type { ParsedApiResponse } from './types';
import type {
  GetMyPostBookmarksRequest,
  PostBookmarkRemoveVo,
  PostBookmarkStateVo,
  RemovePostBookmarkRequest,
  SetPostBookmarkStateRequest,
  UserPostBookmarkPageVo,
} from './post-bookmark-contract';

const postBookmarkApiBase = '/api/v1/PostBookmark';
const authenticated = { withAuth: true } as const;

export function setPostBookmarkState(
  request: SetPostBookmarkStateRequest,
): Promise<ParsedApiResponse<PostBookmarkStateVo>> {
  return apiPost<PostBookmarkStateVo>(
    `${postBookmarkApiBase}/SetState`,
    request,
    authenticated,
  );
}

export function getMyPostBookmarks(
  request: GetMyPostBookmarksRequest = {},
): Promise<ParsedApiResponse<UserPostBookmarkPageVo>> {
  const query = new URLSearchParams({
    pageIndex: String(request.pageIndex ?? 1),
    pageSize: String(request.pageSize ?? 20),
  });
  return apiGet<UserPostBookmarkPageVo>(
    `${postBookmarkApiBase}/GetMine?${query}`,
    authenticated,
  );
}

export function removePostBookmark(
  request: RemovePostBookmarkRequest,
): Promise<ParsedApiResponse<PostBookmarkRemoveVo>> {
  return apiPost<PostBookmarkRemoveVo>(
    `${postBookmarkApiBase}/Remove`,
    request,
    authenticated,
  );
}
