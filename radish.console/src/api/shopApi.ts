/**
 * 商城管理 API 客户端
 */

import { env } from '../config/env';
import type {
  ApiResponse,
  PagedResponse,
  Product,
  ProductCategory,
  CreateProductDto,
  UpdateProductDto,
  Order,
  ProductType,
  OrderStatus,
} from './types';

/**
 * 获取 Token
 */
function getToken(): string | null {
  return localStorage.getItem('access_token');
}

/**
 * 带认证的 fetch 封装
 */
async function apiFetch<T>(
  url: string,
  options: RequestInit = {}
): Promise<T> {
  const token = getToken();

  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...options.headers,
  };

  if (token) {
    (headers as Record<string, string>).Authorization = `Bearer ${token}`;
  }

  const response = await fetch(url, {
    ...options,
    headers,
  });

  if (!response.ok) {
    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
  }

  const json = await response.json() as any;

  if (!json.isSuccess) {
    throw new Error(json.messageInfo || '请求失败');
  }

  return json.responseData;
}

// ==================== 商品分类 API ====================

/**
 * 获取商品分类列表
 */
export async function getCategories(): Promise<ProductCategory[]> {
  const url = `${env.apiBaseUrl}/api/v1/Shop/GetCategories`;
  return apiFetch<ProductCategory[]>(url);
}

/**
 * 获取分类详情
 */
export async function getCategory(categoryId: string): Promise<ProductCategory> {
  const url = `${env.apiBaseUrl}/api/v1/Shop/GetCategory/${encodeURIComponent(categoryId)}`;
  return apiFetch<ProductCategory>(url);
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

  const url = `${env.apiBaseUrl}/api/v1/Shop/AdminGetProducts?${searchParams.toString()}`;
  return apiFetch<PagedResponse<Product>>(url);
}

/**
 * 获取商品详情
 */
export async function getProduct(productId: number): Promise<Product> {
  const url = `${env.apiBaseUrl}/api/v1/Shop/GetProduct/${productId}`;
  return apiFetch<Product>(url);
}

/**
 * 创建商品
 */
export async function createProduct(dto: CreateProductDto): Promise<number> {
  const url = `${env.apiBaseUrl}/api/v1/Shop/CreateProduct`;
  return apiFetch<number>(url, {
    method: 'POST',
    body: JSON.stringify(dto),
  });
}

/**
 * 更新商品
 */
export async function updateProduct(dto: UpdateProductDto): Promise<boolean> {
  const url = `${env.apiBaseUrl}/api/v1/Shop/UpdateProduct`;
  return apiFetch<boolean>(url, {
    method: 'PUT',
    body: JSON.stringify(dto),
  });
}

/**
 * 上架商品
 */
export async function putOnSale(productId: number): Promise<boolean> {
  const url = `${env.apiBaseUrl}/api/v1/Shop/PutOnSale/${productId}`;
  return apiFetch<boolean>(url, {
    method: 'POST',
  });
}

/**
 * 下架商品
 */
export async function takeOffSale(productId: number): Promise<boolean> {
  const url = `${env.apiBaseUrl}/api/v1/Shop/TakeOffSale/${productId}`;
  return apiFetch<boolean>(url, {
    method: 'POST',
  });
}

// ==================== 订单管理 API ====================

/**
 * 获取订单列表（管理后台）
 */
export async function adminGetOrders(params: {
  userId?: number;
  status?: OrderStatus;
  productId?: number;
  orderNo?: string;
  pageIndex?: number;
  pageSize?: number;
}): Promise<PagedResponse<Order>> {
  const searchParams = new URLSearchParams();

  if (params.userId) searchParams.append('userId', params.userId.toString());
  if (params.status !== undefined) searchParams.append('status', params.status.toString());
  if (params.productId) searchParams.append('productId', params.productId.toString());
  if (params.orderNo) searchParams.append('orderNo', params.orderNo);
  searchParams.append('pageIndex', (params.pageIndex || 1).toString());
  searchParams.append('pageSize', (params.pageSize || 20).toString());

  const url = `${env.apiBaseUrl}/api/v1/Shop/AdminGetOrders?${searchParams.toString()}`;
  return apiFetch<PagedResponse<Order>>(url);
}

/**
 * 获取订单详情
 */
export async function getOrder(orderId: number): Promise<Order> {
  const url = `${env.apiBaseUrl}/api/v1/Shop/GetOrder/${orderId}`;
  return apiFetch<Order>(url);
}

/**
 * 重新发放权益
 */
export async function retryGrantBenefit(orderId: number): Promise<boolean> {
  const url = `${env.apiBaseUrl}/api/v1/Shop/RetryGrantBenefit/${orderId}`;
  return apiFetch<boolean>(url, {
    method: 'POST',
  });
}

// ==================== 工具函数 ====================

/**
 * 获取商品类型显示名称
 */
export function getProductTypeDisplay(type: ProductType): string {
  switch (type) {
    case ProductType.Benefit:
      return '权益';
    case ProductType.Consumable:
      return '消耗品';
    case ProductType.Physical:
      return '实物';
    default:
      return '未知';
  }
}

/**
 * 获取订单状态显示名称
 */
export function getOrderStatusDisplay(status: OrderStatus): string {
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
      return '未知';
  }
}

/**
 * 获取订单状态颜色
 */
export function getOrderStatusColor(status: OrderStatus): string {
  switch (status) {
    case OrderStatus.Pending:
      return 'warning';
    case OrderStatus.Paid:
      return 'processing';
    case OrderStatus.Completed:
      return 'success';
    case OrderStatus.Cancelled:
      return 'default';
    case OrderStatus.Refunded:
      return 'default';
    case OrderStatus.Failed:
      return 'error';
    default:
      return 'default';
  }
}
