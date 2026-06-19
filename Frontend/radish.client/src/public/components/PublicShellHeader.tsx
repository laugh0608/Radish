import { Icon } from '@radish/ui/icon';
import styles from './PublicShellHeader.module.css';

interface PublicShellHeaderProps {
  brandMark: string;
  brandName: string;
  brandSubline: string;
  onBrandClick: () => void;
  onNavigateToDiscover?: () => void;
  discoverLabel?: string;
  circleHref?: string;
  circleLabel?: string;
  showCircleAction?: boolean;
  desktopHref?: string;
  desktopLabel?: string;
}

export const PublicShellHeader = ({
  brandMark,
  brandName,
  brandSubline,
  onBrandClick,
  onNavigateToDiscover,
  discoverLabel = '发现',
  circleHref = '/circle',
  circleLabel = '圈子',
  showCircleAction = true,
  desktopHref = '/desktop',
  desktopLabel = '工作台'
}: PublicShellHeaderProps) => {
  const shouldShowCircleAction = showCircleAction && circleHref.trim().length > 0;

  return (
    <header className={styles.hero}>
      <div className={styles.heroInner}>
        <button type="button" className={styles.brand} onClick={onBrandClick}>
          <span className={styles.brandMark}>{brandMark}</span>
          <span className={styles.brandText}>
            <span className={styles.brandName}>{brandName}</span>
            <span className={styles.brandSubline}>{brandSubline}</span>
          </span>
        </button>
        <div className={styles.heroActions}>
          {onNavigateToDiscover ? (
            <button
              type="button"
              className={`${styles.actionButton} ${styles.primaryAction}`}
              onClick={onNavigateToDiscover}
              title={discoverLabel}
            >
              <Icon icon="mdi:compass-outline" size={18} />
              <span className={styles.actionLabel}>{discoverLabel}</span>
            </button>
          ) : null}
          {shouldShowCircleAction ? (
            <a
              className={`${styles.actionButton} ${onNavigateToDiscover ? styles.secondaryAction : styles.primaryAction}`}
              href={circleHref}
              aria-label={circleLabel}
              title={circleLabel}
            >
              <Icon icon="mdi:account-group-outline" size={18} />
              <span className={styles.actionLabel}>{circleLabel}</span>
            </a>
          ) : null}
          <a className={styles.desktopLink} href={desktopHref} aria-label={desktopLabel} title={desktopLabel}>
            <Icon icon="mdi:view-dashboard-outline" size={18} />
            <span className={styles.actionLabel}>{desktopLabel}</span>
          </a>
        </div>
      </div>
    </header>
  );
};
