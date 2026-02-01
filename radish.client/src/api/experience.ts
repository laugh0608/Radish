/**
 * 经验值系统相关的 API 调用
 * 直接使用后端 Vo 字段名，无需映射
 */

import { apiGet, configureApiClient, type PagedResponse } from '@radish/ui';
import { getApiBaseUrl } from '@/config/env';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

/**
 * 用户经验值 Vo（直接使用后端字段名）
 */
export interface ExperienceData {
  voUserId: number;
  voUserName?: string;
  voAvatarUrl?: string;
  voCurrentLevel: number;
  voCurrentLevelName: string;  // 后端字段名
  voCurrentExp: number;
  voTotalExp: number;
  voExpToNextLevel: number;
  voNextLevel: number;
  voNextLevelName: string;
  voLevelProgress: number;
  voThemeColor: string;
  voIconUrl?: string;
  voBadgeUrl?: string;
  voLevelUpAt?: string;
  voRank?: number;
  voExpFrozen: boolean;
  voFrozenUntil?: string;
  voFrozenReason?: string;
}

/**
 * 经验值交易记录 Vo（直接使用后端字段名）
 */
export interface ExpTransactionData {
  voId: number;
  voUserId: number;
  voUserName?: string;
  voExpType: string;  // 后端字段名
  voExpTypeDisplay: string;  // 后端字段名
  voExpAmount: number;  // 后端字段名（经验值变动量）
  voBusinessType?: string;  // 后端字段名
  voBusinessId?: number;  // 后端字段名
  voRemark?: string;  // 后端字段名（备注）
  voExpBefore: number;
  voExpAfter: number;
  voLevelBefore: number;
  voLevelAfter: number;
  voIsLevelUp: boolean;  // 后端计算属性
  voCreateTime: string;
}

/**
 * 排行榜项目 Vo（直接使用后端字段名）
 */
export interface LeaderboardItemData {
  voUserId: number;
  voUserName?: string;
  voAvatarUrl?: string;
  voCurrentLevel: number;
  voTotalExp: number;
  voCurrentLevelName: string;  // 后端字段名
  voRank?: number;
  voExpGainedThisWeek?: number;
  voExpGainedThisMonth?: number;
  voIsCurrentUser?: boolean;
  voThemeColor?: string;
}

/**
 * 未读数量响应
 */
interface RankResponse {
  voRank: number;
}

/**
 * 经验值 API
 */
export const experienceApi = {
  /**
   * 获取我的经验值信息
   */
  async getMyExperience(): Promise<ExperienceData | null> {
    const response = await apiGet<ExperienceData>('/api/v1/Experience/GetMyExperience', {
      withAuth: true,
    });

    if (response.ok && response.data) {
      return response.data;
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

    const response = await apiGet<PagedResponse<ExpTransactionData>>(
      `/api/v1/Experience/GetTransactions?pageIndex=${pageIndex}&pageSize=${pageSize}`,
      { withAuth: true }
    );

    if (response.ok && response.data) {
      return response.data;
    }
    return null;
  },

  /**
   * 获取排行榜
   */
  async getLeaderboard(pageIndex: number = 1, pageSize: number = 50): Promise<PagedResponse<LeaderboardItemData> | null> {
    const response = await apiGet<PagedResponse<LeaderboardItemData>>(
      `/api/v1/Experience/GetLeaderboard?pageIndex=${pageIndex}&pageSize=${pageSize}`,
      { withAuth: true }
    );

    if (response.ok && response.data) {
      return response.data;
    }
    return null;
  },

  /**
   * 获取我的排名
   */
  async getMyRank(): Promise<number | null> {
    const response = await apiGet<RankResponse>('/api/v1/Experience/GetMyRank', {
      withAuth: true,
    });

    return response.ok && response.data ? response.data.voRank : null;
  },
};
