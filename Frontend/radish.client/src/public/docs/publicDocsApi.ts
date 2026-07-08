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
import { WikiDocumentStatus, WikiDocumentVisibility } from '@/apps/wiki/types/wiki';

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

function isPublicReadableDocument(document: {
  voStatus?: number;
  voVisibility?: number;
  voIsDeleted?: boolean;
}): boolean {
  return document.voStatus === WikiDocumentStatus.Published
    && document.voVisibility === WikiDocumentVisibility.Public
    && document.voIsDeleted !== true;
}

function filterPublicReadableTree(nodes: WikiDocumentTreeNodeVo[]): WikiDocumentTreeNodeVo[] {
  return nodes.flatMap((node) => {
    const children = filterPublicReadableTree(node.voChildren || []);
    if (!isPublicReadableDocument(node)) {
      return children;
    }

    return [{
      ...node,
      voChildren: children,
    }];
  });
}

function normalizePublicWikiPage(page: WikiPageModel<WikiDocumentVo>): WikiPageModel<WikiDocumentVo> {
  const documents = (page.data || []).filter(isPublicReadableDocument);
  return {
    ...page,
    data: documents,
    dataCount: documents.length,
  };
}

export async function getPublicWikiTree(): Promise<WikiDocumentTreeNodeVo[]> {
  const tree = await ensureOk(
    apiGet<WikiDocumentTreeNodeVo[]>('/api/v1/Wiki/GetTree', { withAuth: resolveReadWithAuth() }),
    '加载公开文档目录失败'
  );

  return filterPublicReadableTree(tree);
}

export async function getPublicWikiList(query: WikiListQuery = {}): Promise<WikiPageModel<WikiDocumentVo>> {
  const page = await ensureOk(
    apiGet<WikiPageModel<WikiDocumentVo>>(buildWikiListUrl({
      ...query,
      status: WikiDocumentStatus.Published,
    }), { withAuth: resolveReadWithAuth() }),
    '加载公开文档列表失败'
  );

  return normalizePublicWikiPage(page);
}

export async function getPublicWikiDocumentBySlug(slug: string): Promise<WikiDocumentDetailVo> {
  const document = await ensureOk(
    apiGet<WikiDocumentDetailVo>(`/api/v1/Wiki/GetBySlug/${encodeURIComponent(slug)}`, { withAuth: resolveReadWithAuth() }),
    '加载公开文档详情失败'
  );

  if (!isPublicReadableDocument(document)) {
    throw new Error('公开文档不存在或暂不可读');
  }

  return document;
}
