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
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import { formatConsoleNumber } from '@/utils/localeFormatters';
import { CategoryForm } from './CategoryForm';
import '../adminFeature.css';
import './CategoryList.css';

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
  const [categories, setCategories] = useState<CategoryVo[]>([]);
  const [total, setTotal] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [keyword, setKeyword] = useState('');
  const [isEnabled, setIsEnabled] = useState<'all' | 'enabled' | 'disabled'>('all');
  const [includeDeleted, setIncludeDeleted] = useState(false);
  const [formVisible, setFormVisible] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [editingCategory, setEditingCategory] = useState<CategoryVo | undefined>(undefined);
  const activeFilterCount = [
    keyword.trim() ? 'keyword' : undefined,
    isEnabled !== 'all' ? 'status' : undefined,
    includeDeleted ? 'deleted' : undefined,
  ].filter(Boolean).length;
  const enabledCategories = categories.filter((category) => category.voIsEnabled && !category.voIsDeleted).length;
  const rootCategories = categories.filter((category) => category.voLevel <= 0).length;

  const canViewCategories = usePermission(CONSOLE_PERMISSIONS.categoriesView);
  const canCreateCategory = usePermission(CONSOLE_PERMISSIONS.categoriesCreate);
  const canEditCategory = usePermission(CONSOLE_PERMISSIONS.categoriesEdit);
  const canDeleteCategoryPermission = usePermission(CONSOLE_PERMISSIONS.categoriesDelete);
  const canRestoreCategory = usePermission(CONSOLE_PERMISSIONS.categoriesRestore);
  const canToggleCategory = usePermission(CONSOLE_PERMISSIONS.categoriesToggle);
  const canSortCategory = usePermission(CONSOLE_PERMISSIONS.categoriesSort);

  const loadCategories = useCallback(async (targetPageIndex = pageIndex, targetPageSize = pageSize) => {
    try {
      setLoading(true);
      const response = await getCategoryPage({
        pageIndex: targetPageIndex,
        pageSize: targetPageSize,
        keyword: keyword.trim() || undefined,
        isEnabled: isEnabled === 'all' ? undefined : isEnabled === 'enabled',
        includeDeleted,
      });

      setCategories(response.data);
      setTotal(response.dataCount);
      setPageIndex(response.page);
      setPageSize(response.pageSize);
    } catch (error) {
      log.error('CategoryList', '加载分类失败:', error);
      message.error(t('categories.feedback.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [includeDeleted, isEnabled, keyword, pageIndex, pageSize, t]);

  useEffect(() => {
    if (!canViewCategories) {
      return;
    }

    void loadCategories(1, pageSize);
  }, [canViewCategories, loadCategories, pageSize]);

  const handleCreate = () => {
    setFormMode('create');
    setEditingCategory(undefined);
    setFormVisible(true);
  };

  const handleEdit = (record: CategoryVo) => {
    setFormMode('edit');
    setEditingCategory(record);
    setFormVisible(true);
  };

  const handleDelete = async (id: number) => {
    try {
      await deleteCategory(id);
      message.success(t('categories.feedback.deleted'));
      await loadCategories();
    } catch (error) {
      log.error('CategoryList', '删除分类失败:', error);
      message.error(t('categories.feedback.deleteFailed'));
    }
  };

  const handleRestore = async (id: number) => {
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

  const columns: TableColumnsType<CategoryVo> = [
    {
      title: 'ID',
      dataIndex: 'voId',
      key: 'voId',
      width: 100,
    },
    {
      title: t('categories.table.name'),
      dataIndex: 'voName',
      key: 'voName',
      width: 220,
    },
    {
      title: t('taxonomy.common.slug'),
      dataIndex: 'voSlug',
      key: 'voSlug',
      width: 220,
    },
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
      key: 'voOrderSort',
      width: 140,
      render: (_, record) => (
        <Input
          disabled={!canSortCategory}
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
      render: (_, record) => (
        <Space size="small" wrap>
          {record.voIsDeleted && canRestoreCategory && (
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
              {canEditCategory ? (
                <Button variant="ghost" size="small" icon={<EditOutlined />} onClick={() => handleEdit(record)}>
                  {t('taxonomy.common.edit')}
                </Button>
              ) : null}

              {canToggleCategory ? (record.voIsEnabled ? (
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

              {canDeleteCategoryPermission ? (
                <Popconfirm
                  title={t('categories.delete.title')}
                  description={t('categories.delete.description')}
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
            <Button icon={<ReloadOutlined />} onClick={() => {
              void loadCategories();
            }}>
              {t('taxonomy.common.refresh')}
            </Button>
            {canCreateCategory ? (
              <Button variant="primary" icon={<PlusOutlined />} onClick={handleCreate}>
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

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title={t('categories.filter.title')}
            description={t('categories.filter.description')}
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? t('taxonomy.common.filterCount', { count: activeFilterCount }) : t('taxonomy.common.notFiltered')}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Input
                className="category-list-filter-input"
                allowClear
                placeholder={t('categories.filter.placeholder')}
                prefix={<SearchOutlined />}
                value={keyword}
                onChange={(event) => setKeyword(event.target.value)}
              />

              <Select
                className="category-list-filter-select"
                value={isEnabled}
                onChange={(value) => setIsEnabled(value)}
                options={[
                  { label: t('taxonomy.common.allStatus'), value: 'all' },
                  { label: t('taxonomy.common.enabled'), value: 'enabled' },
                  { label: t('taxonomy.common.disabled'), value: 'disabled' },
                ]}
              />

              <Select
                className="category-list-filter-select category-list-filter-select--deleted"
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
            <Table<CategoryVo>
              rowKey="voId"
              columns={columns}
              dataSource={categories}
              loading={loading}
              pagination={{
                current: pageIndex,
                pageSize,
                total,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (count) => t('taxonomy.common.total', { count }),
                onChange: (current, size) => {
                  void loadCategories(current, size);
                },
              }}
              scroll={{ x: 1500 }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
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
              <span className="admin-table-summary__value">{t(includeDeleted ? 'taxonomy.common.recordsShown' : 'taxonomy.common.recordsHidden')}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('categories.summary.pageSize')}</span>
              <span className="admin-table-summary__value">{t('taxonomy.common.pageSize', { count: pageSize })}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('categories.summary.sort')}</span>
              <span className="admin-table-summary__value">
                {t(canSortCategory ? 'taxonomy.common.sortWritable' : 'taxonomy.common.sortReadOnly')}
              </span>
            </div>
          </div>
        </aside>
      </div>

      <CategoryForm
        visible={formVisible}
        mode={formMode}
        category={editingCategory}
        onCancel={() => setFormVisible(false)}
        onSuccess={() => {
          setFormVisible(false);
          void loadCategories();
        }}
      />
    </div>
  );
};
