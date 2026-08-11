import { useMemo, useState, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import type { WikiDocumentTreeNodeVo, WikiDocumentVo } from '@/apps/wiki/types/wiki';
import { buildDocsAuthorPath } from '@/docs/docsAuthorRouteState';
import { formatDateTimeByTimeZone } from '@/utils/dateTime';
import { buildPublicDocsPath, createDefaultDocsSearchRoute } from '../docsRouteState';
import { PublicDocsListRail } from './PublicDocsRails';
import { PublicDocsStatusCard } from './PublicDocsStatusCard';
import { toStatusText, toVisibilityText } from './publicDocsFormat';
import {
  handlePublicDocsLinkClick,
  usePublicDocsScrollRestore,
  type PublicDocsDiagnosticCopyHandler,
} from './publicDocsViewSupport';
import styles from './PublicDocsApp.module.css';

const PUBLIC_DOCS_DIRECTORY_PREVIEW_LIMIT = 36;

interface PublicDocsTreeRow {
  id: string;
  slug: string;
  title: string;
  depth: number;
  childCount: number;
}

export interface PublicDocsCollectionState {
  tree: WikiDocumentTreeNodeVo[];
  documents: WikiDocumentVo[];
  totalDocuments: number;
  loadingTree: boolean;
  loadingDocuments: boolean;
  treeError: string | null;
  listError: string | null;
}

interface PublicDocsListProps {
  displayTimeZone: string;
  collectionState: PublicDocsCollectionState;
  scrollContainerRef: RefObject<HTMLDivElement | null>;
  restoreScrollTop: number | null;
  canUseDocsAuthorTools: boolean;
  getDiagnosticActionLabel: () => string;
  onCopyDiagnostics: PublicDocsDiagnosticCopyHandler;
  onScrollRestored: () => void;
  onReload: () => void;
  onOpenSearch: () => void;
  onOpenDocument: (slug: string) => void;
}

function flattenPublicDocsTree(
  nodes: WikiDocumentTreeNodeVo[],
  depth: number = 0
): PublicDocsTreeRow[] {
  return nodes.flatMap((node) => {
    const children = node.voChildren || [];
    return [
      {
        id: node.voId,
        slug: node.voSlug,
        title: node.voTitle,
        depth,
        childCount: children.length
      },
      ...flattenPublicDocsTree(children, depth + 1)
    ];
  });
}

export const PublicDocsList = ({
  displayTimeZone,
  collectionState,
  scrollContainerRef,
  restoreScrollTop,
  canUseDocsAuthorTools,
  getDiagnosticActionLabel,
  onCopyDiagnostics,
  onScrollRestored,
  onReload,
  onOpenSearch,
  onOpenDocument
}: PublicDocsListProps) => {
  const { t } = useTranslation();
  const [directoryExpanded, setDirectoryExpanded] = useState(false);
  const {
    tree,
    documents,
    totalDocuments,
    loadingTree,
    loadingDocuments,
    treeError,
    listError
  } = collectionState;

  usePublicDocsScrollRestore({
    isReady: !loadingTree && !loadingDocuments,
    restoreScrollTop,
    scrollContainerRef,
    onScrollRestored
  });

  const treeRows = useMemo(() => flattenPublicDocsTree(tree), [tree]);
  const visibleTreeRows = directoryExpanded
    ? treeRows
    : treeRows.slice(0, PUBLIC_DOCS_DIRECTORY_PREVIEW_LIMIT);
  const isDirectoryTruncated = treeRows.length > visibleTreeRows.length;
  const listCards = useMemo(() => documents.slice(0, 12), [documents]);
  const searchHref = buildPublicDocsPath(createDefaultDocsSearchRoute());
  const authorHref = buildDocsAuthorPath({ kind: 'mine' });
  const hasAnyContent = treeRows.length > 0 || listCards.length > 0;
  const isLoading = (loadingTree || loadingDocuments) && !hasAnyContent;
  const isError = !hasAnyContent && Boolean(treeError || listError);
  const isEmpty = !loadingTree && !loadingDocuments && !hasAnyContent && !treeError && !listError;

  return (
    <section className={styles.sectionCard} data-public-docs-view="list">
      <div className={styles.sectionHeader}>
        <div className={styles.sectionHeading}>
          <h1 className={styles.pageTitle}>{t('wiki.public.pageTitle')}</h1>
          <p className={styles.pageIntro}>{t('wiki.public.pageIntro')}</p>
        </div>
        <div className={styles.sectionActions}>
          <a
            className={styles.secondaryButton}
            href={searchHref}
            onClick={(event) => handlePublicDocsLinkClick(event, onOpenSearch)}
          >
            <Icon icon="mdi:magnify" size={18} />
            <span>{t('wiki.public.searchAction')}</span>
          </a>
        </div>
      </div>

      <div className={styles.contentWrap}>
        {isLoading ? (
          <PublicDocsStatusCard
            tone="loading"
            title={t('wiki.public.loadingTitle')}
            description={t('wiki.public.loadingDescription')}
          />
        ) : isError ? (
          <PublicDocsStatusCard
            tone="error"
            title={t('wiki.public.listErrorTitle')}
            description={treeError || listError || t('wiki.public.loadingDescription')}
            primaryAction={{
              label: t('common.retry'),
              onClick: onReload
            }}
            diagnosticAction={{
              label: getDiagnosticActionLabel(),
              onClick: () => {
                void onCopyDiagnostics('list-load', treeError || listError);
              }
            }}
          />
        ) : isEmpty ? (
          <PublicDocsStatusCard
            tone="empty"
            title={t('wiki.public.emptyTitle')}
            description={t('wiki.public.emptyDescription')}
          />
        ) : (
          <>
            {(treeError || listError) && (
              <div className={styles.inlineNotice}>
                <span className={styles.inlineNoticeText}>
                  {treeError && listError
                    ? t('wiki.public.partialBothFailed')
                    : treeError
                      ? t('wiki.public.partialTreeFailed')
                      : t('wiki.public.partialListFailed')}
                </span>
                <button type="button" className={styles.inlineTextButton} onClick={onReload}>
                  {t('common.retry')}
                </button>
              </div>
            )}

            <div className={styles.docsIndexGrid}>
              <div className={styles.indexMainColumn}>
                <section className={styles.panel}>
                  <div className={styles.panelHeader}>
                    <div>
                      <h2 className={styles.panelTitle}>{t('wiki.public.latestTitle')}</h2>
                      <p className={styles.panelHint}>{t('wiki.public.latestHint')}</p>
                    </div>
                    <span className={styles.panelStat}>{t('wiki.public.documentCount', { count: totalDocuments })}</span>
                  </div>

                  {listCards.length === 0 ? (
                    <PublicDocsStatusCard
                      tone="empty"
                      compact={true}
                      title={t('wiki.public.cardsEmptyTitle')}
                      description={t('wiki.public.cardsEmptyDescription')}
                    />
                  ) : (
                    <div className={styles.cardList}>
                      {listCards.map((document) => {
                        const href = buildPublicDocsPath({ kind: 'detail', slug: document.voSlug });

                        return (
                          <a
                            key={document.voId}
                            className={styles.docCard}
                            href={href}
                            onClick={(event) => handlePublicDocsLinkClick(event, () => onOpenDocument(document.voSlug))}
                          >
                            <div className={styles.docCardMeta}>
                              <span className={styles.metaChip}>{toVisibilityText(t, document.voVisibility)}</span>
                              <span className={styles.metaChip}>{toStatusText(t, document.voStatus)}</span>
                            </div>
                            <h3 className={styles.docCardTitle}>{document.voTitle}</h3>
                            <p className={styles.docCardSummary}>
                              {document.voSummary?.trim() || t('wiki.public.summaryFallback')}
                            </p>
                            <div className={styles.docCardFooter}>
                              <span>{formatDateTimeByTimeZone(document.voModifyTime || document.voCreateTime, displayTimeZone)}</span>
                              <span className={styles.docCardAction}>{t('wiki.public.openDocument')}</span>
                            </div>
                          </a>
                        );
                      })}
                    </div>
                  )}
                </section>

                <section className={`${styles.panel} ${styles.directoryPanel}`}>
                  <div className={styles.panelHeader}>
                    <div>
                      <h2 className={styles.panelTitle}>{t('wiki.public.directoryTitle')}</h2>
                      <p className={styles.panelHint}>{t('wiki.public.directoryHint')}</p>
                    </div>
                    <span className={styles.panelStat}>
                      {directoryExpanded
                        ? t('wiki.public.directoryCount', { count: treeRows.length })
                        : t('wiki.public.directoryPreviewCount', { visible: visibleTreeRows.length, total: treeRows.length })}
                    </span>
                  </div>

                  {treeRows.length === 0 ? (
                    <PublicDocsStatusCard
                      tone="empty"
                      compact={true}
                      title={t('wiki.public.directoryEmptyTitle')}
                      description={t('wiki.public.directoryEmptyDescription')}
                    />
                  ) : (
                    <>
                      <div className={styles.directoryList}>
                        {visibleTreeRows.map((row) => {
                          const href = buildPublicDocsPath({ kind: 'detail', slug: row.slug });

                          return (
                            <a
                              key={row.id}
                              className={styles.directoryItem}
                              href={href}
                              onClick={(event) => handlePublicDocsLinkClick(event, () => onOpenDocument(row.slug))}
                            >
                              <span className={styles.directoryPrefix} style={{ marginLeft: `${row.depth * 14}px` }}>
                                {row.depth > 0 ? '└' : '•'}
                              </span>
                              <span className={styles.directoryTitle}>{row.title}</span>
                              {row.childCount > 0 && (
                                <span className={styles.directoryMeta}>{t('wiki.public.childCount', { count: row.childCount })}</span>
                              )}
                            </a>
                          );
                        })}
                      </div>
                      {(isDirectoryTruncated || directoryExpanded) ? (
                        <div className={styles.directoryFooter}>
                          <button
                            type="button"
                            className={styles.inlineTextButton}
                            onClick={() => setDirectoryExpanded((current) => !current)}
                          >
                            {directoryExpanded
                              ? t('wiki.public.directoryCollapse')
                              : t('wiki.public.directoryExpand', { hidden: treeRows.length - visibleTreeRows.length })}
                          </button>
                          <span className={styles.directoryFooterHint}>{t('wiki.public.directorySearchHint')}</span>
                        </div>
                      ) : null}
                    </>
                  )}
                </section>
              </div>

              <PublicDocsListRail
                directoryCount={treeRows.length}
                totalDocuments={totalDocuments}
                canUseDocsAuthorTools={canUseDocsAuthorTools}
                authorHref={authorHref}
              />
            </div>
          </>
        )}
      </div>
    </section>
  );
};
