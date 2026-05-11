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
  voTargetPostId?: number | null;
  voTargetCommentId?: number | null;
  voTargetChannelId?: number | null;
  voTargetMessageId?: number | null;
  voTargetNavigationStatus: string;
  voTargetNavigationMessage?: string | null;
  voTargetSnapshotTitle?: string | null;
  voTargetSnapshotSummary?: string | null;
  voTargetSnapshotIsPersisted: boolean;
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
  voSourceReportTargetType?: string | null;
  voSourceReportTargetContentId?: number | null;
  voSourceReportTargetPostId?: number | null;
  voSourceReportTargetCommentId?: number | null;
  voSourceReportTargetChannelId?: number | null;
  voSourceReportTargetMessageId?: number | null;
  voSourceReportTargetNavigationStatus: string;
  voSourceReportTargetNavigationMessage?: string | null;
  voSourceReportTargetSnapshotTitle?: string | null;
  voSourceReportTargetSnapshotSummary?: string | null;
  voSourceReportTargetSnapshotIsPersisted: boolean;
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

export interface ApplyUserModerationActionRequest {
  targetUserId: number;
  actionType: number;
  durationHours?: number | null;
  reason?: string;
  sourceReportId?: number | null;
}

export interface ContentModerationActionLogQuery {
  pageIndex?: number;
  pageSize?: number;
  targetUserId?: number;
  sourceReportId?: number;
  actionType?: string;
  isActive?: boolean;
  keyword?: string;
}

export async function getReviewQueue(params: {
  status?: number;
  targetType?: string;
  reasonType?: string;
  navigationStatus?: string;
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
}): Promise<VoPagedResult<ContentReportQueueItemVo>> {
  const searchParams = new URLSearchParams();
  searchParams.set('pageIndex', String(params.pageIndex ?? 1));
  searchParams.set('pageSize', String(params.pageSize ?? 20));
  if (params.status !== undefined && params.status >= 0) {
    searchParams.set('status', String(params.status));
  }
  if (params.targetType?.trim()) {
    searchParams.set('targetType', params.targetType.trim());
  }
  if (params.reasonType?.trim()) {
    searchParams.set('reasonType', params.reasonType.trim());
  }
  if (params.navigationStatus?.trim()) {
    searchParams.set('navigationStatus', params.navigationStatus.trim());
  }
  if (params.keyword?.trim()) {
    searchParams.set('keyword', params.keyword.trim());
  }

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

export async function applyUserModerationAction(request: ApplyUserModerationActionRequest): Promise<UserModerationActionVo> {
  const response = await apiPost<UserModerationActionVo>('/api/v1/ContentModeration/ApplyUserAction', request, { withAuth: true });
  if (!response.ok || !response.data) {
    throw new Error(response.message || '执行治理动作失败');
  }

  return response.data;
}

export async function getActionLogs(params: ContentModerationActionLogQuery): Promise<VoPagedResult<UserModerationActionVo>> {
  const searchParams = new URLSearchParams();
  searchParams.set('pageIndex', String(params.pageIndex ?? 1));
  searchParams.set('pageSize', String(params.pageSize ?? 20));
  if (params.targetUserId) {
    searchParams.set('targetUserId', String(params.targetUserId));
  }
  if (params.sourceReportId) {
    searchParams.set('sourceReportId', String(params.sourceReportId));
  }
  if (params.actionType?.trim()) {
    searchParams.set('actionType', params.actionType.trim());
  }
  if (params.isActive !== undefined) {
    searchParams.set('isActive', String(params.isActive));
  }
  if (params.keyword?.trim()) {
    searchParams.set('keyword', params.keyword.trim());
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
