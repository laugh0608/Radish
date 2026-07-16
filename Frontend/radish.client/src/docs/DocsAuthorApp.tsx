import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Icon } from '@radish/ui/icon';
import { MarkdownEditor } from '@radish/ui/markdown-editor';
import { MarkdownRenderer } from '@radish/ui/markdown-renderer';
import { toast } from '@radish/ui/toast';
import {
  buildAttachmentAssetUrl,
  type MarkdownDocumentUploadResult,
  type MarkdownImageUploadResult,
} from '@radish/ui';
import type { LongId } from '@/api/user';
import { uploadDocument, uploadImage } from '@/api/attachment';
import { createMarkdownEditorLabels } from '@/i18n/markdownEditorLabels';
import {
  createWikiDocument,
  getWikiDocumentById,
  getWikiList,
  getWikiRevisionDetail,
  getWikiRevisionList,
  getWikiTree,
  updateWikiDocument,
} from '@/apps/wiki/api/wiki';
import {
  buildCreateRequest,
  buildUpdateRequest,
  collectDescendantIds,
  EMPTY_DRAFT,
  flattenTreeOptions,
  formatWikiTime,
  getSuggestedSortValue,
  normalizeOptionalLongId,
  normalizeOptionalNumber,
  type EditorDraft,
  type ParentOption,
} from '@/apps/wiki/wikiApp.helpers';
import type {
  WikiDocumentDetailVo,
  WikiDocumentRevisionDetailVo,
  WikiDocumentRevisionItemVo,
  WikiDocumentTreeNodeVo,
  WikiDocumentVo,
} from '@/apps/wiki/types/wiki';
import { WikiDocumentVisibility } from '@/apps/wiki/types/wiki';
import { WebStateSlot, type WebStateSlotTone } from '@/components/web-shell';
import { getApiBaseUrl } from '@/config/env';
import { PublicShellHeader } from '@/public/components/PublicShellHeader';
import { buildPublicDocsPath } from '@/public/docsRouteState';
import { redirectToLogin } from '@/services/auth';
import { bootstrapAuth, hydrateAuthUser } from '@/services/authBootstrap';
import {
  buildDocsAuthorComposeReturnPath,
  buildDocsAuthorEditReturnPath,
  buildDocsAuthorMineReturnPath,
  buildDocsAuthorRevisionsReturnPath,
} from '@/services/authReturnPath';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { log } from '@/utils/logger';
import { canUseDocsAuthorTools, isBuiltInWikiDocument } from './docsAuthorAccess';
import {
  formatDocsAuthorNumber,
  getDocsAuthorSourceText,
  getDocsAuthorStatusText,
  getDocsAuthorSummaryPreview,
  getDocsAuthorVisibilityText,
  validateDocsAuthorDraft,
} from './docsAuthorPresentation';
import {
  buildDocsAuthorPath,
  createDefaultDocsAuthorRoute,
  type DocsAuthorRoute,
} from './docsAuthorRouteState';
import { shouldHandleAuthorLinkClick, useDocsAuthorNavigation } from './useDocsAuthorNavigation';
import styles from './DocsAuthorApp.module.css';

interface CollectionState {
  tree: WikiDocumentTreeNodeVo[];
  documents: WikiDocumentVo[];
  totalDocuments: number;
  loading: boolean;
  error: string | null;
}

interface EditorState {
  draft: EditorDraft;
  document: WikiDocumentDetailVo | null;
  loading: boolean;
  submitting: boolean;
  error: string | null;
  sortSuggestion: string;
}

interface RevisionState {
  document: WikiDocumentDetailVo | null;
  revisions: WikiDocumentRevisionItemVo[];
  selectedRevision: WikiDocumentRevisionDetailVo | null;
  selectedRevisionId: LongId | null;
  loading: boolean;
  loadingDetail: boolean;
  error: string | null;
}

const initialCollectionState: CollectionState = {
  tree: [],
  documents: [],
  totalDocuments: 0,
  loading: false,
  error: null,
};

const initialEditorState: EditorState = {
  draft: EMPTY_DRAFT,
  document: null,
  loading: false,
  submitting: false,
  error: null,
  sortSuggestion: '0',
};

const initialRevisionState: RevisionState = {
  document: null,
  revisions: [],
  selectedRevision: null,
  selectedRevisionId: null,
  loading: false,
  loadingDetail: false,
  error: null,
};

function canEditDocument(document: WikiDocumentDetailVo | null): boolean {
  return Boolean(document) && canMaintainWikiDocument(document);
}

function getErrorMessage(error: unknown, fallback: string): string {
  return error instanceof Error && error.message.trim() ? error.message : fallback;
}

function createDraftFromDocument(document: WikiDocumentDetailVo): EditorDraft {
  return {
    title: document.voTitle,
    slug: document.voSlug,
    summary: document.voSummary ?? '',
    markdownContent: document.voMarkdownContent,
    parentId: document.voParentId == null ? '' : String(document.voParentId),
    sort: String(document.voSort ?? 0),
    coverAttachmentId: document.voCoverAttachmentId == null ? '' : String(document.voCoverAttachmentId),
    changeSummary: '',
    visibility: String(document.voVisibility ?? WikiDocumentVisibility.Authenticated),
    allowedRoles: (document.voAllowedRoles || []).join('\n'),
    allowedPermissions: (document.voAllowedPermissions || []).join('\n'),
  };
}

function createDraftForCompose(tree: WikiDocumentTreeNodeVo[]): EditorDraft {
  return {
    ...EMPTY_DRAFT,
    sort: String(getSuggestedSortValue(tree)),
  };
}

function canMaintainWikiDocument(document: WikiDocumentVo | WikiDocumentDetailVo | null): boolean {
  return Boolean(document) && !isBuiltInWikiDocument(document) && document?.voIsDeleted !== true;
}

function countMaintainableDocuments(documents: WikiDocumentVo[]): number {
  return documents.filter((document) => canMaintainWikiDocument(document)).length;
}

function countBuiltInDocuments(documents: WikiDocumentVo[]): number {
  return documents.filter((document) => isBuiltInWikiDocument(document)).length;
}

function pickPreviewDocument(documents: WikiDocumentVo[]): WikiDocumentVo | null {
  return documents.find((document) => canMaintainWikiDocument(document)) ?? documents[0] ?? null;
}

function buildRouteReturnPath(route: DocsAuthorRoute): string {
  if (route.kind === 'compose') {
    return buildDocsAuthorComposeReturnPath();
  }

  if (route.kind === 'edit') {
    return buildDocsAuthorEditReturnPath(route.documentId) ?? buildDocsAuthorMineReturnPath();
  }

  if (route.kind === 'revisions') {
    return buildDocsAuthorRevisionsReturnPath(route.documentId) ?? buildDocsAuthorMineReturnPath();
  }

  return buildDocsAuthorMineReturnPath();
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

export function DocsAuthorApp() {
  const { t, i18n } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const userId = useUserStore((state) => state.userId);
  const roles = useUserStore((state) => state.roles || []);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const canUseAuthorTools = useMemo(() => canUseDocsAuthorTools(roles), [roles]);
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const [collectionState, setCollectionState] = useState<CollectionState>(initialCollectionState);
  const [editorState, setEditorState] = useState<EditorState>(initialEditorState);
  const [isEditorUploading, setIsEditorUploading] = useState(false);
  const { route, navigateToRoute } = useDocsAuthorNavigation(isEditorUploading);
  const [revisionState, setRevisionState] = useState<RevisionState>(initialRevisionState);
  const treeRef = useRef<WikiDocumentTreeNodeVo[]>([]);

  const mineHref = buildDocsAuthorPath({ kind: 'mine' });
  const composeHref = buildDocsAuthorPath({ kind: 'compose' });

  useEffect(() => {
    treeRef.current = collectionState.tree;
  }, [collectionState.tree]);

  const loadCollections = useCallback(async () => {
    setCollectionState((current) => ({
      ...current,
      loading: true,
      error: null,
    }));

    try {
      const [tree, list] = await Promise.all([
        getWikiTree(t),
        getWikiList({ pageIndex: 1, pageSize: 100 }, t),
      ]);

      setCollectionState({
        tree,
        documents: list.data || [],
        totalDocuments: list.dataCount || list.data?.length || 0,
        loading: false,
        error: null,
      });
    } catch (error) {
      log.error('DocsAuthorApp', '加载文档作者集合失败:', error);
      setCollectionState((current) => ({
        ...current,
        loading: false,
        error: getErrorMessage(error, t('wiki.author.feedback.loadListFailed')),
      }));
    }
  }, [t]);

  const loadEditor = useCallback(async (nextRoute: DocsAuthorRoute) => {
    const currentTree = treeRef.current;

    if (nextRoute.kind === 'compose') {
      const draft = createDraftForCompose(currentTree);
      setEditorState({
        ...initialEditorState,
        draft,
        sortSuggestion: draft.sort,
      });
      return;
    }

    if (nextRoute.kind !== 'edit') {
      return;
    }

    setEditorState((current) => ({
      ...current,
      loading: true,
      error: null,
    }));

    try {
      const document = await getWikiDocumentById(nextRoute.documentId, true, t);
      const draft = createDraftFromDocument(document);
      setEditorState({
        draft,
        document,
        loading: false,
        submitting: false,
        error: null,
        sortSuggestion: String(getSuggestedSortValue(currentTree, document.voParentId, document.voId)),
      });
    } catch (error) {
      log.error('DocsAuthorApp', '加载文档编辑详情失败:', error);
      setEditorState((current) => ({
        ...current,
        document: null,
        loading: false,
        submitting: false,
        error: getErrorMessage(error, t('wiki.author.feedback.loadDetailFailed')),
      }));
    }
  }, [t]);

  const loadRevisionDetail = useCallback(async (revisionId: LongId) => {
    setRevisionState((current) => ({
      ...current,
      selectedRevisionId: revisionId,
      selectedRevision: null,
      loadingDetail: true,
      error: null,
    }));

    try {
      const detail = await getWikiRevisionDetail(revisionId, t);
      setRevisionState((current) => ({
        ...current,
        selectedRevision: detail,
        loadingDetail: false,
      }));
    } catch (error) {
      log.error('DocsAuthorApp', '加载文档修订详情失败:', error);
      setRevisionState((current) => ({
        ...current,
        selectedRevision: null,
        loadingDetail: false,
        error: getErrorMessage(error, t('wiki.author.feedback.loadRevisionDetailFailed')),
      }));
    }
  }, [t]);

  const loadRevisions = useCallback(async (documentId: LongId) => {
    setRevisionState({
      ...initialRevisionState,
      loading: true,
    });

    try {
      const [document, revisions] = await Promise.all([
        getWikiDocumentById(documentId, true, t),
        getWikiRevisionList(documentId, t),
      ]);
      const selectedRevisionId = revisions.find((revision) => revision.voIsCurrent)?.voId ?? revisions[0]?.voId ?? null;

      setRevisionState({
        document,
        revisions,
        selectedRevision: null,
        selectedRevisionId,
        loading: false,
        loadingDetail: false,
        error: null,
      });

      if (selectedRevisionId) {
        await loadRevisionDetail(selectedRevisionId);
      }
    } catch (error) {
      log.error('DocsAuthorApp', '加载文档修订列表失败:', error);
      setRevisionState({
        ...initialRevisionState,
        loading: false,
        error: getErrorMessage(error, t('wiki.author.feedback.loadRevisionsFailed')),
      });
    }
  }, [loadRevisionDetail, t]);

  useEffect(() => {
    const cleanup = bootstrapAuth({ apiBaseUrl });
    let cancelled = false;

    hydrateAuthUser({ apiBaseUrl })
      .catch((error) => {
        log.warn('DocsAuthorApp', '文档作者入口登录态初始化失败', error);
        return null;
      })
      .finally(() => {
        if (!cancelled) {
          setAuthReady(true);
        }
      });

    return () => {
      cancelled = true;
      cleanup();
    };
  }, [apiBaseUrl]);

  useEffect(() => {
    document.title = t('wiki.author.documentTitle');
  }, [t]);

  useEffect(() => {
    if (!authReady || loggedIn || redirecting) {
      return;
    }

    setRedirecting(true);
    redirectToLogin({
      returnPath: buildRouteReturnPath(route),
    });
  }, [authReady, loggedIn, redirecting, route]);

  useEffect(() => {
    if (!authReady || !loggedIn || !canUseAuthorTools) {
      return;
    }

    void loadCollections();
  }, [authReady, canUseAuthorTools, loadCollections, loggedIn]);

  useEffect(() => {
    if (!authReady || !loggedIn || !canUseAuthorTools) {
      return;
    }

    if (route.kind === 'compose' || route.kind === 'edit') {
      void loadEditor(route);
      return;
    }

    if (route.kind === 'revisions') {
      void loadRevisions(route.documentId);
    }
  }, [authReady, canUseAuthorTools, loadEditor, loadRevisions, loggedIn, route]);

  const handleRouteLinkClick = (event: MouseEvent<HTMLAnchorElement>, nextRoute: DocsAuthorRoute) => {
    if (!shouldHandleAuthorLinkClick(event)) {
      return;
    }

    event.preventDefault();
    if (isEditorUploading) {
      return;
    }

    navigateToRoute(nextRoute);
  };

  const preventEditorNavigationWhileUploading = (event: MouseEvent<HTMLAnchorElement>) => {
    if (isEditorUploading && shouldHandleAuthorLinkClick(event)) {
      event.preventDefault();
    }
  };

  const setDraft = useCallback((updater: (current: EditorDraft) => EditorDraft) => {
    setEditorState((current) => ({
      ...current,
      draft: updater(current.draft),
    }));
  }, []);

  const handleParentChange = useCallback((nextParentId: string) => {
    const currentDocumentId = route.kind === 'edit' ? route.documentId : undefined;
    const nextSuggestedSort = String(getSuggestedSortValue(
      collectionState.tree,
      normalizeOptionalLongId(nextParentId),
      currentDocumentId,
    ));

    setEditorState((current) => ({
      ...current,
      draft: {
        ...current.draft,
        parentId: nextParentId,
        sort: !current.draft.sort.trim() || current.draft.sort === current.sortSuggestion ? nextSuggestedSort : current.draft.sort,
      },
      sortSuggestion: nextSuggestedSort,
    }));
  }, [collectionState.tree, route]);

  const handleImageUpload = async (
    file: File,
    reportProgress: (progress: number) => void,
  ): Promise<MarkdownImageUploadResult> => {
    const attachment = await uploadImage(
      {
        file,
        businessType: 'Wiki',
        generateThumbnail: true,
        removeExif: true,
        onProgress: reportProgress,
      },
      t,
    );

    return {
      attachmentId: attachment.voId,
      displayVariant: 'original',
      previewUrl: buildAttachmentAssetUrl(attachment.voId, 'original'),
    };
  };

  const handleDocumentUpload = async (
    file: File,
    reportProgress: (progress: number) => void,
  ): Promise<MarkdownDocumentUploadResult> => {
    const attachment = await uploadDocument(
      {
        file,
        businessType: 'Wiki',
        onProgress: reportProgress,
      },
      t,
    );

    return {
      attachmentId: attachment.voId,
      fileName: attachment.voOriginalName || file.name,
    };
  };

  const handleSave = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const validationMessage = validateDocsAuthorDraft(editorState.draft, t);
    if (validationMessage) {
      toast.error(validationMessage);
      return;
    }

    if (route.kind === 'edit' && !canEditDocument(editorState.document)) {
      toast.error(t('wiki.author.feedback.editUnavailable'));
      return;
    }

    setEditorState((current) => ({
      ...current,
      submitting: true,
    }));

    try {
      if (route.kind === 'compose') {
        const createdId = await createWikiDocument(buildCreateRequest(editorState.draft), t);
        toast.success(t('wiki.author.feedback.created'));
        await loadCollections();
        navigateToRoute({ kind: 'edit', documentId: createdId });
        return;
      }

      if (route.kind !== 'edit') {
        return;
      }

      await updateWikiDocument(route.documentId, buildUpdateRequest(editorState.draft), t);
      toast.success(t('wiki.author.feedback.saved'));
      await Promise.all([
        loadCollections(),
        loadEditor(route),
      ]);
    } catch (error) {
      log.error('DocsAuthorApp', '保存文档失败:', error);
      toast.error(getErrorMessage(error, t('wiki.author.feedback.saveFailed')));
    } finally {
      setEditorState((current) => ({
        ...current,
        submitting: false,
      }));
    }
  };

  const renderContent = () => {
    if (!authReady || !loggedIn) {
      return (
        <StatusPanel
          icon="mdi:lock-outline"
          title={t('wiki.author.auth.loadingTitle')}
          description={t('wiki.author.auth.loadingDescription')}
        />
      );
    }

    if (!canUseAuthorTools) {
      return (
        <StatusPanel
          icon="mdi:shield-alert-outline"
          title={t('wiki.author.auth.forbiddenTitle')}
          description={t('wiki.author.auth.forbiddenDescription')}
          actionHref={buildPublicDocsPath({ kind: 'list' })}
          actionLabel={t('wiki.author.actions.publicDocs')}
        />
      );
    }

    if (route.kind === 'compose' || route.kind === 'edit') {
      return (
        <DocsEditorPage
          route={route}
          tree={collectionState.tree}
          state={editorState}
          isEditorUploading={isEditorUploading}
          onBack={(event) => handleRouteLinkClick(event, createDefaultDocsAuthorRoute())}
          onNavigate={handleRouteLinkClick}
          onParentChange={handleParentChange}
          onSetDraft={setDraft}
          onSave={handleSave}
          onImageUpload={handleImageUpload}
          onDocumentUpload={handleDocumentUpload}
          onEditorUploadingChange={setIsEditorUploading}
        />
      );
    }

    if (route.kind === 'revisions') {
      return (
        <DocsRevisionsPage
          state={revisionState}
          language={i18n.resolvedLanguage}
          onBack={(event) => handleRouteLinkClick(event, createDefaultDocsAuthorRoute())}
          onEdit={(event, documentId) => handleRouteLinkClick(event, { kind: 'edit', documentId })}
          onSelectRevision={(revisionId) => void loadRevisionDetail(revisionId)}
        />
      );
    }

    return (
      <DocsMinePage
        state={collectionState}
        language={i18n.resolvedLanguage}
        onReload={() => void loadCollections()}
        onNavigate={handleRouteLinkClick}
      />
    );
  };

  return (
    <div className={styles.page}>
      <PublicShellHeader
        variant="private"
        activeKey="more"
        brandMark={t('wiki.author.brandMark')}
        brandName={t('wiki.author.title')}
        brandSubline={t('wiki.author.brandSubline')}
        onBrandClick={() => navigateToRoute(createDefaultDocsAuthorRoute())}
        navigationLocked={isEditorUploading}
      />

      <main className={styles.main}>
        <section className={styles.authorHero} aria-label={t('wiki.author.heroAriaLabel')}>
          <div className={styles.authorHeroCopy}>
            <p className={styles.kicker}>Author Workspace</p>
            <h1 className={styles.authorHeroTitle}>{t('wiki.author.title')}</h1>
            <p className={styles.authorHeroDescription}>{t('wiki.author.heroDescription')}</p>
          </div>
          <div className={styles.authorSummaryGrid}>
            <div className={styles.authorSummaryCard}>
              <span className={styles.authorSummaryIcon}>
                <Icon icon="mdi:file-tree-outline" size={20} />
              </span>
              <strong>{formatDocsAuthorNumber(collectionState.tree.length, i18n.resolvedLanguage)}</strong>
              <span>{t('wiki.author.metrics.directoryNodes')}</span>
            </div>
            <div className={styles.authorSummaryCard}>
              <span className={styles.authorSummaryIcon}>
                <Icon icon="mdi:file-document-multiple-outline" size={20} />
              </span>
              <strong>{formatDocsAuthorNumber(collectionState.totalDocuments, i18n.resolvedLanguage)}</strong>
              <span>{t('wiki.author.metrics.totalDocuments')}</span>
            </div>
            <div className={styles.authorSummaryCard}>
              <span className={styles.authorSummaryIcon}>
                <Icon icon={canUseAuthorTools ? 'mdi:shield-check-outline' : 'mdi:shield-alert-outline'} size={20} />
              </span>
              <strong>{canUseAuthorTools ? t('wiki.author.access.writable') : t('wiki.author.access.restricted')}</strong>
              <span>{t(`wiki.author.route.${route.kind}`)}</span>
            </div>
          </div>
        </section>

        <div className={styles.navBar}>
          <a
            className={route.kind === 'mine' ? styles.navItemActive : styles.navItem}
            href={mineHref}
            onClick={(event) => handleRouteLinkClick(event, { kind: 'mine' })}
            aria-disabled={isEditorUploading}
          >
            <Icon icon="mdi:file-document-multiple-outline" size={18} />
            <span>{t('wiki.author.actions.myDocuments')}</span>
          </a>
          <a
            className={route.kind === 'compose' ? styles.navItemActive : styles.navItem}
            href={composeHref}
            onClick={(event) => handleRouteLinkClick(event, { kind: 'compose' })}
            aria-disabled={isEditorUploading}
          >
            <Icon icon="mdi:plus-box-outline" size={18} />
            <span>{t('wiki.author.actions.create')}</span>
          </a>
          <a
            className={styles.navItem}
            href={buildPublicDocsPath({ kind: 'list' })}
            onClick={preventEditorNavigationWhileUploading}
            aria-disabled={isEditorUploading}
          >
            <Icon icon="mdi:book-open-page-variant-outline" size={18} />
            <span>{t('wiki.author.actions.publicReading')}</span>
          </a>
        </div>

        {renderContent()}
      </main>
    </div>
  );
}

interface StatusPanelProps {
  icon: string;
  title: string;
  description: string;
  actionHref?: string;
  actionLabel?: string;
}

function StatusPanel({ icon, title, description, actionHref, actionLabel }: StatusPanelProps) {
  const tone: WebStateSlotTone = icon === 'mdi:progress-clock'
    ? 'loading'
    : icon === 'mdi:shield-alert-outline'
      ? 'permission'
      : icon === 'mdi:alert-circle-outline'
        ? 'error'
        : icon === 'mdi:file-document-outline' || icon === 'mdi:history'
          ? 'empty'
          : icon === 'mdi:lock-outline'
            ? 'auth'
            : 'info';

  return (
    <section className={styles.statusPanel}>
      <WebStateSlot
        tone={tone}
        icon={icon}
        title={title}
        description={description}
        actions={actionHref && actionLabel ? [{ label: actionLabel, href: actionHref }] : undefined}
      />
    </section>
  );
}

interface DocsMinePageProps {
  state: CollectionState;
  language?: string;
  onReload: () => void;
  onNavigate: (event: MouseEvent<HTMLAnchorElement>, route: DocsAuthorRoute) => void;
}

function DocsMinePage({ state, language, onReload, onNavigate }: DocsMinePageProps) {
  const { t } = useTranslation();
  const hasDocuments = state.documents.length > 0;
  const previewDocument = pickPreviewDocument(state.documents);
  const maintainableCount = countMaintainableDocuments(state.documents);
  const builtInCount = countBuiltInDocuments(state.documents);
  const deletedCount = state.documents.filter((document) => document.voIsDeleted).length;

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

        <div className={styles.summaryGrid}>
          <SummaryTile label={t('wiki.author.metrics.directoryNodes')} value={state.tree.length} language={language} />
          <SummaryTile label={t('wiki.author.metrics.totalDocuments')} value={state.totalDocuments} language={language} />
          <SummaryTile label={t('wiki.author.metrics.loadedDocuments')} value={state.documents.length} language={language} />
        </div>

        {state.error ? (
          <StatusPanel
            icon="mdi:alert-circle-outline"
            title={t('wiki.author.mine.errorTitle')}
            description={state.error}
          />
        ) : state.loading && !hasDocuments ? (
          <StatusPanel
            icon="mdi:progress-clock"
            title={t('wiki.author.mine.loadingTitle')}
            description={t('wiki.author.mine.loadingDescription')}
          />
        ) : !hasDocuments ? (
          <StatusPanel
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
                key={document.voId}
                document={document}
                language={language}
                onNavigate={onNavigate}
              />
            ))}
          </div>
        )}
      </section>

      <aside className={styles.authorRail} aria-label={t('wiki.author.mine.contextAriaLabel')}>
        <section className={styles.railCard}>
          <p className={styles.railKicker}>{t('wiki.author.rail.library')}</p>
          <div className={styles.railMetricGrid}>
            <RailMetric label={t('wiki.author.rail.maintainable')} value={maintainableCount} language={language} />
            <RailMetric label={t('wiki.author.rail.builtInReadOnly')} value={builtInCount} language={language} />
            <RailMetric label={t('wiki.author.rail.deleted')} value={deletedCount} language={language} />
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
                <span className={styles.railChip}>{getDocsAuthorStatusText(previewDocument.voStatus, t)}</span>
                <span className={styles.railChip}>{getDocsAuthorVisibilityText(previewDocument.voVisibility, t)}</span>
                <span className={styles.railChip}>v{previewDocument.voVersion}</span>
              </div>
              <div className={styles.railActionList}>
                {canMaintainWikiDocument(previewDocument) ? (
                  <a
                    className={styles.railLink}
                    href={buildDocsAuthorPath({ kind: 'edit', documentId: previewDocument.voId })}
                    onClick={(event) => onNavigate(event, { kind: 'edit', documentId: previewDocument.voId })}
                  >
                    <Icon icon="mdi:pencil-outline" size={18} />
                    <span>{t('wiki.author.actions.edit')}</span>
                  </a>
                ) : null}
                <a
                  className={styles.railLink}
                  href={buildDocsAuthorPath({ kind: 'revisions', documentId: previewDocument.voId })}
                  onClick={(event) => onNavigate(event, { kind: 'revisions', documentId: previewDocument.voId })}
                >
                  <Icon icon="mdi:history" size={18} />
                  <span>{t('wiki.author.actions.revisions')}</span>
                </a>
                <a className={styles.railLink} href={buildPublicDocsPath({ kind: 'detail', slug: previewDocument.voSlug })}>
                  <Icon icon="mdi:book-open-page-variant-outline" size={18} />
                  <span>{t('wiki.author.actions.publicReading')}</span>
                </a>
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

interface SummaryTileProps {
  label: string;
  value: number;
  language?: string;
}

function SummaryTile({ label, value, language }: SummaryTileProps) {
  return (
    <div className={styles.summaryTile}>
      <span className={styles.summaryLabel}>{label}</span>
      <strong className={styles.summaryValue}>{formatDocsAuthorNumber(value, language)}</strong>
    </div>
  );
}

interface RailMetricProps {
  label: string;
  value: number | string;
  language?: string;
}

function RailMetric({ label, value, language }: RailMetricProps) {
  return (
    <div className={styles.railMetric}>
      <span>{label}</span>
      <strong>{typeof value === 'number' ? formatDocsAuthorNumber(value, language) : value}</strong>
    </div>
  );
}

function getDocumentEditBlockedReason(document: WikiDocumentVo, t: TFunction): string | null {
  if (document.voIsDeleted) {
    return t('wiki.author.document.deletedReadOnly');
  }

  if (isBuiltInWikiDocument(document)) {
    return t('wiki.author.document.builtInReadOnly');
  }

  return null;
}

interface DocumentRowProps {
  document: WikiDocumentVo;
  language?: string;
  onNavigate: (event: MouseEvent<HTMLAnchorElement>, route: DocsAuthorRoute) => void;
}

function DocumentRow({ document, language, onNavigate }: DocumentRowProps) {
  const { t } = useTranslation();
  const editRoute: DocsAuthorRoute = { kind: 'edit', documentId: document.voId };
  const revisionsRoute: DocsAuthorRoute = { kind: 'revisions', documentId: document.voId };
  const publicHref = buildPublicDocsPath({ kind: 'detail', slug: document.voSlug });
  const editBlockedReason = getDocumentEditBlockedReason(document, t);
  const canEdit = !editBlockedReason;

  return (
    <article className={styles.documentRow}>
      <div className={styles.documentMain}>
        <div className={styles.metaRow}>
          <span className={styles.statusChip}>{document.voIsDeleted ? t('wiki.author.document.deleted') : getDocsAuthorStatusText(document.voStatus, t)}</span>
          <span className={styles.metaChip}>{getDocsAuthorVisibilityText(document.voVisibility, t)}</span>
          <span className={styles.metaChip}>{getDocsAuthorSourceText(document.voSourceType, t)}</span>
          <span className={styles.metaChip}>v{document.voVersion}</span>
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
        {canEdit ? (
          <a
            className={styles.primaryButton}
            href={buildDocsAuthorPath(editRoute)}
            onClick={(event) => onNavigate(event, editRoute)}
          >
            {t('wiki.author.actions.edit')}
          </a>
        ) : (
          <span className={styles.readOnlyButton} title={editBlockedReason ?? undefined}>
            {editBlockedReason ?? t('wiki.author.access.readOnly')}
          </span>
        )}
        <a
          className={styles.secondaryButton}
          href={buildDocsAuthorPath(revisionsRoute)}
          onClick={(event) => onNavigate(event, revisionsRoute)}
        >
          {t('wiki.author.actions.revisions')}
        </a>
        <a className={styles.secondaryButton} href={publicHref}>
          {t('wiki.author.actions.read')}
        </a>
      </div>
    </article>
  );
}

interface DocsEditorPageProps {
  route: DocsAuthorRoute & ({ kind: 'compose' } | { kind: 'edit' });
  tree: WikiDocumentTreeNodeVo[];
  state: EditorState;
  isEditorUploading: boolean;
  onBack: (event: MouseEvent<HTMLAnchorElement>) => void;
  onNavigate: (event: MouseEvent<HTMLAnchorElement>, route: DocsAuthorRoute) => void;
  onParentChange: (parentId: string) => void;
  onSetDraft: (updater: (current: EditorDraft) => EditorDraft) => void;
  onSave: (event: FormEvent<HTMLFormElement>) => void;
  onImageUpload: (file: File, reportProgress: (progress: number) => void) => Promise<MarkdownImageUploadResult>;
  onDocumentUpload: (file: File, reportProgress: (progress: number) => void) => Promise<MarkdownDocumentUploadResult>;
  onEditorUploadingChange: (uploading: boolean) => void;
}

function DocsEditorPage({
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
}: DocsEditorPageProps) {
  const { t, i18n } = useTranslation();
  const markdownEditorLabels = useMemo(
    () => createMarkdownEditorLabels(t, i18n.resolvedLanguage ?? i18n.language),
    [i18n.language, i18n.resolvedLanguage, t],
  );
  const handleEditorUploadError = useCallback((kind: 'image' | 'document', error: unknown) => {
    log.error('DocsEditorPage', `Markdown ${kind} upload failed:`, error);
  }, []);
  const parentOptions = useMemo(
    () => buildParentOptions(tree, route.kind === 'edit' ? route.documentId : null),
    [route, tree]
  );
  const readOnly = route.kind === 'edit' && !canEditDocument(state.document);
  const pageTitle = route.kind === 'compose' ? t('wiki.author.editor.createTitle') : state.document?.voTitle || t('wiki.author.editor.editTitle');
  const pageIntro = route.kind === 'compose'
    ? t('wiki.author.editor.createIntro')
    : t('wiki.author.editor.editIntro');
  const publicReadHref = state.document && !state.document.voIsDeleted && state.document.voSlug.trim()
    ? buildPublicDocsPath({ kind: 'detail', slug: state.document.voSlug })
    : null;
  const draftVisibilityText = getDocsAuthorVisibilityText(
    normalizeOptionalNumber(state.draft.visibility) ?? WikiDocumentVisibility.Authenticated,
    t,
  );
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

  if (state.loading) {
    return (
      <StatusPanel
        icon="mdi:progress-clock"
        title={t('wiki.author.editor.loadingTitle')}
        description={t('wiki.author.editor.loadingDescription')}
      />
    );
  }

  if (state.error) {
    return (
      <StatusPanel
        icon="mdi:alert-circle-outline"
        title={t('wiki.author.editor.errorTitle')}
        description={state.error}
      />
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
              if (!event.defaultPrevented) {
                onBack(event);
              }
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
                if (!event.defaultPrevented) {
                  onNavigate(event, { kind: 'revisions', documentId: route.documentId });
                }
              }}
              aria-disabled={isEditorUploading}
            >
              <Icon icon="mdi:history" size={18} />
              <span>{t('wiki.author.actions.revisions')}</span>
            </a>
          ) : null}
          {publicReadHref ? (
            <a
              className={styles.secondaryButton}
              href={publicReadHref}
              onClick={preventNavigationWhileUploading}
              aria-disabled={isEditorUploading}
            >
              <Icon icon="mdi:book-open-page-variant-outline" size={18} />
              <span>{t('wiki.author.actions.publicReading')}</span>
            </a>
          ) : null}
        </div>
      </div>

      {readOnly && state.document ? (
        <div className={styles.inlineNotice}>
          <Icon icon="mdi:lock-outline" size={20} />
          <span>{t('wiki.author.editor.readOnlyNotice', {
            source: getDocsAuthorSourceText(state.document.voSourceType, t),
            deleted: state.document.voIsDeleted ? t('wiki.author.editor.deletedSuffix') : '',
          })}</span>
        </div>
      ) : null}

      <form className={styles.editorForm} onSubmit={handleEditorSubmit}>
        <div className={styles.formGrid}>
          <label className={styles.field}>
            <span>{t('wiki.author.form.title')}</span>
            <input
              className={styles.input}
              value={state.draft.title}
              disabled={readOnly || state.submitting}
              onChange={(event) => onSetDraft((current) => ({ ...current, title: event.target.value }))}
              placeholder={t('wiki.author.form.titlePlaceholder')}
            />
          </label>
          <label className={styles.field}>
            <span>Slug</span>
            <input
              className={styles.input}
              value={state.draft.slug}
              disabled={readOnly || state.submitting}
              onChange={(event) => onSetDraft((current) => ({ ...current, slug: event.target.value }))}
              placeholder={t('wiki.author.form.slugPlaceholder')}
            />
          </label>
          <label className={styles.field}>
            <span>{t('wiki.author.form.parent')}</span>
            <select
              className={styles.select}
              value={state.draft.parentId}
              disabled={readOnly || state.submitting}
              onChange={(event) => onParentChange(event.target.value)}
            >
              <option value="">{t('wiki.author.form.root')}</option>
              {parentOptions.map((option) => (
                <option key={option.id} value={String(option.id)}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
          <label className={styles.field}>
            <span>{t('wiki.author.form.sort')}</span>
            <input
              className={styles.input}
              value={state.draft.sort}
              disabled={readOnly || state.submitting}
              onChange={(event) => onSetDraft((current) => ({ ...current, sort: event.target.value }))}
            />
          </label>
          <label className={styles.field}>
            <span>{t('wiki.author.form.coverAttachmentId')}</span>
            <input
              className={styles.input}
              value={state.draft.coverAttachmentId}
              disabled={readOnly || state.submitting}
              onChange={(event) => onSetDraft((current) => ({ ...current, coverAttachmentId: event.target.value }))}
              placeholder={t('wiki.author.form.optional')}
            />
          </label>
          {route.kind === 'edit' ? (
            <label className={styles.field}>
              <span>{t('wiki.author.form.changeSummary')}</span>
              <input
                className={styles.input}
                value={state.draft.changeSummary}
                disabled={readOnly || state.submitting}
                onChange={(event) => onSetDraft((current) => ({ ...current, changeSummary: event.target.value }))}
                placeholder={t('wiki.author.form.changeSummaryPlaceholder')}
              />
            </label>
          ) : null}
          <label className={`${styles.field} ${styles.fieldFull}`}>
            <span>{t('wiki.author.form.summary')}</span>
            <textarea
              className={styles.textarea}
              value={state.draft.summary}
              disabled={readOnly || state.submitting}
              onChange={(event) => onSetDraft((current) => ({ ...current, summary: event.target.value }))}
              placeholder={t('wiki.author.form.summaryPlaceholder')}
            />
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

        <div className={styles.editorActions}>
          <span className={styles.editorHint}>
            {t('wiki.author.editor.currentVisibility', { visibility: draftVisibilityText })}
          </span>
          <button type="submit" className={styles.primaryButton} disabled={readOnly || state.submitting || isEditorUploading}>
            <Icon icon={state.submitting ? 'mdi:progress-clock' : 'mdi:content-save-outline'} size={18} />
            <span>{state.submitting ? t('wiki.author.actions.saving') : t('wiki.author.actions.save')}</span>
          </button>
        </div>
      </form>
      </section>

      <aside className={styles.authorRail} aria-label={t('wiki.author.editor.contextAriaLabel')}>
        <section className={styles.railCard}>
          <p className={styles.railKicker}>{t('wiki.author.editor.context')}</p>
          <div className={styles.railMetricGrid}>
            <RailMetric label={t('wiki.author.editor.mode')} value={route.kind === 'compose' ? t('wiki.author.editor.modeCreate') : t('wiki.author.editor.modeEdit')} />
            <RailMetric label={t('wiki.author.editor.parentOptions')} value={parentOptions.length} language={i18n.resolvedLanguage} />
            <RailMetric label={t('wiki.author.editor.suggestedSort')} value={state.sortSuggestion} />
          </div>
          <div className={styles.railChipList}>
            <span className={styles.railChip}>{draftVisibilityText}</span>
            <span className={styles.railChip}>{readOnly ? t('wiki.author.access.readOnly') : t('wiki.author.access.savable')}</span>
          </div>
        </section>

        {state.document ? (
          <section className={styles.railCard}>
            <p className={styles.railKicker}>{t('wiki.author.editor.evidence')}</p>
            <h2 className={styles.railTitle}>{state.document.voTitle}</h2>
            <p className={styles.railText}>{getDocsAuthorSummaryPreview(state.document.voSummary, t)}</p>
            <div className={styles.railChipList}>
              <span className={styles.railChip}>{getDocsAuthorStatusText(state.document.voStatus, t)}</span>
              <span className={styles.railChip}>{getDocsAuthorSourceText(state.document.voSourceType, t)}</span>
              <span className={styles.railChip}>v{state.document.voVersion}</span>
            </div>
          </section>
        ) : null}

        <section className={styles.railCard}>
          <p className={styles.railKicker}>{t('wiki.author.editor.flowActions')}</p>
          <div className={styles.railActionList}>
            <a
              className={styles.railLink}
              href={buildDocsAuthorPath({ kind: 'mine' })}
              onClick={(event) => {
                preventNavigationWhileUploading(event);
                if (!event.defaultPrevented) {
                  onBack(event);
                }
              }}
              aria-disabled={isEditorUploading}
            >
              <Icon icon="mdi:arrow-left" size={18} />
              <span>{t('wiki.author.actions.backToList')}</span>
            </a>
            {route.kind === 'edit' ? (
              <a
                className={styles.railLink}
                href={buildDocsAuthorPath({ kind: 'revisions', documentId: route.documentId })}
                onClick={(event) => {
                  preventNavigationWhileUploading(event);
                  if (!event.defaultPrevented) {
                    onNavigate(event, { kind: 'revisions', documentId: route.documentId });
                  }
                }}
                aria-disabled={isEditorUploading}
              >
                <Icon icon="mdi:history" size={18} />
                <span>{t('wiki.author.actions.revisions')}</span>
              </a>
            ) : null}
            {publicReadHref ? (
              <a
                className={styles.railLink}
                href={publicReadHref}
                onClick={preventNavigationWhileUploading}
                aria-disabled={isEditorUploading}
              >
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

interface DocsRevisionsPageProps {
  state: RevisionState;
  language?: string;
  onBack: (event: MouseEvent<HTMLAnchorElement>) => void;
  onEdit: (event: MouseEvent<HTMLAnchorElement>, documentId: LongId) => void;
  onSelectRevision: (revisionId: LongId) => void;
}

function DocsRevisionsPage({ state, language, onBack, onEdit, onSelectRevision }: DocsRevisionsPageProps) {
  const { t } = useTranslation();
  const publicReadHref = state.document && !state.document.voIsDeleted && state.document.voSlug.trim()
    ? buildPublicDocsPath({ kind: 'detail', slug: state.document.voSlug })
    : null;
  const currentRevision = state.revisions.find((revision) => revision.voIsCurrent) ?? null;
  const selectedVersion = state.selectedRevision?.voVersion ?? currentRevision?.voVersion ?? state.document?.voVersion ?? '-';

  return (
    <div className={styles.authorWorkspace}>
      <section className={styles.panel}>
        <div className={styles.panelHeader}>
          <div>
            <p className={styles.kicker}>Revision History</p>
            <h1 className={styles.pageTitle}>{state.document?.voTitle || t('wiki.author.revisions.title')}</h1>
            <p className={styles.pageIntro}>{t('wiki.author.revisions.intro')}</p>
          </div>
          <div className={styles.headerActions}>
            <a className={styles.secondaryButton} href={buildDocsAuthorPath({ kind: 'mine' })} onClick={onBack}>
              <Icon icon="mdi:arrow-left" size={18} />
              <span>{t('wiki.author.actions.backToList')}</span>
            </a>
            {state.document && canEditDocument(state.document) ? (
              <a
                className={styles.primaryButton}
                href={buildDocsAuthorPath({ kind: 'edit', documentId: state.document.voId })}
                onClick={(event) => onEdit(event, state.document!.voId)}
              >
                <Icon icon="mdi:pencil-outline" size={18} />
                <span>{t('wiki.author.actions.edit')}</span>
              </a>
            ) : null}
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
      ) : state.error && state.revisions.length === 0 ? (
        <StatusPanel
          icon="mdi:alert-circle-outline"
          title={t('wiki.author.revisions.errorTitle')}
          description={state.error}
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
            {state.revisions.map((revision) => (
              <button
                key={revision.voId}
                type="button"
                className={revision.voId === state.selectedRevisionId ? styles.revisionItemActive : styles.revisionItem}
                onClick={() => onSelectRevision(revision.voId)}
              >
                <span className={styles.revisionTitle}>v{revision.voVersion}</span>
                <span className={styles.revisionSummary}>{revision.voChangeSummary || t('wiki.author.revisions.noSummary')}</span>
                <span className={styles.revisionMeta}>
                  {formatWikiTime(revision.voCreateTime, language)} · {revision.voCreateBy}
                </span>
                {revision.voIsCurrent ? <span className={styles.statusChip}>{t('wiki.author.revisions.current')}</span> : null}
              </button>
            ))}
          </aside>

          <article className={styles.revisionPreview}>
            {state.loadingDetail ? (
              <StatusPanel
                icon="mdi:progress-clock"
                title={t('wiki.author.revisions.detailLoadingTitle')}
                description={t('wiki.author.revisions.detailLoadingDescription')}
              />
            ) : state.selectedRevision ? (
              <>
                <div className={styles.revisionPreviewHeader}>
                  <div>
                    <h2 className={styles.revisionPreviewTitle}>v{state.selectedRevision.voVersion}</h2>
                    <p className={styles.revisionMeta}>{state.selectedRevision.voTitle}</p>
                  </div>
                  <div className={styles.metaRow}>
                    <span className={styles.metaChip}>{getDocsAuthorSourceText(state.selectedRevision.voSourceType, t)}</span>
                    <span className={styles.metaChip}>{formatWikiTime(state.selectedRevision.voCreateTime, language)}</span>
                  </div>
                </div>
                <MarkdownRenderer content={state.selectedRevision.voMarkdownContent} className={styles.markdownContent} />
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
            <RailMetric label={t('wiki.author.revisions.currentVersion')} value={currentRevision ? `v${currentRevision.voVersion}` : '-'} />
            <RailMetric label={t('wiki.author.revisions.selectedVersion')} value={selectedVersion === '-' ? '-' : `v${selectedVersion}`} />
          </div>
          <p className={styles.railText}>{t('wiki.author.revisions.boundary')}</p>
        </section>

        {state.selectedRevision ? (
          <section className={styles.railCard}>
            <p className={styles.railKicker}>{t('wiki.author.revisions.selectedSnapshot')}</p>
            <h2 className={styles.railTitle}>{state.selectedRevision.voTitle}</h2>
            <p className={styles.railText}>{state.selectedRevision.voChangeSummary || t('wiki.author.revisions.noSummary')}</p>
            <div className={styles.railChipList}>
              <span className={styles.railChip}>{getDocsAuthorSourceText(state.selectedRevision.voSourceType, t)}</span>
              <span className={styles.railChip}>{formatWikiTime(state.selectedRevision.voCreateTime, language)}</span>
              {state.selectedRevision.voIsCurrent ? <span className={styles.railChip}>{t('wiki.author.revisions.current')}</span> : null}
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
            {state.document && canEditDocument(state.document) ? (
              <a
                className={styles.railLink}
                href={buildDocsAuthorPath({ kind: 'edit', documentId: state.document.voId })}
                onClick={(event) => onEdit(event, state.document!.voId)}
              >
                <Icon icon="mdi:pencil-outline" size={18} />
                <span>{t('wiki.author.actions.edit')}</span>
              </a>
            ) : null}
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
