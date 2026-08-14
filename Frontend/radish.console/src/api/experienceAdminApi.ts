import { apiGet, apiPost, createApiResponseError, type PagedResponse } from '@radish/http';

export interface UserExperienceVo {
  voUserId: string;
  voUserName?: string | null;
  voAvatarUrl?: string | null;
  voCurrentLevel: number;
  voCurrentLevelName: string;
  voCurrentExp: number;
  voTotalExp: number;
  voExpToNextLevel: number;
  voNextLevel: number;
  voNextLevelName: string;
  voLevelProgress: number;
  voThemeColor: string;
  voRank?: number | null;
  voExpFrozen: boolean;
  voFrozenUntil?: string | null;
  voFrozenReason?: string | null;
  voVersion: number;
}

export interface LevelConfigVo {
  voLevel: number;
  voLevelName: string;
  voExpRequired: number;
  voExpCumulative: number;
  voThemeColor?: string | null;
  voDescription?: string | null;
  voPrivileges?: string[] | null;
  voIsEnabled: boolean;
  voSortOrder: number;
}

export interface UserExpDailyStatsVo {
  voId: string;
  voUserId: string;
  voStatDate: string;
  voExpEarned: number;
  voExpFromPost: number;
  voExpFromComment: number;
  voExpFromLike: number;
  voExpFromHighlight: number;
  voExpFromLogin: number;
  voPostCount: number;
  voCommentCount: number;
  voLikeGivenCount: number;
  voLikeReceivedCount: number;
  voObservations?: UserExpDailyStatObservationVo[] | null;
}

export interface UserExpDailyStatObservationVo {
  voLabel: string;
  voTone: 'success' | 'processing' | 'warning' | 'default';
  voKind: 'context' | 'anomaly';
  voRuleCode: string;
  voDescription?: string | null;
}

export interface UserExpDailyStatsSummaryVo {
  voTotalExp: number;
  voAverageExp: number;
  voPeakDayExp: number;
  voPeakStatDate?: string | null;
  voZeroGainDays: number;
  voReviewDays: number;
  voNotices: string[];
}

export interface UserExpAnomalyRuleSummaryVo {
  voRuleCode: string;
  voRuleLabel: string;
  voThresholdDescription: string;
  voHitDays: number;
  voLatestHitDate?: string | null;
  voStrongestSignal: string;
  voSeverity: 'observe' | 'review' | 'freeze-suggest';
  voSuggestedAction: string;
}

export interface UserExpGovernanceRecommendationVo {
  voLevel: 'normal' | 'review' | 'freeze-suggest';
  voTitle: string;
  voReason: string;
  voSuggestedAction: string;
}

export interface UserExperienceGovernanceActionVo {
  voActionId: string;
  voTargetUserId: string;
  voTargetUserName?: string | null;
  voActionType: 'Review' | 'Freeze' | 'Unfreeze' | 'AutoUnfreeze' | 'Unknown' | string;
  voActionTypeDisplay: string;
  voReviewResult?: 'NoIssue' | 'Observe' | 'FreezeSuggest' | null;
  voReviewResultDisplay?: string | null;
  voRemark: string;
  voEvidenceSummary?: string | null;
  voWindowDays?: number | null;
  voStatDate?: string | null;
  voRuleCodes: string[];
  voRuleLabels: string[];
  voRecommendationLevel?: 'normal' | 'review' | 'freeze-suggest' | null;
  voRecommendationTitle?: string | null;
  voRecommendationReason?: string | null;
  voFrozenUntil?: string | null;
  voExpectedVersion?: number | null;
  voResultVersion?: number | null;
  voOperatorId: string;
  voOperatorName?: string | null;
  voCreateTime: string;
}

export interface UserExpDailyLimitSnapshotVo {
  voDailyLimitEnabled: boolean;
  voMaxDailyExp: number;
  voMaxExpFromPost: number;
  voMaxExpFromComment: number;
  voMaxExpFromLike: number;
  voMaxExpFromHighlight: number;
  voMaxExpFromLogin: number;
}

export interface UserExpDailyStatsWindowVo {
  voWindowDays: number;
  voStats: UserExpDailyStatsVo[];
  voSummary?: UserExpDailyStatsSummaryVo | null;
  voRuleSummaries?: UserExpAnomalyRuleSummaryVo[] | null;
  voRecommendation?: UserExpGovernanceRecommendationVo | null;
  voLimits?: UserExpDailyLimitSnapshotVo | null;
}

export interface ExpTransactionVo {
  voId: string;
  voUserId: string;
  voUserName?: string | null;
  voOperatorId: string;
  voOperatorName?: string | null;
  voExpType: string;
  voExpTypeDisplay: string;
  voExpAmount: number;
  voBusinessType?: string | null;
  voBusinessId?: string | null;
  voRemark?: string | null;
  voExpBefore: number;
  voExpAfter: number;
  voLevelBefore: number;
  voLevelAfter: number;
  voCreateTime: string;
}

export interface AdminAdjustExperienceRequest {
  userId: string;
  deltaExp: number;
  reason: string;
  expectedVersion: number;
  idempotencyKey: string;
}

export interface AdminFreezeExperienceRequest {
  userId: string;
  reason: string;
  frozenUntil?: string;
  expectedVersion: number;
}

export interface AdminUnfreezeExperienceRequest {
  userId: string;
  reason: string;
  expectedVersion: number;
}

export interface AdminRecordExperienceGovernanceReviewRequest {
  userId: string;
  reviewResult: 'NoIssue' | 'Observe' | 'FreezeSuggest';
  remark: string;
  windowDays?: number;
  statDate?: string;
  ruleCodes?: string[];
  ruleLabels?: string[];
  recommendationLevel?: 'normal' | 'review' | 'freeze-suggest';
  recommendationReason?: string;
  expectedVersion: number;
  idempotencyKey: string;
}

export interface AdminExperienceAdjustmentResultVo {
  voExperience: UserExperienceVo;
  voTransaction: ExpTransactionVo;
  voReplayed: boolean;
}

export interface AdminExperienceGovernanceResultVo {
  voExperience: UserExperienceVo;
  voAction: UserExperienceGovernanceActionVo;
  voReplayed: boolean;
}

export interface ExperienceLevelRecalculationChangeVo {
  voLevel: number;
  voLevelName: string;
  voBeforeExpRequired: number;
  voAfterExpRequired: number;
  voBeforeExpCumulative: number;
  voAfterExpCumulative: number;
  voChanged: boolean;
}

export interface ExperienceLevelRecalculationPreviewVo {
  voFingerprint: string;
  voFormulaType: string;
  voFormulaSummary: string;
  voChangedLevelCount: number;
  voMissingLevels: number[];
  voChanges: ExperienceLevelRecalculationChangeVo[];
}

export interface ExperienceLevelRecalculationAuditVo {
  voAuditId: string;
  voFormulaType: string;
  voFormulaSummary: string;
  voPreviewFingerprint: string;
  voChangedLevelCount: number;
  voReason: string;
  voOperatorId: string;
  voOperatorName: string;
  voCreateTime: string;
}

export interface ExperienceLevelRecalculationResultVo {
  voLevels: LevelConfigVo[];
  voAudit: ExperienceLevelRecalculationAuditVo;
}

export async function getUserExperience(userId: string): Promise<UserExperienceVo> {
  const response = await apiGet<UserExperienceVo>(
    `/api/v1/Experience/GetUserExperience/${encodeURIComponent(String(userId))}`,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, 'experience.feedback.loadUserFailed');
  }

  return response.data;
}

export async function getLevelConfigs(): Promise<LevelConfigVo[]> {
  const response = await apiGet<LevelConfigVo[]>('/api/v1/Experience/GetLevelConfigs', { withAuth: true });
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, 'experience.feedback.loadLevelsFailed');
  }

  return response.data;
}

export async function getUserDailyStats(
  userId: string,
  days: number = 7
): Promise<UserExpDailyStatsWindowVo> {
  const searchParams = new URLSearchParams({
    days: String(days),
  });
  const response = await apiGet<UserExpDailyStatsWindowVo>(
    `/api/v1/Experience/GetUserDailyStats/${encodeURIComponent(String(userId))}?${searchParams.toString()}`,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, 'experience.feedback.loadStatsFailed');
  }

  return response.data;
}

export async function getUserGovernanceActions(
  userId: string,
  pageIndex: number = 1,
  pageSize: number = 20
): Promise<PagedResponse<UserExperienceGovernanceActionVo>> {
  const searchParams = new URLSearchParams({
    pageIndex: String(pageIndex),
    pageSize: String(pageSize),
  });
  const response = await apiGet<PagedResponse<UserExperienceGovernanceActionVo>>(
    `/api/v1/Experience/GetUserGovernanceActions/${encodeURIComponent(String(userId))}?${searchParams.toString()}`,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, 'experience.feedback.loadActionsFailed');
  }

  return response.data;
}

export async function getUserTransactions(params: {
  userId: string;
  pageIndex?: number;
  pageSize?: number;
  expType?: string;
  startDate?: string;
  endDate?: string;
}): Promise<PagedResponse<ExpTransactionVo>> {
  const searchParams = new URLSearchParams({
    pageIndex: String(params.pageIndex ?? 1),
    pageSize: String(params.pageSize ?? 20),
  });

  if (params.expType?.trim()) {
    searchParams.set('expType', params.expType.trim());
  }

  if (params.startDate?.trim()) {
    searchParams.set('startDate', params.startDate.trim());
  }

  if (params.endDate?.trim()) {
    searchParams.set('endDate', params.endDate.trim());
  }

  const response = await apiGet<PagedResponse<ExpTransactionVo>>(
    `/api/v1/Experience/GetUserTransactions/${encodeURIComponent(String(params.userId))}?${searchParams.toString()}`,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, 'experience.feedback.loadTransactionsFailed');
  }

  return response.data;
}

export async function adminAdjustExperience(
  request: AdminAdjustExperienceRequest
): Promise<AdminExperienceAdjustmentResultVo> {
  const response = await apiPost<AdminExperienceAdjustmentResultVo>(
    '/api/v1/Experience/AdminAdjustExperience',
    request,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, 'experience.feedback.adjustFailed');
  }

  return response.data;
}

export async function adminFreezeExperience(
  request: AdminFreezeExperienceRequest
): Promise<AdminExperienceGovernanceResultVo> {
  const response = await apiPost<AdminExperienceGovernanceResultVo>(
    '/api/v1/Experience/AdminFreezeExperience',
    request,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, 'experience.feedback.freezeFailed');
  }

  return response.data;
}

export async function adminUnfreezeExperience(
  request: AdminUnfreezeExperienceRequest
): Promise<AdminExperienceGovernanceResultVo> {
  const response = await apiPost<AdminExperienceGovernanceResultVo>(
    '/api/v1/Experience/AdminUnfreezeExperience',
    request,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, 'experience.feedback.unfreezeFailed');
  }

  return response.data;
}

export async function adminRecordGovernanceReview(
  request: AdminRecordExperienceGovernanceReviewRequest
): Promise<AdminExperienceGovernanceResultVo> {
  const response = await apiPost<AdminExperienceGovernanceResultVo>(
    '/api/v1/Experience/AdminRecordGovernanceReview',
    request,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, 'experience.feedback.reviewFailed');
  }

  return response.data;
}

export async function previewLevelConfigRecalculation(): Promise<ExperienceLevelRecalculationPreviewVo> {
  const response = await apiGet<ExperienceLevelRecalculationPreviewVo>(
    '/api/v1/Experience/PreviewLevelConfigRecalculation',
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, 'experience.feedback.recalculateFailed');
  }

  return response.data;
}

export async function recalculateLevelConfigs(request: {
  expectedFingerprint: string;
  reason: string;
}): Promise<ExperienceLevelRecalculationResultVo> {
  const response = await apiPost<ExperienceLevelRecalculationResultVo>(
    '/api/v1/Experience/RecalculateLevelConfigs',
    request,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, 'experience.feedback.recalculateFailed');
  }

  return response.data;
}

export async function getLevelRecalculationAudits(
  pageIndex: number = 1,
  pageSize: number = 20
): Promise<PagedResponse<ExperienceLevelRecalculationAuditVo>> {
  const searchParams = new URLSearchParams({ pageIndex: String(pageIndex), pageSize: String(pageSize) });
  const response = await apiGet<PagedResponse<ExperienceLevelRecalculationAuditVo>>(
    `/api/v1/Experience/GetLevelRecalculationAudits?${searchParams.toString()}`,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, 'experience.feedback.recalculateFailed');
  }

  return response.data;
}
