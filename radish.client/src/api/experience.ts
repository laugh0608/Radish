/**
 * 经验值系统相关的 API 调用
 */

import { apiGet, configureApiClient, type PagedResponse } from '@radish/ui';
import { mapUserExperience, mapExpTransaction, mapLeaderboardItem, type ExperienceData, type ExpTransactionData, type LeaderboardItemData } from '@/utils/viewModelMapper';

// 配置 API 客户端
const defaultApiBase = 'https://localhost:5000';
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined || defaultApiBase;

configureApiClient({
  baseUrl: apiBaseUrl.replace(/\/$/, ''),
});

/**
 * 用户经验值信息
 */
export interface UserExperience {
  VoUserId: number;
  VoCurrentExp: number;
  VoCurrentLevel: number;
  VoNextLevelExp: number;
  VoTotalExp: number;
  VoLevelProgress: number;
  VoLevelName: string;
  VoLevelDescription: string;
  VoCanLevelUp: boolean;
  VoNextLevelName: string;
  VoExpToNextLevel: number;
  VoExpGainedToday: number;
  VoDailyExpLimit: number;
  VoRemainingDailyExp: number;
  VoIsMaxLevel: boolean;
  VoRank: number;
  VoPercentile: number;
  VoCreateTime: string;
  VoUpdateTime: string;
}

/**
 * 经验值交易记录
 */
export interface ExpTransaction {
  VoId: number;
  VoUserId: number;
  VoUserName: string;
  VoExpChange: number;
  VoExpBefore: number;
  VoExpAfter: number;
  VoLevelBefore: number;
  VoLevelAfter: number;
  VoTransactionType: string;
  VoTransactionTypeDisplay: string;
  VoDescription: string;
  VoRelatedId: number;
  VoRelatedType: string;
  VoIsPositive: boolean;
  VoFormattedExpChange: string;
  VoFormattedDescription: string;
  VoCreateTime: string;
  VoUpdateTime: string;
}

/**
 * 排行榜项目
 */
export interface LeaderboardItem {
  VoUserId: number;
  VoUserName: string;
  VoAvatar: string;
  VoCurrentLevel: number;
  VoTotalExp: number;
  VoLevelName: string;
  VoRank: number;
  VoExpGainedThisWeek: number;
  VoExpGainedThisMonth: number;
  VoIsCurrentUser: boolean;
}

/**
 * 经验值 API
 */
export const experienceApi = {
  /**
   * 获取我的经验值信息
   */
  async getMyExperience(): Promise<ExperienceData | null> {
    const response = await apiGet<UserExperience>('/api/v1/Experience/GetMyExperience', {
      withAuth: true,
    });

    if (response.ok && response.data) {
      return mapUserExperience(response.data);
    }
    return null;
  },

  /**
   * 获取经验值交易记录
   */
  async getTransactions(params: {
    pageIndex?: number;
    pageSize?: number;
  }): Promise<PagedResponse<ExpTransactionData> | null> {
    const { pageIndex = 1, pageSize = 20 } = params;

    const response = await apiGet<PagedResponse<ExpTransaction>>(
      `/api/v1/Experience/GetMyTransactions?pageIndex=${pageIndex}&pageSize=${pageSize}`,
      { withAuth: true }
    );

    if (response.ok && response.data) {
      return {
        ...response.data,
        data: response.data.data.map(mapExpTransaction),
      };
    }
    return null;
  },

  /**
   * 获取排行榜
   */
  async getLeaderboard(pageIndex: number = 1, pageSize: number = 50): Promise<PagedResponse<LeaderboardItemData> | null> {
    const response = await apiGet<PagedResponse<LeaderboardItem>>(
      `/api/v1/Experience/GetLeaderboard?pageIndex=${pageIndex}&pageSize=${pageSize}`,
      { withAuth: true }
    );

    if (response.ok && response.data) {
      return {
        ...response.data,
        data: response.data.data.map(mapLeaderboardItem),
      };
    }
    return null;
  },

  /**
   * 获取我的排名
   */
  async getMyRank(): Promise<number | null> {
    const response = await apiGet<{ VoRank: number }>('/api/v1/Experience/GetMyRank', {
      withAuth: true,
    });

    return response.ok && response.data ? response.data.VoRank : null;
  },
};

// 导出映射后的类型供组件使用
export type { ExperienceData, ExpTransactionData, LeaderboardItemData };