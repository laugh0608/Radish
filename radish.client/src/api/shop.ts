/**
 * 商城系统相关的 API 调用
 * 直接使用后端 Vo 字段名，不进行映射
 */

import { apiGet, apiPost, configureApiClient, type ParsedApiResponse } from '@radish/http';
import type { TFunction } from 'i18next';
import type {
  ProductCategory,
  ProductListItem,
  Product,
  OrderListItem,
  Order,
  UserBenefit,
  UserInventoryItem,
  PagedResponse,
  CreateOrderRequest,
  PurchaseResult,
  UseItemRequest,
  UseItemResult
} from '@/types/shop';
import { getApiBaseUrl } from '@/config/env';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

// ==================== 类型重导出 ====================

export type {
  ProductCategory,
  ProductListItem,
  Product,
  OrderListItem,
  Order,
  UserBenefit,
  UserInventoryItem,
  PagedResponse,
  CreateOrderRequest,
  PurchaseResult,
  UseItemRequest,
  UseItemResult
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

// ==================== API 方法 ====================

/**
 * 获取商品分类列表
 */
export async function getCategories(t: TFunction): Promise<ParsedApiResponse<ProductCategory[]>> {
  return await apiGet<ProductCategory[]>('/api/v1/Shop/GetCategories');
}

/**
 * 获取分类详情
 */
export async function getCategory(categoryId: string, t: TFunction): Promise<ParsedApiResponse<ProductCategory>> {
  return await apiGet<ProductCategory>(`/api/v1/Shop/GetCategory/${encodeURIComponent(categoryId)}`);
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
): Promise<ParsedApiResponse<PagedResponse<ProductListItem>>> {
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

  return await apiGet<PagedResponse<ProductListItem>>(`/api/v1/Shop/GetProducts?${params.toString()}`);
}

/**
 * 获取商品详情
 */
export async function getProduct(productId: number, t: TFunction): Promise<ParsedApiResponse<Product>> {
  return await apiGet<Product>(`/api/v1/Shop/GetProduct/${productId}`);
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
): Promise<ParsedApiResponse<PagedResponse<OrderListItem>>> {
  const params = new URLSearchParams({
    pageIndex: pageIndex.toString(),
    pageSize: pageSize.toString()
  });

  if (status !== undefined) {
    params.append('status', status);
  }

  return await apiGet<PagedResponse<OrderListItem>>(
    `/api/v1/Shop/GetMyOrders?${params.toString()}`,
    { withAuth: true }
  );
}

/**
 * 获取订单详情
 */
export async function getOrder(orderId: number, t: TFunction): Promise<ParsedApiResponse<Order>> {
  return await apiGet<Order>(`/api/v1/Shop/GetOrder/${orderId}`, { withAuth: true });
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
export async function getMyBenefits(includeExpired: boolean = false, t: TFunction): Promise<ParsedApiResponse<UserBenefit[]>> {
  return await apiGet<UserBenefit[]>(
    `/api/v1/Shop/GetMyBenefits?includeExpired=${includeExpired}`,
    { withAuth: true }
  );
}

/**
 * 获取我的激活权益
 */
export async function getMyActiveBenefits(t: TFunction): Promise<ParsedApiResponse<UserBenefit[]>> {
  return await apiGet<UserBenefit[]>('/api/v1/Shop/GetMyActiveBenefits', { withAuth: true });
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
export async function getMyInventory(t: TFunction): Promise<ParsedApiResponse<UserInventoryItem[]>> {
  return await apiGet<UserInventoryItem[]>('/api/v1/Shop/GetMyInventory', { withAuth: true });
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
