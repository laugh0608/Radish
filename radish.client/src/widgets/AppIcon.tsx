import { Icon } from '@radish/ui';
import type { AppDefinition } from '@/desktop/types';
import styles from './AppIcon.module.css';

export interface AppIconProps {
  /** 应用定义 */
  app: AppDefinition;
  /** 双击事件 */
  onDoubleClick?: () => void;
}

/**
 * 桌面应用图标组件
 */
export const AppIcon = ({ app, onDoubleClick }: AppIconProps) => {
  return (
    <div
      className={styles.icon}
      onDoubleClick={onDoubleClick}
      title={app.description || app.name}
    >
      <div className={styles.iconImage}>
        {app.icon.startsWith('mdi:') || app.icon.startsWith('ic:') ? (
          <Icon icon={app.icon} size={48} />
        ) : (
          <span className={styles.emoji}>{app.icon}</span>
        )}
      </div>
      <span className={styles.iconName}>{app.name}</span>
    </div>
  );
};
