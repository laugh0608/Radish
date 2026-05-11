import { apiGet, apiPost, type PagedResponse } from '@radish/http';

export interface UserExperienceVo {
  voUserId: number | string;
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
  voId: number | string;
  voUserId: number | string;
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
  voId: number | string;
  voUserId: number | string;
  voUserName?: string | null;
  voOperatorId: number | string;
  voOperatorName?: string | null;
  voExpType: string;
  voExpTypeDisplay: string;
  voExpAmount: number;
  voBusinessType?: string | null;
  voBusinessId?: number | string | null;
  voRemark?: string | null;
  voExpBefore: number;
  voExpAfter: number;
  voLevelBefore: number;
  voLevelAfter: number;
  voCreateTime: string;
}

export interface AdminAdjustExperienceRequest {
  userId: string | number;
  deltaExp: number;
  reason?: string;
}

export interface AdminFreezeExperienceRequest {
  userId: string | number;
  reason: string;
  frozenUntil?: string;
}

export async function getUserExperience(userId: string | number): Promise<UserExperienceVo> {
  const response = await apiGet<UserExperienceVo>(
    `/api/v1/Experience/GetUserExperience/${encodeURIComponent(String(userId))}`,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取用户经验失败');
  }

  return response.data;
}

export async function getLevelConfigs(): Promise<LevelConfigVo[]> {
  const response = await apiGet<LevelConfigVo[]>('/api/v1/Experience/GetLevelConfigs', { withAuth: true });
  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取等级配置失败');
  }

  return response.data;
}

export async function getUserDailyStats(
  userId: string | number,
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
    throw new Error(response.message || '获取用户经验统计失败');
  }

  return response.data;
}

export async function getUserTransactions(params: {
  userId: string | number;
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
    throw new Error(response.message || '获取用户经验流水失败');
  }

  return response.data;
}

export async function adminAdjustExperience(request: AdminAdjustExperienceRequest): Promise<void> {
  const response = await apiPost('/api/v1/Experience/AdminAdjustExperience', request, { withAuth: true });
  if (!response.ok) {
    throw new Error(response.message || '调整经验失败');
  }
}

export async function adminFreezeExperience(request: AdminFreezeExperienceRequest): Promise<void> {
  const response = await apiPost('/api/v1/Experience/AdminFreezeExperience', request, { withAuth: true });
  if (!response.ok) {
    throw new Error(response.message || '冻结经验失败');
  }
}

export async function adminUnfreezeExperience(userId: string | number): Promise<void> {
  const response = await apiPost(
    '/api/v1/Experience/AdminUnfreezeExperience',
    { userId },
    { withAuth: true }
  );
  if (!response.ok) {
    throw new Error(response.message || '解冻经验失败');
  }
}

export async function recalculateLevelConfigs(): Promise<LevelConfigVo[]> {
  const response = await apiPost<LevelConfigVo[]>('/api/v1/Experience/RecalculateLevelConfigs', {}, { withAuth: true });
  if (!response.ok || !response.data) {
    throw new Error(response.message || '重算等级配置失败');
  }

  return response.data;
}
