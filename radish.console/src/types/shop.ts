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
  productType: import('../api/types').ProductType;
  stockType: import('../api/types').StockType;
  stock?: number;
  soldCount: number;
  limitPerUser?: number;
  durationType: import('../api/types').DurationType;
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
  productType: import('../api/types').ProductType;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  status: import('../api/types').OrderStatus;
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
  productType: import('../api/types').ProductType;
  stockType: import('../api/types').StockType;
  stock?: number;
  limitPerUser?: number;
  durationType: import('../api/types').DurationType;
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
  stockType?: import('../api/types').StockType;
  stock?: number;
  limitPerUser?: number;
  durationType?: import('../api/types').DurationType;
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
