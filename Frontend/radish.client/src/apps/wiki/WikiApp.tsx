import { useCallback, useEffect, useMemo, useRef, useState, type ChangeEvent, type MouseEvent } from 'react';
import { ConfirmDialog } from '@radish/ui/confirm-dialog';
import { BottomSheet } from '@radish/ui/bottom-sheet';
import { toast } from '@radish/ui/toast';
import { MarkdownEditor } from '@radish/ui/markdown-editor';
import { MarkdownRenderer } from '@radish/ui/markdown-renderer';
import { Modal } from '@radish/ui/modal';
import {
  buildAttachmentAssetUrl,
  type MarkdownDocumentUploadResult,
  type MarkdownImageUploadResult,
} from '@radish/ui';
import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import { uploadDocument, uploadImage } from '@/api/attachment';
import { useCurrentWindow } from '@/desktop/useCurrentWindow';
import { useAuthStore } from '@/stores/authStore';
import { useUserStore } from '@/stores/userStore';
import type { LongId } from '@/api/user';
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
  WikiDocumentDetailVo,
  WikiDocumentRevisionDetailVo,
  WikiDocumentRevisionItemVo,
  WikiDocumentTreeNodeVo,
  WikiDocumentVo,
} from './types/wiki';
import { WikiDocumentStatus, WikiDocumentVisibility } from './types/wiki';
import {
  buildCreateRequest,
  buildUpdateRequest,
  collectDescendantIds,
  collectExpandableNodeIds,
  describeRevisionSummary,
  EMPTY_DRAFT,
  findAncestorIds,
  flattenTree,
  flattenTreeOptions,
  formatWikiTime,
  getSuggestedSortValue,
  normalizeAccessList,
  normalizeOptionalNumber,
  normalizeOptionalLongId,
  pickInitialDocumentId,
  parseWikiWindowParams,
  SORT_PRESET_VALUES,
  type EditorDraft,
} from './wikiApp.helpers';
import { WikiSidebar, type DeletionFilter, type SidebarView } from './WikiSidebar';
import styles from './WikiApp.module.css';

type EditorMode = 'create' | 'edit';

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

export const WikiApp = () => {
  const { t, i18n } = useTranslation();
  const currentWindow = useCurrentWindow();
  const loggedIn = useAuthStore((state) => state.isAuthenticated);
  const roles = useUserStore((state) => state.roles || []);
  const windowParams = useMemo(() => parseWikiWindowParams(currentWindow?.appParams), [currentWindow?.appParams]);
  const initialWindowRouteRef = useRef<{ documentId?: LongId; slug?: string } | null>(
    windowParams.documentId || windowParams.slug ? windowParams : null
  );
  const handledWindowRouteRef = useRef<string | null>(null);
  const isAdmin = useMemo(
    () => roles.some((role) => ['admin', 'system'].includes(role.trim().toLowerCase())),
    [roles]
  );

  const [tree, setTree] = useState<WikiDocumentTreeNodeVo[]>([]);
  const [documents, setDocuments] = useState<WikiDocumentVo[]>([]);
  const [totalResultsCount, setTotalResultsCount] = useState(0);
  const [selectedDocumentId, setSelectedDocumentId] = useState<LongId | null>(null);
  const [selectedDocument, setSelectedDocument] = useState<WikiDocumentDetailVo | null>(null);
  const [revisionList, setRevisionList] = useState<WikiDocumentRevisionItemVo[]>([]);
  const [selectedRevisionId, setSelectedRevisionId] = useState<LongId | null>(null);
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
  const [expandedNodeIds, setExpandedNodeIds] = useState<Set<string>>(new Set());
  const [isCompactLayout, setIsCompactLayout] = useState(false);
  const [isReadingLayout, setIsReadingLayout] = useState(false);
  const [isSidebarSheetOpen, setIsSidebarSheetOpen] = useState(false);
  const [sortSuggestion, setSortSuggestion] = useState(EMPTY_DRAFT.sort);

  const containerRef = useRef<HTMLDivElement | null>(null);
  const importInputRef = useRef<HTMLInputElement | null>(null);
  const initialLoadedRef = useRef(false);
  const selectedDocumentIdRef = useRef<LongId | null>(null);
  const selectedRevisionIdRef = useRef<LongId | null>(null);
  const activeDocumentIds = useMemo(() => new Set(flattenTree(tree).map(String)), [tree]);
  const treeOptions = useMemo(() => flattenTreeOptions(tree), [tree]);
  const expandableNodeIds = useMemo(() => new Set(collectExpandableNodeIds(tree).map(String)), [tree]);
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
  const shouldUseSidebarOverlay = isReadingLayout && (editorVisible || Boolean(selectedDocumentId) || loadingDetail);
  const invalidParentIds = useMemo(() => {
    if (!selectedDocumentId) {
      return new Set<string>();
    }

    const ids = new Set(Array.from(collectDescendantIds(tree, selectedDocumentId), String));
    ids.add(String(selectedDocumentId));
    return ids;
  }, [selectedDocumentId, tree]);

  const refreshCollections = useCallback(async (preserveSelection: boolean = true) => {
    setLoadingTree(true);
    setLoadingList(true);

    try {
      const [treeData, pageData] = await Promise.all([
        getWikiTree(t),
        getWikiList({
          pageIndex: 1,
          pageSize: 100,
          keyword: keyword.trim() || undefined,
          status: isAdmin && statusFilter !== '' ? Number(statusFilter) : undefined,
          includeDeleted: showingDeleted,
          deletedOnly: showingDeleted,
        }, t),
      ]);

      setTree(treeData);
      setDocuments(pageData.data || []);
      setTotalResultsCount(pageData.dataCount || pageData.data?.length || 0);

      if (!preserveSelection || !selectedDocumentIdRef.current) {
        setSelectedDocumentId(pickInitialDocumentId(treeData, pageData.data || []));
        return;
      }

      const selectedDocumentIdKey = String(selectedDocumentIdRef.current);
      const hasInTree = flattenTree(treeData).some((id) => String(id) === selectedDocumentIdKey);
      const hasInList = (pageData.data || []).some((item) => String(item.voId) === selectedDocumentIdKey);

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

  const loadDocumentDetail = useCallback(async (documentId: LongId) => {
    setLoadingDetail(true);

    try {
      const detail = await getWikiDocumentById(documentId, showingDeleted, t);
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
      const detail = await getWikiDocumentBySlug(normalizedSlug, t);
      setSelectedDocumentId(detail.voId);
      setSelectedDocument(detail);
    } catch (error) {
      log.error('WikiApp', '按 Slug 加载文档失败:', error);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.loadDocumentFailed'));
    }
  }, [t]);

  const loadDocumentByRouteId = useCallback(async (documentId: LongId) => {
    try {
      const detail = await getWikiDocumentById(documentId, showingDeleted, t);
      setSelectedDocumentId(detail.voId);
      setSelectedDocument(detail);
    } catch (error) {
      log.error('WikiApp', '按窗口参数 ID 加载文档失败:', error);
      toast.error(error instanceof Error ? error.message : t('wiki.toast.loadDocumentFailed'));
    }
  }, [showingDeleted, t]);

  const loadRevisionList = useCallback(async (documentId: LongId, preserveSelection: boolean = true) => {
    if (!isAdmin) {
      setRevisionList([]);
      setSelectedRevisionId(null);
      setSelectedRevision(null);
      return;
    }

    setLoadingRevisionList(true);

    try {
      const revisions = await getWikiRevisionList(documentId, t);
      setRevisionList(revisions);

      if (revisions.length === 0) {
        setSelectedRevisionId(null);
        setSelectedRevision(null);
        return;
      }

      if (
        preserveSelection &&
        selectedRevisionIdRef.current &&
        revisions.some((item) => String(item.voId) === String(selectedRevisionIdRef.current))
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

  const loadRevisionDetail = useCallback(async (revisionId: LongId) => {
    if (!isAdmin) {
      setSelectedRevision(null);
      return;
    }

    setLoadingRevisionDetail(true);

    try {
      const detail = await getWikiRevisionDetail(revisionId, t);
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
      void loadDocumentByRouteId(initialWindowRoute.documentId);
      return;
    }

    if (initialWindowRoute.slug) {
      void loadDocumentBySlug(initialWindowRoute.slug);
    }
  }, [loadDocumentByRouteId, loadDocumentBySlug]);

  useEffect(() => {
    const routeSignature = windowParams.documentId
      ? `id:${windowParams.documentId}`
      : windowParams.slug
        ? `slug:${windowParams.slug}`
        : '';

    if (!routeSignature || handledWindowRouteRef.current === routeSignature) {
      return;
    }

    handledWindowRouteRef.current = routeSignature;

    if (windowParams.documentId) {
      void loadDocumentByRouteId(windowParams.documentId);
      return;
    }

    if (windowParams.slug) {
      void loadDocumentBySlug(windowParams.slug);
    }
  }, [loadDocumentByRouteId, loadDocumentBySlug, windowParams.documentId, windowParams.slug]);

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

    const updateLayout = (width: number, height: number) => {
      setIsCompactLayout(width < 1080);
      setIsReadingLayout(height < 760);
    };

    updateLayout(container.clientWidth, container.clientHeight);

    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (!entry) {
        return;
      }

      updateLayout(entry.contentRect.width, entry.contentRect.height);
    });

    observer.observe(container);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (!shouldUseSidebarOverlay && isSidebarSheetOpen) {
      setIsSidebarSheetOpen(false);
    }
  }, [isSidebarSheetOpen, shouldUseSidebarOverlay]);

  useEffect(() => {
    setExpandedNodeIds((current) => {
      const next = new Set<string>();

      current.forEach((id) => {
        if (expandableNodeIds.has(id)) {
          next.add(id);
        }
      });

      if (next.size === 0) {
        tree.forEach((node) => {
          if ((node.voChildren || []).length > 0) {
            next.add(String(node.voId));
          }
        });
      }

      if (selectedDocumentId) {
        findAncestorIds(tree, selectedDocumentId).forEach((id) => {
          const ancestorIdKey = String(id);
          if (expandableNodeIds.has(ancestorIdKey)) {
            next.add(ancestorIdKey);
          }
        });
      }

      return next;
    });
  }, [expandableNodeIds, selectedDocumentId, tree]);

  const openCreateEditor = () => {
    const parentId = selectedDocument && !selectedDocument.voIsDeleted ? String(selectedDocument.voId) : '';
    const suggestedSort = String(getSuggestedSortValue(tree, normalizeOptionalLongId(parentId)));

    setIsSidebarSheetOpen(false);
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

    setIsSidebarSheetOpen(false);
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

  const handleSelectDocument = useCallback((documentId: LongId) => {
    setSelectedDocumentId(documentId);
    if (shouldUseSidebarOverlay) {
      setIsSidebarSheetOpen(false);
    }
  }, [shouldUseSidebarOverlay]);

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
    const nextSuggestedSort = String(getSuggestedSortValue(tree, normalizeOptionalLongId(nextParentId), currentDocumentId));

    setDraft((current) => ({
      ...current,
      parentId: nextParentId,
      sort: !current.sort.trim() || current.sort === sortSuggestion ? nextSuggestedSort : current.sort,
    }));
    setSortSuggestion(nextSuggestedSort);
  };

  const refreshDocumentWorkspace = useCallback(async (documentId: LongId, preserveRevisionSelection: boolean = true) => {
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
        const createdId = await createWikiDocument(buildCreateRequest(draft), t);
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

      await updateWikiDocument(selectedDocumentId, buildUpdateRequest(draft), t);
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
      await publishWikiDocument(selectedDocumentId, t);
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
      await unpublishWikiDocument(selectedDocumentId, t);
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
      await archiveWikiDocument(selectedDocumentId, t);
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
      await deleteWikiDocument(selectedDocumentId, t);
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
      await restoreWikiDocument(restoredDocumentId, t);
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
      const { blob, fileName } = await downloadWikiMarkdown(selectedDocumentId, t);
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
      }, t);

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
      await rollbackWikiRevision(selectedRevisionId, t);
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

  const handleToggleTreeNode = useCallback((nodeId: LongId) => {
    setExpandedNodeIds((current) => {
      const next = new Set(current);
      const nodeIdKey = String(nodeId);
      if (next.has(nodeIdKey)) {
        next.delete(nodeIdKey);
      } else {
        next.add(nodeIdKey);
      }

      return next;
    });
  }, []);

  const handleExpandTreeNode = useCallback((nodeId: LongId) => {
    setExpandedNodeIds((current) => new Set(current).add(String(nodeId)));
  }, []);

  return (
    <div
      ref={containerRef}
      className={`${styles.container} ${isCompactLayout ? styles.containerCompact : ''} ${shouldUseSidebarOverlay ? styles.containerReading : ''}`}
    >
      {!shouldUseSidebarOverlay ? (
        <aside className={styles.sidebar}>
          <WikiSidebar
            tree={tree}
            documents={documents}
            selectedDocumentId={selectedDocumentId}
            expandedNodeIds={expandedNodeIds}
            sidebarView={sidebarView}
            keyword={keyword}
            statusFilter={statusFilter}
            deletionFilter={deletionFilter}
            loadingTree={loadingTree}
            loadingList={loadingList}
            loggedIn={loggedIn}
            isAdmin={isAdmin}
            submitting={submitting}
            showingDeleted={showingDeleted}
            totalTreeDocuments={totalTreeDocuments}
            totalResultsCount={totalResultsCount}
            currentListCount={currentListCount}
            totalResultsText={totalResultsText}
            hasActiveFilters={hasActiveFilters}
            getStatusText={(status) => toStatusText(t, status)}
            getStatusClassName={toStatusClassName}
            getVisibilityText={(visibility) => toVisibilityText(t, visibility)}
            onToggleTreeNode={handleToggleTreeNode}
            onExpandTreeNode={handleExpandTreeNode}
            onSelectDocument={handleSelectDocument}
            onSidebarViewChange={setSidebarView}
            onKeywordChange={setKeyword}
            onStatusFilterChange={setStatusFilter}
            onDeletionFilterChange={setDeletionFilter}
            onSearch={handleSearch}
            onRefresh={() => refreshCollections(true)}
            onCreate={openCreateEditor}
            onImport={triggerImport}
          />
        </aside>
      ) : null}

      <main className={`${styles.main} ${shouldUseSidebarOverlay ? styles.mainReading : ''}`}>
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
                        disabled={editorMode === 'edit' && invalidParentIds.has(String(option.id))}
                      >
                        {option.label}
                        {editorMode === 'edit' && invalidParentIds.has(String(option.id)) ? t('wiki.form.parentDisabledSuffix') : ''}
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
                minHeight={shouldUseSidebarOverlay ? 280 : 360}
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
                  <span className={styles.metaChip}>{t('wiki.meta.created', { value: formatWikiTime(selectedDocument.voCreateTime, i18n.resolvedLanguage) })}</span>
                  <span className={styles.metaChip}>{t('wiki.meta.updated', { value: formatWikiTime(selectedDocument.voModifyTime, i18n.resolvedLanguage) })}</span>
                  {selectedDocument.voIsDeleted ? (
                    <span className={styles.metaChip}>
                      {t('wiki.meta.deleted', {
                        time: formatWikiTime(selectedDocument.voDeletedAt, i18n.resolvedLanguage),
                        user: selectedDocument.voDeletedBy || t('common.unknownUser'),
                      })}
                    </span>
                  ) : null}
                  {selectedDocument.voPublishedAt ? (
                    <span className={styles.metaChip}>{t('wiki.meta.published', { value: formatWikiTime(selectedDocument.voPublishedAt, i18n.resolvedLanguage) })}</span>
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

          {!editorVisible && selectedDocumentId && !activeDocumentIds.has(String(selectedDocumentId)) ? (
            <div className={`${styles.errorText} ${styles.errorTextSpacing}`}>
              {t('wiki.empty.orphanWarning')}
            </div>
          ) : null}
        </div>

        {shouldUseSidebarOverlay && !isSidebarSheetOpen ? (
          <button
            type="button"
            className={styles.sidebarOverlayTrigger}
            onClick={() => setIsSidebarSheetOpen(true)}
          >
            {t('wiki.actions.directory')}
          </button>
        ) : null}
      </main>

      {shouldUseSidebarOverlay ? (
        <BottomSheet
          isOpen={isSidebarSheetOpen}
          onClose={() => setIsSidebarSheetOpen(false)}
          title={t('wiki.actions.directory')}
          height="86%"
          bodyClassName={styles.sidebarSheetBody}
        >
          <div className={styles.sidebarSheet}>
            <WikiSidebar
              renderMode="overlay"
              tree={tree}
              documents={documents}
              selectedDocumentId={selectedDocumentId}
              expandedNodeIds={expandedNodeIds}
              sidebarView={sidebarView}
              keyword={keyword}
              statusFilter={statusFilter}
              deletionFilter={deletionFilter}
              loadingTree={loadingTree}
              loadingList={loadingList}
              loggedIn={loggedIn}
              isAdmin={isAdmin}
              submitting={submitting}
              showingDeleted={showingDeleted}
              totalTreeDocuments={totalTreeDocuments}
              totalResultsCount={totalResultsCount}
              currentListCount={currentListCount}
              totalResultsText={totalResultsText}
              hasActiveFilters={hasActiveFilters}
              getStatusText={(status) => toStatusText(t, status)}
              getStatusClassName={toStatusClassName}
              getVisibilityText={(visibility) => toVisibilityText(t, visibility)}
              onToggleTreeNode={handleToggleTreeNode}
              onExpandTreeNode={handleExpandTreeNode}
              onSelectDocument={handleSelectDocument}
              onSidebarViewChange={setSidebarView}
              onKeywordChange={setKeyword}
              onStatusFilterChange={setStatusFilter}
              onDeletionFilterChange={setDeletionFilter}
              onSearch={handleSearch}
              onRefresh={() => refreshCollections(true)}
              onCreate={openCreateEditor}
              onImport={triggerImport}
            />
          </div>
        </BottomSheet>
      ) : null}

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
                    className={`${styles.revisionItem} ${String(selectedRevisionId) === String(revision.voId) ? styles.revisionItemActive : ''}`}
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
                        time: formatWikiTime(revision.voCreateTime, i18n.resolvedLanguage),
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
                    <span className={styles.metaChip}>{t('wiki.meta.created', { value: formatWikiTime(selectedRevision.voCreateTime, i18n.resolvedLanguage) })}</span>
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
