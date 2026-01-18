/**
 * ViewModel字段映射工具
 * 用于将后端的Vo前缀字段映射为前端友好的字段名
 */

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
    userId: vo.VoUserId.toString(),
    currentExp: vo.VoCurrentExp,
    currentLevel: vo.VoCurrentLevel,
    nextLevelExp: vo.VoNextLevelExp,
    totalExp: vo.VoTotalExp,
    levelProgress: vo.VoLevelProgress,
    levelName: vo.VoLevelName,
    levelDescription: vo.VoLevelDescription,
    canLevelUp: vo.VoCanLevelUp,
    nextLevelName: vo.VoNextLevelName,
    expToNextLevel: vo.VoExpToNextLevel,
    expGainedToday: vo.VoExpGainedToday,
    dailyExpLimit: vo.VoDailyExpLimit,
    remainingDailyExp: vo.VoRemainingDailyExp,
    isMaxLevel: vo.VoIsMaxLevel,
    rank: vo.VoRank,
    percentile: vo.VoPercentile,
    createTime: vo.VoCreateTime,
    updateTime: vo.VoUpdateTime,
    themeColor: '#3b82f6', // 默认主题色
    currentLevelName: vo.VoLevelName,
    nextLevel: vo.VoCurrentLevel + 1,
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