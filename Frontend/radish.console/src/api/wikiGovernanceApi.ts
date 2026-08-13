import {
  apiFetch,
  apiGet,
  apiPost,
  apiPut,
  createApiResponseError,
  parseApiResponseWithI18n,
  type ApiResponse,
  type ParsedApiResponse,
  type ReviewWikiDraftRequest,
  type WikiAuthorDraftDetailVo,
  type WikiReviewQueueItemVo,
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
  voGovernanceVersion: number;
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

export interface WikiDocumentGovernanceEventVo {
  voId: LongId;
  voDocumentId: LongId;
  voAction: string;
  voFromStatus: number;
  voToStatus: number;
  voFromVisibility: number;
  voToVisibility: number;
  voFromAllowedRoles: string[];
  voToAllowedRoles: string[];
  voFromAllowedPermissions: string[];
  voToAllowedPermissions: string[];
  voFromIsDeleted: boolean;
  voToIsDeleted: boolean;
  voFromDocumentVersion: number;
  voToDocumentVersion: number;
  voExpectedGovernanceVersion: number;
  voResultGovernanceVersion: number;
  voSourceRevisionId?: LongId | null;
  voReason: string;
  voActorUserId: LongId;
  voActorName: string;
  voCreateTime: string;
}

export interface WikiDocumentGovernanceMutationVo {
  voDocument: WikiDocumentDetailVo;
  voEvent: WikiDocumentGovernanceEventVo;
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
  expectedGovernanceVersion: number;
  reason: string;
}

export interface WikiGovernanceActionRequest {
  expectedGovernanceVersion: number;
  reason: string;
}

export interface WikiContentGovernanceActionRequest extends WikiGovernanceActionRequest {
  expectedDocumentVersion: number;
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

export async function publishWikiDocument(id: LongId, request: WikiContentGovernanceActionRequest, t: TFunction): Promise<WikiDocumentGovernanceMutationVo> {
  return await ensureOk(apiPost<WikiDocumentGovernanceMutationVo>(`/api/v1/Wiki/Publish/${encodeURIComponent(id)}`, request, { withAuth: true }), t('documents.feedback.publishFailed'));
}

export async function unpublishWikiDocument(id: LongId, request: WikiGovernanceActionRequest, t: TFunction): Promise<WikiDocumentGovernanceMutationVo> {
  return await ensureOk(apiPost<WikiDocumentGovernanceMutationVo>(`/api/v1/Wiki/Unpublish/${encodeURIComponent(id)}`, request, { withAuth: true }), t('documents.feedback.unpublishFailed'));
}

export async function archiveWikiDocument(id: LongId, request: WikiGovernanceActionRequest, t: TFunction): Promise<WikiDocumentGovernanceMutationVo> {
  return await ensureOk(apiPost<WikiDocumentGovernanceMutationVo>(`/api/v1/Wiki/Archive/${encodeURIComponent(id)}`, request, { withAuth: true }), t('documents.feedback.archiveFailed'));
}

export async function deleteWikiDocument(id: LongId, request: WikiGovernanceActionRequest, t: TFunction): Promise<WikiDocumentGovernanceMutationVo> {
  return await ensureOk(apiPost<WikiDocumentGovernanceMutationVo>(`/api/v1/Wiki/Delete/${encodeURIComponent(id)}`, request, { withAuth: true }), t('documents.feedback.deleteFailed'));
}

export async function restoreWikiDocument(id: LongId, request: WikiGovernanceActionRequest, t: TFunction): Promise<WikiDocumentGovernanceMutationVo> {
  return await ensureOk(apiPost<WikiDocumentGovernanceMutationVo>(`/api/v1/Wiki/Restore/${encodeURIComponent(id)}`, request, { withAuth: true }), t('documents.feedback.restoreFailed'));
}

export async function updateWikiAccessPolicy(id: LongId, request: UpdateWikiAccessPolicyRequest, t: TFunction): Promise<WikiDocumentGovernanceMutationVo> {
  return await ensureOk(
    apiPut<WikiDocumentGovernanceMutationVo>(`/api/v1/Wiki/UpdateAccessPolicy/${encodeURIComponent(id)}`, request, { withAuth: true }),
    t('documents.feedback.accessUpdateFailed')
  );
}

export async function getWikiRevisionList(id: LongId, pageIndex: number, pageSize: number, t: TFunction): Promise<WikiPageModel<WikiDocumentRevisionItemVo>> {
  return await ensureOk(
    apiGet<WikiPageModel<WikiDocumentRevisionItemVo>>(`/api/v1/Wiki/GetRevisionList/${encodeURIComponent(id)}?pageIndex=${pageIndex}&pageSize=${pageSize}`, { withAuth: true }),
    t('documents.feedback.loadRevisionsFailed')
  );
}

export async function getWikiRevisionDetail(revisionId: LongId, t: TFunction): Promise<WikiDocumentRevisionDetailVo> {
  return await ensureOk(
    apiGet<WikiDocumentRevisionDetailVo>(`/api/v1/Wiki/GetRevisionDetail/${encodeURIComponent(revisionId)}`, { withAuth: true }),
    t('documents.feedback.loadRevisionDetailFailed')
  );
}

export async function rollbackWikiRevision(revisionId: LongId, request: WikiContentGovernanceActionRequest, t: TFunction): Promise<WikiDocumentGovernanceMutationVo> {
  return await ensureOk(apiPost<WikiDocumentGovernanceMutationVo>(`/api/v1/Wiki/Rollback/${encodeURIComponent(revisionId)}`, request, { withAuth: true }), t('documents.feedback.rollbackFailed'));
}

export async function getWikiReviewQueue(pageIndex: number, pageSize: number, t: TFunction): Promise<WikiPageModel<WikiReviewQueueItemVo>> {
  return await ensureOk(
    apiGet<WikiPageModel<WikiReviewQueueItemVo>>(`/api/v1/Wiki/AdminGetReviewQueue?pageIndex=${pageIndex}&pageSize=${pageSize}`, { withAuth: true }),
    t('documents.review.feedback.loadQueueFailed'),
  );
}

export async function getWikiGovernanceHistory(id: LongId, pageIndex: number, pageSize: number, t: TFunction): Promise<WikiPageModel<WikiDocumentGovernanceEventVo>> {
  return await ensureOk(
    apiGet<WikiPageModel<WikiDocumentGovernanceEventVo>>(`/api/v1/Wiki/AdminGetGovernanceHistory/${encodeURIComponent(id)}?pageIndex=${pageIndex}&pageSize=${pageSize}`, { withAuth: true }),
    t('documents.governance.feedback.loadHistoryFailed'),
  );
}

export async function getWikiReviewDraft(draftId: LongId, t: TFunction): Promise<WikiAuthorDraftDetailVo> {
  return await ensureOk(
    apiGet<WikiAuthorDraftDetailVo>(`/api/v1/Wiki/AdminGetDraftById/${encodeURIComponent(draftId)}`, { withAuth: true }),
    t('documents.review.feedback.loadDraftFailed'),
  );
}

export async function reviewWikiDraft(
  draftId: LongId,
  request: ReviewWikiDraftRequest,
  t: TFunction,
): Promise<WikiAuthorDraftDetailVo> {
  return await ensureOk(
    apiPost<WikiAuthorDraftDetailVo>(`/api/v1/Wiki/AdminReviewDraft/${encodeURIComponent(draftId)}`, request, { withAuth: true }),
    t('documents.review.feedback.actionFailed'),
  );
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
