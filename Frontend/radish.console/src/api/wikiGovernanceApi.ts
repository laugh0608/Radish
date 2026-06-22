import {
  apiFetch,
  apiGet,
  apiPost,
  apiPut,
  parseApiResponse,
  type ApiResponse,
} from '@radish/http';

export type LongId = string;

export interface WikiDocumentVo {
  voId: LongId;
  voTitle: string;
  voSlug: string;
  voSummary?: string | null;
  voCoverAttachmentId?: LongId | null;
  voParentId?: LongId | null;
  voSort: number;
  voStatus: number;
  voVisibility: number;
  voAllowedRoles: string[];
  voAllowedPermissions: string[];
  voSourceType: string;
  voSourcePath?: string | null;
  voVersion: number;
  voPublishedAt?: string | null;
  voIsDeleted: boolean;
  voDeletedAt?: string | null;
  voDeletedBy?: string | null;
  voCreateTime: string;
  voModifyTime?: string | null;
}

export interface WikiDocumentDetailVo extends WikiDocumentVo {
  voMarkdownContent: string;
}

export interface WikiDocumentRevisionItemVo {
  voId: LongId;
  voDocumentId: LongId;
  voVersion: number;
  voTitle: string;
  voChangeSummary?: string | null;
  voSourceType: string;
  voCreateTime: string;
  voCreateBy: string;
  voIsCurrent: boolean;
}

export interface WikiDocumentRevisionDetailVo extends WikiDocumentRevisionItemVo {
  voMarkdownContent: string;
  voCreateId: LongId;
}

export interface WikiPageModel<T> {
  page: number;
  pageSize: number;
  dataCount: number;
  pageCount: number;
  data: T[];
}

export interface WikiGovernanceQuery {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  status?: number;
  visibility?: number;
  sourceType?: string;
  includeDeleted?: boolean;
  deletedOnly?: boolean;
}

export interface UpdateWikiAccessPolicyRequest {
  visibility: number;
  allowedRoles?: string[];
  allowedPermissions?: string[];
}

function appendOptionalParam(searchParams: URLSearchParams, key: string, value: string | number | boolean | undefined) {
  if (value === undefined || value === '') {
    return;
  }

  searchParams.set(key, String(value));
}

async function ensureOk<T>(request: Promise<{ ok: boolean; data?: T; message?: string }>, fallbackMessage: string): Promise<T> {
  const response = await request;
  if (!response.ok || response.data === undefined) {
    throw new Error(response.message || fallbackMessage);
  }

  return response.data;
}

export async function getWikiGovernancePage(query: WikiGovernanceQuery): Promise<WikiPageModel<WikiDocumentVo>> {
  const searchParams = new URLSearchParams();
  searchParams.set('pageIndex', String(query.pageIndex ?? 1));
  searchParams.set('pageSize', String(query.pageSize ?? 20));
  appendOptionalParam(searchParams, 'keyword', query.keyword?.trim());
  appendOptionalParam(searchParams, 'status', query.status);
  appendOptionalParam(searchParams, 'visibility', query.visibility);
  appendOptionalParam(searchParams, 'sourceType', query.sourceType);
  appendOptionalParam(searchParams, 'includeDeleted', query.includeDeleted);
  appendOptionalParam(searchParams, 'deletedOnly', query.deletedOnly);

  return await ensureOk(
    apiGet<WikiPageModel<WikiDocumentVo>>(`/api/v1/Wiki/AdminGetList?${searchParams.toString()}`, { withAuth: true }),
    '加载文档治理列表失败'
  );
}

export async function getWikiGovernanceDetail(id: LongId, includeDeleted = true): Promise<WikiDocumentDetailVo> {
  return await ensureOk(
    apiGet<WikiDocumentDetailVo>(
      `/api/v1/Wiki/AdminGetById/${encodeURIComponent(id)}?includeDeleted=${includeDeleted}`,
      { withAuth: true }
    ),
    '加载文档详情失败'
  );
}

export async function publishWikiDocument(id: LongId): Promise<void> {
  await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Publish/${encodeURIComponent(id)}`, undefined, { withAuth: true }), '发布文档失败');
}

export async function unpublishWikiDocument(id: LongId): Promise<void> {
  await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Unpublish/${encodeURIComponent(id)}`, undefined, { withAuth: true }), '下架文档失败');
}

export async function archiveWikiDocument(id: LongId): Promise<void> {
  await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Archive/${encodeURIComponent(id)}`, undefined, { withAuth: true }), '归档文档失败');
}

export async function deleteWikiDocument(id: LongId): Promise<void> {
  await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Delete/${encodeURIComponent(id)}`, undefined, { withAuth: true }), '删除文档失败');
}

export async function restoreWikiDocument(id: LongId): Promise<void> {
  await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Restore/${encodeURIComponent(id)}`, undefined, { withAuth: true }), '恢复文档失败');
}

export async function updateWikiAccessPolicy(id: LongId, request: UpdateWikiAccessPolicyRequest): Promise<void> {
  await ensureOk(
    apiPut<boolean>(`/api/v1/Wiki/UpdateAccessPolicy/${encodeURIComponent(id)}`, request, { withAuth: true }),
    '更新文档访问策略失败'
  );
}

export async function getWikiRevisionList(id: LongId): Promise<WikiDocumentRevisionItemVo[]> {
  return await ensureOk(
    apiGet<WikiDocumentRevisionItemVo[]>(`/api/v1/Wiki/GetRevisionList/${encodeURIComponent(id)}`, { withAuth: true }),
    '加载版本列表失败'
  );
}

export async function getWikiRevisionDetail(revisionId: LongId): Promise<WikiDocumentRevisionDetailVo> {
  return await ensureOk(
    apiGet<WikiDocumentRevisionDetailVo>(`/api/v1/Wiki/GetRevisionDetail/${encodeURIComponent(revisionId)}`, { withAuth: true }),
    '加载版本详情失败'
  );
}

export async function rollbackWikiRevision(revisionId: LongId): Promise<void> {
  await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Rollback/${encodeURIComponent(revisionId)}`, undefined, { withAuth: true }), '回滚版本失败');
}

export async function importWikiMarkdown(file: File): Promise<LongId> {
  const formData = new FormData();
  formData.append('file', file);
  formData.append('publishAfterImport', 'false');

  const response = await apiFetch('/api/v1/Wiki/ImportMarkdown', {
    method: 'POST',
    withAuth: true,
    body: formData,
  });
  const json = await response.json() as ApiResponse<LongId>;
  const parsed = parseApiResponse<LongId>(json);

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

export async function exportWikiMarkdown(id: LongId): Promise<{ blob: Blob; fileName: string }> {
  const response = await apiFetch(`/api/v1/Wiki/ExportMarkdown/${encodeURIComponent(id)}`, {
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
  return {
    blob,
    fileName: getFileNameFromDisposition(response.headers.get('content-disposition'), `document-${id}.md`),
  };
}
