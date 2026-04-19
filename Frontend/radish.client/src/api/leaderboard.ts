/**
 * 排行榜系统相关的 API 调用
 * 直接使用后端 Vo 字段名，无需映射
 */

import { apiGet, configureApiClient, type PagedResponse } from '@radish/http';
import { getApiBaseUrl } from '@/config/env';
import {
  LeaderboardType,
  type LeaderboardTypeData,
  type UnifiedLeaderboardItemData,
} from './leaderboard.contract';

export {
  LeaderboardCategory,
  LeaderboardType,
  type LeaderboardCategory as LeaderboardCategoryValue,
  type LeaderboardLongId,
  type LeaderboardType as LeaderboardTypeValue,
  type LeaderboardTypeData,
  type UnifiedLeaderboardItemData,
} from './leaderboard.contract';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

/**
 * 排行榜 API
 */
export const leaderboardApi = {
  /**
   * 获取排行榜数据
   */
  async getLeaderboard(
    type: LeaderboardType = LeaderboardType.Experience,
    pageIndex: number = 1,
    pageSize: number = 50
  ): Promise<PagedResponse<UnifiedLeaderboardItemData> | null> {
    const response = await apiGet<PagedResponse<UnifiedLeaderboardItemData>>(
      `/api/v1/Leaderboard/GetLeaderboard?type=${type}&pageIndex=${pageIndex}&pageSize=${pageSize}`,
      { withAuth: true }
    );

    if (response.ok && response.data) {
      return response.data;
    }
    return null;
  },

  /**
   * 获取当前用户在指定排行榜中的排名
   */
  async getMyRank(type: LeaderboardType = LeaderboardType.Experience): Promise<number | null> {
    const response = await apiGet<number>(`/api/v1/Leaderboard/GetMyRank?type=${type}`, {
      withAuth: true,
    });

    return response.ok && response.data !== undefined ? response.data : null;
  },

  /**
   * 获取所有排行榜类型
   */
  async getTypes(): Promise<LeaderboardTypeData[] | null> {
    const response = await apiGet<LeaderboardTypeData[]>('/api/v1/Leaderboard/GetTypes', {
      withAuth: true,
    });

    if (response.ok && response.data) {
      return response.data;
    }
    return null;
  },
};
