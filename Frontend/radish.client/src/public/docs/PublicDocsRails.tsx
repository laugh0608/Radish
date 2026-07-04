import type { MouseEvent } from 'react';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import type { WikiDocumentDetailVo, WikiDocumentVo } from '@/apps/wiki/types/wiki';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { buildPublicDocsPath } from '../docsRouteState';
import { PublicReadingGuide } from '../components/PublicReadingGuide';
import { toSourceText, toStatusText, toVisibilityText } from './publicDocsFormat';
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

const listGuideItems = [
  {
    labelKey: 'wiki.public.listGuideFocusLabel',
    valueKey: 'wiki.public.listGuideFocusValue',
  },
  {
    labelKey: 'wiki.public.listGuideNextLabel',
    valueKey: 'wiki.public.listGuideNextValue',
  },
  {
    labelKey: 'wiki.public.listGuideBoundaryLabel',
    valueKey: 'wiki.public.listGuideBoundaryValue',
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
  searchHref: string;
  onOpenSearch: () => void;
}

interface PublicDocsSearchRailProps {
  browseDirectoryHref: string;
  hasKeyword: boolean;
  resultCount: number;
  currentPage: number;
  totalPages: number;
  onBrowseDirectory: () => void;
}

interface PublicDocsDetailRailProps {
  document: WikiDocumentDetailVo;
  relatedDocuments: WikiDocumentVo[];
  displayTimeZone: string;
  backLabel: string;
  backHref: string;
  canEditDocument: boolean;
  editHref: string;
  onBack: () => void;
  onOpenDocument: (slug: string) => void;
}

function shouldHandlePublicDocsLink(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function handlePublicDocsLinkClick(event: MouseEvent<HTMLAnchorElement>, action: () => void) {
  if (!shouldHandlePublicDocsLink(event)) {
    return;
  }

  event.preventDefault();
  action();
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
  authorHref,
  searchHref,
  onOpenSearch
}: PublicDocsListRailProps) {
  const { t } = useTranslation();

  return (
    <aside className={styles.sideRail} aria-label={t('wiki.public.indexRailLabel')}>
      <PublicReadingGuide
        label={t('wiki.public.listGuideKicker')}
        title={t('wiki.public.listGuideTitle')}
        description={t('wiki.public.listGuideDescription')}
        items={toGuideItems(t, listGuideItems)}
      />

      <section className={styles.railPanel}>
        <div className={styles.railPanelHeader}>
          <span className={styles.railIcon}>
            <Icon icon="mdi:magnify" size={18} />
          </span>
          <div>
            <h2 className={styles.railTitle}>{t('wiki.public.indexRailSearchTitle')}</h2>
            <p className={styles.railText}>{t('wiki.public.indexRailSearchDescription')}</p>
          </div>
        </div>
        <a
          className={`${styles.secondaryButton} ${styles.railAction}`}
          href={searchHref}
          onClick={(event) => handlePublicDocsLinkClick(event, onOpenSearch)}
        >
          <Icon icon="mdi:magnify" size={18} />
          <span>{t('wiki.public.searchAction')}</span>
        </a>
      </section>

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
  browseDirectoryHref,
  hasKeyword,
  resultCount,
  currentPage,
  totalPages,
  onBrowseDirectory
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
            <Icon icon="mdi:file-tree-outline" size={18} />
          </span>
          <div>
            <h2 className={styles.railTitle}>{t('wiki.public.searchRailDirectoryTitle')}</h2>
            <p className={styles.railText}>{t('wiki.public.searchRailDirectoryDescription')}</p>
          </div>
        </div>
        <a
          className={`${styles.secondaryButton} ${styles.railAction}`}
          href={browseDirectoryHref}
          onClick={(event) => handlePublicDocsLinkClick(event, onBrowseDirectory)}
        >
          <Icon icon="mdi:arrow-left" size={18} />
          <span>{t('wiki.public.backToList')}</span>
        </a>
      </section>

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
  displayTimeZone,
  backLabel,
  backHref,
  canEditDocument,
  editHref,
  onBack,
  onOpenDocument
}: PublicDocsDetailRailProps) {
  const { t } = useTranslation();
  const relatedCards = useMemo(
    () => relatedDocuments.filter((item) => item.voSlug !== document.voSlug).slice(0, 4),
    [document.voSlug, relatedDocuments]
  );
  const updatedAt = formatDateTimeByTimeZone(document.voModifyTime || document.voCreateTime, displayTimeZone);

  return (
    <aside className={styles.sideRail} aria-label={t('wiki.public.detailRailLabel')}>
      <section className={styles.railPanel}>
        <div className={styles.railPanelHeader}>
          <span className={styles.railIcon}>
            <Icon icon="mdi:arrow-u-left-top" size={18} />
          </span>
          <div>
            <h2 className={styles.railTitle}>{t('wiki.public.detailRailSourceTitle')}</h2>
            <p className={styles.railText}>{t('wiki.public.detailRailSourceDescription')}</p>
          </div>
        </div>
        <a
          className={`${styles.secondaryButton} ${styles.railAction}`}
          href={backHref}
          onClick={(event) => handlePublicDocsLinkClick(event, onBack)}
        >
          <Icon icon="mdi:arrow-left" size={18} />
          <span>{backLabel}</span>
        </a>
      </section>

      <PublicReadingGuide
        label={t('wiki.public.detailGuideKicker')}
        title={t('wiki.public.detailGuideTitle')}
        description={t('wiki.public.detailGuideDescription')}
        items={toGuideItems(t, detailGuideItems)}
      />

      <section className={styles.railPanel}>
        <div className={styles.railPanelHeader}>
          <span className={styles.railIcon}>
            <Icon icon="mdi:information-outline" size={18} />
          </span>
          <div>
            <h2 className={styles.railTitle}>{t('wiki.public.detailRailMetaTitle')}</h2>
            <p className={styles.railText}>{t('wiki.public.detailRailMetaDescription')}</p>
          </div>
        </div>
        <div className={styles.railChipRow}>
          <span className={styles.metaChip}>{toVisibilityText(t, document.voVisibility)}</span>
          <span className={styles.metaChip}>{toStatusText(t, document.voStatus)}</span>
          <span className={styles.metaChip}>{t('wiki.meta.source', { value: toSourceText(t, document.voSourceType) })}</span>
          <span className={styles.metaChip}>{t('wiki.meta.updated', { value: updatedAt })}</span>
        </div>
      </section>

      <section className={styles.railPanel}>
        <div className={styles.railPanelHeader}>
          <span className={styles.railIcon}>
            <Icon icon={canEditDocument ? 'mdi:pencil-outline' : 'mdi:book-open-variant'} size={18} />
          </span>
          <div>
            <h2 className={styles.railTitle}>{t('wiki.public.detailRailAuthorTitle')}</h2>
            <p className={styles.railText}>
              {canEditDocument
                ? t('wiki.public.detailRailAuthorDescription')
                : t('wiki.public.detailRailReaderDescription')}
            </p>
          </div>
        </div>
        {canEditDocument ? (
          <a className={`${styles.primaryButton} ${styles.railAction}`} href={editHref}>
            <Icon icon="mdi:pencil-outline" size={18} />
            <span>{t('wiki.public.detailRailAuthorAction')}</span>
          </a>
        ) : null}
      </section>

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
