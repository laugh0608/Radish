import {
  useCallback,
  useEffect,
  useId,
  useMemo,
  useState,
  type FormEvent,
  type MouseEvent,
} from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { BottomSheet } from '@radish/ui/bottom-sheet';
import { Icon } from '@radish/ui/icon';
import { MarkdownEditor } from '@radish/ui/markdown-editor';
import {
  WikiCollaboratorState,
  WikiDraftReviewState,
  type WikiAuthorDraftDetailVo,
} from '@radish/http';
import type { LongId } from '@/api/user';
import {
  collectDescendantIds,
  flattenTreeOptions,
  type EditorDraft,
  type ParentOption,
} from '@/apps/wiki/wikiApp.helpers';
import type { WikiDocumentTreeNodeVo } from '@/apps/wiki/types/wiki';
import { WebStateSlot } from '@/components/web-shell';
import { createMarkdownEditorLabels } from '@/i18n/markdownEditorLabels';
import { buildPublicDocsPath } from '@/public/docsRouteState';
import { log } from '@/utils/logger';
import {
  countDocsAuthorMarkdownCharacters,
  getDocsAuthorInitial,
  getDocsAuthorOutline,
} from './docsAuthorEditorPresentation';
import { DocsAuthorEditorContext } from './DocsAuthorEditorContext';
import { resolveDocsAuthorPublicReadSlug } from './docsAuthorPresentation';
import { buildDocsAuthorPath, type DocsAuthorRoute } from './docsAuthorRouteState';
import type {
  MarkdownDocumentUploadResult,
  MarkdownImageUploadResult,
  ProtectedMarkdownAttachmentOptions,
} from '@radish/ui';
import styles from './DocsAuthorEditorPage.module.css';

const MOBILE_AUTHOR_QUERY = '(max-width: 760px)';

export interface DocsAuthorEditorState {
  draft: EditorDraft;
  baselineDraft: EditorDraft;
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
  isDirty: boolean;
  isEditorUploading: boolean;
  onBack: (event: MouseEvent<HTMLAnchorElement>) => void;
  onNavigate: (event: MouseEvent<HTMLAnchorElement>, route: DocsAuthorRoute) => void;
  onExternalNavigate: (event: MouseEvent<HTMLAnchorElement>) => void;
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
  protectedAttachments: ProtectedMarkdownAttachmentOptions;
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

function useMobileAuthorLayout(): boolean {
  const [isMobile, setIsMobile] = useState(
    typeof window !== 'undefined' && window.matchMedia(MOBILE_AUTHOR_QUERY).matches,
  );

  useEffect(() => {
    const media = window.matchMedia(MOBILE_AUTHOR_QUERY);
    const handleChange = (event: MediaQueryListEvent) => setIsMobile(event.matches);
    setIsMobile(media.matches);
    media.addEventListener('change', handleChange);
    return () => media.removeEventListener('change', handleChange);
  }, []);

  return isMobile;
}

export function DocsAuthorEditorPage({
  route,
  tree,
  state,
  isDirty,
  isEditorUploading,
  onBack,
  onNavigate,
  onExternalNavigate,
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
  protectedAttachments,
}: DocsAuthorEditorPageProps) {
  const { t, i18n } = useTranslation();
  const editorFormId = useId();
  const isMobile = useMobileAuthorLayout();
  const [mobileContextOpen, setMobileContextOpen] = useState(false);
  const markdownEditorLabels = useMemo(
    () => createMarkdownEditorLabels(t, i18n.resolvedLanguage ?? i18n.language),
    [i18n.language, i18n.resolvedLanguage, t],
  );
  const parentOptions = useMemo(
    () => buildParentOptions(tree, route.kind === 'edit' ? route.documentId : null),
    [route, tree],
  );
  const outline = useMemo(
    () => getDocsAuthorOutline(state.draft.markdownContent),
    [state.draft.markdownContent],
  );
  const readOnly = route.kind === 'edit' && state.document?.voCanEdit !== true;
  const reviewSubmitted = state.document?.voReviewState === WikiDraftReviewState.Submitted;
  const reviewChangesRequested = state.document?.voReviewState === WikiDraftReviewState.ChangesRequested;
  const reviewStateText = getDraftReviewStateText(state.document?.voReviewState, t);
  const pageTitle = route.kind === 'compose'
    ? t('wiki.author.editor.createTitle')
    : state.document?.voTitle || t('wiki.author.editor.editTitle');
  const parentLabel = parentOptions.find((option) => String(option.id) === state.draft.parentId)?.label
    ?? t('wiki.author.form.root');
  const publicReadSlug = state.document
    ? resolveDocsAuthorPublicReadSlug({
        status: state.document.voDocumentStatus,
        documentVersion: state.document.voDocumentVersion,
        documentSlug: state.document.voDocumentSlug,
      })
    : null;
  const publicReadHref = publicReadSlug
    ? buildPublicDocsPath({ kind: 'detail', slug: publicReadSlug })
    : null;
  const acceptedCollaborators = state.document?.voCollaborators.filter(
    (collaborator) => collaborator.voInviteState === WikiCollaboratorState.Accepted,
  ) ?? [];
  const readOnlyNotice = state.document?.voHasDraftPayload === false
    ? t('wiki.author.editor.payloadPurgedNotice')
    : state.document?.voReadOnlyReason || t('wiki.author.editor.authorReadOnlyNotice');
  const saveStateText = state.submitting
    ? t('wiki.author.actions.saving')
    : isEditorUploading
      ? t('wiki.author.editor.uploading')
      : isDirty
        ? t('wiki.author.editor.unsaved')
        : route.kind === 'compose'
          ? t('wiki.author.editor.notCreated')
          : t('wiki.author.editor.saved');
  const canSubmit = state.document?.voCanSubmit === true && !isDirty && !isEditorUploading;
  const handleEditorUploadError = useCallback((kind: 'image' | 'document', error: unknown) => {
    log.error('DocsAuthorEditorPage', `Markdown ${kind} upload failed:`, error);
  }, []);

  useEffect(() => {
    if (!isMobile) {
      setMobileContextOpen(false);
    }
  }, [isMobile]);

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

  const editorContext = (
    <DocsAuthorEditorContext
      route={route}
      draft={state.draft}
      document={state.document}
      outline={outline}
      parentOptions={parentOptions}
      reviewStateText={reviewStateText}
      readOnly={readOnly}
      submitting={state.submitting}
      currentUserPublicId={currentUserPublicId}
      onNavigate={onNavigate}
      onParentChange={onParentChange}
      onSetDraft={onSetDraft}
      onInviteCollaborator={onInviteCollaborator}
      onRemoveCollaborator={onRemoveCollaborator}
      onRespondInvitation={onRespondInvitation}
      protectedAttachments={protectedAttachments}
    />
  );

  return (
    <div className={styles.authorEditorPage}>
      <header className={styles.editorTaskCard}>
        <div className={styles.editorTaskMetaRow}>
          <nav className={styles.editorBreadcrumb} aria-label={t('wiki.author.editor.breadcrumbAriaLabel')}>
            <a href={buildDocsAuthorPath({ kind: 'mine' })} onClick={onBack}>
              <Icon icon="mdi:arrow-left" size={16} />
              <span>{t('wiki.author.actions.myDocuments')}</span>
            </a>
            <span>/</span>
            <span>{parentLabel}</span>
          </nav>
          {publicReadHref ? (
            <a className={styles.editorPublishedLink} href={publicReadHref} onClick={onExternalNavigate}>
              <Icon icon="mdi:open-in-new" size={16} />
              <span>{t('wiki.author.document.documentVersion', { version: state.document?.voDocumentVersion ?? 0 })}</span>
            </a>
          ) : null}
        </div>

        <div className={styles.editorTaskTitleRow}>
          <h1>{pageTitle}</h1>
          <div className={styles.editorPrimaryActions}>
            <span className={`${styles.editorSaveState} ${isDirty ? styles.editorSaveStateDirty : ''}`}>
              <Icon icon={isDirty ? 'mdi:circle-edit-outline' : 'mdi:cloud-check-outline'} size={16} />
              {saveStateText}
            </span>
            <button
              type="submit"
              form={editorFormId}
              className={styles.editorSaveButton}
              disabled={readOnly || state.submitting || isEditorUploading || !isDirty}
            >
              <Icon icon="mdi:content-save-outline" size={17} />
              <span>{t('wiki.author.actions.save')}</span>
            </button>
            {state.document?.voCanSubmit ? (
              <button
                type="button"
                className={styles.editorSubmitButton}
                disabled={!canSubmit || state.submitting}
                onClick={onSubmitDraft}
                title={isDirty ? t('wiki.author.editor.saveBeforeSubmit') : undefined}
              >
                <Icon icon="mdi:send-check-outline" size={17} />
                <span>{t('wiki.author.actions.submitReview')}</span>
              </button>
            ) : null}
            {reviewSubmitted && state.document?.voAuthorRole.toLowerCase() === 'owner' ? (
              <button type="button" className={styles.editorSaveButton} disabled={state.submitting} onClick={onWithdrawDraft}>
                <Icon icon="mdi:undo-variant" size={17} />
                <span>{t('wiki.author.actions.withdrawReview')}</span>
              </button>
            ) : null}
          </div>
        </div>

        <div className={styles.editorStatusBand}>
          <span className={styles.editorStatusDot} aria-hidden="true" />
          <div className={styles.editorStatusCopy}>
            <strong>{reviewStateText}</strong>
            <span>{t('wiki.author.editor.versionEvidence', {
              document: state.document?.voDocumentVersion ?? 0,
              draft: state.document?.voDraftVersion ?? 0,
              role: state.document?.voAuthorRole ?? t('wiki.author.editor.modeCreate'),
            })}</span>
          </div>
          {state.document ? (
            <div className={styles.editorPeopleSummary} aria-label={t('wiki.author.editor.collaboratorSummary', { count: acceptedCollaborators.length + 1 })}>
              <span>{getDocsAuthorInitial(state.document.voOwnerUserName, 'O')}</span>
              {acceptedCollaborators[0] ? <span>{getDocsAuthorInitial(acceptedCollaborators[0].voUserName, 'E')}</span> : null}
              <strong>{acceptedCollaborators.length + 1}</strong>
            </div>
          ) : null}
        </div>
      </header>

      <div className={styles.editorMobileViewSwitch}>
        <button type="button" className={styles.editorMobileViewActive}>
          <Icon icon="mdi:file-document-edit-outline" size={17} />
          <span>{t('wiki.author.editor.bodyView')}</span>
        </button>
        <button type="button" onClick={() => setMobileContextOpen(true)}>
          <Icon icon="mdi:view-split-vertical" size={17} />
          <span>{t('wiki.author.editor.infoView')}</span>
        </button>
      </div>

      <div className={styles.editorWorkspaceGrid}>
        <form id={editorFormId} className={styles.authorEditorForm} onSubmit={(event) => {
          if (isEditorUploading) {
            event.preventDefault();
            return;
          }
          onSave(event);
        }}>
          <section className={styles.authorEditorSurface}>
            {readOnly && state.document ? (
              <div className={styles.editorReadOnlyNotice}>
                <Icon icon="mdi:lock-outline" size={18} />
                <span>{readOnlyNotice}</span>
              </div>
            ) : null}
            {reviewChangesRequested && state.document?.voReviewComment ? (
              <div className={styles.editorReviewNotice}>
                <Icon icon="mdi:message-alert-outline" size={18} />
                <div>
                  <strong>{t('wiki.author.reviewState.changesRequested')}</strong>
                  <span>{state.document.voReviewComment}</span>
                </div>
              </div>
            ) : null}

            <label className={styles.editorTitleField}>
              <span>{t('wiki.author.form.title')} <em>{t('wiki.author.form.required')}</em></span>
              <input
                value={state.draft.title}
                required
                disabled={readOnly || state.submitting}
                onChange={(event) => onSetDraft((current) => ({ ...current, title: event.target.value }))}
                placeholder={t('wiki.author.form.titlePlaceholder')}
              />
            </label>

            <MarkdownEditor
              value={state.draft.markdownContent}
              onChange={(value) => onSetDraft((current) => ({ ...current, markdownContent: value }))}
              labels={markdownEditorLabels}
              minHeight={isMobile ? 520 : 610}
              disabled={readOnly || state.submitting}
              placeholder={t('wiki.author.form.markdownPlaceholder')}
              defaultMode="edit"
              allowSplit={false}
              theme="light"
              className={styles.authorMarkdownEditor}
              toolbarLead={(
                <span className={styles.editorMarkdownLabel}>
                  <Icon icon="mdi:file-document-outline" size={16} />
                  {t('wiki.author.editor.markdownBody')}
                </span>
              )}
              onImageUpload={onImageUpload}
              onDocumentUpload={onDocumentUpload}
              onUploadError={handleEditorUploadError}
              onUploadingChange={onEditorUploadingChange}
              protectedAttachments={protectedAttachments}
            />

            {state.conflict ? (
              <section className={styles.editorConflictPanel} aria-live="assertive">
                <div className={styles.editorConflictHeading}>
                  <span><Icon icon="mdi:shield-check-outline" size={20} /></span>
                  <div>
                    <strong>{t('wiki.author.conflict.localPreserved')}</strong>
                    <p>{t('wiki.author.conflict.description', {
                      draft: state.conflict.serverDraftVersion ?? '-',
                      document: state.conflict.serverDocumentVersion ?? '-',
                    })}</p>
                  </div>
                </div>
                <div className={styles.editorConflictActions}>
                  <button type="button" onClick={onCopyConflictContent}><Icon icon="mdi:content-copy" size={16} />{t('wiki.author.conflict.copy')}</button>
                  <button type="button" onClick={onDownloadConflictContent}><Icon icon="mdi:download-outline" size={16} />{t('wiki.author.conflict.download')}</button>
                  <button type="button" className={styles.editorConflictPrimary} onClick={onReloadServerDraft}><Icon icon="mdi:refresh" size={16} />{t('wiki.author.conflict.reload')}</button>
                </div>
              </section>
            ) : null}

            <footer className={styles.editorSourceStatus}>
              <span>Markdown · {t('wiki.author.editor.characterCount', { count: countDocsAuthorMarkdownCharacters(state.draft.markdownContent) })}</span>
              <span>{t('wiki.author.editor.draftStatus', {
                version: state.document?.voDraftVersion ?? 0,
                role: state.document?.voAuthorRole ?? t('wiki.author.editor.modeCreate'),
              })}</span>
            </footer>
          </section>
        </form>

        {!isMobile ? (
          <aside className={styles.editorDesktopContext} aria-label={t('wiki.author.editor.contextAriaLabel')}>
            {editorContext}
          </aside>
        ) : null}
      </div>

      {isMobile ? (
        <BottomSheet
          isOpen={mobileContextOpen}
          onClose={() => setMobileContextOpen(false)}
          closeLabel={t('wiki.author.editor.closeInfo')}
          title={t('wiki.author.editor.infoView')}
          height="88%"
          bodyClassName={styles.editorContextSheetBody}
        >
          {editorContext}
        </BottomSheet>
      ) : null}
    </div>
  );
}
