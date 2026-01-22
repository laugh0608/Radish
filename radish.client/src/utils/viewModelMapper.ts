/**
 * ViewModel字段映射工具
 * 用于将后端的Vo前缀字段映射为前端友好的字段名
 */

// ==================== 论坛系统映射 ====================

/**
 * 分类数据（前端友好）
 */
export interface CategoryData {
  id: number;
  name: string;
  slug: string;
  description?: string | null;
  icon?: string;
  coverImage?: string;
  parentId?: number | null;
  level: number;
  orderSort: number;
  postCount: number;
  isEnabled: boolean;
  createTime: string;
  createBy: string;
}

/**
 * 将CategoryVo映射为CategoryData
 */
export function mapCategory(vo: any): CategoryData {
  return {
    id: vo.voId ?? vo.VoId,
    name: vo.voName ?? vo.VoName,
    slug: vo.voSlug ?? vo.VoSlug,
    description: vo.voDescription ?? vo.VoDescription,
    icon: vo.voIcon ?? vo.VoIcon,
    coverImage: vo.voCoverImage ?? vo.VoCoverImage,
    parentId: vo.voParentId ?? vo.VoParentId,
    level: vo.voLevel ?? vo.VoLevel,
    orderSort: vo.voOrderSort ?? vo.VoOrderSort,
    postCount: vo.voPostCount ?? vo.VoPostCount,
    isEnabled: vo.voIsEnabled ?? vo.VoIsEnabled,
    createTime: vo.voCreateTime ?? vo.VoCreateTime,
    createBy: vo.voCreateBy ?? vo.VoCreateBy,
  };
}


/**
 * 帖子列表项数据（前端友好）
 */
export interface PostItemData {
  id: number;
  title: string;
  slug: string;
  summary?: string | null;
  categoryId: number;
  categoryName?: string;
  authorId: number;
  authorName: string;
  viewCount: number;
  likeCount: number;
  commentCount: number;
  isTop: boolean;
  isEssence: boolean;
  isLocked: boolean;
  createTime: string;
  updateTime: string;
}

/**
 * 将PostVo映射为PostItemData
 */
export function mapPostItem(vo: any): PostItemData {
  return {
    id: vo.voId ?? vo.VoId,
    title: vo.voTitle ?? vo.VoTitle,
    slug: vo.voSlug ?? vo.VoSlug,
    summary: vo.voSummary ?? vo.VoSummary,
    categoryId: vo.voCategoryId ?? vo.VoCategoryId,
    categoryName: vo.voCategoryName ?? vo.VoCategoryName,
    authorId: vo.voAuthorId ?? vo.VoAuthorId,
    authorName: vo.voAuthorName ?? vo.VoAuthorName,
    viewCount: vo.voViewCount ?? vo.VoViewCount,
    likeCount: vo.voLikeCount ?? vo.VoLikeCount,
    commentCount: vo.voCommentCount ?? vo.VoCommentCount,
    isTop: vo.voIsTop ?? vo.VoIsTop,
    isEssence: vo.voIsEssence ?? vo.VoIsEssence,
    isLocked: vo.voIsLocked ?? vo.VoIsLocked,
    createTime: vo.voCreateTime ?? vo.VoCreateTime,
    updateTime: vo.voUpdateTime ?? vo.VoUpdateTime,
  };
}

/**
 * 帖子详情数据（前端友好）
 */
export interface PostDetailData extends PostItemData {
  content: string;
  contentType: string;
  coverImage?: string;
  tags?: string; // 逗号分隔的标签名
  tagNames?: string[]; // 标签名数组（由前端解析）
}

/**
 * 将PostVo映射为PostDetailData
 */
export function mapPostDetail(vo: any): PostDetailData {
  const tags = vo.voTags ?? vo.VoTags ?? '';
  return {
    id: vo.voId ?? vo.VoId,
    title: vo.voTitle ?? vo.VoTitle,
    slug: vo.voSlug ?? vo.VoSlug,
    summary: vo.voSummary ?? vo.VoSummary,
    content: vo.voContent ?? vo.VoContent,
    contentType: vo.voContentType ?? vo.VoContentType,
    coverImage: vo.voCoverImage ?? vo.VoCoverImage,
    categoryId: vo.voCategoryId ?? vo.VoCategoryId,
    categoryName: vo.voCategoryName ?? vo.VoCategoryName,
    authorId: vo.voAuthorId ?? vo.VoAuthorId,
    authorName: vo.voAuthorName ?? vo.VoAuthorName,
    tags,
    tagNames: tags ? tags.split(',').map((t: string) => t.trim()) : [],
    viewCount: vo.voViewCount ?? vo.VoViewCount,
    likeCount: vo.voLikeCount ?? vo.VoLikeCount,
    commentCount: vo.voCommentCount ?? vo.VoCommentCount,
    isTop: vo.voIsTop ?? vo.VoIsTop,
    isEssence: vo.voIsEssence ?? vo.VoIsEssence,
    isLocked: vo.voIsLocked ?? vo.VoIsLocked,
    createTime: vo.voCreateTime ?? vo.VoCreateTime,
    updateTime: vo.voUpdateTime ?? vo.VoUpdateTime,
  };
}

/**
 * 评论节点数据（前端友好）
 */
export interface CommentNodeData {
  id: number;
  postId: number;
  content: string;
  authorId: number;
  authorName: string;
  parentId?: number | null;
  rootId?: number | null;
  replyToUserId?: number | null;
  replyToUserName?: string | null;
  level: number;
  likeCount: number;
  replyCount: number;
  isTop: boolean;
  isLiked?: boolean;
  createTime: string;
  updateTime: string;
  // 前端扩展字段
  children?: CommentNodeData[];
  childrenTotal?: number;
  isGodComment?: boolean;
  isSofa?: boolean;
  highlightRank?: number;
}

/**
 * 将CommentVo映射为CommentNodeData
 */
export function mapComment(vo: any): CommentNodeData {
  return {
    id: vo.voId ?? vo.VoId,
    postId: vo.voPostId ?? vo.VoPostId,
    content: vo.voContent ?? vo.VoContent,
    authorId: vo.voAuthorId ?? vo.VoAuthorId,
    authorName: vo.voAuthorName ?? vo.VoAuthorName,
    parentId: vo.voParentId ?? vo.VoParentId,
    rootId: vo.voRootId ?? vo.VoRootId,
    replyToUserId: vo.voReplyToUserId ?? vo.VoReplyToUserId,
    replyToUserName: vo.voReplyToUserName ?? vo.VoReplyToUserName,
    level: vo.voLevel ?? vo.VoLevel,
    likeCount: vo.voLikeCount ?? vo.VoLikeCount,
    replyCount: vo.voReplyCount ?? vo.VoReplyCount,
    isTop: vo.voIsTop ?? vo.VoIsTop,
    isLiked: vo.voIsLiked ?? vo.VoIsLiked,
    createTime: vo.voCreateTime ?? vo.VoCreateTime,
    updateTime: vo.voUpdateTime ?? vo.VoUpdateTime,
    // 前端扩展字段（如果后端返回的话）
    children: (vo.voChildren ?? vo.VoChildren)?.map(mapComment),
    childrenTotal: vo.voChildrenTotal ?? vo.VoChildrenTotal,
    isGodComment: vo.voIsGodComment ?? vo.VoIsGodComment,
    isSofa: vo.voIsSofa ?? vo.VoIsSofa,
    highlightRank: vo.voHighlightRank ?? vo.VoHighlightRank,
  };
}

/**
 * 评论高亮记录数据（前端友好）
 */
export interface CommentHighlightData {
  id: number;
  postId: number;
  commentId: number;
  parentCommentId?: number | null;
  highlightType: number;
  statDate: string;
  likeCount: number;
  rank: number;
  contentSnapshot?: string | null;
  authorId: number;
  authorName: string;
  isCurrent: boolean;
  createTime: string;
}

/**
 * 将CommentHighlightVo映射为CommentHighlightData
 */
export function mapCommentHighlight(vo: any): CommentHighlightData {
  return {
    id: vo.voId ?? vo.VoId,
    postId: vo.voPostId ?? vo.VoPostId,
    commentId: vo.voCommentId ?? vo.VoCommentId,
    parentCommentId: vo.voParentCommentId ?? vo.VoParentCommentId,
    highlightType: vo.voHighlightType ?? vo.VoHighlightType,
    statDate: vo.voStatDate ?? vo.VoStatDate,
    likeCount: vo.voLikeCount ?? vo.VoLikeCount,
    rank: vo.voRank ?? vo.VoRank,
    contentSnapshot: vo.voContentSnapshot ?? vo.VoContentSnapshot,
    authorId: vo.voAuthorId ?? vo.VoAuthorId,
    authorName: vo.voAuthorName ?? vo.VoAuthorName,
    isCurrent: vo.voIsCurrent ?? vo.VoIsCurrent,
    createTime: vo.voCreateTime ?? vo.VoCreateTime,
  };
}

// ==================== 商城系统映射 ====================

/**
 * 商品分类数据（前端友好）
 */
export interface ProductCategoryData {
  id: string;
  name: string;
  icon?: string;
  description?: string;
  sortOrder: number;
  isEnabled: boolean;
  productCount: number;
}

/**
 * 将CategoryVo映射为ProductCategoryData
 */
export function mapProductCategory(vo: any): ProductCategoryData {
  const idValue = (vo.voId ?? vo.VoId) as string | number | undefined;

  return {
    id: idValue != null ? idValue.toString() : '',
    name: vo.voName ?? vo.VoName,
    icon: vo.voIcon ?? vo.VoIcon,
    description: vo.voDescription ?? vo.VoDescription,
    sortOrder: vo.voSortOrder ?? vo.VoSortOrder ?? 0,
    isEnabled: vo.voIsEnabled ?? vo.VoIsEnabled ?? true,
    productCount: vo.voProductCount ?? vo.VoProductCount ?? 0,
  };
}

/**
 * 商品列表项数据（前端友好）
 */
export interface ProductListItemData {
  id: number;
  name: string;
  icon?: string;
  coverImage?: string;
  categoryId: string;
  productType: string;
  price: number;
  originalPrice?: number;
  hasDiscount: boolean;
  soldCount: number;
  inStock: boolean;
  durationDisplay: string;
}

/**
 * 将ProductVo映射为ProductListItemData
 */
export function mapProductListItem(vo: any): ProductListItemData {
  return {
    id: vo.voId ?? vo.VoId,
    name: vo.voName ?? vo.VoName,
    icon: vo.voIcon ?? vo.VoIcon,
    coverImage: vo.voCoverImage ?? vo.VoCoverImage,
    categoryId: vo.voCategoryId ?? vo.VoCategoryId,
    productType: vo.voProductType ?? vo.VoProductType,
    price: vo.voPrice ?? vo.VoPrice ?? 0, // 确保价格不为null/undefined
    originalPrice: vo.voOriginalPrice ?? vo.VoOriginalPrice ?? undefined,
    hasDiscount: vo.voHasDiscount ?? vo.VoHasDiscount ?? false,
    soldCount: vo.voSoldCount ?? vo.VoSoldCount ?? 0,
    inStock: vo.voInStock ?? vo.VoInStock ?? false,
    durationDisplay: vo.voDurationDisplay ?? vo.VoDurationDisplay ?? '',
  };
}

/**
 * 商品详情数据（前端友好）
 */
export interface ProductData {
  id: number;
  name: string;
  description?: string;
  icon?: string;
  coverImage?: string;
  categoryId: string;
  categoryName?: string;
  productType: string;
  productTypeDisplay: string;
  benefitType?: string;
  consumableType?: string;
  benefitValue?: string;
  price: number;
  originalPrice?: number;
  hasDiscount: boolean;
  discountPercent?: number;
  stockType: string;
  stock: number;
  soldCount: number;
  limitPerUser: number;
  durationType: string;
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
    productTypeDisplay: vo.voProductTypeDisplay ?? vo.VoProductTypeDisplay,
    benefitType: vo.voBenefitType ?? vo.VoBenefitType,
    consumableType: vo.voConsumableType ?? vo.VoConsumableType,
    benefitValue: vo.voBenefitValue ?? vo.VoBenefitValue,
    price: vo.voPrice ?? vo.VoPrice ?? 0, // 确保价格不为null/undefined
    originalPrice: vo.voOriginalPrice ?? vo.VoOriginalPrice ?? undefined,
    hasDiscount: vo.voHasDiscount ?? vo.VoHasDiscount ?? false,
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
    onSaleTime: vo.voOnSaleTime ?? vo.VoOnSaleTime,
    offSaleTime: vo.voOffSaleTime ?? vo.VoOffSaleTime,
    createTime: vo.voCreateTime ?? vo.VoCreateTime,
  };
}

/**
 * 订单列表项数据（前端友好）
 */
export interface OrderListItemData {
  id: number;
  orderNo: string;
  productName: string;
  productIcon?: string;
  quantity: number;
  totalPrice: number;
  status: string;
  statusDisplay: string;
  createTime: string;
}

/**
 * 将OrderVo映射为OrderListItemData
 */
export function mapOrderListItem(vo: any): OrderListItemData {
  return {
    id: vo.voId ?? vo.VoId,
    orderNo: vo.voOrderNo ?? vo.VoOrderNo,
    productName: vo.voProductName ?? vo.VoProductName,
    productIcon: vo.voProductIcon ?? vo.VoProductIcon,
    quantity: vo.voQuantity ?? vo.VoQuantity,
    totalPrice: vo.voTotalPrice ?? vo.VoTotalPrice ?? 0,
    status: vo.voStatus ?? vo.VoStatus,
    statusDisplay: vo.voStatusDisplay ?? vo.VoStatusDisplay,
    createTime: vo.voCreateTime ?? vo.VoCreateTime,
  };
}

/**
 * 订单详情数据（前端友好）
 */
export interface OrderData {
  id: number;
  orderNo: string;
  userId: number;
  userName?: string;
  productId: number;
  productName: string;
  productIcon?: string;
  productType: string;
  productTypeDisplay: string;
  benefitType?: string;
  consumableType?: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  status: string;
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
    productTypeDisplay: vo.voProductTypeDisplay ?? vo.VoProductTypeDisplay,
    benefitType: vo.voBenefitType ?? vo.VoBenefitType,
    consumableType: vo.voConsumableType ?? vo.VoConsumableType,
    quantity: vo.voQuantity ?? vo.VoQuantity,
    unitPrice: vo.voUnitPrice ?? vo.VoUnitPrice ?? 0,
    totalPrice: vo.voTotalPrice ?? vo.VoTotalPrice ?? 0,
    status: vo.voStatus ?? vo.VoStatus,
    statusDisplay: vo.voStatusDisplay ?? vo.VoStatusDisplay,
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

/**
 * 用户权益数据（前端友好）
 */
export interface UserBenefitData {
  id: number;
  userId: number;
  benefitType: string;
  benefitTypeDisplay: string;
  benefitValue?: string;
  sourceId: number;
  sourceType: string;
  sourceTypeDisplay: string;
  durationType: string;
  effectiveAt: string;
  expiresAt?: string;
  isExpired: boolean;
  isActive: boolean;
  durationDisplay: string;
  createTime: string;
}

/**
 * 将UserBenefitVo映射为UserBenefitData
 */
export function mapUserBenefit(vo: any): UserBenefitData {
  return {
    id: vo.voId ?? vo.VoId,
    userId: vo.voUserId ?? vo.VoUserId,
    benefitType: vo.voBenefitType ?? vo.VoBenefitType,
    benefitTypeDisplay: vo.voBenefitTypeDisplay ?? vo.VoBenefitTypeDisplay,
    benefitValue: vo.voBenefitValue ?? vo.VoBenefitValue,
    sourceId: vo.voSourceId ?? vo.VoSourceId,
    sourceType: vo.voSourceType ?? vo.VoSourceType,
    sourceTypeDisplay: vo.voSourceTypeDisplay ?? vo.VoSourceTypeDisplay,
    durationType: vo.voDurationType ?? vo.VoDurationType,
    effectiveAt: vo.voEffectiveAt ?? vo.VoEffectiveAt,
    expiresAt: vo.voExpiresAt ?? vo.VoExpiresAt,
    isExpired: vo.voIsExpired ?? vo.VoIsExpired,
    isActive: vo.voIsActive ?? vo.VoIsActive,
    durationDisplay: vo.voDurationDisplay ?? vo.VoDurationDisplay,
    createTime: vo.voCreateTime ?? vo.VoCreateTime,
  };
}

/**
 * 用户背包项数据（前端友好）
 */
export interface UserInventoryItemData {
  id: number;
  userId: number;
  consumableType: string;
  consumableTypeDisplay: string;
  itemValue?: string;
  itemName?: string;
  itemIcon?: string;
  quantity: number;
  createTime: string;
}

/**
 * 将UserInventoryVo映射为UserInventoryItemData
 */
export function mapUserInventoryItem(vo: any): UserInventoryItemData {
  return {
    id: vo.voId ?? vo.VoId,
    userId: vo.voUserId ?? vo.VoUserId,
    consumableType: vo.voConsumableType ?? vo.VoConsumableType,
    consumableTypeDisplay: vo.voConsumableTypeDisplay ?? vo.VoConsumableTypeDisplay,
    itemValue: vo.voItemValue ?? vo.VoItemValue,
    itemName: vo.voItemName ?? vo.VoItemName,
    itemIcon: vo.voItemIcon ?? vo.VoItemIcon,
    quantity: vo.voQuantity ?? vo.VoQuantity,
    createTime: vo.voCreateTime ?? vo.VoCreateTime,
  };
}

// ==================== 经验系统映射 ====================

/**
 * 用户经验值信息映射
 */
export interface ExperienceData {
  userId: string;
  currentExp: number;
  currentLevel: number;
  nextLevelExp: number;
  totalExp: number;
  levelProgress: number;
  levelName: string;
  levelDescription: string;
  canLevelUp: boolean;
  nextLevelName: string;
  expToNextLevel: number;
  expGainedToday: number;
  dailyExpLimit: number;
  remainingDailyExp: number;
  isMaxLevel: boolean;
  rank: number;
  percentile: number;
  createTime: string;
  updateTime: string;
  themeColor?: string;
  currentLevelName: string;
  nextLevel?: number;
  expFrozen?: boolean;
}

/**
 * 将UserExperience映射为ExperienceData
 */
export function mapUserExperience(vo: any): ExperienceData {
  const userIdValue = vo.voUserId ?? vo.VoUserId;

  return {
    userId: userIdValue != null ? userIdValue.toString() : '0',
    currentExp: vo.voCurrentExp ?? vo.VoCurrentExp ?? 0,
    currentLevel: vo.voCurrentLevel ?? vo.VoCurrentLevel ?? 1,
    nextLevelExp: vo.voNextLevelExp ?? vo.VoNextLevelExp ?? 0,
    totalExp: vo.voTotalExp ?? vo.VoTotalExp ?? 0,
    levelProgress: vo.voLevelProgress ?? vo.VoLevelProgress ?? 0,
    levelName: vo.voLevelName ?? vo.VoLevelName ?? '新手',
    levelDescription: vo.voLevelDescription ?? vo.VoLevelDescription ?? '',
    canLevelUp: vo.voCanLevelUp ?? vo.VoCanLevelUp ?? false,
    nextLevelName: vo.voNextLevelName ?? vo.VoNextLevelName ?? '新手',
    expToNextLevel: vo.voExpToNextLevel ?? vo.VoExpToNextLevel ?? 0,
    expGainedToday: vo.voExpGainedToday ?? vo.VoExpGainedToday ?? 0,
    dailyExpLimit: vo.voDailyExpLimit ?? vo.VoDailyExpLimit ?? 0,
    remainingDailyExp: vo.voRemainingDailyExp ?? vo.VoRemainingDailyExp ?? 0,
    isMaxLevel: vo.voIsMaxLevel ?? vo.VoIsMaxLevel ?? false,
    rank: vo.voRank ?? vo.VoRank ?? 0,
    percentile: vo.voPercentile ?? vo.VoPercentile ?? 0,
    createTime: vo.voCreateTime ?? vo.VoCreateTime ?? '',
    updateTime: vo.voUpdateTime ?? vo.VoUpdateTime ?? '',
    themeColor: '#3b82f6', // 默认主题色
    currentLevelName: vo.voLevelName ?? vo.VoLevelName ?? '新手',
    nextLevel: (vo.voCurrentLevel ?? vo.VoCurrentLevel ?? 1) + 1,
    expFrozen: vo.voExpFrozen ?? false,
  };
}

/**
 * 经验值交易记录映射
 */
export interface ExpTransactionData {
  id: number;
  userId: number;
  userName: string;
  expChange: number;
  expBefore: number;
  expAfter: number;
  levelBefore: number;
  levelAfter: number;
  transactionType: string;
  transactionTypeDisplay: string;
  description: string;
  relatedId: number;
  relatedType: string;
  isPositive: boolean;
  formattedExpChange: string;
  formattedDescription: string;
  createTime: string;
  updateTime: string;
  // 兼容旧字段名
  expType: string;
  expAmount: number;
  remark: string;
}

/**
 * 将ExpTransaction映射为ExpTransactionData
 */
export function mapExpTransaction(vo: any): ExpTransactionData {
  return {
    id: vo.voId ?? vo.VoId,
    userId: vo.voUserId ?? vo.VoUserId,
    userName: vo.voUserName ?? vo.VoUserName,
    expChange: vo.voExpChange ?? vo.VoExpChange,
    expBefore: vo.voExpBefore ?? vo.VoExpBefore,
    expAfter: vo.voExpAfter ?? vo.VoExpAfter,
    levelBefore: vo.voLevelBefore ?? vo.VoLevelBefore,
    levelAfter: vo.voLevelAfter ?? vo.VoLevelAfter,
    transactionType: vo.voTransactionType ?? vo.VoTransactionType,
    transactionTypeDisplay: vo.voTransactionTypeDisplay ?? vo.VoTransactionTypeDisplay,
    description: vo.voDescription ?? vo.VoDescription,
    relatedId: vo.voRelatedId ?? vo.VoRelatedId,
    relatedType: vo.voRelatedType ?? vo.VoRelatedType,
    isPositive: vo.voIsPositive ?? vo.VoIsPositive,
    formattedExpChange: vo.voFormattedExpChange ?? vo.VoFormattedExpChange,
    formattedDescription: vo.voFormattedDescription ?? vo.VoFormattedDescription,
    createTime: vo.voCreateTime ?? vo.VoCreateTime,
    updateTime: vo.voUpdateTime ?? vo.VoUpdateTime,
    // 兼容旧字段名
    expType: vo.voTransactionType ?? vo.VoTransactionType,
    expAmount: vo.voExpChange ?? vo.VoExpChange,
    remark: vo.voDescription ?? vo.VoDescription,
  };
}

/**
 * 排行榜项目映射
 */
export interface LeaderboardItemData {
  userId: number;
  userName: string;
  avatar: string;
  currentLevel: number;
  totalExp: number;
  levelName: string;
  rank: number;
  expGainedThisWeek: number;
  expGainedThisMonth: number;
  isCurrentUser: boolean;
  themeColor?: string;
  currentLevelName?: string;
}

/**
 * 将LeaderboardItem映射为LeaderboardItemData
 */
export function mapLeaderboardItem(vo: any): LeaderboardItemData {
  return {
    userId: parseInt(vo.voUserId || vo.VoUserId, 10) || 0,
    userName: vo.voUserName || vo.VoUserName || '未知用户',
    avatar: vo.voAvatarUrl || vo.VoAvatar || vo.VoAvatarUrl || '',
    currentLevel: vo.voCurrentLevel || vo.VoCurrentLevel || 1,
    totalExp: parseInt(vo.voTotalExp || vo.VoTotalExp, 10) || 0,
    levelName: vo.voCurrentLevelName || vo.VoLevelName || vo.voLevelName || '新手',
    rank: vo.voRank || vo.VoRank || 0,
    expGainedThisWeek: vo.voExpGainedThisWeek || vo.VoExpGainedThisWeek || 0,
    expGainedThisMonth: vo.voExpGainedThisMonth || vo.VoExpGainedThisMonth || 0,
    isCurrentUser: vo.voIsCurrentUser || vo.VoIsCurrentUser || false,
    themeColor: vo.voThemeColor || vo.VoThemeColor || '#3b82f6', // 默认主题色
    currentLevelName: vo.voCurrentLevelName || vo.VoLevelName || vo.voLevelName || '新手',
  };
}

/**
 * 通知信息映射
 */
export interface NotificationData {
  id: number;
  userId: number;
  title: string;
  content: string;
  type: string;
  typeDisplay: string;
  isRead: boolean;
  relatedId: number;
  relatedType: string;
  relatedUrl: string;
  icon: string;
  color: string;
  priority: number;
  expiresAt: string;
  readAt: string;
  createTime: string;
  updateTime: string;
}

/**
 * 将Notification映射为NotificationData
 */
export function mapNotification(vo: any): NotificationData {
  return {
    id: vo.voId ?? vo.VoId,
    userId: vo.voUserId ?? vo.VoUserId,
    title: vo.voTitle ?? vo.VoTitle,
    content: vo.voContent ?? vo.VoContent,
    type: vo.voType ?? vo.VoType,
    typeDisplay: vo.voTypeDisplay ?? vo.VoTypeDisplay,
    isRead: vo.voIsRead ?? vo.VoIsRead,
    relatedId: vo.voRelatedId ?? vo.VoRelatedId,
    relatedType: vo.voRelatedType ?? vo.VoRelatedType,
    relatedUrl: vo.voRelatedUrl ?? vo.VoRelatedUrl,
    icon: vo.voIcon ?? vo.VoIcon,
    color: vo.voColor ?? vo.VoColor,
    priority: vo.voPriority ?? vo.VoPriority,
    expiresAt: vo.voExpiresAt ?? vo.VoExpiresAt,
    readAt: vo.voReadAt ?? vo.VoReadAt,
    createTime: vo.voCreateTime ?? vo.VoCreateTime,
    updateTime: vo.voUpdateTime ?? vo.VoUpdateTime,
  };
}
