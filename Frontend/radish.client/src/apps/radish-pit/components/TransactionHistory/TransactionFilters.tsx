import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { log } from '@/utils/logger';
import { formatCoinNumber } from '../../utils';
import styles from './TransactionFilters.module.css';

export interface FilterOptions {
  transactionType?: string;
  status?: string;
  dateRange?: { start: string; end: string };
  searchKeyword?: string;
}

interface TransactionFiltersProps {
  filters: FilterOptions;
  onFilterChange: (filters: FilterOptions) => void;
}

/**
 * 交易筛选器组件
 */
export const TransactionFilters = ({ filters, onFilterChange }: TransactionFiltersProps) => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const [isExpanded, setIsExpanded] = useState(false);
  const [localFilters, setLocalFilters] = useState<FilterOptions>(filters);

  useEffect(() => {
    setLocalFilters(filters);
  }, [filters]);

  const transactionTypes = [
    { value: '', label: t('pit.history.filter.allTypes') },
    { value: 'SYSTEM_GRANT', label: t('pit.transactionType.SYSTEM_GRANT') },
    { value: 'LIKE_REWARD', label: t('pit.transactionType.LIKE_REWARD') },
    { value: 'COMMENT_REWARD', label: t('pit.transactionType.COMMENT_REWARD') },
    { value: 'TRANSFER', label: t('pit.transactionType.TRANSFER') },
    { value: 'TIP', label: t('pit.transactionType.TIP') },
    { value: 'CONSUME', label: t('pit.transactionType.CONSUME') },
    { value: 'REFUND', label: t('pit.transactionType.REFUND') },
    { value: 'PENALTY', label: t('pit.transactionType.PENALTY') },
    { value: 'ADMIN_ADJUST', label: t('pit.transactionType.ADMIN_ADJUST') },
  ];

  const statusOptions = [
    { value: '', label: t('pit.history.filter.allStatuses') },
    { value: 'PENDING', label: t('pit.transactionStatus.PENDING') },
    { value: 'SUCCESS', label: t('pit.transactionStatus.SUCCESS') },
    { value: 'FAILED', label: t('pit.transactionStatus.FAILED') },
  ];

  const handleFilterChange = (
    key: keyof FilterOptions,
    value: string | { start: string; end: string } | undefined
  ) => {
    const newFilters = { ...localFilters, [key]: value };
    setLocalFilters(newFilters);
    onFilterChange(newFilters);
    log.debug('TransactionFilters', '筛选条件变更', { key, value });
  };

  const handleDateRangeChange = (type: 'start' | 'end', value: string) => {
    const currentDateRange = localFilters.dateRange || { start: '', end: '' };
    const newDateRange = {
      ...currentDateRange,
      [type]: value,
    };
    handleFilterChange('dateRange', newDateRange);
  };

  const handleClearFilters = () => {
    const clearedFilters: FilterOptions = {};
    setLocalFilters(clearedFilters);
    onFilterChange(clearedFilters);
    log.debug('TransactionFilters', '清空筛选条件');
  };

  const hasActiveFilters = Boolean(
    localFilters.transactionType ||
      localFilters.status ||
      localFilters.searchKeyword ||
      localFilters.dateRange?.start ||
      localFilters.dateRange?.end
  );

  const activeFilterCount = [
    localFilters.transactionType,
    localFilters.status,
    localFilters.searchKeyword,
    localFilters.dateRange?.start || localFilters.dateRange?.end ? 'dateRange' : '',
  ].filter(Boolean).length;

  const toggleExpanded = () => {
    setIsExpanded(!isExpanded);
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <h3 className={styles.title}>
            <span className={styles.icon}>🔍</span>
            {t('pit.history.filter.title')}
          </h3>
          {hasActiveFilters && (
            <span className={styles.activeIndicator}>
              {t('pit.history.filter.activeCount', {
                count: activeFilterCount,
                value: formatCoinNumber(activeFilterCount, language),
              })}
            </span>
          )}
        </div>
        <div className={styles.headerRight}>
          {hasActiveFilters && (
            <button className={styles.clearButton} onClick={handleClearFilters} title={t('pit.history.filter.clear')}>
              {t('pit.history.filter.clear')}
            </button>
          )}
          <button
            className={`${styles.expandButton} ${isExpanded ? styles.expanded : ''}`}
            onClick={toggleExpanded}
            title={t(isExpanded ? 'pit.history.filter.collapse' : 'pit.history.filter.expand')}
          >
            {t(isExpanded ? 'pit.history.filter.collapse' : 'pit.history.filter.expand')}
          </button>
        </div>
      </div>

      <div className={`${styles.content} ${isExpanded ? styles.expanded : ''}`}>
        <div className={styles.filtersGrid}>
          <div className={styles.filterGroup}>
            <label className={styles.filterLabel}>{t('pit.history.filter.search')}</label>
            <input
              type="text"
              className={styles.searchInput}
              placeholder={t('pit.history.filter.searchPlaceholder')}
              value={localFilters.searchKeyword || ''}
              onChange={(e) => handleFilterChange('searchKeyword', e.target.value)}
            />
          </div>

          <div className={styles.filterGroup}>
            <label className={styles.filterLabel}>{t('pit.history.filter.type')}</label>
            <select
              className={styles.filterSelect}
              value={localFilters.transactionType || ''}
              onChange={(e) => handleFilterChange('transactionType', e.target.value)}
            >
              {transactionTypes.map((type) => (
                <option key={type.value} value={type.value}>
                  {type.label}
                </option>
              ))}
            </select>
          </div>

          <div className={styles.filterGroup}>
            <label className={styles.filterLabel}>{t('pit.history.filter.status')}</label>
            <select
              className={styles.filterSelect}
              value={localFilters.status || ''}
              onChange={(e) => handleFilterChange('status', e.target.value)}
            >
              {statusOptions.map((status) => (
                <option key={status.value} value={status.value}>
                  {status.label}
                </option>
              ))}
            </select>
          </div>

          <div className={styles.filterGroup}>
            <label className={styles.filterLabel}>{t('pit.history.filter.startDate')}</label>
            <input
              type="date"
              className={styles.dateInput}
              value={localFilters.dateRange?.start || ''}
              onChange={(e) => handleDateRangeChange('start', e.target.value)}
            />
          </div>

          <div className={styles.filterGroup}>
            <label className={styles.filterLabel}>{t('pit.history.filter.endDate')}</label>
            <input
              type="date"
              className={styles.dateInput}
              value={localFilters.dateRange?.end || ''}
              onChange={(e) => handleDateRangeChange('end', e.target.value)}
            />
          </div>
        </div>

        <div className={styles.quickFilters}>
          <span className={styles.quickFiltersLabel}>{t('pit.history.filter.quick')}</span>
          <div className={styles.quickFilterButtons}>
            <button className={styles.quickFilterButton} onClick={() => handleFilterChange('transactionType', 'LIKE_REWARD')}>
              {t('pit.transactionType.LIKE_REWARD')}
            </button>
            <button className={styles.quickFilterButton} onClick={() => handleFilterChange('transactionType', 'COMMENT_REWARD')}>
              {t('pit.transactionType.COMMENT_REWARD')}
            </button>
            <button className={styles.quickFilterButton} onClick={() => handleFilterChange('transactionType', 'TRANSFER')}>
              {t('pit.transactionType.TRANSFER')}
            </button>
            <button className={styles.quickFilterButton} onClick={() => handleFilterChange('status', 'SUCCESS')}>
              {t('pit.history.filter.successful')}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
