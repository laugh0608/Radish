/**
 * 萝卜坑应用自定义Hooks
 */

import { useState, useEffect, useCallback, useMemo } from 'react';
import { log } from '@/utils/logger';
import { useUserStore } from '@/stores/userStore';
import { coinApi } from '@/api/coin';
import type {
  AccountStats,
  TransferFormData,
  TransferResult,
  SecurityStatus,
  StatisticsData,
  NotificationItem
} from '../types';

/**
 * 萝卜余额Hook
 */
export const useCoinBalance = () => {
  const { isAuthenticated } = useUserStore();
  const [balance, setBalance] = useState<number>(0);
  const [frozenBalance, setFrozenBalance] = useState<number>(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchBalance = useCallback(async () => {
    if (!isAuthenticated()) {
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const balanceData = await coinApi.getBalance();
      setBalance(balanceData.voBalance);
      setFrozenBalance(balanceData.voFrozenBalance || 0);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : '获取存量失败';
      setError(errorMessage);
      log.error('获取萝卜余额失败:', err);
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated]);

  useEffect(() => {
    fetchBalance();
  }, [fetchBalance]);

  return {
    balance,
    frozenBalance,
    loading,
    error,
    refetch: fetchBalance
  };
};

/**
 * 账户统计Hook
 */
export const useAccountStats = () => {
  const { isAuthenticated } = useUserStore();
  const [stats, setStats] = useState<AccountStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchStats = useCallback(async () => {
    if (!isAuthenticated()) {
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const balanceData = await coinApi.getBalance();

      // 获取最近交易数量
      const recentTransactions = await coinApi.getTransactions(1, 10);

      setStats({
        totalEarned: balanceData.voTotalEarned || 0,
        totalSpent: balanceData.voTotalSpent || 0,
        totalTransferredIn: balanceData.voTotalTransferredIn || 0,
        totalTransferredOut: balanceData.voTotalTransferredOut || 0,
        recentTransactionCount: recentTransactions.voItems?.length || 0
      });
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : '获取统计信息失败';
      setError(errorMessage);
      log.error('获取账户统计失败:', err);
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated]);

  useEffect(() => {
    fetchStats();
  }, [fetchStats]);

  return {
    stats,
    loading,
    error,
    refetch: fetchStats
  };
};

/**
 * 转账Hook
 */
export const useTransfer = () => {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const transfer = useCallback(async (formData: TransferFormData): Promise<TransferResult> => {
    try {
      setLoading(true);
      setError(null);

      // TODO: 实现转账API调用
      // 这里需要等待后端转账接口实现
      await new Promise(resolve => setTimeout(resolve, 1000)); // 模拟API调用

      // 模拟成功结果
      const result: TransferResult = {
        success: true,
        transactionNo: `TXN_${Date.now()}`,
        message: '转移成功'
      };

      log.info('转账成功:', result);
      return result;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : '转移失败';
      setError(errorMessage);
      log.error('转账失败:', err);

      return {
        success: false,
        message: errorMessage
      };
    } finally {
      setLoading(false);
    }
  }, []);

  return {
    transfer,
    loading,
    error
  };
};

/**
 * 安全状态Hook
 */
export const useSecurityStatus = () => {
  const { isAuthenticated } = useUserStore();
  const [status, setStatus] = useState<SecurityStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchSecurityStatus = useCallback(async () => {
    if (!isAuthenticated()) {
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);

      // TODO: 实现安全状态API调用
      // 这里需要等待后端支付密码接口实现
      await new Promise(resolve => setTimeout(resolve, 500)); // 模拟API调用

      // 模拟安全状态
      setStatus({
        hasPaymentPassword: false,
        failedAttempts: 0,
        isLocked: false
      });
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : '获取安全状态失败';
      setError(errorMessage);
      log.error('获取安全状态失败:', err);
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated]);

  useEffect(() => {
    fetchSecurityStatus();
  }, [fetchSecurityStatus]);

  return {
    status,
    loading,
    error,
    refetch: fetchSecurityStatus
  };
};

/**
 * 统计数据Hook
 */
export const useStatistics = (timeRange: 'month' | 'quarter' | 'year' = 'month') => {
  const { isAuthenticated } = useUserStore();
  const [data, setData] = useState<StatisticsData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchStatistics = useCallback(async () => {
    if (!isAuthenticated()) {
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);

      // TODO: 实现统计数据API调用
      // 这里需要等待后端统计接口实现
      await new Promise(resolve => setTimeout(resolve, 800)); // 模拟API调用

      // 模拟统计数据
      const mockData: StatisticsData = {
        monthlyIncome: [1200, 1500, 1800, 2100, 1900, 2200],
        monthlyExpense: [800, 900, 1200, 1100, 1300, 1000],
        categoryStats: [
          { category: '点赞奖励', amount: 5000, count: 50 },
          { category: '评论奖励', amount: 3000, count: 30 },
          { category: '神评奖励', amount: 2000, count: 10 },
          { category: '系统赠送', amount: 1000, count: 5 }
        ],
        trendData: Array.from({ length: 30 }, (_, i) => ({
          date: new Date(Date.now() - (29 - i) * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
          income: Math.floor(Math.random() * 200) + 50,
          expense: Math.floor(Math.random() * 150) + 20
        }))
      };

      setData(mockData);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : '获取统计数据失败';
      setError(errorMessage);
      log.error('获取统计数据失败:', err);
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated, timeRange]);

  useEffect(() => {
    fetchStatistics();
  }, [fetchStatistics]);

  return {
    data,
    loading,
    error,
    refetch: fetchStatistics
  };
};

/**
 * 通知Hook
 */
export const useNotifications = () => {
  const { isAuthenticated } = useUserStore();
  const [notifications, setNotifications] = useState<NotificationItem[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchNotifications = useCallback(async () => {
    if (!isAuthenticated()) {
      setLoading(false);
      return;
    }

    try {
      setLoading(true);
      setError(null);

      // TODO: 实现通知API调用
      // 这里需要等待后端通知接口实现
      await new Promise(resolve => setTimeout(resolve, 600)); // 模拟API调用

      // 模拟通知数据
      const mockNotifications: NotificationItem[] = [
        {
          id: '1',
          type: 'transaction',
          title: '收到转移',
          content: '用户张三向您转移了500胡萝卜',
          amount: 500,
          isRead: false,
          createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString()
        },
        {
          id: '2',
          type: 'transaction',
          title: '点赞奖励',
          content: '您的帖子获得点赞，获得100胡萝卜奖励',
          amount: 100,
          isRead: false,
          createdAt: new Date(Date.now() - 5 * 60 * 60 * 1000).toISOString()
        },
        {
          id: '3',
          type: 'security',
          title: '安全提醒',
          content: '建议您设置支付密码以保护账户安全',
          isRead: true,
          createdAt: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString()
        }
      ];

      setNotifications(mockNotifications);
      setUnreadCount(mockNotifications.filter(n => !n.isRead).length);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : '获取通知失败';
      setError(errorMessage);
      log.error('获取通知失败:', err);
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated]);

  const markAsRead = useCallback(async (notificationId: string) => {
    try {
      // TODO: 实现标记已读API调用
      setNotifications(prev =>
        prev.map(n =>
          n.id === notificationId ? { ...n, isRead: true } : n
        )
      );
      setUnreadCount(prev => Math.max(0, prev - 1));
    } catch (err) {
      log.error('标记通知已读失败:', err);
    }
  }, []);

  const markAllAsRead = useCallback(async () => {
    try {
      // TODO: 实现标记全部已读API调用
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
      setUnreadCount(0);
    } catch (err) {
      log.error('标记全部通知已读失败:', err);
    }
  }, []);

  useEffect(() => {
    fetchNotifications();
  }, [fetchNotifications]);

  return {
    notifications,
    unreadCount,
    loading,
    error,
    refetch: fetchNotifications,
    markAsRead,
    markAllAsRead
  };
};

/**
 * API基础URL Hook
 */
export const useApiBaseUrl = () => {
  return useMemo(() => {
    if (typeof window !== 'undefined') {
      return window.location.origin;
    }
    return 'https://localhost:5000';
  }, []);
};