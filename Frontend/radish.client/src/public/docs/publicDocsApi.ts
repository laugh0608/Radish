import { apiGet, configureApiClient, createApiResponseError } from '@radish/http';
import type { ParsedApiResponse } from '@radish/http';
import { getApiBaseUrl } from '@/config/env';
import type {
  WikiDocumentDetailVo,
  WikiDocumentTreeNodeVo,
  WikiDocumentVo,
  WikiListQuery,
  WikiPageModel,
} from '@/apps/wiki/types/wiki';

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

async function ensureOk<T>(request: Promise<ParsedApiResponse<T>>, fallbackMessage: string): Promise<T> {
  const response = await request;
  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(response, fallbackMessage);
  }

  return response.data;
}

function buildPublicWikiListUrl(query: WikiListQuery): string {
  const params = new URLSearchParams();
  params.set('pageIndex', String(query.pageIndex ?? 1));
  params.set('pageSize', String(query.pageSize ?? 100));

  if (query.keyword?.trim()) {
    params.set('keyword', query.keyword.trim());
  }

  if (query.parentId != null) {
    params.set('parentId', String(query.parentId));
  }

  return `/api/v1/Wiki/PublicGetList?${params.toString()}`;
}

export async function getPublicWikiTree(): Promise<WikiDocumentTreeNodeVo[]> {
  return await ensureOk(
    apiGet<WikiDocumentTreeNodeVo[]>('/api/v1/Wiki/PublicGetTree', { withAuth: false }),
    '加载公开文档目录失败'
  );
}

export async function getPublicWikiList(query: WikiListQuery = {}): Promise<WikiPageModel<WikiDocumentVo>> {
  return await ensureOk(
    apiGet<WikiPageModel<WikiDocumentVo>>(buildPublicWikiListUrl(query), { withAuth: false }),
    '加载公开文档列表失败'
  );
}

export async function getPublicWikiDocumentBySlug(slug: string): Promise<WikiDocumentDetailVo> {
  return await ensureOk(
    apiGet<WikiDocumentDetailVo>(`/api/v1/Wiki/PublicGetBySlug/${encodeURIComponent(slug)}`, { withAuth: false }),
    '加载公开文档详情失败'
  );
}
