import { Icon } from '@radish/ui/icon';
import styles from './PublicShellHeader.module.css';

interface PublicShellHeaderProps {
  brandMark: string;
  brandName: string;
  brandSubline: string;
  onBrandClick: () => void;
  onNavigateToDiscover?: () => void;
  discoverLabel?: string;
  desktopLabel?: string;
}

export const PublicShellHeader = ({
  brandMark,
  brandName,
  brandSubline,
  onBrandClick,
  onNavigateToDiscover,
  discoverLabel = '发现',
  desktopLabel = 'WebOS'
}: PublicShellHeaderProps) => {
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
            <button type="button" className={styles.actionButton} onClick={onNavigateToDiscover}>
              <Icon icon="mdi:compass-outline" size={18} />
              <span>{discoverLabel}</span>
            </button>
          ) : null}
          <a className={styles.desktopLink} href="/">
            <Icon icon="mdi:view-dashboard-outline" size={18} />
            <span>{desktopLabel}</span>
          </a>
        </div>
      </div>
    </header>
  );
};
