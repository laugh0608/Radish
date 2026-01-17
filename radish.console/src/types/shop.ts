/**
 * 商品类型枚举
 */
export enum ProductType {
  /** 权益类（徽章、头像框、称号） */
  Benefit = 0,
  /** 消耗品（改名卡、经验卡等） */
  Consumable = 1,
  /** 实物商品 */
  Physical = 2,
}

/**
 * 库存类型枚举
 */
export enum StockType {
  /** 无限库存 */
  Unlimited = 0,
  /** 限量库存 */
  Limited = 1,
}

/**
 * 时长类型枚举
 */
export enum DurationType {
  /** 永久 */
  Permanent = 0,
  /** 固定天数 */
  FixedDays = 1,
  /** 固定时间点 */
  FixedDate = 2,
}

/**
 * 订单状态枚举
 */
export enum OrderStatus {
  /** 待支付 */
  Pending = 0,
  /** 已支付 */
  Paid = 1,
  /** 已完成（权益已发放） */
  Completed = 2,
  /** 已取消 */
  Cancelled = 3,
  /** 已退款 */
  Refunded = 4,
  /** 失败（支付失败或权益发放失败） */
  Failed = 5,
}

/**
 * 商品实体
 */
export interface Product {
  id: number;
  name: string;
  description?: string;
  categoryId: string;
  categoryName?: string;
  price: number;
  originalPrice?: number;
  imageUrl?: string;
  productType: ProductType;
  stockType: StockType;
  stock?: number;
  soldCount: number;
  limitPerUser?: number;
  durationType: DurationType;
  durationDays?: number;
  expiryDate?: string;
  isActive: boolean;
  sortOrder: number;
  createdAt: string;
  updatedAt?: string;
}

/**
 * 商品分类
 */
export interface ProductCategory {
  id: string;
  name: string;
  description?: string;
  icon?: string;
  sortOrder: number;
}

/**
 * 订单实体
 */
export interface Order {
  id: number;
  orderNo: string;
  userId: number;
  userName?: string;
  productId: number;
  productName: string;
  productType: ProductType;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  status: OrderStatus;
  paidAt?: string;
  completedAt?: string;
  cancelledAt?: string;
  failureReason?: string;
  createdAt: string;
  updatedAt?: string;
}

/**
 * 创建商品请求
 */
export interface CreateProductRequest {
  name: string;
  description?: string;
  categoryId: string;
  price: number;
  originalPrice?: number;
  imageUrl?: string;
  productType: ProductType;
  stockType: StockType;
  stock?: number;
  limitPerUser?: number;
  durationType: DurationType;
  durationDays?: number;
  expiryDate?: string;
  isActive: boolean;
  sortOrder?: number;
}

/**
 * 更新商品请求
 */
export interface UpdateProductRequest {
  name?: string;
  description?: string;
  categoryId?: string;
  price?: number;
  originalPrice?: number;
  imageUrl?: string;
  stockType?: StockType;
  stock?: number;
  limitPerUser?: number;
  durationType?: DurationType;
  durationDays?: number;
  expiryDate?: string;
  isActive?: boolean;
  sortOrder?: number;
}

/**
 * 商品统计
 */
export interface ProductStats {
  totalProducts: number;
  activeProducts: number;
  totalSales: number;
  totalRevenue: number;
  topSellingProducts: Array<{
    productId: number;
    productName: string;
    soldCount: number;
    revenue: number;
  }>;
}

/**
 * 订单统计
 */
export interface OrderStats {
  totalOrders: number;
  pendingOrders: number;
  completedOrders: number;
  failedOrders: number;
  todayOrders: number;
  todayRevenue: number;
  orderTrend: Array<{
    date: string;
    orderCount: number;
    revenue: number;
  }>;
}
