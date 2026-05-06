import { useState, useEffect } from 'react';
import { log } from '@/utils/logger';
import { paymentPasswordApi, type PaymentPasswordSecurityLog } from '@/api/paymentPassword';
import { formatDateTime } from '../../utils';
import styles from './SecurityLog.module.css';

/**
 * 安全日志组件
 */
export const SecurityLog = () => {
  const [logs, setLogs] = useState<PaymentPasswordSecurityLog[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<'all' | 'success' | 'failed'>('all');

  useEffect(() => {
    loadSecurityLogs();
  }, []);

  const loadSecurityLogs = async () => {
    try {
      setLoading(true);
      setError(null);

      const response = await paymentPasswordApi.getSecurityLogs(1, 20);
      setLogs(response.data);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : '加载安全日志失败';
      setError(errorMessage);
      log.error('加载安全日志失败:', err);
    } finally {
      setLoading(false);
    }
  };

  const getLogIcon = (type: string, result: string): string => {
    if (result === 'failed') return '❌';

    switch (type) {
      case 'password_verify': return '🔍';
      case 'password_change': return '🔑';
      case 'account_lock': return '🔒';
      case 'account_unlock': return '🔓';
      default: return '📝';
    }
  };

  const filteredLogs = logs.filter(logItem => {
    if (filter === 'all') return true;
    return logItem.voResult === filter;
  });

  if (loading) {
    return (
      <div className={styles.container}>
        <div className={styles.loading}>
          <div className={styles.loadingSpinner}></div>
          <p>加载安全日志中...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className={styles.container}>
        <div className={styles.error}>
          <div className={styles.errorIcon}>⚠️</div>
          <h3>加载失败</h3>
          <p>{error}</p>
          <button className={styles.retryButton} onClick={loadSecurityLogs}>
            重试
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className={styles.container}>
      {/* 筛选器 */}
      <div className={styles.filters}>
        <h3 className={styles.filtersTitle}>
          <span className={styles.filtersIcon}>📋</span>
          安全日志
        </h3>
        <div className={styles.filterButtons}>
          <button
            className={`${styles.filterButton} ${filter === 'all' ? styles.active : ''}`}
            onClick={() => setFilter('all')}
          >
            全部 ({logs.length})
          </button>
          <button
            className={`${styles.filterButton} ${filter === 'success' ? styles.active : ''}`}
            onClick={() => setFilter('success')}
          >
            成功 ({logs.filter(l => l.voResult === 'success').length})
          </button>
          <button
            className={`${styles.filterButton} ${filter === 'failed' ? styles.active : ''}`}
            onClick={() => setFilter('failed')}
          >
            失败 ({logs.filter(l => l.voResult === 'failed').length})
          </button>
        </div>
      </div>

      {/* 日志列表 */}
      <div className={styles.logList}>
        {filteredLogs.length === 0 ? (
          <div className={styles.empty}>
            <div className={styles.emptyIcon}>📝</div>
            <p>暂无安全日志</p>
            <p className={styles.emptyHint}>安全相关操作记录将显示在这里</p>
          </div>
        ) : (
          filteredLogs.map((logItem) => (
            <div key={logItem.voId} className={styles.logItem}>
              <div className={styles.logIcon}>
                {getLogIcon(logItem.voType, logItem.voResult)}
              </div>

              <div className={styles.logContent}>
                <div className={styles.logMain}>
                  <div className={styles.logAction}>{logItem.voAction}</div>
                  <div className={`${styles.logResult} ${styles[logItem.voResult]}`}>
                    {logItem.voResult === 'success' ? '成功' : '失败'}
                  </div>
                </div>

                <div className={styles.logDetails}>
                  <div className={styles.logTime}>
                    {formatDateTime(logItem.voCreatedAt)}
                  </div>
                  {logItem.voIpAddress && (
                    <div className={styles.logIp}>
                      IP: {logItem.voIpAddress}
                    </div>
                  )}
                </div>
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
};
