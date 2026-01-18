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
    id: vo.VoId,
    name: vo.VoName,
    slug: vo.VoSlug,
    description: vo.VoDescription,
    icon: vo.VoIcon,
    coverImage: vo.VoCoverImage,
    parentId: vo.VoParentId,
    level: vo.VoLevel,
    orderSort: vo.VoOrderSort,
    postCount: vo.VoPostCount,
    isEnabled: vo.VoIsEnabled,
    createTime: vo.VoCreateTime,
    createBy: vo.VoCreateBy,
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
    id: vo.VoId,
    title: vo.VoTitle,
    slug: vo.VoSlug,
    summary: vo.VoSummary,
    categoryId: vo.VoCategoryId,
    categoryName: vo.VoCategoryName,
    authorId: vo.VoAuthorId,
    authorName: vo.VoAuthorName,
    viewCount: vo.VoViewCount,
    likeCount: vo.VoLikeCount,
    commentCount: vo.VoCommentCount,
    isTop: vo.VoIsTop,
    isEssence: vo.VoIsEssence,
    isLocked: vo.VoIsLocked,
    createTime: vo.VoCreateTime,
    updateTime: vo.VoUpdateTime,
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
  const tags = vo.VoTags;
  return {
    id: vo.VoId,
    title: vo.VoTitle,
    slug: vo.VoSlug,
    summary: vo.VoSummary,
    content: vo.VoContent,
    contentType: vo.VoContentType,
    coverImage: vo.VoCoverImage,
    categoryId: vo.VoCategoryId,
    categoryName: vo.VoCategoryName,
    authorId: vo.VoAuthorId,
    authorName: vo.VoAuthorName,
    tags: tags,
    tagNames: tags ? tags.split(',').map((t: string) => t.trim()) : [],
    viewCount: vo.VoViewCount,
    likeCount: vo.VoLikeCount,
    commentCount: vo.VoCommentCount,
    isTop: vo.VoIsTop,
    isEssence: vo.VoIsEssence,
    isLocked: vo.VoIsLocked,
    createTime: vo.VoCreateTime,
    updateTime: vo.VoUpdateTime,
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
    id: vo.VoId,
    postId: vo.VoPostId,
    content: vo.VoContent,
    authorId: vo.VoAuthorId,
    authorName: vo.VoAuthorName,
    parentId: vo.VoParentId,
    rootId: vo.VoRootId,
    replyToUserId: vo.VoReplyToUserId,
    replyToUserName: vo.VoReplyToUserName,
    level: vo.VoLevel,
    likeCount: vo.VoLikeCount,
    replyCount: vo.VoReplyCount,
    isTop: vo.VoIsTop,
    isLiked: vo.VoIsLiked,
    createTime: vo.VoCreateTime,
    updateTime: vo.VoUpdateTime,
    // 前端扩展字段（如果后端返回的话）
    children: vo.VoChildren ? vo.VoChildren.map(mapComment) : undefined,
    childrenTotal: vo.VoChildrenTotal,
    isGodComment: vo.VoIsGodComment,
    isSofa: vo.VoIsSofa,
    highlightRank: vo.VoHighlightRank,
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
    id: vo.VoId,
    postId: vo.VoPostId,
    commentId: vo.VoCommentId,
    parentCommentId: vo.VoParentCommentId,
    highlightType: vo.VoHighlightType,
    statDate: vo.VoStatDate,
    likeCount: vo.VoLikeCount,
    rank: vo.VoRank,
    contentSnapshot: vo.VoContentSnapshot,
    authorId: vo.VoAuthorId,
    authorName: vo.VoAuthorName,
    isCurrent: vo.VoIsCurrent,
    createTime: vo.VoCreateTime,
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
  return {
    id: vo.VoId?.toString() || vo.VoSlug,
    name: vo.VoName,
    icon: vo.VoIcon,
    description: vo.VoDescription,
    sortOrder: vo.VoOrderSort,
    isEnabled: vo.VoIsEnabled,
    productCount: vo.VoProductCount || 0,
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
    id: vo.VoId,
    name: vo.VoName,
    icon: vo.VoIcon,
    coverImage: vo.VoCoverImage,
    categoryId: vo.VoCategoryId,
    productType: vo.VoProductType,
    price: vo.VoPrice,
    originalPrice: vo.VoOriginalPrice,
    hasDiscount: vo.VoHasDiscount,
    soldCount: vo.VoSoldCount,
    inStock: vo.VoInStock,
    durationDisplay: vo.VoDurationDisplay,
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
    id: vo.VoId,
    name: vo.VoName,
    description: vo.VoDescription,
    icon: vo.VoIcon,
    coverImage: vo.VoCoverImage,
    categoryId: vo.VoCategoryId,
    categoryName: vo.VoCategoryName,
    productType: vo.VoProductType,
    productTypeDisplay: vo.VoProductTypeDisplay,
    benefitType: vo.VoBenefitType,
    consumableType: vo.VoConsumableType,
    benefitValue: vo.VoBenefitValue,
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
    onSaleTime: vo.VoOnSaleTime,
    offSaleTime: vo.VoOffSaleTime,
    createTime: vo.VoCreateTime,
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
    id: vo.VoId,
    orderNo: vo.VoOrderNo,
    productName: vo.VoProductName,
    productIcon: vo.VoProductIcon,
    quantity: vo.VoQuantity,
    totalPrice: vo.VoTotalPrice,
    status: vo.VoStatus,
    statusDisplay: vo.VoStatusDisplay,
    createTime: vo.VoCreateTime,
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
    id: vo.VoId,
    orderNo: vo.VoOrderNo,
    userId: vo.VoUserId,
    userName: vo.VoUserName,
    productId: vo.VoProductId,
    productName: vo.VoProductName,
    productIcon: vo.VoProductIcon,
    productType: vo.VoProductType,
    productTypeDisplay: vo.VoProductTypeDisplay,
    benefitType: vo.VoBenefitType,
    consumableType: vo.VoConsumableType,
    quantity: vo.VoQuantity,
    unitPrice: vo.VoUnitPrice,
    totalPrice: vo.VoTotalPrice,
    status: vo.VoStatus,
    statusDisplay: vo.VoStatusDisplay,
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
    id: vo.VoId,
    userId: vo.VoUserId,
    benefitType: vo.VoBenefitType,
    benefitTypeDisplay: vo.VoBenefitTypeDisplay,
    benefitValue: vo.VoBenefitValue,
    sourceId: vo.VoSourceId,
    sourceType: vo.VoSourceType,
    sourceTypeDisplay: vo.VoSourceTypeDisplay,
    durationType: vo.VoDurationType,
    effectiveAt: vo.VoEffectiveAt,
    expiresAt: vo.VoExpiresAt,
    isExpired: vo.VoIsExpired,
    isActive: vo.VoIsActive,
    durationDisplay: vo.VoDurationDisplay,
    createTime: vo.VoCreateTime,
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
    id: vo.VoId,
    userId: vo.VoUserId,
    consumableType: vo.VoConsumableType,
    consumableTypeDisplay: vo.VoConsumableTypeDisplay,
    itemValue: vo.VoItemValue,
    itemName: vo.VoItemName,
    itemIcon: vo.VoItemIcon,
    quantity: vo.VoQuantity,
    createTime: vo.VoCreateTime,
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
  return {
    userId: vo.VoUserId?.toString() || '0',
    currentExp: vo.VoCurrentExp || 0,
    currentLevel: vo.VoCurrentLevel || 1,
    nextLevelExp: vo.VoNextLevelExp || 0,
    totalExp: vo.VoTotalExp || 0,
    levelProgress: vo.VoLevelProgress || 0,
    levelName: vo.VoLevelName || '新手',
    levelDescription: vo.VoLevelDescription || '',
    canLevelUp: vo.VoCanLevelUp || false,
    nextLevelName: vo.VoNextLevelName || '新手',
    expToNextLevel: vo.VoExpToNextLevel || 0,
    expGainedToday: vo.VoExpGainedToday || 0,
    dailyExpLimit: vo.VoDailyExpLimit || 0,
    remainingDailyExp: vo.VoRemainingDailyExp || 0,
    isMaxLevel: vo.VoIsMaxLevel || false,
    rank: vo.VoRank || 0,
    percentile: vo.VoPercentile || 0,
    createTime: vo.VoCreateTime || '',
    updateTime: vo.VoUpdateTime || '',
    themeColor: '#3b82f6', // 默认主题色
    currentLevelName: vo.VoLevelName || '新手',
    nextLevel: (vo.VoCurrentLevel || 1) + 1,
    expFrozen: false,
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
    id: vo.VoId,
    userId: vo.VoUserId,
    userName: vo.VoUserName,
    expChange: vo.VoExpChange,
    expBefore: vo.VoExpBefore,
    expAfter: vo.VoExpAfter,
    levelBefore: vo.VoLevelBefore,
    levelAfter: vo.VoLevelAfter,
    transactionType: vo.VoTransactionType,
    transactionTypeDisplay: vo.VoTransactionTypeDisplay,
    description: vo.VoDescription,
    relatedId: vo.VoRelatedId,
    relatedType: vo.VoRelatedType,
    isPositive: vo.VoIsPositive,
    formattedExpChange: vo.VoFormattedExpChange,
    formattedDescription: vo.VoFormattedDescription,
    createTime: vo.VoCreateTime,
    updateTime: vo.VoUpdateTime,
    // 兼容旧字段名
    expType: vo.VoTransactionType,
    expAmount: vo.VoExpChange,
    remark: vo.VoDescription,
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
    userId: vo.VoUserId,
    userName: vo.VoUserName,
    avatar: vo.VoAvatar,
    currentLevel: vo.VoCurrentLevel,
    totalExp: vo.VoTotalExp,
    levelName: vo.VoLevelName,
    rank: vo.VoRank,
    expGainedThisWeek: vo.VoExpGainedThisWeek,
    expGainedThisMonth: vo.VoExpGainedThisMonth,
    isCurrentUser: vo.VoIsCurrentUser,
    themeColor: '#3b82f6', // 默认主题色
    currentLevelName: vo.VoLevelName,
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
    id: vo.VoId,
    userId: vo.VoUserId,
    title: vo.VoTitle,
    content: vo.VoContent,
    type: vo.VoType,
    typeDisplay: vo.VoTypeDisplay,
    isRead: vo.VoIsRead,
    relatedId: vo.VoRelatedId,
    relatedType: vo.VoRelatedType,
    relatedUrl: vo.VoRelatedUrl,
    icon: vo.VoIcon,
    color: vo.VoColor,
    priority: vo.VoPriority,
    expiresAt: vo.VoExpiresAt,
    readAt: vo.VoReadAt,
    createTime: vo.VoCreateTime,
    updateTime: vo.VoUpdateTime,
  };
}