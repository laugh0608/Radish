import { Icon } from '@radish/ui/icon';
import { useTranslation } from 'react-i18next';
import type { AppDefinition } from '@/desktop/types';
import styles from './AppIcon.module.css';

export interface AppIconProps {
  /** 应用定义 */
  app: AppDefinition;
  /** 双击事件 */
  onDoubleClick?: () => void;
  /** 指针移入事件 */
  onPointerEnter?: () => void;
}

/**
 * 桌面应用图标组件
 */
export const AppIcon = ({ app, onDoubleClick, onPointerEnter }: AppIconProps) => {
  const { t } = useTranslation();
  const appName = app.nameKey ? t(app.nameKey) : app.name;
  const appDescription = app.descriptionKey
    ? t(app.descriptionKey)
    : (app.description || app.name);

  // 根据应用类型添加不同的样式
  const getIconClass = () => {
    const classes = [styles.icon];
    if (app.type === 'external') {
      classes.push(styles.external);
    } else if (app.type === 'iframe') {
      classes.push(styles.iframe);
    }
    return classes.join(' ');
  };

  return (
    <div
      className={getIconClass()}
      onDoubleClick={onDoubleClick}
      onPointerEnter={onPointerEnter}
      onFocus={onPointerEnter}
      title={appDescription}
    >
      <div className={styles.iconImage}>
        {app.icon.startsWith('mdi:') || app.icon.startsWith('ic:') ? (
          <Icon icon={app.icon} size={36} />
        ) : (
          <span className={styles.emoji}>{app.icon}</span>
        )}
        {/* 外部应用显示外链图标标识 */}
        {app.type === 'external' && (
          <div className={styles.externalBadge}>
            <Icon icon="mdi:open-in-new" size={14} />
          </div>
        )}
        {/* 嵌入应用显示 iframe 标识 */}
        {app.type === 'iframe' && (
          <div className={styles.iframeBadge}>
            <Icon icon="mdi:web" size={14} />
          </div>
        )}
      </div>
      <span className={styles.iconName}>{appName}</span>
    </div>
  );
};
