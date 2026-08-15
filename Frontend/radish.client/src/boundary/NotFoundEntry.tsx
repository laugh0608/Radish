import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { WebStateSlot } from '@/components/web-shell';
import styles from './BoundaryPage.module.css';

export function NotFoundEntry() {
  const { t } = useTranslation();
  const pathname = window.location.pathname;

  useEffect(() => {
    document.title = `${t('boundary.notFound.documentTitle')} · Radish`;
  }, [t]);

  return (
    <main className={styles.page}>
      <WebStateSlot
        className={styles.panel}
        tone="notFound"
        title={t('boundary.notFound.title')}
        description={t('boundary.notFound.description')}
        meta={(
          <span className={styles.path}>
            {t('boundary.notFound.path', { path: pathname })}
          </span>
        )}
        actions={[
          {
            label: t('boundary.notFound.discover'),
            href: '/discover',
          },
          {
            label: t('boundary.notFound.workbench'),
            href: '/workbench',
            kind: 'secondary',
          },
        ]}
      />
    </main>
  );
}
