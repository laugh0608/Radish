/**
 * 商城管理 API 客户端
 */

import { apiGet, apiPost, apiPut, apiDelete, configureApiClient } from '@radish/ui';
import type {
  PagedResponse,
  Product,
  ProductCategory,
  CreateProductDto,
  UpdateProductDto,
  Order,
} from './types';
import {
  ProductType,
  OrderStatus,
} from './types';

// 配置 API 客户端
const defaultApiBase = 'https://localhost:5000';
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL || defaultApiBase;

configureApiClient({
  baseUrl: apiBaseUrl,
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
export async function adminGetProduct(productId: number): Promise<Product> {
  const response = await apiGet<Product>(
    `/api/v1/Shop/AdminGetProduct/${productId}`,
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
export async function createProduct(product: CreateProductDto): Promise<number> {
  const response = await apiPost<number>('/api/v1/Shop/CreateProduct', product, { withAuth: true });

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
export async function deleteProduct(productId: number): Promise<void> {
  const response = await apiDelete<null>(`/api/v1/Shop/DeleteProduct/${productId}`, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '删除商品失败');
  }
}

/**
 * 上架商品
 */
export async function putProductOnSale(productId: number): Promise<void> {
  const response = await apiPost<null>(`/api/v1/Shop/PutOnSale/${productId}`, undefined, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '上架商品失败');
  }
}

/**
 * 下架商品
 */
export async function takeProductOffSale(productId: number): Promise<void> {
  const response = await apiPost<null>(`/api/v1/Shop/TakeOffSale/${productId}`, undefined, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '下架商品失败');
  }
}

// ==================== 订单管理 API ====================

/**
 * 获取订单列表（管理后台）
 */
export async function adminGetOrders(params: {
  status?: OrderStatus;
  productType?: ProductType;
  keyword?: string;
  startDate?: string;
  endDate?: string;
  pageIndex?: number;
  pageSize?: number;
}): Promise<PagedResponse<Order>> {
  const searchParams = new URLSearchParams();

  if (params.status !== undefined) searchParams.append('status', params.status.toString());
  if (params.productType !== undefined) searchParams.append('productType', params.productType.toString());
  if (params.keyword) searchParams.append('keyword', params.keyword);
  if (params.startDate) searchParams.append('startDate', params.startDate);
  if (params.endDate) searchParams.append('endDate', params.endDate);
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
export async function adminGetOrder(orderId: number): Promise<Order> {
  const response = await apiGet<Order>(`/api/v1/Shop/AdminGetOrder/${orderId}`, { withAuth: true });

  if (!response.ok || !response.data) {
    throw new Error(response.message || '获取订单详情失败');
  }

  return response.data;
}

/**
 * 处理订单（发放权益）
 */
export async function processOrder(orderId: number): Promise<void> {
  const response = await apiPost<null>(`/api/v1/Shop/ProcessOrder/${orderId}`, undefined, { withAuth: true });

  if (!response.ok) {
    throw new Error(response.message || '处理订单失败');
  }
}

/**
 * 取消订单
 */
export async function cancelOrder(orderId: number, reason: string): Promise<void> {
  const response = await apiPost<null>(
    `/api/v1/Shop/CancelOrder/${orderId}`,
    { reason },
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '取消订单失败');
  }
}

/**
 * 退款订单
 */
export async function refundOrder(orderId: number, reason: string): Promise<void> {
  const response = await apiPost<null>(
    `/api/v1/Shop/RefundOrder/${orderId}`,
    { reason },
    { withAuth: true }
  );

  if (!response.ok) {
    throw new Error(response.message || '退款订单失败');
  }
}