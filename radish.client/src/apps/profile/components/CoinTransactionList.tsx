import { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { getTransactions, type CoinTransaction } from '@/api/coin';
import styles from './CoinTransactionList.module.css';

interface CoinTransactionListProps {
  apiBaseUrl: string;
}

/**
 * 萝卜币交易记录列表组件
 */
export const CoinTransactionList = ({ apiBaseUrl: _apiBaseUrl }: CoinTransactionListProps) => {
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
        setTransactions(result.voItems);
        setTotalCount(result.voDataCount);
        setTotalPages(result.voPageCount);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : '加载交易记录失败');
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
        <div className={styles.loading}>加载中...</div>
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
      {/* 筛选器 */}
      <div className={styles.filters}>
        <div className={styles.filterGroup}>
          <label>交易类型：</label>
          <select
            value={filterType || ''}
            onChange={(e) => handleFilterTypeChange(e.target.value || null)}
          >
            <option value="">全部</option>
            <option value="SYSTEM_GRANT">系统赠送</option>
            <option value="LIKE_REWARD">点赞奖励</option>
            <option value="COMMENT_REWARD">评论奖励</option>
            <option value="HIGHLIGHT_REWARD">神评/沙发奖励</option>
            <option value="TRANSFER">转账</option>
            <option value="ADMIN_ADJUST">管理员调整</option>
          </select>
        </div>

        <div className={styles.filterGroup}>
          <label>交易状态：</label>
          <select
            value={filterStatus || ''}
            onChange={(e) => handleFilterStatusChange(e.target.value || null)}
          >
            <option value="">全部</option>
            <option value="SUCCESS">成功</option>
            <option value="PENDING">待处理</option>
            <option value="FAILED">失败</option>
          </select>
        </div>
      </div>

      {/* 交易列表 */}
      {transactions.length === 0 ? (
        <div className={styles.empty}>暂无交易记录</div>
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
                      从: {tx.voFromUserName || '系统'}
                    </span>
                    <span className={styles.arrow}>→</span>
                    <span className={styles.to}>
                      到: {tx.voToUserName || '系统'}
                    </span>
                  </div>

                  {tx.voRemark && (
                    <div className={styles.remark}>
                      备注: {tx.voRemark}
                    </div>
                  )}

                  <div className={styles.meta}>
                    <span className={styles.transactionNo}>
                      流水号: {tx.voTransactionNo}
                    </span>
                    <span className={styles.time}>
                      {new Date(tx.voCreateTime).toLocaleString('zh-CN')}
                    </span>
                  </div>
                </div>

                <div className={styles.amounts}>
                  <div className={styles.amount}>
                    {tx.voAmountDisplay} 白萝卜
                  </div>
                  {tx.voFee > 0 && (
                    <div className={styles.fee}>
                      手续费: {tx.voFeeDisplay}
                    </div>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* 分页 */}
      {totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            onClick={handlePreviousPage}
            disabled={pageIndex === 1}
            className={styles.pageButton}
          >
            上一页
          </button>
          <span className={styles.pageInfo}>
            第 {pageIndex} / {totalPages} 页 (共 {totalCount} 条)
          </span>
          <button
            onClick={handleNextPage}
            disabled={pageIndex === totalPages}
            className={styles.pageButton}
          >
            下一页
          </button>
        </div>
      )}
    </div>
  );
};
