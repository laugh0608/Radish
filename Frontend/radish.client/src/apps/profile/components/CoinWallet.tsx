import { useState, useEffect, useEffectEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { getBalance, type UserBalance } from '@/api/coin';
import {
  compareCoinValues,
  formatCoinAmount,
  formatCoinNumber,
} from '@/coin/coinPresentation';
import { CoinTransactionList } from './CoinTransactionList';
import styles from './CoinWallet.module.css';

interface CoinWalletProps {
  apiBaseUrl: string;
}

/**
 * 萝卜币钱包组件
 *
 * 显示用户余额信息和交易记录
 */
export const CoinWallet = ({ apiBaseUrl }: CoinWalletProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const [balance, setBalance] = useState<UserBalance | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadBalance = async () => {
    setLoading(true);
    setError(null);

    try {
      const result = await getBalance(t);
      if (result) {
        setBalance(result);
      } else {
        setError(t('profile.wallet.getBalanceFailed'));
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : t('profile.wallet.getBalanceFailed'));
    } finally {
      setLoading(false);
    }
  };

  const loadBalanceForEffect = useEffectEvent(loadBalance);

  useEffect(() => {
    void loadBalanceForEffect();
  }, [loadBalanceForEffect]);

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>{t('common.loading')}</div>
      </div>
    );
  }

  if (error || !balance) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>{error || t('profile.wallet.loadBalanceFailed')}</div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.balanceCard}>
        <div className={styles.cardHeader}>
          <h3 className={styles.cardTitle}>
            <span className={styles.icon}>🥕</span>
            {t('profile.wallet.title')}
          </h3>
          <button className={styles.refreshButton} onClick={loadBalance} title={t('profile.wallet.refresh')}>
            🔄
          </button>
        </div>

        <div className={styles.balanceMain}>
          <div className={styles.balanceItem}>
            <div className={styles.balanceLabel}>{t('profile.wallet.availableBalance')}</div>
              <div className={styles.balanceValue}>
              <span className={styles.carrotAmount}>{formatCoinNumber(balance.voBalance, language)}</span>
              <span className={styles.unit}>{t('profile.wallet.carrotUnit')}</span>
            </div>
            <div className={styles.balanceValueAlt}>
              {formatCoinAmount(balance.voBalance, language, t, 'white')}
            </div>
          </div>

          {compareCoinValues(balance.voFrozenBalance, 0) > 0 && (
            <div className={styles.balanceItem}>
              <div className={styles.balanceLabel}>{t('profile.wallet.frozenBalance')}</div>
              <div className={styles.balanceValue}>
                <span className={styles.carrotAmount}>{formatCoinNumber(balance.voFrozenBalance, language)}</span>
                <span className={styles.unit}>{t('profile.wallet.carrotUnit')}</span>
              </div>
              <div className={styles.balanceValueAlt}>
                {formatCoinAmount(balance.voFrozenBalance, language, t, 'white')}
              </div>
            </div>
          )}
        </div>

        <div className={styles.statsGrid}>
          <div className={styles.statItem}>
            <div className={styles.statValue}>+{formatCoinNumber(balance.voTotalEarned, language)}</div>
            <div className={styles.statLabel}>{t('profile.wallet.totalEarned')}</div>
          </div>
          <div className={styles.statItem}>
            <div className={styles.statValue}>-{formatCoinNumber(balance.voTotalSpent, language)}</div>
            <div className={styles.statLabel}>{t('profile.wallet.totalSpent')}</div>
          </div>
          <div className={styles.statItem}>
            <div className={styles.statValue}>+{formatCoinNumber(balance.voTotalTransferredIn, language)}</div>
            <div className={styles.statLabel}>{t('profile.wallet.totalTransferredIn')}</div>
          </div>
          <div className={styles.statItem}>
            <div className={styles.statValue}>-{formatCoinNumber(balance.voTotalTransferredOut, language)}</div>
            <div className={styles.statLabel}>{t('profile.wallet.totalTransferredOut')}</div>
          </div>
        </div>

        <div className={styles.conversionTip}>
          {t('profile.wallet.conversionTip')}
        </div>
      </div>

      <div className={styles.transactionsSection}>
        <h3 className={styles.sectionTitle}>{t('profile.transactions.title')}</h3>
        <CoinTransactionList apiBaseUrl={apiBaseUrl} />
      </div>
    </div>
  );
};
