import { useCallback, useEffect, useState } from 'react';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { useTranslation } from 'react-i18next';
import {
  Table,
  Button,
  Space,
  Tag,
  message,
  Popconfirm,
  AntInput as Input,
  AntSelect as Select,
  type TableColumnsType,
} from '@radish/ui';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  ReloadOutlined,
  CheckOutlined,
  CloseOutlined,
  SearchOutlined,
  TagsOutlined,
} from '@radish/ui';
import {
  getTagPage,
  deleteTag,
  restoreTag,
  toggleTagStatus,
  updateTagSort,
  type TagVo,
} from '@/api/tagApi';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import { usePermission } from '@/hooks/usePermission';
import { TagForm } from './TagForm';
import { log } from '@/utils/logger';
import { formatConsoleNumber } from '@/utils/localeFormatters';
import '../adminFeature.css';
import './TagList.css';

export const TagList = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('tags.documentTitle'));

  const [loading, setLoading] = useState(false);
  const [tags, setTags] = useState<TagVo[]>([]);
  const [total, setTotal] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const canViewTags = usePermission(CONSOLE_PERMISSIONS.tagsView);
  const canCreateTag = usePermission(CONSOLE_PERMISSIONS.tagsCreate);
  const canEditTag = usePermission(CONSOLE_PERMISSIONS.tagsEdit);
  const canDeleteTagPermission = usePermission(CONSOLE_PERMISSIONS.tagsDelete);
  const canRestoreTag = usePermission(CONSOLE_PERMISSIONS.tagsRestore);
  const canToggleTag = usePermission(CONSOLE_PERMISSIONS.tagsToggle);
  const canSortTag = usePermission(CONSOLE_PERMISSIONS.tagsSort);

  const [keyword, setKeyword] = useState('');
  const [isEnabled, setIsEnabled] = useState<'all' | 'enabled' | 'disabled'>('all');
  const [isFixed, setIsFixed] = useState<'all' | 'fixed' | 'normal'>('all');
  const [includeDeleted, setIncludeDeleted] = useState(false);

  const [formVisible, setFormVisible] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [editingTag, setEditingTag] = useState<TagVo | undefined>(undefined);
  const activeFilterCount = [
    keyword.trim() ? 'keyword' : undefined,
    isEnabled !== 'all' ? 'status' : undefined,
    isFixed !== 'all' ? 'type' : undefined,
    includeDeleted ? 'deleted' : undefined,
  ].filter(Boolean).length;
  const enabledTags = tags.filter((tag) => tag.voIsEnabled && !tag.voIsDeleted).length;
  const fixedTags = tags.filter((tag) => tag.voIsFixed).length;

  const loadTags = useCallback(async (targetPageIndex = pageIndex, targetPageSize = pageSize) => {
    try {
      setLoading(true);
      const response = await getTagPage({
        pageIndex: targetPageIndex,
        pageSize: targetPageSize,
        keyword: keyword.trim() || undefined,
        isEnabled: isEnabled === 'all' ? undefined : isEnabled === 'enabled',
        isFixed: isFixed === 'all' ? undefined : isFixed === 'fixed',
        includeDeleted,
      });

      setTags(response.data);
      setTotal(response.dataCount);
      setPageIndex(response.page);
      setPageSize(response.pageSize);
    } catch (error) {
      log.error('TagList', '加载标签失败:', error);
      message.error(t('tags.feedback.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [includeDeleted, isEnabled, isFixed, keyword, pageIndex, pageSize, t]);

  useEffect(() => {
    if (!canViewTags) {
      return;
    }

    void loadTags(1, pageSize);
  }, [canViewTags, loadTags, pageSize]);

  const handleCreate = () => {
    setFormMode('create');
    setEditingTag(undefined);
    setFormVisible(true);
  };

  const handleEdit = (record: TagVo) => {
    setFormMode('edit');
    setEditingTag(record);
    setFormVisible(true);
  };

  const handleDelete = async (id: number) => {
    try {
      await deleteTag(id);
      message.success(t('tags.feedback.deleted'));
      await loadTags();
    } catch (error) {
      log.error('TagList', '删除标签失败:', error);
      message.error(t('tags.feedback.deleteFailed'));
    }
  };

  const handleRestore = async (id: number) => {
    try {
      await restoreTag(id);
      message.success(t('tags.feedback.restored'));
      await loadTags();
    } catch (error) {
      log.error('TagList', '恢复标签失败:', error);
      message.error(t('tags.feedback.restoreFailed'));
    }
  };

  const handleToggleStatus = async (record: TagVo, enabled: boolean) => {
    try {
      await toggleTagStatus(record.voId, enabled);
      message.success(t(enabled ? 'tags.feedback.enabled' : 'tags.feedback.disabled'));
      await loadTags();
    } catch (error) {
      log.error('TagList', '更新标签状态失败:', error);
      message.error(t('tags.feedback.toggleFailed'));
    }
  };

  const handleSortChange = async (record: TagVo, value: string) => {
    const parsed = Number(value);
    if (!Number.isFinite(parsed) || parsed < 0) {
      message.error(t('taxonomy.common.sortInvalid'));
      return;
    }

    if (parsed === record.voSortOrder) {
      return;
    }

    try {
      await updateTagSort(record.voId, parsed);
      message.success(t('taxonomy.common.sortUpdated'));
      await loadTags();
    } catch (error) {
      log.error('TagList', '更新排序失败:', error);
      message.error(t('tags.feedback.sortFailed'));
    }
  };

  const columns: TableColumnsType<TagVo> = [
    {
      title: 'ID',
      dataIndex: 'voId',
      key: 'voId',
      width: 100,
    },
    {
      title: t('tags.table.name'),
      dataIndex: 'voName',
      key: 'voName',
      width: 200,
    },
    {
      title: t('taxonomy.common.slug'),
      dataIndex: 'voSlug',
      key: 'voSlug',
      width: 220,
    },
    {
      title: t('tags.table.type'),
      key: 'voIsFixed',
      width: 120,
      render: (_, record) => (
        <Tag color={record.voIsFixed ? 'blue' : 'default'}>
          {t(record.voIsFixed ? 'tags.type.fixed' : 'tags.type.normal')}
        </Tag>
      ),
    },
    {
      title: t('taxonomy.common.status'),
      key: 'voIsEnabled',
      width: 100,
      render: (_, record) => (
        record.voIsDeleted
          ? <Tag color="default">{t('taxonomy.common.deleted')}</Tag>
          : (
              <Tag color={record.voIsEnabled ? 'success' : 'error'}>
                {t(record.voIsEnabled ? 'taxonomy.common.enabled' : 'taxonomy.common.disabled')}
              </Tag>
            )
      ),
    },
    {
      title: t('taxonomy.common.posts'),
      dataIndex: 'voPostCount',
      key: 'voPostCount',
      width: 100,
      render: (value: number) => formatConsoleNumber(value, language),
    },
    {
      title: t('taxonomy.common.sort'),
      key: 'voSortOrder',
      width: 140,
      render: (_, record) => (
        <Input
          disabled={!canSortTag}
          defaultValue={String(record.voSortOrder)}
          onBlur={(event) => {
            void handleSortChange(record, event.target.value);
          }}
        />
      ),
    },
    {
      title: t('taxonomy.common.description'),
      dataIndex: 'voDescription',
      key: 'voDescription',
      ellipsis: true,
    },
    {
      title: t('taxonomy.common.actions'),
      key: 'actions',
      width: 300,
      fixed: 'right',
      render: (_, record) => (
        <Space size="small" wrap>
          {record.voIsDeleted && canRestoreTag && (
            <Button
              variant="ghost"
              size="small"
              onClick={() => {
                void handleRestore(record.voId);
              }}
            >
              {t('taxonomy.common.restore')}
            </Button>
          )}

          {!record.voIsDeleted && (
            <>
              {canEditTag ? (
                <Button variant="ghost" size="small" icon={<EditOutlined />} onClick={() => handleEdit(record)}>
                  {t('taxonomy.common.edit')}
                </Button>
              ) : null}

              {canToggleTag ? (record.voIsEnabled ? (
                <Button
                  variant="ghost"
                  size="small"
                  icon={<CloseOutlined />}
                  onClick={() => {
                    void handleToggleStatus(record, false);
                  }}
                >
                  {t('taxonomy.common.disabled')}
                </Button>
              ) : (
                <Button
                  variant="ghost"
                  size="small"
                  icon={<CheckOutlined />}
                  onClick={() => {
                    void handleToggleStatus(record, true);
                  }}
                >
                  {t('taxonomy.common.enabled')}
                </Button>
              )) : null}

              {canDeleteTagPermission ? (
                <Popconfirm
                  title={t('tags.delete.title')}
                  description={t('tags.delete.description')}
                  onConfirm={() => {
                    void handleDelete(record.voId);
                  }}
                  okText={t('taxonomy.common.confirm')}
                  cancelText={t('taxonomy.common.cancel')}
                >
                  <Button variant="danger" size="small" icon={<DeleteOutlined />}>
                    {t('taxonomy.common.delete')}
                  </Button>
                </Popconfirm>
              ) : null}
            </>
          )}
        </Space>
      ),
    },
  ];
  return (
    <div className="admin-feature-page tag-list-page">
      <ConsolePageHeader
        eyebrow={t('tags.page.eyebrow')}
        title={t('tags.page.title')}
        description={t('tags.page.description')}
        icon={<TagsOutlined />}
        status={(
          <ConsoleStatusChip tone={canCreateTag ? 'success' : 'neutral'}>
            {t(canCreateTag ? 'taxonomy.common.createWritable' : 'taxonomy.common.readOnly')}
          </ConsoleStatusChip>
        )}
        actions={(
          <>
            <Button icon={<ReloadOutlined />} onClick={() => {
              void loadTags();
            }}>
              {t('taxonomy.common.refresh')}
            </Button>
            {canCreateTag ? (
              <Button variant="primary" icon={<PlusOutlined />} onClick={handleCreate}>
                {t('tags.actions.create')}
              </Button>
            ) : null}
          </>
        )}
      />

      <ConsoleMetricGrid label={t('tags.metrics.ariaLabel')}>
        <ConsoleMetricCard label={t('tags.metrics.result')} value={formatConsoleNumber(total, language)} description={t('tags.metrics.resultDescription')} tone="info" />
        <ConsoleMetricCard label={t('tags.metrics.page')} value={formatConsoleNumber(tags.length, language)} description={t('tags.metrics.pageDescription')} />
        <ConsoleMetricCard label={t('tags.metrics.enabled')} value={formatConsoleNumber(enabledTags, language)} description={t('tags.metrics.enabledDescription')} tone="success" />
        <ConsoleMetricCard label={t('tags.metrics.fixed')} value={formatConsoleNumber(fixedTags, language)} description={t('tags.metrics.fixedDescription')} tone="warning" />
      </ConsoleMetricGrid>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title={t('tags.filter.title')}
            description={t('tags.filter.description')}
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? t('taxonomy.common.filterCount', { count: activeFilterCount }) : t('taxonomy.common.notFiltered')}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Input
                className="tag-list-filter-input"
                allowClear
                placeholder={t('tags.filter.placeholder')}
                prefix={<SearchOutlined />}
                value={keyword}
                onChange={(event) => setKeyword(event.target.value)}
              />

              <Select
                className="tag-list-filter-select"
                value={isFixed}
                onChange={(value) => setIsFixed(value)}
                options={[
                  { label: t('tags.type.all'), value: 'all' },
                  { label: t('tags.type.fixed'), value: 'fixed' },
                  { label: t('tags.type.normal'), value: 'normal' },
                ]}
              />

              <Select
                className="tag-list-filter-select"
                value={isEnabled}
                onChange={(value) => setIsEnabled(value)}
                options={[
                  { label: t('taxonomy.common.allStatus'), value: 'all' },
                  { label: t('taxonomy.common.enabled'), value: 'enabled' },
                  { label: t('taxonomy.common.disabled'), value: 'disabled' },
                ]}
              />

              <Select
                className="tag-list-filter-select tag-list-filter-select--deleted"
                value={includeDeleted ? 'yes' : 'no'}
                onChange={(value) => setIncludeDeleted(value === 'yes')}
                options={[
                  { label: t('taxonomy.common.hideDeleted'), value: 'no' },
                  { label: t('taxonomy.common.showDeleted'), value: 'yes' },
                ]}
              />
            </div>
          </ConsoleToolbar>

          <section className="admin-table-panel">
            <Table<TagVo>
              rowKey="voId"
              columns={columns}
              dataSource={tags}
              loading={loading}
              pagination={{
                current: pageIndex,
                pageSize,
                total,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (count) => t('taxonomy.common.total', { count }),
                onChange: (current, size) => {
                  void loadTags(current, size);
                },
              }}
              scroll={{ x: 1450 }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>{t('tags.summary.title')}</h3>
          <p className="admin-feature-subtle">{t('tags.summary.description')}</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('tags.summary.scope')}</span>
              <span className="admin-table-summary__value">
                {activeFilterCount > 0 ? t('taxonomy.common.filterSummary', { count: activeFilterCount }) : t('tags.summary.all')}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('tags.summary.deleted')}</span>
              <span className="admin-table-summary__value">{t(includeDeleted ? 'taxonomy.common.recordsShown' : 'taxonomy.common.recordsHidden')}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('tags.summary.pageSize')}</span>
              <span className="admin-table-summary__value">{t('taxonomy.common.pageSize', { count: pageSize })}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('tags.summary.sort')}</span>
              <span className="admin-table-summary__value">
                {t(canSortTag ? 'taxonomy.common.sortWritable' : 'taxonomy.common.sortReadOnly')}
              </span>
            </div>
          </div>
        </aside>
      </div>

      <TagForm
        visible={formVisible}
        mode={formMode}
        tag={editingTag}
        onCancel={() => setFormVisible(false)}
        onSuccess={() => {
          setFormVisible(false);
          void loadTags();
        }}
      />
    </div>
  );
};
