import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import {
  absoluteCoinValue,
  formatCoinAmount,
  formatCoinNumber,
  formatCoinRelativeDateTime,
  formatTransactionStatus,
  formatTransactionType,
  getSafeUserDisplayName,
  getTransactionIcon,
  getTransactionStatusTone,
  resolveTransactionDirection,
} from '../../utils';
import { useUserStore } from '@/stores/userStore';
import type { CoinTransaction } from '@/api/coin';
import styles from './TransactionList.module.css';

interface TransactionListProps {
  transactions: CoinTransaction[];
  loading: boolean;
  error: string | null;
  displayMode: 'carrot' | 'white';
  currentPage: number;
  totalPages: number;
  totalCount: number;
  onTransactionClick: (transaction: CoinTransaction) => void;
  onPageChange: (page: number) => void;
  onRefresh: () => void;
}

/**
 * 交易列表组件
 */
export const TransactionList = ({
  transactions,
  loading,
  error,
  displayMode,
  currentPage,
  totalPages,
  totalCount,
  onTransactionClick,
  onPageChange,
  onRefresh
}: TransactionListProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const displayTimeZone = getBrowserTimeZoneId(DEFAULT_TIME_ZONE);
  const { userId } = useUserStore();
  const currentUserId = String(userId);

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>{t('pit.history.loading')}</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>
          <div className={styles.errorIcon}>⚠️</div>
          <h3>{t('pit.common.loadFailed')}</h3>
          <p>{error}</p>
          <button className={styles.retryButton} onClick={onRefresh}>
            {t('pit.common.retry')}
          </button>
        </div>
      </div>
    );
  }

  if (transactions.length === 0) {
    return (
      <div className={styles.container}>
        <div className={styles.empty}>
          <div className={styles.emptyIcon}>📝</div>
          <h3>{t('pit.history.emptyTitle')}</h3>
          <p>{t('pit.history.emptyFiltered')}</p>
          <p className={styles.emptyHint}>{t('pit.history.emptyHint')}</p>
        </div>
      </div>
    );
  }

  const renderPagination = () => {
    if (totalPages <= 1) return null;

    const pages = [];
    const maxVisiblePages = 7;
    let startPage = Math.max(1, currentPage - Math.floor(maxVisiblePages / 2));
    const endPage = Math.min(totalPages, startPage + maxVisiblePages - 1);

    if (endPage - startPage + 1 < maxVisiblePages) {
      startPage = Math.max(1, endPage - maxVisiblePages + 1);
    }

    // 首页
    if (startPage > 1) {
      pages.push(
        <button
          key={1}
          className={`${styles.pageButton} ${currentPage === 1 ? styles.active : ''}`}
          onClick={() => onPageChange(1)}
        >
          1
        </button>
      );
      if (startPage > 2) {
        pages.push(<span key="start-ellipsis" className={styles.ellipsis}>...</span>);
      }
    }

    // 中间页码
    for (let i = startPage; i <= endPage; i++) {
      pages.push(
        <button
          key={i}
          className={`${styles.pageButton} ${currentPage === i ? styles.active : ''}`}
          onClick={() => onPageChange(i)}
        >
          {i}
        </button>
      );
    }

    // 末页
    if (endPage < totalPages) {
      if (endPage < totalPages - 1) {
        pages.push(<span key="end-ellipsis" className={styles.ellipsis}>...</span>);
      }
      pages.push(
        <button
          key={totalPages}
          className={`${styles.pageButton} ${currentPage === totalPages ? styles.active : ''}`}
          onClick={() => onPageChange(totalPages)}
        >
          {totalPages}
        </button>
      );
    }

    return (
      <div className={styles.pagination}>
        <button
          className={styles.pageButton}
          onClick={() => onPageChange(currentPage - 1)}
          disabled={currentPage <= 1}
        >
          {t('pit.common.prevPage')}
        </button>
        {pages}
        <button
          className={styles.pageButton}
          onClick={() => onPageChange(currentPage + 1)}
          disabled={currentPage >= totalPages}
        >
          {t('pit.common.nextPage')}
        </button>
      </div>
    );
  };

  return (
    <div className={styles.container}>
      {/* 统计信息 */}
      <div className={styles.summary}>
        <div className={styles.summaryInfo}>
          {t('pit.history.pageSummary', {
            start: formatCoinNumber((currentPage - 1) * 20 + 1, language),
            end: formatCoinNumber(Math.min(currentPage * 20, totalCount), language),
            value: formatCoinNumber(totalCount, language),
          })}
        </div>
        <div className={styles.summaryActions}>
          <span className={styles.displayMode}>
            {t(displayMode === 'carrot' ? 'pit.currency.carrotMode' : 'pit.currency.whiteMode')}
          </span>
        </div>
      </div>

      {/* 交易列表 */}
      <div className={styles.transactionList}>
        {transactions.map((transaction) => (
          <div
            key={transaction.voId}
            className={styles.transactionItem}
            onClick={() => onTransactionClick(transaction)}
          >
            <div className={styles.transactionIcon}>
              {getTransactionIcon(transaction.voTransactionType)}
            </div>

            <div className={styles.transactionContent}>
              <div className={styles.transactionMain}>
                <div className={styles.transactionLeft}>
                  <div className={styles.transactionType}>
                    {formatTransactionType(transaction.voTransactionType, t)}
                  </div>
                  <div className={styles.transactionParties}>
                    {renderTransactionParties(transaction, currentUserId, t)}
                  </div>
                </div>

                <div className={styles.transactionRight}>
                  <div className={styles.transactionAmount}>
                    <span className={`${styles.amountValue} ${
                      resolveTransactionDirection(transaction, currentUserId) === 'in' ? styles.positive : styles.negative
                    }`}>
                      {resolveTransactionDirection(transaction, currentUserId) === 'in' ? '+' : '-'}
                      {formatCoinAmount(absoluteCoinValue(transaction.voAmount), language, t, displayMode)}
                    </span>
                  </div>
                  <div className={`${styles.transactionStatus} ${styles[getTransactionStatusTone(transaction.voStatus)]}`}>
                    {formatTransactionStatus(transaction.voStatus, t)}
                  </div>
                </div>
              </div>

              <div className={styles.transactionDetails}>
                <div className={styles.transactionTime}>
                  {formatCoinRelativeDateTime(transaction.voCreateTime, displayTimeZone, language)}
                </div>
                <div className={styles.transactionNo}>
                  {t('pit.common.transactionNoValue', { value: transaction.voTransactionNo })}
                </div>
              </div>

              {transaction.voRemark && (
                <div className={styles.transactionNote}>
                  <span className={styles.noteIcon}>💬</span>
                  {transaction.voRemark}
                </div>
              )}
            </div>

            <div className={styles.transactionArrow}>
              →
            </div>
          </div>
        ))}
      </div>

      {/* 分页 */}
      {renderPagination()}
    </div>
  );
};

/**
 * 渲染交易参与方信息
 */
const renderTransactionParties = (
  transaction: CoinTransaction,
  currentUserId: string,
  t: TFunction,
): string => {
  const fromUser = transaction.voFromUserId ? transaction.voFromUserName : null;
  const toUser = transaction.voToUserId ? transaction.voToUserName : null;

  if (!fromUser && !toUser) {
    return t('pit.history.systemTransaction');
  }

  if (!fromUser) {
    return `${t('pit.common.system')} → ${getSafeUserDisplayName(toUser || '', transaction.voToUserId === currentUserId, t('pit.common.me'))}`;
  }

  if (!toUser) {
    return `${getSafeUserDisplayName(fromUser, transaction.voFromUserId === currentUserId, t('pit.common.me'))} → ${t('pit.common.system')}`;
  }

  return `${getSafeUserDisplayName(fromUser, transaction.voFromUserId === currentUserId, t('pit.common.me'))} → ${getSafeUserDisplayName(toUser, transaction.voToUserId === currentUserId, t('pit.common.me'))}`;
};
