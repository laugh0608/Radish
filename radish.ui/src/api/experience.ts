import { apiGet, apiPost } from './client';
import type { PagedResponse } from './types';

/**
 * 用户经验值信息
 */
export interface UserExperience {
  userId: string;
  userName?: string;
  avatarUrl?: string;
  currentLevel: number;
  currentLevelName: string;
  currentExp: string;
  totalExp: string;
  expToNextLevel: string;
  nextLevel: number;
  nextLevelName: string;
  levelProgress: number;
  themeColor: string;
  iconUrl?: string;
  badgeUrl?: string;
  levelUpAt?: string;
  rank?: number;
  expFrozen: boolean;
  frozenUntil?: string;
  frozenReason?: string;
}

/**
 * 经验值交易记录
 */
export interface ExpTransaction {
  id: string;
  userId: string;
  expType: string;
  expAmount: number;
  businessType?: string;
  businessId?: string;
  remark?: string;
  expBefore: string;
  expAfter: string;
  levelBefore: number;
  levelAfter: number;
  createdDate: string;
  createTime: string;
}

/**
 * 经验值交易分页查询参数
 */
export interface ExpTransactionQueryParams {
  pageIndex?: number;
  pageSize?: number;
  expType?: string;
  startDate?: string;
  endDate?: string;
}

/**
 * 排行榜项
 */
export interface LeaderboardItem {
  rank: number;
  userId: string;
  userName: string;
  avatarUrl?: string;
  currentLevel: number;
  currentLevelName: string;
  totalExp: string;
  themeColor?: string;
  badgeUrl?: string;
  isCurrentUser: boolean;
}

/**
 * 经验值 API 客户端
 */
export const experienceApi = {
  /**
   * 获取当前用户经验值信息
   */
  async getMyExperience(): Promise<UserExperience> {
    const response = await apiGet<UserExperience>(
      '/api/v1/Experience/GetMyExperience',
      { withAuth: true }
    );
    if (!response.ok) {
      throw new Error(response.message || '获取经验值信息失败');
    }
    return response.data!;
  },

  /**
   * 获取指定用户经验值信息
   */
  async getUserExperience(userId: string | number): Promise<UserExperience> {
    const response = await apiGet<UserExperience>(
      `/api/v1/Experience/GetMyExperience/${userId}`,
      { withAuth: true }
    );
    if (!response.ok) {
      throw new Error(response.message || '获取经验值信息失败');
    }
    return response.data!;
  },

  /**
   * 批量获取用户经验值信息
   */
  async getBatchUserExperiences(userIds: (string | number)[]): Promise<Record<string, UserExperience>> {
    const response = await apiPost<Record<string, UserExperience>>(
      '/api/v1/Experience/GetBatchUserExperiences',
      { userIds },
      { withAuth: true }
    );
    if (!response.ok) {
      throw new Error(response.message || '批量获取经验值信息失败');
    }
    return response.data!;
  },

  /**
   * 获取经验值交易记录
   */
  async getTransactions(params?: ExpTransactionQueryParams): Promise<PagedResponse<ExpTransaction>> {
    const queryString = params ? '?' + new URLSearchParams(
      Object.entries(params)
        .filter(([_, v]) => v !== undefined)
        .map(([k, v]) => [k, String(v)])
    ).toString() : '';

    const response = await apiGet<PagedResponse<ExpTransaction>>(
      `/api/v1/Experience/GetMyTransactions${queryString}`,
      { withAuth: true }
    );
    if (!response.ok) {
      throw new Error(response.message || '获取交易记录失败');
    }
    return response.data!;
  },

  /**
   * 获取经验值排行榜
   */
  async getLeaderboard(pageIndex = 1, pageSize = 50): Promise<PagedResponse<LeaderboardItem>> {
    const response = await apiGet<PagedResponse<LeaderboardItem>>(
      `/api/v1/Experience/GetLeaderboard?pageIndex=${pageIndex}&pageSize=${pageSize}`
    );
    if (!response.ok) {
      throw new Error(response.message || '获取排行榜失败');
    }
    return response.data!;
  },

  /**
   * 获取当前用户排名
   */
  async getMyRank(): Promise<number> {
    const response = await apiGet<number>(
      '/api/v1/Experience/GetMyRank',
      { withAuth: true }
    );
    if (!response.ok) {
      throw new Error(response.message || '获取排名失败');
    }
    return response.data!;
  },
};
