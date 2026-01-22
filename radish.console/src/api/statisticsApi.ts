/**
 * 统计报表 API 客户端
 */

import { apiGet } from '@radish/ui';

/**
 * 仪表盘统计数据类型
 */
export interface DashboardStats {
  totalUsers: number;
  totalOrders: number;
  totalProducts: number;
  totalRevenue: number;
}

/**
 * 订单趋势数据类型
 */
export interface OrderTrendItem {
  date: string;
  orderCount: number;
  revenue: number;
}

/**
 * 商品销售排行数据类型
 */
export interface ProductSalesItem {
  productName: string;
  salesCount: number;
  revenue: number;
}

/**
 * 用户等级分布数据类型
 */
export interface UserLevelDistributionItem {
  level: number;
  levelName: string;
  userCount: number;
}

/**
 * 获取仪表盘统计数据
 */
export async function getDashboardStats(): Promise<DashboardStats> {
  const response = await apiGet<DashboardStats>('/api/v1/Statistics/GetDashboardStats', { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取统计数据失败');
  }

  return {
    totalUsers: response.data.totalUsers || 0,
    totalOrders: response.data.totalOrders || 0,
    totalProducts: response.data.totalProducts || 0,
    totalRevenue: response.data.totalRevenue || 0,
  };
}

/**
 * 获取订单趋势数据
 */
export async function getOrderTrend(days: number = 30): Promise<OrderTrendItem[]> {
  const response = await apiGet<OrderTrendItem[]>(`/api/v1/Statistics/GetOrderTrend?days=${days}`, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取订单趋势失败');
  }

  return response.data.map((item: any) => ({
    date: item.date || '',
    orderCount: item.orderCount || 0,
    revenue: item.revenue || 0,
  }));
}

/**
 * 获取商品销售排行
 */
export async function getProductSalesRanking(limit: number = 10): Promise<ProductSalesItem[]> {
  const response = await apiGet<ProductSalesItem[]>(`/api/v1/Statistics/GetProductSalesRanking?limit=${limit}`, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取商品销售排行失败');
  }

  return response.data.map((item: any) => ({
    productName: item.productName || '',
    salesCount: item.salesCount || 0,
    revenue: item.revenue || 0,
  }));
}

/**
 * 获取用户等级分布
 */
export async function getUserLevelDistribution(): Promise<UserLevelDistributionItem[]> {
  const response = await apiGet<UserLevelDistributionItem[]>('/api/v1/Statistics/GetUserLevelDistribution', { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取用户等级分布失败');
  }

  return response.data.map((item: any) => ({
    level: item.level || 0,
    levelName: item.levelName || '',
    userCount: item.userCount || 0,
  }));
}