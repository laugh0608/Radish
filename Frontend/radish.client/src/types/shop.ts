/**
 * 商城系统相关的 TypeScript 类型定义
 * 直接使用后端 Vo 字段名，不进行映射
 */

import type { LongId } from '@/api/user';

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
  voId: LongId;
  voName: string;
  voIcon?: string | null;
  voCoverImage?: string | null;
  voCategoryId: string;
  voProductType: string | number;
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
  voId: LongId;
  voName: string;
  voDescription?: string | null;
  voIcon?: string | null;
  voCoverImage?: string | null;
  voCategoryId: string;
  voCategoryName?: string | null;
  voProductType: string | number;
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

export interface ShopProductCapability {
  voProductType: string | number;
  voBenefitType?: string | number | null;
  voConsumableType?: string | number | null;
  voCanSell: boolean;
  voCanActivate: boolean;
  voConfigurationRequirements: string[];
  voUnavailableReason?: string | null;
}

export interface ProductBuyCheckResult {
  canBuy: boolean;
  reason: string;
}

/**
 * 订单列表项 Vo
 */
export interface OrderListItem {
  voId: LongId;
  voOrderNo: string;
  voProductName: string;
  voProductIcon?: string | null;
  voQuantity: number;
  voTotalPrice: number;
  voStatus: string | number;
  voStatusDisplay?: string;
  voFailureStage?: string | number;
  voCreateTime?: string;
}

/**
 * 订单详情 Vo
 */
export interface Order {
  voId: LongId;
  voOrderNo: string;
  voUserId: LongId;
  voUserName?: string | null;
  voProductId: LongId;
  voProductName: string;
  voProductIcon?: string | null;
  voProductType: string | number;
  voProductTypeDisplay?: string;
  voBenefitType?: string | null;
  voConsumableType?: string | null;
  voQuantity: number;
  voUnitPrice: number;
  voTotalPrice: number;
  voStatus: string | number;
  voStatusDisplay?: string;
  voFailureStage?: string | number;
  voFailureStageDisplay?: string;
  voCanRetryFulfillment?: boolean;
  voCoinTransactionId?: LongId | null;
  voGrantedBenefitId?: LongId | null;
  voGrantedInventoryId?: LongId | null;
  voBenefitExpiresAt?: string | null;
  voFixedExpiresAt?: string | null;
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
  voId: LongId;
  voUserId: LongId;
  voBenefitType: string;
  voBenefitTypeDisplay?: string;
  voBenefitValue?: string | null;
  voBenefitName?: string | null;
  voBenefitIcon?: string | null;
  voSourceOrderId?: LongId | null;
  voSourceProductId?: LongId | null;
  voSourceType: string;
  voSourceTypeDisplay?: string;
  voDurationType: string;
  voEffectiveAt?: string;
  voExpiresAt?: string | null;
  voIsExpired?: boolean;
  voIsActive?: boolean;
  voStatus: string | number;
  voStatusDisplay?: string;
  voCanActivate: boolean;
  voCanDeactivate: boolean;
  voUnavailableReason?: string | null;
  voRevokedAt?: string | null;
  voRevokedByName?: string | null;
  voRevocationReason?: string | null;
  voDurationDisplay?: string;
  voCreateTime?: string;
}

/** 持续权益选择、停用或撤销结果。 */
export interface UserBenefitActionResult {
  voChanged: boolean;
  voAction: string;
  voBenefitId: LongId;
  voBenefitType: string | number;
  voStatus: string | number;
  voCurrentBenefitId?: LongId | null;
  voCurrentBenefit?: UserBenefit | null;
}

/**
 * 用户背包项 Vo
 */
export interface UserInventoryItem {
  voId: LongId;
  voUserId: LongId;
  voConsumableType: string;
  voConsumableTypeDisplay?: string;
  voItemValue?: string | null;
  voItemName?: string | null;
  voItemIcon?: string | null;
  voQuantity: number;
  voSourceProductId?: LongId | null;
  voCreateTime?: string;
}

/**
 * 创建订单请求
 */
export interface CreateOrderRequest {
  productId: LongId;
  quantity?: number;
  paymentPassword: string;
  idempotencyKey?: string;
  userRemark?: string;
}

/**
 * 购买结果
 */
export interface PurchaseResult {
  success: boolean;
  orderId?: LongId;
  orderNo?: string;
  errorMessage?: string;
  errorCode?: string;
  requiresPasscodeUpgrade?: boolean;
  userBenefitId?: LongId;
  grantedBenefitId?: LongId;
  grantedInventoryId?: LongId;
  deductedCoins?: number;
  remainingBalance?: number;
}

/**
 * 使用道具请求
 */
export interface UseItemRequest {
  inventoryId: LongId;
  quantity?: number;
  targetId?: LongId;
  idempotencyKey: string;
}

/**
 * 使用改名卡请求
 */
export interface UseRenameCardRequest {
  inventoryId: LongId;
  newDisplayName: string;
  idempotencyKey: string;
}

/**
 * 使用道具结果
 */
export interface UseItemResult {
  success: boolean;
  errorMessage?: string;
  operationId?: LongId;
  isIdempotentReplay?: boolean;
  remainingQuantity: number;
  effectDescription?: string;
  effectType?: string;
  effectValue?: string;
  effectResourceType?: string;
  effectResourceId?: LongId;
  effectResourceNo?: string;
}
