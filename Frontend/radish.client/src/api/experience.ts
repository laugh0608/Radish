/**
 * 经验值系统相关的 API 调用
 * 直接使用后端 Vo 字段名，无需映射
 */

import {
  apiGet,
  configureApiClient,
  createApiResponseError,
  type PagedResponse,
  type ParsedApiResponse,
} from '@radish/http';
import type { TFunction } from 'i18next';
import { getApiBaseUrl } from '@/config/env';
import type { LongId } from './user';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

/**
 * 用户经验值 Vo（直接使用后端字段名）
 */
export interface ExperienceData {
  voUserId: LongId;
  voUserName?: string;
  voAvatarUrl?: string;
  voCurrentLevel: number;
  voCurrentLevelName: string;  // 后端字段名
  voCurrentExp: LongId;  // 当前等级内的经验值进度，后端 long 按字符串返回
  voTotalExp: LongId;  // 累计总经验值，后端 long 按字符串返回
  voExpToNextLevel: LongId;  // 距离下一级还需多少经验，后端 long 按字符串返回
  voNextLevel: number;
  voNextLevelName: string;
  voLevelProgress: number;  // 0-1 之间的小数
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
  voId: LongId;
  voUserId: LongId;
  voUserName?: string;
  voExpType: string;  // 后端字段名
  /** 后端兼容展示字段；client 的控制与本地化必须只使用稳定 voExpType。 */
  voExpTypeDisplay: string;
  voExpAmount: number;  // 后端字段名（经验值变动量）
  voBusinessType?: string;  // 后端字段名
  voBusinessId?: LongId;  // 后端字段名
  voRemark?: string;  // 后端字段名（备注）
  voExpBefore: LongId;
  voExpAfter: LongId;
  voLevelBefore: number;
  voLevelAfter: number;
  voIsLevelUp: boolean;  // 后端计算属性
  voCreateTime: string;
}

/**
 * 排行榜项目 Vo（直接使用后端字段名）
 */
export interface LeaderboardItemData {
  voUserId: LongId;
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

function ensureRequiredResponse<T>(response: ParsedApiResponse<T>, fallbackMessage: string): T {
  if (!response.ok || response.data === undefined) {
    throw createApiResponseError(
      response.messageKey ? response : { ...response, message: undefined },
      fallbackMessage,
    );
  }

  return response.data;
}

/**
 * 经验值 API
 */
export const experienceApi = {
  /**
   * 获取我的经验值信息
   */
  async getMyExperience(t: TFunction): Promise<ExperienceData> {
    const response = await apiGet<ExperienceData>('/api/v1/Experience/GetMyExperience', {
      withAuth: true,
    });
    return ensureRequiredResponse(response, t('experience.api.loadFailed'));
  },

  /**
   * 获取经验值交易记录
   */
  async getTransactions(params: {
    pageIndex?: number;
    pageSize?: number;
  }, t: TFunction): Promise<PagedResponse<ExpTransactionData>> {
    const { pageIndex = 1, pageSize = 20 } = params;

    const response = await apiGet<PagedResponse<ExpTransactionData>>(
      `/api/v1/Experience/GetTransactions?pageIndex=${pageIndex}&pageSize=${pageSize}`,
      { withAuth: true }
    );

    return ensureRequiredResponse(response, t('experience.api.transactionsFailed'));
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
