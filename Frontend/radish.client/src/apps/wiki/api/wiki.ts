import {
  apiFetch,
  apiGet,
  apiPost,
  apiPut,
  configureApiClient,
  createApiResponseError,
  isApiResponseNotFoundError,
  parseApiResponseWithI18n,
  type ApiResponse,
  type CreateWikiAuthorDraftRequest,
  type ParsedApiResponse,
  type SaveWikiAuthorDraftRequest,
  type SubmitWikiDraftRequest,
  type WikiAuthorDocumentVo,
  type WikiAuthorDraftDetailVo,
  type WikiDocumentCollaboratorVo,
} from '@radish/http';
import type { TFunction } from 'i18next';
import type { LongId } from '@/api/user';
import { getApiBaseUrl } from '@/config/env';
import { buildWikiListUrl } from '../wikiApp.helpers';
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

async function ensureOk<T>(
  request: Promise<ParsedApiResponse<T>>,
  fallbackMessage: string,
): Promise<T> {
  const response = await request;
  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(
      response.messageKey ? response : { ...response, message: undefined },
      fallbackMessage,
    );
  }

  return response.data;
}

export async function getWikiTree(t: TFunction): Promise<WikiDocumentTreeNodeVo[]> {
  return await ensureOk(apiGet<WikiDocumentTreeNodeVo[]>('/api/v1/Wiki/GetTree', { withAuth: true }), t('wiki.toast.loadListFailed'));
}

export async function getWikiList(query: WikiListQuery, t: TFunction): Promise<WikiPageModel<WikiDocumentVo>> {
  return await ensureOk(apiGet<WikiPageModel<WikiDocumentVo>>(buildWikiListUrl(query), { withAuth: true }), t('wiki.toast.loadListFailed'));
}

export async function getWikiDocumentById(id: LongId, includeDeleted: boolean, t: TFunction): Promise<WikiDocumentDetailVo> {
  const suffix = includeDeleted ? '?includeDeleted=true' : '';
  return await ensureOk(
    apiGet<WikiDocumentDetailVo>(`/api/v1/Wiki/GetById/${encodeURIComponent(String(id))}${suffix}`, { withAuth: true }),
    t('wiki.toast.loadDetailFailed')
  );
}

export async function getWikiDocumentBySlug(slug: string, t: TFunction): Promise<WikiDocumentDetailVo> {
  return await ensureOk(apiGet<WikiDocumentDetailVo>(`/api/v1/Wiki/GetBySlug/${encodeURIComponent(slug)}`, { withAuth: true }), t('wiki.toast.loadDetailFailed'));
}

export async function getWikiAuthorList(t: TFunction): Promise<WikiPageModel<WikiAuthorDocumentVo>> {
  return await ensureOk(
    apiGet<WikiPageModel<WikiAuthorDocumentVo>>('/api/v1/Wiki/AuthorGetList?pageIndex=1&pageSize=100', { withAuth: true }),
    t('wiki.author.feedback.loadListFailed'),
  );
}

export async function getWikiAuthorDraft(documentId: LongId, t: TFunction): Promise<WikiAuthorDraftDetailVo> {
  return await ensureOk(
    apiGet<WikiAuthorDraftDetailVo>(`/api/v1/Wiki/AuthorGetById/${encodeURIComponent(String(documentId))}`, { withAuth: true }),
    t('wiki.author.feedback.loadDetailFailed'),
  );
}

export async function createWikiAuthorDraft(
  request: CreateWikiAuthorDraftRequest,
  t: TFunction,
): Promise<WikiAuthorDraftDetailVo> {
  return await ensureOk(
    apiPost<WikiAuthorDraftDetailVo>('/api/v1/Wiki/AuthorCreate', request, { withAuth: true }),
    t('wiki.author.feedback.saveFailed'),
  );
}

export async function startWikiAuthorDraft(documentId: LongId, t: TFunction): Promise<WikiAuthorDraftDetailVo> {
  return await ensureOk(
    apiPost<WikiAuthorDraftDetailVo>(`/api/v1/Wiki/AuthorStartDraft/${encodeURIComponent(String(documentId))}`, undefined, { withAuth: true }),
    t('wiki.author.feedback.saveFailed'),
  );
}

export async function saveWikiAuthorDraft(
  draftId: LongId,
  request: SaveWikiAuthorDraftRequest,
  t: TFunction,
): Promise<WikiAuthorDraftDetailVo> {
  return await ensureOk(
    apiPut<WikiAuthorDraftDetailVo>(`/api/v1/Wiki/AuthorSaveDraft/${encodeURIComponent(String(draftId))}`, request, { withAuth: true }),
    t('wiki.author.feedback.saveFailed'),
  );
}

export async function submitWikiAuthorDraft(
  draftId: LongId,
  request: SubmitWikiDraftRequest,
  t: TFunction,
): Promise<WikiAuthorDraftDetailVo> {
  return await ensureOk(
    apiPost<WikiAuthorDraftDetailVo>(`/api/v1/Wiki/AuthorSubmitDraft/${encodeURIComponent(String(draftId))}`, request, { withAuth: true }),
    t('wiki.author.feedback.submitFailed'),
  );
}

export async function withdrawWikiAuthorDraft(
  draftId: LongId,
  request: SubmitWikiDraftRequest,
  t: TFunction,
): Promise<WikiAuthorDraftDetailVo> {
  return await ensureOk(
    apiPost<WikiAuthorDraftDetailVo>(`/api/v1/Wiki/AuthorWithdrawDraft/${encodeURIComponent(String(draftId))}`, request, { withAuth: true }),
    t('wiki.author.feedback.withdrawFailed'),
  );
}

export async function getWikiAuthorCollaborators(documentId: LongId, t: TFunction): Promise<WikiDocumentCollaboratorVo[]> {
  return await ensureOk(
    apiGet<WikiDocumentCollaboratorVo[]>(`/api/v1/Wiki/AuthorGetCollaborators/${encodeURIComponent(String(documentId))}`, { withAuth: true }),
    t('wiki.author.feedback.loadCollaboratorsFailed'),
  );
}

export async function inviteWikiAuthorCollaborator(
  documentId: LongId,
  userPublicId: string,
  t: TFunction,
): Promise<WikiDocumentCollaboratorVo> {
  return await ensureOk(
    apiPost<WikiDocumentCollaboratorVo>(
      `/api/v1/Wiki/AuthorInviteCollaborator/${encodeURIComponent(String(documentId))}`,
      { userPublicId },
      { withAuth: true },
    ),
    t('wiki.author.feedback.inviteFailed'),
  );
}

export async function respondWikiAuthorInvitation(
  collaboratorId: LongId,
  accept: boolean,
  t: TFunction,
): Promise<WikiDocumentCollaboratorVo> {
  return await ensureOk(
    apiPost<WikiDocumentCollaboratorVo>(
      `/api/v1/Wiki/AuthorRespondInvitation/${encodeURIComponent(String(collaboratorId))}`,
      { accept },
      { withAuth: true },
    ),
    t('wiki.author.feedback.invitationResponseFailed'),
  );
}

export async function removeWikiAuthorCollaborator(collaboratorId: LongId, t: TFunction): Promise<boolean> {
  return await ensureOk(
    apiPost<boolean>(
      `/api/v1/Wiki/AuthorRemoveCollaborator/${encodeURIComponent(String(collaboratorId))}`,
      undefined,
      { withAuth: true },
    ),
    t('wiki.author.feedback.removeCollaboratorFailed'),
  );
}

export async function createWikiDocument(request: CreateWikiDocumentRequest, t: TFunction): Promise<LongId> {
  const detail = await createWikiAuthorDraft({
    title: request.title,
    slug: request.slug,
    summary: request.summary,
    markdownContent: request.markdownContent,
    coverAttachmentId: request.coverAttachmentId,
    proposedParentId: request.parentId,
  }, t);
  return detail.voDocumentId;
}

export async function updateWikiDocument(id: LongId, request: UpdateWikiDocumentRequest, t: TFunction): Promise<boolean> {
  let detail: WikiAuthorDraftDetailVo;
  try {
    detail = await getWikiAuthorDraft(id, t);
  } catch (error) {
    if (!isApiResponseNotFoundError(error)) {
      throw error;
    }
    detail = await startWikiAuthorDraft(id, t);
  }

  await saveWikiAuthorDraft(detail.voDraftId, {
    title: request.title,
    slug: request.slug,
    summary: request.summary,
    markdownContent: request.markdownContent,
    coverAttachmentId: request.coverAttachmentId,
    proposedParentId: request.parentId,
    changeSummary: request.changeSummary,
    expectedDraftVersion: detail.voDraftVersion,
  }, t);
  return true;
}

export async function deleteWikiDocument(id: LongId, t: TFunction): Promise<boolean> {
  return await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Delete/${encodeURIComponent(String(id))}`, undefined, { withAuth: true }), t('wiki.toast.deleteFailed'));
}

export async function restoreWikiDocument(id: LongId, t: TFunction): Promise<boolean> {
  return await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Restore/${encodeURIComponent(String(id))}`, undefined, { withAuth: true }), t('wiki.toast.restoreFailed'));
}

export async function publishWikiDocument(id: LongId, t: TFunction): Promise<boolean> {
  return await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Publish/${encodeURIComponent(String(id))}`, undefined, { withAuth: true }), t('wiki.toast.publishFailed'));
}

export async function unpublishWikiDocument(id: LongId, t: TFunction): Promise<boolean> {
  return await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Unpublish/${encodeURIComponent(String(id))}`, undefined, { withAuth: true }), t('wiki.toast.unpublishFailed'));
}

export async function archiveWikiDocument(id: LongId, t: TFunction): Promise<boolean> {
  return await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Archive/${encodeURIComponent(String(id))}`, undefined, { withAuth: true }), t('wiki.toast.archiveFailed'));
}

export async function getWikiRevisionList(id: LongId, t: TFunction): Promise<WikiDocumentRevisionItemVo[]> {
  return await ensureOk(apiGet<WikiDocumentRevisionItemVo[]>(`/api/v1/Wiki/GetRevisionList/${encodeURIComponent(String(id))}`, { withAuth: true }), t('wiki.toast.loadRevisionListFailed'));
}

export async function getWikiRevisionDetail(revisionId: LongId, t: TFunction): Promise<WikiDocumentRevisionDetailVo> {
  return await ensureOk(apiGet<WikiDocumentRevisionDetailVo>(`/api/v1/Wiki/GetRevisionDetail/${encodeURIComponent(String(revisionId))}`, { withAuth: true }), t('wiki.toast.loadRevisionDetailFailed'));
}

export async function rollbackWikiRevision(revisionId: LongId, t: TFunction): Promise<boolean> {
  return await ensureOk(apiPost<boolean>(`/api/v1/Wiki/Rollback/${encodeURIComponent(String(revisionId))}`, undefined, { withAuth: true }), t('wiki.toast.rollbackFailed'));
}

export async function importWikiMarkdown(request: ImportWikiMarkdownRequest, t: TFunction): Promise<LongId> {
  const formData = new FormData();
  formData.append('file', request.file);

  if (request.slug?.trim()) {
    formData.append('slug', request.slug.trim());
  }

  if (request.summary?.trim()) {
    formData.append('summary', request.summary.trim());
  }

  if (request.parentId != null) {
    formData.append('parentId', String(request.parentId));
  }

  formData.append('sort', String(request.sort ?? 0));
  formData.append('publishAfterImport', String(Boolean(request.publishAfterImport)));

  const response = await apiFetch('/api/v1/Wiki/ImportMarkdown', {
    method: 'POST',
    withAuth: true,
    body: formData,
  });

  const json = await response.json() as ApiResponse<LongId>;
  const parsed = parseApiResponseWithI18n<LongId>(json, (key) => t(key));

  if (!parsed.ok || parsed.data === undefined) {
    throw createApiResponseError(parsed, t('wiki.toast.importFailed'));
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

export async function downloadWikiMarkdown(id: LongId, t: TFunction): Promise<{ blob: Blob; fileName: string }> {
  const response = await apiFetch(`/api/v1/Wiki/ExportMarkdown/${encodeURIComponent(String(id))}`, {
    method: 'GET',
    withAuth: true,
    headers: {
      Accept: 'text/markdown, application/json',
    },
  });

  if (!response.ok) {
    let errorMessage = t('wiki.toast.exportFailed');

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
  const fileName = getFileNameFromDisposition(response.headers.get('content-disposition'), `document-${id}.md`);
  return { blob, fileName };
}
