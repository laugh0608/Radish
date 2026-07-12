import { useCallback, useEffect, useState } from 'react';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
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
import { CategoryForm } from './CategoryForm';
import '../adminFeature.css';
import './CategoryList.css';

const renderLevelText = (level: number) => {
  if (level <= 0) {
    return '顶级';
  }

  return `L${level}`;
};

export const CategoryList = () => {
  useDocumentTitle('分类管理');

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
      message.error('加载分类失败');
    } finally {
      setLoading(false);
    }
  }, [includeDeleted, isEnabled, keyword, pageIndex, pageSize]);

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
      message.success('删除分类成功');
      await loadCategories();
    } catch (error) {
      log.error('CategoryList', '删除分类失败:', error);
      message.error('删除分类失败');
    }
  };

  const handleRestore = async (id: number) => {
    try {
      await restoreCategory(id);
      message.success('恢复分类成功');
      await loadCategories();
    } catch (error) {
      log.error('CategoryList', '恢复分类失败:', error);
      message.error('恢复分类失败');
    }
  };

  const handleToggleStatus = async (record: CategoryVo, enabled: boolean) => {
    try {
      await toggleCategoryStatus(record.voId, enabled);
      message.success(enabled ? '启用成功' : '禁用成功');
      await loadCategories();
    } catch (error) {
      log.error('CategoryList', '更新分类状态失败:', error);
      message.error('更新分类状态失败');
    }
  };

  const handleSortChange = async (record: CategoryVo, value: string) => {
    const parsed = Number(value);
    if (!Number.isFinite(parsed) || parsed < 0) {
      message.error('排序值必须是大于等于0的数字');
      return;
    }

    if (parsed === record.voOrderSort) {
      return;
    }

    try {
      await updateCategorySort(record.voId, parsed);
      message.success('排序更新成功');
      await loadCategories();
    } catch (error) {
      log.error('CategoryList', '更新分类排序失败:', error);
      message.error('更新分类排序失败');
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
      title: '分类名称',
      dataIndex: 'voName',
      key: 'voName',
      width: 220,
    },
    {
      title: 'Slug',
      dataIndex: 'voSlug',
      key: 'voSlug',
      width: 220,
    },
    {
      title: '层级',
      key: 'voLevel',
      width: 100,
      render: (_, record) => <Tag>{renderLevelText(record.voLevel)}</Tag>,
    },
    {
      title: '父级 ID',
      key: 'voParentId',
      width: 120,
      render: (_, record) => record.voParentId ?? '-',
    },
    {
      title: '状态',
      key: 'voIsEnabled',
      width: 100,
      render: (_, record) => (
        record.voIsDeleted
          ? <Tag color="default">已删除</Tag>
          : (
              <Tag color={record.voIsEnabled ? 'success' : 'error'}>
                {record.voIsEnabled ? '启用' : '禁用'}
              </Tag>
            )
      ),
    },
    {
      title: '帖子数',
      dataIndex: 'voPostCount',
      key: 'voPostCount',
      width: 100,
    },
    {
      title: '排序',
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
      title: '描述',
      dataIndex: 'voDescription',
      key: 'voDescription',
      ellipsis: true,
    },
    {
      title: '操作',
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
              恢复
            </Button>
          )}

          {!record.voIsDeleted && (
            <>
              {canEditCategory ? (
                <Button variant="ghost" size="small" icon={<EditOutlined />} onClick={() => handleEdit(record)}>
                  编辑
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
                  禁用
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
                  启用
                </Button>
              )) : null}

              {canDeleteCategoryPermission ? (
                <Popconfirm
                  title="确认删除"
                  description="确定要删除这个分类吗？"
                  onConfirm={() => {
                    void handleDelete(record.voId);
                  }}
                  okText="确认"
                  cancelText="取消"
                >
                  <Button variant="danger" size="small" icon={<DeleteOutlined />}>
                    删除
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
        eyebrow="内容分类"
        title="分类管理"
        description="维护社区内容分类、层级关系和排序权重。"
        icon={<AppstoreOutlined />}
        status={(
          <ConsoleStatusChip tone={canCreateCategory ? 'success' : 'neutral'}>
            {canCreateCategory ? '可新增' : '只读'}
          </ConsoleStatusChip>
        )}
        actions={(
          <>
            <Button icon={<ReloadOutlined />} onClick={() => {
              void loadCategories();
            }}>
              刷新
            </Button>
            {canCreateCategory ? (
              <Button variant="primary" icon={<PlusOutlined />} onClick={handleCreate}>
                新增分类
              </Button>
            ) : null}
          </>
        )}
      />

      <ConsoleMetricGrid label="分类列表指标">
        <ConsoleMetricCard label="当前结果" value={total} description="当前筛选后的分类数量" tone="info" />
        <ConsoleMetricCard label="本页分类" value={categories.length} description="当前页可见分类" />
        <ConsoleMetricCard label="本页启用" value={enabledCategories} description="当前页启用分类" tone="success" />
        <ConsoleMetricCard label="顶级分类" value={rootCategories} description="当前页顶级分类" tone="warning" />
      </ConsoleMetricGrid>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title="筛选分类"
            description="按分类名称、描述、Slug、启用状态和软删除范围定位分类。"
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? `${activeFilterCount} 个条件` : '未筛选'}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Input
                className="category-list-filter-input"
                allowClear
                placeholder="搜索分类名称/描述/Slug"
                prefix={<SearchOutlined />}
                value={keyword}
                onChange={(event) => setKeyword(event.target.value)}
              />

              <Select
                className="category-list-filter-select"
                value={isEnabled}
                onChange={(value) => setIsEnabled(value)}
                options={[
                  { label: '全部状态', value: 'all' },
                  { label: '启用', value: 'enabled' },
                  { label: '禁用', value: 'disabled' },
                ]}
              />

              <Select
                className="category-list-filter-select category-list-filter-select--deleted"
                value={includeDeleted ? 'yes' : 'no'}
                onChange={(value) => setIncludeDeleted(value === 'yes')}
                options={[
                  { label: '隐藏已删除', value: 'no' },
                  { label: '显示已删除', value: 'yes' },
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
                showTotal: (count) => `共 ${count} 条`,
                onChange: (current, size) => {
                  void loadCategories(current, size);
                },
              }}
              scroll={{ x: 1500 }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>列表摘要</h3>
          <p className="admin-feature-subtle">用于核对当前分类范围、层级入口和排序权限。</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">查询范围</span>
              <span className="admin-table-summary__value">
                {activeFilterCount > 0 ? `${activeFilterCount} 个筛选条件` : '全部分类'}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">软删除记录</span>
              <span className="admin-table-summary__value">{includeDeleted ? '已显示' : '已隐藏'}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">分页规模</span>
              <span className="admin-table-summary__value">{pageSize} 条 / 页</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">排序权限</span>
              <span className="admin-table-summary__value">
                {canSortCategory ? '可调整排序' : '仅可查看排序'}
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
