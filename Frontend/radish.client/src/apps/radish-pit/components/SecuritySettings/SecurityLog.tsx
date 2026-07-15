import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { log } from '@/utils/logger';
import { paymentPasswordApi, type PaymentPasswordSecurityLog } from '@/api/paymentPassword';
import { DEFAULT_TIME_ZONE, getBrowserTimeZoneId } from '@/utils/dateTime';
import { formatCoinDateTime, formatCoinNumber, formatSecurityLogType } from '../../utils';
import styles from './SecurityLog.module.css';

type SecurityLogFilter = 'all' | 'success' | 'failed';

const PAGE_SIZE = 20;

/**
 * 安全日志组件。展示名称只由稳定 voType / voResult 词元解析。
 */
export const SecurityLog = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  const displayTimeZone = getBrowserTimeZoneId(DEFAULT_TIME_ZONE);
  const [logs, setLogs] = useState<PaymentPasswordSecurityLog[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<SecurityLogFilter>('all');
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const loadSecurityLogs = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      const response = await paymentPasswordApi.getSecurityLogs(currentPage, PAGE_SIZE, t);
      setLogs(response.data);
      setTotalPages(Math.max(1, response.pageCount || 1));
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : t('pit.api.securityLogsFailed');
      setError(errorMessage);
      log.error('SecurityLog', '加载安全日志失败', err);
    } finally {
      setLoading(false);
    }
  }, [currentPage, t]);

  useEffect(() => {
    void loadSecurityLogs();
  }, [loadSecurityLogs]);

  const getLogIcon = (type: string, result: string): string => {
    if (result === 'failed') return '❌';

    switch (type) {
      case 'password_verify': return '🔍';
      case 'password_change':
      case 'password_set': return '🔑';
      case 'account_lock': return '🔒';
      case 'account_unlock': return '🔓';
      default: return '📝';
    }
  };

  const filteredLogs = logs.filter((logItem) => filter === 'all' || logItem.voResult === filter);
  const successfulCount = logs.filter((logItem) => logItem.voResult === 'success').length;
  const failedCount = logs.filter((logItem) => logItem.voResult === 'failed').length;

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>{t('pit.security.log.loading')}</p>
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
          <button className={styles.retryButton} onClick={() => void loadSecurityLogs()}>
            {t('pit.common.retry')}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      <div className={styles.filters}>
        <h3 className={styles.filtersTitle}>
          <span className={styles.filtersIcon}>📋</span>
          {t('pit.security.log.title')}
        </h3>
        <div className={styles.filterButtons}>
          {([
            ['all', logs.length],
            ['success', successfulCount],
            ['failed', failedCount],
          ] as Array<[SecurityLogFilter, number]>).map(([value, count]) => (
            <button
              key={value}
              className={`${styles.filterButton} ${filter === value ? styles.active : ''}`}
              onClick={() => setFilter(value)}
            >
              {t(`pit.security.log.filter.${value}`, {
                count,
                value: formatCoinNumber(count, language),
              })}
            </button>
          ))}
        </div>
      </div>

      <div className={styles.logList}>
        {filteredLogs.length === 0 ? (
          <div className={styles.empty}>
            <div className={styles.emptyIcon}>📝</div>
            <p>{t('pit.security.log.empty')}</p>
            <p className={styles.emptyHint}>{t('pit.security.log.emptyDescription')}</p>
          </div>
        ) : (
          filteredLogs.map((logItem) => (
            <div key={logItem.voId} className={styles.logItem}>
              <div className={styles.logIcon}>
                {getLogIcon(logItem.voType, logItem.voResult)}
              </div>

              <div className={styles.logContent}>
                <div className={styles.logMain}>
                  <div className={styles.logAction}>{formatSecurityLogType(logItem.voType, t)}</div>
                  <div className={`${styles.logResult} ${styles[logItem.voResult] ?? ''}`}>
                    {t(`pit.security.log.result.${logItem.voResult}`, { defaultValue: logItem.voResult })}
                  </div>
                </div>

                <div className={styles.logDetails}>
                  <div className={styles.logTime}>
                    {formatCoinDateTime(logItem.voCreatedAt, displayTimeZone, language)}
                  </div>
                  {logItem.voIpAddress && (
                    <div className={styles.logIp}>
                      {t('pit.security.log.ip', { value: logItem.voIpAddress })}
                    </div>
                  )}
                </div>
              </div>
            </div>
          ))
        )}
      </div>

      {totalPages > 1 && (
        <div className={styles.pagination}>
          <button
            className={styles.pageButton}
            disabled={currentPage <= 1}
            onClick={() => setCurrentPage((page) => Math.max(1, page - 1))}
          >
            {t('pit.common.prevPage')}
          </button>
          <span>{t('pit.common.pageInfo', {
            current: formatCoinNumber(currentPage, language),
            total: formatCoinNumber(totalPages, language),
          })}</span>
          <button
            className={styles.pageButton}
            disabled={currentPage >= totalPages}
            onClick={() => setCurrentPage((page) => Math.min(totalPages, page + 1))}
          >
            {t('pit.common.nextPage')}
          </button>
        </div>
      )}
    </div>
  );
};
