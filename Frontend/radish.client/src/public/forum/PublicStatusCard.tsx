import { Icon } from '@radish/ui/icon';
import { handlePublicForumLinkClick } from './publicForumLinkHandlers';
import styles from './PublicForumApp.module.css';

type PublicStatusTone = 'loading' | 'empty' | 'error' | 'notFound' | 'info';

interface PublicStatusCardProps {
  tone: PublicStatusTone;
  title: string;
  description: string;
  compact?: boolean;
  primaryAction?: {
    label: string;
    href?: string;
    onClick: () => void;
  };
  secondaryAction?: {
    label: string;
    href?: string;
    onClick: () => void;
  };
}

export function PublicStatusCard({
  tone,
  title,
  description,
  compact = false,
  primaryAction,
  secondaryAction
}: PublicStatusCardProps) {
  const icon = tone === 'loading'
    ? 'mdi:progress-clock'
    : tone === 'empty'
      ? 'mdi:text-box-search-outline'
      : tone === 'notFound'
        ? 'mdi:file-search-outline'
        : tone === 'info'
          ? 'mdi:information-outline'
          : 'mdi:alert-circle-outline';

  return (
    <div
      className={`${styles.statusCard} ${compact ? styles.statusCardCompact : ''}`}
      data-tone={tone}
    >
      <div className={styles.statusIcon}>
        <Icon icon={icon} size={compact ? 18 : 22} />
      </div>
      <div className={styles.statusBody}>
        <h2 className={styles.statusTitle}>{title}</h2>
        <p className={styles.statusDescription}>{description}</p>
        {(primaryAction || secondaryAction) && (
          <div className={styles.statusActions}>
            {primaryAction && (
              primaryAction.href ? (
                <a
                  className={styles.retryButton}
                  href={primaryAction.href}
                  onClick={(event) => handlePublicForumLinkClick(event, primaryAction.onClick)}
                >
                  {primaryAction.label}
                </a>
              ) : (
                <button type="button" className={styles.retryButton} onClick={primaryAction.onClick}>
                  {primaryAction.label}
                </button>
              )
            )}
            {secondaryAction && (
              secondaryAction.href ? (
                <a
                  className={styles.secondaryButton}
                  href={secondaryAction.href}
                  onClick={(event) => handlePublicForumLinkClick(event, secondaryAction.onClick)}
                >
                  {secondaryAction.label}
                </a>
              ) : (
                <button type="button" className={styles.secondaryButton} onClick={secondaryAction.onClick}>
                  {secondaryAction.label}
                </button>
              )
            )}
          </div>
        )}
      </div>
    </div>
  );
}
