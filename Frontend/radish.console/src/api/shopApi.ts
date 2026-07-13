/**
 * 商城管理 API 客户端
 * 直接使用后端 Vo 字段名，无需映射
 */

import { apiGet, apiPost, apiPut, apiDelete, configureApiClient } from '@radish/http';
import type {
  PagedResponse,
  Product,
  ProductCategory,
  CreateProductDto,
  UpdateProductDto,
  Order,
  ShopEntitlementOperation,
  UserBenefit,
  UserBenefitActionResult,
} from './types';
import {
  ProductType,
  OrderStatus,
  ConsumableType,
  BenefitType,
} from './types';
import { getApiBaseUrl } from '@/config/env';

// 配置 API 客户端
configureApiClient({
  baseUrl: getApiBaseUrl(),
});

// ==================== 商品分类 API ====================

/**
 * 获取商品分类列表
 */
export async function getCategories(): Promise<ProductCategory[]> {
  const response = await apiGet<ProductCategory[]>('/api/v1/Shop/GetCategories', { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取分类列表失败');
  }

  return response.data;
}

/**
 * 获取分类详情
 */
export async function getCategory(categoryId: string): Promise<ProductCategory> {
  const response = await apiGet<ProductCategory>(
    `/api/v1/Shop/GetCategory/${encodeURIComponent(categoryId)}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取分类详情失败');
  }

  return response.data;
}

// ==================== 商品管理 API ====================

/**
 * 获取商品列表（管理后台）
 */
export async function adminGetProducts(params: {
  categoryId?: string;
  productType?: ProductType;
  isOnSale?: boolean;
  keyword?: string;
  pageIndex?: number;
  pageSize?: number;
}): Promise<PagedResponse<Product>> {
  const searchParams = new URLSearchParams();

  if (params.categoryId) searchParams.append('categoryId', params.categoryId);
  if (params.productType !== undefined) searchParams.append('productType', params.productType.toString());
  if (params.isOnSale !== undefined) searchParams.append('isOnSale', params.isOnSale.toString());
  if (params.keyword) searchParams.append('keyword', params.keyword);
  searchParams.append('pageIndex', (params.pageIndex || 1).toString());
  searchParams.append('pageSize', (params.pageSize || 20).toString());

  const response = await apiGet<PagedResponse<Product>>(
    `/api/v1/Shop/AdminGetProducts?${searchParams.toString()}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取商品列表失败');
  }

  return response.data;
}

/**
 * 获取商品详情（管理后台）
 */
export async function adminGetProduct(productId: string): Promise<Product> {
  const response = await apiGet<Product>(
    `/api/v1/Shop/AdminGetProduct/${encodeURIComponent(String(productId))}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取商品详情失败');
  }

  return response.data;
}

/**
 * 创建商品
 */
export async function createProduct(product: CreateProductDto): Promise<string> {
  const response = await apiPost<string>('/api/v1/Shop/CreateProduct', product, { withAuth: true });

  if (!response.ok || response.data === undefined) {
    throw new Error(response.message || '创建商品失败');
  }

  return response.data;
}

/**
 * 更新商品
 */
export async function updateProduct(product: UpdateProductDto): Promise<void> {
  const response = await apiPut<null>('/api/v1/Shop/UpdateProduct', product, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '更新商品失败');
  }
}

/**
 * 删除商品
 */
export async function deleteProduct(productId: string): Promise<void> {
  const response = await apiDelete<null>(
    `/api/v1/Shop/DeleteProduct/${encodeURIComponent(String(productId))}`,
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '删除商品失败');
  }
}

// ==================== 订单管理 API ====================

/**
 * 获取订单列表（管理后台）
 */
export async function adminGetOrders(params: {
  status?: OrderStatus;
  pageIndex?: number;
  pageSize?: number;
  userId?: string;
  productId?: string;
  orderNo?: string;
}): Promise<PagedResponse<Order>> {
  const searchParams = new URLSearchParams();

  if (params.status !== undefined) searchParams.append('status', params.status.toString());
  if (params.userId) searchParams.append('userId', params.userId.toString());
  if (params.productId) searchParams.append('productId', params.productId.toString());
  if (params.orderNo) searchParams.append('orderNo', params.orderNo);
  searchParams.append('pageIndex', (params.pageIndex || 1).toString());
  searchParams.append('pageSize', (params.pageSize || 20).toString());

  const response = await apiGet<PagedResponse<Order>>(
    `/api/v1/Shop/AdminGetOrders?${searchParams.toString()}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取订单列表失败');
  }

  return response.data;
}

/**
 * 获取订单详情（管理后台）
 */
export async function adminGetOrder(orderId: string): Promise<Order> {
  const response = await apiGet<Order>(
    `/api/v1/Shop/AdminGetOrder/${encodeURIComponent(String(orderId))}`,
    { withAuth: true }
  );

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取订单详情失败');
  }

  return response.data;
}

/** 获取指定用户最近的持续权益与消耗品业务流水。 */
export async function adminGetEntitlementOperations(params: {
  userId: string;
  operationType?: string;
  benefitType?: BenefitType;
  consumableType?: ConsumableType;
  pageIndex?: number;
  pageSize?: number;
}): Promise<PagedResponse<ShopEntitlementOperation>> {
  const searchParams = new URLSearchParams({
    userId: params.userId,
    pageIndex: (params.pageIndex || 1).toString(),
    pageSize: (params.pageSize || 20).toString(),
  });
  if (params.consumableType !== undefined) {
    searchParams.set('consumableType', params.consumableType.toString());
  }
  if (params.operationType) {
    searchParams.set('operationType', params.operationType);
  }
  if (params.benefitType !== undefined) {
    searchParams.set('benefitType', params.benefitType.toString());
  }

  const response = await apiGet<PagedResponse<ShopEntitlementOperation>>(
    `/api/v1/Shop/AdminGetEntitlementOperations?${searchParams.toString()}`,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取商城权益流水失败');
  }

  return response.data;
}

/** 获取指定用户的全部持续权益及服务端 UTC 状态。 */
export async function adminGetUserBenefits(userId: string): Promise<UserBenefit[]> {
  const response = await apiGet<UserBenefit[]>(
    `/api/v1/Shop/AdminGetUserBenefits?userId=${encodeURIComponent(userId)}`,
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取用户持续权益失败');
  }

  return response.data;
}

/** 撤销一份持续权益。 */
export async function adminRevokeBenefit(
  benefitId: string,
  reason: string
): Promise<UserBenefitActionResult> {
  const response = await apiPost<UserBenefitActionResult>(
    `/api/v1/Shop/AdminRevokeBenefit/${encodeURIComponent(benefitId)}`,
    { reason },
    { withAuth: true }
  );
  if (!response.ok || !response.data) {
    throw new Error(response.message || '撤销持续权益失败');
  }

  return response.data;
}

// ==================== 工具函数 ====================

/**
 * 获取订单状态颜色
 */
export function getOrderStatusColor(status: string): string {
  switch (status) {
    case 'Pending':
      return '#faad14'; // 橙色
    case 'Paid':
      return '#52c41a'; // 绿色
    case 'Completed':
      return '#52c41a'; // 绿色
    case 'Cancelled':
      return '#ff4d4f'; // 红色
    case 'Refunded':
      return '#722ed1'; // 紫色
    case 'Failed':
      return '#ff4d4f'; // 红色
    default:
      return '#d9d9d9'; // 灰色
  }
}

/**
 * 获取订单状态显示文本
 */
export function getOrderStatusDisplay(status: string): string {
  switch (status) {
    case 'Pending':
      return '待付款';
    case 'Paid':
      return '已付款';
    case 'Completed':
      return '已完成';
    case 'Cancelled':
      return '已取消';
    case 'Refunded':
      return '已退款';
    case 'Failed':
      return '发放失败';
    default:
      return '未知状态';
  }
}

/**
 * 获取商品类型显示文本
 */
export function getProductTypeDisplay(type: string): string {
  switch (type) {
    case 'Benefit':
      return '权益商品';
    case 'Consumable':
      return '消耗品';
    case 'Physical':
      return '实体商品';
    default:
      return '未知类型';
  }
}

/**
 * 重试发放权益
 */
export async function retryGrantBenefit(orderId: string): Promise<void> {
  const response = await apiPost<null>(
    `/api/v1/Shop/RetryGrantBenefit/${encodeURIComponent(String(orderId))}`,
    {},
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '重试发放权益失败');
  }
}

/**
 * 管理员备注订单
 */
export async function adminRemarkOrder(orderId: string, remark: string): Promise<void> {
  const response = await apiPost<null>(
    `/api/v1/Shop/AdminRemarkOrder/${encodeURIComponent(String(orderId))}`,
    { remark },
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '保存订单备注失败');
  }
}

/**
 * 商品上架
 */
export async function putOnSale(productId: string, expectedVersion: number): Promise<void> {
  const response = await apiPost<null>(
    `/api/v1/Shop/PutOnSale/${encodeURIComponent(String(productId))}`,
    { expectedVersion },
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '商品上架失败');
  }
}

/**
 * 商品下架
 */
export async function takeOffSale(productId: string, expectedVersion: number): Promise<void> {
  const response = await apiPost<null>(
    `/api/v1/Shop/TakeOffSale/${encodeURIComponent(String(productId))}`,
    { expectedVersion },
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '商品下架失败');
  }
}
