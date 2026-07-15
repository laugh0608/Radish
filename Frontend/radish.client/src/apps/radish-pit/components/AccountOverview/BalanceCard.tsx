import { useTranslation } from 'react-i18next';
import type { CoinAmount } from '@/api/coin';
import { addCoinValues, compareCoinValues, formatCoinAmount } from '../../utils';
import styles from './BalanceCard.module.css';

interface BalanceCardProps {
  balance: CoinAmount;
  frozenBalance: CoinAmount;
  displayMode: 'carrot' | 'white';
}

/**
 * 余额卡片组件
 */
export const BalanceCard = ({ balance, frozenBalance, displayMode }: BalanceCardProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const totalBalance = addCoinValues(balance, frozenBalance);

  return (
    <div className={styles.card}>
      <div className={styles.header}>
        <h3 className={styles.title}>
          <span className={styles.icon}>💰</span>
          {t('pit.balance.title')}
        </h3>
        <div className={styles.displayMode}>
          {t(displayMode === 'carrot' ? 'pit.currency.carrotMode' : 'pit.currency.whiteMode')}
        </div>
      </div>

      <div className={styles.content}>
        {/* 主要余额 */}
        <div className={styles.mainBalance}>
          <div className={styles.balanceAmount}>
            {formatCoinAmount(balance, language, t, displayMode, false)}
          </div>
          <div className={styles.balanceUnit}>
            {t(displayMode === 'white' ? 'pit.currency.white' : 'pit.currency.carrot')}
          </div>
          <div className={styles.balanceLabel}>{t('pit.balance.available')}</div>
        </div>

        {/* 余额详情 */}
        <div className={styles.balanceDetails}>
          <div className={styles.detailItem}>
            <div className={styles.detailLabel}>{t('pit.balance.frozen')}</div>
            <div className={styles.detailValue}>
              {formatCoinAmount(frozenBalance, language, t, displayMode)}
            </div>
          </div>
          <div className={styles.detailItem}>
            <div className={styles.detailLabel}>{t('pit.balance.total')}</div>
            <div className={styles.detailValue}>
              {formatCoinAmount(totalBalance, language, t, displayMode)}
            </div>
          </div>
        </div>

        {/* 兑换比例提示 */}
        {displayMode === 'white' && (
          <div className={styles.exchangeRate}>
            <span className={styles.exchangeIcon}>ℹ️</span>
            {t('pit.currency.exchangeRate', { value: formatCoinAmount(1000, language, t) })}
          </div>
        )}
      </div>

      {/* 余额状态指示器 */}
      <div className={styles.statusIndicator}>
        <div className={`${styles.statusDot} ${compareCoinValues(balance, 0) > 0 ? styles.active : styles.inactive}`}></div>
        <span className={styles.statusText}>
          {t(compareCoinValues(balance, 0) > 0 ? 'pit.balance.normal' : 'pit.balance.empty')}
        </span>
      </div>
    </div>
  );
};
