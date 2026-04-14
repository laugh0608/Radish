/**
 * 排行榜领域常量与数据契约
 * 保持纯数据定义，避免公开路由状态依赖 API 客户端实现。
 */

/**
 * 排行榜类型枚举
 */
export const LeaderboardType = {
  /** 等级排行榜 */
  Experience: 1,
  /** 萝卜余额排行榜 */
  Balance: 2,
  /** 萝卜花销排行榜 */
  TotalSpent: 3,
  /** 购买达人排行榜 */
  PurchaseCount: 4,
  /** 热门商品排行榜 */
  HotProduct: 5,
  /** 发帖达人排行榜 */
  PostCount: 6,
  /** 评论达人排行榜 */
  CommentCount: 7,
  /** 人气排行榜 */
  Popularity: 8,
} as const;

export type LeaderboardType = (typeof LeaderboardType)[keyof typeof LeaderboardType];

/**
 * 排行榜分类枚举
 */
export const LeaderboardCategory = {
  /** 用户类排行榜 */
  User: 1,
  /** 商品类排行榜 */
  Product: 2,
} as const;

export type LeaderboardCategory = (typeof LeaderboardCategory)[keyof typeof LeaderboardCategory];

export type LeaderboardLongId = number | string;

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
  voUserId?: LeaderboardLongId;
  voUserName?: string;
  voAvatarUrl?: string;
  voCurrentLevel?: number;
  voCurrentLevelName?: string;
  voThemeColor?: string;
  voIsCurrentUser: boolean;

  // 商品信息（商品类排行榜）
  voProductId?: LeaderboardLongId;
  voProductName?: string;
  voProductIcon?: string;
  voProductPrice?: number;

  // 统计数值
  voPrimaryValue: number;
  voPrimaryLabel: string;
  voSecondaryValue?: number;
  voSecondaryLabel?: string;
}
