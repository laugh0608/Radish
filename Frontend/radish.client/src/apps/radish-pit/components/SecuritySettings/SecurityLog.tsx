import { useState, useEffect } from 'react';
import { log } from '@/utils/logger';
import { formatDateTime } from '../../utils';
import styles from './SecurityLog.module.css';

interface SecurityLogItem {
  id: string;
  type: 'password_verify' | 'password_change' | 'account_lock' | 'account_unlock';
  action: string;
  result: 'success' | 'failed';
  ipAddress?: string;
  userAgent?: string;
  createdAt: string;
}

/**
 * 安全日志组件
 */
export const SecurityLog = () => {
  const [logs, setLogs] = useState<SecurityLogItem[]>([]);
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

      // TODO: 实现安全日志API调用
      // 模拟数据
      await new Promise(resolve => setTimeout(resolve, 800));

      const mockLogs: SecurityLogItem[] = [
        {
          id: '1',
          type: 'password_verify',
          action: '支付密码验证',
          result: 'success',
          ipAddress: '192.168.1.100',
          userAgent: 'Mozilla/5.0...',
          createdAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString()
        },
        {
          id: '2',
          type: 'password_verify',
          action: '支付密码验证',
          result: 'failed',
          ipAddress: '192.168.1.100',
          userAgent: 'Mozilla/5.0...',
          createdAt: new Date(Date.now() - 5 * 60 * 60 * 1000).toISOString()
        },
        {
          id: '3',
          type: 'password_change',
          action: '修改支付密码',
          result: 'success',
          ipAddress: '192.168.1.100',
          userAgent: 'Mozilla/5.0...',
          createdAt: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString()
        }
      ];

      setLogs(mockLogs);
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
    return logItem.result === filter;
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
            成功 ({logs.filter(l => l.result === 'success').length})
          </button>
          <button
            className={`${styles.filterButton} ${filter === 'failed' ? styles.active : ''}`}
            onClick={() => setFilter('failed')}
          >
            失败 ({logs.filter(l => l.result === 'failed').length})
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
            <div key={logItem.id} className={styles.logItem}>
              <div className={styles.logIcon}>
                {getLogIcon(logItem.type, logItem.result)}
              </div>

              <div className={styles.logContent}>
                <div className={styles.logMain}>
                  <div className={styles.logAction}>{logItem.action}</div>
                  <div className={`${styles.logResult} ${styles[logItem.result]}`}>
                    {logItem.result === 'success' ? '成功' : '失败'}
                  </div>
                </div>

                <div className={styles.logDetails}>
                  <div className={styles.logTime}>
                    {formatDateTime(logItem.createdAt)}
                  </div>
                  {logItem.ipAddress && (
                    <div className={styles.logIp}>
                      IP: {logItem.ipAddress}
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