/**
 * 商城系统相关的 API 调用
 */

import { parseApiResponseWithI18n, apiGet, apiPost, configureApiClient, type ApiResponse } from '@radish/ui';
import type { TFunction } from 'i18next';

const defaultApiBase = 'https://localhost:5000';

/**
 * 获取 API Base URL
 */
function getApiBaseUrl(): string {
  const configured = import.meta.env.VITE_API_BASE_URL as string | undefined;
  return (configured ?? defaultApiBase).replace(/\/$/, '');
}

/**
 * 带认证的 fetch 封装
 */
interface ApiFetchOptions extends RequestInit {
  withAuth?: boolean;
}

function apiFetch(input: RequestInfo | URL, options: ApiFetchOptions = {}) {
  const { withAuth, headers, ...rest } = options;

  const finalHeaders: HeadersInit = {
    Accept: 'application/json',
    'Content-Type': 'application/json',
    ...headers
  };

  if (withAuth && typeof window !== 'undefined') {
    const token = window.localStorage.getItem('access_token');
    if (token) {
      (finalHeaders as Record<string, string>).Authorization = `Bearer ${token}`;
    }
  }

  return fetch(input, {
    ...rest,
    headers: finalHeaders
  });
}

// ==================== 类型定义 ====================

/**
 * 商品类型枚举
 */
export const ProductType = {
  Benefit: 0,    // 权益
  Consumable: 1, // 消耗品
  Physical: 2    // 实物
} as const;

export type ProductType = typeof ProductType[keyof typeof ProductType];

/**
 * 权益类型枚举
 */
export const BenefitType = {
  Badge: 0,        // 徽章
  AvatarFrame: 1,  // 头像框
  Title: 2,        // 称号
  Theme: 3,        // 主题
  Signature: 4,    // 签名档
  NameColor: 5,    // 用户名颜色
  LikeEffect: 6    // 点赞特效
} as const;

export type BenefitType = typeof BenefitType[keyof typeof BenefitType];

/**
 * 消耗品类型枚举
 */
export const ConsumableType = {
  RenameCard: 0,        // 改名卡
  PostPinCard: 1,       // 置顶卡
  PostHighlightCard: 2, // 高亮卡
  ExpCard: 3,           // 经验卡
  CoinCard: 4,          // 萝卜币红包
  DoubleExpCard: 5,     // 双倍经验卡
  LotteryTicket: 6      // 抽奖券
} as const;

export type ConsumableType = typeof ConsumableType[keyof typeof ConsumableType];

/**
 * 订单状态枚举
 */
export const OrderStatus = {
  Pending: 0,    // 待支付
  Paid: 1,       // 已支付
  Completed: 2,  // 已完成
  Cancelled: 3,  // 已取消
  Refunded: 4,   // 已退款
  Failed: 5      // 发放失败
} as const;

export type OrderStatus = typeof OrderStatus[keyof typeof OrderStatus];

/**
 * 库存类型枚举
 */
export const StockType = {
  Unlimited: 0, // 无限
  Limited: 1    // 限量
} as const;

export type StockType = typeof StockType[keyof typeof StockType];

/**
 * 有效期类型枚举
 */
export const DurationType = {
  Permanent: 0, // 永久
  Days: 1,      // 固定天数
  FixedDate: 2  // 固定到期时间
} as const;

export type DurationType = typeof DurationType[keyof typeof DurationType];

/**
 * 商品分类
 */
export interface ProductCategory {
  id: string;
  name: string;
  icon?: string;
  description?: string;
  sortOrder: number;
  isEnabled: boolean;
  productCount: number;
}

/**
 * 商品列表项
 */
export interface ProductListItem {
  id: number;
  name: string;
  icon?: string;
  coverImage?: string;
  categoryId: string;
  productType: ProductType;
  price: number;
  originalPrice?: number;
  hasDiscount: boolean;
  soldCount: number;
  inStock: boolean;
  durationDisplay: string;
}

/**
 * 商品详情
 */
export interface Product {
  id: number;
  name: string;
  description?: string;
  icon?: string;
  coverImage?: string;
  categoryId: string;
  categoryName?: string;
  productType: ProductType;
  productTypeDisplay: string;
  benefitType?: BenefitType;
  consumableType?: ConsumableType;
  benefitValue?: string;
  price: number;
  originalPrice?: number;
  hasDiscount: boolean;
  discountPercent?: number;
  stockType: StockType;
  stock: number;
  soldCount: number;
  limitPerUser: number;
  inStock: boolean;
  durationType: DurationType;
  durationDays?: number;
  expiresAt?: string;
  durationDisplay: string;
  sortOrder: number;
  isOnSale: boolean;
  isEnabled: boolean;
  onSaleTime?: string;
  offSaleTime?: string;
  createTime: string;
}

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
 * 订单列表项
 */
export interface OrderListItem {
  id: number;
  orderNo: string;
  productName: string;
  productIcon?: string;
  quantity: number;
  totalPrice: number;
  status: OrderStatus;
  statusDisplay: string;
  createTime: string;
}

/**
 * 订单详情
 */
export interface Order {
  id: number;
  orderNo: string;
  userId: number;
  userName?: string;
  productId: number;
  productName: string;
  productIcon?: string;
  productType: ProductType;
  productTypeDisplay: string;
  benefitType?: BenefitType;
  consumableType?: ConsumableType;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  status: OrderStatus;
  statusDisplay: string;
  benefitExpiresAt?: string;
  durationDisplay?: string;
  createTime: string;
  paidTime?: string;
  completedTime?: string;
  cancelledTime?: string;
  cancelReason?: string;
  failReason?: string;
}

/**
 * 用户权益
 */
export interface UserBenefit {
  id: number;
  userId: number;
  benefitType: BenefitType;
  benefitTypeDisplay: string;
  benefitValue: string;
  benefitName?: string;
  benefitIcon?: string;
  sourceType: string;
  sourceTypeDisplay: string;
  durationType: DurationType;
  effectiveAt: string;
  expiresAt?: string;
  isExpired: boolean;
  isActive: boolean;
  durationDisplay: string;
  createTime: string;
}

/**
 * 用户背包项
 */
export interface UserInventoryItem {
  id: number;
  userId: number;
  consumableType: ConsumableType;
  consumableTypeDisplay: string;
  itemValue?: string;
  itemName?: string;
  itemIcon?: string;
  quantity: number;
  createTime: string;
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
export async function getCategories(t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Shop/GetCategories`;
  const response = await apiFetch(url);

  if (!response.ok) {
    throw new Error(`获取分类列表失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<ProductCategory[]>;
  return parseApiResponse(json, t);
}

/**
 * 获取分类详情
 */
export async function getCategory(categoryId: string, t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Shop/GetCategory/${encodeURIComponent(categoryId)}`;
  const response = await apiFetch(url);

  if (!response.ok) {
    if (response.status === 404) {
      throw new Error('分类不存在');
    }
    throw new Error(`获取分类详情失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<ProductCategory>;
  return parseApiResponse(json, t);
}

/**
 * 获取商品列表
 */
export async function getProducts(
  t: TFunction,
  categoryId?: string,
  productType?: ProductType,
  keyword?: string,
  pageIndex: number = 1,
  pageSize: number = 20
) {
  const params = new URLSearchParams({
    pageIndex: pageIndex.toString(),
    pageSize: pageSize.toString()
  });

  if (categoryId) {
    params.append('categoryId', categoryId);
  }

  if (productType !== undefined) {
    params.append('productType', productType.toString());
  }

  if (keyword) {
    params.append('keyword', keyword);
  }

  const url = `${getApiBaseUrl()}/api/v1/Shop/GetProducts?${params.toString()}`;
  const response = await apiFetch(url);

  if (!response.ok) {
    throw new Error(`获取商品列表失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<PagedResponse<ProductListItem>>;
  return parseApiResponse(json, t);
}

/**
 * 获取商品详情
 */
export async function getProduct(productId: number, t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Shop/GetProduct/${productId}`;
  const response = await apiFetch(url);

  if (!response.ok) {
    if (response.status === 404) {
      throw new Error('商品不存在');
    }
    throw new Error(`获取商品详情失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<Product>;
  return parseApiResponse(json, t);
}

/**
 * 检查是否可以购买商品
 */
export async function checkCanBuy(productId: number, quantity: number = 1, t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Shop/CheckCanBuy/${productId}?quantity=${quantity}`;
  const response = await apiFetch(url, { withAuth: true });

  if (!response.ok) {
    throw new Error(`检查购买权限失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<{ canBuy: boolean; reason: string }>;
  return parseApiResponse(json, t);
}

/**
 * 购买商品
 */
export async function purchaseProduct(request: CreateOrderRequest, t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Shop/Purchase`;
  const response = await apiFetch(url, {
    method: 'POST',
    body: JSON.stringify(request),
    withAuth: true
  });

  if (!response.ok) {
    throw new Error(`购买商品失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<PurchaseResult>;
  return parseApiResponse(json, t);
}

/**
 * 获取我的订单列表
 */
export async function getMyOrders(
  t: TFunction,
  status?: OrderStatus,
  pageIndex: number = 1,
  pageSize: number = 20
) {
  const params = new URLSearchParams({
    pageIndex: pageIndex.toString(),
    pageSize: pageSize.toString()
  });

  if (status !== undefined) {
    params.append('status', status.toString());
  }

  const url = `${getApiBaseUrl()}/api/v1/Shop/GetMyOrders?${params.toString()}`;
  const response = await apiFetch(url, { withAuth: true });

  if (!response.ok) {
    throw new Error(`获取订单列表失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<PagedResponse<OrderListItem>>;
  return parseApiResponse(json, t);
}

/**
 * 获取订单详情
 */
export async function getOrder(orderId: number, t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Shop/GetOrder/${orderId}`;
  const response = await apiFetch(url, { withAuth: true });

  if (!response.ok) {
    if (response.status === 404) {
      throw new Error('订单不存在');
    }
    throw new Error(`获取订单详情失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<Order>;
  return parseApiResponse(json, t);
}

/**
 * 取消订单
 */
export async function cancelOrder(orderId: number, t: TFunction, reason?: string) {
  const url = `${getApiBaseUrl()}/api/v1/Shop/CancelOrder/${orderId}`;
  const body = reason ? JSON.stringify({ reason }) : undefined;

  const response = await apiFetch(url, {
    method: 'POST',
    body,
    withAuth: true
  });

  if (!response.ok) {
    throw new Error(`取消订单失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<boolean>;
  return parseApiResponse(json, t);
}

/**
 * 获取我的权益列表
 */
export async function getMyBenefits(includeExpired: boolean = false, t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Shop/GetMyBenefits?includeExpired=${includeExpired}`;
  const response = await apiFetch(url, { withAuth: true });

  if (!response.ok) {
    throw new Error(`获取权益列表失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<UserBenefit[]>;
  return parseApiResponse(json, t);
}

/**
 * 获取我的激活权益
 */
export async function getMyActiveBenefits(t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Shop/GetMyActiveBenefits`;
  const response = await apiFetch(url, { withAuth: true });

  if (!response.ok) {
    throw new Error(`获取激活权益失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<UserBenefit[]>;
  return parseApiResponse(json, t);
}

/**
 * 激活权益
 */
export async function activateBenefit(benefitId: number, t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Shop/ActivateBenefit/${benefitId}`;
  const response = await apiFetch(url, {
    method: 'POST',
    withAuth: true
  });

  if (!response.ok) {
    throw new Error(`激活权益失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<boolean>;
  return parseApiResponse(json, t);
}

/**
 * 取消激活权益
 */
export async function deactivateBenefit(benefitId: number, t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Shop/DeactivateBenefit/${benefitId}`;
  const response = await apiFetch(url, {
    method: 'POST',
    withAuth: true
  });

  if (!response.ok) {
    throw new Error(`取消激活权益失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<boolean>;
  return parseApiResponse(json, t);
}

/**
 * 获取我的背包
 */
export async function getMyInventory(t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Shop/GetMyInventory`;
  const response = await apiFetch(url, { withAuth: true });

  if (!response.ok) {
    throw new Error(`获取背包失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<UserInventoryItem[]>;
  return parseApiResponse(json, t);
}

/**
 * 使用道具
 */
export async function useItem(request: UseItemRequest, t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Shop/UseItem`;
  const response = await apiFetch(url, {
    method: 'POST',
    body: JSON.stringify(request),
    withAuth: true
  });

  if (!response.ok) {
    throw new Error(`使用道具失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<UseItemResult>;
  return parseApiResponse(json, t);
}

/**
 * 使用改名卡
 */
export async function useRenameCard(inventoryId: number, newNickname: string, t: TFunction) {
  const url = `${getApiBaseUrl()}/api/v1/Shop/UseRenameCard/${inventoryId}?newNickname=${encodeURIComponent(newNickname)}`;
  const response = await apiFetch(url, {
    method: 'POST',
    withAuth: true
  });

  if (!response.ok) {
    throw new Error(`使用改名卡失败: HTTP ${response.status}`);
  }

  const json = await response.json() as ApiResponse<UseItemResult>;
  return parseApiResponse(json, t);
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