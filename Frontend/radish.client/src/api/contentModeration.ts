import { apiPost, configureApiClient } from '@radish/http';
import { getApiBaseUrl } from '@/config/env';

configureApiClient({
  baseUrl: getApiBaseUrl(),
});

export type ContentReportTargetType = 'Post' | 'Comment' | 'PostQuickReply' | 'ChatMessage' | 'Product';

export interface SubmitContentReportRequest {
  targetType: ContentReportTargetType;
  targetContentId: number;
  reasonType: string;
  reasonDetail?: string;
}

export async function submitContentReport(request: SubmitContentReportRequest): Promise<number> {
  const response = await apiPost<number>('/api/v1/ContentModeration/Report', request, { withAuth: true });

  if (!response.ok || response.data === undefined) {
    throw new Error(response.message || '举报提交失败');
  }

  return response.data;
}
