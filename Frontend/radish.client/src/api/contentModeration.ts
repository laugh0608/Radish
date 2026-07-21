import {
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
