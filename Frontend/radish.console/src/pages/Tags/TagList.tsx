import { useCallback, useEffect, useRef, useState } from 'react';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import { useTranslation } from 'react-i18next';
import {
  Table,
  Button,
  Space,
  Tag,
  message,
  Popconfirm,
  BottomSheet,
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
  ConsoleResourceList,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import { usePermission } from '@/hooks/usePermission';
import { TagForm } from './TagForm';
import { log } from '@/utils/logger';
import { formatConsoleNumber } from '@/utils/localeFormatters';
import '../adminFeature.css';
import './TagList.css';

type TagStatusFilter = 'all' | 'enabled' | 'disabled';
type TagTypeFilter = 'all' | 'fixed' | 'normal';
type ResourceReadState = 'loading' | 'ready' | 'unavailable' | 'stale';

interface TagListQuery {
  pageIndex: number;
  pageSize: number;
  keyword: string;
  isEnabled: TagStatusFilter;
  isFixed: TagTypeFilter;
  includeDeleted: boolean;
}

const DEFAULT_QUERY: TagListQuery = {
  pageIndex: 1,
  pageSize: 20,
  keyword: '',
  isEnabled: 'all',
  isFixed: 'all',
  includeDeleted: false,
};

export const TagList = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('tags.documentTitle'));

  const [loading, setLoading] = useState(false);
  const [readState, setReadState] = useState<ResourceReadState>('loading');
  const [tags, setTags] = useState<TagVo[]>([]);
  const [total, setTotal] = useState(0);
  const [query, setQuery] = useState<TagListQuery>(DEFAULT_QUERY);
  const [keywordDraft, setKeywordDraft] = useState(DEFAULT_QUERY.keyword);
  const [statusDraft, setStatusDraft] = useState<TagStatusFilter>(DEFAULT_QUERY.isEnabled);
  const [typeDraft, setTypeDraft] = useState<TagTypeFilter>(DEFAULT_QUERY.isFixed);
  const [includeDeletedDraft, setIncludeDeletedDraft] = useState(DEFAULT_QUERY.includeDeleted);
  const [filterSheetOpen, setFilterSheetOpen] = useState(false);
  const [formVisible, setFormVisible] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [editingTag, setEditingTag] = useState<TagVo | undefined>(undefined);
  const requestSequence = useRef(0);
  const snapshotQueryKey = useRef<string | undefined>(undefined);

  const canViewTags = usePermission(CONSOLE_PERMISSIONS.tagsView);
  const canCreateTag = usePermission(CONSOLE_PERMISSIONS.tagsCreate);
  const canEditTag = usePermission(CONSOLE_PERMISSIONS.tagsEdit);
  const canDeleteTagPermission = usePermission(CONSOLE_PERMISSIONS.tagsDelete);
  const canRestoreTag = usePermission(CONSOLE_PERMISSIONS.tagsRestore);
  const canToggleTag = usePermission(CONSOLE_PERMISSIONS.tagsToggle);
  const canSortTag = usePermission(CONSOLE_PERMISSIONS.tagsSort);

  const activeFilterCount = [
    query.keyword ? 'keyword' : undefined,
    query.isEnabled !== 'all' ? 'status' : undefined,
    query.isFixed !== 'all' ? 'type' : undefined,
    query.includeDeleted ? 'deleted' : undefined,
  ].filter(Boolean).length;
  const enabledTags = tags.filter((tag) => tag.voIsEnabled && !tag.voIsDeleted).length;
  const fixedTags = tags.filter((tag) => tag.voIsFixed).length;
  const pageCount = Math.max(1, Math.ceil(total / query.pageSize));
  const actionsAreAuthoritative = readState === 'ready';

  const loadTags = useCallback(async () => {
    const requestId = requestSequence.current + 1;
    const queryKey = JSON.stringify(query);
    const hasCurrentSnapshot = snapshotQueryKey.current === queryKey;
    requestSequence.current = requestId;

    try {
      setLoading(true);
      setReadState('loading');
      if (!hasCurrentSnapshot) {
        setTags([]);
        setTotal(0);
      }
      const response = await getTagPage({
        pageIndex: query.pageIndex,
        pageSize: query.pageSize,
        keyword: query.keyword || undefined,
        isEnabled: query.isEnabled === 'all' ? undefined : query.isEnabled === 'enabled',
        isFixed: query.isFixed === 'all' ? undefined : query.isFixed === 'fixed',
        includeDeleted: query.includeDeleted,
      });

      if (requestSequence.current !== requestId) {
        return;
      }

      if (response.data.length === 0 && response.dataCount > 0 && query.pageIndex > response.pageCount) {
        setQuery((current) => ({ ...current, pageIndex: Math.max(1, response.pageCount) }));
        return;
      }

      setTags(response.data);
      setTotal(response.dataCount);
      snapshotQueryKey.current = queryKey;
      setReadState('ready');
    } catch (error) {
      if (requestSequence.current !== requestId) {
        return;
      }

      log.error('TagList', '加载标签失败:', error);
      setReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
      message.error(t('tags.feedback.loadFailed'));
    } finally {
      if (requestSequence.current === requestId) {
        setLoading(false);
      }
    }
  }, [query, t]);

  useEffect(() => {
    if (canViewTags) {
      void loadTags();
    }
  }, [canViewTags, loadTags]);

  const rejectUnauthorizedAction = () => {
    message.error(t('taxonomy.common.permissionDenied'));
  };

  const rejectNonAuthoritativeAction = () => {
    message.warning(t('taxonomy.common.writeRequiresFreshData'));
  };

  const handleApplyFilters = () => {
    setQuery((current) => ({
      ...current,
      pageIndex: 1,
      keyword: keywordDraft.trim(),
      isEnabled: statusDraft,
      isFixed: typeDraft,
      includeDeleted: includeDeletedDraft,
    }));
    setFilterSheetOpen(false);
  };

  const handleResetFilters = () => {
    setKeywordDraft(DEFAULT_QUERY.keyword);
    setStatusDraft(DEFAULT_QUERY.isEnabled);
    setTypeDraft(DEFAULT_QUERY.isFixed);
    setIncludeDeletedDraft(DEFAULT_QUERY.includeDeleted);
    setQuery((current) => ({ ...DEFAULT_QUERY, pageSize: current.pageSize }));
    setFilterSheetOpen(false);
  };

  const handlePageChange = (pageIndex: number, pageSize = query.pageSize) => {
    setQuery((current) => ({ ...current, pageIndex, pageSize }));
  };

  const handleCreate = () => {
    if (!canCreateTag) {
      rejectUnauthorizedAction();
      return;
    }
    if (!actionsAreAuthoritative) {
      rejectNonAuthoritativeAction();
      return;
    }

    setFormMode('create');
    setEditingTag(undefined);
    setFormVisible(true);
  };

  const handleEdit = (record: TagVo) => {
    if (!canEditTag) {
      rejectUnauthorizedAction();
      return;
    }
    if (!actionsAreAuthoritative) {
      rejectNonAuthoritativeAction();
      return;
    }

    setFormMode('edit');
    setEditingTag(record);
    setFormVisible(true);
  };

  const handleDelete = async (id: string) => {
    if (!canDeleteTagPermission) {
      rejectUnauthorizedAction();
      return;
    }
    if (!actionsAreAuthoritative) {
      rejectNonAuthoritativeAction();
      return;
    }

    try {
      await deleteTag(id);
      message.success(t('tags.feedback.deleted'));
      await loadTags();
    } catch (error) {
      log.error('TagList', '删除标签失败:', error);
      message.error(t('tags.feedback.deleteFailed'));
    }
  };

  const handleRestore = async (id: string) => {
    if (!canRestoreTag) {
      rejectUnauthorizedAction();
      return;
    }
    if (!actionsAreAuthoritative) {
      rejectNonAuthoritativeAction();
      return;
    }

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
    if (!canToggleTag) {
      rejectUnauthorizedAction();
      return;
    }
    if (!actionsAreAuthoritative) {
      rejectNonAuthoritativeAction();
      return;
    }

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
    if (!canSortTag) {
      rejectUnauthorizedAction();
      return;
    }
    if (!actionsAreAuthoritative) {
      rejectNonAuthoritativeAction();
      return;
    }

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

  const renderActions = (record: TagVo) => (
    <Space size="small" wrap>
      {record.voIsDeleted && canRestoreTag && actionsAreAuthoritative ? (
        <Button variant="ghost" size="small" onClick={() => void handleRestore(record.voId)}>
          {t('taxonomy.common.restore')}
        </Button>
      ) : null}

      {!record.voIsDeleted ? (
        <>
          {canEditTag && actionsAreAuthoritative ? (
            <Button variant="ghost" size="small" icon={<EditOutlined />} onClick={() => handleEdit(record)}>
              {t('taxonomy.common.edit')}
            </Button>
          ) : null}

          {canToggleTag && actionsAreAuthoritative ? (record.voIsEnabled ? (
            <Button variant="ghost" size="small" icon={<CloseOutlined />} onClick={() => void handleToggleStatus(record, false)}>
              {t('taxonomy.common.disabled')}
            </Button>
          ) : (
            <Button variant="ghost" size="small" icon={<CheckOutlined />} onClick={() => void handleToggleStatus(record, true)}>
              {t('taxonomy.common.enabled')}
            </Button>
          )) : null}

          {canDeleteTagPermission && actionsAreAuthoritative ? (
            <Popconfirm
              title={t('tags.delete.title')}
              description={t('tags.delete.description')}
              onConfirm={() => handleDelete(record.voId)}
              okText={t('taxonomy.common.confirm')}
              cancelText={t('taxonomy.common.cancel')}
            >
              <Button variant="danger" size="small" icon={<DeleteOutlined />}>
                {t('taxonomy.common.delete')}
              </Button>
            </Popconfirm>
          ) : null}
        </>
      ) : null}
    </Space>
  );

  const columns: TableColumnsType<TagVo> = [
    { title: 'ID', dataIndex: 'voId', key: 'voId', width: 100 },
    { title: t('tags.table.name'), dataIndex: 'voName', key: 'voName', width: 200 },
    { title: t('taxonomy.common.slug'), dataIndex: 'voSlug', key: 'voSlug', width: 220 },
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
      render: (_, record) => record.voIsDeleted
        ? <Tag color="default">{t('taxonomy.common.deleted')}</Tag>
        : (
            <Tag color={record.voIsEnabled ? 'success' : 'error'}>
              {t(record.voIsEnabled ? 'taxonomy.common.enabled' : 'taxonomy.common.disabled')}
            </Tag>
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
          key={`${record.voId}-${record.voSortOrder}`}
          disabled={!canSortTag || !actionsAreAuthoritative}
          defaultValue={String(record.voSortOrder)}
          onBlur={(event) => void handleSortChange(record, event.target.value)}
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
      render: (_, record) => renderActions(record),
    },
  ];

  const filterControls = (
    <div className="console-resource-filter-controls console-resource-filter-controls--wide">
      <Input
        allowClear
        placeholder={t('tags.filter.placeholder')}
        prefix={<SearchOutlined />}
        value={keywordDraft}
        onChange={(event) => setKeywordDraft(event.target.value)}
        onPressEnter={handleApplyFilters}
      />
      <Select
        value={typeDraft}
        onChange={(value) => setTypeDraft(value)}
        options={[
          { label: t('tags.type.all'), value: 'all' },
          { label: t('tags.type.fixed'), value: 'fixed' },
          { label: t('tags.type.normal'), value: 'normal' },
        ]}
      />
      <Select
        value={statusDraft}
        onChange={(value) => setStatusDraft(value)}
        options={[
          { label: t('taxonomy.common.allStatus'), value: 'all' },
          { label: t('taxonomy.common.enabled'), value: 'enabled' },
          { label: t('taxonomy.common.disabled'), value: 'disabled' },
        ]}
      />
      <Select
        value={includeDeletedDraft ? 'yes' : 'no'}
        onChange={(value) => setIncludeDeletedDraft(value === 'yes')}
        options={[
          { label: t('taxonomy.common.hideDeleted'), value: 'no' },
          { label: t('taxonomy.common.showDeleted'), value: 'yes' },
        ]}
      />
      <div className="console-resource-filter-controls__actions">
        <Button onClick={handleResetFilters}>{t('taxonomy.common.resetFilters')}</Button>
        <Button variant="primary" onClick={handleApplyFilters}>{t('taxonomy.common.applyFilters')}</Button>
      </div>
    </div>
  );

  const readNotice = readState === 'stale' || readState === 'unavailable' ? (
    <div className={`console-resource-list-notice console-resource-list-notice--${readState}`} role="alert">
      <div>
        <strong>{t(readState === 'stale' ? 'taxonomy.common.staleTitle' : 'taxonomy.common.unavailableTitle', { resource: t('tags.resourceName') })}</strong>
        <span>{t(readState === 'stale' ? 'taxonomy.common.staleDescription' : 'taxonomy.common.unavailableDescription')}</span>
      </div>
      <Button size="small" onClick={() => void loadTags()}>{t('taxonomy.common.retry')}</Button>
    </div>
  ) : null;

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
            <Button icon={<ReloadOutlined />} onClick={() => void loadTags()}>
              {t('taxonomy.common.refresh')}
            </Button>
            {canCreateTag ? (
              <Button variant="primary" icon={<PlusOutlined />} disabled={!actionsAreAuthoritative} onClick={handleCreate}>
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

      <ConsoleResourceList
        toolbar={(
          <ConsoleToolbar
            title={t('tags.filter.title')}
            description={t('tags.filter.description')}
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? t('taxonomy.common.filterCount', { count: activeFilterCount }) : t('taxonomy.common.notFiltered')}
              </ConsoleStatusChip>
            )}
          >
            {filterControls}
          </ConsoleToolbar>
        )}
        mobileToolbar={(
          <div className="console-resource-mobile-summary">
            <div className="console-resource-mobile-summary__copy">
              <strong>{t('tags.mobile.result', { count: total })}</strong>
              <span>{activeFilterCount > 0 ? t('taxonomy.common.filterSummary', { count: activeFilterCount }) : t('taxonomy.common.notFiltered')}</span>
            </div>
            <div className="console-resource-mobile-summary__actions">
              <Button size="small" icon={<SearchOutlined />} onClick={() => setFilterSheetOpen(true)}>
                {t('taxonomy.common.filterAction')}
              </Button>
              <Button size="small" icon={<ReloadOutlined />} onClick={() => void loadTags()}>
                {t('taxonomy.common.refresh')}
              </Button>
            </div>
          </div>
        )}
        desktopList={(
          <section className="admin-table-panel">
            {readNotice}
            <Table<TagVo>
              rowKey="voId"
              columns={columns}
              dataSource={tags}
              loading={loading}
              pagination={{
                current: query.pageIndex,
                pageSize: query.pageSize,
                total,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (count) => t('taxonomy.common.total', { count }),
                onChange: handlePageChange,
              }}
              scroll={{ x: 1450 }}
            />
          </section>
        )}
        mobileList={(
          <>
            {readNotice}
            {loading && tags.length === 0 ? <div className="console-resource-mobile-loading">{t('taxonomy.common.loading')}</div> : null}
            {readState === 'ready' && tags.length === 0 ? (
              <div className="console-resource-mobile-empty">
                <strong>{t('tags.mobile.emptyTitle')}</strong>
                <span>{t('tags.mobile.emptyDescription')}</span>
              </div>
            ) : null}
            {tags.map((record) => (
              <article className="console-resource-mobile-card" key={record.voId}>
                <div className="console-resource-mobile-card__header">
                  <div className="console-resource-mobile-card__identity">
                    <strong>{record.voName}</strong>
                    <span>#{record.voId} · {record.voSlug || '-'}</span>
                  </div>
                  {record.voIsDeleted
                    ? <Tag color="default">{t('taxonomy.common.deleted')}</Tag>
                    : <Tag color={record.voIsEnabled ? 'success' : 'error'}>{t(record.voIsEnabled ? 'taxonomy.common.enabled' : 'taxonomy.common.disabled')}</Tag>}
                </div>
                <div className="console-resource-mobile-card__facts">
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('tags.table.type')}</span>
                    <strong>{t(record.voIsFixed ? 'tags.type.fixed' : 'tags.type.normal')}</strong>
                  </div>
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('taxonomy.common.posts')}</span>
                    <strong>{formatConsoleNumber(record.voPostCount, language)}</strong>
                  </div>
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('taxonomy.common.sort')}</span>
                    <strong>{record.voSortOrder}</strong>
                  </div>
                </div>
                {record.voDescription ? <p className="console-resource-mobile-card__description">{record.voDescription}</p> : null}
                <div className="console-resource-mobile-card__footer">
                  {canSortTag && actionsAreAuthoritative && !record.voIsDeleted ? (
                    <Input
                      key={`${record.voId}-${record.voSortOrder}`}
                      className="console-resource-mobile-sort-input"
                      defaultValue={String(record.voSortOrder)}
                      prefix={t('taxonomy.common.sort')}
                      onBlur={(event) => void handleSortChange(record, event.target.value)}
                    />
                  ) : <span />}
                  {renderActions(record)}
                </div>
              </article>
            ))}
            {tags.length > 0 ? (
              <div className="console-resource-mobile-pagination">
                <Button size="small" disabled={query.pageIndex <= 1 || loading} onClick={() => handlePageChange(query.pageIndex - 1)}>
                  {t('taxonomy.common.previousPage')}
                </Button>
                <span>{t('taxonomy.common.currentPage', { current: query.pageIndex, total: pageCount })}</span>
                <Button size="small" disabled={query.pageIndex >= pageCount || loading} onClick={() => handlePageChange(query.pageIndex + 1)}>
                  {t('taxonomy.common.nextPage')}
                </Button>
              </div>
            ) : null}
          </>
        )}
        context={(
          <>
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
                <span className="admin-table-summary__value">{t(query.includeDeleted ? 'taxonomy.common.recordsShown' : 'taxonomy.common.recordsHidden')}</span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('tags.summary.pageSize')}</span>
                <span className="admin-table-summary__value">{t('taxonomy.common.pageSize', { count: query.pageSize })}</span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('tags.summary.sort')}</span>
                <span className="admin-table-summary__value">{t(canSortTag ? 'taxonomy.common.sortWritable' : 'taxonomy.common.sortReadOnly')}</span>
              </div>
            </div>
          </>
        )}
      />

      <BottomSheet
        isOpen={filterSheetOpen}
        onClose={() => setFilterSheetOpen(false)}
        closeLabel={t('taxonomy.common.closeFilters')}
        title={t('tags.filter.title')}
        height="auto"
        className="console-resource-filter-sheet"
      >
        {filterControls}
      </BottomSheet>

      <TagForm
        visible={formVisible}
        mode={formMode}
        tag={editingTag}
        canSubmit={actionsAreAuthoritative && (formMode === 'create' ? canCreateTag : canEditTag)}
        onCancel={() => setFormVisible(false)}
        onSuccess={() => {
          setFormVisible(false);
          void loadTags();
        }}
      />
    </div>
  );
};
