/**
 * 排行榜系统相关的 API 调用
 * 直接使用后端 Vo 字段名，无需映射
 */

import { apiGet, configureApiClient, type PagedResponse } from '@radish/ui';
import { getApiBaseUrl } from '@/config/env';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

/**
 * 排行榜类型枚举
 */
export enum LeaderboardType {
  /** 等级排行榜 */
  Experience = 1,
  /** 萝卜余额排行榜 */
  Balance = 2,
  /** 萝卜花销排行榜 */
  TotalSpent = 3,
  /** 购买达人排行榜 */
  PurchaseCount = 4,
  /** 热门商品排行榜 */
  HotProduct = 5,
  /** 发帖达人排行榜 */
  PostCount = 6,
  /** 评论达人排行榜 */
  CommentCount = 7,
  /** 人气排行榜 */
  Popularity = 8,
}

/**
 * 排行榜分类枚举
 */
export enum LeaderboardCategory {
  /** 用户类排行榜 */
  User = 1,
  /** 商品类排行榜 */
  Product = 2,
}

/**
 * 排行榜类型 Vo
 */
export interface LeaderboardTypeData {
  voType: LeaderboardType;
  voCategory: LeaderboardCategory;
  voName: string;
  voDescription: string;
  voIcon: string;
  voPrimaryLabel: string;
  voSortOrder: number;
}

/**
 * 统一排行榜项目 Vo
 */
export interface UnifiedLeaderboardItemData {
  // 通用字段
  voLeaderboardType: LeaderboardType;
  voCategory: LeaderboardCategory;
  voRank: number;

  // 用户信息（用户类排行榜）
  voUserId?: number;
  voUserName?: string;
  voAvatarUrl?: string;
  voCurrentLevel?: number;
  voCurrentLevelName?: string;
  voThemeColor?: string;
  voIsCurrentUser: boolean;

  // 商品信息（商品类排行榜）
  voProductId?: number;
  voProductName?: string;
  voProductIcon?: string;
  voProductPrice?: number;

  // 统计数值
  voPrimaryValue: number;
  voPrimaryLabel: string;
  voSecondaryValue?: number;
  voSecondaryLabel?: string;
}

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
