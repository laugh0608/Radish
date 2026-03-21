import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { getBalance, type UserBalance } from '@/api/coin';
import { useUserStore } from '@/stores/userStore';
import styles from './CoinBalance.module.css';

/**
 * 萝卜币余额显示组件
 *
 * 显示用户的萝卜币余额，支持切换显示模式（胡萝卜/白萝卜）
 */
export const CoinBalance = () => {
  const { t } = useTranslation();
  const { isAuthenticated } = useUserStore();
  const [balance, setBalance] = useState<UserBalance | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [displayMode, setDisplayMode] = useState<'carrot' | 'radish'>('carrot');

  const fetchBalance = async () => {
    if (!isAuthenticated()) {
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const result = await getBalance(t);
      if (result) {
        setBalance(result);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : t('desktop.coinBalance.loadFailed'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void fetchBalance();
    // 每 30 秒刷新一次余额
    const interval = setInterval(() => {
      void fetchBalance();
    }, 30000);

    return () => clearInterval(interval);
  }, [isAuthenticated()]);

  if (!isAuthenticated()) {
    return null;
  }

  if (loading && !balance) {
    return (
      <div className={styles.coinBalance}>
        <span className={styles.icon}>🥕</span>
        <span className={styles.loading}>...</span>
      </div>
    );
  }

  if (error || !balance) {
    return (
      <div className={styles.coinBalance} title={error || t('desktop.coinBalance.loadFailed')}>
        <span className={styles.icon}>🥕</span>
        <span className={styles.error}>--</span>
      </div>
    );
  }

  const handleToggleMode = () => {
    setDisplayMode(prev => prev === 'carrot' ? 'radish' : 'carrot');
  };

  return (
    <div
      className={styles.coinBalance}
      onClick={handleToggleMode}
      title={
        balance
          ? t('desktop.coinBalance.toggleTitle', {
              carrot: (balance.voBalance || 0).toLocaleString(),
              radish: balance.voBalanceDisplay || t('profile.wallet.whiteRadishAmount', { amount: 0 }),
            })
          : t('desktop.coinBalance.loading')
      }
    >
      <span className={styles.icon}>
        {displayMode === 'carrot' ? '🥕' : '🌿'}
      </span>
      <span className={styles.amount}>
        {balance ? (
          displayMode === 'carrot'
            ? (balance.voBalance || 0).toLocaleString()
            : (balance.voBalanceDisplay || t('profile.wallet.whiteRadishAmount', { amount: 0 }))
        ) : (
          loading ? '...' : (error ? t('desktop.coinBalance.error') : '0')
        )}
      </span>
      <span className={styles.unit}>
        {displayMode === 'carrot' ? t('profile.wallet.carrotUnit') : t('profile.coin.whiteRadish')}
      </span>
    </div>
  );
};
