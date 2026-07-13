/**
 * API 响应类型定义
 * 直接使用后端 Vo 字段名（voXxx 格式）
 */

/**
 * API 响应包装（与后端 MessageModel 保持一致）
 */
export interface ApiResponse<T> {
  statusCode: number;
  isSuccess: boolean;
  messageInfo: string;
  messageInfoDev?: string;
  code?: string;
  messageKey?: string;
  responseData?: T;
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

/** 商城持续权益与消耗品业务流水。 */
export interface ShopEntitlementOperation {
  voId: string;
  voUserId: string;
  voInventoryId?: string | null;
  voBenefitId?: string | null;
  voRelatedBenefitId?: string | null;
  voOperationType: string;
  voConsumableType?: string | number | null;
  voConsumableTypeDisplay?: string | null;
  voBenefitType?: string | number | null;
  voBenefitTypeDisplay?: string | null;
  voQuantity?: number | null;
  voReason?: string | null;
  voEffectType: string;
  voEffectValue?: string | null;
  voEffectResourceType?: string | null;
  voEffectResourceId?: string | null;
  voEffectResourceNo?: string | null;
  voCreateTime: string;
  voCreateBy?: string | null;
}

/** 服务端 UTC 权益状态。 */
export enum UserBenefitStatus {
  Available = 0,
  Active = 1,
  Expired = 2,
  Revoked = 3,
}

/** 用户持续权益。 */
export interface UserBenefit {
  voId: string;
  voUserId: string;
  voBenefitType: string | number;
  voBenefitTypeDisplay: string;
  voBenefitValue: string;
  voBenefitName?: string | null;
  voSourceOrderId?: string | null;
  voSourceProductId?: string | null;
  voSourceType: string;
  voSourceTypeDisplay: string;
  voEffectiveAt: string;
  voExpiresAt?: string | null;
  voStatus: string | number;
  voStatusDisplay: string;
  voCanActivate: boolean;
  voCanDeactivate: boolean;
  voUnavailableReason?: string | null;
  voRevokedAt?: string | null;
  voRevokedByName?: string | null;
  voRevocationReason?: string | null;
  voDurationDisplay: string;
  voCreateTime: string;
}

export interface UserBenefitActionResult {
  voChanged: boolean;
  voAction: string;
  voBenefitId: string;
  voStatus: string | number;
  voCurrentBenefitId?: string | null;
}

/**
 * 商品类型枚举
 */
export enum ProductType {
  Benefit = 1,    // 权益
  Consumable = 2, // 消耗品
  Physical = 99   // 实物
}

/**
 * 权益类型枚举
 */
export enum BenefitType {
  Badge = 1,        // 徽章
  AvatarFrame = 2,  // 头像框
  Title = 3,        // 称号
  Theme = 4,        // 主题
  Signature = 5,    // 签名档
  NameColor = 6,    // 用户名颜色
  LikeEffect = 7    // 点赞特效
}

/**
 * 消耗品类型枚举
 */
export enum ConsumableType {
  RenameCard = 1,        // 改名卡
  PostPinCard = 2,       // 置顶卡
  PostHighlightCard = 3, // 高亮卡
  ExpCard = 4,           // 经验卡
  CoinCard = 5,          // 萝卜币红包
  DoubleExpCard = 6,     // 双倍经验卡
  LotteryTicket = 99     // 抽奖券
}

/**
 * 订单状态枚举
 */
export enum OrderStatus {
  Pending = 0,    // 待支付
  Paid = 1,       // 已支付
  Completed = 2,  // 已完成
  Cancelled = 3,  // 已取消
  Refunded = 4,   // 已退款
  Failed = 5      // 发放失败
}

/**
 * 库存类型枚举
 */
export enum StockType {
  Unlimited = 0, // 无限
  Limited = 1    // 限量
}

/**
 * 有效期类型枚举
 */
export enum DurationType {
  Permanent = 0, // 永久
  Days = 1,      // 固定天数
  FixedDate = 2  // 固定到期时间
}

/**
 * 商品分类 Vo（直接使用后端字段名）
 */
export interface ProductCategory {
  voId: string;
  voName: string;
  voDescription: string;
  voIconAttachmentId?: string | null;
  voIcon: string;
  voSortOrder: number;
  voIsEnabled: boolean;
  voProductCount: number;
  voCreateTime: string;
  voUpdateTime: string;
}

/**
 * 商品 Vo（直接使用后端字段名）
 */
export interface Product {
  voId: string;
  voName: string;
  voDescription?: string | null;
  voIconAttachmentId?: string | null;
  voIcon?: string | null;
  voCoverAttachmentId?: string | null;
  voCoverImage?: string | null;
  voCategoryId: string;
  voCategoryName?: string | null;
  voProductType: string;
  voBenefitType?: string | null;
  voConsumableType?: string | null;
  voBenefitValue?: string | null;
  voPrice: number;
  voOriginalPrice?: number | null;
  voHasDiscount: boolean;
  voDiscountPercent?: number | null;
  voStockType: string;
  voStock: number;
  voSoldCount: number;
  voLimitPerUser: number;
  voDurationType: string;
  voDurationDays?: number | null;
  voExpiresAt?: string | null;
  voDurationDisplay: string;
  voSortOrder: number;
  voIsOnSale: boolean;
  voIsEnabled: boolean;
  voCreateTime: string;
  voOnSaleTime?: string | null;
  voOffSaleTime?: string | null;
  voVersion: number;
}

/**
 * 订单 Vo（直接使用后端字段名）
 */
export interface Order {
  voId: string;
  voOrderNo: string;
  voUserId: string;
  voUserName: string;
  voProductId: string;
  voProductName: string;
  voProductIcon?: string | null;
  voProductType: string;
  voProductTypeDisplay: string;
  voQuantity: number;
  voUnitPrice: number;
  voTotalPrice: number;
  voStatus: string;
  voStatusDisplay: string;
  voFailureStage: string | number;
  voFailureStageDisplay: string;
  voCanRetryFulfillment: boolean;
  voCoinTransactionId?: string | null;
  voGrantedBenefitId?: string | null;
  voGrantedInventoryId?: string | null;
  voBenefitExpiresAt?: string | null;
  voFixedExpiresAt?: string | null;
  voDurationDisplay?: string | null;
  voCreateTime: string;
  voPaidTime?: string | null;
  voCompletedTime?: string | null;
  voCancelledTime?: string | null;
  voCancelReason?: string | null;
  voFailReason?: string | null;
  voUserRemark?: string | null;
  voAdminRemark?: string | null;
}

/**
 * 创建商品 DTO
 */
export interface CreateProductDto {
  name: string;
  description?: string;
  iconAttachmentId?: string | null;
  coverAttachmentId?: string | null;
  categoryId: string;
  productType: ProductType;
  benefitType?: BenefitType;
  consumableType?: ConsumableType;
  benefitValue?: string;
  price: number;
  originalPrice?: number;
  stockType: StockType;
  stock: number;
  limitPerUser: number;
  durationType: DurationType;
  durationDays?: number;
  expiresAt?: string;
  sortOrder: number;
  isOnSale: boolean;
}

/**
 * 更新商品 DTO
 */
export interface UpdateProductDto extends CreateProductDto {
  id: string;
  expectedVersion: number;
}
