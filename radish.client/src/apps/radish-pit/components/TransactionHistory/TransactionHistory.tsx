import { useState, useEffect, useCallback } from 'react';
import { coinApi } from '@/api/coin';
import { log } from '@/utils/logger';
import { formatCoinAmount, formatDateTime, getTransactionTypeDisplay, getTransactionStatusColor, debounce } from '../../utils';
import { TransactionFilters } from './TransactionFilters';
import { TransactionList } from './TransactionList';
import { TransactionDetail } from './TransactionDetail';
import type { CoinTransactionVo } from '@/api/coin';
import styles from './TransactionHistory.module.css';

interface FilterOptions {
  transactionType?: string;
  status?: string;
  dateRange?: {
    start: string;
    end: string;
  };
  searchKeyword?: string;
}

/**
 * 交易记录组件
 */
export const TransactionHistory = () => {
  const [transactions, setTransactions] = useState<CoinTransactionVo[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [pageSize] = useState(20);
  const [filters, setFilters] = useState<FilterOptions>({});
  const [selectedTransaction, setSelectedTransaction] = useState<CoinTransactionVo | null>(null);
  const [displayMode, setDisplayMode] = useState<'carrot' | 'white'>('carrot');

  // 防抖搜索
  const debouncedSearch = useCallback(
    debounce((searchFilters: FilterOptions) => {
      setCurrentPage(1);
      loadTransactions(1, searchFilters);
    }, 500),
    []
  );

  useEffect(() => {
    loadTransactions(currentPage, filters);
  }, [currentPage]);

  const loadTransactions = async (page: number = 1, searchFilters: FilterOptions = {}) => {
    try {
      setLoading(true);
      setError(null);

      log.debug('TransactionHistory', '加载交易记录', { page, pageSize, filters: searchFilters });

      const response = await coinApi.getTransactions(
        page,
        pageSize,
        searchFilters.transactionType,
        searchFilters.status
      );

      setTransactions(response.voItems || []);
      setTotalPages(response.voTotalPages || 1);
      setTotalCount(response.voTotalCount || 0);
      setCurrentPage(page);

      log.debug('TransactionHistory', '交易记录加载完成', {
        count: response.voItems?.length || 0,
        totalCount: response.voTotalCount
      });
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : '加载交易记录失败';
      setError(errorMessage);
      log.error('加载交易记录失败:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleFilterChange = (newFilters: FilterOptions) => {
    setFilters(newFilters);
    debouncedSearch(newFilters);
  };

  const handlePageChange = (page: number) => {
    if (page >= 1 && page <= totalPages && page !== currentPage) {
      setCurrentPage(page);
    }
  };

  const handleTransactionClick = (transaction: CoinTransactionVo) => {
    setSelectedTransaction(transaction);
  };

  const handleCloseDetail = () => {
    setSelectedTransaction(null);
  };

  const handleRefresh = () => {
    loadTransactions(currentPage, filters);
  };

  const toggleDisplayMode = () => {
    setDisplayMode(prev => prev === 'carrot' ? 'white' : 'carrot');
  };

  const handleExport = async () => {
    try {
      log.debug('TransactionHistory', '导出交易记录');
      // TODO: 实现导出功能
      alert('导出功能开发中...');
    } catch (err) {
      log.error('导出交易记录失败:', err);
      alert('导出失败，请稍后重试');
    }
  };

  return (
    <div className={styles.container}>
      {/* 页面标题和操作 */}
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <h2 className={styles.title}>交易记录</h2>
          <p className={styles.subtitle}>
            查看您的萝卜币交易历史，共 {totalCount.toLocaleString()} 条记录
          </p>
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
          <button
            className={styles.exportButton}
            onClick={handleExport}
            title="导出记录"
          >
            📥 导出
          </button>
          <button
            className={styles.refreshButton}
            onClick={handleRefresh}
            title="刷新数据"
          >
            🔄
          </button>
        </div>
      </div>

      {/* 筛选器 */}
      <div className={styles.filtersSection}>
        <TransactionFilters
          filters={filters}
          onFilterChange={handleFilterChange}
        />
      </div>

      {/* 交易列表 */}
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

      {/* 交易详情模态框 */}
      {selectedTransaction && (
        <TransactionDetail
          transaction={selectedTransaction}
          displayMode={displayMode}
          onClose={handleCloseDetail}
        />
      )}
    </div>
  );
};