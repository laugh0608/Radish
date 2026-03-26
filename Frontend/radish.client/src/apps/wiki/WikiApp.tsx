import { useCallback, useEffect, useMemo, useRef, useState, type ChangeEvent, type MouseEvent, type ReactNode } from 'react';
import { ConfirmDialog } from '@radish/ui/confirm-dialog';
import { toast } from '@radish/ui/toast';
import { MarkdownEditor } from '@radish/ui/markdown-editor';
import { MarkdownRenderer } from '@radish/ui/markdown-renderer';
import { Modal } from '@radish/ui/modal';
import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import { uploadDocument, uploadImage } from '@/api/attachment';
import { useCurrentWindow } from '@/desktop/useCurrentWindow';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import { log } from '@/utils/logger';
import {
  archiveWikiDocument,
  createWikiDocument,
  deleteWikiDocument,
  downloadWikiMarkdown,
  getWikiDocumentById,
  getWikiDocumentBySlug,
  getWikiList,
  getWikiRevisionDetail,
  getWikiRevisionList,
  getWikiTree,
  importWikiMarkdown,
  publishWikiDocument,
  restoreWikiDocument,
  rollbackWikiRevision,
  unpublishWikiDocument,
  updateWikiDocument,
} from './api/wiki';
import type {
  CreateWikiDocumentRequest,
  UpdateWikiDocumentRequest,
  WikiDocumentDetailVo,
  WikiDocumentRevisionDetailVo,
  WikiDocumentRevisionItemVo,
  WikiDocumentTreeNodeVo,
  WikiDocumentVo,
} from './types/wiki';
import { WikiDocumentStatus, WikiDocumentVisibility } from './types/wiki';
import {
  collectDescendantIds,
  flattenTree,
  flattenTreeOptions,
  getSuggestedSortValue,
  SORT_PRESET_VALUES,
} from './wikiApp.helpers';
import styles from './WikiApp.module.css';

type EditorMode = 'create' | 'edit';
type DeletionFilter = 'active' | 'deleted';
type SidebarView = 'tree' | 'results';

type EditorDraft = {
  title: string;
  slug: string;
  summary: string;
  markdownContent: string;
  parentId: string;
  sort: string;
  coverAttachmentId: string;
  changeSummary: string;
  visibility: string;
  allowedRoles: string;
  allowedPermissions: string;
};

const EMPTY_DRAFT: EditorDraft = {
  title: '',
  slug: '',
  summary: '',
  markdownContent: '',
  parentId: '',
  sort: '0',
  coverAttachmentId: '',
  changeSummary: '',
  visibility: String(WikiDocumentVisibility.Authenticated),
  allowedRoles: '',
  allowedPermissions: '',
};

function toStatusText(t: TFunction, status: number): string {
  switch (status) {
    case WikiDocumentStatus.Published:
      return t('wiki.status.published');
    case WikiDocumentStatus.Archived:
      return t('wiki.status.archived');
    default:
      return t('wiki.status.draft');
  }
}

function toStatusClassName(status: number): string {
  switch (status) {
    case WikiDocumentStatus.Published:
      return styles.statusPublished;
    case WikiDocumentStatus.Archived:
      return styles.statusArchived;
    default:
      return styles.statusDraft;
  }
}

function toVisibilityText(t: TFunction, visibility?: number): string {
  switch (visibility) {
    case WikiDocumentVisibility.Public:
      return t('wiki.visibility.public');
    case WikiDocumentVisibility.Restricted:
      return t('wiki.visibility.restricted');
    default:
      return t('wiki.visibility.authenticated');
  }
}

function toSourceText(t: TFunction, sourceType?: string | null): string {
  switch ((sourceType || '').trim().toLowerCase()) {
    case 'manual':
      return t('wiki.source.manual');
    case 'imported':
      return t('wiki.source.imported');
    case 'custom':
      return t('wiki.source.custom');
    case 'builtin':
      return t('wiki.source.builtin');
    case 'rollback':
      return t('wiki.source.rollback');
    default:
      return sourceType?.trim() || t('wiki.source.unknown');
  }
}

function resolveDateLocale(language?: string): string {
  return language?.toLowerCase().startsWith('zh') ? 'zh-CN' : 'en-US';
}

function formatTime(value: string | null | undefined, language?: string): string {
  if (!value) {
    return '--';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString(resolveDateLocale(language), {
    hour12: false,
  });
}

function pickInitialDocumentId(tree: WikiDocumentTreeNodeVo[], list: WikiDocumentVo[]): number | null {
  if (list.length > 0) {
    return list[0].voId;
  }

  if (tree.length > 0) {
    return tree[0].voId;
  }

  return null;
}

function normalizeOptionalString(value: string): string | undefined {
  const trimmed = value.trim();
  return trimmed ? trimmed : undefined;
}

function normalizeOptionalNumber(value: string): number | undefined {
  const trimmed = value.trim();
  if (!trimmed) {
    return undefined;
  }

  const parsed = Number(trimmed);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function normalizeAccessList(value: string): string[] {
  return value
    .split(/[\n,，]/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function parseWikiWindowParams(appParams?: Record<string, unknown> | null): { documentId?: number; slug?: string } {
  if (!appParams) {
    return {};
  }

  const rawDocumentId = typeof appParams.documentId === 'number'
    ? appParams.documentId
    : typeof appParams.documentId === 'string'
      ? Number(appParams.documentId)
      : 0;
  const slug = typeof appParams.slug === 'string' ? appParams.slug.trim() : '';

  return {
    documentId: Number.isFinite(rawDocumentId) && rawDocumentId > 0 ? rawDocumentId : undefined,
    slug: slug || undefined,
  };
}

function buildCreateRequest(draft: EditorDraft): CreateWikiDocumentRequest {
  return {
    title: draft.title.trim(),
    slug: normalizeOptionalString(draft.slug),
    summary: normalizeOptionalString(draft.summary),
    markdownContent: draft.markdownContent,
    parentId: normalizeOptionalNumber(draft.parentId),
    sort: normalizeOptionalNumber(draft.sort) ?? 0,
    coverAttachmentId: normalizeOptionalNumber(draft.coverAttachmentId),
    visibility: normalizeOptionalNumber(draft.visibility) ?? WikiDocumentVisibility.Authenticated,
    allowedRoles: normalizeAccessList(draft.allowedRoles),
    allowedPermissions: normalizeAccessList(draft.allowedPermissions),
  };
}

function buildUpdateRequest(draft: EditorDraft): UpdateWikiDocumentRequest {
  return {
    ...buildCreateRequest(draft),
    changeSummary: normalizeOptionalString(draft.changeSummary),
  };
}

function describeRevisionSummary(t: TFunction, revision: WikiDocumentRevisionItemVo): string {
  if (revision.voChangeSummary?.trim()) {
    return revision.voChangeSummary;
  }

  return revision.voIsCurrent ? t('wiki.revision.currentSnapshot') : t('wiki.revision.noChangeSummary');
}

function collectExpandableNodeIds(nodes: WikiDocumentTreeNodeVo[]): number[] {
  return nodes.flatMap((node) => {
    const children = node.voChildren || [];
    return children.length > 0
      ? [node.voId, ...collectExpandableNodeIds(children)]
      : collectExpandableNodeIds(children);
  });
}

function findAncestorIds(nodes: WikiDocumentTreeNodeVo[], targetId: number, trail: number[] = []): number[] {
  for (const node of nodes) {
    if (node.voId === targetId) {
      return trail;
    }

    const nextTrail = [...trail, node.voId];
    const result = findAncestorIds(node.voChildren || [], targetId, nextTrail);
    if (result.length > 0) {
      return result;
    }
  }

  return [];
}

export const WikiApp = () => {
  const { t, i18n } = useTranslation();
  const currentWindow = useCurrentWindow();
  const loggedIn = useAuthStore((state) => state.isAuthenticated);
  const roles = useUserStore((state) => state.roles || []);
  const windowParams = useMemo(() => parseWikiWindowParams(currentWindow?.appParams), [currentWindow?.appParams]);
  const initialWindowRouteRef = useRef<{ documentId?: number; slug?: string } | null>(
    windowParams.documentId || windowParams.slug ? windowParams : null
  );
  const isAdmin = useMemo(
    () => roles.some((role) => ['admin', 'system'].includes(role.trim().toLowerCase())),
    [roles]
  );

  const [tree, setTree] = useState<WikiDocumentTreeNodeVo[]>([]);
  const [documents, setDocuments] = useState<WikiDocumentVo[]>([]);
  const [totalResultsCount, setTotalResultsCount] = useState(0);
  const [selectedDocumentId, setSelectedDocumentId] = useState<number | null>(null);
  const [selectedDocument, setSelectedDocument] = useState<WikiDocumentDetailVo | null>(null);
  const [revisionList, setRevisionList] = useState<WikiDocumentRevisionItemVo[]>([]);
  const [selectedRevisionId, setSelectedRevisionId] = useState<number | null>(null);
  const [selectedRevision, setSelectedRevision] = useState<WikiDocumentRevisionDetailVo | null>(null);
  const [keyword, setKeyword] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('');
  const [deletionFilter, setDeletionFilter] = useState<DeletionFilter>('active');
  const [loadingTree, setLoadingTree] = useState(false);
  const [loadingList, setLoadingList] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [loadingRevisionList, setLoadingRevisionList] = useState(false);
  const [loadingRevisionDetail, setLoadingRevisionDetail] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [editorVisible, setEditorVisible] = useState(false);
  const [editorMode, setEditorMode] = useState<EditorMode>('create');
  const [draft, setDraft] = useState<EditorDraft>(EMPTY_DRAFT);
  const [rollbackConfirmOpen, setRollbackConfirmOpen] = useState(false);
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false);
  const [historyModalOpen, setHistoryModalOpen] = useState(false);
  const [sidebarView, setSidebarView] = useState<SidebarView>('tree');
  const [expandedNodeIds, setExpandedNodeIds] = useState<Set<number>>(new Set());
  const [isCompactLayout, setIsCompactLayout] = useState(false);
  const [sortSuggestion, setSortSuggestion] = useState(EMPTY_DRAFT.sort);

  const containerRef = useRef<HTMLDivElement | null>(null);
  const importInputRef = useRef<HTMLInputElement | null>(null);
  const initialLoadedRef = useRef(false);
  const selectedDocumentIdRef = useRef<number | null>(null);
  const selectedRevisionIdRef = useRef<number | null>(null);
  const activeDocumentIds = useMemo(() => new Set(flattenTree(tree)), [tree]);
  const treeOptions = useMemo(() => flattenTreeOptions(tree), [tree]);
  const expandableNodeIds = useMemo(() => new Set(collectExpandableNodeIds(tree)), [tree]);
  const showingDeleted = isAdmin && deletionFilter === 'deleted';
  const totalTreeDocuments = activeDocumentIds.size;
  const hasActiveFilters = keyword.trim().length > 0 || statusFilter !== '' || showingDeleted;
  const currentListCount = documents.length;
  const totalResultsText = totalResultsCount > 0 ? String(totalResultsCount) : '0';

  const isBuiltInDocument = useMemo(
    () => selectedDocument?.voSourceType?.trim().toLowerCase() === 'builtin',
    [selectedDocument]
  );

  const isDeletedDocument = Boolean(selectedDocument?.voIsDeleted);
  const canEditSelectedDocument = isAdmin && Boolean(selectedDocument) && !isBuiltInDocument && !isDeletedDocument;
  const canRestoreSelectedDocument = isAdmin && Boolean(selectedDocument) && !isBuiltInDocument && isDeletedDocument;
  const invalidParentIds = useMemo(() => {
    if (!selectedDocumentId) {
      return new Set<number>();
    }

    const ids = collectDescendantIds(tree, selectedDocumentId);
    ids.add(selectedDocumentId);
    return ids;
  }, [selectedDocumentId, tree]);

  const refreshCollections = useCallback(async (preserveSelection: boolean = true) => {
    setLoadingTree(true);
    setLoadingList(true);

    try {
      const [treeData, pageData] = await Promise.all([
        getWikiTree(),
        getWikiList({
          pageIndex: 1,
          pageSize: 100,
          keyword: keyword.trim() || undefined,
          status: isAdmin && statusFilter !== '' ? Number(statusFilter) : undefined,
          includeDeleted: showingDeleted,
          deletedOnly: showingDeleted,
        }),
      ]);

      setTree(treeData);
      setDocuments(pageData.data || []);
      setTotalResultsCount(pageData.dataCount || pageData.data?.length || 0);

      if (!preserveSelection || !selectedDocumentIdRef.current) {
        setSelectedDocumentId(pickInitialDocumentId(treeData, pageData.data || []));
        return;
      }

      const hasInTree = flattenTree(treeData).includes(selectedDocumentIdRef.current);
      const hasInList = (pageData.data || []).some((item) => item.voId === selectedDocumentIdRef.current);

      if (!hasInTree && !hasInList) {
        setSelectedDocumentId(pickInitialDocumentId(treeData, pageData.data || []));
      }
    } catch (error) {
      log.error('WikiApp', '加载 Wiki 列表失败:', error);
      setTotalResultsCount(0);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.loadListFailed'));
    } finally {
      setLoadingTree(false);
      setLoadingList(false);
    }
  }, [isAdmin, keyword, showingDeleted, statusFilter, t]);

  const loadDocumentDetail = useCallback(async (documentId: number) => {
    setLoadingDetail(true);

    try {
      const detail = await getWikiDocumentById(documentId, showingDeleted);
      setSelectedDocument(detail);
    } catch (error) {
      log.error('WikiApp', '加载文档详情失败:', error);
      setSelectedDocument(null);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.loadDetailFailed'));
    } finally {
      setLoadingDetail(false);
    }
  }, [showingDeleted, t]);

  const loadDocumentBySlug = useCallback(async (slug: string) => {
    const normalizedSlug = slug.trim();
    if (!normalizedSlug) {
      return;
    }

    try {
      const detail = await getWikiDocumentBySlug(normalizedSlug);
      setSelectedDocumentId(detail.voId);
      setSelectedDocument(detail);
    } catch (error) {
      log.error('WikiApp', '按 Slug 加载文档失败:', error);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.loadDocumentFailed'));
    }
  }, [t]);

  const loadRevisionList = useCallback(async (documentId: number, preserveSelection: boolean = true) => {
    if (!isAdmin) {
      setRevisionList([]);
      setSelectedRevisionId(null);
      setSelectedRevision(null);
      return;
    }

    setLoadingRevisionList(true);

    try {
      const revisions = await getWikiRevisionList(documentId);
      setRevisionList(revisions);

      if (revisions.length === 0) {
        setSelectedRevisionId(null);
        setSelectedRevision(null);
        return;
      }

      if (
        preserveSelection &&
        selectedRevisionIdRef.current &&
        revisions.some((item) => item.voId === selectedRevisionIdRef.current)
      ) {
        return;
      }

      setSelectedRevisionId(revisions[0].voId);
    } catch (error) {
      log.error('WikiApp', '加载版本历史失败:', error);
      setRevisionList([]);
      setSelectedRevisionId(null);
      setSelectedRevision(null);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.loadRevisionListFailed'));
    } finally {
      setLoadingRevisionList(false);
    }
  }, [isAdmin, t]);

  const loadRevisionDetail = useCallback(async (revisionId: number) => {
    if (!isAdmin) {
      setSelectedRevision(null);
      return;
    }

    setLoadingRevisionDetail(true);

    try {
      const detail = await getWikiRevisionDetail(revisionId);
      setSelectedRevision(detail);
    } catch (error) {
      log.error('WikiApp', '加载版本详情失败:', error);
      setSelectedRevision(null);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.loadRevisionDetailFailed'));
    } finally {
      setLoadingRevisionDetail(false);
    }
  }, [isAdmin, t]);

  useEffect(() => {
    if (initialLoadedRef.current) {
      return;
    }

    initialLoadedRef.current = true;
    void refreshCollections(false);
  }, [refreshCollections]);

  useEffect(() => {
    if (!selectedDocumentId) {
      setSelectedDocument(null);
      setRevisionList([]);
      setSelectedRevisionId(null);
      setSelectedRevision(null);
      return;
    }

    void loadDocumentDetail(selectedDocumentId);
    void loadRevisionList(selectedDocumentId, false);
  }, [loadDocumentDetail, loadRevisionList, selectedDocumentId]);

  useEffect(() => {
    const initialWindowRoute = initialWindowRouteRef.current;
    if (!initialWindowRoute) {
      return;
    }

    initialWindowRouteRef.current = null;

    if (initialWindowRoute.documentId) {
      setSelectedDocumentId(initialWindowRoute.documentId);
      return;
    }

    if (initialWindowRoute.slug) {
      void loadDocumentBySlug(initialWindowRoute.slug);
    }
  }, [loadDocumentBySlug]);

  useEffect(() => {
    selectedDocumentIdRef.current = selectedDocumentId;
  }, [selectedDocumentId]);

  useEffect(() => {
    if (!selectedRevisionId || !isAdmin) {
      setSelectedRevision(null);
      return;
    }

    void loadRevisionDetail(selectedRevisionId);
  }, [isAdmin, loadRevisionDetail, selectedRevisionId]);

  useEffect(() => {
    selectedRevisionIdRef.current = selectedRevisionId;
  }, [selectedRevisionId]);

  useEffect(() => {
    if (editorVisible || !canEditSelectedDocument) {
      setHistoryModalOpen(false);
    }
  }, [canEditSelectedDocument, editorVisible]);

  useEffect(() => {
    setSidebarView(hasActiveFilters ? 'results' : 'tree');
  }, [hasActiveFilters]);

  useEffect(() => {
    if (!initialLoadedRef.current || !isAdmin) {
      return;
    }

    setSidebarView(showingDeleted ? 'results' : 'tree');
    void refreshCollections(false);
  }, [deletionFilter, isAdmin, refreshCollections, showingDeleted]);

  useEffect(() => {
    const container = containerRef.current;
    if (!container || typeof ResizeObserver === 'undefined') {
      return;
    }

    const updateLayout = (width: number) => {
      setIsCompactLayout(width < 1080);
    };

    updateLayout(container.clientWidth);

    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (!entry) {
        return;
      }

      updateLayout(entry.contentRect.width);
    });

    observer.observe(container);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    setExpandedNodeIds((current) => {
      const next = new Set<number>();

      current.forEach((id) => {
        if (expandableNodeIds.has(id)) {
          next.add(id);
        }
      });

      if (next.size === 0) {
        tree.forEach((node) => {
          if ((node.voChildren || []).length > 0) {
            next.add(node.voId);
          }
        });
      }

      if (selectedDocumentId) {
        findAncestorIds(tree, selectedDocumentId).forEach((id) => {
          if (expandableNodeIds.has(id)) {
            next.add(id);
          }
        });
      }

      return next;
    });
  }, [expandableNodeIds, selectedDocumentId, tree]);

  const openCreateEditor = () => {
    const parentId = selectedDocument && !selectedDocument.voIsDeleted ? String(selectedDocument.voId) : '';
    const suggestedSort = String(getSuggestedSortValue(tree, normalizeOptionalNumber(parentId)));

    setEditorMode('create');
    setDraft({
      ...EMPTY_DRAFT,
      parentId,
      sort: suggestedSort,
    });
    setSortSuggestion(suggestedSort);
    setEditorVisible(true);
  };

  const openEditEditor = () => {
    if (!selectedDocument) {
      return;
    }

    const suggestedSort = String(getSuggestedSortValue(tree, selectedDocument.voParentId, selectedDocument.voId));

    setEditorMode('edit');
    setDraft({
      title: selectedDocument.voTitle,
      slug: selectedDocument.voSlug,
      summary: selectedDocument.voSummary || '',
      markdownContent: selectedDocument.voMarkdownContent,
      parentId: selectedDocument.voParentId != null ? String(selectedDocument.voParentId) : '',
      sort: String(selectedDocument.voSort ?? 0),
      coverAttachmentId: selectedDocument.voCoverAttachmentId != null ? String(selectedDocument.voCoverAttachmentId) : '',
      changeSummary: '',
      visibility: String(selectedDocument.voVisibility ?? WikiDocumentVisibility.Authenticated),
      allowedRoles: (selectedDocument.voAllowedRoles || []).join(', '),
      allowedPermissions: (selectedDocument.voAllowedPermissions || []).join(', '),
    });
    setSortSuggestion(suggestedSort);
    setEditorVisible(true);
  };

  const closeEditor = () => {
    setEditorVisible(false);
    setDraft(EMPTY_DRAFT);
    setSortSuggestion(EMPTY_DRAFT.sort);
  };

  const openHistoryModal = () => {
    if (!selectedDocumentId || !canEditSelectedDocument) {
      return;
    }

    setHistoryModalOpen(true);
    void loadRevisionList(selectedDocumentId);
  };

  const closeHistoryModal = () => {
    setHistoryModalOpen(false);
  };

  const handleParentChange = (nextParentId: string) => {
    const currentDocumentId = editorMode === 'edit' ? selectedDocumentId ?? undefined : undefined;
    const nextSuggestedSort = String(getSuggestedSortValue(tree, normalizeOptionalNumber(nextParentId), currentDocumentId));

    setDraft((current) => ({
      ...current,
      parentId: nextParentId,
      sort: !current.sort.trim() || current.sort === sortSuggestion ? nextSuggestedSort : current.sort,
    }));
    setSortSuggestion(nextSuggestedSort);
  };

  const refreshDocumentWorkspace = useCallback(async (documentId: number, preserveRevisionSelection: boolean = true) => {
    await refreshCollections(true);
    await loadDocumentDetail(documentId);
    await loadRevisionList(documentId, preserveRevisionSelection);
  }, [loadDocumentDetail, loadRevisionList, refreshCollections]);

  const handleSearch = async () => {
    setSidebarView('results');
    await refreshCollections(false);
  };

  const handleSave = async () => {
    if (!draft.title.trim() || !draft.markdownContent.trim()) {
      toast.error(t('wiki.toast.requiredFields'));
      return;
    }

    if (
      (normalizeOptionalNumber(draft.visibility) ?? WikiDocumentVisibility.Authenticated) === WikiDocumentVisibility.Restricted &&
      normalizeAccessList(draft.allowedRoles).length === 0 &&
      normalizeAccessList(draft.allowedPermissions).length === 0
    ) {
      toast.error(t('wiki.toast.restrictedAccessRequired'));
      return;
    }

    setSubmitting(true);

    try {
      if (editorMode === 'create') {
        const createdId = await createWikiDocument(buildCreateRequest(draft));
        toast.success(t('wiki.toast.created'));
        closeEditor();
        await refreshCollections(true);
        setSelectedDocumentId(createdId);
        return;
      }

      if (!selectedDocumentId) {
        toast.error(t('wiki.toast.noSelection'));
        return;
      }

      await updateWikiDocument(selectedDocumentId, buildUpdateRequest(draft));
      toast.success(t('wiki.toast.updated'));
      closeEditor();
      await refreshDocumentWorkspace(selectedDocumentId, false);
    } catch (error) {
      log.error('WikiApp', '保存文档失败:', error);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.saveFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const handlePublish = async () => {
    if (!selectedDocumentId) {
      return;
    }

    setSubmitting(true);
    try {
      await publishWikiDocument(selectedDocumentId);
      toast.success(t('wiki.toast.published'));
      await refreshDocumentWorkspace(selectedDocumentId);
    } catch (error) {
      log.error('WikiApp', '发布文档失败:', error);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.publishFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleUnpublish = async () => {
    if (!selectedDocumentId) {
      return;
    }

    setSubmitting(true);
    try {
      await unpublishWikiDocument(selectedDocumentId);
      toast.success(t('wiki.toast.unpublished'));
      await refreshDocumentWorkspace(selectedDocumentId);
    } catch (error) {
      log.error('WikiApp', '撤下文档失败:', error);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.unpublishFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleArchive = async () => {
    if (!selectedDocumentId) {
      return;
    }

    setSubmitting(true);
    try {
      await archiveWikiDocument(selectedDocumentId);
      toast.success(t('wiki.toast.archived'));
      await refreshDocumentWorkspace(selectedDocumentId);
    } catch (error) {
      log.error('WikiApp', '归档文档失败:', error);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.archiveFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const openDeleteConfirm = () => {
    if (!selectedDocumentId || !canEditSelectedDocument || submitting) {
      return;
    }

    setDeleteConfirmOpen(true);
  };

  const closeDeleteConfirm = () => {
    setDeleteConfirmOpen(false);
  };

  const handleDelete = async () => {
    if (!selectedDocumentId) {
      return;
    }

    setSubmitting(true);
    try {
      await deleteWikiDocument(selectedDocumentId);
      toast.success(t('wiki.toast.deleted'));
      setDeleteConfirmOpen(false);
      await refreshCollections(true);
    } catch (error) {
      log.error('WikiApp', '删除文档失败:', error);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.deleteFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleRestore = async () => {
    if (!selectedDocumentId) {
      return;
    }

    setSubmitting(true);
    try {
      const restoredDocumentId = selectedDocumentId;
      await restoreWikiDocument(restoredDocumentId);
      toast.success(t('wiki.toast.restored'));

      if (showingDeleted) {
        setDeletionFilter('active');
        setSidebarView('tree');
        setSelectedDocumentId(restoredDocumentId);
      } else {
        await refreshCollections(true);
      }
    } catch (error) {
      log.error('WikiApp', '恢复文档失败:', error);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.restoreFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const handleExport = async () => {
    if (!selectedDocumentId) {
      return;
    }

    try {
      const { blob, fileName } = await downloadWikiMarkdown(selectedDocumentId);
      const downloadUrl = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = downloadUrl;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(downloadUrl);
      toast.success(t('wiki.toast.exported'));
    } catch (error) {
      log.error('WikiApp', '导出 Markdown 失败:', error);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.exportFailed'));
    }
  };

  const triggerImport = () => {
    importInputRef.current?.click();
  };

  const handleImportFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    setSubmitting(true);

    try {
      const importedId = await importWikiMarkdown({
        file,
        parentId: selectedDocumentId ?? undefined,
        publishAfterImport: false,
      });

      toast.success(t('wiki.toast.imported'));
      await refreshCollections(true);
      setSelectedDocumentId(importedId);
    } catch (error) {
      log.error('WikiApp', '导入 Markdown 失败:', error);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.importFailed'));
    } finally {
      setSubmitting(false);
      event.target.value = '';
    }
  };

  const handleImageUpload = async (file: File) => {
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
      url: attachment.voUrl,
      thumbnailUrl: attachment.voThumbnailUrl,
    };
  };

  const handleDocumentUpload = async (file: File) => {
    const attachment = await uploadDocument(
      {
        file,
        businessType: 'Wiki',
      },
      t,
    );

    return {
      url: attachment.voUrl,
      fileName: attachment.voOriginalName || file.name,
    };
  };

  const openRollbackConfirm = () => {
    if (!selectedRevision || selectedRevision.voIsCurrent || submitting) {
      return;
    }

    setRollbackConfirmOpen(true);
  };

  const closeRollbackConfirm = () => {
    setRollbackConfirmOpen(false);
  };

  const handleMarkdownLinkClick = useCallback((event: MouseEvent<HTMLDivElement>) => {
    const target = event.target;
    if (!(target instanceof Element)) {
      return;
    }

    const anchor = target.closest('a');
    const href = anchor?.getAttribute('href');
    if (!href || !href.startsWith('/__documents__/')) {
      return;
    }

    event.preventDefault();
    const slug = decodeURIComponent(href.replace('/__documents__/', '').split('#')[0]);
    void loadDocumentBySlug(slug);
  }, [loadDocumentBySlug]);

  const handleRollback = async () => {
    if (!selectedDocumentId || !selectedRevisionId) {
      return;
    }

    setSubmitting(true);

    try {
      await rollbackWikiRevision(selectedRevisionId);
      toast.success(t('wiki.toast.rolledBack', { version: selectedRevision?.voVersion ?? '' }));
      setRollbackConfirmOpen(false);
      await refreshDocumentWorkspace(selectedDocumentId, false);
    } catch (error) {
      log.error('WikiApp', '回滚文档版本失败:', error);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.rollbackFailed'));
    } finally {
      setSubmitting(false);
    }
  };

  const selectedStatusText = selectedDocument ? toStatusText(t, selectedDocument.voStatus) : t('wiki.status.unselected');
  const selectedStatusClass = selectedDocument ? toStatusClassName(selectedDocument.voStatus) : styles.statusDraft;

  const toggleTreeNode = (nodeId: number) => {
    setExpandedNodeIds((current) => {
      const next = new Set(current);
      if (next.has(nodeId)) {
        next.delete(nodeId);
      } else {
        next.add(nodeId);
      }

      return next;
    });
  };

  const renderTreeNodes = (nodes: WikiDocumentTreeNodeVo[], depth: number = 0): ReactNode => {
    return nodes.map((node) => {
      const children = node.voChildren || [];
      const isExpandable = children.length > 0;
      const isExpanded = expandedNodeIds.has(node.voId);

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
                onClick={() => toggleTreeNode(node.voId)}
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
              className={`${styles.treeNode} ${selectedDocumentId === node.voId ? styles.treeNodeActive : ''}`}
              onClick={() => {
                setSelectedDocumentId(node.voId);
                if (isExpandable) {
                  setExpandedNodeIds((current) => new Set(current).add(node.voId));
                }
              }}
            >
              <span className={styles.treeNodeDepth} />
              <span className={styles.treeNodeTitle}>{node.voTitle}</span>
              {isExpandable ? <span className={styles.treeNodeMeta}>{children.length}</span> : null}
              <span className={`${styles.statusChip} ${toStatusClassName(node.voStatus)}`}>{toStatusText(t, node.voStatus)}</span>
            </button>
          </div>

          {isExpandable && isExpanded ? renderTreeNodes(children, depth + 1) : null}
        </div>
      );
    });
  };

  return (
    <div ref={containerRef} className={`${styles.container} ${isCompactLayout ? styles.containerCompact : ''}`}>
      <aside className={styles.sidebar}>
        <div className={styles.sidebarHeader}>
          <div className={styles.sidebarTitleRow}>
            <div>
              <h2 className={styles.sidebarTitle}>{t('wiki.sidebar.title')}</h2>
              <p className={styles.sidebarHint}>{t('wiki.sidebar.hint')}</p>
            </div>
          </div>

          <div className={styles.sidebarUtilityRow}>
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
                onClick={() => void refreshCollections(true)}
                disabled={loadingTree || loadingList}
              >
                {t('wiki.actions.refresh')}
              </button>
              {isAdmin ? (
                <>
                  <button type="button" className={styles.primaryButton} onClick={openCreateEditor}>
                    {t('wiki.actions.create')}
                  </button>
                  <button type="button" className={styles.ghostButton} onClick={triggerImport} disabled={submitting}>
                    {t('wiki.actions.import')}
                  </button>
                </>
              ) : null}
            </div>
          </div>

          <div className={styles.searchRow}>
            <input
              className={styles.searchInput}
              value={keyword}
              placeholder={t('wiki.sidebar.searchPlaceholder')}
              onChange={(event) => setKeyword(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter') {
                  void handleSearch();
                }
              }}
            />
            <button type="button" className={styles.primaryButton} onClick={() => void handleSearch()}>
              {t('wiki.actions.search')}
            </button>
          </div>

          {isAdmin ? (
            <div className={styles.statusStack}>
              <div className={styles.statusRow}>
                <select
                  className={styles.select}
                  value={statusFilter}
                  onChange={(event) => setStatusFilter(event.target.value)}
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
                  onChange={(event) => setDeletionFilter(event.target.value as DeletionFilter)}
                >
                  <option value="active">{t('wiki.filter.activeOnly')}</option>
                  <option value="deleted">{t('wiki.filter.deletedOnly')}</option>
                </select>
              </div>
            </div>
          ) : null}
        </div>

        <div className={styles.sidebarBody}>
          <div className={styles.sidebarTabs}>
            <button
              type="button"
              className={sidebarView === 'tree' ? styles.sidebarTabActive : styles.sidebarTab}
              onClick={() => setSidebarView('tree')}
            >
              {t('wiki.sidebar.tab.tree')}
            </button>
            <button
              type="button"
              className={sidebarView === 'results' ? styles.sidebarTabActive : styles.sidebarTab}
              onClick={() => setSidebarView('results')}
            >
              {showingDeleted ? t('wiki.sidebar.tab.deletedResults') : t('wiki.sidebar.tab.results')}
            </button>
          </div>

          <section className={styles.sidebarPanel}>
            <div className={styles.sectionHeader}>
              <h3 className={styles.sectionTitle}>
                {sidebarView === 'tree'
                  ? t('wiki.sidebar.tab.tree')
                  : showingDeleted
                    ? t('wiki.sidebar.tab.deletedResults')
                    : t('wiki.sidebar.tab.results')}
              </h3>
              <span className={styles.sectionCount}>{sidebarView === 'tree' ? totalTreeDocuments : totalResultsCount}</span>
            </div>
            <div className={styles.sectionHint}>
              {sidebarView === 'results'
                ? totalResultsCount > currentListCount
                  ? t('wiki.sidebar.sectionHint.pageStats', { current: currentListCount, total: totalResultsCount })
                  : hasActiveFilters
                    ? t('wiki.sidebar.sectionHint.filtered', { total: totalResultsCount })
                    : t('wiki.sidebar.sectionHint.list', { total: totalResultsCount })
                : t('wiki.sidebar.sectionHint.tree')}
            </div>

            {sidebarView === 'tree' ? (
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
                      className={`${styles.listItem} ${selectedDocumentId === document.voId ? styles.listItemActive : ''}`}
                      onClick={() => setSelectedDocumentId(document.voId)}
                    >
                      <div className={styles.listItemHeader}>
                        <span className={styles.listItemTitle}>{document.voTitle}</span>
                        <span className={`${styles.statusChip} ${document.voIsDeleted ? styles.statusDeleted : toStatusClassName(document.voStatus)}`}>
                          {document.voIsDeleted ? t('wiki.status.deleted') : toStatusText(t, document.voStatus)}
                        </span>
                      </div>
                      {document.voSummary ? (
                        <div className={styles.listItemSummary}>{document.voSummary}</div>
                      ) : null}
                      <div className={styles.listItemMeta}>
                        {document.voSlug} · v{document.voVersion} · {toVisibilityText(t, document.voVisibility)}
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
        </div>
      </aside>

      <main className={styles.main}>
        <div className={styles.contentHeader}>
          <div className={styles.titleGroup}>
            <h1 className={styles.title}>
              {editorVisible
                ? (editorMode === 'create' ? t('wiki.content.createTitle') : t('wiki.content.editTitle'))
                : selectedDocument?.voTitle || t('wiki.content.defaultTitle')}
            </h1>
            <p className={styles.subtitle}>
              {editorVisible
                ? t('wiki.content.editingHint')
                : selectedDocument?.voIsDeleted
                  ? t('wiki.content.deletedHint')
                  : selectedDocument?.voSummary || t('wiki.content.defaultHint')}
            </p>
          </div>

          <div className={styles.actionGroup}>
            {!editorVisible && selectedDocument && !selectedDocument.voIsDeleted ? (
              <button type="button" className={styles.secondaryButton} onClick={() => void handleExport()}>
                {t('wiki.actions.export')}
              </button>
            ) : null}
            {canEditSelectedDocument && !editorVisible && selectedDocument ? (
              <>
                <button type="button" className={styles.secondaryButton} onClick={openHistoryModal} disabled={submitting}>
                  {t('wiki.actions.history')}
                </button>
                <button type="button" className={styles.primaryButton} onClick={openEditEditor}>
                  {t('wiki.actions.edit')}
                </button>
                <button type="button" className={styles.dangerButton} onClick={openDeleteConfirm} disabled={submitting}>
                  {t('wiki.actions.delete')}
                </button>
              </>
            ) : null}
            {canRestoreSelectedDocument && !editorVisible && selectedDocument ? (
              <button type="button" className={styles.primaryButton} onClick={() => void handleRestore()} disabled={submitting}>
                {t('wiki.actions.restore')}
              </button>
            ) : null}
            {editorVisible ? (
              <button type="button" className={styles.ghostButton} onClick={closeEditor}>
                {t('wiki.actions.cancelEdit')}
              </button>
            ) : null}
          </div>
        </div>

        <div className={styles.contentBody}>
          {editorVisible ? (
            <div className={styles.editorPanel}>
              <div className={styles.formGrid}>
                <div>
                  <label className={styles.formLabel}>{t('wiki.form.title')}</label>
                  <input
                    className={styles.input}
                    value={draft.title}
                    onChange={(event) => setDraft((current) => ({ ...current, title: event.target.value }))}
                    placeholder={t('wiki.form.titlePlaceholder')}
                  />
                </div>
                <div>
                  <label className={styles.formLabel}>Slug</label>
                  <input
                    className={styles.input}
                    value={draft.slug}
                    onChange={(event) => setDraft((current) => ({ ...current, slug: event.target.value }))}
                    placeholder={t('wiki.form.slugPlaceholder')}
                  />
                </div>
                <div className={styles.formSpanFull}>
                  <label className={styles.formLabel}>{t('wiki.form.summary')}</label>
                  <textarea
                    className={styles.textarea}
                    value={draft.summary}
                    onChange={(event) => setDraft((current) => ({ ...current, summary: event.target.value }))}
                    placeholder={t('wiki.form.summaryPlaceholder')}
                  />
                </div>
                <div>
                  <label className={styles.formLabel}>{t('wiki.form.parent')}</label>
                  <select
                    className={styles.select}
                    value={draft.parentId}
                    onChange={(event) => handleParentChange(event.target.value)}
                  >
                    <option value="">{t('wiki.form.parentRoot')}</option>
                    {treeOptions.map((option) => (
                      <option
                        key={option.id}
                        value={String(option.id)}
                        disabled={editorMode === 'edit' && invalidParentIds.has(option.id)}
                      >
                        {option.label}
                        {editorMode === 'edit' && invalidParentIds.has(option.id) ? t('wiki.form.parentDisabledSuffix') : ''}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className={styles.formLabel}>{t('wiki.form.sort')}</label>
                  <input
                    className={styles.numberInput}
                    value={draft.sort}
                    onChange={(event) => setDraft((current) => ({ ...current, sort: event.target.value }))}
                    placeholder="0"
                    step="10"
                    type="number"
                  />
                  <div className={styles.sortQuickRow}>
                    {SORT_PRESET_VALUES.map((value) => (
                      <button
                        key={value}
                        type="button"
                        className={draft.sort === value ? styles.sortQuickButtonActive : styles.sortQuickButton}
                        onClick={() => setDraft((current) => ({ ...current, sort: value }))}
                      >
                        {value}
                      </button>
                    ))}
                  </div>
                  <div className={styles.sortSuggestionRow}>
                    <span className={styles.fieldHint}>{t('wiki.form.sortSuggestion', { value: sortSuggestion })}</span>
                    {draft.sort !== sortSuggestion ? (
                      <button
                        type="button"
                        className={styles.sortSuggestionButton}
                        onClick={() => setDraft((current) => ({ ...current, sort: sortSuggestion }))}
                      >
                        {t('wiki.form.useSuggestedSort')}
                      </button>
                    ) : null}
                  </div>
                </div>
                <div>
                  <label className={styles.formLabel}>{t('wiki.form.coverAttachmentId')}</label>
                  <input
                    className={styles.numberInput}
                    value={draft.coverAttachmentId}
                    onChange={(event) => setDraft((current) => ({ ...current, coverAttachmentId: event.target.value }))}
                    placeholder={t('wiki.form.optionalPlaceholder')}
                  />
                </div>
                <div>
                  <label className={styles.formLabel}>{t('wiki.form.visibility')}</label>
                  <select
                    className={styles.select}
                    value={draft.visibility}
                    onChange={(event) => setDraft((current) => ({ ...current, visibility: event.target.value }))}
                  >
                    <option value={String(WikiDocumentVisibility.Public)}>{t('wiki.visibility.public')}</option>
                    <option value={String(WikiDocumentVisibility.Authenticated)}>{t('wiki.visibility.authenticated')}</option>
                    <option value={String(WikiDocumentVisibility.Restricted)}>{t('wiki.visibility.restricted')}</option>
                  </select>
                </div>
                {draft.visibility === String(WikiDocumentVisibility.Restricted) ? (
                  <>
                    <div className={styles.formSpanFull}>
                      <label className={styles.formLabel}>{t('wiki.form.allowedRoles')}</label>
                      <textarea
                        className={styles.textarea}
                        value={draft.allowedRoles}
                        onChange={(event) => setDraft((current) => ({ ...current, allowedRoles: event.target.value }))}
                        placeholder={t('wiki.form.allowedRolesPlaceholder')}
                      />
                    </div>
                    <div className={styles.formSpanFull}>
                      <label className={styles.formLabel}>{t('wiki.form.allowedPermissions')}</label>
                      <textarea
                        className={styles.textarea}
                        value={draft.allowedPermissions}
                        onChange={(event) => setDraft((current) => ({ ...current, allowedPermissions: event.target.value }))}
                        placeholder={t('wiki.form.allowedPermissionsPlaceholder')}
                      />
                    </div>
                  </>
                ) : null}
                {editorMode === 'edit' ? (
                  <div>
                    <label className={styles.formLabel}>{t('wiki.form.changeSummary')}</label>
                    <input
                      className={styles.input}
                      value={draft.changeSummary}
                      onChange={(event) => setDraft((current) => ({ ...current, changeSummary: event.target.value }))}
                      placeholder={t('wiki.form.changeSummaryPlaceholder')}
                    />
                  </div>
                ) : null}
              </div>

              <MarkdownEditor
                value={draft.markdownContent}
                onChange={(value) => setDraft((current) => ({ ...current, markdownContent: value }))}
                minHeight={360}
                placeholder={t('wiki.form.markdownPlaceholder')}
                onImageUpload={handleImageUpload}
                onDocumentUpload={handleDocumentUpload}
              />

              <div className={styles.editorActions}>
                <button type="button" className={styles.ghostButton} onClick={closeEditor} disabled={submitting}>
                  {t('common.cancel')}
                </button>
                <button type="button" className={styles.primaryButton} onClick={() => void handleSave()} disabled={submitting}>
                  {submitting ? t('wiki.actions.saving') : editorMode === 'create' ? t('wiki.actions.createDocument') : t('wiki.actions.saveChanges')}
                </button>
              </div>
            </div>
          ) : selectedDocumentId ? (
            loadingDetail ? (
              <div className={styles.emptyState}>{t('wiki.loading.detail')}</div>
            ) : selectedDocument ? (
              <>
                <div className={styles.metaBar}>
                  <span className={`${styles.statusChip} ${selectedDocument.voIsDeleted ? styles.statusDeleted : selectedStatusClass}`}>
                    {selectedDocument.voIsDeleted ? t('wiki.status.deleted') : selectedStatusText}
                  </span>
                  <span className={styles.metaChip}>{t('wiki.meta.slug', { value: selectedDocument.voSlug })}</span>
                  <span className={styles.metaChip}>{t('wiki.meta.version', { value: selectedDocument.voVersion })}</span>
                  <span className={styles.metaChip}>{t('wiki.meta.visibility', { value: toVisibilityText(t, selectedDocument.voVisibility) })}</span>
                  <span className={styles.metaChip}>{t('wiki.meta.source', { value: toSourceText(t, selectedDocument.voSourceType) })}</span>
                  {selectedDocument.voAllowedRoles?.length ? (
                    <span className={styles.metaChip}>{t('wiki.meta.allowedRoles', { value: selectedDocument.voAllowedRoles.join(', ') })}</span>
                  ) : null}
                  {selectedDocument.voAllowedPermissions?.length ? (
                    <span className={styles.metaChip}>{t('wiki.meta.allowedPermissions', { value: selectedDocument.voAllowedPermissions.join(', ') })}</span>
                  ) : null}
                  {selectedDocument.voSourcePath ? (
                    <span className={styles.metaChip}>{t('wiki.meta.sourcePath', { value: selectedDocument.voSourcePath })}</span>
                  ) : null}
                  <span className={styles.metaChip}>{t('wiki.meta.created', { value: formatTime(selectedDocument.voCreateTime, i18n.resolvedLanguage) })}</span>
                  <span className={styles.metaChip}>{t('wiki.meta.updated', { value: formatTime(selectedDocument.voModifyTime, i18n.resolvedLanguage) })}</span>
                  {selectedDocument.voIsDeleted ? (
                    <span className={styles.metaChip}>
                      {t('wiki.meta.deleted', {
                        time: formatTime(selectedDocument.voDeletedAt, i18n.resolvedLanguage),
                        user: selectedDocument.voDeletedBy || t('common.unknownUser'),
                      })}
                    </span>
                  ) : null}
                  {selectedDocument.voPublishedAt ? (
                    <span className={styles.metaChip}>{t('wiki.meta.published', { value: formatTime(selectedDocument.voPublishedAt, i18n.resolvedLanguage) })}</span>
                  ) : null}
                </div>

                {isAdmin && !isBuiltInDocument && !selectedDocument.voIsDeleted ? (
                  <div className={`${styles.toolbarRow} ${styles.toolbarRowSpaced}`}>
                    {selectedDocument.voStatus !== WikiDocumentStatus.Published ? (
                      <button type="button" className={styles.primaryButton} onClick={() => void handlePublish()} disabled={submitting}>
                        {t('wiki.actions.publish')}
                      </button>
                    ) : (
                      <button type="button" className={styles.secondaryButton} onClick={() => void handleUnpublish()} disabled={submitting}>
                        {t('wiki.actions.unpublish')}
                      </button>
                    )}
                    {selectedDocument.voStatus !== WikiDocumentStatus.Archived ? (
                      <button type="button" className={styles.dangerButton} onClick={() => void handleArchive()} disabled={submitting}>
                        {t('wiki.actions.archive')}
                      </button>
                    ) : null}
                  </div>
                ) : null}

                <div className={styles.documentLayout}>
                  <div className={styles.markdownPanel} onClick={handleMarkdownLinkClick}>
                    <MarkdownRenderer content={selectedDocument.voMarkdownContent} className={styles.markdownContent} />
                  </div>
                </div>
              </>
            ) : (
              <div className={styles.emptyState}>{t('wiki.empty.detail')}</div>
            )
          ) : (
            <div className={styles.emptyState}>
              <div>
                <div>{t('wiki.empty.main')}</div>
                {!loggedIn ? <div className={styles.emptyHintSpacing}>{t('wiki.empty.publicHint')}</div> : null}
                {isAdmin && !showingDeleted ? <div className={styles.emptyHintSpacing}>{t('wiki.empty.adminHint')}</div> : null}
              </div>
            </div>
          )}

          {!editorVisible && selectedDocumentId && !activeDocumentIds.has(selectedDocumentId) ? (
            <div className={`${styles.errorText} ${styles.errorTextSpacing}`}>
              {t('wiki.empty.orphanWarning')}
            </div>
          ) : null}
        </div>
      </main>

      <input
        ref={importInputRef}
        className={styles.hiddenInput}
        type="file"
        accept=".md,.markdown,.txt,text/markdown,text/plain"
        onChange={(event) => void handleImportFile(event)}
      />


      <Modal
        isOpen={historyModalOpen}
        onClose={closeHistoryModal}
        title={selectedDocument ? t('wiki.history.titleWithName', { title: selectedDocument.voTitle }) : t('wiki.history.title')}
        size="large"
      >
        <div className={styles.historyModalContent}>
          <div className={styles.historyModalSummary}>
            <span className={styles.historyCount}>{t('wiki.history.count', { count: revisionList.length })}</span>
            <span className={styles.historyHint}>{t('wiki.history.summaryHint')}</span>
          </div>

          <div className={styles.historyBody}>
            <div className={styles.historyList}>
              {loadingRevisionList ? (
                <div className={styles.loadingText}>{t('wiki.loading.revisionList')}</div>
              ) : revisionList.length > 0 ? (
                revisionList.map((revision) => (
                  <button
                    key={revision.voId}
                    type="button"
                    className={`${styles.revisionItem} ${selectedRevisionId === revision.voId ? styles.revisionItemActive : ''}`}
                    onClick={() => setSelectedRevisionId(revision.voId)}
                  >
                    <div className={styles.revisionItemHeader}>
                      <span className={styles.revisionTitle}>v{revision.voVersion}</span>
                      {revision.voIsCurrent ? (
                        <span className={`${styles.statusChip} ${styles.statusPublished}`}>{t('wiki.status.current')}</span>
                      ) : null}
                    </div>
                    <div className={styles.revisionMeta}>{revision.voTitle}</div>
                    <div className={styles.revisionMeta}>
                      {t('wiki.history.revisionMeta', {
                        time: formatTime(revision.voCreateTime, i18n.resolvedLanguage),
                        author: revision.voCreateBy,
                      })}
                    </div>
                    <div className={styles.revisionSummary}>{describeRevisionSummary(t, revision)}</div>
                  </button>
                ))
              ) : (
                <div className={styles.mutedText}>{t('wiki.empty.revisionList')}</div>
              )}
            </div>

            <div className={styles.revisionDetail}>
              {loadingRevisionDetail ? (
                <div className={styles.loadingText}>{t('wiki.loading.revisionDetail')}</div>
              ) : selectedRevision ? (
                <>
                  <div className={styles.revisionDetailHeader}>
                    <div>
                      <div className={styles.revisionDetailTitle}>{t('wiki.history.revisionTitle', { version: selectedRevision.voVersion })}</div>
                      <div className={styles.revisionMeta}>{selectedRevision.voTitle}</div>
                    </div>
                    <div className={styles.revisionActions}>
                      <span className={styles.metaChip}>{t('wiki.meta.source', { value: toSourceText(t, selectedRevision.voSourceType) })}</span>
                      {!selectedRevision.voIsCurrent ? (
                        <button type="button" className={styles.dangerButton} onClick={openRollbackConfirm} disabled={submitting}>
                          {t('wiki.actions.rollbackToVersion')}
                        </button>
                      ) : null}
                    </div>
                  </div>

                  <div className={styles.revisionMetaBar}>
                    <span className={styles.metaChip}>{t('wiki.meta.created', { value: formatTime(selectedRevision.voCreateTime, i18n.resolvedLanguage) })}</span>
                    <span className={styles.metaChip}>{t('wiki.meta.author', { value: selectedRevision.voCreateBy })}</span>
                    <span className={styles.metaChip}>{t('wiki.meta.description', { value: selectedRevision.voChangeSummary || t('wiki.revision.noChangeSummary') })}</span>
                  </div>

                  <div className={styles.revisionPreview}>
                    <div onClick={handleMarkdownLinkClick}>
                      <MarkdownRenderer content={selectedRevision.voMarkdownContent} className={styles.revisionMarkdownContent} />
                    </div>
                  </div>
                </>
              ) : (
                <div className={styles.mutedText}>{t('wiki.empty.selectRevision')}</div>
              )}
            </div>
          </div>
        </div>
      </Modal>

      <ConfirmDialog
        isOpen={deleteConfirmOpen}
        title={t('wiki.confirm.deleteTitle')}
        message={selectedDocument ? t('wiki.confirm.deleteMessageWithTitle', { title: selectedDocument.voTitle }) : t('wiki.confirm.deleteMessage')}
        confirmText={t('wiki.confirm.deleteConfirm')}
        danger
        onCancel={closeDeleteConfirm}
        onConfirm={() => void handleDelete()}
      />

      <ConfirmDialog
        isOpen={rollbackConfirmOpen}
        title={t('wiki.confirm.rollbackTitle')}
        message={selectedRevision ? t('wiki.confirm.rollbackMessageWithVersion', { version: selectedRevision.voVersion }) : t('wiki.confirm.rollbackMessage')}
        confirmText={t('wiki.confirm.rollbackConfirm')}
        danger
        onCancel={closeRollbackConfirm}
        onConfirm={() => void handleRollback()}
      />
    </div>
  );
};
