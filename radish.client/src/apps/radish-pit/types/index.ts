/**
 * 萝卜坑应用类型定义
 */

// 导航标签类型
export type TabType = 'overview' | 'transfer' | 'history' | 'security' | 'statistics';

// 账户统计信息
export interface AccountStats {
  totalEarned: number;        // 累计获得
  totalSpent: number;         // 累计消费
  totalTransferredIn: number; // 累计转入
  totalTransferredOut: number; // 累计转出
  recentTransactionCount: number; // 最近交易数量
}

// 转账表单数据
export interface TransferFormData {
  recipientId: number;
  recipientName: string;
  amount: number;
  note?: string;
  paymentPassword: string;
}

// 转账结果
export interface TransferResult {
  success: boolean;
  transactionNo?: string;
  message: string;
}

// 安全设置状态
export interface SecurityStatus {
  hasPaymentPassword: boolean;
  lastPasswordChangeTime?: string;
  failedAttempts: number;
  isLocked: boolean;
  lockedUntil?: string;
}

// 统计数据
export interface StatisticsData {
  monthlyIncome: number[];
  monthlyExpense: number[];
  categoryStats: {
    category: string;
    amount: number;
    count: number;
  }[];
  trendData: {
    date: string;
    income: number;
    expense: number;
  }[];
}

// 通知类型
export interface NotificationItem {
  id: string;
  type: 'transaction' | 'security' | 'system';
  title: string;
  content: string;
  amount?: number;
  isRead: boolean;
  createdAt: string;
}

// 词汇替换映射
export const VOCABULARY_MAP = {
  wallet: '萝卜坑',
  payment: '转移',
  recharge: '收纳',
  withdraw: '取出',
  balance: '存量',
  transfer: '转移',
  transaction: '记录',
  security: '安全',
  statistics: '统计'
} as const;