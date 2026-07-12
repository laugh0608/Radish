import { useState, useEffect, useCallback, useMemo } from 'react';
import { coinApi } from '@/api/coin';
import { log } from '@/utils/logger';
import { debounce } from '../../utils';
import { TransactionFilters } from './TransactionFilters';
import type { FilterOptions } from './TransactionFilters';
import { TransactionList } from './TransactionList';
import { TransactionDetail } from './TransactionDetail';
import type { CoinTransaction } from '@/api/coin';
import styles from './TransactionHistory.module.css';

const EXPORT_PAGE_SIZE = 100;

const csvHeaders = [
  '交易流水号',
  '创建时间',
  '交易类型',
  '状态',
  '金额',
  '手续费',
  '发起方',
  '接收方',
  '业务类型',
  '业务编号',
  '备注',
];

const csvCell = (value: string | number | null | undefined): string => {
  const text = value === null || value === undefined ? '' : String(value);
  return `"${text.replace(/"/g, '""')}"`;
};

const matchesDateRange = (transaction: CoinTransaction, dateRange: FilterOptions['dateRange']): boolean => {
  if (!dateRange?.start && !dateRange?.end) {
    return true;
  }

  const createTime = new Date(transaction.voCreateTime).getTime();
  if (Number.isNaN(createTime)) {
    return false;
  }

  if (dateRange.start) {
    const startTime = new Date(`${dateRange.start}T00:00:00`).getTime();
    if (!Number.isNaN(startTime) && createTime < startTime) {
      return false;
    }
  }

  if (dateRange.end) {
    const endTime = new Date(`${dateRange.end}T23:59:59.999`).getTime();
    if (!Number.isNaN(endTime) && createTime > endTime) {
      return false;
    }
  }

  return true;
};

const matchesSearchKeyword = (transaction: CoinTransaction, searchKeyword?: string): boolean => {
  const keyword = searchKeyword?.trim().toLowerCase();
  if (!keyword) {
    return true;
  }

  return [
    transaction.voTransactionNo,
    transaction.voFromUserName,
    transaction.voToUserName,
    transaction.voTransactionTypeDisplay,
    transaction.voStatusDisplay,
    transaction.voBusinessType,
    transaction.voBusinessId,
    transaction.voRemark,
  ]
    .filter(Boolean)
    .some((value) => String(value).toLowerCase().includes(keyword));
};

const applyClientExportFilters = (
  exportTransactions: CoinTransaction[],
  exportFilters: FilterOptions
): CoinTransaction[] => {
  return exportTransactions.filter(
    (transaction) =>
      matchesDateRange(transaction, exportFilters.dateRange) &&
      matchesSearchKeyword(transaction, exportFilters.searchKeyword)
  );
};

const buildTransactionsCsv = (exportTransactions: CoinTransaction[]): string => {
  const rows = exportTransactions.map((transaction) =>
    [
      transaction.voTransactionNo,
      transaction.voCreateTime,
      transaction.voTransactionTypeDisplay || transaction.voTransactionType,
      transaction.voStatusDisplay || transaction.voStatus,
      transaction.voAmountDisplay || transaction.voAmount,
      transaction.voFeeDisplay || transaction.voFee,
      transaction.voFromUserName,
      transaction.voToUserName,
      transaction.voBusinessType,
      transaction.voBusinessId,
      transaction.voRemark,
    ]
      .map(csvCell)
      .join(',')
  );

  return [`\uFEFF${csvHeaders.map(csvCell).join(',')}`, ...rows].join('\n');
};

const createExportFileName = (): string => {
  const now = new Date();
  const timestamp = [
    now.getFullYear(),
    String(now.getMonth() + 1).padStart(2, '0'),
    String(now.getDate()).padStart(2, '0'),
    '-',
    String(now.getHours()).padStart(2, '0'),
    String(now.getMinutes()).padStart(2, '0'),
    String(now.getSeconds()).padStart(2, '0'),
  ].join('');
  return `radish-transactions-${timestamp}.csv`;
};

const downloadCsv = (csvContent: string, fileName: string) => {
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
};

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
  const [exporting, setExporting] = useState(false);
  const [exportNotice, setExportNotice] = useState<string | null>(null);

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

        setTransactions(response.data);
        setTotalPages(response.pageCount);
        setTotalCount(response.dataCount);
        setCurrentPage(page);

        log.debug('TransactionHistory', '交易记录加载完成', {
          count: response.data.length,
          totalCount: response.dataCount,
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

  const debouncedSearch = useMemo(
    () => debounce((searchFilters: FilterOptions) => {
      setCurrentPage(1);
      void loadTransactions(1, searchFilters);
    }, 500),
    [loadTransactions]
  );

  useEffect(() => {
    void loadTransactions(currentPage, filters);
  }, [currentPage, filters, loadTransactions]);

  const handleFilterChange = (newFilters: FilterOptions) => {
    setExportNotice(null);
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
    setExportNotice(null);
    void loadTransactions(currentPage, filters);
  };

  const toggleDisplayMode = () => {
    setDisplayMode((prev) => (prev === 'carrot' ? 'white' : 'carrot'));
  };

  const handleExport = async () => {
    try {
      setExporting(true);
      setExportNotice(null);

      log.debug('TransactionHistory', '导出交易记录', { filters });

      const firstPage = await coinApi.getTransactions(
        1,
        EXPORT_PAGE_SIZE,
        filters.transactionType || null,
        filters.status || null
      );
      const exportTransactions = [...firstPage.data];

      for (let page = 2; page <= firstPage.pageCount; page += 1) {
        const pageResult = await coinApi.getTransactions(
          page,
          EXPORT_PAGE_SIZE,
          filters.transactionType || null,
          filters.status || null
        );
        exportTransactions.push(...pageResult.data);
      }

      const filteredTransactions = applyClientExportFilters(exportTransactions, filters);
      if (filteredTransactions.length === 0) {
        setExportNotice('当前筛选条件下没有可导出的交易记录');
        return;
      }

      downloadCsv(buildTransactionsCsv(filteredTransactions), createExportFileName());
      setExportNotice(`已导出 ${filteredTransactions.length.toLocaleString()} 条交易记录`);
    } catch (err) {
      setExportNotice('导出失败，请稍后重试');
      log.error('导出交易记录失败:', err);
    } finally {
      setExporting(false);
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
          <button
            className={styles.exportButton}
            onClick={handleExport}
            disabled={exporting || loading}
            title="导出当前筛选条件下的交易记录"
          >
            📥 {exporting ? '导出中' : '导出'}
          </button>
          <button className={styles.refreshButton} onClick={handleRefresh} title="刷新数据">
            🔄
          </button>
        </div>
      </div>

      <div className={styles.filtersSection}>
        <TransactionFilters filters={filters} onFilterChange={handleFilterChange} />
      </div>

      {exportNotice && <div className={styles.exportNotice}>{exportNotice}</div>}

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
