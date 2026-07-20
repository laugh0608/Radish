import { type ChangeEvent, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import {
  AntInput as Input,
  AntModal as Modal,
  AntSelect as Select,
  Button,
  Popconfirm,
  Space,
  Table,
  Tag,
  message,
  type TableColumnsType,
} from '@radish/ui';
import {
  CheckOutlined,
  ClockCircleOutlined,
  CloseOutlined,
  DeleteOutlined,
  EyeOutlined,
  FileTextOutlined,
  LockOutlined,
  ReloadOutlined,
  SearchOutlined,
  SyncOutlined,
  UnlockOutlined,
} from '@radish/ui';
import {
  archiveWikiDocument,
  deleteWikiDocument,
  exportWikiMarkdown,
  getWikiGovernanceDetail,
  getWikiGovernancePage,
  getWikiRevisionDetail,
  getWikiRevisionList,
  importWikiMarkdown,
  publishWikiDocument,
  restoreWikiDocument,
  rollbackWikiRevision,
  unpublishWikiDocument,
  updateWikiAccessPolicy,
  type LongId,
  type WikiDocumentDetailVo,
  type WikiDocumentRevisionDetailVo,
  type WikiDocumentRevisionItemVo,
  type WikiDocumentVo,
} from '@/api/wikiGovernanceApi';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import {
  DOCUMENT_STATUS,
  DOCUMENT_VISIBILITY,
  formatDocumentDateTime,
  formatDocumentNumber,
  getDocumentAccessSummary,
  getDocumentSourceTypeText,
  getDocumentStatusText,
  getDocumentSummary,
  getDocumentVisibilityText,
} from './documentGovernancePresentation';
import '../adminForm.css';
import '../adminFeature.css';
import './DocumentGovernancePage.css';

function getStatusTag(status: number, t: TFunction) {
  if (status === DOCUMENT_STATUS.published) {
    return <Tag color="success">{getDocumentStatusText(status, t)}</Tag>;
  }

  if (status === DOCUMENT_STATUS.archived) {
    return <Tag color="default">{getDocumentStatusText(status, t)}</Tag>;
  }

  return <Tag color="warning">{getDocumentStatusText(status, t)}</Tag>;
}

function getVisibilityTag(visibility: number, t: TFunction) {
  if (visibility === DOCUMENT_VISIBILITY.public) {
    return <Tag color="green">{getDocumentVisibilityText(visibility, t)}</Tag>;
  }

  if (visibility === DOCUMENT_VISIBILITY.restricted) {
    return <Tag color="red">{getDocumentVisibilityText(visibility, t)}</Tag>;
  }

  return <Tag color="blue">{getDocumentVisibilityText(visibility, t)}</Tag>;
}

function splitAccessList(rawValue: string) {
  return rawValue
    .split(/[\n,，;；\s]+/g)
    .map((item) => item.trim())
    .filter(Boolean);
}

function triggerDownload(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

function isBuiltInDocument(record: WikiDocumentVo | WikiDocumentDetailVo | null | undefined) {
  return record?.voSourceType === 'BuiltIn';
}

function canMaintainDocument(record: WikiDocumentVo | WikiDocumentDetailVo | null | undefined) {
  return Boolean(record) && !record?.voIsDeleted && !isBuiltInDocument(record);
}

export const DocumentGovernancePage = () => {
  const { t, i18n } = useTranslation();
  useDocumentTitle(t('documents.documentTitle'));

  const statusOptions = useMemo(() => [
    { label: t('documents.status.all'), value: 'all' },
    { label: t('documents.status.draft'), value: String(DOCUMENT_STATUS.draft) },
    { label: t('documents.status.published'), value: String(DOCUMENT_STATUS.published) },
    { label: t('documents.status.archived'), value: String(DOCUMENT_STATUS.archived) },
  ], [t]);
  const visibilityOptions = useMemo(() => [
    { label: t('documents.visibility.all'), value: 'all' },
    { label: t('documents.visibility.public'), value: String(DOCUMENT_VISIBILITY.public) },
    { label: t('documents.visibility.authenticated'), value: String(DOCUMENT_VISIBILITY.authenticated) },
    { label: t('documents.visibility.restricted'), value: String(DOCUMENT_VISIBILITY.restricted) },
  ], [t]);
  const sourceTypeOptions = useMemo(() => [
    { label: t('documents.source.all'), value: 'all' },
    { label: t('documents.source.custom'), value: 'Custom' },
    { label: t('documents.source.imported'), value: 'Imported' },
    { label: t('documents.source.builtin'), value: 'BuiltIn' },
  ], [t]);
  const deletedOptions = useMemo(() => [
    { label: t('documents.deleted.active'), value: 'active' },
    { label: t('documents.deleted.all'), value: 'all' },
    { label: t('documents.deleted.only'), value: 'deleted' },
  ], [t]);

  const importInputRef = useRef<HTMLInputElement | null>(null);
  const [loading, setLoading] = useState(false);
  const [documents, setDocuments] = useState<WikiDocumentVo[]>([]);
  const [total, setTotal] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [keyword, setKeyword] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [visibilityFilter, setVisibilityFilter] = useState('all');
  const [sourceTypeFilter, setSourceTypeFilter] = useState('all');
  const [deletedFilter, setDeletedFilter] = useState<'active' | 'all' | 'deleted'>('active');
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailDocument, setDetailDocument] = useState<WikiDocumentDetailVo | null>(null);
  const [accessDocument, setAccessDocument] = useState<WikiDocumentVo | null>(null);
  const [accessVisibility, setAccessVisibility] = useState(String(DOCUMENT_VISIBILITY.authenticated));
  const [accessRolesText, setAccessRolesText] = useState('');
  const [accessPermissionsText, setAccessPermissionsText] = useState('');
  const [accessSaving, setAccessSaving] = useState(false);
  const [revisionDocument, setRevisionDocument] = useState<WikiDocumentVo | null>(null);
  const [revisionLoading, setRevisionLoading] = useState(false);
  const [revisionItems, setRevisionItems] = useState<WikiDocumentRevisionItemVo[]>([]);
  const [revisionDetail, setRevisionDetail] = useState<WikiDocumentRevisionDetailVo | null>(null);
  const [revisionDetailLoading, setRevisionDetailLoading] = useState(false);
  const [importing, setImporting] = useState(false);

  const canView = usePermission(CONSOLE_PERMISSIONS.docsView);
  const canPublish = usePermission(CONSOLE_PERMISSIONS.docsPublish);
  const canArchive = usePermission(CONSOLE_PERMISSIONS.docsArchive);
  const canDelete = usePermission(CONSOLE_PERMISSIONS.docsDelete);
  const canRestore = usePermission(CONSOLE_PERMISSIONS.docsRestore);
  const canUpdatePermissions = usePermission(CONSOLE_PERMISSIONS.docsPermissions);
  const canRollback = usePermission(CONSOLE_PERMISSIONS.docsRollback);
  const canImport = usePermission(CONSOLE_PERMISSIONS.docsImport);
  const canExport = usePermission(CONSOLE_PERMISSIONS.docsExport);

  const includeDeleted = deletedFilter === 'all' || deletedFilter === 'deleted';
  const deletedOnly = deletedFilter === 'deleted';
  const activeFilterCount = [
    keyword.trim() ? 'keyword' : undefined,
    statusFilter !== 'all' ? 'status' : undefined,
    visibilityFilter !== 'all' ? 'visibility' : undefined,
    sourceTypeFilter !== 'all' ? 'sourceType' : undefined,
    deletedFilter !== 'active' ? 'deleted' : undefined,
  ].filter(Boolean).length;
  const publishedCount = documents.filter((document) => document.voStatus === DOCUMENT_STATUS.published && !document.voIsDeleted).length;
  const draftCount = documents.filter((document) => document.voStatus === DOCUMENT_STATUS.draft && !document.voIsDeleted).length;
  const archivedCount = documents.filter((document) => document.voStatus === DOCUMENT_STATUS.archived && !document.voIsDeleted).length;
  const deletedCount = documents.filter((document) => document.voIsDeleted).length;
  const builtInCount = documents.filter((document) => isBuiltInDocument(document)).length;
  const restrictedCount = documents.filter((document) => document.voVisibility === DOCUMENT_VISIBILITY.restricted).length;
  const governanceDocument = detailDocument
    ?? accessDocument
    ?? revisionDocument
    ?? documents.find((document) => document.voVisibility === DOCUMENT_VISIBILITY.restricted && canMaintainDocument(document))
    ?? documents.find((document) => document.voStatus !== DOCUMENT_STATUS.published && canMaintainDocument(document))
    ?? documents[0]
    ?? null;
  const governanceDocumentRevisionCount = governanceDocument && revisionDocument && String(governanceDocument.voId) === String(revisionDocument.voId)
    ? revisionItems.length
    : 0;

  const loadDocuments = async (targetPageIndex = pageIndex, targetPageSize = pageSize) => {
    try {
      setLoading(true);
      const page = await getWikiGovernancePage({
        pageIndex: targetPageIndex,
        pageSize: targetPageSize,
        keyword: keyword.trim() || undefined,
        status: statusFilter === 'all' ? undefined : Number(statusFilter),
        visibility: visibilityFilter === 'all' ? undefined : Number(visibilityFilter),
        sourceType: sourceTypeFilter === 'all' ? undefined : sourceTypeFilter,
        includeDeleted,
        deletedOnly,
      }, t);

      setDocuments(page.data);
      setTotal(page.dataCount);
      setPageIndex(page.page);
      setPageSize(page.pageSize);
    } catch (error) {
      log.error('DocumentGovernancePage', '加载文档治理列表失败:', error);
      message.error(error instanceof Error ? error.message : t('documents.feedback.loadListFailed'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!canView) {
      return;
    }

    void loadDocuments(1, pageSize);
    // Document governance reloads from filter state; loadDocuments also preserves current pagination entry points.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canView, keyword, statusFilter, visibilityFilter, sourceTypeFilter, deletedFilter, pageSize]);

  const handleViewDetail = async (record: WikiDocumentVo) => {
    setDetailDocument(null);
    setDetailLoading(true);
    try {
      const detail = await getWikiGovernanceDetail(record.voId, true, t);
      setDetailDocument(detail);
    } catch (error) {
      log.error('DocumentGovernancePage', '加载文档详情失败:', error);
      message.error(error instanceof Error ? error.message : t('documents.feedback.loadDetailFailed'));
    } finally {
      setDetailLoading(false);
    }
  };

  const runDocumentAction = async (action: () => Promise<void>, successMessage: string, failureMessage: string) => {
    try {
      await action();
      message.success(successMessage);
      await loadDocuments();
    } catch (error) {
      log.error('DocumentGovernancePage', failureMessage, error);
      message.error(error instanceof Error ? error.message : failureMessage);
    }
  };

  const openAccessPolicy = (record: WikiDocumentVo) => {
    if (!canUpdatePermissions || record.voIsDeleted || isBuiltInDocument(record)) {
      message.error(t('documents.feedback.accessUnavailable'));
      return;
    }

    setAccessDocument(record);
    setAccessVisibility(String(record.voVisibility || DOCUMENT_VISIBILITY.authenticated));
    setAccessRolesText((record.voAllowedRoles || []).join('\n'));
    setAccessPermissionsText((record.voAllowedPermissions || []).join('\n'));
  };

  const saveAccessPolicy = async () => {
    if (!accessDocument) {
      return;
    }

    if (!canUpdatePermissions || accessDocument.voIsDeleted || isBuiltInDocument(accessDocument)) {
      message.error(t('documents.feedback.accessUnavailable'));
      return;
    }

    const allowedRoles = splitAccessList(accessRolesText);
    const allowedPermissions = splitAccessList(accessPermissionsText);
    const visibility = Number(accessVisibility);
    if (visibility === DOCUMENT_VISIBILITY.restricted && allowedRoles.length === 0 && allowedPermissions.length === 0) {
      message.error(t('documents.feedback.restrictedAccessRequired'));
      return;
    }

    try {
      setAccessSaving(true);
      await updateWikiAccessPolicy(accessDocument.voId, {
        visibility,
        allowedRoles,
        allowedPermissions,
      }, t);
      message.success(t('documents.feedback.accessUpdated'));
      setAccessDocument(null);
      await loadDocuments();
    } catch (error) {
      log.error('DocumentGovernancePage', '更新文档访问策略失败:', error);
      message.error(error instanceof Error ? error.message : t('documents.feedback.accessUpdateFailed'));
    } finally {
      setAccessSaving(false);
    }
  };

  const openRevisions = async (record: WikiDocumentVo) => {
    setRevisionDocument(record);
    setRevisionItems([]);
    setRevisionDetail(null);
    setRevisionLoading(true);
    try {
      const revisions = await getWikiRevisionList(record.voId, t);
      setRevisionItems(revisions);
      if (revisions[0]) {
        await loadRevisionDetail(revisions[0].voId);
      }
    } catch (error) {
      log.error('DocumentGovernancePage', '加载版本列表失败:', error);
      message.error(error instanceof Error ? error.message : t('documents.feedback.loadRevisionsFailed'));
    } finally {
      setRevisionLoading(false);
    }
  };

  const loadRevisionDetail = async (revisionId: LongId) => {
    setRevisionDetailLoading(true);
    try {
      const detail = await getWikiRevisionDetail(revisionId, t);
      setRevisionDetail(detail);
    } catch (error) {
      log.error('DocumentGovernancePage', '加载版本详情失败:', error);
      message.error(error instanceof Error ? error.message : t('documents.feedback.loadRevisionDetailFailed'));
    } finally {
      setRevisionDetailLoading(false);
    }
  };

  const handleRollback = async (revisionId: LongId) => {
    if (!canRollback || !revisionDocument || revisionDocument.voIsDeleted || isBuiltInDocument(revisionDocument)) {
      message.error(t('documents.feedback.rollbackUnavailable'));
      return;
    }

    await runDocumentAction(
      async () => {
        await rollbackWikiRevision(revisionId, t);
        if (revisionDocument) {
          const revisions = await getWikiRevisionList(revisionDocument.voId, t);
          setRevisionItems(revisions);
        }
      },
      t('documents.feedback.rolledBack'),
      t('documents.feedback.rollbackFailed')
    );
  };

  const handleImportFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) {
      return;
    }

    if (!canImport) {
      message.error(t('documents.feedback.importForbidden'));
      return;
    }

    if (!/\.(md|markdown|txt)$/i.test(file.name)) {
      message.error(t('documents.feedback.importUnsupported'));
      return;
    }

    try {
      setImporting(true);
      const id = await importWikiMarkdown(file, t);
      message.success(t('documents.feedback.imported', { id }));
      await loadDocuments(1, pageSize);
    } catch (error) {
      log.error('DocumentGovernancePage', '导入 Markdown 失败:', error);
      message.error(error instanceof Error ? error.message : t('documents.feedback.importFailed'));
    } finally {
      setImporting(false);
    }
  };

  const handleExport = async (record: WikiDocumentVo) => {
    if (!canExport) {
      message.error(t('documents.feedback.exportForbidden'));
      return;
    }

    try {
      const result = await exportWikiMarkdown(record.voId, t);
      triggerDownload(result.blob, result.fileName);
      message.success(t('documents.feedback.exported'));
    } catch (error) {
      log.error('DocumentGovernancePage', '导出 Markdown 失败:', error);
      message.error(error instanceof Error ? error.message : t('documents.feedback.exportFailed'));
    }
  };

  const columns: TableColumnsType<WikiDocumentVo> = [
    {
      title: t('documents.table.document'),
      key: 'document',
      width: 280,
      fixed: 'left',
      render: (_, record) => (
        <Space orientation="vertical" size={2}>
          <strong>{record.voTitle}</strong>
          <span className="admin-feature-subtle">{record.voSlug}</span>
        </Space>
      ),
    },
    {
      title: t('documents.table.status'),
      key: 'status',
      width: 110,
      render: (_, record) => record.voIsDeleted ? <Tag color="default">{t('documents.status.recycleBin')}</Tag> : getStatusTag(record.voStatus, t),
    },
    {
      title: t('documents.table.visibility'),
      key: 'visibility',
      width: 120,
      render: (_, record) => getVisibilityTag(record.voVisibility, t),
    },
    {
      title: t('documents.table.source'),
      key: 'sourceType',
      width: 100,
      render: (_, record) => (
        <Tag color={isBuiltInDocument(record) ? 'purple' : 'default'}>{getDocumentSourceTypeText(record.voSourceType, t)}</Tag>
      ),
    },
    {
      title: t('documents.table.version'),
      dataIndex: 'voVersion',
      key: 'voVersion',
      width: 90,
    },
    {
      title: t('documents.table.publishedAt'),
      key: 'voPublishedAt',
      width: 170,
      render: (_, record) => formatDocumentDateTime(record.voPublishedAt, i18n.resolvedLanguage),
    },
    {
      title: t('documents.table.updatedAt'),
      key: 'voModifyTime',
      width: 170,
      render: (_, record) => formatDocumentDateTime(record.voModifyTime ?? record.voCreateTime, i18n.resolvedLanguage),
    },
    {
      title: t('documents.table.actions'),
      key: 'actions',
      width: 420,
      fixed: 'right',
      render: (_, record) => {
        const writeDisabled = record.voIsDeleted || isBuiltInDocument(record);
        return (
          <Space size="small" wrap>
            <Button variant="ghost" size="small" icon={<EyeOutlined />} onClick={() => { void handleViewDetail(record); }}>
              {t('documents.actions.detail')}
            </Button>
            <Button variant="ghost" size="small" icon={<ClockCircleOutlined />} onClick={() => { void openRevisions(record); }}>
              {t('documents.actions.revisions')}
            </Button>
            {canExport ? (
              <Button variant="ghost" size="small" icon={<FileTextOutlined />} onClick={() => { void handleExport(record); }}>
                {t('documents.actions.export')}
              </Button>
            ) : null}
            {!record.voIsDeleted && !isBuiltInDocument(record) && canPublish && record.voStatus !== DOCUMENT_STATUS.published ? (
              <Button
                variant="ghost"
                size="small"
                icon={<CheckOutlined />}
                onClick={() => {
                  void runDocumentAction(
                    () => publishWikiDocument(record.voId, t),
                    t('documents.feedback.published'),
                    t('documents.feedback.publishFailed')
                  );
                }}
              >
                {t('documents.actions.publish')}
              </Button>
            ) : null}
            {!record.voIsDeleted && !isBuiltInDocument(record) && canPublish && record.voStatus !== DOCUMENT_STATUS.draft ? (
              <Button
                variant="ghost"
                size="small"
                icon={<CloseOutlined />}
                onClick={() => {
                  void runDocumentAction(
                    () => unpublishWikiDocument(record.voId, t),
                    record.voStatus === DOCUMENT_STATUS.archived ? t('documents.feedback.backToDraft') : t('documents.feedback.unpublished'),
                    t('documents.feedback.unpublishFailed')
                  );
                }}
              >
                {record.voStatus === DOCUMENT_STATUS.archived ? t('documents.actions.toDraft') : t('documents.actions.unpublish')}
              </Button>
            ) : null}
            {!record.voIsDeleted && !isBuiltInDocument(record) && canArchive && record.voStatus !== DOCUMENT_STATUS.archived ? (
              <Button
                variant="ghost"
                size="small"
                icon={<SyncOutlined />}
                onClick={() => {
                  void runDocumentAction(
                    () => archiveWikiDocument(record.voId, t),
                    t('documents.feedback.archived'),
                    t('documents.feedback.archiveFailed')
                  );
                }}
              >
                {t('documents.actions.archive')}
              </Button>
            ) : null}
            {!writeDisabled && canUpdatePermissions ? (
              <Button variant="ghost" size="small" icon={<LockOutlined />} onClick={() => openAccessPolicy(record)}>
                {t('documents.actions.permissions')}
              </Button>
            ) : null}
            {record.voIsDeleted && !isBuiltInDocument(record) && canRestore ? (
              <Button
                variant="ghost"
                size="small"
                icon={<UnlockOutlined />}
                onClick={() => {
                  void runDocumentAction(
                    () => restoreWikiDocument(record.voId, t),
                    t('documents.feedback.restored'),
                    t('documents.feedback.restoreFailed')
                  );
                }}
              >
                {t('documents.actions.restore')}
              </Button>
            ) : null}
            {!writeDisabled && canDelete ? (
              <Popconfirm
                title={t('documents.delete.title')}
                description={t('documents.delete.description')}
                okText={t('documents.actions.confirm')}
                cancelText={t('documents.actions.cancel')}
                onConfirm={() => {
                  void runDocumentAction(
                    () => deleteWikiDocument(record.voId, t),
                    t('documents.feedback.deleted'),
                    t('documents.feedback.deleteFailed')
                  );
                }}
              >
                <Button variant="danger" size="small" icon={<DeleteOutlined />}>
                  {t('documents.actions.delete')}
                </Button>
              </Popconfirm>
            ) : null}
          </Space>
        );
      },
    },
  ];

  const documentHeaderActions = (
    <>
      <input
        ref={importInputRef}
        type="file"
        accept=".md,.markdown,.txt,text/markdown,text/plain"
        className="admin-form-hidden-input"
        onChange={(event) => {
          void handleImportFile(event);
        }}
      />
      {canImport ? (
        <Button
          variant="primary"
          icon={<FileTextOutlined />}
          disabled={importing}
          onClick={() => importInputRef.current?.click()}
        >
          {importing ? t('documents.actions.importing') : t('documents.actions.import')}
        </Button>
      ) : null}
      <Button
        variant="ghost"
        icon={<ReloadOutlined />}
        onClick={() => {
          void loadDocuments();
        }}
      >
        {t('documents.actions.refresh')}
      </Button>
    </>
  );

  return (
    <div className="admin-feature-page document-governance-page">
      <ConsolePageHeader
        eyebrow={t('documents.page.eyebrow')}
        title={t('documents.page.title')}
        description={t('documents.page.description')}
        icon={<FileTextOutlined />}
        status={(
          <ConsoleStatusChip tone={canView ? 'success' : 'danger'}>
            {canView ? t('documents.page.viewable') : t('documents.page.forbidden')}
          </ConsoleStatusChip>
        )}
        actions={documentHeaderActions}
      />

      <ConsoleMetricGrid label={t('documents.page.metricsAriaLabel')}>
        <ConsoleMetricCard label={t('documents.metrics.documents')} value={formatDocumentNumber(documents.length, i18n.resolvedLanguage)} description={t('documents.metrics.documentsDescription')} tone="info" />
        <ConsoleMetricCard label={t('documents.metrics.published')} value={formatDocumentNumber(publishedCount, i18n.resolvedLanguage)} description={t('documents.metrics.publishedDescription')} tone="success" />
        <ConsoleMetricCard label={t('documents.metrics.restricted')} value={formatDocumentNumber(restrictedCount, i18n.resolvedLanguage)} description={t('documents.metrics.restrictedDescription')} tone="warning" />
        <ConsoleMetricCard label={t('documents.metrics.builtIn')} value={formatDocumentNumber(builtInCount, i18n.resolvedLanguage)} description={t('documents.metrics.builtInDescription')} />
      </ConsoleMetricGrid>

      <section className="governance-task-flow" aria-label={t('documents.page.flowAriaLabel')}>
        <div className="governance-task-flow__item">
          <span>1</span>
          <strong>{t('documents.flow.listTitle')}</strong>
          <p>{t('documents.flow.listDescription', { total, current: documents.length })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>2</span>
          <strong>{t('documents.flow.statusTitle')}</strong>
          <p>{t('documents.flow.statusDescription', { published: publishedCount, draft: draftCount, archived: archivedCount })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>3</span>
          <strong>{t('documents.flow.accessTitle')}</strong>
          <p>{t('documents.flow.accessDescription', { count: restrictedCount })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>4</span>
          <strong>{t('documents.flow.revisionTitle')}</strong>
          <p>{governanceDocument
            ? t('documents.flow.revisionSelected', { status: getDocumentStatusText(governanceDocument.voStatus, t), version: governanceDocument.voVersion })
            : t('documents.flow.revisionEmpty')}{t('documents.flow.revisionSuffix')}</p>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title={t('documents.filter.title')}
            description={t('documents.filter.description')}
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? t('documents.filter.active', { count: activeFilterCount }) : t('documents.filter.none')}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Input
                allowClear
                prefix={<SearchOutlined />}
                placeholder={t('documents.filter.placeholder')}
                value={keyword}
                onChange={(event) => setKeyword(event.target.value)}
              />
              <Select value={statusFilter} options={statusOptions} onChange={setStatusFilter} />
              <Select value={visibilityFilter} options={visibilityOptions} onChange={setVisibilityFilter} />
              <Select value={sourceTypeFilter} options={sourceTypeOptions} onChange={setSourceTypeFilter} />
              <Select value={deletedFilter} options={deletedOptions} onChange={setDeletedFilter} />
            </div>
          </ConsoleToolbar>

          <section className="admin-table-panel">
            <Table
              rowKey="voId"
              loading={loading}
              columns={columns}
              dataSource={documents}
              scroll={{ x: 1500 }}
              pagination={{
                current: pageIndex,
                pageSize,
                total,
                showSizeChanger: true,
                onChange: (nextPage, nextPageSize) => {
                  void loadDocuments(nextPage, nextPageSize);
                },
              }}
            />
          </section>
        </main>

        <aside className="admin-table-aside" aria-label={t('documents.rail.ariaLabel')}>
          <div className="admin-feature-rail__header">
            <div>
              <span className="admin-feature-rail__eyebrow">{t('documents.rail.eyebrow')}</span>
              <h3>{t('documents.rail.title')}</h3>
            </div>
            <ConsoleStatusChip tone={canView ? 'success' : 'danger'}>
              {canView ? t('documents.page.viewable') : t('documents.page.forbidden')}
            </ConsoleStatusChip>
          </div>
          <p className="admin-feature-subtle">{t('documents.rail.description')}</p>
          <div className="admin-feature-rail__list">
            <div className="admin-feature-rail__item">
              <span>{t('documents.rail.queryScope')}</span>
              <strong>{activeFilterCount > 0 ? t('documents.filter.active', { count: activeFilterCount }) : t('documents.filter.none')}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>{t('documents.rail.publishGovernance')}</span>
              <strong>{canPublish || canArchive ? t('documents.rail.publishWritable') : t('documents.rail.publishReadOnly')}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>{t('documents.rail.accessPolicy')}</span>
              <strong>{canUpdatePermissions ? t('documents.rail.accessWritable') : t('documents.rail.accessReadOnly')}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>{t('documents.rail.revisionGovernance')}</span>
              <strong>{canRollback ? t('documents.rail.revisionWritable') : t('documents.rail.revisionReadOnly')}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>{t('documents.rail.recycleBin')}</span>
              <strong>{deletedCount > 0 ? t('documents.rail.recycleBinCount', { count: deletedCount }) : t('documents.rail.recycleBinEmpty')}</strong>
            </div>
          </div>
          {governanceDocument ? (
            <>
              <div className="admin-feature-rail__callout">
                <span>{t('documents.rail.currentDocument')}</span>
                <strong>{governanceDocument.voTitle}</strong>
                <p>{getDocumentSummary(governanceDocument, t)}</p>
                <Space wrap>
                  {getStatusTag(governanceDocument.voStatus, t)}
                  {getVisibilityTag(governanceDocument.voVisibility, t)}
                  <Tag color={isBuiltInDocument(governanceDocument) ? 'purple' : 'default'}>{getDocumentSourceTypeText(governanceDocument.voSourceType, t)}</Tag>
                  {governanceDocument.voIsDeleted ? <Tag color="default">{t('documents.status.recycleBin')}</Tag> : null}
                </Space>
              </div>
              <div className="admin-feature-rail__list">
                <div className="admin-feature-rail__item">
                  <span>{t('documents.rail.accessPolicy')}</span>
                  <strong>{getDocumentAccessSummary(governanceDocument, t)}</strong>
                </div>
                <div className="admin-feature-rail__item">
                  <span>{t('documents.rail.revisionEvidence')}</span>
                  <strong>{governanceDocumentRevisionCount > 0
                    ? t('documents.rail.revisionWithCount', { versions: t('documents.count.versions', { count: governanceDocumentRevisionCount }), version: governanceDocument.voVersion })
                    : t('documents.rail.currentVersion', { version: governanceDocument.voVersion })}</strong>
                </div>
                <div className="admin-feature-rail__item">
                  <span>{t('documents.rail.sourcePath')}</span>
                  <strong>{governanceDocument.voSourcePath || governanceDocument.voSlug}</strong>
                </div>
              </div>
              <div className="admin-feature-rail__actions">
                <Button variant="ghost" size="small" icon={<EyeOutlined />} onClick={() => { void handleViewDetail(governanceDocument); }}>
                  {t('documents.actions.detail')}
                </Button>
                <Button variant="ghost" size="small" icon={<ClockCircleOutlined />} onClick={() => { void openRevisions(governanceDocument); }}>
                  {t('documents.actions.revisions')}
                </Button>
                {canUpdatePermissions && canMaintainDocument(governanceDocument) ? (
                  <Button variant="ghost" size="small" icon={<LockOutlined />} onClick={() => openAccessPolicy(governanceDocument)}>
                    {t('documents.rail.accessPolicy')}
                  </Button>
                ) : null}
                {canExport ? (
                  <Button variant="ghost" size="small" icon={<FileTextOutlined />} onClick={() => { void handleExport(governanceDocument); }}>
                    {t('documents.actions.export')}
                  </Button>
                ) : null}
                {canPublish && canMaintainDocument(governanceDocument) && governanceDocument.voStatus !== DOCUMENT_STATUS.published ? (
                  <Button
                    variant="ghost"
                    size="small"
                    icon={<CheckOutlined />}
                    onClick={() => {
                      void runDocumentAction(
                        () => publishWikiDocument(governanceDocument.voId, t),
                        t('documents.feedback.published'),
                        t('documents.feedback.publishFailed')
                      );
                    }}
                  >
                    {t('documents.actions.publish')}
                  </Button>
                ) : null}
                {canPublish && canMaintainDocument(governanceDocument) && governanceDocument.voStatus === DOCUMENT_STATUS.published ? (
                  <Button
                    variant="ghost"
                    size="small"
                    icon={<CloseOutlined />}
                    onClick={() => {
                      void runDocumentAction(
                        () => unpublishWikiDocument(governanceDocument.voId, t),
                        t('documents.feedback.unpublished'),
                        t('documents.feedback.unpublishFailed')
                      );
                    }}
                  >
                    {t('documents.actions.unpublish')}
                  </Button>
                ) : null}
              </div>
            </>
          ) : (
            <p className="admin-feature-rail__empty">{t('documents.rail.empty')}</p>
          )}
        </aside>
      </div>

      <Modal
        title={t('documents.detail.title')}
        open={detailLoading || Boolean(detailDocument)}
        width={860}
        footer={null}
        onCancel={() => {
          setDetailDocument(null);
          setDetailLoading(false);
        }}
      >
        {detailLoading ? (
          <p className="admin-feature-subtle">{t('documents.detail.loading')}</p>
        ) : detailDocument ? (
          <Space orientation="vertical" size="middle" className="admin-feature-modal-stack">
            <Space wrap>
              {getStatusTag(detailDocument.voStatus, t)}
              {getVisibilityTag(detailDocument.voVisibility, t)}
              <Tag color={isBuiltInDocument(detailDocument) ? 'purple' : 'default'}>{getDocumentSourceTypeText(detailDocument.voSourceType, t)}</Tag>
              {detailDocument.voIsDeleted ? <Tag color="default">{t('documents.status.recycleBin')}</Tag> : null}
            </Space>
            <div>
              <h3>{detailDocument.voTitle}</h3>
              <p className="admin-feature-subtle">{detailDocument.voSlug}</p>
            </div>
            <div className="admin-table-summary">
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('documents.detail.summary')}</span>
                <span className="admin-table-summary__value">{detailDocument.voSummary || '-'}</span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('documents.detail.sourcePath')}</span>
                <span className="admin-table-summary__value">{detailDocument.voSourcePath || '-'}</span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('documents.detail.rolesAndPermissions')}</span>
                <span className="admin-table-summary__value">
                  {(detailDocument.voAllowedRoles || []).join(', ') || '-'} / {(detailDocument.voAllowedPermissions || []).join(', ') || '-'}
                </span>
              </div>
            </div>
            <Input.TextArea value={detailDocument.voMarkdownContent} readOnly rows={12} />
          </Space>
        ) : null}
      </Modal>

      <Modal
        title={t('documents.access.title')}
        open={Boolean(accessDocument)}
        width={640}
        okText={t('documents.actions.save')}
        cancelText={t('documents.actions.cancel')}
        confirmLoading={accessSaving}
        onOk={() => {
          void saveAccessPolicy();
        }}
        onCancel={() => setAccessDocument(null)}
      >
        <Space orientation="vertical" size="middle" className="admin-feature-modal-stack">
          <p className="admin-feature-subtle">{t('documents.access.description')}</p>
          <Select
            className="admin-feature-control-full"
            value={accessVisibility}
            options={visibilityOptions.filter((option) => option.value !== 'all')}
            onChange={setAccessVisibility}
          />
          <Input.TextArea
            rows={4}
            value={accessRolesText}
            placeholder={t('documents.access.rolesPlaceholder')}
            onChange={(event) => setAccessRolesText(event.target.value)}
          />
          <Input.TextArea
            rows={4}
            value={accessPermissionsText}
            placeholder={t('documents.access.permissionsPlaceholder')}
            onChange={(event) => setAccessPermissionsText(event.target.value)}
          />
        </Space>
      </Modal>

      <Modal
        title={revisionDocument ? t('documents.revision.titleWithDocument', { title: revisionDocument.voTitle }) : t('documents.revision.title')}
        open={Boolean(revisionDocument)}
        width={960}
        footer={null}
        onCancel={() => {
          setRevisionDocument(null);
          setRevisionItems([]);
          setRevisionDetail(null);
        }}
      >
        <div className="admin-table-layout">
          <div className="admin-table-main">
            <div className="admin-table-scroll-region">
              <Table
                rowKey="voId"
                size="small"
                loading={revisionLoading}
                dataSource={revisionItems}
                pagination={false}
                columns={[
                  {
                    title: t('documents.table.version'),
                    dataIndex: 'voVersion',
                    key: 'voVersion',
                    width: 80,
                    render: (version, record) => record.voIsCurrent ? <Tag color="success">v{version}</Tag> : `v${version}`,
                  },
                  {
                    title: t('documents.revision.description'),
                    dataIndex: 'voChangeSummary',
                    key: 'voChangeSummary',
                    render: (summary) => summary || '-',
                  },
                  {
                    title: t('documents.revision.time'),
                    key: 'voCreateTime',
                    width: 170,
                    render: (_, record) => formatDocumentDateTime(record.voCreateTime, i18n.resolvedLanguage),
                  },
                  {
                    title: t('documents.revision.actions'),
                    key: 'actions',
                    width: 170,
                    render: (_, record) => (
                      <Space size="small" wrap>
                        <Button
                          variant="ghost"
                          size="small"
                          onClick={() => {
                            void loadRevisionDetail(record.voId);
                          }}
                        >
                          {t('documents.actions.view')}
                        </Button>
                        {canRollback && revisionDocument && !isBuiltInDocument(revisionDocument) && !revisionDocument.voIsDeleted && !record.voIsCurrent ? (
                          <Popconfirm
                            title={t('documents.revision.rollbackTitle')}
                            description={t('documents.revision.rollbackDescription', { version: record.voVersion })}
                            okText={t('documents.actions.confirm')}
                            cancelText={t('documents.actions.cancel')}
                            onConfirm={() => {
                              void handleRollback(record.voId);
                            }}
                          >
                            <Button variant="ghost" size="small">
                              {t('documents.actions.rollback')}
                            </Button>
                          </Popconfirm>
                        ) : null}
                      </Space>
                    ),
                  },
                ]}
              />
            </div>
          </div>
          <aside className="admin-table-aside">
            <h3>{t('documents.revision.contentTitle')}</h3>
            {revisionDetailLoading ? (
              <p className="admin-feature-subtle">{t('documents.revision.loadingDetail')}</p>
            ) : revisionDetail ? (
              <Space orientation="vertical" size="middle" className="admin-feature-modal-stack">
                <Tag color={revisionDetail.voIsCurrent ? 'success' : 'default'}>v{revisionDetail.voVersion}</Tag>
                <p className="admin-feature-subtle">{revisionDetail.voChangeSummary || t('documents.revision.noSummary')}</p>
                <Input.TextArea value={revisionDetail.voMarkdownContent} readOnly rows={12} />
              </Space>
            ) : (
              <p className="admin-feature-subtle">{t('documents.revision.select')}</p>
            )}
          </aside>
        </div>
      </Modal>
    </div>
  );
};
