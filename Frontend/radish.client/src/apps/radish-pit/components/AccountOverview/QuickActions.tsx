import { log } from '@/utils/logger';
import type { TabType } from '../../types';
import styles from './QuickActions.module.css';

interface QuickActionsProps {
  onNavigate: (tab: TabType) => void;
}

/**
 * 快捷操作组件
 */
export const QuickActions = ({ onNavigate }: QuickActionsProps) => {
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
      title: '快速转移',
      description: '向其他用户转移萝卜',
      color: 'blue'
    },
    {
      id: 'history',
      icon: '📋',
      title: '查看记录',
      description: '查看详细交易记录',
      color: 'green'
    },
    {
      id: 'security',
      icon: '🔒',
      title: '安全设置',
      description: '设置支付密码和安全选项',
      color: 'orange'
    },
    {
      id: 'statistics',
      icon: '📈',
      title: '收支统计',
      description: '查看详细的收支分析',
      color: 'purple'
    }
  ];

  return (
    <div className={styles.card}>
      <div className={styles.header}>
        <h3 className={styles.title}>
          <span className={styles.icon}>⚡</span>
          快捷操作
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
