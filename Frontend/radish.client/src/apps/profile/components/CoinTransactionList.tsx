import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { getTransactions, type CoinTransaction } from '@/api/coin';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import styles from './CoinTransactionList.module.css';

interface CoinTransactionListProps {
  apiBaseUrl: string;
  displayTimeZone?: string;
}

/**
 * 萝卜币交易记录列表组件
 */
export const CoinTransactionList = ({ displayTimeZone = 'Asia/Shanghai' }: CoinTransactionListProps) => {
  const { t } = useTranslation();
  const [transactions, setTransactions] = useState<CoinTransaction[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize] = useState(20);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [filterType, setFilterType] = useState<string | null>(null);
  const [filterStatus, setFilterStatus] = useState<string | null>(null);

  useEffect(() => {
    void loadTransactions();
  }, [pageIndex, filterType, filterStatus]);

  const loadTransactions = async () => {
    setLoading(true);
    setError(null);

    try {
      const result = await getTransactions(pageIndex, pageSize, filterType, filterStatus, t);
      if (result) {
        setTransactions(result.data);
        setTotalCount(result.dataCount);
        setTotalPages(result.pageCount);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : t('profile.transactions.loadFailed'));
    } finally {
      setLoading(false);
    }
  };

  const handlePreviousPage = () => {
    if (pageIndex > 1) {
      setPageIndex(pageIndex - 1);
    }
  };

  const handleNextPage = () => {
    if (pageIndex < totalPages) {
      setPageIndex(pageIndex + 1);
    }
  };

  const handleFilterTypeChange = (type: string | null) => {
    setFilterType(type);
    setPageIndex(1);
  };

  const handleFilterStatusChange = (status: string | null) => {
    setFilterStatus(status);
    setPageIndex(1);
  };

  if (loading && transactions.length === 0) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>{t('common.loading')}</div>
      </div>
    );
  }

  if (error && transactions.length === 0) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>{error}</div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.filters}>
        <div className={styles.filterGroup}>
          <label>{t('profile.transactions.filter.type')}</label>
          <select
            value={filterType || ''}
            onChange={(e) => handleFilterTypeChange(e.target.value || null)}
          >
            <option value="">{t('common.all')}</option>
            <option value="SYSTEM_GRANT">{t('profile.transactions.type.systemGrant')}</option>
            <option value="LIKE_REWARD">{t('profile.transactions.type.likeReward')}</option>
            <option value="COMMENT_REWARD">{t('profile.transactions.type.commentReward')}</option>
            <option value="HIGHLIGHT_REWARD">{t('profile.transactions.type.highlightReward')}</option>
            <option value="TRANSFER">{t('profile.transactions.type.transfer')}</option>
            <option value="ADMIN_ADJUST">{t('profile.transactions.type.adminAdjust')}</option>
          </select>
        </div>

        <div className={styles.filterGroup}>
          <label>{t('profile.transactions.filter.status')}</label>
          <select
            value={filterStatus || ''}
            onChange={(e) => handleFilterStatusChange(e.target.value || null)}
          >
            <option value="">{t('common.all')}</option>
            <option value="SUCCESS">{t('profile.transactions.status.success')}</option>
            <option value="PENDING">{t('profile.transactions.status.pending')}</option>
            <option value="FAILED">{t('profile.transactions.status.failed')}</option>
          </select>
        </div>
      </div>

      {transactions.length === 0 ? (
        <div className={styles.empty}>{t('profile.transactions.empty')}</div>
      ) : (
        <div className={styles.list}>
          {transactions.map((tx) => (
            <div key={tx.voId} className={styles.transaction}>
              <div className={styles.transactionHeader}>
                <span className={`${styles.type} ${styles[tx.voTransactionType]}`}>
                  {tx.voTransactionTypeDisplay}
                </span>
                <span className={`${styles.status} ${styles[tx.voStatus]}`}>
                  {tx.voStatusDisplay}
                </span>
              </div>

              <div className={styles.transactionBody}>
                <div className={styles.info}>
                  <div className={styles.participants}>
                    <span className={styles.from}>
                      {t('profile.transactions.from', { name: tx.voFromUserName || t('profile.transactions.systemUser') })}
                    </span>
                    <span className={styles.arrow}>→</span>
                    <span className={styles.to}>
                      {t('profile.transactions.to', { name: tx.voToUserName || t('profile.transactions.systemUser') })}
                    </span>
                  </div>

                  {tx.voRemark && (
                    <div className={styles.remark}>
                      {t('profile.transactions.remark', { value: tx.voRemark })}
                    </div>
                  )}

                  <div className={styles.meta}>
                    <span className={styles.transactionNo}>
                      {t('profile.transactions.transactionNo', { value: tx.voTransactionNo })}
                    </span>
                    <span className={styles.time}>
                      {formatDateTimeByTimeZone(tx.voCreateTime, displayTimeZone)}
                    </span>
                  </div>
                </div>

                <div className={styles.amounts}>
                  <div className={styles.amount}>
                    {t('profile.transactions.amountWhiteRadish', { amount: tx.voAmountDisplay })}
                  </div>
                  {tx.voFee > 0 && (
                    <div className={styles.fee}>
                      {t('profile.transactions.fee', { amount: tx.voFeeDisplay })}
                    </div>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            onClick={handlePreviousPage}
            disabled={pageIndex === 1}
            className={styles.pageButton}
          >
            {t('common.previousPage')}
          </button>
          <span className={styles.pageInfo}>
            {t('profile.transactions.pageInfo', { current: pageIndex, total: totalPages, count: totalCount })}
          </span>
          <button
            onClick={handleNextPage}
            disabled={pageIndex === totalPages}
            className={styles.pageButton}
          >
            {t('common.nextPage')}
          </button>
        </div>
      )}
    </div>
  );
};
