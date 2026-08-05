import { apiGet } from './client';
import type { ParsedApiResponse } from './types';
import type {
  GetPublicDiscoverFeedRequest,
  PublicDiscoverFeedVo,
} from './public-discover-contract';

export function getPublicDiscoverFeed(
  request: GetPublicDiscoverFeedRequest = {},
): Promise<ParsedApiResponse<PublicDiscoverFeedVo>> {
  const query = new URLSearchParams({
    pageSize: String(request.pageSize ?? 20),
  });
  const cursor = request.cursor?.trim();
  if (cursor) {
    query.set('cursor', cursor);
  }

  return apiGet<PublicDiscoverFeedVo>(`/api/v1/PublicDiscover/GetFeed?${query.toString()}`);
}
