import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { Icon } from '@radish/ui/icon';
import { MarkdownRenderer } from '@radish/ui/markdown-renderer';
import { toast } from '@radish/ui/toast';
import {
  buildAttachmentAssetUrl,
  type MarkdownDocumentUploadResult,
  type MarkdownImageUploadResult,
} from '@radish/ui';
import {
  ApiResponseError,
  WikiDraftReviewState,
  type CreateWikiAuthorDraftRequest,
  type SaveWikiAuthorDraftRequest,
  type WikiAuthorDocumentVo,
  type WikiAuthorDraftDetailVo,
} from '@radish/http';
import type { LongId } from '@/api/user';
import { uploadDocument, uploadImage } from '@/api/attachment';
import {
  createWikiAuthorDraft,
  getWikiAuthorDraft,
  getWikiAuthorList,
  getWikiDocumentById,
  getWikiRevisionDetail,
  getWikiRevisionList,
  getWikiTree,
  inviteWikiAuthorCollaborator,
  removeWikiAuthorCollaborator,
  respondWikiAuthorInvitation,
  saveWikiAuthorDraft,
  startWikiAuthorDraft,
  submitWikiAuthorDraft,
  withdrawWikiAuthorDraft,
} from '@/apps/wiki/api/wiki';
import {
  EMPTY_DRAFT,
  formatWikiTime,
  getSuggestedSortValue,
  normalizeOptionalLongId,
  type EditorDraft,
} from '@/apps/wiki/wikiApp.helpers';
import type {
  WikiDocumentDetailVo,
  WikiDocumentRevisionDetailVo,
  WikiDocumentRevisionItemVo,
  WikiDocumentTreeNodeVo,
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
import {
  formatDocsAuthorNumber,
  getDocsAuthorSourceText,
  getDocsAuthorSummaryPreview,
  validateDocsAuthorDraft,
} from './docsAuthorPresentation';
import { DocsAuthorEditorPage, type DocsAuthorEditorState } from './DocsAuthorEditorPage';
import {
  buildDocsAuthorPath,
  createDefaultDocsAuthorRoute,
  type DocsAuthorRoute,
} from './docsAuthorRouteState';
import { shouldHandleAuthorLinkClick, useDocsAuthorNavigation } from './useDocsAuthorNavigation';
import styles from './DocsAuthorApp.module.css';

interface CollectionState {
  tree: WikiDocumentTreeNodeVo[];
  documents: WikiAuthorDocumentVo[];
  totalDocuments: number;
  loading: boolean;
  error: string | null;
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

const initialEditorState: DocsAuthorEditorState = {
  draft: EMPTY_DRAFT,
  document: null,
  loading: false,
  submitting: false,
  error: null,
  sortSuggestion: '0',
  conflict: null,
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

function canEditDocument(document: WikiAuthorDraftDetailVo | null): boolean {
  return document?.voCanEdit === true;
}

function getErrorMessage(error: unknown, fallback: string): string {
  return error instanceof Error && error.message.trim() ? error.message : fallback;
}

function createDraftFromDocument(document: WikiAuthorDraftDetailVo): EditorDraft {
  return {
    title: document.voTitle,
    slug: document.voSlug,
    summary: document.voSummary ?? '',
    markdownContent: document.voMarkdownContent,
    parentId: document.voProposedParentId == null ? '' : String(document.voProposedParentId),
    sort: '0',
    coverAttachmentId: document.voCoverAttachmentId == null ? '' : String(document.voCoverAttachmentId),
    changeSummary: '',
    visibility: String(WikiDocumentVisibility.Authenticated),
    allowedRoles: '',
    allowedPermissions: '',
  };
}

function createDraftForCompose(tree: WikiDocumentTreeNodeVo[]): EditorDraft {
  return {
    ...EMPTY_DRAFT,
    sort: String(getSuggestedSortValue(tree)),
  };
}

function buildAuthorDraftRequest(draft: EditorDraft): CreateWikiAuthorDraftRequest {
  return {
    title: draft.title.trim(),
    slug: draft.slug.trim() || undefined,
    summary: draft.summary.trim() || undefined,
    markdownContent: draft.markdownContent,
    coverAttachmentId: normalizeOptionalLongId(draft.coverAttachmentId),
    proposedParentId: normalizeOptionalLongId(draft.parentId),
    changeSummary: draft.changeSummary.trim() || undefined,
  };
}

function buildSaveAuthorDraftRequest(draft: EditorDraft, expectedDraftVersion: number): SaveWikiAuthorDraftRequest {
  return {
    ...buildAuthorDraftRequest(draft),
    expectedDraftVersion,
  };
}

function countOwnedDocuments(documents: WikiAuthorDocumentVo[]): number {
  return documents.filter((document) => document.voAuthorRole.toLowerCase() === 'owner').length;
}

function countCollaboratingDocuments(documents: WikiAuthorDocumentVo[]): number {
  return documents.filter((document) => document.voAuthorRole.toLowerCase() !== 'owner').length;
}

function pickPreviewDocument(documents: WikiAuthorDocumentVo[]): WikiAuthorDocumentVo | null {
  return documents[0] ?? null;
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

export function DocsAuthorApp() {
  const { t, i18n } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const userId = useUserStore((state) => state.userId);
  const userPublicId = useUserStore((state) => state.publicId || '');
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const [collectionState, setCollectionState] = useState<CollectionState>(initialCollectionState);
  const [editorState, setEditorState] = useState<DocsAuthorEditorState>(initialEditorState);
  const [isEditorUploading, setIsEditorUploading] = useState(false);
  const { route, navigateToRoute } = useDocsAuthorNavigation(isEditorUploading);
  const [revisionState, setRevisionState] = useState<RevisionState>(initialRevisionState);
  const treeRef = useRef<WikiDocumentTreeNodeVo[]>([]);
  const accountEpochRef = useRef(0);

  const mineHref = buildDocsAuthorPath({ kind: 'mine' });
  const composeHref = buildDocsAuthorPath({ kind: 'compose' });

  useEffect(() => {
    treeRef.current = collectionState.tree;
  }, [collectionState.tree]);

  const loadCollections = useCallback(async () => {
    const accountEpoch = accountEpochRef.current;
    setCollectionState((current) => ({
      ...current,
      loading: true,
      error: null,
    }));

    try {
      const [tree, list] = await Promise.all([
        getWikiTree(t),
        getWikiAuthorList(t),
      ]);
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }

      setCollectionState({
        tree,
        documents: list.data || [],
        totalDocuments: list.dataCount || list.data?.length || 0,
        loading: false,
        error: null,
      });
    } catch (error) {
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
      log.error('DocsAuthorApp', '加载文档作者集合失败:', error);
      setCollectionState((current) => ({
        ...current,
        loading: false,
        error: getErrorMessage(error, t('wiki.author.feedback.loadListFailed')),
      }));
    }
  }, [t]);

  const loadEditor = useCallback(async (nextRoute: DocsAuthorRoute) => {
    const accountEpoch = accountEpochRef.current;
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
      const document = await getWikiAuthorDraft(nextRoute.documentId, t);
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
      const draft = createDraftFromDocument(document);
      setEditorState({
        draft,
        document,
        loading: false,
        submitting: false,
        error: null,
        sortSuggestion: String(getSuggestedSortValue(currentTree, document.voProposedParentId, document.voDocumentId)),
        conflict: null,
      });
    } catch (error) {
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
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
    const accountEpoch = accountEpochRef.current;
    setRevisionState((current) => ({
      ...current,
      selectedRevisionId: revisionId,
      selectedRevision: null,
      loadingDetail: true,
      error: null,
    }));

    try {
      const detail = await getWikiRevisionDetail(revisionId, t);
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
      setRevisionState((current) => ({
        ...current,
        selectedRevision: detail,
        loadingDetail: false,
      }));
    } catch (error) {
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
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
    const accountEpoch = accountEpochRef.current;
    setRevisionState({
      ...initialRevisionState,
      loading: true,
    });

    try {
      const [document, revisions] = await Promise.all([
        getWikiDocumentById(documentId, true, t),
        getWikiRevisionList(documentId, t),
      ]);
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
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
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
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
    accountEpochRef.current += 1;
    treeRef.current = [];
    setCollectionState(initialCollectionState);
    setEditorState(initialEditorState);
    setRevisionState(initialRevisionState);
    setIsEditorUploading(false);
  }, [userId]);

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
    if (!authReady || !loggedIn) {
      return;
    }

    void loadCollections();
  }, [authReady, loadCollections, loggedIn, userId]);

  useEffect(() => {
    if (!authReady || !loggedIn) {
      return;
    }

    if (route.kind === 'compose' || route.kind === 'edit') {
      void loadEditor(route);
      return;
    }

    if (route.kind === 'revisions') {
      void loadRevisions(route.documentId);
    }
  }, [authReady, loadEditor, loadRevisions, loggedIn, route, userId]);

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
    const accountEpoch = accountEpochRef.current;
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
        const created = await createWikiAuthorDraft(buildAuthorDraftRequest(editorState.draft), t);
        if (accountEpoch !== accountEpochRef.current) {
          return;
        }
        toast.success(t('wiki.author.feedback.created'));
        await loadCollections();
        if (accountEpoch !== accountEpochRef.current) {
          return;
        }
        navigateToRoute({ kind: 'edit', documentId: created.voDocumentId });
        return;
      }

      if (route.kind !== 'edit') {
        return;
      }

      const currentDocument = editorState.document;
      if (!currentDocument) {
        throw new Error(t('wiki.author.feedback.loadDetailFailed'));
      }

      const saved = await saveWikiAuthorDraft(
        currentDocument.voDraftId,
        buildSaveAuthorDraftRequest(editorState.draft, currentDocument.voDraftVersion),
        t,
      );
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
      toast.success(t('wiki.author.feedback.saved'));
      setEditorState((current) => ({
        ...current,
        document: saved,
        draft: createDraftFromDocument(saved),
        conflict: null,
      }));
      await loadCollections();
    } catch (error) {
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
      log.error('DocsAuthorApp', '保存文档失败:', error);
      if (error instanceof ApiResponseError && error.code === 'Wiki.DraftVersionConflict') {
        let serverDraftVersion: number | null = null;
        let serverDocumentVersion: number | null = null;
        if (route.kind === 'edit') {
          try {
            const server = await getWikiAuthorDraft(route.documentId, t);
            serverDraftVersion = server.voDraftVersion;
            serverDocumentVersion = server.voDocumentVersion;
          } catch (reloadError) {
            log.warn('DocsAuthorApp', '读取冲突后的服务器草稿版本失败:', reloadError);
          }
        }
        setEditorState((current) => ({
          ...current,
          conflict: {
            localMarkdownContent: current.draft.markdownContent,
            serverDraftVersion,
            serverDocumentVersion,
          },
        }));
      }
      toast.error(getErrorMessage(error, t('wiki.author.feedback.saveFailed')));
    } finally {
      if (accountEpoch === accountEpochRef.current) {
        setEditorState((current) => ({
          ...current,
          submitting: false,
        }));
      }
    }
  };

  const handleStartDraft = async (documentId: LongId) => {
    const accountEpoch = accountEpochRef.current;
    try {
      await startWikiAuthorDraft(documentId, t);
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
      await loadCollections();
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
      navigateToRoute({ kind: 'edit', documentId });
    } catch (error) {
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
      log.error('DocsAuthorApp', '开启下一份工作草稿失败:', error);
      toast.error(getErrorMessage(error, t('wiki.author.feedback.startDraftFailed')));
    }
  };

  const handleSubmitDraft = async () => {
    const detail = editorState.document;
    if (!detail?.voCanSubmit) {
      toast.error(t('wiki.author.feedback.submitUnavailable'));
      return;
    }

    const accountEpoch = accountEpochRef.current;
    try {
      const submitted = await submitWikiAuthorDraft(detail.voDraftId, {
        expectedDraftVersion: detail.voDraftVersion,
        changeSummary: editorState.draft.changeSummary.trim() || undefined,
      }, t);
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
      setEditorState((current) => ({ ...current, document: submitted, conflict: null }));
      toast.success(t('wiki.author.feedback.submitted'));
      await loadCollections();
    } catch (error) {
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
      log.error('DocsAuthorApp', '提交文档草稿审核失败:', error);
      toast.error(getErrorMessage(error, t('wiki.author.feedback.submitFailed')));
    }
  };

  const handleWithdrawDraft = async () => {
    const detail = editorState.document;
    if (!detail) {
      return;
    }

    const accountEpoch = accountEpochRef.current;
    try {
      const withdrawn = await withdrawWikiAuthorDraft(detail.voDraftId, {
        expectedDraftVersion: detail.voDraftVersion,
        changeSummary: editorState.draft.changeSummary.trim() || undefined,
      }, t);
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
      setEditorState((current) => ({ ...current, document: withdrawn, conflict: null }));
      toast.success(t('wiki.author.feedback.withdrawn'));
      await loadCollections();
    } catch (error) {
      if (accountEpoch !== accountEpochRef.current) {
        return;
      }
      log.error('DocsAuthorApp', '撤回文档草稿审核失败:', error);
      toast.error(getErrorMessage(error, t('wiki.author.feedback.withdrawFailed')));
    }
  };

  const refreshEditorDetail = async () => {
    if (route.kind !== 'edit') {
      return;
    }
    await loadEditor(route);
  };

  const handleInviteCollaborator = async (publicId: string) => {
    const detail = editorState.document;
    if (!detail) {
      return;
    }
    const accountEpoch = accountEpochRef.current;
    await inviteWikiAuthorCollaborator(detail.voDocumentId, publicId, t);
    if (accountEpoch !== accountEpochRef.current) {
      return;
    }
    await refreshEditorDetail();
    if (accountEpoch !== accountEpochRef.current) {
      return;
    }
    toast.success(t('wiki.author.feedback.invited'));
  };

  const handleRemoveCollaborator = async (collaboratorId: LongId) => {
    const accountEpoch = accountEpochRef.current;
    await removeWikiAuthorCollaborator(collaboratorId, t);
    if (accountEpoch !== accountEpochRef.current) {
      return;
    }
    await refreshEditorDetail();
    if (accountEpoch !== accountEpochRef.current) {
      return;
    }
    toast.success(t('wiki.author.feedback.collaboratorRemoved'));
  };

  const handleRespondInvitation = async (collaboratorId: LongId, accept: boolean) => {
    const accountEpoch = accountEpochRef.current;
    await respondWikiAuthorInvitation(collaboratorId, accept, t);
    if (accountEpoch !== accountEpochRef.current) {
      return;
    }
    await refreshEditorDetail();
    await loadCollections();
    if (accountEpoch !== accountEpochRef.current) {
      return;
    }
    toast.success(t(accept ? 'wiki.author.feedback.invitationAccepted' : 'wiki.author.feedback.invitationDeclined'));
  };

  const handleCopyConflictContent = async () => {
    const content = editorState.conflict?.localMarkdownContent;
    if (!content) {
      return;
    }
    try {
      await navigator.clipboard.writeText(content);
      toast.success(t('wiki.author.feedback.localContentCopied'));
    } catch (error) {
      log.warn('DocsAuthorApp', '复制本地冲突内容失败:', error);
      toast.error(t('wiki.author.feedback.localContentCopyFailed'));
    }
  };

  const handleDownloadConflictContent = () => {
    const content = editorState.conflict?.localMarkdownContent;
    if (!content) {
      return;
    }
    const blob = new Blob([content], { type: 'text/markdown;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${editorState.document?.voSlug || 'wiki-draft'}-local.md`;
    anchor.click();
    URL.revokeObjectURL(url);
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

    if (route.kind === 'compose' || route.kind === 'edit') {
      return (
        <DocsAuthorEditorPage
          key={userId}
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
          currentUserPublicId={userPublicId}
          onSubmitDraft={() => void handleSubmitDraft()}
          onWithdrawDraft={() => void handleWithdrawDraft()}
          onInviteCollaborator={handleInviteCollaborator}
          onRemoveCollaborator={handleRemoveCollaborator}
          onRespondInvitation={handleRespondInvitation}
          onCopyConflictContent={() => void handleCopyConflictContent()}
          onDownloadConflictContent={handleDownloadConflictContent}
          onReloadServerDraft={() => void refreshEditorDetail()}
        />
      );
    }

    if (route.kind === 'revisions') {
      return (
        <DocsRevisionsPage
          state={revisionState}
          language={i18n.resolvedLanguage}
          onBack={(event) => handleRouteLinkClick(event, createDefaultDocsAuthorRoute())}
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
        onStartDraft={(documentId) => void handleStartDraft(documentId)}
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
                <Icon icon={loggedIn ? 'mdi:shield-check-outline' : 'mdi:shield-alert-outline'} size={20} />
              </span>
              <strong>{loggedIn ? t('wiki.author.access.writable') : t('wiki.author.access.restricted')}</strong>
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
  onStartDraft: (documentId: LongId) => void;
}

function DocsMinePage({ state, language, onReload, onNavigate, onStartDraft }: DocsMinePageProps) {
  const { t } = useTranslation();
  const hasDocuments = state.documents.length > 0;
  const previewDocument = pickPreviewDocument(state.documents);
  const ownedCount = countOwnedDocuments(state.documents);
  const collaboratingCount = countCollaboratingDocuments(state.documents);
  const submittedCount = state.documents.filter((document) => document.voReviewState === WikiDraftReviewState.Submitted).length;

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
                key={document.voDocumentId}
                document={document}
                language={language}
                onNavigate={onNavigate}
                onStartDraft={onStartDraft}
              />
            ))}
          </div>
        )}
      </section>

      <aside className={styles.authorRail} aria-label={t('wiki.author.mine.contextAriaLabel')}>
        <section className={styles.railCard}>
          <p className={styles.railKicker}>{t('wiki.author.rail.library')}</p>
          <div className={styles.railMetricGrid}>
            <RailMetric label={t('wiki.author.rail.owned')} value={ownedCount} language={language} />
            <RailMetric label={t('wiki.author.rail.collaborating')} value={collaboratingCount} language={language} />
            <RailMetric label={t('wiki.author.rail.submitted')} value={submittedCount} language={language} />
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
                <span className={styles.railChip}>{previewDocument.voAuthorRole}</span>
                <span className={styles.railChip}>{getDraftReviewStateText(previewDocument.voReviewState, t)}</span>
                <span className={styles.railChip}>{t('wiki.author.document.versionPair', { document: previewDocument.voDocumentVersion, draft: previewDocument.voDraftVersion ?? '-' })}</span>
              </div>
              <div className={styles.railActionList}>
                {previewDocument.voDraftId && previewDocument.voCanEdit ? (
                  <a
                    className={styles.railLink}
                    href={buildDocsAuthorPath({ kind: 'edit', documentId: previewDocument.voDocumentId })}
                    onClick={(event) => onNavigate(event, { kind: 'edit', documentId: previewDocument.voDocumentId })}
                  >
                    <Icon icon="mdi:pencil-outline" size={18} />
                    <span>{t('wiki.author.actions.edit')}</span>
                  </a>
                ) : previewDocument.voCanEdit ? (
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
                {previewDocument.voDocumentVersion > 0 ? <a className={styles.railLink} href={buildPublicDocsPath({ kind: 'detail', slug: previewDocument.voSlug })}>
                  <Icon icon="mdi:book-open-page-variant-outline" size={18} />
                  <span>{t('wiki.author.actions.publicReading')}</span>
                </a> : null}
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
  onNavigate: (event: MouseEvent<HTMLAnchorElement>, route: DocsAuthorRoute) => void;
  onStartDraft: (documentId: LongId) => void;
}

function DocumentRow({ document, language, onNavigate, onStartDraft }: DocumentRowProps) {
  const { t } = useTranslation();
  const editRoute: DocsAuthorRoute = { kind: 'edit', documentId: document.voDocumentId };
  const revisionsRoute: DocsAuthorRoute = { kind: 'revisions', documentId: document.voDocumentId };
  const publicHref = buildPublicDocsPath({ kind: 'detail', slug: document.voSlug });

  return (
    <article className={styles.documentRow}>
      <div className={styles.documentMain}>
        <div className={styles.metaRow}>
          <span className={styles.statusChip}>{getDraftReviewStateText(document.voReviewState, t)}</span>
          <span className={styles.metaChip}>{document.voAuthorRole}</span>
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
        {document.voDraftId && document.voCanEdit ? (
          <a
            className={styles.primaryButton}
            href={buildDocsAuthorPath(editRoute)}
            onClick={(event) => onNavigate(event, editRoute)}
          >
            {t('wiki.author.actions.edit')}
          </a>
        ) : document.voCanEdit ? (
          <button type="button" className={styles.primaryButton} onClick={() => onStartDraft(document.voDocumentId)}>
            {t('wiki.author.actions.startDraft')}
          </button>
        ) : <span className={styles.readOnlyButton}>{t('wiki.author.access.readOnly')}</span>}
        <a
          className={styles.secondaryButton}
          href={buildDocsAuthorPath(revisionsRoute)}
          onClick={(event) => onNavigate(event, revisionsRoute)}
        >
          {t('wiki.author.actions.revisions')}
        </a>
        {document.voDocumentVersion > 0 ? <a className={styles.secondaryButton} href={publicHref}>
          {t('wiki.author.actions.read')}
        </a> : null}
      </div>
    </article>
  );
}

interface DocsRevisionsPageProps {
  state: RevisionState;
  language?: string;
  onBack: (event: MouseEvent<HTMLAnchorElement>) => void;
  onSelectRevision: (revisionId: LongId) => void;
}

function DocsRevisionsPage({ state, language, onBack, onSelectRevision }: DocsRevisionsPageProps) {
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
