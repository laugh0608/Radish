import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Icon } from '@radish/ui/icon';
import { toast } from '@radish/ui/toast';
import {
  type MarkdownDocumentUploadResult,
  type MarkdownImageUploadResult,
} from '@radish/ui';
import {
  ApiResponseError,
  type CreateWikiAuthorDraftRequest,
  type SaveWikiAuthorDraftRequest,
  type WikiAuthorDraftDetailVo,
  type WikiAuthorListQuery,
  type WikiAuthorRevisionItemVo,
} from '@radish/http';
import type { LongId } from '@/api/user';
import { uploadDocument, uploadImage } from '@/api/attachment';
import {
  createWikiAuthorDraft,
  getWikiAuthorDraft,
  getWikiAuthorList,
  getWikiAuthorRevisionDetail,
  getWikiAuthorRevisionHistory,
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
  getSuggestedSortValue,
  normalizeOptionalLongId,
  type EditorDraft,
} from '@/apps/wiki/wikiApp.helpers';
import type { WikiDocumentTreeNodeVo } from '@/apps/wiki/types/wiki';
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
  validateDocsAuthorDraft,
} from './docsAuthorPresentation';
import { areDocsAuthorDraftsEqual } from './docsAuthorEditorPresentation';
import { DocsAuthorEditorPage, type DocsAuthorEditorState } from './DocsAuthorEditorPage';
import { DocsMinePage, type CollectionState } from './DocsMinePage';
import { DocsRevisionsPage } from './DocsRevisionsPage';
import { createDocsProtectedAttachmentOptions } from './docsProtectedAttachments';
import {
  buildDocsAuthorPath,
  createDefaultDocsAuthorRoute,
  type DocsAuthorRoute,
} from './docsAuthorRouteState';
import { shouldHandleAuthorLinkClick, useDocsAuthorNavigation } from './useDocsAuthorNavigation';
import {
  findPreviousRevision,
  isSameLongId,
  type RevisionComparisonMode,
  type RevisionState,
} from './docsRevisionComparison';
import styles from './DocsAuthorApp.module.css';
import editorStyles from './DocsAuthorEditorPage.module.css';

const DEFAULT_AUTHOR_LIST_QUERY: WikiAuthorListQuery = {
  scope: 'all',
  draftStage: 'all',
  pageIndex: 1,
  pageSize: 20,
};

const initialCollectionState: CollectionState = {
  tree: [],
  documents: [],
  totalDocuments: 0,
  page: DEFAULT_AUTHOR_LIST_QUERY.pageIndex,
  pageSize: DEFAULT_AUTHOR_LIST_QUERY.pageSize,
  pageCount: 0,
  scope: DEFAULT_AUTHOR_LIST_QUERY.scope,
  draftStage: DEFAULT_AUTHOR_LIST_QUERY.draftStage,
  loading: false,
  error: null,
};

const initialEditorState: DocsAuthorEditorState = {
  draft: EMPTY_DRAFT,
  baselineDraft: EMPTY_DRAFT,
  document: null,
  loading: false,
  submitting: false,
  error: null,
  sortSuggestion: '0',
  conflict: null,
};

const initialRevisionState: RevisionState = {
  history: null,
  revisions: [],
  selectedRevision: null,
  selectedRevisionId: null,
  comparisonMode: 'previous',
  comparisonRevision: null,
  comparisonRevisionId: null,
  loading: false,
  loadingDetail: false,
  loadingComparison: false,
  historyError: null,
  detailError: null,
  detailStale: false,
  comparisonError: null,
  comparisonStale: false,
  comparisonMissing: false,
};

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
  const userRoleScope = useUserStore((state) => (state.roles || []).join(','));
  const userPermissionScope = useUserStore((state) => (state.permissions || []).join(','));
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const [collectionState, setCollectionState] = useState<CollectionState>(initialCollectionState);
  const [editorState, setEditorState] = useState<DocsAuthorEditorState>(initialEditorState);
  const [isEditorUploading, setIsEditorUploading] = useState(false);
  const isEditorDirty = !areDocsAuthorDraftsEqual(editorState.draft, editorState.baselineDraft);
  const acknowledgeEditorDraft = useCallback(() => {
    setEditorState((current) => ({
      ...current,
      baselineDraft: current.draft,
    }));
  }, []);
  const { route, navigateToRoute } = useDocsAuthorNavigation({
    navigationLocked: isEditorUploading,
    confirmRequired: isEditorDirty,
    confirmMessage: t('wiki.author.editor.unsavedLeaveConfirm'),
    onConfirmNavigation: acknowledgeEditorDraft,
  });
  const [revisionState, setRevisionState] = useState<RevisionState>(initialRevisionState);
  const protectedAttachmentRouteDocumentId = route.kind === 'edit' || route.kind === 'revisions'
    ? route.documentId
    : 'none';
  const protectedAttachments = useMemo(() => {
    const documentEpoch = editorState.document
      ? [
          editorState.document.voDocumentId,
          editorState.document.voDocumentVersion,
          editorState.document.voDraftVersion,
          editorState.document.voCanEdit,
          editorState.document.voReviewState,
        ].join(':')
      : 'none';
    return createDocsProtectedAttachmentOptions(
      t,
      `author:${userId || 'anonymous'}:${userRoleScope}:${userPermissionScope}:${route.kind}:${protectedAttachmentRouteDocumentId}:${documentEpoch}:${revisionState.selectedRevisionId ?? 'none'}`,
    );
  }, [
    editorState.document,
    protectedAttachmentRouteDocumentId,
    revisionState.selectedRevisionId,
    route.kind,
    t,
    userId,
    userPermissionScope,
    userRoleScope,
  ]);
  const treeRef = useRef<WikiDocumentTreeNodeVo[]>([]);
  const accountEpochRef = useRef(0);
  const authorListQueryRef = useRef<WikiAuthorListQuery>(DEFAULT_AUTHOR_LIST_QUERY);
  const authorListEpochRef = useRef(0);
  const revisionDocumentIdRef = useRef<LongId | null>(null);
  const revisionHistoryEpochRef = useRef(0);
  const revisionDetailEpochRef = useRef(0);
  const revisionComparisonEpochRef = useRef(0);

  const mineHref = buildDocsAuthorPath({ kind: 'mine' });
  const composeHref = buildDocsAuthorPath({ kind: 'compose' });

  useEffect(() => {
    treeRef.current = collectionState.tree;
  }, [collectionState.tree]);

  const loadCollections = useCallback(async (requestedQuery?: WikiAuthorListQuery) => {
    const accountEpoch = accountEpochRef.current;
    const requestEpoch = ++authorListEpochRef.current;
    const query = requestedQuery ?? authorListQueryRef.current;
    authorListQueryRef.current = query;
    setCollectionState((current) => {
      const preserveExisting = current.scope === query.scope &&
        current.draftStage === query.draftStage &&
        current.page === query.pageIndex &&
        current.pageSize === query.pageSize;
      return {
        ...current,
        documents: preserveExisting ? current.documents : [],
        totalDocuments: preserveExisting ? current.totalDocuments : 0,
        pageCount: preserveExisting ? current.pageCount : 0,
        scope: query.scope,
        draftStage: query.draftStage,
        page: query.pageIndex,
        pageSize: query.pageSize,
        loading: true,
        error: null,
      };
    });

    try {
      const [tree, list] = await Promise.all([
        getWikiTree(t),
        getWikiAuthorList(query, t),
      ]);
      if (accountEpoch !== accountEpochRef.current || requestEpoch !== authorListEpochRef.current) {
        return;
      }

      setCollectionState({
        tree,
        documents: list.data || [],
        totalDocuments: list.dataCount || list.data?.length || 0,
        page: list.page || query.pageIndex,
        pageSize: list.pageSize || query.pageSize,
        pageCount: list.pageCount || 0,
        scope: query.scope,
        draftStage: query.draftStage,
        loading: false,
        error: null,
      });
    } catch (error) {
      if (accountEpoch !== accountEpochRef.current || requestEpoch !== authorListEpochRef.current) {
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
        baselineDraft: draft,
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
        baselineDraft: draft,
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

  const loadRevisionDetail = useCallback(async (revisionId: LongId, documentId = revisionDocumentIdRef.current) => {
    if (!documentId) {
      return;
    }
    const accountEpoch = accountEpochRef.current;
    const requestEpoch = ++revisionDetailEpochRef.current;
    revisionComparisonEpochRef.current += 1;
    setRevisionState((current) => ({
      ...current,
      selectedRevisionId: revisionId,
      selectedRevision: isSameLongId(current.selectedRevision?.voId, revisionId)
        ? current.selectedRevision
        : null,
      comparisonRevision: isSameLongId(current.selectedRevisionId, revisionId)
        ? current.comparisonRevision
        : null,
      comparisonRevisionId: isSameLongId(current.selectedRevisionId, revisionId)
        ? current.comparisonRevisionId
        : null,
      loadingDetail: true,
      loadingComparison: false,
      detailError: null,
      detailStale: false,
      comparisonError: null,
      comparisonStale: false,
      comparisonMissing: false,
    }));

    try {
      const detail = await getWikiAuthorRevisionDetail(revisionId, t);
      if (accountEpoch !== accountEpochRef.current ||
          requestEpoch !== revisionDetailEpochRef.current ||
          documentId !== revisionDocumentIdRef.current) {
        return;
      }
      setRevisionState((current) => ({
        ...current,
        selectedRevision: isSameLongId(current.selectedRevisionId, revisionId) ? detail : current.selectedRevision,
        loadingDetail: false,
        detailError: null,
        detailStale: false,
      }));
    } catch (error) {
      if (accountEpoch !== accountEpochRef.current ||
          requestEpoch !== revisionDetailEpochRef.current ||
          documentId !== revisionDocumentIdRef.current) {
        return;
      }
      log.error('DocsAuthorApp', '加载文档修订详情失败:', error);
      setRevisionState((current) => ({
        ...current,
        selectedRevision: isSameLongId(current.selectedRevisionId, revisionId)
          ? current.selectedRevision
          : null,
        loadingDetail: false,
        detailError: getErrorMessage(error, t('wiki.author.feedback.loadRevisionDetailFailed')),
        detailStale: Boolean(
          isSameLongId(current.selectedRevisionId, revisionId)
          && isSameLongId(current.selectedRevision?.voId, revisionId),
        ),
      }));
    }
  }, [t]);

  const loadRevisionComparison = useCallback(async (
    selectedRevisionId: LongId,
    mode: RevisionComparisonMode,
    revisions: WikiAuthorRevisionItemVo[],
    documentId = revisionDocumentIdRef.current,
  ) => {
    if (!documentId) {
      return;
    }

    const comparisonSummary = mode === 'previous'
      ? findPreviousRevision(revisions, selectedRevisionId)
      : revisions.find((revision) => revision.voIsCurrent) ?? null;
    const accountEpoch = accountEpochRef.current;
    const requestEpoch = ++revisionComparisonEpochRef.current;

    if (!comparisonSummary || isSameLongId(comparisonSummary.voId, selectedRevisionId)) {
      setRevisionState((current) => (
        isSameLongId(current.selectedRevisionId, selectedRevisionId) && current.comparisonMode === mode
          ? {
              ...current,
              comparisonRevision: null,
              comparisonRevisionId: null,
              loadingComparison: false,
              comparisonError: null,
              comparisonStale: false,
              comparisonMissing: mode === 'previous',
            }
          : current
      ));
      return;
    }

    setRevisionState((current) => {
      if (!isSameLongId(current.selectedRevisionId, selectedRevisionId) || current.comparisonMode !== mode) {
        return current;
      }

      const preserveExisting = isSameLongId(current.comparisonRevisionId, comparisonSummary.voId);
      return {
        ...current,
        comparisonRevision: preserveExisting ? current.comparisonRevision : null,
        comparisonRevisionId: comparisonSummary.voId,
        loadingComparison: true,
        comparisonError: null,
        comparisonStale: false,
        comparisonMissing: false,
      };
    });

    try {
      const detail = await getWikiAuthorRevisionDetail(comparisonSummary.voId, t);
      if (accountEpoch !== accountEpochRef.current
          || requestEpoch !== revisionComparisonEpochRef.current
          || documentId !== revisionDocumentIdRef.current) {
        return;
      }
      setRevisionState((current) => (
        isSameLongId(current.selectedRevisionId, selectedRevisionId)
        && current.comparisonMode === mode
        && isSameLongId(current.comparisonRevisionId, comparisonSummary.voId)
          ? {
              ...current,
              comparisonRevision: detail,
              loadingComparison: false,
              comparisonError: null,
              comparisonStale: false,
            }
          : current
      ));
    } catch (error) {
      if (accountEpoch !== accountEpochRef.current
          || requestEpoch !== revisionComparisonEpochRef.current
          || documentId !== revisionDocumentIdRef.current) {
        return;
      }
      log.error('DocsAuthorApp', '加载文档差异比较基准失败:', error);
      setRevisionState((current) => {
        if (!isSameLongId(current.selectedRevisionId, selectedRevisionId)
            || current.comparisonMode !== mode
            || !isSameLongId(current.comparisonRevisionId, comparisonSummary.voId)) {
          return current;
        }
        return {
          ...current,
          loadingComparison: false,
          comparisonError: getErrorMessage(error, t('wiki.author.feedback.loadRevisionComparisonFailed')),
          comparisonStale: Boolean(current.comparisonRevision),
        };
      });
    }
  }, [t]);

  const loadRevisions = useCallback(async (documentId: LongId) => {
    const accountEpoch = accountEpochRef.current;
    const requestEpoch = ++revisionHistoryEpochRef.current;
    revisionDetailEpochRef.current += 1;
    revisionComparisonEpochRef.current += 1;
    const preserveExisting = revisionDocumentIdRef.current === documentId;
    revisionDocumentIdRef.current = documentId;
    setRevisionState((current) => preserveExisting
      ? {
          ...current,
          loading: true,
          loadingDetail: false,
          historyError: null,
          detailError: null,
          detailStale: false,
        }
      : {
          ...initialRevisionState,
          loading: true,
        });

    try {
      const history = await getWikiAuthorRevisionHistory(documentId, t);
      if (accountEpoch !== accountEpochRef.current ||
          requestEpoch !== revisionHistoryEpochRef.current ||
          documentId !== revisionDocumentIdRef.current) {
        return;
      }
      const revisions = history.voRevisions;
      const selectedRevisionId = revisions.find((revision) => revision.voIsCurrent)?.voId ?? revisions[0]?.voId ?? null;

      setRevisionState({
        ...initialRevisionState,
        history,
        revisions,
        selectedRevision: null,
        selectedRevisionId,
        loading: false,
        loadingDetail: false,
        historyError: null,
        detailError: null,
        detailStale: false,
      });

      if (selectedRevisionId) {
        await loadRevisionDetail(selectedRevisionId, documentId);
      }
    } catch (error) {
      if (accountEpoch !== accountEpochRef.current ||
          requestEpoch !== revisionHistoryEpochRef.current ||
          documentId !== revisionDocumentIdRef.current) {
        return;
      }
      log.error('DocsAuthorApp', '加载文档修订列表失败:', error);
      setRevisionState((current) => preserveExisting
        ? {
            ...current,
            loading: false,
            historyError: getErrorMessage(error, t('wiki.author.feedback.loadRevisionsFailed')),
          }
        : {
            ...initialRevisionState,
            loading: false,
            historyError: getErrorMessage(error, t('wiki.author.feedback.loadRevisionsFailed')),
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
    authorListEpochRef.current += 1;
    revisionHistoryEpochRef.current += 1;
    revisionDetailEpochRef.current += 1;
    revisionComparisonEpochRef.current += 1;
    revisionDocumentIdRef.current = null;
    treeRef.current = [];
    authorListQueryRef.current = DEFAULT_AUTHOR_LIST_QUERY;
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

  useEffect(() => {
    if (route.kind !== 'revisions' || !revisionState.selectedRevisionId || revisionState.revisions.length === 0) {
      return;
    }

    void loadRevisionComparison(
      revisionState.selectedRevisionId,
      revisionState.comparisonMode,
      revisionState.revisions,
      route.documentId,
    );
  }, [
    loadRevisionComparison,
    revisionState.comparisonMode,
    revisionState.revisions,
    revisionState.selectedRevisionId,
    route,
  ]);

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

  const handleExternalAuthorNavigation = (event: MouseEvent<HTMLAnchorElement>) => {
    if (!shouldHandleAuthorLinkClick(event)) {
      return;
    }

    if (isEditorUploading) {
      event.preventDefault();
      return;
    }

    if (isEditorDirty && !window.confirm(t('wiki.author.editor.unsavedLeaveConfirm'))) {
      event.preventDefault();
      return;
    }

    if (isEditorDirty) {
      acknowledgeEditorDraft();
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

    if (route.kind === 'edit' && editorState.document?.voCanEdit !== true) {
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
        const createdDraft = createDraftFromDocument(created);
        setEditorState({
          draft: createdDraft,
          baselineDraft: createdDraft,
          document: created,
          loading: false,
          submitting: false,
          error: null,
          sortSuggestion: createdDraft.sort,
          conflict: null,
        });
        await loadCollections();
        if (accountEpoch !== accountEpochRef.current) {
          return;
        }
        navigateToRoute({ kind: 'edit', documentId: created.voDocumentId }, { skipConfirmation: true });
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
        baselineDraft: createDraftFromDocument(saved),
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

    if (isEditorDirty) {
      toast.error(t('wiki.author.editor.saveBeforeSubmit'));
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
          isDirty={isEditorDirty}
          isEditorUploading={isEditorUploading}
          onBack={(event) => handleRouteLinkClick(event, createDefaultDocsAuthorRoute())}
          onNavigate={handleRouteLinkClick}
          onExternalNavigate={handleExternalAuthorNavigation}
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
          protectedAttachments={protectedAttachments}
        />
      );
    }

    if (route.kind === 'revisions') {
      return (
        <DocsRevisionsPage
          state={revisionState}
          language={i18n.resolvedLanguage}
          onBack={(event) => handleRouteLinkClick(event, createDefaultDocsAuthorRoute())}
          onReload={() => void loadRevisions(route.documentId)}
          onSelectRevision={(revisionId) => {
            const selectedSummary = revisionState.revisions.find((revision) => isSameLongId(revision.voId, revisionId));
            if (selectedSummary?.voIsCurrent) {
              setRevisionState((current) => ({ ...current, comparisonMode: 'previous' }));
            }
            void loadRevisionDetail(revisionId, route.documentId);
          }}
          onComparisonModeChange={(comparisonMode) => {
            revisionComparisonEpochRef.current += 1;
            setRevisionState((current) => ({
              ...current,
              comparisonMode,
              comparisonRevision: null,
              comparisonRevisionId: null,
              loadingComparison: false,
              comparisonError: null,
              comparisonStale: false,
              comparisonMissing: false,
            }));
          }}
          onRetryRevision={() => revisionState.selectedRevisionId
            ? void loadRevisionDetail(revisionState.selectedRevisionId, route.documentId)
            : undefined}
          onRetryComparison={() => revisionState.selectedRevisionId
            ? void loadRevisionComparison(
                revisionState.selectedRevisionId,
                revisionState.comparisonMode,
                revisionState.revisions,
                route.documentId,
              )
            : undefined}
        />
      );
    }

    return (
      <DocsMinePage
        state={collectionState}
        language={i18n.resolvedLanguage}
        onReload={() => void loadCollections()}
        onQueryChange={(query) => void loadCollections(query)}
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

      <main className={`${styles.main} ${route.kind === 'compose' || route.kind === 'edit' ? editorStyles.editorMain : ''}`}>
        {route.kind !== 'compose' && route.kind !== 'edit' ? (
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
                className={styles.navItem}
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
                onClick={handleExternalAuthorNavigation}
                aria-disabled={isEditorUploading}
              >
                <Icon icon="mdi:book-open-page-variant-outline" size={18} />
                <span>{t('wiki.author.actions.publicReading')}</span>
              </a>
          </div>
        ) : null}

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
  actionOnClick?: () => void;
}

function StatusPanel({ icon, title, description, actionHref, actionLabel, actionOnClick }: StatusPanelProps) {
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
        actions={actionLabel ? [{ label: actionLabel, href: actionHref, onClick: actionOnClick }] : undefined}
      />
    </section>
  );
}
