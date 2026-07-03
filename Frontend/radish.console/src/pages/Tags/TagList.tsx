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
import '../adminFeature.css';
import './TagList.css';

export const TagList = () => {
  useDocumentTitle('标签管理');

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

  const loadTags = async (targetPageIndex = pageIndex, targetPageSize = pageSize) => {
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
      message.error('加载标签失败');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (!canViewTags) {
      return;
    }

    void loadTags(1, pageSize);
  }, [keyword, isEnabled, isFixed, includeDeleted, canViewTags, pageSize]);

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
      message.success('删除标签成功');
      await loadTags();
    } catch (error) {
      log.error('TagList', '删除标签失败:', error);
      message.error('删除标签失败');
    }
  };

  const handleRestore = async (id: number) => {
    try {
      await restoreTag(id);
      message.success('恢复标签成功');
      await loadTags();
    } catch (error) {
      log.error('TagList', '恢复标签失败:', error);
      message.error('恢复标签失败');
    }
  };

  const handleToggleStatus = async (record: TagVo, enabled: boolean) => {
    try {
      await toggleTagStatus(record.voId, enabled);
      message.success(enabled ? '启用成功' : '禁用成功');
      await loadTags();
    } catch (error) {
      log.error('TagList', '更新标签状态失败:', error);
      message.error('更新标签状态失败');
    }
  };

  const handleSortChange = async (record: TagVo, value: string) => {
    const parsed = Number(value);
    if (!Number.isFinite(parsed) || parsed < 0) {
      message.error('排序值必须是大于等于0的数字');
      return;
    }

    if (parsed === record.voSortOrder) {
      return;
    }

    try {
      await updateTagSort(record.voId, parsed);
      message.success('排序更新成功');
      await loadTags();
    } catch (error) {
      log.error('TagList', '更新排序失败:', error);
      message.error('更新排序失败');
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
      title: '标签名称',
      dataIndex: 'voName',
      key: 'voName',
      width: 200,
    },
    {
      title: 'Slug',
      dataIndex: 'voSlug',
      key: 'voSlug',
      width: 220,
    },
    {
      title: '标签类型',
      key: 'voIsFixed',
      width: 120,
      render: (_, record) => (
        <Tag color={record.voIsFixed ? 'blue' : 'default'}>
          {record.voIsFixed ? '固定标签' : '普通标签'}
        </Tag>
      ),
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
          {record.voIsDeleted && canRestoreTag && (
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
              {canEditTag ? (
                <Button variant="ghost" size="small" icon={<EditOutlined />} onClick={() => handleEdit(record)}>
                  编辑
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

              {canDeleteTagPermission ? (
                <Popconfirm
                  title="确认删除"
                  description="确定要删除这个标签吗？"
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
    <div className="admin-feature-page tag-list-page">
      <ConsolePageHeader
        eyebrow="CONTENT TAXONOMY"
        title="标签管理"
        description="维护社区内容标签、固定标签和排序权重。"
        icon={<TagsOutlined />}
        status={(
          <ConsoleStatusChip tone={canCreateTag ? 'success' : 'neutral'}>
            {canCreateTag ? '可新增' : '只读'}
          </ConsoleStatusChip>
        )}
        actions={(
          <>
            <Button icon={<ReloadOutlined />} onClick={() => {
              void loadTags();
            }}>
              刷新
            </Button>
            {canCreateTag ? (
              <Button variant="primary" icon={<PlusOutlined />} onClick={handleCreate}>
                新增标签
              </Button>
            ) : null}
          </>
        )}
      />

      <ConsoleMetricGrid label="标签列表指标">
        <ConsoleMetricCard label="当前结果" value={total} description="当前筛选后的标签数量" tone="info" />
        <ConsoleMetricCard label="本页标签" value={tags.length} description="当前页可见标签" />
        <ConsoleMetricCard label="本页启用" value={enabledTags} description="当前页启用标签" tone="success" />
        <ConsoleMetricCard label="固定标签" value={fixedTags} description="固定标签数量" tone="warning" />
      </ConsoleMetricGrid>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title="筛选标签"
            description="按标签名称、描述、类型、启用状态和软删除范围定位标签。"
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? `${activeFilterCount} 个条件` : '未筛选'}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Input
                className="tag-list-filter-input"
                allowClear
                placeholder="搜索标签名称/描述"
                prefix={<SearchOutlined />}
                value={keyword}
                onChange={(event) => setKeyword(event.target.value)}
              />

              <Select
                className="tag-list-filter-select"
                value={isFixed}
                onChange={(value) => setIsFixed(value)}
                options={[
                  { label: '全部类型', value: 'all' },
                  { label: '固定标签', value: 'fixed' },
                  { label: '普通标签', value: 'normal' },
                ]}
              />

              <Select
                className="tag-list-filter-select"
                value={isEnabled}
                onChange={(value) => setIsEnabled(value)}
                options={[
                  { label: '全部状态', value: 'all' },
                  { label: '启用', value: 'enabled' },
                  { label: '禁用', value: 'disabled' },
                ]}
              />

              <Select
                className="tag-list-filter-select tag-list-filter-select--deleted"
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
                showTotal: (count) => `共 ${count} 条`,
                onChange: (current, size) => {
                  void loadTags(current, size);
                },
              }}
              scroll={{ x: 1450 }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>列表摘要</h3>
          <p className="admin-feature-subtle">用于核对当前标签范围、排序权限和软删除可见性。</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">查询范围</span>
              <span className="admin-table-summary__value">
                {activeFilterCount > 0 ? `${activeFilterCount} 个筛选条件` : '全部标签'}
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
                {canSortTag ? '可调整排序' : '仅可查看排序'}
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
