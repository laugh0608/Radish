import { type MouseEvent } from 'react';
import { Icon } from '@radish/ui/icon';
import styles from './PublicShellHeader.module.css';

interface PublicShellHeaderProps {
  brandMark: string;
  brandName: string;
  brandSubline: string;
  onBrandClick: () => void;
  onNavigateToDiscover?: () => void;
  discoverHref?: string;
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
  discoverHref = '/discover',
  discoverLabel = '发现',
  circleHref = '/circle',
  circleLabel = '圈子',
  showCircleAction = true,
  desktopHref = '/desktop',
  desktopLabel = '工作台'
}: PublicShellHeaderProps) => {
  const normalizedDiscoverHref = discoverHref.trim();
  const shouldShowDiscoverAction = !!onNavigateToDiscover && normalizedDiscoverHref.length > 0;
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
          {shouldShowDiscoverAction ? (
            <a
              className={`${styles.actionButton} ${styles.primaryAction}`}
              href={normalizedDiscoverHref}
              aria-label={discoverLabel}
              title={discoverLabel}
              onClick={(event) => {
                if (!shouldHandlePublicShellLinkClick(event)) {
                  return;
                }

                event.preventDefault();
                onNavigateToDiscover?.();
              }}
            >
              <Icon icon="mdi:compass-outline" size={18} />
              <span className={styles.actionLabel}>{discoverLabel}</span>
            </a>
          ) : null}
          {shouldShowCircleAction ? (
            <a
              className={`${styles.actionButton} ${shouldShowDiscoverAction ? styles.secondaryAction : styles.primaryAction}`}
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

function shouldHandlePublicShellLinkClick(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}
