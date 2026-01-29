/**
 * 商城系统相关的 API 调用
 */

import { apiGet, apiPost, configureApiClient, type ParsedApiResponse } from '@radish/ui';
import type { TFunction } from 'i18next';
import {
  mapProductCategory,
  mapProduct,
  mapProductListItem,
  mapOrder,
  mapOrderListItem,
  mapUserBenefit,
  mapUserInventoryItem,
  type ProductCategoryData,
  type ProductData,
  type ProductListItemData,
  type OrderData,
  type OrderListItemData,
  type UserBenefitData,
  type UserInventoryItemData
} from '@/utils/viewModelMapper';

// 配置 API 客户端
const defaultApiBase = 'https://localhost:5000';
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL as string | undefined || defaultApiBase;

configureApiClient({
  baseUrl: apiBaseUrl.replace(/\/$/, ''),
});

// ==================== 类型重导出 ====================

// 从 viewModelMapper 重导出类型，保持向后兼容
export type {
  ProductCategoryData as ProductCategory,
  ProductListItemData as ProductListItem,
  ProductData as Product,
  OrderListItemData as OrderListItem,
  OrderData as Order,
  UserBenefitData as UserBenefit,
  UserInventoryItemData as UserInventoryItem
};

// ==================== 枚举常量（用于比较） ====================

/**
 * 商品类型枚举值（字符串形式，用于与后端返回值比较）
 */
export const ProductType = {
  Benefit: 'Benefit',       // 权益
  Consumable: 'Consumable', // 消耗品
  Physical: 'Physical'      // 实物
} as const;

export type ProductTypeValue = typeof ProductType[keyof typeof ProductType];

/**
 * 订单状态枚举值（字符串形式，用于与后端返回值比较）
 */
export const OrderStatus = {
  Pending: 'Pending',       // 待支付
  Paid: 'Paid',             // 已支付
  Completed: 'Completed',   // 已完成
  Cancelled: 'Cancelled',   // 已取消
  Refunded: 'Refunded',     // 已退款
  Failed: 'Failed'          // 发放失败
} as const;

export type OrderStatusValue = typeof OrderStatus[keyof typeof OrderStatus];

/**
 * 库存类型枚举值（字符串形式）
 */
export const StockType = {
  Unlimited: 'Unlimited', // 无限
  Limited: 'Limited'      // 限量
} as const;

export type StockTypeValue = typeof StockType[keyof typeof StockType];

/**
 * 分页响应
 */
export interface PagedResponse<T> {
  page: number;
  pageSize: number;
  dataCount: number;
  pageCount: number;
  data: T[];
}

/**
 * 创建订单请求
 */
export interface CreateOrderRequest {
  productId: number;
  quantity?: number;
  userRemark?: string;
}

/**
 * 购买结果
 */
export interface PurchaseResult {
  success: boolean;
  orderId?: number;
  orderNo?: string;
  errorMessage?: string;
  userBenefitId?: number;
  deductedCoins?: number;
  remainingBalance?: number;
}

/**
 * 使用道具请求
 */
export interface UseItemRequest {
  inventoryId: number;
  quantity?: number;
  targetId?: number;
}

/**
 * 使用道具结果
 */
export interface UseItemResult {
  success: boolean;
  errorMessage?: string;
  remainingQuantity: number;
  effectDescription?: string;
}

// ==================== API 方法 ====================

/**
 * 获取商品分类列表
 */
export async function getCategories(t: TFunction): Promise<ParsedApiResponse<ProductCategoryData[]>> {
  const response = await apiGet<any[]>('/api/v1/Shop/GetCategories');

  if (!response.ok || !response.data) {
    return response as ParsedApiResponse<ProductCategoryData[]>;
  }

  return {
    ...response,
    data: response.data.map(mapProductCategory)
  };
}

/**
 * 获取分类详情
 */
export async function getCategory(categoryId: string, t: TFunction): Promise<ParsedApiResponse<ProductCategoryData>> {
  const response = await apiGet<any>(`/api/v1/Shop/GetCategory/${encodeURIComponent(categoryId)}`);

  if (!response.ok || !response.data) {
    return response as ParsedApiResponse<ProductCategoryData>;
  }

  return {
    ...response,
    data: mapProductCategory(response.data)
  };
}

/**
 * 获取商品列表
 */
export async function getProducts(
  t: TFunction,
  categoryId?: string,
  productType?: ProductTypeValue,
  keyword?: string,
  pageIndex: number = 1,
  pageSize: number = 20
): Promise<ParsedApiResponse<PagedResponse<ProductListItemData>>> {
  const params = new URLSearchParams({
    pageIndex: pageIndex.toString(),
    pageSize: pageSize.toString()
  });

  if (categoryId) {
    params.append('categoryId', categoryId);
  }

  if (productType !== undefined) {
    params.append('productType', productType);
  }

  if (keyword) {
    params.append('keyword', keyword);
  }

  const response = await apiGet<PagedResponse<any>>(`/api/v1/Shop/GetProducts?${params.toString()}`);

  if (!response.ok || !response.data) {
    return response as ParsedApiResponse<PagedResponse<ProductListItemData>>;
  }

  return {
    ...response,
    data: {
      ...response.data,
      data: response.data.data.map(mapProductListItem)
    }
  };
}

/**
 * 获取商品详情
 */
export async function getProduct(productId: number, t: TFunction): Promise<ParsedApiResponse<ProductData>> {
  const response = await apiGet<any>(`/api/v1/Shop/GetProduct/${productId}`);

  if (!response.ok || !response.data) {
    return response as ParsedApiResponse<ProductData>;
  }

  return {
    ...response,
    data: mapProduct(response.data)
  };
}

/**
 * 检查是否可以购买商品
 */
export async function checkCanBuy(productId: number, quantity: number = 1, t: TFunction) {
  return await apiGet<{ canBuy: boolean; reason: string }>(
    `/api/v1/Shop/CheckCanBuy/${productId}?quantity=${quantity}`,
    { withAuth: true }
  );
}

/**
 * 购买商品
 */
export async function purchaseProduct(request: CreateOrderRequest, t: TFunction) {
  return await apiPost<PurchaseResult>('/api/v1/Shop/Purchase', request, { withAuth: true });
}

/**
 * 获取我的订单列表
 */
export async function getMyOrders(
  t: TFunction,
  status?: OrderStatusValue,
  pageIndex: number = 1,
  pageSize: number = 20
): Promise<ParsedApiResponse<PagedResponse<OrderListItemData>>> {
  const params = new URLSearchParams({
    pageIndex: pageIndex.toString(),
    pageSize: pageSize.toString()
  });

  if (status !== undefined) {
    params.append('status', status);
  }

  const response = await apiGet<PagedResponse<any>>(
    `/api/v1/Shop/GetMyOrders?${params.toString()}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    return response as ParsedApiResponse<PagedResponse<OrderListItemData>>;
  }

  return {
    ...response,
    data: {
      ...response.data,
      data: response.data.data.map(mapOrderListItem)
    }
  };
}

/**
 * 获取订单详情
 */
export async function getOrder(orderId: number, t: TFunction): Promise<ParsedApiResponse<OrderData>> {
  const response = await apiGet<any>(`/api/v1/Shop/GetOrder/${orderId}`, { withAuth: true });

  if (!response.ok || !response.data) {
    return response as ParsedApiResponse<OrderData>;
  }

  return {
    ...response,
    data: mapOrder(response.data)
  };
}

/**
 * 取消订单
 */
export async function cancelOrder(orderId: number, t: TFunction, reason?: string) {
  const body = reason ? { reason } : undefined;
  return await apiPost<boolean>(`/api/v1/Shop/CancelOrder/${orderId}`, body, { withAuth: true });
}

/**
 * 获取我的权益列表
 */
export async function getMyBenefits(includeExpired: boolean = false, t: TFunction): Promise<ParsedApiResponse<UserBenefitData[]>> {
  const response = await apiGet<any[]>(
    `/api/v1/Shop/GetMyBenefits?includeExpired=${includeExpired}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    return response as ParsedApiResponse<UserBenefitData[]>;
  }

  return {
    ...response,
    data: response.data.map(mapUserBenefit)
  };
}

/**
 * 获取我的激活权益
 */
export async function getMyActiveBenefits(t: TFunction): Promise<ParsedApiResponse<UserBenefitData[]>> {
  const response = await apiGet<any[]>('/api/v1/Shop/GetMyActiveBenefits', { withAuth: true });

  if (!response.ok || !response.data) {
    return response as ParsedApiResponse<UserBenefitData[]>;
  }

  return {
    ...response,
    data: response.data.map(mapUserBenefit)
  };
}

/**
 * 激活权益
 */
export async function activateBenefit(benefitId: number, t: TFunction) {
  return await apiPost<boolean>(`/api/v1/Shop/ActivateBenefit/${benefitId}`, undefined, { withAuth: true });
}

/**
 * 取消激活权益
 */
export async function deactivateBenefit(benefitId: number, t: TFunction) {
  return await apiPost<boolean>(`/api/v1/Shop/DeactivateBenefit/${benefitId}`, undefined, { withAuth: true });
}

/**
 * 获取我的背包
 */
export async function getMyInventory(t: TFunction): Promise<ParsedApiResponse<UserInventoryItemData[]>> {
  const response = await apiGet<any[]>('/api/v1/Shop/GetMyInventory', { withAuth: true });

  if (!response.ok || !response.data) {
    return response as ParsedApiResponse<UserInventoryItemData[]>;
  }

  return {
    ...response,
    data: response.data.map(mapUserInventoryItem)
  };
}

/**
 * 使用道具
 */
export async function useItem(request: UseItemRequest, t: TFunction) {
  return await apiPost<UseItemResult>('/api/v1/Shop/UseItem', request, { withAuth: true });
}

/**
 * 使用改名卡
 */
export async function useRenameCard(inventoryId: number, newNickname: string, t: TFunction) {
  return await apiPost<UseItemResult>(
    `/api/v1/Shop/UseRenameCard/${inventoryId}?newNickname=${encodeURIComponent(newNickname)}`,
    undefined,
    { withAuth: true }
  );
}

/**
 * 格式化商品价格显示
 */
export function formatProductPrice(price: number, originalPrice?: number): string {
  const formatPrice = (p: number) => `${p.toLocaleString()} 胡萝卜`;

  if (originalPrice && originalPrice > price) {
    return `${formatPrice(price)} (原价 ${formatPrice(originalPrice)})`;
  }

  return formatPrice(price);
}

/**
 * 获取商品类型显示名称
 */
export function getProductTypeDisplay(type: string): string {
  switch (type) {
    case ProductType.Benefit:
      return '权益';
    case ProductType.Consumable:
      return '消耗品';
    case ProductType.Physical:
      return '实物';
    default:
      return type || '未知';
  }
}

/**
 * 获取订单状态显示名称
 */
export function getOrderStatusDisplay(status: string): string {
  switch (status) {
    case OrderStatus.Pending:
      return '待支付';
    case OrderStatus.Paid:
      return '已支付';
    case OrderStatus.Completed:
      return '已完成';
    case OrderStatus.Cancelled:
      return '已取消';
    case OrderStatus.Refunded:
      return '已退款';
    case OrderStatus.Failed:
      return '发放失败';
    default:
      return status || '未知';
  }
}

/**
 * 获取订单状态颜色
 */
export function getOrderStatusColor(status: string): string {
  switch (status) {
    case OrderStatus.Pending:
      return '#faad14'; // 橙色
    case OrderStatus.Paid:
      return '#1890ff'; // 蓝色
    case OrderStatus.Completed:
      return '#52c41a'; // 绿色
    case OrderStatus.Cancelled:
      return '#8c8c8c'; // 灰色
    case OrderStatus.Refunded:
      return '#722ed1'; // 紫色
    case OrderStatus.Failed:
      return '#f5222d'; // 红色
    default:
      return '#8c8c8c';
  }
}