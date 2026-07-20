import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { log } from '@/utils/logger';
import { useUserStore } from '@/stores/userStore';
import { coinApi, type CoinAmount } from '@/api/coin';
import { paymentPasswordApi } from '@/api/paymentPassword';
import { isPaymentPasscodeUpgradeRequiredError } from '@/utils/paymentPasscode';
import type {
  AccountStats,
  TransferFormData,
  TransferResult,
  SecurityStatus,
  StatisticsData,
} from '../types';

export const useCoinBalance = () => {
  const { t } = useTranslation();
  const { isAuthenticated } = useUserStore();
  const [balance, setBalance] = useState<CoinAmount>('0');
  const [frozenBalance, setFrozenBalance] = useState<CoinAmount>('0');
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
      const balanceData = await coinApi.getBalance(t);
      setBalance(balanceData.voBalance);
      setFrozenBalance(balanceData.voFrozenBalance);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : t('pit.api.balanceFailed');
      setError(errorMessage);
      log.error('获取萝卜余额失败:', err);
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated, t]);

  useEffect(() => {
    void fetchBalance();
  }, [fetchBalance]);

  return { balance, frozenBalance, loading, error, refetch: fetchBalance };
};

export const useAccountStats = () => {
  const { t } = useTranslation();
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
      const [balanceData, recentTransactions] = await Promise.all([
        coinApi.getBalance(t),
        coinApi.getTransactions(1, 10, null, null, t),
      ]);

      setStats({
        totalEarned: balanceData.voTotalEarned,
        totalSpent: balanceData.voTotalSpent,
        totalTransferredIn: balanceData.voTotalTransferredIn,
        totalTransferredOut: balanceData.voTotalTransferredOut,
        recentTransactionCount: recentTransactions.data.length,
      });
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : t('pit.api.statisticsFailed');
      setError(errorMessage);
      log.error('获取账户统计失败:', err);
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated, t]);

  useEffect(() => {
    void fetchStats();
  }, [fetchStats]);

  return { stats, loading, error, refetch: fetchStats };
};

export const useTransfer = () => {
  const { t } = useTranslation();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const transfer = useCallback(async (formData: TransferFormData): Promise<TransferResult> => {
    try {
      setLoading(true);
      setError(null);
      const result = await coinApi.transfer({
        toUserId: formData.recipientId,
        amount: formData.amount,
        remark: formData.note,
        paymentPassword: formData.paymentPassword,
        idempotencyKey: formData.idempotencyKey,
      }, t);

      log.info('转账成功:', result);
      return {
        success: true,
        transactionNo: result.voTransactionNo,
        message: t('pit.transfer.result.successMessage', { recipient: formData.recipientName }),
      };
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : t('pit.api.transferFailed');
      setError(errorMessage);
      log.error('转账失败:', err);
      const requiresPasscodeUpgrade = isPaymentPasscodeUpgradeRequiredError({
        code: err && typeof err === 'object' && 'code' in err ? String(err.code ?? '') : undefined,
      });

      return {
        success: false,
        message: errorMessage,
        requiresPasscodeUpgrade,
      };
    } finally {
      setLoading(false);
    }
  }, [t]);

  return { transfer, loading, error };
};

export const useSecurityStatus = () => {
  const { t } = useTranslation();
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
      const passwordStatus = await paymentPasswordApi.getPaymentPasswordStatus(t);

      if (passwordStatus) {
        setStatus({
          hasPaymentPassword: passwordStatus.voHasPaymentPassword,
          isLegacyPasscode: passwordStatus.voIsLegacyPasscode,
          requiresPasscodeUpgrade: passwordStatus.voRequiresPasscodeUpgrade,
          lastPasswordChangeTime: passwordStatus.voLastModifiedTime ?? undefined,
          lastPasswordUsedTime: passwordStatus.voLastUsedTime ?? undefined,
          failedAttempts: passwordStatus.voFailedAttempts,
          isLocked: passwordStatus.voIsLocked,
          lockedUntil: passwordStatus.voLockedUntil ?? undefined,
          lockedRemainingMinutes: passwordStatus.voLockedRemainingMinutes,
          strengthLevel: passwordStatus.voStrengthLevel,
        });
      } else {
        setStatus({
          hasPaymentPassword: false,
          isLegacyPasscode: false,
          requiresPasscodeUpgrade: false,
          failedAttempts: 0,
          isLocked: false,
        });
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : t('pit.api.securityStatusFailed');
      setError(errorMessage);
      log.error('获取安全状态失败:', err);
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated, t]);

  useEffect(() => {
    void fetchSecurityStatus();
  }, [fetchSecurityStatus]);

  return { status, loading, error, refetch: fetchSecurityStatus };
};

export const useStatistics = (timeRange: 'month' | 'quarter' | 'year' = 'month') => {
  const { t } = useTranslation();
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
      setData(await coinApi.getStatistics(timeRange, t));
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : t('pit.api.statisticsFailed');
      setError(errorMessage);
      log.error('获取统计数据失败:', err);
    } finally {
      setLoading(false);
    }
  }, [isAuthenticated, t, timeRange]);

  useEffect(() => {
    void fetchStatistics();
  }, [fetchStatistics]);

  return { data, loading, error, refetch: fetchStatistics };
};
