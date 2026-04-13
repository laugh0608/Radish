import { apiGet, configureApiClient } from '@radish/http';
import { getApiBaseUrl } from '@/config/env';
import { tokenService } from '@/services/tokenService';
import { buildWikiListUrl } from '@/apps/wiki/wikiApp.helpers';
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

async function ensureOk<T>(request: Promise<{ ok: boolean; data?: T; message?: string }>, fallbackMessage: string): Promise<T> {
  const response = await request;
  if (!response.ok || response.data === undefined) {
    throw new Error(response.message || fallbackMessage);
  }

  return response.data;
}

function resolveReadWithAuth() {
  return Boolean(tokenService.getAccessToken());
}

export async function getPublicWikiTree(): Promise<WikiDocumentTreeNodeVo[]> {
  return await ensureOk(
    apiGet<WikiDocumentTreeNodeVo[]>('/api/v1/Wiki/GetTree', { withAuth: resolveReadWithAuth() }),
    '加载公开文档目录失败'
  );
}

export async function getPublicWikiList(query: WikiListQuery = {}): Promise<WikiPageModel<WikiDocumentVo>> {
  return await ensureOk(
    apiGet<WikiPageModel<WikiDocumentVo>>(buildWikiListUrl(query), { withAuth: resolveReadWithAuth() }),
    '加载公开文档列表失败'
  );
}

export async function getPublicWikiDocumentBySlug(slug: string): Promise<WikiDocumentDetailVo> {
  return await ensureOk(
    apiGet<WikiDocumentDetailVo>(`/api/v1/Wiki/GetBySlug/${encodeURIComponent(slug)}`, { withAuth: resolveReadWithAuth() }),
    '加载公开文档详情失败'
  );
}
