import { useTranslation } from 'react-i18next';
import type { LongId } from '@/api/user';
import type { WikiDocumentTreeNodeVo, WikiDocumentVo } from './types/wiki';
import { WikiDocumentStatus } from './types/wiki';
import styles from './WikiApp.module.css';

export type DeletionFilter = 'active' | 'deleted';
export type SidebarView = 'tree' | 'results';
export type SidebarRenderMode = 'default' | 'overlay';

interface WikiSidebarProps {
  renderMode?: SidebarRenderMode;
  tree: WikiDocumentTreeNodeVo[];
  documents: WikiDocumentVo[];
  selectedDocumentId: LongId | null;
  expandedNodeIds: Set<string>;
  sidebarView: SidebarView;
  keyword: string;
  statusFilter: string;
  deletionFilter: DeletionFilter;
  loadingTree: boolean;
  loadingList: boolean;
  loggedIn: boolean;
  isAdmin: boolean;
  submitting: boolean;
  showingDeleted: boolean;
  totalTreeDocuments: number;
  totalResultsCount: number;
  currentListCount: number;
  totalResultsText: string;
  hasActiveFilters: boolean;
  getStatusText: (status: number) => string;
  getStatusClassName: (status: number) => string;
  getVisibilityText: (visibility?: number) => string;
  onToggleTreeNode: (nodeId: LongId) => void;
  onExpandTreeNode: (nodeId: LongId) => void;
  onSelectDocument: (documentId: LongId) => void;
  onSidebarViewChange: (view: SidebarView) => void;
  onKeywordChange: (keyword: string) => void;
  onStatusFilterChange: (status: string) => void;
  onDeletionFilterChange: (filter: DeletionFilter) => void;
  onSearch: () => Promise<void>;
  onRefresh: () => Promise<void>;
  onCreate: () => void;
  onImport: () => void;
}

export const WikiSidebar = ({
  renderMode = 'default',
  tree,
  documents,
  selectedDocumentId,
  expandedNodeIds,
  sidebarView,
  keyword,
  statusFilter,
  deletionFilter,
  loadingTree,
  loadingList,
  loggedIn,
  isAdmin,
  submitting,
  showingDeleted,
  totalTreeDocuments,
  totalResultsCount,
  currentListCount,
  totalResultsText,
  hasActiveFilters,
  getStatusText,
  getStatusClassName,
  getVisibilityText,
  onToggleTreeNode,
  onExpandTreeNode,
  onSelectDocument,
  onSidebarViewChange,
  onKeywordChange,
  onStatusFilterChange,
  onDeletionFilterChange,
  onSearch,
  onRefresh,
  onCreate,
  onImport,
}: WikiSidebarProps) => {
  const { t } = useTranslation();
  const isOverlayMode = renderMode === 'overlay';

  const renderTreeNodes = (nodes: WikiDocumentTreeNodeVo[], depth: number = 0) => {
    return nodes.map((node) => {
      const children = node.voChildren || [];
      const isExpandable = children.length > 0;
      const isExpanded = expandedNodeIds.has(String(node.voId));

      return (
        <div key={node.voId} className={styles.treeNodeGroup}>
          <div
            className={styles.treeNodeRow}
            style={{
              marginLeft: `${depth * 18}px`,
              width: `calc(100% - ${depth * 18}px)`,
            }}
          >
            {isExpandable ? (
              <button
                type="button"
                className={styles.treeToggleButton}
                onClick={() => onToggleTreeNode(node.voId)}
                aria-label={isExpanded
                  ? t('wiki.tree.collapseNode', { title: node.voTitle })
                  : t('wiki.tree.expandNode', { title: node.voTitle })}
                aria-expanded={isExpanded}
              >
                <span className={`${styles.treeToggleIcon} ${isExpanded ? styles.treeToggleIconExpanded : ''}`}>▶</span>
              </button>
            ) : (
              <span className={styles.treeToggleSpacer} aria-hidden="true" />
            )}

            <button
              type="button"
              className={`${styles.treeNode} ${String(selectedDocumentId) === String(node.voId) ? styles.treeNodeActive : ''}`}
              onClick={() => {
                onSelectDocument(node.voId);
                if (isExpandable) {
                  onExpandTreeNode(node.voId);
                }
              }}
            >
              <span className={styles.treeNodeDepth} />
              <span className={styles.treeNodeTitle}>{node.voTitle}</span>
              {isExpandable ? <span className={styles.treeNodeMeta}>{children.length}</span> : null}
              <span className={`${styles.statusChip} ${getStatusClassName(node.voStatus)}`}>{getStatusText(node.voStatus)}</span>
            </button>
          </div>

          {isExpandable && isExpanded ? renderTreeNodes(children, depth + 1) : null}
        </div>
      );
    });
  };

  const renderSidebarPanel = (view: SidebarView, compact: boolean = false) => (
    <section className={`${styles.sidebarPanel} ${compact ? styles.sidebarPanelCompact : ''}`}>
      <div className={styles.sectionHeader}>
        <h3 className={styles.sectionTitle}>
          {view === 'tree'
            ? t('wiki.sidebar.tab.tree')
            : showingDeleted
              ? t('wiki.sidebar.tab.deletedResults')
              : t('wiki.sidebar.tab.results')}
        </h3>
        <span className={styles.sectionCount}>{view === 'tree' ? totalTreeDocuments : totalResultsCount}</span>
      </div>
      <div className={styles.sectionHint}>
        {view === 'results'
          ? totalResultsCount > currentListCount
            ? t('wiki.sidebar.sectionHint.pageStats', { current: currentListCount, total: totalResultsCount })
            : hasActiveFilters
              ? t('wiki.sidebar.sectionHint.filtered', { total: totalResultsCount })
              : t('wiki.sidebar.sectionHint.list', { total: totalResultsCount })
          : t('wiki.sidebar.sectionHint.tree')}
      </div>

      {view === 'tree' ? (
        <div className={styles.treeScroll}>
          {loadingTree ? (
            <div className={styles.loadingText}>{t('wiki.loading.tree')}</div>
          ) : tree.length > 0 ? (
            renderTreeNodes(tree)
          ) : (
            <div className={styles.mutedText}>{loggedIn ? t('wiki.empty.tree') : t('wiki.empty.publicTree')}</div>
          )}
        </div>
      ) : (
        <div className={styles.listScroll}>
          {loadingList ? (
            <div className={styles.loadingText}>{t('wiki.loading.list')}</div>
          ) : documents.length > 0 ? (
            documents.map((document) => (
              <button
                key={document.voId}
                type="button"
                className={`${styles.listItem} ${String(selectedDocumentId) === String(document.voId) ? styles.listItemActive : ''}`}
                onClick={() => onSelectDocument(document.voId)}
              >
                <div className={styles.listItemHeader}>
                  <span className={styles.listItemTitle}>{document.voTitle}</span>
                  <span className={`${styles.statusChip} ${document.voIsDeleted ? styles.statusDeleted : getStatusClassName(document.voStatus)}`}>
                    {document.voIsDeleted ? t('wiki.status.deleted') : getStatusText(document.voStatus)}
                  </span>
                </div>
                {document.voSummary ? (
                  <div className={styles.listItemSummary}>{document.voSummary}</div>
                ) : null}
                <div className={styles.listItemMeta}>
                  {document.voSlug} · v{document.voVersion} · {getVisibilityText(document.voVisibility)}
                </div>
              </button>
            ))
          ) : (
            <div className={styles.mutedText}>
              {showingDeleted
                ? t('wiki.empty.deletedResults')
                : loggedIn
                  ? t('wiki.empty.results')
                  : t('wiki.empty.publicResults')}
            </div>
          )}
        </div>
      )}
    </section>
  );

  return (
    <>
      <div className={`${styles.sidebarHeader} ${isOverlayMode ? styles.sidebarHeaderOverlay : ''}`}>
        {!isOverlayMode ? (
          <div className={styles.sidebarTitleRow}>
            <div>
              <h2 className={styles.sidebarTitle}>{t('wiki.sidebar.title')}</h2>
              <p className={styles.sidebarHint}>{t('wiki.sidebar.hint')}</p>
            </div>
          </div>
        ) : null}

        <div className={`${styles.sidebarUtilityRow} ${isOverlayMode ? styles.sidebarUtilityRowOverlay : ''}`}>
          <div className={styles.sidebarStatsRow}>
            <span className={styles.sidebarStatBadge}>
              <span className={styles.sidebarStatLabel}>{t('wiki.sidebar.stats.directory')}</span>
              <strong className={styles.sidebarStatValue}>{totalTreeDocuments}</strong>
            </span>
            <span className={styles.sidebarStatBadge}>
              <span className={styles.sidebarStatLabel}>
                {showingDeleted ? t('wiki.sidebar.stats.recycleBinTotal') : t('wiki.sidebar.stats.resultsTotal')}
              </span>
              <strong className={styles.sidebarStatValue}>{totalResultsText}</strong>
            </span>
          </div>

          <div className={styles.toolbarRow}>
            <button
              type="button"
              className={styles.secondaryButton}
              onClick={() => void onRefresh()}
              disabled={loadingTree || loadingList}
            >
              {t('wiki.actions.refresh')}
            </button>
            {isAdmin ? (
              <>
                <button type="button" className={styles.primaryButton} onClick={onCreate}>
                  {t('wiki.actions.create')}
                </button>
                <button type="button" className={styles.ghostButton} onClick={onImport} disabled={submitting}>
                  {t('wiki.actions.import')}
                </button>
              </>
            ) : null}
          </div>
        </div>

        <div className={styles.sidebarFilters}>
          <div className={styles.searchRow}>
            <input
              className={styles.searchInput}
              value={keyword}
              placeholder={t('wiki.sidebar.searchPlaceholder')}
              onChange={(event) => onKeywordChange(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter') {
                  void onSearch();
                }
              }}
            />
            <button type="button" className={styles.primaryButton} onClick={() => void onSearch()}>
              {t('wiki.actions.search')}
            </button>
          </div>

          {isAdmin ? (
            <div className={styles.statusStack}>
              <div className={styles.statusRow}>
                <select
                  className={styles.select}
                  value={statusFilter}
                  onChange={(event) => onStatusFilterChange(event.target.value)}
                >
                  <option value="">{t('wiki.filter.allStatuses')}</option>
                  <option value={String(WikiDocumentStatus.Draft)}>{t('wiki.status.draft')}</option>
                  <option value={String(WikiDocumentStatus.Published)}>{t('wiki.status.published')}</option>
                  <option value={String(WikiDocumentStatus.Archived)}>{t('wiki.status.archived')}</option>
                </select>
              </div>
              <div className={styles.statusRow}>
                <select
                  className={styles.select}
                  value={deletionFilter}
                  onChange={(event) => onDeletionFilterChange(event.target.value as DeletionFilter)}
                >
                  <option value="active">{t('wiki.filter.activeOnly')}</option>
                  <option value="deleted">{t('wiki.filter.deletedOnly')}</option>
                </select>
              </div>
            </div>
          ) : null}
        </div>
      </div>

      <div className={`${styles.sidebarBody} ${isOverlayMode ? styles.sidebarBodyOverlay : ''}`}>
        {isOverlayMode ? (
          <div className={styles.sidebarSplit}>
            {renderSidebarPanel('tree', true)}
            {renderSidebarPanel('results', true)}
          </div>
        ) : (
          <>
            <div className={styles.sidebarTabs}>
              <button
                type="button"
                className={sidebarView === 'tree' ? styles.sidebarTabActive : styles.sidebarTab}
                onClick={() => onSidebarViewChange('tree')}
              >
                {t('wiki.sidebar.tab.tree')}
              </button>
              <button
                type="button"
                className={sidebarView === 'results' ? styles.sidebarTabActive : styles.sidebarTab}
                onClick={() => onSidebarViewChange('results')}
              >
                {showingDeleted ? t('wiki.sidebar.tab.deletedResults') : t('wiki.sidebar.tab.results')}
              </button>
            </div>

            {renderSidebarPanel(sidebarView)}
          </>
        )}
      </div>
    </>
  );
};
