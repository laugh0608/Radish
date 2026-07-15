import {
  apiFetch,
  apiGet,
  apiPost,
  apiPut,
  createApiResponseError,
  parseApiResponseWithI18n,
  type ApiResponse,
  type ParsedApiResponse,
} from '@radish/http';
import type { TFunction } from 'i18next';

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

async function ensureOk<T>(request: Promise<ParsedApiResponse<T>>, fallbackMessage: string): Promise<T> {
  const response = await request;
  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(
      response.messageKey ? response : { ...response, message: undefined },
      fallbackMessage,
    );
  }

  return response.data;
}

export async function getWikiGovernancePage(query: WikiGovernanceQuery, t: TFunction): Promise<WikiPageModel<WikiDocumentVo>> {
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
    t('documents.feedback.loadListFailed')
  );
}

export async function getWikiGovernanceDetail(id: LongId, includeDeleted: boolean, t: TFunction): Promise<WikiDocumentDetailVo> {
  return await ensureOk(
    apiGet<WikiDocumentDetailVo>(
      `/api/v1/Wiki/AdminGetById/${encodeURIComponent(id)}?includeDeleted=${includeDeleted}`,
      { withAuth: true }
    ),
    t('documents.feedback.loadDetailFailed')
  );
}

export async function publishWikiDocument(id: LongId, t: TFunction): Promise<void> {
  await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Publish/${encodeURIComponent(id)}`, undefined, { withAuth: true }), t('documents.feedback.publishFailed'));
}

export async function unpublishWikiDocument(id: LongId, t: TFunction): Promise<void> {
  await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Unpublish/${encodeURIComponent(id)}`, undefined, { withAuth: true }), t('documents.feedback.unpublishFailed'));
}

export async function archiveWikiDocument(id: LongId, t: TFunction): Promise<void> {
  await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Archive/${encodeURIComponent(id)}`, undefined, { withAuth: true }), t('documents.feedback.archiveFailed'));
}

export async function deleteWikiDocument(id: LongId, t: TFunction): Promise<void> {
  await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Delete/${encodeURIComponent(id)}`, undefined, { withAuth: true }), t('documents.feedback.deleteFailed'));
}

export async function restoreWikiDocument(id: LongId, t: TFunction): Promise<void> {
  await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Restore/${encodeURIComponent(id)}`, undefined, { withAuth: true }), t('documents.feedback.restoreFailed'));
}

export async function updateWikiAccessPolicy(id: LongId, request: UpdateWikiAccessPolicyRequest, t: TFunction): Promise<void> {
  await ensureOk(
    apiPut<boolean>(`/api/v1/Wiki/UpdateAccessPolicy/${encodeURIComponent(id)}`, request, { withAuth: true }),
    t('documents.feedback.accessUpdateFailed')
  );
}

export async function getWikiRevisionList(id: LongId, t: TFunction): Promise<WikiDocumentRevisionItemVo[]> {
  return await ensureOk(
    apiGet<WikiDocumentRevisionItemVo[]>(`/api/v1/Wiki/GetRevisionList/${encodeURIComponent(id)}`, { withAuth: true }),
    t('documents.feedback.loadRevisionsFailed')
  );
}

export async function getWikiRevisionDetail(revisionId: LongId, t: TFunction): Promise<WikiDocumentRevisionDetailVo> {
  return await ensureOk(
    apiGet<WikiDocumentRevisionDetailVo>(`/api/v1/Wiki/GetRevisionDetail/${encodeURIComponent(revisionId)}`, { withAuth: true }),
    t('documents.feedback.loadRevisionDetailFailed')
  );
}

export async function rollbackWikiRevision(revisionId: LongId, t: TFunction): Promise<void> {
  await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Rollback/${encodeURIComponent(revisionId)}`, undefined, { withAuth: true }), t('documents.feedback.rollbackFailed'));
}

export async function importWikiMarkdown(file: File, t: TFunction): Promise<LongId> {
  const formData = new FormData();
  formData.append('file', file);
  formData.append('publishAfterImport', 'false');

  const response = await apiFetch('/api/v1/Wiki/ImportMarkdown', {
    method: 'POST',
    withAuth: true,
    body: formData,
  });
  const json = await response.json() as ApiResponse<LongId>;
  const parsed = parseApiResponseWithI18n<LongId>(json, (key) => t(key));

  if (!parsed.ok || parsed.data === undefined) {
    throw createApiResponseError(parsed, t('documents.feedback.importFailed'));
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

export async function exportWikiMarkdown(id: LongId, t: TFunction): Promise<{ blob: Blob; fileName: string }> {
  const response = await apiFetch(`/api/v1/Wiki/ExportMarkdown/${encodeURIComponent(id)}`, {
    method: 'GET',
    withAuth: true,
    headers: {
      Accept: 'text/markdown, application/json',
    },
  });

  if (!response.ok) {
    let errorMessage = t('documents.feedback.exportFailed');
    try {
      const json = await response.json() as ApiResponse<string>;
      const parsed = parseApiResponseWithI18n<string>(json, (key) => t(key));
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
