/**
 * API 响应类型定义
 */

import type { ProductData, OrderData, ProductCategoryData } from '@/utils/viewModelMapper';

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
  LotteryTicket = 7      // 抽奖券
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
 * 商品分类（导出映射后的类型）
 */
export type ProductCategory = ProductCategoryData;

/**
 * 商品详情（导出映射后的类型）
 */
export type Product = ProductData;

/**
 * 创建商品 DTO
 */
export interface CreateProductDto {
  name: string;
  description?: string;
  icon?: string;
  coverImage?: string;
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
  id: number;
}

/**
 * 订单详情（导出映射后的类型）
 */
export type Order = OrderData;
