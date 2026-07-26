import { useCallback, useMemo, useState, type FormEvent, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Icon } from '@radish/ui/icon';
import { MarkdownEditor } from '@radish/ui/markdown-editor';
import { toast } from '@radish/ui/toast';
import type { MarkdownDocumentUploadResult, MarkdownImageUploadResult } from '@radish/ui';
import {
  WikiCollaboratorState,
  WikiDraftReviewState,
  type WikiAuthorDraftDetailVo,
} from '@radish/http';
import type { LongId } from '@/api/user';
import {
  collectDescendantIds,
  flattenTreeOptions,
  formatWikiTime,
  type EditorDraft,
  type ParentOption,
} from '@/apps/wiki/wikiApp.helpers';
import type { WikiDocumentTreeNodeVo } from '@/apps/wiki/types/wiki';
import { WebStateSlot } from '@/components/web-shell';
import { createMarkdownEditorLabels } from '@/i18n/markdownEditorLabels';
import { buildPublicDocsPath } from '@/public/docsRouteState';
import { log } from '@/utils/logger';
import { formatDocsAuthorNumber, getDocsAuthorSummaryPreview } from './docsAuthorPresentation';
import { buildDocsAuthorPath, type DocsAuthorRoute } from './docsAuthorRouteState';
import { shouldHandleAuthorLinkClick } from './useDocsAuthorNavigation';
import styles from './DocsAuthorApp.module.css';

export interface DocsAuthorEditorState {
  draft: EditorDraft;
  document: WikiAuthorDraftDetailVo | null;
  loading: boolean;
  submitting: boolean;
  error: string | null;
  sortSuggestion: string;
  conflict: DocsAuthorDraftConflictState | null;
}

interface DocsAuthorDraftConflictState {
  localMarkdownContent: string;
  serverDraftVersion: number | null;
  serverDocumentVersion: number | null;
}

interface DocsAuthorEditorPageProps {
  route: DocsAuthorRoute & ({ kind: 'compose' } | { kind: 'edit' });
  tree: WikiDocumentTreeNodeVo[];
  state: DocsAuthorEditorState;
  isEditorUploading: boolean;
  onBack: (event: MouseEvent<HTMLAnchorElement>) => void;
  onNavigate: (event: MouseEvent<HTMLAnchorElement>, route: DocsAuthorRoute) => void;
  onParentChange: (parentId: string) => void;
  onSetDraft: (updater: (current: EditorDraft) => EditorDraft) => void;
  onSave: (event: FormEvent<HTMLFormElement>) => void;
  onImageUpload: (file: File, reportProgress: (progress: number) => void) => Promise<MarkdownImageUploadResult>;
  onDocumentUpload: (file: File, reportProgress: (progress: number) => void) => Promise<MarkdownDocumentUploadResult>;
  onEditorUploadingChange: (uploading: boolean) => void;
  currentUserPublicId: string;
  onSubmitDraft: () => void;
  onWithdrawDraft: () => void;
  onInviteCollaborator: (publicId: string) => Promise<void>;
  onRemoveCollaborator: (collaboratorId: LongId) => Promise<void>;
  onRespondInvitation: (collaboratorId: LongId, accept: boolean) => Promise<void>;
  onCopyConflictContent: () => void;
  onDownloadConflictContent: () => void;
  onReloadServerDraft: () => void;
}

function buildParentOptions(tree: WikiDocumentTreeNodeVo[], documentId: LongId | null): ParentOption[] {
  if (!documentId) {
    return flattenTreeOptions(tree);
  }

  const descendants = collectDescendantIds(tree, documentId);
  return flattenTreeOptions(tree).filter((option) =>
    String(option.id) !== String(documentId) && !descendants.has(option.id)
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

function getCollaboratorStateText(state: number, t: TFunction): string {
  switch (state) {
    case WikiCollaboratorState.Accepted:
      return t('wiki.author.collaboration.state.accepted');
    case WikiCollaboratorState.Declined:
      return t('wiki.author.collaboration.state.declined');
    case WikiCollaboratorState.Revoked:
      return t('wiki.author.collaboration.state.revoked');
    default:
      return t('wiki.author.collaboration.state.pending');
  }
}

interface EditorRailMetricProps {
  label: string;
  value: number | string;
  language?: string;
}

function EditorRailMetric({ label, value, language }: EditorRailMetricProps) {
  return (
    <div className={styles.railMetric}>
      <span>{label}</span>
      <strong>{typeof value === 'number' ? formatDocsAuthorNumber(value, language) : value}</strong>
    </div>
  );
}

export function DocsAuthorEditorPage({
  route,
  tree,
  state,
  isEditorUploading,
  onBack,
  onNavigate,
  onParentChange,
  onSetDraft,
  onSave,
  onImageUpload,
  onDocumentUpload,
  onEditorUploadingChange,
  currentUserPublicId,
  onSubmitDraft,
  onWithdrawDraft,
  onInviteCollaborator,
  onRemoveCollaborator,
  onRespondInvitation,
  onCopyConflictContent,
  onDownloadConflictContent,
  onReloadServerDraft,
}: DocsAuthorEditorPageProps) {
  const { t, i18n } = useTranslation();
  const [invitePublicId, setInvitePublicId] = useState('');
  const [collaborationBusy, setCollaborationBusy] = useState(false);
  const markdownEditorLabels = useMemo(
    () => createMarkdownEditorLabels(t, i18n.resolvedLanguage ?? i18n.language),
    [i18n.language, i18n.resolvedLanguage, t],
  );
  const handleEditorUploadError = useCallback((kind: 'image' | 'document', error: unknown) => {
    log.error('DocsAuthorEditorPage', `Markdown ${kind} upload failed:`, error);
  }, []);
  const parentOptions = useMemo(
    () => buildParentOptions(tree, route.kind === 'edit' ? route.documentId : null),
    [route, tree]
  );
  const readOnly = route.kind === 'edit' && state.document?.voCanEdit !== true;
  const pageTitle = route.kind === 'compose' ? t('wiki.author.editor.createTitle') : state.document?.voTitle || t('wiki.author.editor.editTitle');
  const pageIntro = route.kind === 'compose'
    ? t('wiki.author.editor.createIntro')
    : t('wiki.author.editor.editIntro');
  const publicReadHref = state.document && state.document.voDocumentVersion > 0 && state.document.voSlug.trim()
    ? buildPublicDocsPath({ kind: 'detail', slug: state.document.voSlug })
    : null;
  const ownInvitation = state.document?.voCollaborators.find((collaborator) =>
    collaborator.voUserPublicId === currentUserPublicId
      && collaborator.voInviteState === WikiCollaboratorState.Pending
  ) ?? null;
  const reviewSubmitted = state.document?.voReviewState === WikiDraftReviewState.Submitted;
  const runCollaborationAction = async (action: () => Promise<void>) => {
    setCollaborationBusy(true);
    try {
      await action();
    } catch (error) {
      log.error('DocsAuthorEditorPage', '文档协作动作失败:', error);
      const message = error instanceof Error && error.message.trim()
        ? error.message
        : t('wiki.author.feedback.collaborationActionFailed');
      toast.error(message);
    } finally {
      setCollaborationBusy(false);
    }
  };
  const handleEditorSubmit = (event: FormEvent<HTMLFormElement>) => {
    if (isEditorUploading) {
      event.preventDefault();
      return;
    }

    onSave(event);
  };
  const preventNavigationWhileUploading = (event: MouseEvent<HTMLAnchorElement>) => {
    if (isEditorUploading && shouldHandleAuthorLinkClick(event)) {
      event.preventDefault();
    }
  };

  if (state.loading || state.error) {
    return (
      <section className={styles.statusPanel}>
        <WebStateSlot
          tone={state.loading ? 'loading' : 'error'}
          icon={state.loading ? 'mdi:progress-clock' : 'mdi:alert-circle-outline'}
          title={state.loading ? t('wiki.author.editor.loadingTitle') : t('wiki.author.editor.errorTitle')}
          description={state.loading ? t('wiki.author.editor.loadingDescription') : state.error ?? ''}
        />
      </section>
    );
  }

  return (
    <div className={styles.authorWorkspace}>
      <section className={styles.panel}>
        <div className={styles.panelHeader}>
          <div>
            <p className={styles.kicker}>Document Draft</p>
            <h1 className={styles.pageTitle}>{pageTitle}</h1>
            <p className={styles.pageIntro}>{pageIntro}</p>
          </div>
          <div className={styles.headerActions}>
            <a
              className={styles.secondaryButton}
              href={buildDocsAuthorPath({ kind: 'mine' })}
              onClick={(event) => {
                preventNavigationWhileUploading(event);
                if (!event.defaultPrevented) onBack(event);
              }}
              aria-disabled={isEditorUploading}
            >
              <Icon icon="mdi:arrow-left" size={18} />
              <span>{t('wiki.author.actions.backToList')}</span>
            </a>
            {route.kind === 'edit' ? (
              <a
                className={styles.secondaryButton}
                href={buildDocsAuthorPath({ kind: 'revisions', documentId: route.documentId })}
                onClick={(event) => {
                  preventNavigationWhileUploading(event);
                  if (!event.defaultPrevented) onNavigate(event, { kind: 'revisions', documentId: route.documentId });
                }}
                aria-disabled={isEditorUploading}
              >
                <Icon icon="mdi:history" size={18} />
                <span>{t('wiki.author.actions.revisions')}</span>
              </a>
            ) : null}
            {publicReadHref ? (
              <a className={styles.secondaryButton} href={publicReadHref} onClick={preventNavigationWhileUploading} aria-disabled={isEditorUploading}>
                <Icon icon="mdi:book-open-page-variant-outline" size={18} />
                <span>{t('wiki.author.actions.publicReading')}</span>
              </a>
            ) : null}
          </div>
        </div>

        {readOnly && state.document ? (
          <div className={styles.inlineNotice}>
            <Icon icon="mdi:lock-outline" size={20} />
            <span>{state.document.voReadOnlyReason || t('wiki.author.editor.authorReadOnlyNotice')}</span>
          </div>
        ) : null}

        <form className={styles.editorForm} onSubmit={handleEditorSubmit}>
          <div className={styles.formGrid}>
            <label className={styles.field}>
              <span>{t('wiki.author.form.title')}</span>
              <input className={styles.input} value={state.draft.title} disabled={readOnly || state.submitting} onChange={(event) => onSetDraft((current) => ({ ...current, title: event.target.value }))} placeholder={t('wiki.author.form.titlePlaceholder')} />
            </label>
            <label className={styles.field}>
              <span>Slug</span>
              <input className={styles.input} value={state.draft.slug} disabled={readOnly || state.submitting} onChange={(event) => onSetDraft((current) => ({ ...current, slug: event.target.value }))} placeholder={t('wiki.author.form.slugPlaceholder')} />
            </label>
            <label className={styles.field}>
              <span>{t('wiki.author.form.parent')}</span>
              <select className={styles.select} value={state.draft.parentId} disabled={readOnly || state.submitting} onChange={(event) => onParentChange(event.target.value)}>
                <option value="">{t('wiki.author.form.root')}</option>
                {parentOptions.map((option) => <option key={option.id} value={String(option.id)}>{option.label}</option>)}
              </select>
            </label>
            <label className={styles.field}>
              <span>{t('wiki.author.form.coverAttachmentId')}</span>
              <input className={styles.input} value={state.draft.coverAttachmentId} disabled={readOnly || state.submitting} onChange={(event) => onSetDraft((current) => ({ ...current, coverAttachmentId: event.target.value }))} placeholder={t('wiki.author.form.optional')} />
            </label>
            {route.kind === 'edit' ? (
              <label className={styles.field}>
                <span>{t('wiki.author.form.changeSummary')}</span>
                <input className={styles.input} value={state.draft.changeSummary} disabled={readOnly || state.submitting} onChange={(event) => onSetDraft((current) => ({ ...current, changeSummary: event.target.value }))} placeholder={t('wiki.author.form.changeSummaryPlaceholder')} />
              </label>
            ) : null}
            <label className={`${styles.field} ${styles.fieldFull}`}>
              <span>{t('wiki.author.form.summary')}</span>
              <textarea className={styles.textarea} value={state.draft.summary} disabled={readOnly || state.submitting} onChange={(event) => onSetDraft((current) => ({ ...current, summary: event.target.value }))} placeholder={t('wiki.author.form.summaryPlaceholder')} />
            </label>
          </div>

          <MarkdownEditor
            value={state.draft.markdownContent}
            onChange={(value) => onSetDraft((current) => ({ ...current, markdownContent: value }))}
            labels={markdownEditorLabels}
            minHeight={420}
            disabled={readOnly || state.submitting}
            placeholder={t('wiki.author.form.markdownPlaceholder')}
            onImageUpload={onImageUpload}
            onDocumentUpload={onDocumentUpload}
            onUploadError={handleEditorUploadError}
            onUploadingChange={onEditorUploadingChange}
          />

          {state.conflict ? (
            <section className={styles.conflictPanel} aria-live="assertive">
              <div>
                <strong>{t('wiki.author.conflict.title')}</strong>
                <p>{t('wiki.author.conflict.description', { draft: state.conflict.serverDraftVersion ?? '-', document: state.conflict.serverDocumentVersion ?? '-' })}</p>
              </div>
              <div className={styles.conflictActions}>
                <button type="button" className={styles.secondaryButton} onClick={onCopyConflictContent}>{t('wiki.author.conflict.copy')}</button>
                <button type="button" className={styles.secondaryButton} onClick={onDownloadConflictContent}>{t('wiki.author.conflict.download')}</button>
                <button type="button" className={styles.primaryButton} onClick={onReloadServerDraft}>{t('wiki.author.conflict.reload')}</button>
              </div>
            </section>
          ) : null}

          <div className={styles.editorActions}>
            <span className={styles.editorHint}>
              {state.document
                ? t('wiki.author.editor.versionEvidence', { document: state.document.voDocumentVersion, draft: state.document.voDraftVersion, role: state.document.voAuthorRole })
                : t('wiki.author.editor.newDraftEvidence')}
            </span>
            <button type="submit" className={styles.primaryButton} disabled={readOnly || state.submitting || isEditorUploading}>
              <Icon icon={state.submitting ? 'mdi:progress-clock' : 'mdi:content-save-outline'} size={18} />
              <span>{state.submitting ? t('wiki.author.actions.saving') : t('wiki.author.actions.save')}</span>
            </button>
            {state.document?.voCanSubmit ? (
              <button type="button" className={styles.primaryButton} disabled={state.submitting || isEditorUploading} onClick={onSubmitDraft}>
                <Icon icon="mdi:send-check-outline" size={18} />
                <span>{t('wiki.author.actions.submitReview')}</span>
              </button>
            ) : null}
            {reviewSubmitted && state.document?.voAuthorRole.toLowerCase() === 'owner' ? (
              <button type="button" className={styles.secondaryButton} disabled={state.submitting} onClick={onWithdrawDraft}>
                <Icon icon="mdi:undo-variant" size={18} />
                <span>{t('wiki.author.actions.withdrawReview')}</span>
              </button>
            ) : null}
          </div>
        </form>
      </section>

      <aside className={styles.authorRail} aria-label={t('wiki.author.editor.contextAriaLabel')}>
        <section className={styles.railCard}>
          <p className={styles.railKicker}>{t('wiki.author.editor.context')}</p>
          <div className={styles.railMetricGrid}>
            <EditorRailMetric label={t('wiki.author.editor.mode')} value={route.kind === 'compose' ? t('wiki.author.editor.modeCreate') : t('wiki.author.editor.modeEdit')} />
            <EditorRailMetric label={t('wiki.author.editor.documentVersion')} value={state.document?.voDocumentVersion ?? 0} language={i18n.resolvedLanguage} />
            <EditorRailMetric label={t('wiki.author.editor.draftVersion')} value={state.document?.voDraftVersion ?? 0} language={i18n.resolvedLanguage} />
          </div>
          <div className={styles.railChipList}>
            <span className={styles.railChip}>{state.document?.voAuthorRole || t('wiki.author.editor.modeCreate')}</span>
            <span className={styles.railChip}>{readOnly ? t('wiki.author.access.readOnly') : t('wiki.author.access.savable')}</span>
          </div>
        </section>

        {state.document ? (
          <section className={styles.railCard}>
            <p className={styles.railKicker}>{t('wiki.author.editor.evidence')}</p>
            <h2 className={styles.railTitle}>{state.document.voTitle}</h2>
            <p className={styles.railText}>{getDocsAuthorSummaryPreview(state.document.voSummary, t)}</p>
            <div className={styles.railChipList}>
              <span className={styles.railChip}>{getDraftReviewStateText(state.document.voReviewState, t)}</span>
              <span className={styles.railChip}>{t('wiki.author.document.documentVersion', { version: state.document.voDocumentVersion })}</span>
              <span className={styles.railChip}>{t('wiki.author.document.draftVersion', { version: state.document.voDraftVersion })}</span>
            </div>
            {state.document.voReviewComment ? <p className={styles.railText}>{state.document.voReviewComment}</p> : null}
          </section>
        ) : null}

        {state.document ? (
          <details className={styles.railCard} open>
            <summary className={styles.railKicker}>{t('wiki.author.collaboration.title')}</summary>
            <p className={styles.railText}>{t('wiki.author.collaboration.owner', { name: state.document.voOwnerUserName })}</p>
            <div className={styles.collaboratorList}>
              {state.document.voCollaborators.map((collaborator) => (
                <div key={collaborator.voId} className={styles.collaboratorRow}>
                  <div>
                    <strong>{collaborator.voUserName}</strong>
                    <span>{collaborator.voUserPublicId} · {getCollaboratorStateText(collaborator.voInviteState, t)}</span>
                  </div>
                  {state.document?.voCanManageCollaborators && collaborator.voInviteState !== WikiCollaboratorState.Revoked ? (
                    <button type="button" disabled={collaborationBusy} onClick={() => void runCollaborationAction(() => onRemoveCollaborator(collaborator.voId))}>{t('wiki.author.collaboration.remove')}</button>
                  ) : null}
                </div>
              ))}
            </div>
            {state.document.voCanManageCollaborators ? (
              <form className={styles.inviteForm} onSubmit={(event) => {
                event.preventDefault();
                const publicId = invitePublicId.trim();
                if (!publicId) return;
                void runCollaborationAction(async () => {
                  await onInviteCollaborator(publicId);
                  setInvitePublicId('');
                });
              }}>
                <input value={invitePublicId} onChange={(event) => setInvitePublicId(event.target.value)} placeholder={t('wiki.author.collaboration.publicIdPlaceholder')} />
                <button type="submit" disabled={collaborationBusy || !invitePublicId.trim()}>{t('wiki.author.collaboration.invite')}</button>
              </form>
            ) : null}
            {ownInvitation ? (
              <div className={styles.conflictActions}>
                <button type="button" disabled={collaborationBusy} onClick={() => void runCollaborationAction(() => onRespondInvitation(ownInvitation.voId, true))}>{t('wiki.author.collaboration.accept')}</button>
                <button type="button" disabled={collaborationBusy} onClick={() => void runCollaborationAction(() => onRespondInvitation(ownInvitation.voId, false))}>{t('wiki.author.collaboration.decline')}</button>
              </div>
            ) : null}
          </details>
        ) : null}

        {state.document?.voReviewEvents.length ? (
          <details className={styles.railCard}>
            <summary className={styles.railKicker}>{t('wiki.author.timeline.title')}</summary>
            <ol className={styles.timelineList}>
              {state.document.voReviewEvents.map((event) => (
                <li key={event.voId}>
                  <strong>{event.voAction}</strong>
                  <span>{event.voActorName} · {formatWikiTime(event.voCreateTime, i18n.resolvedLanguage)}</span>
                  {event.voComment ? <p>{event.voComment}</p> : null}
                </li>
              ))}
            </ol>
          </details>
        ) : null}

        <section className={styles.railCard}>
          <p className={styles.railKicker}>{t('wiki.author.editor.flowActions')}</p>
          <div className={styles.railActionList}>
            <a className={styles.railLink} href={buildDocsAuthorPath({ kind: 'mine' })} onClick={(event) => {
              preventNavigationWhileUploading(event);
              if (!event.defaultPrevented) onBack(event);
            }} aria-disabled={isEditorUploading}>
              <Icon icon="mdi:arrow-left" size={18} />
              <span>{t('wiki.author.actions.backToList')}</span>
            </a>
            {route.kind === 'edit' ? (
              <a className={styles.railLink} href={buildDocsAuthorPath({ kind: 'revisions', documentId: route.documentId })} onClick={(event) => {
                preventNavigationWhileUploading(event);
                if (!event.defaultPrevented) onNavigate(event, { kind: 'revisions', documentId: route.documentId });
              }} aria-disabled={isEditorUploading}>
                <Icon icon="mdi:history" size={18} />
                <span>{t('wiki.author.actions.revisions')}</span>
              </a>
            ) : null}
            {publicReadHref ? (
              <a className={styles.railLink} href={publicReadHref} onClick={preventNavigationWhileUploading} aria-disabled={isEditorUploading}>
                <Icon icon="mdi:book-open-page-variant-outline" size={18} />
                <span>{t('wiki.author.actions.publicReading')}</span>
              </a>
            ) : null}
          </div>
          <p className={styles.railText}>{t('wiki.author.editor.boundary')}</p>
        </section>
      </aside>
    </div>
  );
}
