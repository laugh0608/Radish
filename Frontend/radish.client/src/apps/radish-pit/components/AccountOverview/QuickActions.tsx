import { log } from '@/utils/logger';
import { useTranslation } from 'react-i18next';
import type { TabType } from '../../types';
import styles from './QuickActions.module.css';

interface QuickActionsProps {
  onNavigate: (tab: TabType) => void;
}

/**
 * 快捷操作组件
 */
export const QuickActions = ({ onNavigate }: QuickActionsProps) => {
  const { t } = useTranslation();
  const handleAction = (action: TabType) => {
    log.debug('QuickActions', `执行快捷操作: ${action}`);
    onNavigate(action);
  };

  const actions: Array<{
    id: TabType;
    icon: string;
    title: string;
    description: string;
    color: string;
  }> = [
    {
      id: 'transfer',
      icon: '💸',
      title: t('pit.overview.action.transferTitle'),
      description: t('pit.overview.action.transferDescription'),
      color: 'blue'
    },
    {
      id: 'history',
      icon: '📋',
      title: t('pit.overview.action.historyTitle'),
      description: t('pit.overview.action.historyDescription'),
      color: 'green'
    },
    {
      id: 'security',
      icon: '🔒',
      title: t('pit.overview.action.securityTitle'),
      description: t('pit.overview.action.securityDescription'),
      color: 'orange'
    },
    {
      id: 'statistics',
      icon: '📈',
      title: t('pit.overview.action.statisticsTitle'),
      description: t('pit.overview.action.statisticsDescription'),
      color: 'purple'
    }
  ];

  return (
    <div className={styles.card}>
      <div className={styles.header}>
        <h3 className={styles.title}>
          <span className={styles.icon}>⚡</span>
          {t('pit.overview.quickActions')}
        </h3>
      </div>

      <div className={styles.content}>
        <div className={styles.actionsGrid}>
          {actions.map((action) => (
            <button
              key={action.id}
              className={`${styles.actionButton} ${styles[action.color]}`}
              onClick={() => handleAction(action.id)}
            >
              <div className={styles.actionIcon}>{action.icon}</div>
              <div className={styles.actionContent}>
                <div className={styles.actionTitle}>{action.title}</div>
                <div className={styles.actionDescription}>{action.description}</div>
              </div>
              <div className={styles.actionArrow}>→</div>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
};
