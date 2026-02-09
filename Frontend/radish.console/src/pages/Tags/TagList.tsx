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
} from '@radish/ui';
import {
  getTagPage,
  deleteTag,
  restoreTag,
  toggleTagStatus,
  updateTagSort,
  type TagVo,
} from '@/api/tagApi';
import { TagForm } from './TagForm';
import { log } from '@/utils/logger';
import './TagList.css';

export const TagList = () => {
  useDocumentTitle('标签管理');

  const [loading, setLoading] = useState(false);
  const [tags, setTags] = useState<TagVo[]>([]);
  const [total, setTotal] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const [keyword, setKeyword] = useState('');
  const [isEnabled, setIsEnabled] = useState<'all' | 'enabled' | 'disabled'>('all');
  const [isFixed, setIsFixed] = useState<'all' | 'fixed' | 'normal'>('all');
  const [includeDeleted, setIncludeDeleted] = useState(false);

  const [formVisible, setFormVisible] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [editingTag, setEditingTag] = useState<TagVo | undefined>(undefined);

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
    void loadTags(1, pageSize);
  }, [keyword, isEnabled, isFixed, includeDeleted]);

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
        <Space size="small">
          {record.voIsDeleted && (
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
              <Button variant="ghost" size="small" icon={<EditOutlined />} onClick={() => handleEdit(record)}>
                编辑
              </Button>

              {record.voIsEnabled ? (
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
              )}

              <Popconfirm
                title="确认删除"
                description="确定要删除这个标签吗？"
                onConfirm={() => {
                  void handleDelete(record.voId);
                }}
                okText="确认"
                cancelText="取消"
              >
                <Button variant="ghost" size="small" danger icon={<DeleteOutlined />}>
                  删除
                </Button>
              </Popconfirm>
            </>
          )}
        </Space>
      ),
    },
  ];

  return (
    <div className="tag-list-page">
      <div className="page-header">
        <h2>标签管理</h2>
        <Space>
          <Button icon={<ReloadOutlined />} onClick={() => {
            void loadTags();
          }}>
            刷新
          </Button>
          <Button variant="primary" icon={<PlusOutlined />} onClick={handleCreate}>
            新增标签
          </Button>
        </Space>
      </div>

      <div className="filter-bar">
        <Space wrap>
          <Input
            allowClear
            placeholder="搜索标签名称/描述"
            prefix={<SearchOutlined />}
            style={{ width: 240 }}
            value={keyword}
            onChange={(event) => setKeyword(event.target.value)}
          />

          <Select
            value={isFixed}
            style={{ width: 140 }}
            onChange={(value) => setIsFixed(value)}
            options={[
              { label: '全部类型', value: 'all' },
              { label: '固定标签', value: 'fixed' },
              { label: '普通标签', value: 'normal' },
            ]}
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
