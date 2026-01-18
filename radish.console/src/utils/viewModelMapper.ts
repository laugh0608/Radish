/**
 * ViewModel字段映射工具
 * 用于将后端的Vo前缀字段映射为前端友好的字段名
 */

/**
 * 商品分类数据（前端友好）
 */
export interface ProductCategoryData {
  id: number;
  name: string;
  description: string;
  icon: string;
  sortOrder: number;
  isEnabled: boolean;
  productCount: number;
  createTime: string;
  updateTime: string;
}

/**
 * 将ProductCategoryVo映射为ProductCategoryData
 */
export function mapProductCategory(vo: any): ProductCategoryData {
  return {
    id: vo.VoId,
    name: vo.VoName,
    description: vo.VoDescription,
    icon: vo.VoIcon,
    sortOrder: vo.VoSortOrder,
    isEnabled: vo.VoIsEnabled,
    productCount: vo.VoProductCount,
    createTime: vo.VoCreateTime,
    updateTime: vo.VoUpdateTime,
  };
}

/**
 * 商品数据（前端友好）
 */
export interface ProductData {
  id: number;
  name: string;
  description: string;
  icon: string;
  coverImage: string;
  categoryId: number;
  categoryName: string;
  productType: string; // 字符串类型，不是枚举
  benefitType?: string;
  consumableType?: string;
  price: number;
  originalPrice: number;
  hasDiscount: boolean;
  discountPercent: number;
  stockType: string;
  stock: number;
  soldCount: number;
  limitPerUser: number;
  durationType: string;
  durationDays: number;
  expiresAt: string;
  durationDisplay: string;
  sortOrder: number;
  isOnSale: boolean;
  isEnabled: boolean;
  createTime: string;
  updateTime: string;
}

/**
 * 将ProductVo映射为ProductData
 */
export function mapProduct(vo: any): ProductData {
  return {
    id: vo.VoId,
    name: vo.VoName,
    description: vo.VoDescription,
    icon: vo.VoIcon,
    coverImage: vo.VoCoverImage,
    categoryId: vo.VoCategoryId,
    categoryName: vo.VoCategoryName,
    productType: vo.VoProductType,
    benefitType: vo.VoBenefitType,
    consumableType: vo.VoConsumableType,
    price: vo.VoPrice,
    originalPrice: vo.VoOriginalPrice,
    hasDiscount: vo.VoHasDiscount,
    discountPercent: vo.VoDiscountPercent,
    stockType: vo.VoStockType,
    stock: vo.VoStock,
    soldCount: vo.VoSoldCount,
    limitPerUser: vo.VoLimitPerUser,
    durationType: vo.VoDurationType,
    durationDays: vo.VoDurationDays,
    expiresAt: vo.VoExpiresAt,
    durationDisplay: vo.VoDurationDisplay,
    sortOrder: vo.VoSortOrder,
    isOnSale: vo.VoIsOnSale,
    isEnabled: vo.VoIsEnabled,
    createTime: vo.VoCreateTime,
    updateTime: vo.VoUpdateTime,
  };
}

/**
 * 订单数据（前端友好）
 */
export interface OrderData {
  id: number;
  orderNo: string;
  userId: number;
  userName: string;
  productId: number;
  productName: string;
  productIcon: string;
  productType: string;
  productTypeDisplay: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  status: string;
  statusDisplay: string;
  benefitExpiresAt: string;
  durationDisplay: string;
  createTime: string;
  paidTime: string;
  completedTime: string;
  cancelledTime: string;
  cancelReason: string;
  failReason: string;
}

/**
 * 将OrderVo映射为OrderData
 */
export function mapOrder(vo: any): OrderData {
  return {
    id: vo.VoId,
    orderNo: vo.VoOrderNo,
    userId: vo.VoUserId,
    userName: vo.VoUserName,
    productId: vo.VoProductId,
    productName: vo.VoProductName,
    productIcon: vo.VoProductIcon,
    productType: vo.VoProductType,
    productTypeDisplay: vo.VoProductTypeDisplay || '',
    quantity: vo.VoQuantity,
    unitPrice: vo.VoUnitPrice,
    totalPrice: vo.VoTotalPrice,
    status: vo.VoStatus,
    statusDisplay: vo.VoStatusDisplay || '',
    benefitExpiresAt: vo.VoBenefitExpiresAt,
    durationDisplay: vo.VoDurationDisplay,
    createTime: vo.VoCreateTime,
    paidTime: vo.VoPaidTime,
    completedTime: vo.VoCompletedTime,
    cancelledTime: vo.VoCancelledTime,
    cancelReason: vo.VoCancelReason,
    failReason: vo.VoFailReason,
  };
}
