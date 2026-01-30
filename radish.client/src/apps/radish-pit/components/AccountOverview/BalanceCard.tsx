import { formatCoinAmount } from '../../utils';
import styles from './BalanceCard.module.css';

interface BalanceCardProps {
  balance: number;
  frozenBalance: number;
  displayMode: 'carrot' | 'white';
}

/**
 * 余额卡片组件
 */
export const BalanceCard = ({ balance, frozenBalance, displayMode }: BalanceCardProps) => {
  const totalBalance = balance + frozenBalance;
  const useWhiteRadish = displayMode === 'white';

  return (
    <div className={styles.card}>
      <div className={styles.header}>
        <h3 className={styles.title}>
          <span className={styles.icon}>💰</span>
          当前存量
        </h3>
        <div className={styles.displayMode}>
          {displayMode === 'carrot' ? '胡萝卜模式' : '白萝卜模式'}
        </div>
      </div>

      <div className={styles.content}>
        {/* 主要余额 */}
        <div className={styles.mainBalance}>
          <div className={styles.balanceAmount}>
            {formatCoinAmount(balance, false, useWhiteRadish)}
          </div>
          <div className={styles.balanceUnit}>
            {useWhiteRadish ? '白萝卜' : '胡萝卜'}
          </div>
          <div className={styles.balanceLabel}>可用存量</div>
        </div>

        {/* 余额详情 */}
        <div className={styles.balanceDetails}>
          <div className={styles.detailItem}>
            <div className={styles.detailLabel}>冻结存量</div>
            <div className={styles.detailValue}>
              {formatCoinAmount(frozenBalance, true, useWhiteRadish)}
            </div>
          </div>
          <div className={styles.detailItem}>
            <div className={styles.detailLabel}>总存量</div>
            <div className={styles.detailValue}>
              {formatCoinAmount(totalBalance, true, useWhiteRadish)}
            </div>
          </div>
        </div>

        {/* 兑换比例提示 */}
        {displayMode === 'white' && (
          <div className={styles.exchangeRate}>
            <span className={styles.exchangeIcon}>ℹ️</span>
            1 白萝卜 = 1,000 胡萝卜
          </div>
        )}
      </div>

      {/* 余额状态指示器 */}
      <div className={styles.statusIndicator}>
        <div className={`${styles.statusDot} ${balance > 0 ? styles.active : styles.inactive}`}></div>
        <span className={styles.statusText}>
          {balance > 0 ? '账户正常' : '存量不足'}
        </span>
      </div>
    </div>
  );
};