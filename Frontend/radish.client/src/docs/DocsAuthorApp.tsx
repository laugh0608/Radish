import { useCallback, useEffect, useMemo, useState, type FormEvent, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
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
  normalizeAccessList,
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
import { WikiDocumentStatus, WikiDocumentVisibility } from '@/apps/wiki/types/wiki';
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
  buildDocsAuthorPath,
  createDefaultDocsAuthorRoute,
  parseDocsAuthorRoute,
  type DocsAuthorRoute,
} from './docsAuthorRouteState';
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

function resolveInitialDocsAuthorRoute(): DocsAuthorRoute {
  if (typeof window === 'undefined') {
    return createDefaultDocsAuthorRoute();
  }

  return parseDocsAuthorRoute(window.location.pathname) ?? createDefaultDocsAuthorRoute();
}

function shouldHandleAuthorLinkClick(event: MouseEvent<HTMLAnchorElement>): boolean {
  return !event.defaultPrevented
    && event.button === 0
    && !event.metaKey
    && !event.ctrlKey
    && !event.shiftKey
    && !event.altKey;
}

function canEditDocument(document: WikiDocumentDetailVo | null): boolean {
  return Boolean(document) && !isBuiltInWikiDocument(document) && document?.voIsDeleted !== true;
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

function toStatusText(status?: number): string {
  switch (status) {
    case WikiDocumentStatus.Published:
      return '已发布';
    case WikiDocumentStatus.Archived:
      return '已归档';
    default:
      return '草稿';
  }
}

function toVisibilityText(visibility?: number): string {
  switch (visibility) {
    case WikiDocumentVisibility.Public:
      return '公开';
    case WikiDocumentVisibility.Restricted:
      return '受限';
    default:
      return '登录可见';
  }
}

function toSourceText(sourceType?: string | null): string {
  switch ((sourceType || '').trim().toLowerCase()) {
    case 'manual':
      return '手动维护';
    case 'imported':
      return '导入';
    case 'custom':
      return '自定义';
    case 'builtin':
      return '内置';
    case 'rollback':
      return '回滚';
    default:
      return sourceType?.trim() || '未知';
  }
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

function validateDraft(draft: EditorDraft): string | null {
  if (!draft.title.trim() || !draft.markdownContent.trim()) {
    return '标题和正文不能为空';
  }

  const visibility = normalizeOptionalNumber(draft.visibility) ?? WikiDocumentVisibility.Authenticated;
  if (
    visibility === WikiDocumentVisibility.Restricted
    && normalizeAccessList(draft.allowedRoles).length === 0
    && normalizeAccessList(draft.allowedPermissions).length === 0
  ) {
    return '受限文档需要至少一个角色或权限';
  }

  return null;
}

export function DocsAuthorApp() {
  const { t, i18n } = useTranslation();
  const apiBaseUrl = useMemo(() => getApiBaseUrl(), []);
  const isAuthenticated = useAuthStore((state) => state.isAuthenticated);
  const userId = useUserStore((state) => state.userId);
  const roles = useUserStore((state) => state.roles || []);
  const loggedIn = isAuthenticated && userId.trim().length > 0;
  const canUseAuthorTools = useMemo(() => canUseDocsAuthorTools(roles), [roles]);
  const [route, setRoute] = useState<DocsAuthorRoute>(() => resolveInitialDocsAuthorRoute());
  const [authReady, setAuthReady] = useState(false);
  const [redirecting, setRedirecting] = useState(false);
  const [collectionState, setCollectionState] = useState<CollectionState>(initialCollectionState);
  const [editorState, setEditorState] = useState<EditorState>(initialEditorState);
  const [revisionState, setRevisionState] = useState<RevisionState>(initialRevisionState);

  const mineHref = buildDocsAuthorPath({ kind: 'mine' });
  const composeHref = buildDocsAuthorPath({ kind: 'compose' });

  const navigateToRoute = useCallback((nextRoute: DocsAuthorRoute, options?: { replace?: boolean }) => {
    const nextPath = buildDocsAuthorPath(nextRoute);
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    if (options?.replace) {
      window.history.replaceState(window.history.state, '', nextPath);
    } else if (currentPath !== nextPath) {
      window.history.pushState(window.history.state, '', nextPath);
    }

    setRoute(nextRoute);
  }, []);

  const loadCollections = useCallback(async () => {
    setCollectionState((current) => ({
      ...current,
      loading: true,
      error: null,
    }));

    try {
      const [tree, list] = await Promise.all([
        getWikiTree(),
        getWikiList({ pageIndex: 1, pageSize: 100 }),
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
        error: getErrorMessage(error, '加载文档列表失败'),
      }));
    }
  }, []);

  const loadEditor = useCallback(async (nextRoute: DocsAuthorRoute) => {
    if (nextRoute.kind === 'compose') {
      const draft = createDraftForCompose(collectionState.tree);
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
      const document = await getWikiDocumentById(nextRoute.documentId, true);
      const draft = createDraftFromDocument(document);
      setEditorState({
        draft,
        document,
        loading: false,
        submitting: false,
        error: null,
        sortSuggestion: String(getSuggestedSortValue(collectionState.tree, document.voParentId, document.voId)),
      });
    } catch (error) {
      log.error('DocsAuthorApp', '加载文档编辑详情失败:', error);
      setEditorState((current) => ({
        ...current,
        document: null,
        loading: false,
        submitting: false,
        error: getErrorMessage(error, '加载文档详情失败'),
      }));
    }
  }, [collectionState.tree]);

  const loadRevisionDetail = useCallback(async (revisionId: LongId) => {
    setRevisionState((current) => ({
      ...current,
      selectedRevisionId: revisionId,
      selectedRevision: null,
      loadingDetail: true,
      error: null,
    }));

    try {
      const detail = await getWikiRevisionDetail(revisionId);
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
        error: getErrorMessage(error, '加载修订详情失败'),
      }));
    }
  }, []);

  const loadRevisions = useCallback(async (documentId: LongId) => {
    setRevisionState({
      ...initialRevisionState,
      loading: true,
    });

    try {
      const [document, revisions] = await Promise.all([
        getWikiDocumentById(documentId, true),
        getWikiRevisionList(documentId),
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
        error: getErrorMessage(error, '加载修订列表失败'),
      });
    }
  }, [loadRevisionDetail]);

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
    const handlePopState = () => {
      setRoute(resolveInitialDocsAuthorRoute());
    };

    window.addEventListener('popstate', handlePopState);
    return () => {
      window.removeEventListener('popstate', handlePopState);
    };
  }, []);

  useEffect(() => {
    const canonicalPath = buildDocsAuthorPath(route);
    const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
    if (currentPath !== canonicalPath) {
      window.history.replaceState(window.history.state, '', canonicalPath);
    }
  }, [route]);

  useEffect(() => {
    document.title = '文档作者台 · Radish';
  }, []);

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
    navigateToRoute(nextRoute);
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

  const handleImageUpload = async (file: File): Promise<MarkdownImageUploadResult> => {
    const attachment = await uploadImage(
      {
        file,
        businessType: 'Wiki',
        generateThumbnail: true,
        removeExif: true,
      },
      t,
    );

    return {
      attachmentId: attachment.voId,
      displayVariant: 'original',
      previewUrl: buildAttachmentAssetUrl(attachment.voId, 'original'),
    };
  };

  const handleDocumentUpload = async (file: File): Promise<MarkdownDocumentUploadResult> => {
    const attachment = await uploadDocument(
      {
        file,
        businessType: 'Wiki',
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
    const validationMessage = validateDraft(editorState.draft);
    if (validationMessage) {
      toast.error(validationMessage);
      return;
    }

    if (route.kind === 'edit' && !canEditDocument(editorState.document)) {
      toast.error('当前文档不可在作者入口编辑');
      return;
    }

    setEditorState((current) => ({
      ...current,
      submitting: true,
    }));

    try {
      if (route.kind === 'compose') {
        const createdId = await createWikiDocument(buildCreateRequest(editorState.draft));
        toast.success('文档已创建');
        await loadCollections();
        navigateToRoute({ kind: 'edit', documentId: createdId });
        return;
      }

      if (route.kind !== 'edit') {
        return;
      }

      await updateWikiDocument(route.documentId, buildUpdateRequest(editorState.draft));
      toast.success('文档已保存');
      await Promise.all([
        loadCollections(),
        loadEditor(route),
      ]);
    } catch (error) {
      log.error('DocsAuthorApp', '保存文档失败:', error);
      toast.error(getErrorMessage(error, '保存文档失败'));
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
          title="正在确认登录状态"
          description="文档作者入口需要登录后继续，当前会保留目标页面并跳转登录。"
        />
      );
    }

    if (!canUseAuthorTools) {
      return (
        <StatusPanel
          icon="mdi:shield-alert-outline"
          title="当前账号没有文档作者权限"
          description="本阶段作者入口沿用 Wiki 写接口权限，只开放给 System 或 Admin 角色。"
          actionHref={buildPublicDocsPath({ kind: 'list' })}
          actionLabel="返回公开文档"
        />
      );
    }

    if (route.kind === 'compose' || route.kind === 'edit') {
      return (
        <DocsEditorPage
          route={route}
          tree={collectionState.tree}
          state={editorState}
          onBack={(event) => handleRouteLinkClick(event, createDefaultDocsAuthorRoute())}
          onParentChange={handleParentChange}
          onSetDraft={setDraft}
          onSave={handleSave}
          onImageUpload={handleImageUpload}
          onDocumentUpload={handleDocumentUpload}
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
        brandMark="文"
        brandName="文档作者台"
        brandSubline="正式 Web 文档写作入口"
        onBrandClick={() => navigateToRoute(createDefaultDocsAuthorRoute())}
        onNavigateToDiscover={() => { window.location.href = buildPublicDocsPath({ kind: 'list' }); }}
        discoverHref={buildPublicDocsPath({ kind: 'list' })}
        discoverLabel="公开文档"
        showCircleAction={false}
        desktopLabel="工作台"
      />

      <main className={styles.main}>
        <div className={styles.navBar}>
          <a
            className={route.kind === 'mine' ? styles.navItemActive : styles.navItem}
            href={mineHref}
            onClick={(event) => handleRouteLinkClick(event, { kind: 'mine' })}
          >
            <Icon icon="mdi:file-document-multiple-outline" size={18} />
            <span>我的文档</span>
          </a>
          <a
            className={route.kind === 'compose' ? styles.navItemActive : styles.navItem}
            href={composeHref}
            onClick={(event) => handleRouteLinkClick(event, { kind: 'compose' })}
          >
            <Icon icon="mdi:plus-box-outline" size={18} />
            <span>新建文档</span>
          </a>
          <a className={styles.navItem} href={buildPublicDocsPath({ kind: 'list' })}>
            <Icon icon="mdi:book-open-page-variant-outline" size={18} />
            <span>公开阅读</span>
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
  return (
    <section className={styles.statusPanel}>
      <span className={styles.statusIcon}>
        <Icon icon={icon} size={24} />
      </span>
      <div className={styles.statusBody}>
        <h1 className={styles.statusTitle}>{title}</h1>
        <p className={styles.statusDescription}>{description}</p>
        {actionHref && actionLabel ? (
          <a className={styles.secondaryButton} href={actionHref}>
            {actionLabel}
          </a>
        ) : null}
      </div>
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
  const hasDocuments = state.documents.length > 0;

  return (
    <section className={styles.panel}>
      <div className={styles.panelHeader}>
        <div>
          <p className={styles.kicker}>Author Workspace</p>
          <h1 className={styles.pageTitle}>文档作者台</h1>
          <p className={styles.pageIntro}>集中处理文档草稿、内容修订和公开阅读入口，不承载发布与治理动作。</p>
        </div>
        <div className={styles.headerActions}>
          <a
            className={styles.primaryButton}
            href={buildDocsAuthorPath({ kind: 'compose' })}
            onClick={(event) => onNavigate(event, { kind: 'compose' })}
          >
            <Icon icon="mdi:plus" size={18} />
            <span>新建文档</span>
          </a>
          <button type="button" className={styles.secondaryButton} onClick={onReload} disabled={state.loading}>
            <Icon icon={state.loading ? 'mdi:progress-clock' : 'mdi:refresh'} size={18} />
            <span>{state.loading ? '刷新中' : '刷新'}</span>
          </button>
        </div>
      </div>

      <div className={styles.summaryGrid}>
        <SummaryTile label="目录节点" value={state.tree.length} />
        <SummaryTile label="文档总数" value={state.totalDocuments} />
        <SummaryTile label="本页加载" value={state.documents.length} />
      </div>

      {state.error ? (
        <StatusPanel
          icon="mdi:alert-circle-outline"
          title="文档列表加载失败"
          description={state.error}
        />
      ) : state.loading && !hasDocuments ? (
        <StatusPanel
          icon="mdi:progress-clock"
          title="正在加载文档"
          description="正在读取 Wiki 目录和文档列表。"
        />
      ) : !hasDocuments ? (
        <StatusPanel
          icon="mdi:file-document-outline"
          title="暂无可维护文档"
          description="可以先创建一篇登录可见文档，发布与权限治理留在 Console 管理批次处理。"
          actionHref={buildDocsAuthorPath({ kind: 'compose' })}
          actionLabel="新建文档"
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
  );
}

interface SummaryTileProps {
  label: string;
  value: number;
}

function SummaryTile({ label, value }: SummaryTileProps) {
  return (
    <div className={styles.summaryTile}>
      <span className={styles.summaryLabel}>{label}</span>
      <strong className={styles.summaryValue}>{value.toLocaleString()}</strong>
    </div>
  );
}

interface DocumentRowProps {
  document: WikiDocumentVo;
  language?: string;
  onNavigate: (event: MouseEvent<HTMLAnchorElement>, route: DocsAuthorRoute) => void;
}

function DocumentRow({ document, language, onNavigate }: DocumentRowProps) {
  const editRoute: DocsAuthorRoute = { kind: 'edit', documentId: document.voId };
  const revisionsRoute: DocsAuthorRoute = { kind: 'revisions', documentId: document.voId };
  const publicHref = buildPublicDocsPath({ kind: 'detail', slug: document.voSlug });
  const canEdit = !isBuiltInWikiDocument(document) && !document.voIsDeleted;

  return (
    <article className={styles.documentRow}>
      <div className={styles.documentMain}>
        <div className={styles.metaRow}>
          <span className={styles.statusChip}>{document.voIsDeleted ? '已删除' : toStatusText(document.voStatus)}</span>
          <span className={styles.metaChip}>{toVisibilityText(document.voVisibility)}</span>
          <span className={styles.metaChip}>{toSourceText(document.voSourceType)}</span>
          <span className={styles.metaChip}>v{document.voVersion}</span>
        </div>
        <h2 className={styles.documentTitle}>{document.voTitle}</h2>
        <p className={styles.documentSummary}>
          {document.voSummary?.trim() || '没有摘要'}
        </p>
        <div className={styles.documentMeta}>
          <span>slug: {document.voSlug}</span>
          <span>更新: {formatWikiTime(document.voModifyTime || document.voCreateTime, language)}</span>
        </div>
      </div>
      <div className={styles.documentActions}>
        {canEdit ? (
          <a
            className={styles.primaryButton}
            href={buildDocsAuthorPath(editRoute)}
            onClick={(event) => onNavigate(event, editRoute)}
          >
            编辑
          </a>
        ) : null}
        <a
          className={styles.secondaryButton}
          href={buildDocsAuthorPath(revisionsRoute)}
          onClick={(event) => onNavigate(event, revisionsRoute)}
        >
          修订
        </a>
        <a className={styles.secondaryButton} href={publicHref}>
          阅读
        </a>
      </div>
    </article>
  );
}

interface DocsEditorPageProps {
  route: DocsAuthorRoute & ({ kind: 'compose' } | { kind: 'edit' });
  tree: WikiDocumentTreeNodeVo[];
  state: EditorState;
  onBack: (event: MouseEvent<HTMLAnchorElement>) => void;
  onParentChange: (parentId: string) => void;
  onSetDraft: (updater: (current: EditorDraft) => EditorDraft) => void;
  onSave: (event: FormEvent<HTMLFormElement>) => void;
  onImageUpload: (file: File) => Promise<MarkdownImageUploadResult>;
  onDocumentUpload: (file: File) => Promise<MarkdownDocumentUploadResult>;
}

function DocsEditorPage({
  route,
  tree,
  state,
  onBack,
  onParentChange,
  onSetDraft,
  onSave,
  onImageUpload,
  onDocumentUpload,
}: DocsEditorPageProps) {
  const parentOptions = useMemo(
    () => buildParentOptions(tree, route.kind === 'edit' ? route.documentId : null),
    [route, tree]
  );
  const readOnly = route.kind === 'edit' && !canEditDocument(state.document);
  const pageTitle = route.kind === 'compose' ? '新建文档' : state.document?.voTitle || '编辑文档';
  const pageIntro = route.kind === 'compose'
    ? '默认创建登录可见草稿，发布、归档和权限治理留在 Console。'
    : '编辑正文、摘要和目录位置；当前入口不处理发布状态切换。';

  if (state.loading) {
    return (
      <StatusPanel
        icon="mdi:progress-clock"
        title="正在加载编辑信息"
        description="正在读取文档详情和目录位置。"
      />
    );
  }

  if (state.error) {
    return (
      <StatusPanel
        icon="mdi:alert-circle-outline"
        title="编辑信息加载失败"
        description={state.error}
      />
    );
  }

  return (
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
            onClick={onBack}
          >
            <Icon icon="mdi:arrow-left" size={18} />
            <span>返回列表</span>
          </a>
          {route.kind === 'edit' ? (
            <a
              className={styles.secondaryButton}
              href={buildDocsAuthorPath({ kind: 'revisions', documentId: route.documentId })}
            >
              <Icon icon="mdi:history" size={18} />
              <span>修订记录</span>
            </a>
          ) : null}
        </div>
      </div>

      {readOnly && state.document ? (
        <div className={styles.inlineNotice}>
          <Icon icon="mdi:lock-outline" size={20} />
          <span>
            当前文档来自 {toSourceText(state.document.voSourceType)}
            {state.document.voIsDeleted ? '，且已删除' : ''}，作者入口保持只读。
          </span>
        </div>
      ) : null}

      <form className={styles.editorForm} onSubmit={onSave}>
        <div className={styles.formGrid}>
          <label className={styles.field}>
            <span>标题</span>
            <input
              className={styles.input}
              value={state.draft.title}
              disabled={readOnly || state.submitting}
              onChange={(event) => onSetDraft((current) => ({ ...current, title: event.target.value }))}
              placeholder="文档标题"
            />
          </label>
          <label className={styles.field}>
            <span>Slug</span>
            <input
              className={styles.input}
              value={state.draft.slug}
              disabled={readOnly || state.submitting}
              onChange={(event) => onSetDraft((current) => ({ ...current, slug: event.target.value }))}
              placeholder="留空时由后端生成"
            />
          </label>
          <label className={styles.field}>
            <span>上级文档</span>
            <select
              className={styles.select}
              value={state.draft.parentId}
              disabled={readOnly || state.submitting}
              onChange={(event) => onParentChange(event.target.value)}
            >
              <option value="">根目录</option>
              {parentOptions.map((option) => (
                <option key={option.id} value={String(option.id)}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>
          <label className={styles.field}>
            <span>排序</span>
            <input
              className={styles.input}
              value={state.draft.sort}
              disabled={readOnly || state.submitting}
              onChange={(event) => onSetDraft((current) => ({ ...current, sort: event.target.value }))}
            />
          </label>
          <label className={styles.field}>
            <span>封面附件 ID</span>
            <input
              className={styles.input}
              value={state.draft.coverAttachmentId}
              disabled={readOnly || state.submitting}
              onChange={(event) => onSetDraft((current) => ({ ...current, coverAttachmentId: event.target.value }))}
              placeholder="可选"
            />
          </label>
          {route.kind === 'edit' ? (
            <label className={styles.field}>
              <span>变更摘要</span>
              <input
                className={styles.input}
                value={state.draft.changeSummary}
                disabled={readOnly || state.submitting}
                onChange={(event) => onSetDraft((current) => ({ ...current, changeSummary: event.target.value }))}
                placeholder="本次修改说明"
              />
            </label>
          ) : null}
          <label className={`${styles.field} ${styles.fieldFull}`}>
            <span>摘要</span>
            <textarea
              className={styles.textarea}
              value={state.draft.summary}
              disabled={readOnly || state.submitting}
              onChange={(event) => onSetDraft((current) => ({ ...current, summary: event.target.value }))}
              placeholder="用于公开阅读页和列表摘要"
            />
          </label>
        </div>

        <MarkdownEditor
          value={state.draft.markdownContent}
          onChange={(value) => onSetDraft((current) => ({ ...current, markdownContent: value }))}
          minHeight={420}
          disabled={readOnly || state.submitting}
          placeholder="输入 Markdown 正文"
          onImageUpload={onImageUpload}
          onDocumentUpload={onDocumentUpload}
        />

        <div className={styles.editorActions}>
          <span className={styles.editorHint}>
            当前可见性: {toVisibilityText(normalizeOptionalNumber(state.draft.visibility))}
          </span>
          <button type="submit" className={styles.primaryButton} disabled={readOnly || state.submitting}>
            <Icon icon={state.submitting ? 'mdi:progress-clock' : 'mdi:content-save-outline'} size={18} />
            <span>{state.submitting ? '保存中' : '保存文档'}</span>
          </button>
        </div>
      </form>
    </section>
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
  return (
    <section className={styles.panel}>
      <div className={styles.panelHeader}>
        <div>
          <p className={styles.kicker}>Revision History</p>
          <h1 className={styles.pageTitle}>{state.document?.voTitle || '修订记录'}</h1>
          <p className={styles.pageIntro}>查看历史快照内容，不在作者入口执行回滚治理动作。</p>
        </div>
        <div className={styles.headerActions}>
          <a className={styles.secondaryButton} href={buildDocsAuthorPath({ kind: 'mine' })} onClick={onBack}>
            <Icon icon="mdi:arrow-left" size={18} />
            <span>返回列表</span>
          </a>
          {state.document && canEditDocument(state.document) ? (
            <a
              className={styles.primaryButton}
              href={buildDocsAuthorPath({ kind: 'edit', documentId: state.document.voId })}
              onClick={(event) => onEdit(event, state.document!.voId)}
            >
              <Icon icon="mdi:pencil-outline" size={18} />
              <span>编辑文档</span>
            </a>
          ) : null}
        </div>
      </div>

      {state.loading ? (
        <StatusPanel
          icon="mdi:progress-clock"
          title="正在加载修订记录"
          description="正在读取文档版本历史。"
        />
      ) : state.error && state.revisions.length === 0 ? (
        <StatusPanel
          icon="mdi:alert-circle-outline"
          title="修订记录加载失败"
          description={state.error}
        />
      ) : state.revisions.length === 0 ? (
        <StatusPanel
          icon="mdi:history"
          title="暂无修订记录"
          description="当前文档还没有可查看的历史快照。"
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
                <span className={styles.revisionSummary}>{revision.voChangeSummary || '无变更摘要'}</span>
                <span className={styles.revisionMeta}>
                  {formatWikiTime(revision.voCreateTime, language)} · {revision.voCreateBy}
                </span>
                {revision.voIsCurrent ? <span className={styles.statusChip}>当前</span> : null}
              </button>
            ))}
          </aside>

          <article className={styles.revisionPreview}>
            {state.loadingDetail ? (
              <StatusPanel
                icon="mdi:progress-clock"
                title="正在加载版本内容"
                description="正在读取选中的修订快照。"
              />
            ) : state.selectedRevision ? (
              <>
                <div className={styles.revisionPreviewHeader}>
                  <div>
                    <h2 className={styles.revisionPreviewTitle}>v{state.selectedRevision.voVersion}</h2>
                    <p className={styles.revisionMeta}>{state.selectedRevision.voTitle}</p>
                  </div>
                  <div className={styles.metaRow}>
                    <span className={styles.metaChip}>{toSourceText(state.selectedRevision.voSourceType)}</span>
                    <span className={styles.metaChip}>{formatWikiTime(state.selectedRevision.voCreateTime, language)}</span>
                  </div>
                </div>
                <MarkdownRenderer content={state.selectedRevision.voMarkdownContent} className={styles.markdownContent} />
              </>
            ) : (
              <StatusPanel
                icon="mdi:file-search-outline"
                title="请选择一个修订版本"
                description="从左侧版本列表选择后查看快照内容。"
              />
            )}
          </article>
        </div>
      )}
    </section>
  );
}
