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
  CheckOutlined,
  CloseOutlined,
  DeleteOutlined,
  EditOutlined,
  PlusOutlined,
  ReloadOutlined,
  SearchOutlined,
  AppstoreOutlined,
} from '@radish/ui';
import {
  deleteCategory,
  getCategoryPage,
  restoreCategory,
  toggleCategoryStatus,
  updateCategorySort,
  type CategoryVo,
} from '@/api/categoryApi';
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
import { log } from '@/utils/logger';
import { formatConsoleNumber } from '@/utils/localeFormatters';
import { CategoryForm } from './CategoryForm';
import '../adminFeature.css';
import './CategoryList.css';

type CategoryStatusFilter = 'all' | 'enabled' | 'disabled';
type ResourceReadState = 'loading' | 'ready' | 'unavailable' | 'stale';

interface CategoryListQuery {
  pageIndex: number;
  pageSize: number;
  keyword: string;
  isEnabled: CategoryStatusFilter;
  includeDeleted: boolean;
}

const DEFAULT_QUERY: CategoryListQuery = {
  pageIndex: 1,
  pageSize: 20,
  keyword: '',
  isEnabled: 'all',
  includeDeleted: false,
};

const renderLevelText = (level: number, rootLabel: string) => {
  if (level <= 0) {
    return rootLabel;
  }

  return `L${level}`;
};

export const CategoryList = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('categories.documentTitle'));

  const [loading, setLoading] = useState(false);
  const [readState, setReadState] = useState<ResourceReadState>('loading');
  const [categories, setCategories] = useState<CategoryVo[]>([]);
  const [total, setTotal] = useState(0);
  const [query, setQuery] = useState<CategoryListQuery>(DEFAULT_QUERY);
  const [keywordDraft, setKeywordDraft] = useState(DEFAULT_QUERY.keyword);
  const [statusDraft, setStatusDraft] = useState<CategoryStatusFilter>(DEFAULT_QUERY.isEnabled);
  const [includeDeletedDraft, setIncludeDeletedDraft] = useState(DEFAULT_QUERY.includeDeleted);
  const [filterSheetOpen, setFilterSheetOpen] = useState(false);
  const [formVisible, setFormVisible] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [editingCategory, setEditingCategory] = useState<CategoryVo | undefined>(undefined);
  const requestSequence = useRef(0);
  const snapshotQueryKey = useRef<string | undefined>(undefined);

  const canViewCategories = usePermission(CONSOLE_PERMISSIONS.categoriesView);
  const canCreateCategory = usePermission(CONSOLE_PERMISSIONS.categoriesCreate);
  const canEditCategory = usePermission(CONSOLE_PERMISSIONS.categoriesEdit);
  const canDeleteCategoryPermission = usePermission(CONSOLE_PERMISSIONS.categoriesDelete);
  const canRestoreCategory = usePermission(CONSOLE_PERMISSIONS.categoriesRestore);
  const canToggleCategory = usePermission(CONSOLE_PERMISSIONS.categoriesToggle);
  const canSortCategory = usePermission(CONSOLE_PERMISSIONS.categoriesSort);

  const activeFilterCount = [
    query.keyword ? 'keyword' : undefined,
    query.isEnabled !== 'all' ? 'status' : undefined,
    query.includeDeleted ? 'deleted' : undefined,
  ].filter(Boolean).length;
  const enabledCategories = categories.filter((category) => category.voIsEnabled && !category.voIsDeleted).length;
  const rootCategories = categories.filter((category) => category.voLevel <= 0).length;
  const pageCount = Math.max(1, Math.ceil(total / query.pageSize));
  const actionsAreAuthoritative = readState === 'ready';

  const loadCategories = useCallback(async () => {
    const requestId = requestSequence.current + 1;
    const queryKey = JSON.stringify(query);
    const hasCurrentSnapshot = snapshotQueryKey.current === queryKey;
    requestSequence.current = requestId;

    try {
      setLoading(true);
      setReadState('loading');
      if (!hasCurrentSnapshot) {
        setCategories([]);
        setTotal(0);
      }
      const response = await getCategoryPage({
        pageIndex: query.pageIndex,
        pageSize: query.pageSize,
        keyword: query.keyword || undefined,
        isEnabled: query.isEnabled === 'all' ? undefined : query.isEnabled === 'enabled',
        includeDeleted: query.includeDeleted,
      });

      if (requestSequence.current !== requestId) {
        return;
      }

      if (response.data.length === 0 && response.dataCount > 0 && query.pageIndex > response.pageCount) {
        setQuery((current) => ({ ...current, pageIndex: Math.max(1, response.pageCount) }));
        return;
      }

      setCategories(response.data);
      setTotal(response.dataCount);
      snapshotQueryKey.current = queryKey;
      setReadState('ready');
    } catch (error) {
      if (requestSequence.current !== requestId) {
        return;
      }

      log.error('CategoryList', '加载分类失败:', error);
      setReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
      message.error(t('categories.feedback.loadFailed'));
    } finally {
      if (requestSequence.current === requestId) {
        setLoading(false);
      }
    }
  }, [query, t]);

  useEffect(() => {
    if (canViewCategories) {
      void loadCategories();
    }
  }, [canViewCategories, loadCategories]);

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
      includeDeleted: includeDeletedDraft,
    }));
    setFilterSheetOpen(false);
  };

  const handleResetFilters = () => {
    setKeywordDraft(DEFAULT_QUERY.keyword);
    setStatusDraft(DEFAULT_QUERY.isEnabled);
    setIncludeDeletedDraft(DEFAULT_QUERY.includeDeleted);
    setQuery((current) => ({ ...DEFAULT_QUERY, pageSize: current.pageSize }));
    setFilterSheetOpen(false);
  };

  const handlePageChange = (pageIndex: number, pageSize = query.pageSize) => {
    setQuery((current) => ({ ...current, pageIndex, pageSize }));
  };

  const handleCreate = () => {
    if (!canCreateCategory) {
      rejectUnauthorizedAction();
      return;
    }
    if (!actionsAreAuthoritative) {
      rejectNonAuthoritativeAction();
      return;
    }

    setFormMode('create');
    setEditingCategory(undefined);
    setFormVisible(true);
  };

  const handleEdit = (record: CategoryVo) => {
    if (!canEditCategory) {
      rejectUnauthorizedAction();
      return;
    }
    if (!actionsAreAuthoritative) {
      rejectNonAuthoritativeAction();
      return;
    }

    setFormMode('edit');
    setEditingCategory(record);
    setFormVisible(true);
  };

  const handleDelete = async (id: string) => {
    if (!canDeleteCategoryPermission) {
      rejectUnauthorizedAction();
      return;
    }
    if (!actionsAreAuthoritative) {
      rejectNonAuthoritativeAction();
      return;
    }

    try {
      await deleteCategory(id);
      message.success(t('categories.feedback.deleted'));
      await loadCategories();
    } catch (error) {
      log.error('CategoryList', '删除分类失败:', error);
      message.error(t('categories.feedback.deleteFailed'));
    }
  };

  const handleRestore = async (id: string) => {
    if (!canRestoreCategory) {
      rejectUnauthorizedAction();
      return;
    }
    if (!actionsAreAuthoritative) {
      rejectNonAuthoritativeAction();
      return;
    }

    try {
      await restoreCategory(id);
      message.success(t('categories.feedback.restored'));
      await loadCategories();
    } catch (error) {
      log.error('CategoryList', '恢复分类失败:', error);
      message.error(t('categories.feedback.restoreFailed'));
    }
  };

  const handleToggleStatus = async (record: CategoryVo, enabled: boolean) => {
    if (!canToggleCategory) {
      rejectUnauthorizedAction();
      return;
    }
    if (!actionsAreAuthoritative) {
      rejectNonAuthoritativeAction();
      return;
    }

    try {
      await toggleCategoryStatus(record.voId, enabled);
      message.success(t(enabled ? 'categories.feedback.enabled' : 'categories.feedback.disabled'));
      await loadCategories();
    } catch (error) {
      log.error('CategoryList', '更新分类状态失败:', error);
      message.error(t('categories.feedback.toggleFailed'));
    }
  };

  const handleSortChange = async (record: CategoryVo, value: string) => {
    if (!canSortCategory) {
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

    if (parsed === record.voOrderSort) {
      return;
    }

    try {
      await updateCategorySort(record.voId, parsed);
      message.success(t('taxonomy.common.sortUpdated'));
      await loadCategories();
    } catch (error) {
      log.error('CategoryList', '更新分类排序失败:', error);
      message.error(t('categories.feedback.sortFailed'));
    }
  };

  const renderActions = (record: CategoryVo) => (
    <Space size="small" wrap>
      {record.voIsDeleted && canRestoreCategory && actionsAreAuthoritative ? (
        <Button
          variant="ghost"
          size="small"
          onClick={() => {
            void handleRestore(record.voId);
          }}
        >
          {t('taxonomy.common.restore')}
        </Button>
      ) : null}

      {!record.voIsDeleted ? (
        <>
          {canEditCategory && actionsAreAuthoritative ? (
            <Button variant="ghost" size="small" icon={<EditOutlined />} onClick={() => handleEdit(record)}>
              {t('taxonomy.common.edit')}
            </Button>
          ) : null}

          {canToggleCategory && actionsAreAuthoritative ? (record.voIsEnabled ? (
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

          {canDeleteCategoryPermission && actionsAreAuthoritative ? (
            <Popconfirm
              title={t('categories.delete.title')}
              description={t('categories.delete.description')}
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

  const columns: TableColumnsType<CategoryVo> = [
    { title: 'ID', dataIndex: 'voId', key: 'voId', width: 100 },
    { title: t('categories.table.name'), dataIndex: 'voName', key: 'voName', width: 220 },
    { title: t('taxonomy.common.slug'), dataIndex: 'voSlug', key: 'voSlug', width: 220 },
    {
      title: t('categories.table.level'),
      key: 'voLevel',
      width: 100,
      render: (_, record) => <Tag>{renderLevelText(record.voLevel, t('categories.level.root'))}</Tag>,
    },
    {
      title: t('categories.table.parentId'),
      key: 'voParentId',
      width: 120,
      render: (_, record) => record.voParentId ?? '-',
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
      key: 'voOrderSort',
      width: 140,
      render: (_, record) => (
        <Input
          key={`${record.voId}-${record.voOrderSort}`}
          disabled={!canSortCategory || !actionsAreAuthoritative}
          defaultValue={String(record.voOrderSort)}
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
      render: (_, record) => renderActions(record),
    },
  ];

  const filterControls = (
    <div className="console-resource-filter-controls">
      <Input
        allowClear
        placeholder={t('categories.filter.placeholder')}
        prefix={<SearchOutlined />}
        value={keywordDraft}
        onChange={(event) => setKeywordDraft(event.target.value)}
        onPressEnter={handleApplyFilters}
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
        <strong>{t(readState === 'stale' ? 'taxonomy.common.staleTitle' : 'taxonomy.common.unavailableTitle', { resource: t('categories.resourceName') })}</strong>
        <span>{t(readState === 'stale' ? 'taxonomy.common.staleDescription' : 'taxonomy.common.unavailableDescription')}</span>
      </div>
      <Button size="small" onClick={() => void loadCategories()}>{t('taxonomy.common.retry')}</Button>
    </div>
  ) : null;

  return (
    <div className="admin-feature-page category-list-page">
      <ConsolePageHeader
        eyebrow={t('categories.page.eyebrow')}
        title={t('categories.page.title')}
        description={t('categories.page.description')}
        icon={<AppstoreOutlined />}
        status={(
          <ConsoleStatusChip tone={canCreateCategory ? 'success' : 'neutral'}>
            {t(canCreateCategory ? 'taxonomy.common.createWritable' : 'taxonomy.common.readOnly')}
          </ConsoleStatusChip>
        )}
        actions={(
          <>
            <Button icon={<ReloadOutlined />} onClick={() => void loadCategories()}>
              {t('taxonomy.common.refresh')}
            </Button>
            {canCreateCategory ? (
              <Button variant="primary" icon={<PlusOutlined />} disabled={!actionsAreAuthoritative} onClick={handleCreate}>
                {t('categories.actions.create')}
              </Button>
            ) : null}
          </>
        )}
      />

      <ConsoleMetricGrid label={t('categories.metrics.ariaLabel')}>
        <ConsoleMetricCard label={t('categories.metrics.result')} value={formatConsoleNumber(total, language)} description={t('categories.metrics.resultDescription')} tone="info" />
        <ConsoleMetricCard label={t('categories.metrics.page')} value={formatConsoleNumber(categories.length, language)} description={t('categories.metrics.pageDescription')} />
        <ConsoleMetricCard label={t('categories.metrics.enabled')} value={formatConsoleNumber(enabledCategories, language)} description={t('categories.metrics.enabledDescription')} tone="success" />
        <ConsoleMetricCard label={t('categories.metrics.root')} value={formatConsoleNumber(rootCategories, language)} description={t('categories.metrics.rootDescription')} tone="warning" />
      </ConsoleMetricGrid>

      <ConsoleResourceList
        toolbar={(
          <ConsoleToolbar
            title={t('categories.filter.title')}
            description={t('categories.filter.description')}
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
              <strong>{t('categories.mobile.result', { count: total })}</strong>
              <span>{activeFilterCount > 0 ? t('taxonomy.common.filterSummary', { count: activeFilterCount }) : t('taxonomy.common.notFiltered')}</span>
            </div>
            <div className="console-resource-mobile-summary__actions">
              <Button size="small" icon={<SearchOutlined />} onClick={() => setFilterSheetOpen(true)}>
                {t('taxonomy.common.filterAction')}
              </Button>
              <Button size="small" icon={<ReloadOutlined />} onClick={() => void loadCategories()}>
                {t('taxonomy.common.refresh')}
              </Button>
            </div>
          </div>
        )}
        desktopList={(
          <section className="admin-table-panel">
            {readNotice}
            <Table<CategoryVo>
              rowKey="voId"
              columns={columns}
              dataSource={categories}
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
              scroll={{ x: 1500 }}
            />
          </section>
        )}
        mobileList={(
          <>
            {readNotice}
            {loading && categories.length === 0 ? (
              <div className="console-resource-mobile-loading">{t('taxonomy.common.loading')}</div>
            ) : null}
            {readState === 'ready' && categories.length === 0 ? (
              <div className="console-resource-mobile-empty">
                <strong>{t('categories.mobile.emptyTitle')}</strong>
                <span>{t('categories.mobile.emptyDescription')}</span>
              </div>
            ) : null}
            {categories.map((record) => (
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
                    <span>{t('categories.table.level')}</span>
                    <strong>{renderLevelText(record.voLevel, t('categories.level.root'))}</strong>
                  </div>
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('categories.table.parentId')}</span>
                    <strong>{record.voParentId ?? '-'}</strong>
                  </div>
                  <div className="console-resource-mobile-card__fact">
                    <span>{t('taxonomy.common.posts')}</span>
                    <strong>{formatConsoleNumber(record.voPostCount, language)}</strong>
                  </div>
                </div>
                {record.voDescription ? <p className="console-resource-mobile-card__description">{record.voDescription}</p> : null}
                <div className="console-resource-mobile-card__footer">
                  {canSortCategory && actionsAreAuthoritative && !record.voIsDeleted ? (
                    <Input
                      key={`${record.voId}-${record.voOrderSort}`}
                      className="console-resource-mobile-sort-input"
                      defaultValue={String(record.voOrderSort)}
                      prefix={t('taxonomy.common.sort')}
                      onBlur={(event) => void handleSortChange(record, event.target.value)}
                    />
                  ) : <span />}
                  {renderActions(record)}
                </div>
              </article>
            ))}
            {categories.length > 0 ? (
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
            <h3>{t('categories.summary.title')}</h3>
            <p className="admin-feature-subtle">{t('categories.summary.description')}</p>
            <div className="admin-table-summary">
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('categories.summary.scope')}</span>
                <span className="admin-table-summary__value">
                  {activeFilterCount > 0 ? t('taxonomy.common.filterSummary', { count: activeFilterCount }) : t('categories.summary.all')}
                </span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('categories.summary.deleted')}</span>
                <span className="admin-table-summary__value">{t(query.includeDeleted ? 'taxonomy.common.recordsShown' : 'taxonomy.common.recordsHidden')}</span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('categories.summary.pageSize')}</span>
                <span className="admin-table-summary__value">{t('taxonomy.common.pageSize', { count: query.pageSize })}</span>
              </div>
              <div className="admin-table-summary__item">
                <span className="admin-table-summary__label">{t('categories.summary.sort')}</span>
                <span className="admin-table-summary__value">{t(canSortCategory ? 'taxonomy.common.sortWritable' : 'taxonomy.common.sortReadOnly')}</span>
              </div>
            </div>
          </>
        )}
      />

      <BottomSheet
        isOpen={filterSheetOpen}
        onClose={() => setFilterSheetOpen(false)}
        closeLabel={t('taxonomy.common.closeFilters')}
        title={t('categories.filter.title')}
        height="auto"
        className="console-resource-filter-sheet"
      >
        {filterControls}
      </BottomSheet>

      <CategoryForm
        visible={formVisible}
        mode={formMode}
        category={editingCategory}
        canSubmit={actionsAreAuthoritative && (formMode === 'create' ? canCreateCategory : canEditCategory)}
        onCancel={() => setFormVisible(false)}
        onSuccess={() => {
          setFormVisible(false);
          void loadCategories();
        }}
      />
    </div>
  );
};
