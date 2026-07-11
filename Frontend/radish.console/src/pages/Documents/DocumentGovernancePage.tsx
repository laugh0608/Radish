import { type ChangeEvent, useEffect, useRef, useState } from 'react';
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
import '../adminForm.css';
import '../adminFeature.css';
import './DocumentGovernancePage.css';

const DOCUMENT_STATUS = {
  draft: 0,
  published: 1,
  archived: 2,
} as const;

const DOCUMENT_VISIBILITY = {
  public: 1,
  authenticated: 2,
  restricted: 3,
} as const;

const statusOptions = [
  { label: '全部状态', value: 'all' },
  { label: '草稿', value: String(DOCUMENT_STATUS.draft) },
  { label: '已发布', value: String(DOCUMENT_STATUS.published) },
  { label: '已归档', value: String(DOCUMENT_STATUS.archived) },
];

const visibilityOptions = [
  { label: '全部可见性', value: 'all' },
  { label: '公开', value: String(DOCUMENT_VISIBILITY.public) },
  { label: '登录可见', value: String(DOCUMENT_VISIBILITY.authenticated) },
  { label: '受限', value: String(DOCUMENT_VISIBILITY.restricted) },
];

const sourceTypeOptions = [
  { label: '全部来源', value: 'all' },
  { label: '手写文档', value: 'Custom' },
  { label: '导入文档', value: 'Imported' },
  { label: '内置文档', value: 'BuiltIn' },
];

const deletedOptions = [
  { label: '未删除', value: 'active' },
  { label: '含回收站', value: 'all' },
  { label: '仅回收站', value: 'deleted' },
];

function getStatusTag(status: number) {
  if (status === DOCUMENT_STATUS.published) {
    return <Tag color="success">已发布</Tag>;
  }

  if (status === DOCUMENT_STATUS.archived) {
    return <Tag color="default">已归档</Tag>;
  }

  return <Tag color="warning">草稿</Tag>;
}

function getVisibilityTag(visibility: number) {
  if (visibility === DOCUMENT_VISIBILITY.public) {
    return <Tag color="green">公开</Tag>;
  }

  if (visibility === DOCUMENT_VISIBILITY.restricted) {
    return <Tag color="red">受限</Tag>;
  }

  return <Tag color="blue">登录可见</Tag>;
}

function getSourceTypeText(sourceType: string) {
  if (sourceType === 'BuiltIn') {
    return '内置';
  }

  if (sourceType === 'Imported') {
    return '导入';
  }

  return '手写';
}

function formatDateTime(value?: string | null) {
  if (!value) {
    return '-';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString('zh-CN', { hour12: false });
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

function getStatusText(status: number) {
  if (status === DOCUMENT_STATUS.published) {
    return '已发布';
  }

  if (status === DOCUMENT_STATUS.archived) {
    return '已归档';
  }

  return '草稿';
}

function getVisibilityText(visibility: number) {
  if (visibility === DOCUMENT_VISIBILITY.public) {
    return '公开';
  }

  if (visibility === DOCUMENT_VISIBILITY.restricted) {
    return '受限';
  }

  return '登录可见';
}

function getDocumentSummary(record: WikiDocumentVo | WikiDocumentDetailVo) {
  const summary = record.voSummary?.trim();
  if (!summary) {
    return '暂无摘要。';
  }

  return summary.length > 88 ? `${summary.slice(0, 88)}...` : summary;
}

function getAccessSummary(record: WikiDocumentVo | WikiDocumentDetailVo) {
  if (record.voVisibility !== DOCUMENT_VISIBILITY.restricted) {
    return getVisibilityText(record.voVisibility);
  }

  const roles = record.voAllowedRoles?.length ?? 0;
  const permissions = record.voAllowedPermissions?.length ?? 0;
  return `受限：${roles} 个角色 / ${permissions} 个权限`;
}

export const DocumentGovernancePage = () => {
  useDocumentTitle('文档治理');

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
      });

      setDocuments(page.data);
      setTotal(page.dataCount);
      setPageIndex(page.page);
      setPageSize(page.pageSize);
    } catch (error) {
      log.error('DocumentGovernancePage', '加载文档治理列表失败:', error);
      message.error(error instanceof Error ? error.message : '加载文档治理列表失败');
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
      const detail = await getWikiGovernanceDetail(record.voId, true);
      setDetailDocument(detail);
    } catch (error) {
      log.error('DocumentGovernancePage', '加载文档详情失败:', error);
      message.error(error instanceof Error ? error.message : '加载文档详情失败');
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
      message.error('当前文档不可调整访问策略');
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
      message.error('当前文档不可调整访问策略');
      return;
    }

    const allowedRoles = splitAccessList(accessRolesText);
    const allowedPermissions = splitAccessList(accessPermissionsText);
    const visibility = Number(accessVisibility);
    if (visibility === DOCUMENT_VISIBILITY.restricted && allowedRoles.length === 0 && allowedPermissions.length === 0) {
      message.error('受限文档至少需要配置一个角色或权限');
      return;
    }

    try {
      setAccessSaving(true);
      await updateWikiAccessPolicy(accessDocument.voId, {
        visibility,
        allowedRoles,
        allowedPermissions,
      });
      message.success('访问策略已更新');
      setAccessDocument(null);
      await loadDocuments();
    } catch (error) {
      log.error('DocumentGovernancePage', '更新文档访问策略失败:', error);
      message.error(error instanceof Error ? error.message : '更新文档访问策略失败');
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
      const revisions = await getWikiRevisionList(record.voId);
      setRevisionItems(revisions);
      if (revisions[0]) {
        await loadRevisionDetail(revisions[0].voId);
      }
    } catch (error) {
      log.error('DocumentGovernancePage', '加载版本列表失败:', error);
      message.error(error instanceof Error ? error.message : '加载版本列表失败');
    } finally {
      setRevisionLoading(false);
    }
  };

  const loadRevisionDetail = async (revisionId: LongId) => {
    setRevisionDetailLoading(true);
    try {
      const detail = await getWikiRevisionDetail(revisionId);
      setRevisionDetail(detail);
    } catch (error) {
      log.error('DocumentGovernancePage', '加载版本详情失败:', error);
      message.error(error instanceof Error ? error.message : '加载版本详情失败');
    } finally {
      setRevisionDetailLoading(false);
    }
  };

  const handleRollback = async (revisionId: LongId) => {
    if (!canRollback || !revisionDocument || revisionDocument.voIsDeleted || isBuiltInDocument(revisionDocument)) {
      message.error('当前版本不可回滚');
      return;
    }

    await runDocumentAction(
      async () => {
        await rollbackWikiRevision(revisionId);
        if (revisionDocument) {
          const revisions = await getWikiRevisionList(revisionDocument.voId);
          setRevisionItems(revisions);
        }
      },
      '版本已回滚',
      '回滚版本失败'
    );
  };

  const handleImportFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) {
      return;
    }

    if (!canImport) {
      message.error('无文档导入权限');
      return;
    }

    if (!/\.(md|markdown|txt)$/i.test(file.name)) {
      message.error('仅支持 .md、.markdown 或 .txt 文件');
      return;
    }

    try {
      setImporting(true);
      const id = await importWikiMarkdown(file);
      message.success(`Markdown 已导入，文档 ID：${id}`);
      await loadDocuments(1, pageSize);
    } catch (error) {
      log.error('DocumentGovernancePage', '导入 Markdown 失败:', error);
      message.error(error instanceof Error ? error.message : '导入 Markdown 失败');
    } finally {
      setImporting(false);
    }
  };

  const handleExport = async (record: WikiDocumentVo) => {
    if (!canExport) {
      message.error('无文档导出权限');
      return;
    }

    try {
      const result = await exportWikiMarkdown(record.voId);
      triggerDownload(result.blob, result.fileName);
      message.success('Markdown 已导出');
    } catch (error) {
      log.error('DocumentGovernancePage', '导出 Markdown 失败:', error);
      message.error(error instanceof Error ? error.message : '导出 Markdown 失败');
    }
  };

  const columns: TableColumnsType<WikiDocumentVo> = [
    {
      title: '文档',
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
      title: '状态',
      key: 'status',
      width: 110,
      render: (_, record) => record.voIsDeleted ? <Tag color="default">回收站</Tag> : getStatusTag(record.voStatus),
    },
    {
      title: '可见性',
      key: 'visibility',
      width: 120,
      render: (_, record) => getVisibilityTag(record.voVisibility),
    },
    {
      title: '来源',
      key: 'sourceType',
      width: 100,
      render: (_, record) => (
        <Tag color={isBuiltInDocument(record) ? 'purple' : 'default'}>{getSourceTypeText(record.voSourceType)}</Tag>
      ),
    },
    {
      title: '版本',
      dataIndex: 'voVersion',
      key: 'voVersion',
      width: 90,
    },
    {
      title: '发布时间',
      key: 'voPublishedAt',
      width: 170,
      render: (_, record) => formatDateTime(record.voPublishedAt),
    },
    {
      title: '更新时间',
      key: 'voModifyTime',
      width: 170,
      render: (_, record) => formatDateTime(record.voModifyTime ?? record.voCreateTime),
    },
    {
      title: '操作',
      key: 'actions',
      width: 420,
      fixed: 'right',
      render: (_, record) => {
        const writeDisabled = record.voIsDeleted || isBuiltInDocument(record);
        return (
          <Space size="small" wrap>
            <Button variant="ghost" size="small" icon={<EyeOutlined />} onClick={() => { void handleViewDetail(record); }}>
              详情
            </Button>
            <Button variant="ghost" size="small" icon={<ClockCircleOutlined />} onClick={() => { void openRevisions(record); }}>
              版本
            </Button>
            {canExport ? (
              <Button variant="ghost" size="small" icon={<FileTextOutlined />} onClick={() => { void handleExport(record); }}>
                导出
              </Button>
            ) : null}
            {!record.voIsDeleted && !isBuiltInDocument(record) && canPublish && record.voStatus !== DOCUMENT_STATUS.published ? (
              <Button
                variant="ghost"
                size="small"
                icon={<CheckOutlined />}
                onClick={() => {
                  void runDocumentAction(
                    () => publishWikiDocument(record.voId),
                    '文档已发布',
                    '发布文档失败'
                  );
                }}
              >
                发布
              </Button>
            ) : null}
            {!record.voIsDeleted && !isBuiltInDocument(record) && canPublish && record.voStatus !== DOCUMENT_STATUS.draft ? (
              <Button
                variant="ghost"
                size="small"
                icon={<CloseOutlined />}
                onClick={() => {
                  void runDocumentAction(
                    () => unpublishWikiDocument(record.voId),
                    record.voStatus === DOCUMENT_STATUS.archived ? '文档已转回草稿' : '文档已下架',
                    '下架文档失败'
                  );
                }}
              >
                {record.voStatus === DOCUMENT_STATUS.archived ? '转草稿' : '下架'}
              </Button>
            ) : null}
            {!record.voIsDeleted && !isBuiltInDocument(record) && canArchive && record.voStatus !== DOCUMENT_STATUS.archived ? (
              <Button
                variant="ghost"
                size="small"
                icon={<SyncOutlined />}
                onClick={() => {
                  void runDocumentAction(
                    () => archiveWikiDocument(record.voId),
                    '文档已归档',
                    '归档文档失败'
                  );
                }}
              >
                归档
              </Button>
            ) : null}
            {!writeDisabled && canUpdatePermissions ? (
              <Button variant="ghost" size="small" icon={<LockOutlined />} onClick={() => openAccessPolicy(record)}>
                权限
              </Button>
            ) : null}
            {record.voIsDeleted && !isBuiltInDocument(record) && canRestore ? (
              <Button
                variant="ghost"
                size="small"
                icon={<UnlockOutlined />}
                onClick={() => {
                  void runDocumentAction(
                    () => restoreWikiDocument(record.voId),
                    '文档已恢复',
                    '恢复文档失败'
                  );
                }}
              >
                恢复
              </Button>
            ) : null}
            {!writeDisabled && canDelete ? (
              <Popconfirm
                title="移入回收站"
                description="确定要把该文档移入回收站吗？"
                okText="确认"
                cancelText="取消"
                onConfirm={() => {
                  void runDocumentAction(
                    () => deleteWikiDocument(record.voId),
                    '文档已移入回收站',
                    '删除文档失败'
                  );
                }}
              >
                <Button variant="danger" size="small" icon={<DeleteOutlined />}>
                  删除
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
          {importing ? '导入中...' : '导入 Markdown'}
        </Button>
      ) : null}
      <Button
        variant="ghost"
        icon={<ReloadOutlined />}
        onClick={() => {
          void loadDocuments();
        }}
      >
        刷新
      </Button>
    </>
  );

  return (
    <div className="admin-feature-page document-governance-page">
      <ConsolePageHeader
        eyebrow="文档治理"
        title="文档治理"
        description="治理文档发布状态、访问策略、版本回滚、导入导出与回收站。"
        icon={<FileTextOutlined />}
        status={(
          <ConsoleStatusChip tone={canView ? 'success' : 'danger'}>
            {canView ? '可查看' : '无权限'}
          </ConsoleStatusChip>
        )}
        actions={documentHeaderActions}
      />

      <ConsoleMetricGrid label="文档治理指标">
        <ConsoleMetricCard label="当前页文档" value={documents.length} description="当前页可见文档" tone="info" />
        <ConsoleMetricCard label="当前页已发布" value={publishedCount} description="已发布且未删除" tone="success" />
        <ConsoleMetricCard label="当前页受限" value={restrictedCount} description="受限访问策略文档" tone="warning" />
        <ConsoleMetricCard label="当前页内置" value={builtInCount} description="内置文档不可直接写入" />
      </ConsoleMetricGrid>

      <section className="governance-task-flow" aria-label="文档治理任务流">
        <div className="governance-task-flow__item">
          <span>1</span>
          <strong>列表定位</strong>
          <p>{total} 个匹配文档，当前页 {documents.length} 个；筛选条件先限定治理范围。</p>
        </div>
        <div className="governance-task-flow__item">
          <span>2</span>
          <strong>发布状态</strong>
          <p>当前页 {publishedCount} 个已发布、{draftCount} 个草稿、{archivedCount} 个归档，发布 / 下架继续走既有动作。</p>
        </div>
        <div className="governance-task-flow__item">
          <span>3</span>
          <strong>访问策略</strong>
          <p>{restrictedCount} 个受限文档；角色和权限名单只通过现有访问策略弹窗维护。</p>
        </div>
        <div className="governance-task-flow__item">
          <span>4</span>
          <strong>版本回看</strong>
          <p>{governanceDocument ? `${getStatusText(governanceDocument.voStatus)}，当前证据 v${governanceDocument.voVersion}` : '选择文档后回看版本证据'}，回滚仅对可维护文档开放。</p>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title="筛选文档"
            description="按标题、slug、状态、可见性、来源和回收站范围定位治理对象。"
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? `${activeFilterCount} 个条件` : '未筛选'}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Input
                allowClear
                prefix={<SearchOutlined />}
                placeholder="标题、slug、摘要、来源路径"
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

        <aside className="admin-table-aside" aria-label="文档治理详情上下文">
          <div className="admin-feature-rail__header">
            <div>
              <span className="admin-feature-rail__eyebrow">文档档案</span>
              <h3>文档治理详情</h3>
            </div>
            <ConsoleStatusChip tone={canView ? 'success' : 'danger'}>
              {canView ? '可查看' : '无权限'}
            </ConsoleStatusChip>
          </div>
          <p className="admin-feature-subtle">文档治理按列表定位、详情证据、访问策略和版本回滚分区承载，不替代作者态编辑器。</p>
          <div className="admin-feature-rail__list">
            <div className="admin-feature-rail__item">
              <span>查询范围</span>
              <strong>{activeFilterCount > 0 ? `${activeFilterCount} 个筛选条件` : '未筛选'}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>发布治理</span>
              <strong>{canPublish || canArchive ? '可处理发布 / 归档' : '仅查看发布状态'}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>访问策略</span>
              <strong>{canUpdatePermissions ? '可调整可见性' : '无访问策略权限'}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>版本治理</span>
              <strong>{canRollback ? '可回滚非当前版本' : '仅查看版本'}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>回收站</span>
              <strong>{deletedCount > 0 ? `当前页 ${deletedCount} 个` : '当前页无回收站文档'}</strong>
            </div>
          </div>
          {governanceDocument ? (
            <>
              <div className="admin-feature-rail__callout">
                <span>当前文档</span>
                <strong>{governanceDocument.voTitle}</strong>
                <p>{getDocumentSummary(governanceDocument)}</p>
                <Space wrap>
                  {getStatusTag(governanceDocument.voStatus)}
                  {getVisibilityTag(governanceDocument.voVisibility)}
                  <Tag color={isBuiltInDocument(governanceDocument) ? 'purple' : 'default'}>{getSourceTypeText(governanceDocument.voSourceType)}</Tag>
                  {governanceDocument.voIsDeleted ? <Tag color="default">回收站</Tag> : null}
                </Space>
              </div>
              <div className="admin-feature-rail__list">
                <div className="admin-feature-rail__item">
                  <span>访问策略</span>
                  <strong>{getAccessSummary(governanceDocument)}</strong>
                </div>
                <div className="admin-feature-rail__item">
                  <span>版本证据</span>
                  <strong>{governanceDocumentRevisionCount > 0 ? `${governanceDocumentRevisionCount} 个版本，当前 v${governanceDocument.voVersion}` : `当前 v${governanceDocument.voVersion}`}</strong>
                </div>
                <div className="admin-feature-rail__item">
                  <span>来源路径</span>
                  <strong>{governanceDocument.voSourcePath || governanceDocument.voSlug}</strong>
                </div>
              </div>
              <div className="admin-feature-rail__actions">
                <Button variant="ghost" size="small" icon={<EyeOutlined />} onClick={() => { void handleViewDetail(governanceDocument); }}>
                  详情
                </Button>
                <Button variant="ghost" size="small" icon={<ClockCircleOutlined />} onClick={() => { void openRevisions(governanceDocument); }}>
                  版本
                </Button>
                {canUpdatePermissions && canMaintainDocument(governanceDocument) ? (
                  <Button variant="ghost" size="small" icon={<LockOutlined />} onClick={() => openAccessPolicy(governanceDocument)}>
                    访问策略
                  </Button>
                ) : null}
                {canExport ? (
                  <Button variant="ghost" size="small" icon={<FileTextOutlined />} onClick={() => { void handleExport(governanceDocument); }}>
                    导出
                  </Button>
                ) : null}
                {canPublish && canMaintainDocument(governanceDocument) && governanceDocument.voStatus !== DOCUMENT_STATUS.published ? (
                  <Button
                    variant="ghost"
                    size="small"
                    icon={<CheckOutlined />}
                    onClick={() => {
                      void runDocumentAction(
                        () => publishWikiDocument(governanceDocument.voId),
                        '文档已发布',
                        '发布文档失败'
                      );
                    }}
                  >
                    发布
                  </Button>
                ) : null}
                {canPublish && canMaintainDocument(governanceDocument) && governanceDocument.voStatus === DOCUMENT_STATUS.published ? (
                  <Button
                    variant="ghost"
                    size="small"
                    icon={<CloseOutlined />}
                    onClick={() => {
                      void runDocumentAction(
                        () => unpublishWikiDocument(governanceDocument.voId),
                        '文档已下架',
                        '下架文档失败'
                      );
                    }}
                  >
                    下架
                  </Button>
                ) : null}
              </div>
            </>
          ) : (
            <p className="admin-feature-rail__empty">当前页暂无文档，调整筛选条件后会形成治理详情上下文。</p>
          )}
        </aside>
      </div>

      <Modal
        title="文档详情"
        open={detailLoading || Boolean(detailDocument)}
        width={860}
        footer={null}
        onCancel={() => {
          setDetailDocument(null);
          setDetailLoading(false);
        }}
      >
        {detailLoading ? (
          <p className="admin-feature-subtle">正在加载文档详情...</p>
        ) : detailDocument ? (
          <Space orientation="vertical" size="middle" className="admin-feature-modal-stack">
            <Space wrap>
              {getStatusTag(detailDocument.voStatus)}
              {getVisibilityTag(detailDocument.voVisibility)}
              <Tag color={isBuiltInDocument(detailDocument) ? 'purple' : 'default'}>{getSourceTypeText(detailDocument.voSourceType)}</Tag>
              {detailDocument.voIsDeleted ? <Tag color="default">回收站</Tag> : null}
            </Space>
            <div>
              <h3>{detailDocument.voTitle}</h3>
              <p className="admin-feature-subtle">{detailDocument.voSlug}</p>
            </div>
            <div className="admin-table-summary">
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">摘要</span>
                <span className="admin-table-summary__value">{detailDocument.voSummary || '-'}</span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">来源路径</span>
                <span className="admin-table-summary__value">{detailDocument.voSourcePath || '-'}</span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">允许角色 / 权限</span>
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
        title="文档访问策略"
        open={Boolean(accessDocument)}
        width={640}
        okText="保存"
        cancelText="取消"
        confirmLoading={accessSaving}
        onOk={() => {
          void saveAccessPolicy();
        }}
        onCancel={() => setAccessDocument(null)}
      >
        <Space orientation="vertical" size="middle" className="admin-feature-modal-stack">
          <p className="admin-feature-subtle">
            访问策略只影响文档可见性和受限访问列表，不修改正文、发布状态或版本内容。
          </p>
          <Select
            className="admin-feature-control-full"
            value={accessVisibility}
            options={visibilityOptions.filter((option) => option.value !== 'all')}
            onChange={setAccessVisibility}
          />
          <Input.TextArea
            rows={4}
            value={accessRolesText}
            placeholder="允许角色，每行一个，例如 admin"
            onChange={(event) => setAccessRolesText(event.target.value)}
          />
          <Input.TextArea
            rows={4}
            value={accessPermissionsText}
            placeholder="允许权限，每行一个，例如 console.docs.view"
            onChange={(event) => setAccessPermissionsText(event.target.value)}
          />
        </Space>
      </Modal>

      <Modal
        title={revisionDocument ? `版本治理：${revisionDocument.voTitle}` : '版本治理'}
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
                    title: '版本',
                    dataIndex: 'voVersion',
                    key: 'voVersion',
                    width: 80,
                    render: (version, record) => record.voIsCurrent ? <Tag color="success">v{version}</Tag> : `v${version}`,
                  },
                  {
                    title: '说明',
                    dataIndex: 'voChangeSummary',
                    key: 'voChangeSummary',
                    render: (summary) => summary || '-',
                  },
                  {
                    title: '时间',
                    key: 'voCreateTime',
                    width: 170,
                    render: (_, record) => formatDateTime(record.voCreateTime),
                  },
                  {
                    title: '操作',
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
                          查看
                        </Button>
                        {canRollback && revisionDocument && !isBuiltInDocument(revisionDocument) && !revisionDocument.voIsDeleted && !record.voIsCurrent ? (
                          <Popconfirm
                            title="回滚版本"
                            description={`确定要回滚到 v${record.voVersion} 吗？`}
                            okText="确认"
                            cancelText="取消"
                            onConfirm={() => {
                              void handleRollback(record.voId);
                            }}
                          >
                            <Button variant="ghost" size="small">
                              回滚
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
            <h3>版本内容</h3>
            {revisionDetailLoading ? (
              <p className="admin-feature-subtle">正在加载版本详情...</p>
            ) : revisionDetail ? (
              <Space orientation="vertical" size="middle" className="admin-feature-modal-stack">
                <Tag color={revisionDetail.voIsCurrent ? 'success' : 'default'}>v{revisionDetail.voVersion}</Tag>
                <p className="admin-feature-subtle">{revisionDetail.voChangeSummary || '无变更说明'}</p>
                <Input.TextArea value={revisionDetail.voMarkdownContent} readOnly rows={12} />
              </Space>
            ) : (
              <p className="admin-feature-subtle">请选择版本。</p>
            )}
          </aside>
        </div>
      </Modal>
    </div>
  );
};
