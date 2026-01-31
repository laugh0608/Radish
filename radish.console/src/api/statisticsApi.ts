/**
 * 统计报表 API 客户端
 * 使用后端 Vo 字段名
 */

import { apiGet } from '@radish/ui';

/**
 * 仪表盘统计数据类型（使用 Vo 前缀）
 */
export interface DashboardStatsVo {
  voTotalUsers: number;
  voTotalOrders: number;
  voTotalProducts: number;
  voTotalRevenue: number;
}

/**
 * 订单趋势数据类型（使用 Vo 前缀）
 */
export interface OrderTrendItemVo {
  voDate: string;
  voOrderCount: number;
  voRevenue: number;
}

/**
 * 商品销售排行数据类型（使用 Vo 前缀）
 */
export interface ProductSalesRankingVo {
  voProductName: string;
  voSalesCount: number;
  voRevenue: number;
}

/**
 * 用户等级分布数据类型（使用 Vo 前缀）
 */
export interface UserLevelDistributionVo {
  voLevel: number;
  voLevelName: string;
  voUserCount: number;
}

/**
 * 获取仪表盘统计数据
 */
export async function getDashboardStats(): Promise<DashboardStatsVo> {
  const response = await apiGet<DashboardStatsVo>('/api/v1/Statistics/GetDashboardStats', { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取统计数据失败');
  }

  return response.data;
}

/**
 * 获取订单趋势数据
 */
export async function getOrderTrend(days: number = 30): Promise<OrderTrendItemVo[]> {
  const response = await apiGet<OrderTrendItemVo[]>(`/api/v1/Statistics/GetOrderTrend?days=${days}`, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取订单趋势失败');
  }

  return response.data;
}

/**
 * 获取商品销售排行
 */
export async function getProductSalesRanking(limit: number = 10): Promise<ProductSalesRankingVo[]> {
  const response = await apiGet<ProductSalesRankingVo[]>(`/api/v1/Statistics/GetProductSalesRanking?limit=${limit}`, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取商品销售排行失败');
  }

  return response.data;
}

/**
 * 获取用户等级分布
 */
export async function getUserLevelDistribution(): Promise<UserLevelDistributionVo[]> {
  const response = await apiGet<UserLevelDistributionVo[]>('/api/v1/Statistics/GetUserLevelDistribution', { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取用户等级分布失败');
  }

  return response.data;
}