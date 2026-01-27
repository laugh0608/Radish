import { useState } from 'react';
import { log } from '@/utils/logger';
import styles from './TransactionFilters.module.css';

interface FilterOptions {
  transactionType?: string;
  status?: string;
  dateRange?: {
    start: string;
    end: string;
  };
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
  const [isExpanded, setIsExpanded] = useState(false);
  const [localFilters, setLocalFilters] = useState<FilterOptions>(filters);

  const transactionTypes = [
    { value: '', label: '全部类型' },
    { value: 'SYSTEM_GRANT', label: '系统赠送' },
    { value: 'LIKE_REWARD', label: '点赞奖励' },
    { value: 'COMMENT_REWARD', label: '评论奖励' },
    { value: 'GODLIKE_REWARD', label: '神评奖励' },
    { value: 'SOFA_REWARD', label: '沙发奖励' },
    { value: 'TRANSFER_IN', label: '转入' },
    { value: 'TRANSFER_OUT', label: '转出' },
    { value: 'PURCHASE', label: '购买消费' },
    { value: 'ADMIN_ADJUST', label: '管理员调整' }
  ];

  const statusOptions = [
    { value: '', label: '全部状态' },
    { value: 'PENDING', label: '处理中' },
    { value: 'SUCCESS', label: '成功' },
    { value: 'FAILED', label: '失败' },
    { value: 'CANCELLED', label: '已取消' }
  ];

  const handleFilterChange = (key: keyof FilterOptions, value: string | { start: string; end: string } | undefined) => {
    const newFilters = { ...localFilters, [key]: value };
    setLocalFilters(newFilters);
    onFilterChange(newFilters);
    log.debug('TransactionFilters', '筛选条件变更', { key, value });
  };

  const handleDateRangeChange = (type: 'start' | 'end', value: string) => {
    const newDateRange = {
      ...localFilters.dateRange,
      [type]: value
    };
    handleFilterChange('dateRange', newDateRange);
  };

  const handleClearFilters = () => {
    const clearedFilters: FilterOptions = {};
    setLocalFilters(clearedFilters);
    onFilterChange(clearedFilters);
    log.debug('TransactionFilters', '清空筛选条件');
  };

  const hasActiveFilters = Object.keys(localFilters).some(key => {
    const value = localFilters[key as keyof FilterOptions];
    if (key === 'dateRange') {
      return value && (value.start || value.end);
    }
    return value && value !== '';
  });

  const toggleExpanded = () => {
    setIsExpanded(!isExpanded);
  };

  return (
    <div className={styles.container}>
      <div className={styles.header}>
        <div className={styles.headerLeft}>
          <h3 className={styles.title}>
            <span className={styles.icon}>🔍</span>
            筛选条件
          </h3>
          {hasActiveFilters && (
            <span className={styles.activeIndicator}>
              {Object.keys(localFilters).length} 个筛选条件
            </span>
          )}
        </div>
        <div className={styles.headerRight}>
          {hasActiveFilters && (
            <button
              className={styles.clearButton}
              onClick={handleClearFilters}
              title="清空筛选条件"
            >
              清空
            </button>
          )}
          <button
            className={`${styles.expandButton} ${isExpanded ? styles.expanded : ''}`}
            onClick={toggleExpanded}
            title={isExpanded ? '收起筛选器' : '展开筛选器'}
          >
            {isExpanded ? '收起' : '展开'}
          </button>
        </div>
      </div>

      <div className={`${styles.content} ${isExpanded ? styles.expanded : ''}`}>
        <div className={styles.filtersGrid}>
          {/* 搜索关键词 */}
          <div className={styles.filterGroup}>
            <label className={styles.filterLabel}>搜索</label>
            <input
              type="text"
              className={styles.searchInput}
              placeholder="搜索交易流水号、备注..."
              value={localFilters.searchKeyword || ''}
              onChange={(e) => handleFilterChange('searchKeyword', e.target.value)}
            />
          </div>

          {/* 交易类型 */}
          <div className={styles.filterGroup}>
            <label className={styles.filterLabel}>交易类型</label>
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

          {/* 交易状态 */}
          <div className={styles.filterGroup}>
            <label className={styles.filterLabel}>交易状态</label>
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

          {/* 日期范围 */}
          <div className={styles.filterGroup}>
            <label className={styles.filterLabel}>开始日期</label>
            <input
              type="date"
              className={styles.dateInput}
              value={localFilters.dateRange?.start || ''}
              onChange={(e) => handleDateRangeChange('start', e.target.value)}
            />
          </div>

          <div className={styles.filterGroup}>
            <label className={styles.filterLabel}>结束日期</label>
            <input
              type="date"
              className={styles.dateInput}
              value={localFilters.dateRange?.end || ''}
              onChange={(e) => handleDateRangeChange('end', e.target.value)}
            />
          </div>
        </div>

        {/* 快捷筛选 */}
        <div className={styles.quickFilters}>
          <span className={styles.quickFiltersLabel}>快捷筛选：</span>
          <div className={styles.quickFilterButtons}>
            <button
              className={styles.quickFilterButton}
              onClick={() => handleFilterChange('transactionType', 'LIKE_REWARD')}
            >
              点赞奖励
            </button>
            <button
              className={styles.quickFilterButton}
              onClick={() => handleFilterChange('transactionType', 'COMMENT_REWARD')}
            >
              评论奖励
            </button>
            <button
              className={styles.quickFilterButton}
              onClick={() => handleFilterChange('transactionType', 'TRANSFER_IN')}
            >
              转入记录
            </button>
            <button
              className={styles.quickFilterButton}
              onClick={() => handleFilterChange('transactionType', 'TRANSFER_OUT')}
            >
              转出记录
            </button>
            <button
              className={styles.quickFilterButton}
              onClick={() => handleFilterChange('status', 'SUCCESS')}
            >
              成功交易
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};