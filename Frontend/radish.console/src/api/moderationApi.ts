import {
  apiGet,
  apiPost,
  createApiResponseError,
  type ApplyContentModerationCorrectiveActionRequest,
  type CaptureContentModerationEvidenceRequest,
  type CaptureContentModerationAppealEvidenceRequest,
  type ContentModerationAppealActionResultVo,
  type ContentModerationAppealEventVo,
  type ContentModerationAppealVersionedOperationRequest,
  type ContentModerationAppealVo,
  type ContentModerationCaseDetailVo,
  type ContentModerationCaseQueueItemVo,
  type ContentModerationCaseReviewResultVo,
  type ReviewContentModerationCaseRequest,
  type ReviewContentModerationAppealRequest,
} from '@radish/http';

export type ConsoleLongId = string;

export interface VoPagedResult<T> {
  voItems: T[];
  voTotal: number;
  voPageIndex: number;
  voPageSize: number;
}

export interface ContentModerationCaseQueueQuery {
  status?: number;
  targetType?: string;
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
}

export interface ContentModerationAppealQueueQuery {
  status?: number;
  targetType?: string;
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
}

export async function getCaseQueue(
  params: ContentModerationCaseQueueQuery,
): Promise<VoPagedResult<ContentModerationCaseQueueItemVo>> {
  const searchParams = new URLSearchParams();
  searchParams.set('pageIndex', String(params.pageIndex ?? 1));
  searchParams.set('pageSize', String(params.pageSize ?? 20));
  if (params.status !== undefined && params.status >= 0) {
    searchParams.set('status', String(params.status));
  }
  if (params.targetType?.trim()) {
    searchParams.set('targetType', params.targetType.trim());
  }
  if (params.keyword?.trim()) {
    searchParams.set('keyword', params.keyword.trim());
  }

  const response = await apiGet<VoPagedResult<ContentModerationCaseQueueItemVo>>(
    `/api/v1/ContentModeration/GetCaseQueue?${searchParams.toString()}`,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '获取治理案件队列失败');
  }

  return response.data;
}

export async function getModerationCase(casePublicId: string): Promise<ContentModerationCaseDetailVo> {
  const response = await apiGet<ContentModerationCaseDetailVo>(
    `/api/v1/ContentModeration/GetCase/${encodeURIComponent(casePublicId)}`,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '获取治理案件详情失败');
  }

  return response.data;
}

export async function captureModerationEvidence(
  request: CaptureContentModerationEvidenceRequest,
): Promise<ContentModerationCaseDetailVo> {
  const response = await apiPost<ContentModerationCaseDetailVo>(
    '/api/v1/ContentModeration/CaptureEvidence',
    request,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '追加治理证据失败');
  }

  return response.data;
}

export async function reviewModerationCase(
  request: ReviewContentModerationCaseRequest,
): Promise<ContentModerationCaseReviewResultVo> {
  const response = await apiPost<ContentModerationCaseReviewResultVo>(
    '/api/v1/ContentModeration/ReviewCase',
    request,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '提交治理案件决定失败');
  }

  return response.data;
}

export async function applyModerationCorrectiveAction(
  request: ApplyContentModerationCorrectiveActionRequest,
): Promise<ContentModerationCaseReviewResultVo> {
  const response = await apiPost<ContentModerationCaseReviewResultVo>(
    '/api/v1/ContentModeration/ApplyCorrectiveAction',
    request,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '执行治理纠正动作失败');
  }

  return response.data;
}

function buildAppealQuery(params: ContentModerationAppealQueueQuery): URLSearchParams {
  const searchParams = new URLSearchParams();
  searchParams.set('pageIndex', String(params.pageIndex ?? 1));
  searchParams.set('pageSize', String(params.pageSize ?? 20));
  if (params.status !== undefined && params.status >= 0) {
    searchParams.set('status', String(params.status));
  }
  if (params.targetType?.trim()) {
    searchParams.set('targetType', params.targetType.trim());
  }
  if (params.keyword?.trim()) {
    searchParams.set('keyword', params.keyword.trim());
  }
  return searchParams;
}

export async function getAppealQueue(
  params: ContentModerationAppealQueueQuery,
): Promise<VoPagedResult<ContentModerationAppealVo>> {
  const response = await apiGet<VoPagedResult<ContentModerationAppealVo>>(
    `/api/v1/ContentModeration/GetAppealQueue?${buildAppealQuery(params).toString()}`,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '获取治理申诉队列失败');
  }
  return response.data;
}

export async function getModerationAppeal(appealPublicId: string): Promise<ContentModerationAppealVo> {
  const response = await apiGet<ContentModerationAppealVo>(
    `/api/v1/ContentModeration/GetAppeal/${encodeURIComponent(appealPublicId)}`,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '获取治理申诉详情失败');
  }
  return response.data;
}

export async function getModerationAppealEvents(
  appealPublicId: string,
): Promise<ContentModerationAppealEventVo[]> {
  const search = new URLSearchParams({ appealPublicId });
  const response = await apiGet<ContentModerationAppealEventVo[]>(
    `/api/v1/ContentModeration/GetAppealEvents?${search.toString()}`,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '获取治理申诉事件失败');
  }
  return response.data;
}

async function runAppealOperation(
  path: string,
  request: ContentModerationAppealVersionedOperationRequest,
  fallbackMessage: string,
): Promise<ContentModerationAppealVo> {
  const response = await apiPost<ContentModerationAppealVo>(path, request, { withAuth: true });
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, fallbackMessage);
  }
  return response.data;
}

export function startModerationAppealReview(
  request: ContentModerationAppealVersionedOperationRequest,
): Promise<ContentModerationAppealVo> {
  return runAppealOperation(
    '/api/v1/ContentModeration/StartAppealReview',
    request,
    '受理治理申诉失败',
  );
}

export async function captureModerationAppealEvidence(
  request: CaptureContentModerationAppealEvidenceRequest,
): Promise<ContentModerationAppealVo> {
  const response = await apiPost<ContentModerationAppealVo>(
    '/api/v1/ContentModeration/CaptureAppealEvidence',
    request,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '追加申诉复核证据失败');
  }
  return response.data;
}

export async function reviewModerationAppeal(
  request: ReviewContentModerationAppealRequest,
): Promise<ContentModerationAppealVo> {
  const response = await apiPost<ContentModerationAppealVo>(
    '/api/v1/ContentModeration/ReviewAppeal',
    request,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '登记申诉复核结果失败');
  }
  return response.data;
}

export async function executeModerationAppealRelief(
  request: ContentModerationAppealVersionedOperationRequest,
): Promise<ContentModerationAppealActionResultVo> {
  const response = await apiPost<ContentModerationAppealActionResultVo>(
    '/api/v1/ContentModeration/ExecuteAppealRelief',
    request,
    { withAuth: true },
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, '执行申诉纠正失败');
  }
  return response.data;
}
