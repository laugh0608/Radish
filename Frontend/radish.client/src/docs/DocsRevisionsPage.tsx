import type { MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import type { LongId } from '@/api/user';
import { formatWikiTime } from '@/apps/wiki/wikiApp.helpers';
import { ContentSnapshotDiff, type ContentSnapshot } from '@/components/content-diff/ContentSnapshotDiff';
import { WebStateSlot, type WebStateSlotTone } from '@/components/web-shell';
import { buildPublicDocsPath } from '@/public/docsRouteState';
import { formatDocsAuthorNumber, getDocsAuthorSourceText, resolveDocsAuthorPublicReadSlug } from './docsAuthorPresentation';
import { buildDocsAuthorPath } from './docsAuthorRouteState';
import {
  isSameLongId,
  type RevisionComparisonMode,
  type RevisionState,
} from './docsRevisionComparison';
import styles from './DocsAuthorApp.module.css';

interface DocsRevisionsPageProps {
  state: RevisionState;
  language?: string;
  onBack: (event: MouseEvent<HTMLAnchorElement>) => void;
  onReload: () => void;
  onSelectRevision: (revisionId: LongId) => void;
  onComparisonModeChange: (mode: RevisionComparisonMode) => void;
  onRetryRevision: () => void;
  onRetryComparison: () => void;
}

interface StatusPanelProps {
  icon: string;
  title: string;
  description: string;
}

function StatusPanel({ icon, title, description }: StatusPanelProps) {
  const tone: WebStateSlotTone = icon === 'mdi:progress-clock'
    ? 'loading'
    : icon === 'mdi:alert-circle-outline'
      ? 'error'
      : icon === 'mdi:history'
        ? 'empty'
        : 'info';

  return (
    <section className={styles.statusPanel}>
      <WebStateSlot tone={tone} icon={icon} title={title} description={description} />
    </section>
  );
}

function RailMetric({ label, value, language }: { label: string; value: number | string; language?: string }) {
  return (
    <div className={styles.railMetric}>
      <span>{label}</span>
      <strong>{typeof value === 'number' ? formatDocsAuthorNumber(value, language) : value}</strong>
    </div>
  );
}

export function DocsRevisionsPage({
  state,
  language,
  onBack,
  onReload,
  onSelectRevision,
  onComparisonModeChange,
  onRetryRevision,
  onRetryComparison,
}: DocsRevisionsPageProps) {
  const { t } = useTranslation();
  const publicReadSlug = state.history
    ? resolveDocsAuthorPublicReadSlug({
        status: state.history.voStatus,
        documentVersion: state.history.voDocumentVersion,
        documentSlug: state.history.voSlug,
      })
    : null;
  const publicReadHref = publicReadSlug
    ? buildPublicDocsPath({ kind: 'detail', slug: publicReadSlug })
    : null;
  const currentRevision = state.revisions.find((revision) => revision.voIsCurrent) ?? null;
  const selectedVersion = state.selectedRevision?.voVersion
    ?? currentRevision?.voVersion
    ?? state.history?.voDocumentVersion
    ?? '-';
  const selectedIsCurrent = Boolean(state.revisions.find(
    (revision) => isSameLongId(revision.voId, state.selectedRevisionId),
  )?.voIsCurrent);
  const selectedSnapshot: ContentSnapshot | null = state.selectedRevision ? {
    content: state.selectedRevision.voMarkdownContent,
    fields: [{
      key: 'title',
      label: t('wiki.author.revisions.field.title'),
      value: state.selectedRevision.voTitle,
    }],
  } : null;
  const comparisonSnapshot: ContentSnapshot | null = state.comparisonRevision ? {
    content: state.comparisonRevision.voMarkdownContent,
    fields: [{
      key: 'title',
      label: t('wiki.author.revisions.field.title'),
      value: state.comparisonRevision.voTitle,
    }],
  } : null;
  const beforeSnapshot = state.comparisonMode === 'previous' ? comparisonSnapshot : selectedSnapshot;
  const afterSnapshot = state.comparisonMode === 'previous' ? selectedSnapshot : comparisonSnapshot;
  const beforeVersion = state.comparisonMode === 'previous'
    ? state.comparisonRevision?.voVersion
    : state.selectedRevision?.voVersion;
  const afterVersion = state.comparisonMode === 'previous'
    ? state.selectedRevision?.voVersion
    : state.comparisonRevision?.voVersion;
  const comparisonUnavailableText = state.comparisonMissing
    ? t('wiki.author.revisions.noEarlierVersion')
    : state.comparisonError || t('wiki.author.revisions.comparisonUnavailable');

  return (
    <div className={styles.authorWorkspace}>
      <section className={styles.panel}>
        <div className={styles.panelHeader}>
          <div>
            <p className={styles.kicker}>Revision History</p>
            <h1 className={styles.pageTitle}>{state.history?.voTitle || t('wiki.author.revisions.title')}</h1>
            <p className={styles.pageIntro}>{t('wiki.author.revisions.intro')}</p>
          </div>
          <div className={styles.headerActions}>
            <button type="button" className={styles.secondaryButton} onClick={onReload} disabled={state.loading}>
              <Icon icon={state.loading ? 'mdi:progress-clock' : 'mdi:refresh'} size={18} />
              <span>{state.loading ? t('wiki.author.actions.refreshing') : t('wiki.author.actions.refresh')}</span>
            </button>
            <a className={styles.secondaryButton} href={buildDocsAuthorPath({ kind: 'mine' })} onClick={onBack}>
              <Icon icon="mdi:arrow-left" size={18} />
              <span>{t('wiki.author.actions.backToList')}</span>
            </a>
            {publicReadHref ? (
              <a className={styles.secondaryButton} href={publicReadHref}>
                <Icon icon="mdi:book-open-page-variant-outline" size={18} />
                <span>{t('wiki.author.actions.publicReading')}</span>
              </a>
            ) : null}
          </div>
        </div>

        {state.loading ? (
          <StatusPanel
            icon="mdi:progress-clock"
            title={t('wiki.author.revisions.loadingTitle')}
            description={t('wiki.author.revisions.loadingDescription')}
          />
        ) : state.historyError && state.revisions.length === 0 ? (
          <StatusPanel
            icon="mdi:alert-circle-outline"
            title={t('wiki.author.revisions.errorTitle')}
            description={state.historyError}
          />
        ) : state.revisions.length === 0 ? (
          <StatusPanel
            icon="mdi:history"
            title={t('wiki.author.revisions.emptyTitle')}
            description={t('wiki.author.revisions.emptyDescription')}
          />
        ) : (
          <div className={styles.revisionLayout}>
            <aside className={styles.revisionList}>
              {state.historyError ? (
                <div className={styles.revisionInlineNotice} role="alert">
                  <strong>{t('wiki.author.revisions.historyStaleTitle')}</strong>
                  <span>{state.historyError}</span>
                </div>
              ) : null}
              {state.revisions.map((revision) => (
                <button
                  key={revision.voId}
                  type="button"
                  className={isSameLongId(revision.voId, state.selectedRevisionId)
                    ? styles.revisionItemActive
                    : styles.revisionItem}
                  onClick={() => onSelectRevision(revision.voId)}
                >
                  <span className={styles.revisionTitle}>v{revision.voVersion}</span>
                  <span className={styles.revisionSummary}>
                    {revision.voChangeSummary || t('wiki.author.revisions.noSummary')}
                  </span>
                  <span className={styles.revisionMeta}>
                    {formatWikiTime(revision.voCreateTime, language)} · {revision.voCreateBy}
                  </span>
                  {revision.voIsCurrent ? (
                    <span className={styles.statusChip}>{t('wiki.author.revisions.current')}</span>
                  ) : null}
                </button>
              ))}
            </aside>

            <article className={styles.revisionPreview}>
              {state.detailError && state.selectedRevision ? (
                <div className={styles.revisionInlineNotice} role="alert">
                  <strong>{t(state.detailStale
                    ? 'wiki.author.revisions.detailStaleTitle'
                    : 'wiki.author.revisions.detailErrorTitle')}</strong>
                  <span>{state.detailError}</span>
                  <button type="button" onClick={onRetryRevision}>{t('wiki.author.revisions.retryDetail')}</button>
                </div>
              ) : null}
              {state.loadingDetail && !state.selectedRevision ? (
                <StatusPanel
                  icon="mdi:progress-clock"
                  title={t('wiki.author.revisions.detailLoadingTitle')}
                  description={t('wiki.author.revisions.detailLoadingDescription')}
                />
              ) : state.detailError && !state.selectedRevision ? (
                <div className={styles.revisionDetailUnavailable}>
                  <StatusPanel
                    icon="mdi:alert-circle-outline"
                    title={t('wiki.author.revisions.detailErrorTitle')}
                    description={state.detailError}
                  />
                  <button type="button" className={styles.secondaryButton} onClick={onRetryRevision}>
                    {t('wiki.author.revisions.retryDetail')}
                  </button>
                </div>
              ) : state.selectedRevision ? (
                <>
                  <div className={styles.revisionPreviewHeader}>
                    <div>
                      <h2 className={styles.revisionPreviewTitle}>
                        {t('wiki.author.revisions.compareTitle', {
                          before: beforeVersion ?? '—',
                          after: afterVersion ?? '—',
                        })}
                      </h2>
                      <p className={styles.revisionMeta}>{t('wiki.author.revisions.diffDescription')}</p>
                    </div>
                    <div className={styles.revisionCompareModes} aria-label={t('wiki.author.revisions.compareModeLabel')}>
                      <button
                        type="button"
                        className={state.comparisonMode === 'previous'
                          ? styles.revisionCompareModeActive
                          : styles.revisionCompareMode}
                        aria-pressed={state.comparisonMode === 'previous'}
                        onClick={() => onComparisonModeChange('previous')}
                      >
                        {t('wiki.author.revisions.comparePrevious')}
                      </button>
                      <button
                        type="button"
                        className={state.comparisonMode === 'current'
                          ? styles.revisionCompareModeActive
                          : styles.revisionCompareMode}
                        aria-pressed={state.comparisonMode === 'current'}
                        disabled={selectedIsCurrent}
                        onClick={() => onComparisonModeChange('current')}
                      >
                        {t('wiki.author.revisions.compareCurrent')}
                      </button>
                    </div>
                  </div>
                  {state.comparisonStale && state.comparisonError ? (
                    <div className={styles.revisionInlineNotice} role="status">
                      <strong>{t('wiki.author.revisions.comparisonStaleTitle')}</strong>
                      <span>{state.comparisonError}</span>
                      <button type="button" onClick={onRetryComparison}>
                        {t('wiki.author.revisions.retryComparison')}
                      </button>
                    </div>
                  ) : null}
                  <ContentSnapshotDiff
                    before={beforeSnapshot}
                    after={afterSnapshot}
                    beforeLabel={t('wiki.author.revisions.versionLabel', { version: beforeVersion ?? '—' })}
                    afterLabel={t('wiki.author.revisions.versionLabel', { version: afterVersion ?? '—' })}
                    ariaLabel={t('wiki.author.revisions.diffAriaLabel')}
                    emptyText={t('wiki.author.revisions.selectDescription')}
                    beforeUnavailableText={state.comparisonMode === 'previous'
                      ? comparisonUnavailableText
                      : state.detailError || t('wiki.author.revisions.detailErrorTitle')}
                    afterUnavailableText={state.comparisonMode === 'current'
                      ? comparisonUnavailableText
                      : state.detailError || t('wiki.author.revisions.detailErrorTitle')}
                    loadingBefore={state.comparisonMode === 'previous' ? state.loadingComparison : state.loadingDetail}
                    loadingAfter={state.comparisonMode === 'current' ? state.loadingComparison : state.loadingDetail}
                    loadingText={t('wiki.author.revisions.comparisonLoading')}
                    onRetryBefore={state.comparisonMode === 'previous' && !state.comparisonMissing
                      ? onRetryComparison
                      : undefined}
                    onRetryAfter={state.comparisonMode === 'current' && !state.comparisonMissing
                      ? onRetryComparison
                      : undefined}
                    retryLabel={t('wiki.author.revisions.retryComparison')}
                  />
                </>
              ) : (
                <StatusPanel
                  icon="mdi:file-search-outline"
                  title={t('wiki.author.revisions.selectTitle')}
                  description={t('wiki.author.revisions.selectDescription')}
                />
              )}
            </article>
          </div>
        )}
      </section>

      <aside className={styles.authorRail} aria-label={t('wiki.author.revisions.contextAriaLabel')}>
        <section className={styles.railCard}>
          <p className={styles.railKicker}>{t('wiki.author.revisions.evidence')}</p>
          <div className={styles.railMetricGrid}>
            <RailMetric label={t('wiki.author.revisions.count')} value={state.revisions.length} language={language} />
            <RailMetric
              label={t('wiki.author.revisions.currentVersion')}
              value={currentRevision ? `v${currentRevision.voVersion}` : '-'}
            />
            <RailMetric
              label={t('wiki.author.revisions.selectedVersion')}
              value={selectedVersion === '-' ? '-' : `v${selectedVersion}`}
            />
          </div>
          <p className={styles.railText}>{t('wiki.author.revisions.boundary')}</p>
        </section>

        {state.selectedRevision ? (
          <section className={styles.railCard}>
            <p className={styles.railKicker}>{t('wiki.author.revisions.selectedSnapshot')}</p>
            <h2 className={styles.railTitle}>{state.selectedRevision.voTitle}</h2>
            <p className={styles.railText}>
              {state.selectedRevision.voChangeSummary || t('wiki.author.revisions.noSummary')}
            </p>
            <div className={styles.railChipList}>
              <span className={styles.railChip}>
                {getDocsAuthorSourceText(state.selectedRevision.voSourceType, t)}
              </span>
              <span className={styles.railChip}>
                {formatWikiTime(state.selectedRevision.voCreateTime, language)}
              </span>
              {state.selectedRevision.voIsCurrent ? (
                <span className={styles.railChip}>{t('wiki.author.revisions.current')}</span>
              ) : null}
            </div>
          </section>
        ) : null}

        <section className={styles.railCard}>
          <p className={styles.railKicker}>{t('wiki.author.revisions.flowActions')}</p>
          <div className={styles.railActionList}>
            <a className={styles.railLink} href={buildDocsAuthorPath({ kind: 'mine' })} onClick={onBack}>
              <Icon icon="mdi:arrow-left" size={18} />
              <span>{t('wiki.author.actions.backToList')}</span>
            </a>
            {publicReadHref ? (
              <a className={styles.railLink} href={publicReadHref}>
                <Icon icon="mdi:book-open-page-variant-outline" size={18} />
                <span>{t('wiki.author.actions.publicReading')}</span>
              </a>
            ) : null}
          </div>
        </section>
      </aside>
    </div>
  );
}
