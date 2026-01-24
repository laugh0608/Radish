/**
 * 萝卜坑应用工具函数
 */

import { VOCABULARY_MAP } from '../types';

/**
 * 格式化萝卜币金额显示
 * @param amount 金额（胡萝卜）
 * @param showUnit 是否显示单位
 * @param useWhiteRadish 是否使用白萝卜显示
 */
export const formatCoinAmount = (
  amount: number,
  showUnit: boolean = true,
  useWhiteRadish: boolean = false
): string => {
  if (useWhiteRadish) {
    const whiteRadishAmount = (amount / 1000).toFixed(3);
    return showUnit ? `${whiteRadishAmount} 白萝卜` : whiteRadishAmount;
  }

  const formattedAmount = amount.toLocaleString();
  return showUnit ? `${formattedAmount} 胡萝卜` : formattedAmount;
};

/**
 * 格式化时间显示
 * @param dateString 时间字符串
 */
export const formatDateTime = (dateString: string): string => {
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMinutes = Math.floor(diffMs / (1000 * 60));
  const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  if (diffMinutes < 1) {
    return '刚刚';
  } else if (diffMinutes < 60) {
    return `${diffMinutes}分钟前`;
  } else if (diffHours < 24) {
    return `${diffHours}小时前`;
  } else if (diffDays < 7) {
    return `${diffDays}天前`;
  } else {
    return date.toLocaleDateString('zh-CN', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }
};

/**
 * 获取交易类型的中文显示名称
 * @param transactionType 交易类型
 */
export const getTransactionTypeDisplay = (transactionType: string): string => {
  const typeMap: Record<string, string> = {
    'SYSTEM_GRANT': '系统赠送',
    'LIKE_REWARD': '点赞奖励',
    'COMMENT_REWARD': '评论奖励',
    'GODLIKE_REWARD': '神评奖励',
    'SOFA_REWARD': '沙发奖励',
    'TRANSFER_IN': '转入',
    'TRANSFER_OUT': '转出',
    'PURCHASE': '购买消费',
    'ADMIN_ADJUST': '管理员调整'
  };

  return typeMap[transactionType] || transactionType;
};

/**
 * 获取交易状态的中文显示名称
 * @param status 交易状态
 */
export const getTransactionStatusDisplay = (status: string): string => {
  const statusMap: Record<string, string> = {
    'PENDING': '处理中',
    'SUCCESS': '成功',
    'FAILED': '失败',
    'CANCELLED': '已取消'
  };

  return statusMap[status] || status;
};

/**
 * 获取交易状态对应的颜色类名
 * @param status 交易状态
 */
export const getTransactionStatusColor = (status: string): string => {
  const colorMap: Record<string, string> = {
    'PENDING': 'warning',
    'SUCCESS': 'success',
    'FAILED': 'error',
    'CANCELLED': 'default'
  };

  return colorMap[status] || 'default';
};

/**
 * 验证转账金额
 * @param amount 转账金额
 * @param balance 当前余额
 */
export const validateTransferAmount = (amount: number, balance: number): {
  isValid: boolean;
  message?: string;
} => {
  if (amount <= 0) {
    return { isValid: false, message: '转移金额必须大于0' };
  }

  if (amount > balance) {
    return { isValid: false, message: '存量不足' };
  }

  if (amount > 100000) {
    return { isValid: false, message: '单笔转移金额不能超过10万胡萝卜' };
  }

  return { isValid: true };
};

/**
 * 生成安全的用户显示名称
 * @param userName 用户名
 * @param isCurrentUser 是否为当前用户
 */
export const getSafeUserDisplayName = (userName: string, isCurrentUser: boolean = false): string => {
  if (isCurrentUser) {
    return '我';
  }

  if (userName.length <= 2) {
    return userName;
  }

  // 对于长用户名，显示首尾字符，中间用*替代
  const firstChar = userName.charAt(0);
  const lastChar = userName.charAt(userName.length - 1);
  const middleStars = '*'.repeat(Math.min(userName.length - 2, 3));

  return `${firstChar}${middleStars}${lastChar}`;
};

/**
 * 替换敏感词汇
 * @param text 原始文本
 */
export const replaceSensitiveWords = (text: string): string => {
  let result = text;

  Object.entries(VOCABULARY_MAP).forEach(([sensitive, replacement]) => {
    const regex = new RegExp(sensitive, 'gi');
    result = result.replace(regex, replacement);
  });

  return result;
};

/**
 * 防抖函数
 * @param func 要防抖的函数
 * @param delay 延迟时间（毫秒）
 */
export const debounce = <T extends (...args: any[]) => any>(
  func: T,
  delay: number
): ((...args: Parameters<T>) => void) => {
  let timeoutId: NodeJS.Timeout;

  return (...args: Parameters<T>) => {
    clearTimeout(timeoutId);
    timeoutId = setTimeout(() => func(...args), delay);
  };
};

/**
 * 节流函数
 * @param func 要节流的函数
 * @param delay 延迟时间（毫秒）
 */
export const throttle = <T extends (...args: any[]) => any>(
  func: T,
  delay: number
): ((...args: Parameters<T>) => void) => {
  let lastCall = 0;

  return (...args: Parameters<T>) => {
    const now = Date.now();
    if (now - lastCall >= delay) {
      lastCall = now;
      func(...args);
    }
  };
};