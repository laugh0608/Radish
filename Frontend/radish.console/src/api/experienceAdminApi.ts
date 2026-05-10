import { apiGet, apiPost } from '@radish/http';

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
}

export interface UserExpDailyStatsSummaryVo {
  voTotalExp: number;
  voAverageExp: number;
  voPeakDayExp: number;
  voPeakStatDate?: string | null;
  voZeroGainDays: number;
  voNotices: string[];
}

export interface UserExpDailyStatsWindowVo {
  voWindowDays: number;
  voStats: UserExpDailyStatsVo[];
  voSummary?: UserExpDailyStatsSummaryVo | null;
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
