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
  voCurrentExp: number;
  voCurrentLevel: number;
  voNextLevelExp: number;
  voTotalExp: number;
  voLevelProgress: number;
  voLevelName: string;
  voLevelDescription: string;
  voCanLevelUp: boolean;
  voNextLevelName: string;
  voExpToNextLevel: number;
  voExpGainedToday: number;
  voDailyExpLimit: number;
  voRemainingDailyExp: number;
  voIsMaxLevel: boolean;
  voRank: number;
  voPercentile: number;
  voCreateTime: string;
  voUpdateTime: string;
  voExpFrozen?: boolean;
  // 前端扩展字段（用于 UI 组件兼容）
  userId?: string;
  currentExp?: number;
  currentLevel?: number;
  nextLevelExp?: number;
  totalExp?: number;
  levelProgress?: number;
  levelName?: string;
  levelDescription?: string;
  canLevelUp?: boolean;
  nextLevelName?: string;
  expToNextLevel?: number;
  expGainedToday?: number;
  dailyExpLimit?: number;
  remainingDailyExp?: number;
  isMaxLevel?: boolean;
  rank?: number;
  percentile?: number;
  createTime?: string;
  updateTime?: string;
  themeColor?: string;
  currentLevelName?: string;
  nextLevel?: number;
  expFrozen?: boolean;
}

/**
 * 经验值交易记录 Vo（直接使用后端字段名）
 */
export interface ExpTransactionData {
  voId: number;
  voUserId: number;
  voUserName: string;
  voExpChange: number;
  voExpBefore: number;
  voExpAfter: number;
  voLevelBefore: number;
  voLevelAfter: number;
  voTransactionType: string;
  voTransactionTypeDisplay: string;
  voDescription: string;
  voRelatedId: number;
  voRelatedType: string;
  voIsPositive: boolean;
  voFormattedExpChange: string;
  voFormattedDescription: string;
  voCreateTime: string;
  voUpdateTime: string;
  // 兼容旧字段名（用于 UI 组件）
  id?: number;
  expType?: string;
  expAmount?: number;
  remark?: string;
  createTime?: string;
}

/**
 * 排行榜项目 Vo（直接使用后端字段名）
 */
export interface LeaderboardItemData {
  voUserId: number;
  voUserName: string;
  voAvatarUrl?: string;
  voCurrentLevel: number;
  voTotalExp: number;
  voLevelName: string;
  voRank: number;
  voExpGainedThisWeek: number;
  voExpGainedThisMonth: number;
  voIsCurrentUser: boolean;
  voThemeColor?: string;
  voCurrentLevelName?: string;
  // 兼容旧字段名（用于 UI 组件）
  userId?: number;
  userName?: string;
  avatar?: string;
  currentLevel?: number;
  totalExp?: number;
  levelName?: string;
  rank?: number;
  expGainedThisWeek?: number;
  expGainedThisMonth?: number;
  isCurrentUser?: boolean;
  themeColor?: string;
  currentLevelName?: string;
}

/**
 * 未读数量响应
 */
interface RankResponse {
  voRank: number;
}

/**
 * 为 ExperienceData 添加兼容字段
 */
function addExperienceCompatFields(data: ExperienceData): ExperienceData {
  return {
    ...data,
    userId: data.voUserId?.toString() || '0',
    currentExp: data.voCurrentExp,
    currentLevel: data.voCurrentLevel,
    nextLevelExp: data.voNextLevelExp,
    totalExp: data.voTotalExp,
    levelProgress: data.voLevelProgress,
    levelName: data.voLevelName,
    levelDescription: data.voLevelDescription,
    canLevelUp: data.voCanLevelUp,
    nextLevelName: data.voNextLevelName,
    expToNextLevel: data.voExpToNextLevel,
    expGainedToday: data.voExpGainedToday,
    dailyExpLimit: data.voDailyExpLimit,
    remainingDailyExp: data.voRemainingDailyExp,
    isMaxLevel: data.voIsMaxLevel,
    rank: data.voRank,
    percentile: data.voPercentile,
    createTime: data.voCreateTime,
    updateTime: data.voUpdateTime,
    themeColor: '#3b82f6',
    currentLevelName: data.voLevelName,
    nextLevel: (data.voCurrentLevel || 1) + 1,
    expFrozen: data.voExpFrozen,
  };
}

/**
 * 为 ExpTransactionData 添加兼容字段
 */
function addTransactionCompatFields(data: ExpTransactionData): ExpTransactionData {
  return {
    ...data,
    id: data.voId,
    expType: data.voTransactionType,
    expAmount: data.voExpChange,
    remark: data.voDescription,
    createTime: data.voCreateTime,
  };
}

/**
 * 为 LeaderboardItemData 添加兼容字段
 */
function addLeaderboardCompatFields(data: LeaderboardItemData): LeaderboardItemData {
  return {
    ...data,
    userId: data.voUserId,
    userName: data.voUserName,
    avatar: data.voAvatarUrl || '',
    currentLevel: data.voCurrentLevel,
    totalExp: data.voTotalExp,
    levelName: data.voLevelName,
    rank: data.voRank,
    expGainedThisWeek: data.voExpGainedThisWeek,
    expGainedThisMonth: data.voExpGainedThisMonth,
    isCurrentUser: data.voIsCurrentUser,
    themeColor: data.voThemeColor || '#3b82f6',
    currentLevelName: data.voCurrentLevelName || data.voLevelName,
  };
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
      return addExperienceCompatFields(response.data);
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
      return {
        ...response.data,
        data: response.data.data.map(addTransactionCompatFields),
      };
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
      return {
        ...response.data,
        data: response.data.data.map(addLeaderboardCompatFields),
      };
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
