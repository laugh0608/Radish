import { useEffect, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Icon } from '@radish/ui/icon';
import { WebStateSlot } from '@/components/web-shell';
import {
  WikiDraftReviewState,
  type WikiAuthorDocumentScope,
  type WikiAuthorDocumentVo,
  type WikiAuthorDraftStage,
  type WikiAuthorListQuery,
} from '@radish/http';
import type { LongId } from '@/api/user';
import { formatWikiTime } from '@/apps/wiki/wikiApp.helpers';
import type { WikiDocumentTreeNodeVo } from '@/apps/wiki/types/wiki';
import { buildPublicDocsPath } from '@/public/docsRouteState';
import {
  formatDocsAuthorNumber,
  getDocsAuthorSummaryPreview,
  resolveDocsAuthorPublicReadSlug,
} from './docsAuthorPresentation';
import {
  countCollaboratingDocsAuthorDocuments,
  countOwnedDocsAuthorDocuments,
  getDocsAuthorRoleText,
  pickDocsAuthorPreviewDocument,
} from './docsAuthorEditorPresentation';
import { buildDocsAuthorPath, type DocsAuthorRoute } from './docsAuthorRouteState';
import styles from './DocsAuthorApp.module.css';

export interface CollectionState {
  tree: WikiDocumentTreeNodeVo[];
  documents: WikiAuthorDocumentVo[];
  totalDocuments: number;
  page: number;
  pageSize: number;
  pageCount: number;
  scope: WikiAuthorDocumentScope;
  draftStage: WikiAuthorDraftStage;
  loading: boolean;
  error: string | null;
}

interface DocsMinePageProps {
  state: CollectionState;
  language?: string;
  onReload: () => void;
  onQueryChange: (query: WikiAuthorListQuery) => void;
  onNavigate: (event: MouseEvent<HTMLAnchorElement>, route: DocsAuthorRoute) => void;
  onStartDraft: (documentId: LongId) => void;
}

export function DocsMinePage({
  state,
  language,
  onReload,
  onQueryChange,
  onNavigate,
  onStartDraft,
}: DocsMinePageProps) {
  const { t } = useTranslation();
  const hasDocuments = state.documents.length > 0;
  const [selectedDocumentId, setSelectedDocumentId] = useState<LongId | null>(null);
  const previewDocument = state.documents.find(
    (document) => String(document.voDocumentId) === String(selectedDocumentId),
  ) ?? pickDocsAuthorPreviewDocument(state.documents);
  const ownedCount = countOwnedDocsAuthorDocuments(state.documents);
  const collaboratingCount = countCollaboratingDocsAuthorDocuments(state.documents);
  const submittedCount = state.documents.filter(
    (document) => document.voReviewState === WikiDraftReviewState.Submitted,
  ).length;
  const previewPublicReadSlug = previewDocument
    ? resolveDocsAuthorPublicReadSlug({
        status: previewDocument.voStatus,
        documentVersion: previewDocument.voDocumentVersion,
        documentSlug: previewDocument.voDocumentSlug,
      })
    : null;

  useEffect(() => {
    if (!previewDocument) {
      setSelectedDocumentId(null);
      return;
    }

    if (String(previewDocument.voDocumentId) !== String(selectedDocumentId)) {
      setSelectedDocumentId(previewDocument.voDocumentId);
    }
  }, [previewDocument, selectedDocumentId]);

  const updateQuery = (next: Partial<Pick<WikiAuthorListQuery, 'scope' | 'draftStage' | 'pageIndex'>>) => {
    onQueryChange({
      scope: next.scope ?? state.scope,
      draftStage: next.draftStage ?? state.draftStage,
      pageIndex: next.pageIndex ?? 1,
      pageSize: state.pageSize,
    });
  };

  return (
    <div className={styles.authorWorkspace}>
      <section className={styles.panel}>
        <div className={styles.panelHeader}>
          <div>
            <p className={styles.kicker}>Author Workspace</p>
            <h1 className={styles.pageTitle}>{t('wiki.author.title')}</h1>
            <p className={styles.pageIntro}>{t('wiki.author.mine.intro')}</p>
          </div>
          <div className={styles.headerActions}>
            <a
              className={styles.primaryButton}
              href={buildDocsAuthorPath({ kind: 'compose' })}
              onClick={(event) => onNavigate(event, { kind: 'compose' })}
            >
              <Icon icon="mdi:plus" size={18} />
              <span>{t('wiki.author.actions.create')}</span>
            </a>
            <button type="button" className={styles.secondaryButton} onClick={onReload} disabled={state.loading}>
              <Icon icon={state.loading ? 'mdi:progress-clock' : 'mdi:refresh'} size={18} />
              <span>{state.loading ? t('wiki.author.actions.refreshing') : t('wiki.author.actions.refresh')}</span>
            </button>
          </div>
        </div>

        <div className={styles.authorListToolbar} aria-label={t('wiki.author.mine.filtersAriaLabel')}>
          <label className={styles.authorListFilter}>
            <span>{t('wiki.author.mine.scopeLabel')}</span>
            <select
              value={state.scope}
              onChange={(event) => updateQuery({ scope: event.target.value as WikiAuthorDocumentScope })}
              disabled={state.loading}
            >
              <option value="all">{t('wiki.author.mine.scope.all')}</option>
              <option value="owned">{t('wiki.author.mine.scope.owned')}</option>
              <option value="collaborating">{t('wiki.author.mine.scope.collaborating')}</option>
            </select>
          </label>
          <label className={styles.authorListFilter}>
            <span>{t('wiki.author.mine.draftStageLabel')}</span>
            <select
              value={state.draftStage}
              onChange={(event) => updateQuery({ draftStage: event.target.value as WikiAuthorDraftStage })}
              disabled={state.loading}
            >
              <option value="all">{t('wiki.author.mine.draftStage.all')}</option>
              <option value="editable">{t('wiki.author.mine.draftStage.editable')}</option>
              <option value="submitted">{t('wiki.author.mine.draftStage.submitted')}</option>
              <option value="terminal">{t('wiki.author.mine.draftStage.terminal')}</option>
              <option value="none">{t('wiki.author.mine.draftStage.none')}</option>
            </select>
          </label>
          <span className={styles.authorListResultCount}>
            {t('wiki.author.mine.resultCount', { count: state.totalDocuments })}
          </span>
        </div>

        {state.error && hasDocuments ? (
          <div className={`${styles.revisionInlineNotice} ${styles.authorListNotice}`} role="status">
            <strong>{t('wiki.author.mine.staleTitle')}</strong>
            <span>{state.error}</span>
            <button type="button" onClick={onReload} disabled={state.loading}>{t('common.retry')}</button>
          </div>
        ) : null}

        {state.error && !hasDocuments ? (
          <MineStatusPanel
            icon="mdi:alert-circle-outline"
            title={t('wiki.author.mine.errorTitle')}
            description={state.error}
            actionLabel={t('common.retry')}
            onAction={onReload}
          />
        ) : state.loading && !hasDocuments ? (
          <MineStatusPanel
            icon="mdi:progress-clock"
            title={t('wiki.author.mine.loadingTitle')}
            description={t('wiki.author.mine.loadingDescription')}
          />
        ) : !hasDocuments ? (
          <MineStatusPanel
            icon="mdi:file-document-outline"
            title={t('wiki.author.mine.emptyTitle')}
            description={t('wiki.author.mine.emptyDescription')}
            actionHref={buildDocsAuthorPath({ kind: 'compose' })}
            actionLabel={t('wiki.author.actions.create')}
          />
        ) : (
          <div className={styles.documentList}>
            {state.documents.map((document) => (
              <DocumentRow
                key={document.voDocumentId}
                document={document}
                language={language}
                selected={String(document.voDocumentId) === String(previewDocument?.voDocumentId)}
                onSelect={() => setSelectedDocumentId(document.voDocumentId)}
                onNavigate={onNavigate}
                onStartDraft={onStartDraft}
              />
            ))}
          </div>
        )}

        {state.pageCount > 1 ? (
          <nav className={styles.authorPagination} aria-label={t('wiki.author.mine.paginationAriaLabel')}>
            <button
              type="button"
              className={styles.secondaryButton}
              disabled={state.loading || state.page <= 1}
              onClick={() => updateQuery({ pageIndex: state.page - 1 })}
            >
              {t('wiki.author.mine.previousPage')}
            </button>
            <span>{t('wiki.author.mine.pageStatus', { page: state.page, pageCount: state.pageCount })}</span>
            <button
              type="button"
              className={styles.secondaryButton}
              disabled={state.loading || state.page >= state.pageCount}
              onClick={() => updateQuery({ pageIndex: state.page + 1 })}
            >
              {t('wiki.author.mine.nextPage')}
            </button>
          </nav>
        ) : null}
      </section>

      <aside className={`${styles.authorRail} ${styles.mineAuthorRail}`} aria-label={t('wiki.author.mine.contextAriaLabel')}>
        <section className={styles.railCard}>
          <p className={styles.railKicker}>{t('wiki.author.rail.library')}</p>
          <div className={styles.railMetricGrid}>
            <MineRailMetric label={t('wiki.author.rail.owned')} value={ownedCount} language={language} />
            <MineRailMetric label={t('wiki.author.rail.collaborating')} value={collaboratingCount} language={language} />
            <MineRailMetric label={t('wiki.author.rail.submitted')} value={submittedCount} language={language} />
          </div>
          <p className={styles.railText}>{t('wiki.author.rail.libraryDescription')}</p>
        </section>

        <section className={styles.railCard}>
          <p className={styles.railKicker}>{t('wiki.author.rail.preview')}</p>
          {previewDocument ? (
            <>
              <h2 className={styles.railTitle}>{previewDocument.voTitle}</h2>
              <p className={styles.railText}>{getDocsAuthorSummaryPreview(previewDocument.voSummary, t)}</p>
              <div className={styles.railChipList}>
                <span className={styles.railChip}>{getDocsAuthorRoleText(previewDocument.voAuthorRole, t)}</span>
                <span className={styles.railChip}>{getDraftReviewStateText(previewDocument.voReviewState, t)}</span>
                <span className={styles.railChip}>{t('wiki.author.document.versionPair', { document: previewDocument.voDocumentVersion, draft: previewDocument.voDraftVersion ?? '-' })}</span>
              </div>
              <div className={styles.railActionList}>
                {previewDocument.voLatestDraftId ? (
                  <a
                    className={styles.railLink}
                    href={buildDocsAuthorPath({ kind: 'edit', documentId: previewDocument.voDocumentId })}
                    onClick={(event) => onNavigate(event, { kind: 'edit', documentId: previewDocument.voDocumentId })}
                  >
                    <Icon icon={previewDocument.voCanEdit ? 'mdi:pencil-outline' : 'mdi:file-eye-outline'} size={18} />
                    <span>{t(previewDocument.voCanEdit ? 'wiki.author.actions.edit' : 'wiki.author.editor.evidence')}</span>
                  </a>
                ) : null}
                {previewDocument.voCanStartDraft ? (
                  <button type="button" className={styles.railLink} onClick={() => onStartDraft(previewDocument.voDocumentId)}>
                    <Icon icon="mdi:file-plus-outline" size={18} />
                    <span>{t('wiki.author.actions.startDraft')}</span>
                  </button>
                ) : null}
                <a
                  className={styles.railLink}
                  href={buildDocsAuthorPath({ kind: 'revisions', documentId: previewDocument.voDocumentId })}
                  onClick={(event) => onNavigate(event, { kind: 'revisions', documentId: previewDocument.voDocumentId })}
                >
                  <Icon icon="mdi:history" size={18} />
                  <span>{t('wiki.author.actions.revisions')}</span>
                </a>
                {previewPublicReadSlug ? (
                  <a className={styles.railLink} href={buildPublicDocsPath({ kind: 'detail', slug: previewPublicReadSlug })}>
                    <Icon icon="mdi:book-open-page-variant-outline" size={18} />
                    <span>{t('wiki.author.actions.publicReading')}</span>
                  </a>
                ) : null}
              </div>
            </>
          ) : (
            <p className={styles.railText}>{t('wiki.author.rail.previewEmpty')}</p>
          )}
        </section>

        <section className={styles.railCard}>
          <p className={styles.railKicker}>{t('wiki.author.rail.boundary')}</p>
          <ul className={styles.railRuleList}>
            <li>{t('wiki.author.rail.boundaryAuthor')}</li>
            <li>{t('wiki.author.rail.boundaryPublic')}</li>
            <li>{t('wiki.author.rail.boundaryGovernance')}</li>
          </ul>
        </section>
      </aside>
    </div>
  );
}

interface MineStatusPanelProps {
  icon: string;
  title: string;
  description: string;
  actionHref?: string;
  actionLabel?: string;
  onAction?: () => void;
}

function MineStatusPanel({ icon, title, description, actionHref, actionLabel, onAction }: MineStatusPanelProps) {
  return (
    <section className={styles.statusPanel}>
      <WebStateSlot
        tone={icon === 'mdi:progress-clock' ? 'loading' : icon === 'mdi:alert-circle-outline' ? 'error' : 'empty'}
        icon={icon}
        title={title}
        description={description}
        actions={actionLabel ? [{ label: actionLabel, href: actionHref, onClick: onAction }] : undefined}
      />
    </section>
  );
}

interface MineRailMetricProps {
  label: string;
  value: number;
  language?: string;
}

function MineRailMetric({ label, value, language }: MineRailMetricProps) {
  return (
    <div className={styles.railMetric}>
      <span>{label}</span>
      <strong>{formatDocsAuthorNumber(value, language)}</strong>
    </div>
  );
}

function getDraftReviewStateText(state: number | null | undefined, t: TFunction): string {
  switch (state) {
    case WikiDraftReviewState.Submitted:
      return t('wiki.author.reviewState.submitted');
    case WikiDraftReviewState.ChangesRequested:
      return t('wiki.author.reviewState.changesRequested');
    case WikiDraftReviewState.Applied:
      return t('wiki.author.reviewState.applied');
    case WikiDraftReviewState.Rejected:
      return t('wiki.author.reviewState.rejected');
    case WikiDraftReviewState.Withdrawn:
      return t('wiki.author.reviewState.withdrawn');
    default:
      return t('wiki.author.reviewState.editing');
  }
}

interface DocumentRowProps {
  document: WikiAuthorDocumentVo;
  language?: string;
  selected: boolean;
  onSelect: () => void;
  onNavigate: (event: MouseEvent<HTMLAnchorElement>, route: DocsAuthorRoute) => void;
  onStartDraft: (documentId: LongId) => void;
}

function DocumentRow({ document, language, selected, onSelect, onNavigate, onStartDraft }: DocumentRowProps) {
  const { t } = useTranslation();
  const editRoute: DocsAuthorRoute = { kind: 'edit', documentId: document.voDocumentId };
  const revisionsRoute: DocsAuthorRoute = { kind: 'revisions', documentId: document.voDocumentId };
  const publicReadSlug = resolveDocsAuthorPublicReadSlug({
    status: document.voStatus,
    documentVersion: document.voDocumentVersion,
    documentSlug: document.voDocumentSlug,
  });
  const publicHref = publicReadSlug
    ? buildPublicDocsPath({ kind: 'detail', slug: publicReadSlug })
    : null;
  const isPendingInvitee = document.voAuthorRole.toLowerCase() === 'invitee';

  return (
    <article className={selected ? styles.documentRowSelected : styles.documentRow}>
      <div className={styles.documentMain}>
        <div className={styles.metaRow}>
          <span className={styles.statusChip}>{getDraftReviewStateText(document.voReviewState, t)}</span>
          <span className={styles.metaChip}>{getDocsAuthorRoleText(document.voAuthorRole, t)}</span>
          <span className={styles.metaChip}>{t('wiki.author.document.documentVersion', { version: document.voDocumentVersion })}</span>
          <span className={styles.metaChip}>{t('wiki.author.document.draftVersion', { version: document.voDraftVersion ?? '-' })}</span>
        </div>
        <h2 className={styles.documentTitle}>{document.voTitle}</h2>
        <p className={styles.documentSummary}>
          {document.voSummary?.trim() || t('wiki.author.summaryFallback')}
        </p>
        <div className={styles.documentMeta}>
          <span>slug: {document.voSlug}</span>
          <span>{t('wiki.author.document.updated', { time: formatWikiTime(document.voModifyTime || document.voCreateTime, language) })}</span>
        </div>
      </div>
      <div className={styles.documentActions}>
        <button
          type="button"
          className={styles.secondaryButton}
          aria-pressed={selected}
          onClick={onSelect}
        >
          {t('wiki.author.mine.selectContext')}
        </button>
        {document.voLatestDraftId ? (
          <a
            className={document.voCanStartDraft ? styles.secondaryButton : styles.primaryButton}
            href={buildDocsAuthorPath(editRoute)}
            onClick={(event) => onNavigate(event, editRoute)}
          >
            {t(isPendingInvitee
              ? 'wiki.author.actions.respondInvitation'
              : document.voCanEdit
                ? 'wiki.author.actions.edit'
                : 'wiki.author.editor.evidence')}
          </a>
        ) : null}
        {document.voCanStartDraft ? (
          <button type="button" className={styles.primaryButton} onClick={() => onStartDraft(document.voDocumentId)}>
            {t('wiki.author.actions.startDraft')}
          </button>
        ) : !document.voLatestDraftId ? (
          <span className={styles.readOnlyButton}>{t('wiki.author.access.readOnly')}</span>
        ) : null}
        <a
          className={styles.secondaryButton}
          href={buildDocsAuthorPath(revisionsRoute)}
          onClick={(event) => onNavigate(event, revisionsRoute)}
        >
          {t('wiki.author.actions.revisions')}
        </a>
        {publicHref ? (
          <a className={styles.secondaryButton} href={publicHref}>
            {t('wiki.author.actions.read')}
          </a>
        ) : null}
      </div>
    </article>
  );
}
