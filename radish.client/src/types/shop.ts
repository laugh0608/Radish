/**
 * 商城系统相关的 TypeScript 类型定义
 * 直接使用后端 Vo 字段名，不进行映射
 */

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
 * 商品分类 Vo
 */
export interface ProductCategory {
  voId: number;
  voName: string;
  voIcon?: string | null;
  voDescription?: string | null;
  voSortOrder?: number;
  voIsEnabled?: boolean;
  voProductCount?: number;
}

/**
 * 商品列表项 Vo
 */
export interface ProductListItem {
  voId: number;
  voName: string;
  voIcon?: string | null;
  voCoverImage?: string | null;
  voCategoryId: string;
  voProductType: string;
  voPrice: number;
  voOriginalPrice?: number | null;
  voHasDiscount?: boolean;
  voSoldCount?: number;
  voInStock?: boolean;
  voDurationDisplay?: string;
}

/**
 * 商品详情 Vo
 */
export interface Product {
  voId: number;
  voName: string;
  voDescription?: string | null;
  voIcon?: string | null;
  voCoverImage?: string | null;
  voCategoryId: string;
  voCategoryName?: string | null;
  voProductType: string;
  voProductTypeDisplay?: string;
  voBenefitType?: string | null;
  voConsumableType?: string | null;
  voBenefitValue?: string | null;
  voPrice: number;
  voOriginalPrice?: number | null;
  voHasDiscount?: boolean;
  voDiscountPercent?: number | null;
  voStockType: string;
  voStock?: number;
  voSoldCount?: number;
  voLimitPerUser?: number;
  voInStock?: boolean;
  voDurationType: string;
  voDurationDays?: number | null;
  voExpiresAt?: string | null;
  voDurationDisplay?: string;
  voSortOrder?: number;
  voIsOnSale?: boolean;
  voIsEnabled?: boolean;
  voOnSaleTime?: string | null;
  voOffSaleTime?: string | null;
  voCreateTime?: string;
}

/**
 * 订单列表项 Vo
 */
export interface OrderListItem {
  voId: number;
  voOrderNo: string;
  voProductName: string;
  voProductIcon?: string | null;
  voQuantity: number;
  voTotalPrice: number;
  voStatus: string;
  voStatusDisplay?: string;
  voCreateTime?: string;
}

/**
 * 订单详情 Vo
 */
export interface Order {
  voId: number;
  voOrderNo: string;
  voUserId: number;
  voUserName?: string | null;
  voProductId: number;
  voProductName: string;
  voProductIcon?: string | null;
  voProductType: string;
  voProductTypeDisplay?: string;
  voBenefitType?: string | null;
  voConsumableType?: string | null;
  voQuantity: number;
  voUnitPrice: number;
  voTotalPrice: number;
  voStatus: string;
  voStatusDisplay?: string;
  voBenefitExpiresAt?: string | null;
  voDurationDisplay?: string | null;
  voCreateTime?: string;
  voPaidTime?: string | null;
  voCompletedTime?: string | null;
  voCancelledTime?: string | null;
  voCancelReason?: string | null;
  voFailReason?: string | null;
}

/**
 * 用户权益 Vo
 */
export interface UserBenefit {
  voId: number;
  voUserId: number;
  voBenefitType: string;
  voBenefitTypeDisplay?: string;
  voBenefitValue?: string | null;
  voBenefitName?: string | null;
  voBenefitIcon?: string | null;
  voSourceId: number;
  voSourceType: string;
  voSourceTypeDisplay?: string;
  voDurationType: string;
  voEffectiveAt?: string;
  voExpiresAt?: string | null;
  voIsExpired?: boolean;
  voIsActive?: boolean;
  voDurationDisplay?: string;
  voCreateTime?: string;
}

/**
 * 用户背包项 Vo
 */
export interface UserInventoryItem {
  voId: number;
  voUserId: number;
  voConsumableType: string;
  voConsumableTypeDisplay?: string;
  voItemValue?: string | null;
  voItemName?: string | null;
  voItemIcon?: string | null;
  voQuantity: number;
  voCreateTime?: string;
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
