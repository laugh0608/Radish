import {
  apiFetch,
  apiGet,
  apiPost,
  apiPut,
  configureApiClient,
  parseApiResponse,
  type ApiResponse,
} from '@radish/http';
import { getApiBaseUrl } from '@/config/env';
import type {
  CreateWikiDocumentRequest,
  ImportWikiMarkdownRequest,
  UpdateWikiDocumentRequest,
  WikiDocumentDetailVo,
  WikiDocumentRevisionDetailVo,
  WikiDocumentRevisionItemVo,
  WikiDocumentTreeNodeVo,
  WikiDocumentVo,
  WikiListQuery,
  WikiPageModel,
} from '../types/wiki';

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

function buildListUrl(query: WikiListQuery = {}): string {
  const params = new URLSearchParams();

  params.set('pageIndex', String(query.pageIndex ?? 1));
  params.set('pageSize', String(query.pageSize ?? 100));

  if (query.keyword?.trim()) {
    params.set('keyword', query.keyword.trim());
  }

  if (typeof query.status === 'number') {
    params.set('status', String(query.status));
  }

  if (typeof query.parentId === 'number') {
    params.set('parentId', String(query.parentId));
  }

  if (query.includeDeleted) {
    params.set('includeDeleted', 'true');
  }

  if (query.deletedOnly) {
    params.set('deletedOnly', 'true');
  }

  return `/api/v1/Wiki/GetList?${params.toString()}`;
}

async function ensureOk<T>(request: Promise<{ ok: boolean; data?: T; message?: string }>, fallbackMessage: string): Promise<T> {
  const response = await request;
  if (!response.ok || response.data === undefined) {
    throw new Error(response.message || fallbackMessage);
  }

  return response.data;
}

export async function getWikiTree(): Promise<WikiDocumentTreeNodeVo[]> {
  return await ensureOk(apiGet<WikiDocumentTreeNodeVo[]>('/api/v1/Wiki/GetTree', { withAuth: true }), '加载文档目录失败');
}

export async function getWikiList(query: WikiListQuery = {}): Promise<WikiPageModel<WikiDocumentVo>> {
  return await ensureOk(apiGet<WikiPageModel<WikiDocumentVo>>(buildListUrl(query), { withAuth: true }), '加载文档列表失败');
}

export async function getWikiDocumentById(id: number, includeDeleted: boolean = false): Promise<WikiDocumentDetailVo> {
  const suffix = includeDeleted ? '?includeDeleted=true' : '';
  return await ensureOk(apiGet<WikiDocumentDetailVo>(`/api/v1/Wiki/GetById/${id}${suffix}`, { withAuth: true }), '加载文档详情失败');
}

export async function getWikiDocumentBySlug(slug: string): Promise<WikiDocumentDetailVo> {
  return await ensureOk(apiGet<WikiDocumentDetailVo>(`/api/v1/Wiki/GetBySlug/${encodeURIComponent(slug)}`, { withAuth: true }), '加载文档详情失败');
}

export async function createWikiDocument(request: CreateWikiDocumentRequest): Promise<number> {
  return await ensureOk(apiPost<number>('/api/v1/Wiki/Create', request, { withAuth: true }), '创建文档失败');
}

export async function updateWikiDocument(id: number, request: UpdateWikiDocumentRequest): Promise<boolean> {
  return await ensureOk(apiPut<boolean>(`/api/v1/Wiki/Update/${id}`, request, { withAuth: true }), '更新文档失败');
}

export async function deleteWikiDocument(id: number): Promise<boolean> {
  return await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Delete/${id}`, undefined, { withAuth: true }), '删除文档失败');
}

export async function restoreWikiDocument(id: number): Promise<boolean> {
  return await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Restore/${id}`, undefined, { withAuth: true }), '恢复文档失败');
}

export async function publishWikiDocument(id: number): Promise<boolean> {
  return await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Publish/${id}`, undefined, { withAuth: true }), '发布文档失败');
}

export async function unpublishWikiDocument(id: number): Promise<boolean> {
  return await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Unpublish/${id}`, undefined, { withAuth: true }), '撤下文档失败');
}

export async function archiveWikiDocument(id: number): Promise<boolean> {
  return await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Archive/${id}`, undefined, { withAuth: true }), '归档文档失败');
}

export async function getWikiRevisionList(id: number): Promise<WikiDocumentRevisionItemVo[]> {
  return await ensureOk(apiGet<WikiDocumentRevisionItemVo[]>(`/api/v1/Wiki/GetRevisionList/${id}`, { withAuth: true }), '加载版本历史失败');
}

export async function getWikiRevisionDetail(revisionId: number): Promise<WikiDocumentRevisionDetailVo> {
  return await ensureOk(apiGet<WikiDocumentRevisionDetailVo>(`/api/v1/Wiki/GetRevisionDetail/${revisionId}`, { withAuth: true }), '加载版本详情失败');
}

export async function rollbackWikiRevision(revisionId: number): Promise<boolean> {
  return await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Rollback/${revisionId}`, undefined, { withAuth: true }), '回滚版本失败');
}

export async function importWikiMarkdown(request: ImportWikiMarkdownRequest): Promise<number> {
  const formData = new FormData();
  formData.append('file', request.file);

  if (request.slug?.trim()) {
    formData.append('slug', request.slug.trim());
  }

  if (request.summary?.trim()) {
    formData.append('summary', request.summary.trim());
  }

  if (typeof request.parentId === 'number') {
    formData.append('parentId', String(request.parentId));
  }

  formData.append('sort', String(request.sort ?? 0));
  formData.append('publishAfterImport', String(Boolean(request.publishAfterImport)));

  const response = await apiFetch('/api/v1/Wiki/ImportMarkdown', {
    method: 'POST',
    withAuth: true,
    body: formData,
  });

  const json = await response.json() as ApiResponse<number>;
  const parsed = parseApiResponse<number>(json);

  if (!parsed.ok || parsed.data === undefined) {
    throw new Error(parsed.message || '导入 Markdown 失败');
  }

  return parsed.data;
}

function getFileNameFromDisposition(contentDisposition: string | null, fallbackFileName: string): string {
  if (!contentDisposition) {
    return fallbackFileName;
  }

  const utf8Match = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i);
  if (utf8Match?.[1]) {
    return decodeURIComponent(utf8Match[1]);
  }

  const plainMatch = contentDisposition.match(/filename="?([^";]+)"?/i);
  if (plainMatch?.[1]) {
    return plainMatch[1];
  }

  return fallbackFileName;
}

export async function downloadWikiMarkdown(id: number): Promise<{ blob: Blob; fileName: string }> {
  const response = await apiFetch(`/api/v1/Wiki/ExportMarkdown/${id}`, {
    method: 'GET',
    withAuth: true,
    headers: {
      Accept: 'text/markdown, application/json',
    },
  });

  if (!response.ok) {
    let errorMessage = '导出 Markdown 失败';

    try {
      const json = await response.json() as ApiResponse<string>;
      const parsed = parseApiResponse<string>(json);
      errorMessage = parsed.message || errorMessage;
    } catch {
      const text = await response.text().catch(() => '');
      if (text) {
        errorMessage = text;
      }
    }

    throw new Error(errorMessage);
  }

  const blob = await response.blob();
  const fileName = getFileNameFromDisposition(response.headers.get('content-disposition'), `wiki-${id}.md`);
  return { blob, fileName };
}
