import {
  apiGet,
  apiPost,
  configureApiClient,
  createApiResponseError,
  type ContentReportReceiptVo,
} from '@radish/http';
import { getApiBaseUrl } from '@/config/env';

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

export type ContentReportTargetType = 'Post' | 'Comment' | 'PostQuickReply' | 'ChatMessage' | 'Product';

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
