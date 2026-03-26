import { useEffect, useState } from 'react';
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
import { usePermission } from '@/hooks/usePermission';
import { log } from '@/utils/logger';
import { CategoryForm } from './CategoryForm';
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

  const canViewCategories = usePermission(CONSOLE_PERMISSIONS.categoriesView);
  const canCreateCategory = usePermission(CONSOLE_PERMISSIONS.categoriesCreate);
  const canEditCategory = usePermission(CONSOLE_PERMISSIONS.categoriesEdit);
  const canDeleteCategoryPermission = usePermission(CONSOLE_PERMISSIONS.categoriesDelete);
  const canRestoreCategory = usePermission(CONSOLE_PERMISSIONS.categoriesRestore);
  const canToggleCategory = usePermission(CONSOLE_PERMISSIONS.categoriesToggle);
  const canSortCategory = usePermission(CONSOLE_PERMISSIONS.categoriesSort);

  const loadCategories = async (targetPageIndex = pageIndex, targetPageSize = pageSize) => {
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
  };

  useEffect(() => {
    if (!canViewCategories) {
      return;
    }

    void loadCategories(1, pageSize);
  }, [canViewCategories, includeDeleted, isEnabled, keyword, pageSize]);

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
        <Space size="small">
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
    <div className="category-list-page">
      <div className="page-header">
        <h2>分类管理</h2>
        <Space>
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
        </Space>
      </div>

      <div className="filter-bar">
        <Space wrap>
          <Input
            allowClear
            placeholder="搜索分类名称/描述/Slug"
            prefix={<SearchOutlined />}
            style={{ width: 260 }}
            value={keyword}
            onChange={(event) => setKeyword(event.target.value)}
          />

          <Select
            value={isEnabled}
            style={{ width: 140 }}
            onChange={(value) => setIsEnabled(value)}
            options={[
              { label: '全部状态', value: 'all' },
              { label: '启用', value: 'enabled' },
              { label: '禁用', value: 'disabled' },
            ]}
          />

          <Select
            value={includeDeleted ? 'yes' : 'no'}
            style={{ width: 160 }}
            onChange={(value) => setIncludeDeleted(value === 'yes')}
            options={[
              { label: '隐藏已删除', value: 'no' },
              { label: '显示已删除', value: 'yes' },
            ]}
          />
        </Space>
      </div>

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
