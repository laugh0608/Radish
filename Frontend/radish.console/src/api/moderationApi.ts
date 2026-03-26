import { apiGet, apiPost } from '@radish/http';

export interface VoPagedResult<T> {
  voItems: T[];
  voTotal: number;
  voPageIndex: number;
  voPageSize: number;
}

export interface ContentReportQueueItemVo {
  voReportId: number;
  voTargetType: string;
  voTargetContentId: number;
  voTargetUserId: number;
  voTargetUserName?: string | null;
  voReporterUserId: number;
  voReporterUserName: string;
  voReasonType: string;
  voReasonDetail?: string | null;
  voStatus: string;
  voReviewActionType: string;
  voReviewDurationHours?: number | null;
  voReviewRemark?: string | null;
  voReviewedByName?: string | null;
  voReviewedAt?: string | null;
  voCreateTime: string;
}

export interface UserModerationActionVo {
  voActionId: number;
  voTargetUserId: number;
  voTargetUserName?: string | null;
  voActionType: string;
  voReason: string;
  voSourceReportId?: number | null;
  voDurationHours?: number | null;
  voStartTime: string;
  voEndTime?: string | null;
  voIsActive: boolean;
  voOperatorUserName: string;
  voCreateTime: string;
}

export interface ReviewContentReportRequest {
  reportId: number;
  isApproved: boolean;
  actionType: number;
  durationHours?: number | null;
  reviewRemark?: string;
}

export async function getReviewQueue(params: {
  status?: number;
  pageIndex?: number;
  pageSize?: number;
}): Promise<VoPagedResult<ContentReportQueueItemVo>> {
  const searchParams = new URLSearchParams();
  searchParams.set('status', String(params.status ?? 0));
  searchParams.set('pageIndex', String(params.pageIndex ?? 1));
  searchParams.set('pageSize', String(params.pageSize ?? 20));

  const response = await apiGet<VoPagedResult<ContentReportQueueItemVo>>(
    `/api/v1/ContentModeration/GetReviewQueue?${searchParams.toString()}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取审核队列失败');
  }

  return response.data;
}

export async function reviewReport(request: ReviewContentReportRequest): Promise<ContentReportQueueItemVo> {
  const response = await apiPost<ContentReportQueueItemVo>('/api/v1/ContentModeration/Review', request, { withAuth: true });
  if (!response.ok || !response.data) {
    throw new Error(response.message || '审核举报失败');
  }

  return response.data;
}

export async function getActionLogs(params: {
  pageIndex?: number;
  pageSize?: number;
  targetUserId?: number;
}): Promise<VoPagedResult<UserModerationActionVo>> {
  const searchParams = new URLSearchParams();
  searchParams.set('pageIndex', String(params.pageIndex ?? 1));
  searchParams.set('pageSize', String(params.pageSize ?? 20));
  if (params.targetUserId) {
    searchParams.set('targetUserId', String(params.targetUserId));
  }

  const response = await apiGet<VoPagedResult<UserModerationActionVo>>(
    `/api/v1/ContentModeration/GetActionLogs?${searchParams.toString()}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取治理动作日志失败');
  }

  return response.data;
}
