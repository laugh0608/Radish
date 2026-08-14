import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import type { WikiDocumentDetailVo, WikiDocumentVo } from '@/apps/wiki/types/wiki';
import { buildPublicDocsPath } from '../docsRouteState';
import { PublicReadingGuide } from '../components/PublicReadingGuide';
import { handlePublicDocsLinkClick } from './publicDocsViewSupport';
import styles from './PublicDocsApp.module.css';

const searchGuideItems = [
  {
    labelKey: 'wiki.public.searchGuideFocusLabel',
    valueKey: 'wiki.public.searchGuideFocusValue',
  },
  {
    labelKey: 'wiki.public.searchGuideNextLabel',
    valueKey: 'wiki.public.searchGuideNextValue',
  },
  {
    labelKey: 'wiki.public.searchGuideBoundaryLabel',
    valueKey: 'wiki.public.searchGuideBoundaryValue',
  },
] as const;

const detailGuideItems = [
  {
    labelKey: 'wiki.public.detailGuideFocusLabel',
    valueKey: 'wiki.public.detailGuideFocusValue',
  },
  {
    labelKey: 'wiki.public.detailGuideNextLabel',
    valueKey: 'wiki.public.detailGuideNextValue',
  },
  {
    labelKey: 'wiki.public.detailGuideBoundaryLabel',
    valueKey: 'wiki.public.detailGuideBoundaryValue',
  },
] as const;

interface PublicDocsListRailProps {
  directoryCount: number;
  totalDocuments: number;
  canUseDocsAuthorTools: boolean;
  authorHref: string;
}

interface PublicDocsSearchRailProps {
  hasKeyword: boolean;
  resultCount: number;
  currentPage: number;
  totalPages: number;
}

interface PublicDocsDetailRailProps {
  document: WikiDocumentDetailVo;
  relatedDocuments: WikiDocumentVo[];
  onOpenDocument: (slug: string) => void;
}

function toGuideItems(
  t: (key: string, options?: Record<string, unknown>) => string,
  items: readonly { labelKey: string; valueKey: string }[]
) {
  return items.map((item) => ({
    label: t(item.labelKey),
    value: t(item.valueKey),
  }));
}

export function PublicDocsListRail({
  directoryCount,
  totalDocuments,
  canUseDocsAuthorTools,
  authorHref
}: PublicDocsListRailProps) {
  const { t } = useTranslation();

  return (
    <aside className={styles.sideRail} aria-label={t('wiki.public.indexRailLabel')}>
      <section className={styles.railPanel}>
        <div className={styles.railPanelHeader}>
          <span className={styles.railIcon}>
            <Icon icon="mdi:file-tree-outline" size={18} />
          </span>
          <div>
            <h2 className={styles.railTitle}>{t('wiki.public.indexRailScopeTitle')}</h2>
            <p className={styles.railText}>{t('wiki.public.indexRailScopeDescription')}</p>
          </div>
        </div>
        <div className={styles.railStatGrid}>
          <span className={styles.railStat}>
            <strong className={styles.railStatValue}>{directoryCount}</strong>
            <span className={styles.railStatLabel}>{t('wiki.public.indexRailDirectoryStat')}</span>
          </span>
          <span className={styles.railStat}>
            <strong className={styles.railStatValue}>{totalDocuments}</strong>
            <span className={styles.railStatLabel}>{t('wiki.public.indexRailDocumentStat')}</span>
          </span>
        </div>
      </section>

      <section className={styles.railPanel}>
        <div className={styles.railPanelHeader}>
          <span className={styles.railIcon}>
            <Icon icon={canUseDocsAuthorTools ? 'mdi:pencil-box-outline' : 'mdi:lock-outline'} size={18} />
          </span>
          <div>
            <h2 className={styles.railTitle}>{t('wiki.public.indexRailAuthorTitle')}</h2>
            <p className={styles.railText}>
              {canUseDocsAuthorTools
                ? t('wiki.public.indexRailAuthorDescription')
                : t('wiki.public.indexRailReaderDescription')}
            </p>
          </div>
        </div>
        {canUseDocsAuthorTools ? (
          <a className={`${styles.primaryButton} ${styles.railAction}`} href={authorHref}>
            <Icon icon="mdi:pencil-box-outline" size={18} />
            <span>{t('wiki.public.indexRailAuthorAction')}</span>
          </a>
        ) : null}
      </section>
    </aside>
  );
}

export function PublicDocsSearchRail({
  hasKeyword,
  resultCount,
  currentPage,
  totalPages
}: PublicDocsSearchRailProps) {
  const { t } = useTranslation();

  return (
    <aside className={styles.sideRail} aria-label={t('wiki.public.searchRailLabel')}>
      <PublicReadingGuide
        label={t('wiki.public.searchGuideKicker')}
        title={t('wiki.public.searchGuideTitle')}
        description={t('wiki.public.searchGuideDescription')}
        items={toGuideItems(t, searchGuideItems)}
      />

      <section className={styles.railPanel}>
        <div className={styles.railPanelHeader}>
          <span className={styles.railIcon}>
            <Icon icon="mdi:format-list-numbered" size={18} />
          </span>
          <div>
            <h2 className={styles.railTitle}>{t('wiki.public.searchRailResultTitle')}</h2>
            <p className={styles.railText}>
              {hasKeyword
                ? t('wiki.public.searchRailResultDescription')
                : t('wiki.public.searchRailIdleDescription')}
            </p>
          </div>
        </div>
        <div className={styles.railChipRow}>
          <span className={styles.metaChip}>{t('wiki.public.searchResultCount', { count: resultCount })}</span>
          {hasKeyword && totalPages > 1 ? (
            <span className={styles.metaChip}>{t('common.pageInfo', { current: currentPage, total: totalPages })}</span>
          ) : null}
        </div>
      </section>
    </aside>
  );
}

export function PublicDocsDetailRail({
  document,
  relatedDocuments,
  onOpenDocument
}: PublicDocsDetailRailProps) {
  const { t } = useTranslation();
  const relatedCards = useMemo(
    () => relatedDocuments.filter((item) => item.voSlug !== document.voSlug).slice(0, 4),
    [document.voSlug, relatedDocuments]
  );

  return (
    <aside className={styles.sideRail} aria-label={t('wiki.public.detailRailLabel')}>
      <PublicReadingGuide
        label={t('wiki.public.detailGuideKicker')}
        title={t('wiki.public.detailGuideTitle')}
        description={t('wiki.public.detailGuideDescription')}
        items={toGuideItems(t, detailGuideItems)}
      />

      <section className={styles.railPanel}>
        <div className={styles.railPanelHeader}>
          <span className={styles.railIcon}>
            <Icon icon="mdi:file-link-outline" size={18} />
          </span>
          <div>
            <h2 className={styles.railTitle}>{t('wiki.public.detailRailRelatedTitle')}</h2>
            <p className={styles.railText}>{t('wiki.public.detailRailRelatedDescription')}</p>
          </div>
        </div>
        {relatedCards.length === 0 ? (
          <p className={styles.railText}>{t('wiki.public.detailRailRelatedEmpty')}</p>
        ) : (
          <div className={styles.relatedDocList}>
            {relatedCards.map((item) => {
              const href = buildPublicDocsPath({ kind: 'detail', slug: item.voSlug });

              return (
                <a
                  key={item.voId}
                  className={styles.relatedDocLink}
                  href={href}
                  onClick={(event) => handlePublicDocsLinkClick(event, () => onOpenDocument(item.voSlug))}
                >
                  <span className={styles.relatedDocTitle}>{item.voTitle}</span>
                  <span className={styles.relatedDocMeta}>
                    {item.voSummary?.trim() || t('wiki.public.summaryFallback')}
                  </span>
                </a>
              );
            })}
          </div>
        )}
      </section>
    </aside>
  );
}
