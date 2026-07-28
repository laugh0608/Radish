import {
  apiGet,
  apiPost,
  configureApiClient,
  createApiResponseError,
  type ContentModerationAppealVersionedOperationRequest,
  type ContentModerationAppealVo,
  type ContentModerationDecisionNoticeVo,
  type ContentReportReceiptVo,
  type SubmitContentModerationAppealRequest,
} from '@radish/http';
import { getApiBaseUrl } from '@/config/env';

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

export type ContentReportTargetType = 'Post' | 'Comment' | 'PostAnswer' | 'PostQuickReply' | 'ChatMessage' | 'Product';

export interface SubmitContentReportRequest {
  targetType: ContentReportTargetType;
  targetContentId: string;
  reasonType: string;
  reasonDetail?: string;
}

export interface MyContentReportPage {
  voItems: ContentReportReceiptVo[];
  voTotal: number;
  voPageIndex: number;
  voPageSize: number;
}

export interface ContentModerationPage<T> {
  voItems: T[];
  voTotal: number;
  voPageIndex: number;
  voPageSize: number;
}

export async function getMyContentReports(
  pageIndex: number,
  pageSize: number,
  fallbackMessage: string,
): Promise<MyContentReportPage> {
  const search = new URLSearchParams({
    pageIndex: String(pageIndex),
    pageSize: String(pageSize),
  });
  const response = await apiGet<MyContentReportPage>(
    `/api/v1/ContentModeration/GetMyReports?${search.toString()}`,
    { withAuth: true },
  );

  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(response, fallbackMessage);
  }

  return response.data;
}

export async function submitContentReport(
  request: SubmitContentReportRequest,
  fallbackMessage: string,
): Promise<ContentReportReceiptVo> {
  const response = await apiPost<ContentReportReceiptVo>('/api/v1/ContentModeration/Report', request, { withAuth: true });

  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(response, fallbackMessage);
  }

  return response.data;
}

export async function getMyAppealableDecisions(
  pageIndex: number,
  pageSize: number,
  fallbackMessage: string,
): Promise<ContentModerationPage<ContentModerationDecisionNoticeVo>> {
  const search = new URLSearchParams({
    pageIndex: String(pageIndex),
    pageSize: String(pageSize),
  });
  const response = await apiGet<ContentModerationPage<ContentModerationDecisionNoticeVo>>(
    `/api/v1/ContentModeration/GetMyAppealableDecisions?${search.toString()}`,
    { withAuth: true },
  );

  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(response, fallbackMessage);
  }

  return response.data;
}

export async function getMyContentModerationAppeals(
  pageIndex: number,
  pageSize: number,
  fallbackMessage: string,
): Promise<ContentModerationPage<ContentModerationAppealVo>> {
  const search = new URLSearchParams({
    pageIndex: String(pageIndex),
    pageSize: String(pageSize),
  });
  const response = await apiGet<ContentModerationPage<ContentModerationAppealVo>>(
    `/api/v1/ContentModeration/GetMyAppeals?${search.toString()}`,
    { withAuth: true },
  );

  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(response, fallbackMessage);
  }

  return response.data;
}

export async function getMyContentModerationAppeal(
  appealPublicId: string,
  fallbackMessage: string,
): Promise<ContentModerationAppealVo> {
  const response = await apiGet<ContentModerationAppealVo>(
    `/api/v1/ContentModeration/GetMyAppeal/${encodeURIComponent(appealPublicId)}`,
    { withAuth: true },
  );

  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(response, fallbackMessage);
  }

  return response.data;
}

export async function submitContentModerationAppeal(
  request: SubmitContentModerationAppealRequest,
  fallbackMessage: string,
): Promise<ContentModerationAppealVo> {
  const response = await apiPost<ContentModerationAppealVo>(
    '/api/v1/ContentModeration/SubmitAppeal',
    request,
    { withAuth: true },
  );

  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(response, fallbackMessage);
  }

  return response.data;
}

export async function withdrawContentModerationAppeal(
  request: ContentModerationAppealVersionedOperationRequest,
  fallbackMessage: string,
): Promise<ContentModerationAppealVo> {
  const response = await apiPost<ContentModerationAppealVo>(
    '/api/v1/ContentModeration/WithdrawAppeal',
    request,
    { withAuth: true },
  );

  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(response, fallbackMessage);
  }

  return response.data;
}
