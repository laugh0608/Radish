import {
  apiGet,
  apiPost,
  createApiResponseError,
  type ApplyContentModerationCorrectiveActionRequest,
  type CaptureContentModerationEvidenceRequest,
  type ContentModerationCaseDetailVo,
  type ContentModerationCaseQueueItemVo,
  type ContentModerationCaseReviewResultVo,
  type ReviewContentModerationCaseRequest,
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
