import { useState, useEffect, useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { coinApi } from '@/api/coin';
import { log } from '@/utils/logger';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import { useUserStore } from '@/stores/userStore';
import {
  formatCoinDateTime,
  formatCoinNumber,
  formatTransactionStatus,
  formatTransactionType,
  getSignedTransactionAmount,
} from '../../utils';
import { TransactionFilters } from './TransactionFilters';
import type { FilterOptions } from './TransactionFilters';
import { TransactionList } from './TransactionList';
import { TransactionDetail } from './TransactionDetail';
import type { CoinTransaction } from '@/api/coin';
import styles from './TransactionHistory.module.css';

const EXPORT_PAGE_SIZE = 100;

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

const matchesSearchKeyword = (
  transaction: CoinTransaction,
  t: TFunction,
  searchKeyword?: string,
): boolean => {
  const keyword = searchKeyword?.trim().toLowerCase();
  if (!keyword) {
    return true;
  }

  return [
    transaction.voTransactionNo,
    transaction.voFromUserName,
    transaction.voToUserName,
    transaction.voTransactionType,
    formatTransactionType(transaction.voTransactionType, t),
    transaction.voStatus,
    formatTransactionStatus(transaction.voStatus, t),
    transaction.voBusinessType,
    transaction.voBusinessId,
    transaction.voRemark,
  ]
    .filter(Boolean)
    .some((value) => String(value).toLowerCase().includes(keyword));
};

const applyClientExportFilters = (
  exportTransactions: CoinTransaction[],
  exportFilters: FilterOptions,
  t: TFunction,
): CoinTransaction[] => {
  return exportTransactions.filter(
    (transaction) =>
      matchesDateRange(transaction, exportFilters.dateRange) &&
      matchesSearchKeyword(transaction, t, exportFilters.searchKeyword)
  );
};

const buildTransactionsCsv = (
  exportTransactions: CoinTransaction[],
  t: TFunction,
  language: string,
  timeZone: string,
  currentUserId: string,
): string => {
  const csvHeaders = [
    t('pit.history.csv.transactionNo'),
    t('pit.history.csv.createdAt'),
    t('pit.history.csv.type'),
    t('pit.history.csv.status'),
    t('pit.history.csv.amount'),
    t('pit.history.csv.fee'),
    t('pit.history.csv.sender'),
    t('pit.history.csv.recipient'),
    t('pit.history.csv.businessType'),
    t('pit.history.csv.businessId'),
    t('pit.history.csv.remark'),
  ];
  const rows = exportTransactions.map((transaction) =>
    [
      transaction.voTransactionNo,
      formatCoinDateTime(transaction.voCreateTime, timeZone, language),
      formatTransactionType(transaction.voTransactionType, t),
      formatTransactionStatus(transaction.voStatus, t),
      formatCoinNumber(getSignedTransactionAmount(transaction, currentUserId), language),
      formatCoinNumber(transaction.voFee, language),
      transaction.voFromUserId ? transaction.voFromUserName : t('pit.common.system'),
      transaction.voToUserId ? transaction.voToUserName : t('pit.common.system'),
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
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const displayTimeZone = useMemo(() => getBrowserTimeZoneId(DEFAULT_TIME_ZONE), []);
  const currentUserId = String(useUserStore((state) => state.userId));
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
          searchFilters.status || null,
          t,
        );

        setTransactions(response.data);
        setTotalPages(Math.max(1, response.pageCount || 1));
        setTotalCount(response.dataCount);
        setCurrentPage(page);

        log.debug('TransactionHistory', '交易记录加载完成', {
          count: response.data.length,
          totalCount: response.dataCount,
        });
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : t('pit.api.transactionsFailed');
        setError(errorMessage);
        log.error('加载交易记录失败:', err);
      } finally {
        setLoading(false);
      }
    },
    [pageSize, t]
  );

  useEffect(() => {
    void loadTransactions(currentPage, filters);
  }, [currentPage, filters, loadTransactions]);

  const handleFilterChange = (newFilters: FilterOptions) => {
    setExportNotice(null);
    setFilters(newFilters);
    setCurrentPage(1);
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
        filters.status || null,
        t,
      );
      const exportTransactions = [...firstPage.data];

      for (let page = 2; page <= firstPage.pageCount; page += 1) {
        const pageResult = await coinApi.getTransactions(
          page,
          EXPORT_PAGE_SIZE,
          filters.transactionType || null,
          filters.status || null,
          t,
        );
        exportTransactions.push(...pageResult.data);
      }

      const filteredTransactions = applyClientExportFilters(exportTransactions, filters, t);
      if (filteredTransactions.length === 0) {
        setExportNotice(t('pit.history.exportEmpty'));
        return;
      }

      downloadCsv(
        buildTransactionsCsv(filteredTransactions, t, language, displayTimeZone, currentUserId),
        createExportFileName(),
      );
      setExportNotice(t('pit.history.exported', {
        count: filteredTransactions.length,
        value: formatCoinNumber(filteredTransactions.length, language),
      }));
    } catch (err) {
      setExportNotice(t('pit.history.exportFailed'));
      log.error('导出交易记录失败:', err);
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <h2 className={styles.title}>{t('pit.history.title')}</h2>
          <p className={styles.subtitle}>{t('pit.history.description', {
            count: totalCount,
            value: formatCoinNumber(totalCount, language),
          })}</p>
        </div>
        <div className={styles.headerRight}>
          <button
            className={styles.displayModeButton}
            onClick={toggleDisplayMode}
            title={t('pit.currency.switchTo', {
              mode: t(displayMode === 'carrot' ? 'pit.currency.white' : 'pit.currency.carrot'),
            })}
          >
            {displayMode === 'carrot' ? '🥕' : '🤍'}
            {t(displayMode === 'carrot' ? 'pit.currency.carrot' : 'pit.currency.white')}
          </button>
          <button
            className={styles.exportButton}
            onClick={handleExport}
            disabled={exporting || loading}
            title={t('pit.history.exportTitle')}
          >
            📥 {t(exporting ? 'pit.history.exporting' : 'pit.history.export')}
          </button>
          <button className={styles.refreshButton} onClick={handleRefresh} title={t('pit.common.refresh')}>
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
