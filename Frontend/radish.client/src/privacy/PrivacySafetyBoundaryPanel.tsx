import { Icon } from '@radish/ui/icon';
import { useTranslation } from 'react-i18next';
import {
  privacyBoundaryEntries,
  safetyResponseEntries,
  type PrivacyBoundaryAudience,
} from './privacySafetyBoundaryData';
import styles from './PrivacySafetyBoundaryPanel.module.css';

interface PrivacySafetyBoundaryPanelProps {
  className?: string;
  variant?: 'default' | 'compact';
}

const audienceToneMap: Record<PrivacyBoundaryAudience, string> = {
  public: 'public',
  private: 'private',
  console: 'console',
  restricted: 'restricted',
};

export function PrivacySafetyBoundaryPanel({
  className,
  variant = 'default',
}: PrivacySafetyBoundaryPanelProps) {
  const { t } = useTranslation();
  const panelClassName = [
    styles.panel,
    variant === 'compact' ? styles.panelCompact : '',
    className ?? '',
  ].filter(Boolean).join(' ');

  return (
    <section className={panelClassName} aria-labelledby="privacy-safety-boundary-title">
      <div className={styles.header}>
        <span>{t('privacySafety.eyebrow')}</span>
        <h2 id="privacy-safety-boundary-title">{t('privacySafety.title')}</h2>
        <p>{t('privacySafety.description')}</p>
      </div>

      <div className={styles.boundaryGrid} aria-label={t('privacySafety.boundaryLabel')}>
        {privacyBoundaryEntries.map((entry) => (
          <article className={styles.boundaryCard} key={entry.id} data-tone={audienceToneMap[entry.audience]}>
            <div className={styles.boundaryCardHeader}>
              <span className={styles.boundaryIcon} aria-hidden="true">
                <Icon icon={entry.icon} size={18} />
              </span>
              <span className={styles.boundaryBadge}>
                {t(`privacySafety.audience.${entry.audience}`)}
              </span>
            </div>
            <h3>{t(entry.titleKey)}</h3>
            <p>{t(entry.descriptionKey)}</p>
            <ul>
              {entry.exampleKeys.map((key) => (
                <li key={key}>{t(key)}</li>
              ))}
            </ul>
          </article>
        ))}
      </div>

      <div className={styles.responseList} aria-label={t('privacySafety.responseLabel')}>
        {safetyResponseEntries.map((entry) => (
          <article className={styles.responseItem} key={entry.id}>
            <span className={styles.responseIcon} aria-hidden="true">
              <Icon icon={entry.icon} size={17} />
            </span>
            <span>
              <strong>{t(entry.titleKey)}</strong>
              <small>{t(entry.descriptionKey)}</small>
            </span>
          </article>
        ))}
      </div>
    </section>
  );
}
