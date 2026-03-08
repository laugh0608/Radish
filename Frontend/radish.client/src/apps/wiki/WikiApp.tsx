import { useCallback, useEffect, useMemo, useRef, useState, type ChangeEvent, type MouseEvent, type ReactNode } from 'react';
import { ConfirmDialog } from '@radish/ui/confirm-dialog';
import { toast } from '@radish/ui/toast';
import { MarkdownEditor } from '@radish/ui/markdown-editor';
import { MarkdownRenderer } from '@radish/ui/markdown-renderer';
import { useTranslation } from 'react-i18next';
import { uploadDocument, uploadImage } from '@/api/attachment';
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
import { WikiDocumentStatus } from './types/wiki';
import styles from './WikiApp.module.css';

type EditorMode = 'create' | 'edit';
type DeletionFilter = 'active' | 'deleted';

type ParentOption = {
  id: number;
  label: string;
};

type EditorDraft = {
  title: string;
  slug: string;
  summary: string;
  markdownContent: string;
  parentId: string;
  sort: string;
  coverAttachmentId: string;
  changeSummary: string;
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
};


const SORT_PRESET_VALUES = ['0', '10', '20', '30', '40', '50', '100'];

function flattenTreeOptions(nodes: WikiDocumentTreeNodeVo[], depth: number = 0): ParentOption[] {
  return nodes.flatMap((node) => [
    {
      id: node.voId,
      label: `${depth > 0 ? `${'　'.repeat(depth)}└ ` : ''}${node.voTitle}`,
    },
    ...flattenTreeOptions(node.voChildren || [], depth + 1),
  ]);
}

function collectDescendantIds(nodes: WikiDocumentTreeNodeVo[], targetId: number): Set<number> {
  const result = new Set<number>();

  const visit = (currentNodes: WikiDocumentTreeNodeVo[], collecting: boolean) => {
    currentNodes.forEach((node) => {
      const shouldCollect = collecting || node.voId === targetId;
      if (collecting) {
        result.add(node.voId);
      }

      visit(node.voChildren || [], shouldCollect);
    });
  };

  visit(nodes, false);
  return result;
}

function toStatusText(status: number): string {
  switch (status) {
    case WikiDocumentStatus.Published:
      return '已发布';
    case WikiDocumentStatus.Archived:
      return '已归档';
    default:
      return '草稿';
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

function toSourceText(sourceType?: string | null): string {
  switch ((sourceType || '').trim().toLowerCase()) {
    case 'manual':
      return '在线文档';
    case 'imported':
      return 'Markdown 导入';
    case 'custom':
      return '在线文档';
    case 'builtin':
      return '固定文档';
    case 'rollback':
      return '回滚生成';
    default:
      return sourceType?.trim() || '未知来源';
  }
}

function formatTime(value?: string | null): string {
  if (!value) {
    return '--';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString('zh-CN', {
    hour12: false,
  });
}

function flattenTree(nodes: WikiDocumentTreeNodeVo[]): number[] {
  return nodes.flatMap((node) => [node.voId, ...flattenTree(node.voChildren || [])]);
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

function buildCreateRequest(draft: EditorDraft): CreateWikiDocumentRequest {
  return {
    title: draft.title.trim(),
    slug: normalizeOptionalString(draft.slug),
    summary: normalizeOptionalString(draft.summary),
    markdownContent: draft.markdownContent,
    parentId: normalizeOptionalNumber(draft.parentId),
    sort: normalizeOptionalNumber(draft.sort) ?? 0,
    coverAttachmentId: normalizeOptionalNumber(draft.coverAttachmentId),
  };
}

function buildUpdateRequest(draft: EditorDraft): UpdateWikiDocumentRequest {
  return {
    ...buildCreateRequest(draft),
    changeSummary: normalizeOptionalString(draft.changeSummary),
  };
}

function describeRevisionSummary(revision: WikiDocumentRevisionItemVo): string {
  if (revision.voChangeSummary?.trim()) {
    return revision.voChangeSummary;
  }

  return revision.voIsCurrent ? '当前版本快照' : '未填写修改说明';
}

export const WikiApp = () => {
  const { t } = useTranslation();
  const roles = useUserStore((state) => state.roles || []);
  const isAdmin = useMemo(
    () => roles.some((role) => ['admin', 'system'].includes(role.trim().toLowerCase())),
    [roles]
  );

  const [tree, setTree] = useState<WikiDocumentTreeNodeVo[]>([]);
  const [documents, setDocuments] = useState<WikiDocumentVo[]>([]);
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

  const importInputRef = useRef<HTMLInputElement | null>(null);
  const initialLoadedRef = useRef(false);
  const activeDocumentIds = useMemo(() => new Set(flattenTree(tree)), [tree]);
  const treeOptions = useMemo(() => flattenTreeOptions(tree), [tree]);
  const showingDeleted = isAdmin && deletionFilter === 'deleted';

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

      if (!preserveSelection || !selectedDocumentId) {
        setSelectedDocumentId(pickInitialDocumentId(treeData, pageData.data || []));
        return;
      }

      const hasInTree = flattenTree(treeData).includes(selectedDocumentId);
      const hasInList = (pageData.data || []).some((item) => item.voId === selectedDocumentId);

      if (!hasInTree && !hasInList) {
        setSelectedDocumentId(pickInitialDocumentId(treeData, pageData.data || []));
      }
    } catch (error) {
      log.error('WikiApp', '加载 Wiki 列表失败:', error);
      toast.error(error instanceof Error ? error.message : '加载 Wiki 列表失败');
    } finally {
      setLoadingTree(false);
      setLoadingList(false);
    }
  }, [isAdmin, keyword, selectedDocumentId, showingDeleted, statusFilter]);

  const loadDocumentDetail = useCallback(async (documentId: number) => {
    setLoadingDetail(true);

    try {
      const detail = await getWikiDocumentById(documentId, showingDeleted);
      setSelectedDocument(detail);
    } catch (error) {
      log.error('WikiApp', '加载文档详情失败:', error);
      setSelectedDocument(null);
      toast.error(error instanceof Error ? error.message : '加载文档详情失败');
    } finally {
      setLoadingDetail(false);
    }
  }, [showingDeleted]);

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
      toast.error(error instanceof Error ? error.message : '加载文档失败');
    }
  }, []);

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

      if (preserveSelection && selectedRevisionId && revisions.some((item) => item.voId === selectedRevisionId)) {
        return;
      }

      setSelectedRevisionId(revisions[0].voId);
    } catch (error) {
      log.error('WikiApp', '加载版本历史失败:', error);
      setRevisionList([]);
      setSelectedRevisionId(null);
      setSelectedRevision(null);
      toast.error(error instanceof Error ? error.message : '加载版本历史失败');
    } finally {
      setLoadingRevisionList(false);
    }
  }, [isAdmin, selectedRevisionId]);

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
      toast.error(error instanceof Error ? error.message : '加载版本详情失败');
    } finally {
      setLoadingRevisionDetail(false);
    }
  }, [isAdmin]);

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
    if (!selectedRevisionId || !isAdmin) {
      setSelectedRevision(null);
      return;
    }

    void loadRevisionDetail(selectedRevisionId);
  }, [isAdmin, loadRevisionDetail, selectedRevisionId]);

  const openCreateEditor = () => {
    setEditorMode('create');
    setDraft({
      ...EMPTY_DRAFT,
      parentId: selectedDocument && !selectedDocument.voIsDeleted ? String(selectedDocument.voId) : '',
    });
    setEditorVisible(true);
  };

  const openEditEditor = () => {
    if (!selectedDocument) {
      return;
    }

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
    });
    setEditorVisible(true);
  };

  const closeEditor = () => {
    setEditorVisible(false);
    setDraft(EMPTY_DRAFT);
  };

  const refreshDocumentWorkspace = useCallback(async (documentId: number, preserveRevisionSelection: boolean = true) => {
    await refreshCollections(true);
    await loadDocumentDetail(documentId);
    await loadRevisionList(documentId, preserveRevisionSelection);
  }, [loadDocumentDetail, loadRevisionList, refreshCollections]);

  const handleSearch = async () => {
    await refreshCollections(false);
  };

  const handleSave = async () => {
    if (!draft.title.trim() || !draft.markdownContent.trim()) {
      toast.error('标题和 Markdown 内容不能为空');
      return;
    }

    setSubmitting(true);

    try {
      if (editorMode === 'create') {
        const createdId = await createWikiDocument(buildCreateRequest(draft));
        toast.success('文档已创建');
        closeEditor();
        await refreshCollections(true);
        setSelectedDocumentId(createdId);
        return;
      }

      if (!selectedDocumentId) {
        toast.error('未选中文档');
        return;
      }

      await updateWikiDocument(selectedDocumentId, buildUpdateRequest(draft));
      toast.success('文档已更新');
      closeEditor();
      await refreshDocumentWorkspace(selectedDocumentId, false);
    } catch (error) {
      log.error('WikiApp', '保存文档失败:', error);
      toast.error(error instanceof Error ? error.message : '保存文档失败');
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
      toast.success('文档已发布');
      await refreshDocumentWorkspace(selectedDocumentId);
    } catch (error) {
      log.error('WikiApp', '发布文档失败:', error);
      toast.error(error instanceof Error ? error.message : '发布文档失败');
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
      toast.success('文档已转为草稿');
      await refreshDocumentWorkspace(selectedDocumentId);
    } catch (error) {
      log.error('WikiApp', '撤下文档失败:', error);
      toast.error(error instanceof Error ? error.message : '撤下文档失败');
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
      toast.success('文档已归档');
      await refreshDocumentWorkspace(selectedDocumentId);
    } catch (error) {
      log.error('WikiApp', '归档文档失败:', error);
      toast.error(error instanceof Error ? error.message : '归档文档失败');
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
      toast.success('文档已删除');
      setDeleteConfirmOpen(false);
      await refreshCollections(true);
    } catch (error) {
      log.error('WikiApp', '删除文档失败:', error);
      toast.error(error instanceof Error ? error.message : '删除文档失败');
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
      await restoreWikiDocument(selectedDocumentId);
      toast.success('文档已恢复');
      await refreshCollections(true);
    } catch (error) {
      log.error('WikiApp', '恢复文档失败:', error);
      toast.error(error instanceof Error ? error.message : '恢复文档失败');
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
      toast.success('Markdown 已导出');
    } catch (error) {
      log.error('WikiApp', '导出 Markdown 失败:', error);
      toast.error(error instanceof Error ? error.message : '导出 Markdown 失败');
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

      toast.success('Markdown 已导入为草稿');
      await refreshCollections(true);
      setSelectedDocumentId(importedId);
    } catch (error) {
      log.error('WikiApp', '导入 Markdown 失败:', error);
      toast.error(error instanceof Error ? error.message : '导入 Markdown 失败');
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
      toast.success(`已回滚到 v${selectedRevision?.voVersion ?? ''}，并生成新版本`);
      setRollbackConfirmOpen(false);
      await refreshDocumentWorkspace(selectedDocumentId, false);
    } catch (error) {
      log.error('WikiApp', '回滚文档版本失败:', error);
      toast.error(error instanceof Error ? error.message : '回滚文档版本失败');
    } finally {
      setSubmitting(false);
    }
  };

  const selectedStatusText = selectedDocument ? toStatusText(selectedDocument.voStatus) : '未选择';
  const selectedStatusClass = selectedDocument ? toStatusClassName(selectedDocument.voStatus) : styles.statusDraft;

  const renderTreeNodes = (nodes: WikiDocumentTreeNodeVo[], depth: number = 0): ReactNode => {
    return nodes.map((node) => (
      <div key={node.voId}>
        <button
          type="button"
          className={`${styles.treeNode} ${selectedDocumentId === node.voId ? styles.treeNodeActive : ''}`}
          style={{ paddingLeft: `${10 + depth * 18}px` }}
          onClick={() => setSelectedDocumentId(node.voId)}
        >
          <span className={styles.treeNodeDepth} />
          <span className={styles.treeNodeTitle}>{node.voTitle}</span>
          <span className={`${styles.statusChip} ${toStatusClassName(node.voStatus)}`}>{toStatusText(node.voStatus)}</span>
        </button>
        {node.voChildren?.length ? renderTreeNodes(node.voChildren, depth + 1) : null}
      </div>
    ));
  };

  return (
    <div className={styles.container}>
      <aside className={styles.sidebar}>
        <div className={styles.sidebarHeader}>
          <div className={styles.sidebarTitleRow}>
            <div>
              <h2 className={styles.sidebarTitle}>文档</h2>
              <p className={styles.sidebarHint}>固定文档、在线文档与 Markdown 导入导出统一入口</p>
            </div>
          </div>

          <div className={styles.toolbarRow}>
            <button type="button" className={styles.secondaryButton} onClick={() => void refreshCollections(true)} disabled={loadingTree || loadingList}>
              刷新
            </button>
            {isAdmin ? (
              <>
                <button type="button" className={styles.primaryButton} onClick={openCreateEditor}>
                  新建
                </button>
                <button type="button" className={styles.ghostButton} onClick={triggerImport} disabled={submitting}>
                  导入
                </button>
              </>
            ) : null}
          </div>

          <div className={styles.searchRow}>
            <input
              className={styles.searchInput}
              value={keyword}
              placeholder="搜索标题 / Slug / 摘要"
              onChange={(event) => setKeyword(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter') {
                  void handleSearch();
                }
              }}
            />
            <button type="button" className={styles.primaryButton} onClick={() => void handleSearch()}>
              搜索
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
                  <option value="">全部状态</option>
                  <option value={String(WikiDocumentStatus.Draft)}>草稿</option>
                  <option value={String(WikiDocumentStatus.Published)}>已发布</option>
                  <option value={String(WikiDocumentStatus.Archived)}>已归档</option>
                </select>
              </div>
              <div className={styles.statusRow}>
                <select
                  className={styles.select}
                  value={deletionFilter}
                  onChange={(event) => setDeletionFilter(event.target.value as DeletionFilter)}
                >
                  <option value="active">正常文档</option>
                  <option value="deleted">回收站</option>
                </select>
              </div>
            </div>
          ) : null}
        </div>

        <div className={styles.sidebarBody}>
          <section className={styles.treeSection}>
            <h3 className={styles.sectionTitle}>目录树</h3>
            <div className={styles.treeScroll}>
              {loadingTree ? (
                <div className={styles.loadingText}>正在加载目录…</div>
              ) : tree.length > 0 ? (
                renderTreeNodes(tree)
              ) : (
                <div className={styles.mutedText}>暂时还没有目录数据</div>
              )}
            </div>
          </section>

          <section className={styles.listSection}>
            <h3 className={styles.sectionTitle}>{showingDeleted ? '回收站' : '检索结果'}</h3>
            <div className={styles.listScroll}>
              {loadingList ? (
                <div className={styles.loadingText}>正在加载列表…</div>
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
                      <span className={`${styles.statusChip} ${document.voIsDeleted ? styles.statusDeleted : toStatusClassName(document.voStatus)}`}>{document.voIsDeleted ? '已删除' : toStatusText(document.voStatus)}</span>
                    </div>
                    {document.voSummary ? (
                      <div className={styles.listItemSummary}>{document.voSummary}</div>
                    ) : null}
                    <div className={styles.listItemMeta}>{document.voSlug} · v{document.voVersion}</div>
                  </button>
                ))
              ) : (
                <div className={styles.mutedText}>{showingDeleted ? '回收站里暂时没有文档' : '没有匹配的文档'}</div>
              )}
            </div>
          </section>
        </div>
      </aside>

      <main className={styles.main}>
        <div className={styles.contentHeader}>
          <div className={styles.titleGroup}>
            <h1 className={styles.title}>{editorVisible ? (editorMode === 'create' ? '新建文档' : '编辑文档') : selectedDocument?.voTitle || '文档'}</h1>
            <p className={styles.subtitle}>
              {editorVisible
                ? '当前为 Markdown 编辑态，保存后即可进入浏览态。'
                : selectedDocument?.voIsDeleted
                  ? '当前文档位于回收站，可执行恢复操作。'
                  : selectedDocument?.voSummary || '选择左侧文档查看详情，或从这里开始创建在线文档与导入 Markdown。'}
            </p>
          </div>

          <div className={styles.actionGroup}>
            {!editorVisible && selectedDocument && !selectedDocument.voIsDeleted ? (
              <button type="button" className={styles.secondaryButton} onClick={() => void handleExport()}>
                导出 Markdown
              </button>
            ) : null}
            {canEditSelectedDocument && !editorVisible && selectedDocument ? (
              <>
                <button type="button" className={styles.primaryButton} onClick={openEditEditor}>
                  编辑
                </button>
                <button type="button" className={styles.dangerButton} onClick={openDeleteConfirm} disabled={submitting}>
                  删除
                </button>
              </>
            ) : null}
            {canRestoreSelectedDocument && !editorVisible && selectedDocument ? (
              <button type="button" className={styles.primaryButton} onClick={() => void handleRestore()} disabled={submitting}>
                恢复
              </button>
            ) : null}
            {editorVisible ? (
              <button type="button" className={styles.ghostButton} onClick={closeEditor}>
                取消编辑
              </button>
            ) : null}
          </div>
        </div>

        <div className={styles.contentBody}>
          {editorVisible ? (
            <div className={styles.editorPanel}>
              <div className={styles.formGrid}>
                <div>
                  <label className={styles.formLabel}>标题</label>
                  <input
                    className={styles.input}
                    value={draft.title}
                    onChange={(event) => setDraft((current) => ({ ...current, title: event.target.value }))}
                    placeholder="例如：新手入门"
                  />
                </div>
                <div>
                  <label className={styles.formLabel}>Slug</label>
                  <input
                    className={styles.input}
                    value={draft.slug}
                    onChange={(event) => setDraft((current) => ({ ...current, slug: event.target.value }))}
                    placeholder="例如：getting-started"
                  />
                </div>
                <div className={styles.formSpanFull}>
                  <label className={styles.formLabel}>摘要</label>
                  <textarea
                    className={styles.textarea}
                    value={draft.summary}
                    onChange={(event) => setDraft((current) => ({ ...current, summary: event.target.value }))}
                    placeholder="可选，用于列表摘要展示"
                  />
                </div>
                <div>
                  <label className={styles.formLabel}>父文档</label>
                  <select
                    className={styles.select}
                    value={draft.parentId}
                    onChange={(event) => setDraft((current) => ({ ...current, parentId: event.target.value }))}
                  >
                    <option value="">顶级文档</option>
                    {treeOptions.map((option) => (
                      <option
                        key={option.id}
                        value={String(option.id)}
                        disabled={editorMode === 'edit' && invalidParentIds.has(option.id)}
                      >
                        {option.label}
                        {editorMode === 'edit' && invalidParentIds.has(option.id) ? '（不可选）' : ''}
                      </option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className={styles.formLabel}>排序</label>
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
                </div>
                <div>
                  <label className={styles.formLabel}>封面附件 ID</label>
                  <input
                    className={styles.numberInput}
                    value={draft.coverAttachmentId}
                    onChange={(event) => setDraft((current) => ({ ...current, coverAttachmentId: event.target.value }))}
                    placeholder="可选"
                  />
                </div>
                {editorMode === 'edit' ? (
                  <div>
                    <label className={styles.formLabel}>修改说明</label>
                    <input
                      className={styles.input}
                      value={draft.changeSummary}
                      onChange={(event) => setDraft((current) => ({ ...current, changeSummary: event.target.value }))}
                      placeholder="例如：补充导入说明"
                    />
                  </div>
                ) : null}
              </div>

              <MarkdownEditor
                value={draft.markdownContent}
                onChange={(value) => setDraft((current) => ({ ...current, markdownContent: value }))}
                minHeight={360}
                placeholder="输入 Markdown 内容，支持图片和文档上传"
                onImageUpload={handleImageUpload}
                onDocumentUpload={handleDocumentUpload}
              />

              <div className={styles.editorActions}>
                <button type="button" className={styles.ghostButton} onClick={closeEditor} disabled={submitting}>
                  取消
                </button>
                <button type="button" className={styles.primaryButton} onClick={() => void handleSave()} disabled={submitting}>
                  {submitting ? '保存中…' : editorMode === 'create' ? '创建文档' : '保存修改'}
                </button>
              </div>
            </div>
          ) : selectedDocumentId ? (
            loadingDetail ? (
              <div className={styles.emptyState}>正在加载文档详情…</div>
            ) : selectedDocument ? (
              <>
                <div className={styles.metaBar}>
                  <span className={`${styles.statusChip} ${selectedDocument.voIsDeleted ? styles.statusDeleted : selectedStatusClass}`}>{selectedDocument.voIsDeleted ? '已删除' : selectedStatusText}</span>
                  <span className={styles.metaChip}>Slug：{selectedDocument.voSlug}</span>
                  <span className={styles.metaChip}>版本：v{selectedDocument.voVersion}</span>
                  <span className={styles.metaChip}>来源：{toSourceText(selectedDocument.voSourceType)}</span>
                  {selectedDocument.voSourcePath ? (
                    <span className={styles.metaChip}>来源路径：{selectedDocument.voSourcePath}</span>
                  ) : null}
                  <span className={styles.metaChip}>创建：{formatTime(selectedDocument.voCreateTime)}</span>
                  <span className={styles.metaChip}>更新：{formatTime(selectedDocument.voModifyTime)}</span>
                  {selectedDocument.voIsDeleted ? (
                    <span className={styles.metaChip}>删除：{formatTime(selectedDocument.voDeletedAt)} · {selectedDocument.voDeletedBy || '未知'}</span>
                  ) : null}
                  {selectedDocument.voPublishedAt ? (
                    <span className={styles.metaChip}>发布：{formatTime(selectedDocument.voPublishedAt)}</span>
                  ) : null}
                </div>

                {isAdmin && !isBuiltInDocument && !selectedDocument.voIsDeleted ? (
                  <div className={styles.toolbarRow} style={{ marginBottom: '16px' }}>
                    {selectedDocument.voStatus !== WikiDocumentStatus.Published ? (
                      <button type="button" className={styles.primaryButton} onClick={() => void handlePublish()} disabled={submitting}>
                        发布
                      </button>
                    ) : (
                      <button type="button" className={styles.secondaryButton} onClick={() => void handleUnpublish()} disabled={submitting}>
                        转为草稿
                      </button>
                    )}
                    {selectedDocument.voStatus !== WikiDocumentStatus.Archived ? (
                      <button type="button" className={styles.dangerButton} onClick={() => void handleArchive()} disabled={submitting}>
                        归档
                      </button>
                    ) : null}
                  </div>
                ) : null}

                <div className={styles.documentLayout}>
                  <div className={styles.markdownPanel} onClick={handleMarkdownLinkClick}>
                    <MarkdownRenderer content={selectedDocument.voMarkdownContent} />
                  </div>

                  {isAdmin && !isBuiltInDocument && !selectedDocument.voIsDeleted ? (
                    <aside className={styles.historyPanel}>
                      <div className={styles.historyPanelHeader}>
                        <div>
                          <h3 className={styles.historyTitle}>版本历史</h3>
                          <p className={styles.historyHint}>查看历史快照并按版本回滚内容</p>
                        </div>
                        <span className={styles.historyCount}>{revisionList.length} 条</span>
                      </div>

                      <div className={styles.historyBody}>
                        <div className={styles.historyList}>
                          {loadingRevisionList ? (
                            <div className={styles.loadingText}>正在加载版本历史…</div>
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
                                    <span className={`${styles.statusChip} ${styles.statusPublished}`}>当前</span>
                                  ) : null}
                                </div>
                                <div className={styles.revisionMeta}>{revision.voTitle}</div>
                                <div className={styles.revisionMeta}>{formatTime(revision.voCreateTime)} · {revision.voCreateBy}</div>
                                <div className={styles.revisionSummary}>{describeRevisionSummary(revision)}</div>
                              </button>
                            ))
                          ) : (
                            <div className={styles.mutedText}>当前文档还没有版本记录</div>
                          )}
                        </div>

                        <div className={styles.revisionDetail}>
                          {loadingRevisionDetail ? (
                            <div className={styles.loadingText}>正在加载版本详情…</div>
                          ) : selectedRevision ? (
                            <>
                              <div className={styles.revisionDetailHeader}>
                                <div>
                                  <div className={styles.revisionDetailTitle}>版本 v{selectedRevision.voVersion}</div>
                                  <div className={styles.revisionMeta}>{selectedRevision.voTitle}</div>
                                </div>
                                <div className={styles.revisionActions}>
                                  <span className={styles.metaChip}>来源：{toSourceText(selectedRevision.voSourceType)}</span>
                                  {!selectedRevision.voIsCurrent ? (
                                    <button type="button" className={styles.dangerButton} onClick={openRollbackConfirm} disabled={submitting}>
                                      回滚到此版本
                                    </button>
                                  ) : null}
                                </div>
                              </div>

                              <div className={styles.revisionMetaBar}>
                                <span className={styles.metaChip}>创建：{formatTime(selectedRevision.voCreateTime)}</span>
                                <span className={styles.metaChip}>作者：{selectedRevision.voCreateBy}</span>
                                <span className={styles.metaChip}>说明：{selectedRevision.voChangeSummary || '未填写修改说明'}</span>
                              </div>

                              <div className={styles.revisionPreview}>
                                <div onClick={handleMarkdownLinkClick}>
                                  <MarkdownRenderer content={selectedRevision.voMarkdownContent} />
                                </div>
                              </div>
                            </>
                          ) : (
                            <div className={styles.mutedText}>请选择左侧版本查看详情</div>
                          )}
                        </div>
                      </div>
                    </aside>
                  ) : null}
                </div>
              </>
            ) : (
              <div className={styles.emptyState}>未能加载到文档详情，请重新选择左侧文档。</div>
            )
          ) : (
            <div className={styles.emptyState}>
              <div>
                <div>当前还没有可展示的文档。</div>
                {isAdmin && !showingDeleted ? <div style={{ marginTop: '8px' }}>你可以先新建一篇在线文档，或导入一个 `.md` 文件。</div> : null}
              </div>
            </div>
          )}

          {!editorVisible && selectedDocumentId && !activeDocumentIds.has(selectedDocumentId) ? (
            <div className={styles.errorText} style={{ marginTop: '12px' }}>
              当前文档未出现在目录树中，可能是筛选结果或父子关系尚未整理。
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

      <ConfirmDialog
        isOpen={deleteConfirmOpen}
        title="确认删除文档"
        message={selectedDocument ? `确定删除《${selectedDocument.voTitle}》吗？删除后可在回收站中恢复。` : '确定删除当前文档吗？'}
        confirmText="确认删除"
        danger
        onCancel={closeDeleteConfirm}
        onConfirm={() => void handleDelete()}
      />

      <ConfirmDialog
        isOpen={rollbackConfirmOpen}
        title="确认回滚版本"
        message={selectedRevision ? `确定将当前文档内容回滚到 v${selectedRevision.voVersion} 吗？系统会保留当前内容，并生成一个新的回滚版本。` : '确定执行版本回滚吗？'}
        confirmText="确认回滚"
        danger
        onCancel={closeRollbackConfirm}
        onConfirm={() => void handleRollback()}
      />
    </div>
  );
};
