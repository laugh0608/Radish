import { useState, useEffect, useCallback } from 'react';
import { coinApi } from '@/api/coin';
import { log } from '@/utils/logger';
import { debounce } from '../../utils';
import { TransactionFilters } from './TransactionFilters';
import type { FilterOptions } from './TransactionFilters';
import { TransactionList } from './TransactionList';
import { TransactionDetail } from './TransactionDetail';
import type { CoinTransaction } from '@/api/coin';
import styles from './TransactionHistory.module.css';

/**
 * 交易记录组件
 */
export const TransactionHistory = () => {
  const [transactions, setTransactions] = useState<CoinTransaction[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [pageSize] = useState(20);
  const [filters, setFilters] = useState<FilterOptions>({});
  const [selectedTransaction, setSelectedTransaction] = useState<CoinTransaction | null>(null);
  const [displayMode, setDisplayMode] = useState<'carrot' | 'white'>('carrot');

  const loadTransactions = useCallback(
    async (page: number = 1, searchFilters: FilterOptions = {}) => {
      try {
        setLoading(true);
        setError(null);

        log.debug('TransactionHistory', '加载交易记录', { page, pageSize, filters: searchFilters });

        const response = await coinApi.getTransactions(
          page,
          pageSize,
          searchFilters.transactionType || null,
          searchFilters.status || null
        );

        setTransactions(response.voItems);
        setTotalPages(response.voTotalPages);
        setTotalCount(response.voTotalCount);
        setCurrentPage(page);

        log.debug('TransactionHistory', '交易记录加载完成', {
          count: response.voItems.length,
          totalCount: response.voTotalCount,
        });
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : '加载交易记录失败';
        setError(errorMessage);
        log.error('加载交易记录失败:', err);
      } finally {
        setLoading(false);
      }
    },
    [pageSize]
  );

  const debouncedSearch = useCallback(
    debounce((searchFilters: FilterOptions) => {
      setCurrentPage(1);
      void loadTransactions(1, searchFilters);
    }, 500),
    [loadTransactions]
  );

  useEffect(() => {
    void loadTransactions(currentPage, filters);
  }, [currentPage, filters, loadTransactions]);

  const handleFilterChange = (newFilters: FilterOptions) => {
    setFilters(newFilters);
    debouncedSearch(newFilters);
  };

  const handlePageChange = (page: number) => {
    if (page >= 1 && page <= totalPages && page !== currentPage) {
      setCurrentPage(page);
    }
  };

  const handleTransactionClick = (transaction: CoinTransaction) => {
    setSelectedTransaction(transaction);
  };

  const handleCloseDetail = () => {
    setSelectedTransaction(null);
  };

  const handleRefresh = () => {
    void loadTransactions(currentPage, filters);
  };

  const toggleDisplayMode = () => {
    setDisplayMode((prev) => (prev === 'carrot' ? 'white' : 'carrot'));
  };

  const handleExport = async () => {
    try {
      log.debug('TransactionHistory', '导出交易记录');
      alert('导出功能开发中...');
    } catch (err) {
      log.error('导出交易记录失败:', err);
      alert('导出失败，请稍后重试');
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <h2 className={styles.title}>交易记录</h2>
          <p className={styles.subtitle}>查看您的萝卜交易历史，共 {totalCount.toLocaleString()} 条记录</p>
        </div>
        <div className={styles.headerRight}>
          <button
            className={styles.displayModeButton}
            onClick={toggleDisplayMode}
            title={`切换到${displayMode === 'carrot' ? '白萝卜' : '胡萝卜'}显示`}
          >
            {displayMode === 'carrot' ? '🥕' : '🤍'}
            {displayMode === 'carrot' ? '胡萝卜' : '白萝卜'}
          </button>
          <button className={styles.exportButton} onClick={handleExport} title="导出记录">
            📥 导出
          </button>
          <button className={styles.refreshButton} onClick={handleRefresh} title="刷新数据">
            🔄
          </button>
        </div>
      </div>

      <div className={styles.filtersSection}>
        <TransactionFilters filters={filters} onFilterChange={handleFilterChange} />
      </div>

      <div className={styles.contentSection}>
        <TransactionList
          transactions={transactions}
          loading={loading}
          error={error}
          displayMode={displayMode}
          currentPage={currentPage}
          totalPages={totalPages}
          totalCount={totalCount}
          onTransactionClick={handleTransactionClick}
          onPageChange={handlePageChange}
          onRefresh={handleRefresh}
        />
      </div>

      {selectedTransaction && (
        <TransactionDetail transaction={selectedTransaction} displayMode={displayMode} onClose={handleCloseDetail} />
      )}
    </div>
  );
};
