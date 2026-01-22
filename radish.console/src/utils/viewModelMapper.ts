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
    id: vo.voId ?? vo.VoId,
    name: vo.voName ?? vo.VoName,
    description: vo.voDescription ?? vo.VoDescription,
    icon: vo.voIcon ?? vo.VoIcon,
    sortOrder: vo.voSortOrder ?? vo.VoSortOrder,
    isEnabled: vo.voIsEnabled ?? vo.VoIsEnabled,
    productCount: vo.voProductCount ?? vo.VoProductCount,
    createTime: vo.voCreateTime ?? vo.VoCreateTime,
    updateTime: vo.voUpdateTime ?? vo.VoUpdateTime,
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
    id: vo.voId ?? vo.VoId,
    name: vo.voName ?? vo.VoName,
    description: vo.voDescription ?? vo.VoDescription,
    icon: vo.voIcon ?? vo.VoIcon,
    coverImage: vo.voCoverImage ?? vo.VoCoverImage,
    categoryId: vo.voCategoryId ?? vo.VoCategoryId,
    categoryName: vo.voCategoryName ?? vo.VoCategoryName,
    productType: vo.voProductType ?? vo.VoProductType,
    benefitType: vo.voBenefitType ?? vo.VoBenefitType,
    consumableType: vo.voConsumableType ?? vo.VoConsumableType,
    price: vo.voPrice ?? vo.VoPrice,
    originalPrice: vo.voOriginalPrice ?? vo.VoOriginalPrice,
    hasDiscount: vo.voHasDiscount ?? vo.VoHasDiscount,
    discountPercent: vo.voDiscountPercent ?? vo.VoDiscountPercent,
    stockType: vo.voStockType ?? vo.VoStockType,
    stock: vo.voStock ?? vo.VoStock,
    soldCount: vo.voSoldCount ?? vo.VoSoldCount,
    limitPerUser: vo.voLimitPerUser ?? vo.VoLimitPerUser,
    durationType: vo.voDurationType ?? vo.VoDurationType,
    durationDays: vo.voDurationDays ?? vo.VoDurationDays,
    expiresAt: vo.voExpiresAt ?? vo.VoExpiresAt,
    durationDisplay: vo.voDurationDisplay ?? vo.VoDurationDisplay,
    sortOrder: vo.voSortOrder ?? vo.VoSortOrder,
    isOnSale: vo.voIsOnSale ?? vo.VoIsOnSale,
    isEnabled: vo.voIsEnabled ?? vo.VoIsEnabled,
    createTime: vo.voCreateTime ?? vo.VoCreateTime,
    updateTime: vo.voUpdateTime ?? vo.VoUpdateTime,
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
    id: vo.voId ?? vo.VoId,
    orderNo: vo.voOrderNo ?? vo.VoOrderNo,
    userId: vo.voUserId ?? vo.VoUserId,
    userName: vo.voUserName ?? vo.VoUserName,
    productId: vo.voProductId ?? vo.VoProductId,
    productName: vo.voProductName ?? vo.VoProductName,
    productIcon: vo.voProductIcon ?? vo.VoProductIcon,
    productType: vo.voProductType ?? vo.VoProductType,
    productTypeDisplay: (vo.voProductTypeDisplay ?? vo.VoProductTypeDisplay) || '',
    quantity: vo.voQuantity ?? vo.VoQuantity,
    unitPrice: vo.voUnitPrice ?? vo.VoUnitPrice,
    totalPrice: vo.voTotalPrice ?? vo.VoTotalPrice,
    status: vo.voStatus ?? vo.VoStatus,
    statusDisplay: (vo.voStatusDisplay ?? vo.VoStatusDisplay) || '',
    benefitExpiresAt: vo.voBenefitExpiresAt ?? vo.VoBenefitExpiresAt,
    durationDisplay: vo.voDurationDisplay ?? vo.VoDurationDisplay,
    createTime: vo.voCreateTime ?? vo.VoCreateTime,
    paidTime: vo.voPaidTime ?? vo.VoPaidTime,
    completedTime: vo.voCompletedTime ?? vo.VoCompletedTime,
    cancelledTime: vo.voCancelledTime ?? vo.VoCancelledTime,
    cancelReason: vo.voCancelReason ?? vo.VoCancelReason,
    failReason: vo.voFailReason ?? vo.VoFailReason,
  };
}
