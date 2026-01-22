import { useState, useEffect } from 'react';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  Table,
  Button,
  Space,
  Tag,
  message,
  Modal,
  Popconfirm,
  AntSelect as Select,
  AntInput as Input,
  type TableColumnsType,
} from '@radish/ui';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  ReloadOutlined,
  SearchOutlined,
} from '@radish/ui';
import {
  getSystemConfigs,
  getConfigCategories,
  deleteConfig,
  type SystemConfig,
} from '@/api/systemConfigApi';
import { SystemConfigForm } from './SystemConfigForm';
import { log } from '@/utils/logger';
import './SystemConfigList.css';

export const SystemConfigList = () => {
  useDocumentTitle('系统配置');
  const [configs, setConfigs] = useState<SystemConfig[]>([]);
  const [filteredConfigs, setFilteredConfigs] = useState<SystemConfig[]>([]);
  const [categories, setCategories] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [formVisible, setFormVisible] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [editingConfigId, setEditingConfigId] = useState<number>();

  // 筛选条件
  const [selectedCategory, setSelectedCategory] = useState<string>('');
  const [searchKeyword, setSearchKeyword] = useState<string>('');

  // 加载配置列表
  const loadConfigs = async () => {
    try {
      setLoading(true);
      const data = await getSystemConfigs();
      setConfigs(data);
      setFilteredConfigs(data);
    } catch (error) {
      log.error('SystemConfigList', '加载配置列表失败:', error);
      message.error('加载配置列表失败');
    } finally {
      setLoading(false);
    }
  };

  // 加载配置分类
  const loadCategories = async () => {
    try {
      const data = await getConfigCategories();
      setCategories(data);
    } catch (error) {
      log.error('SystemConfigList', '加载配置分类失败:', error);
    }
  };

  useEffect(() => {
    loadConfigs();
    loadCategories();
  }, []);

  // 筛选配置
  useEffect(() => {
    let filtered = configs;

    // 按分类筛选
    if (selectedCategory) {
      filtered = filtered.filter(config => config.category === selectedCategory);
    }

    // 按关键词搜索
    if (searchKeyword) {
      const keyword = searchKeyword.toLowerCase();
      filtered = filtered.filter(config =>
        config.name.toLowerCase().includes(keyword) ||
        config.key.toLowerCase().includes(keyword) ||
        config.description?.toLowerCase().includes(keyword)
      );
    }

    setFilteredConfigs(filtered);
  }, [configs, selectedCategory, searchKeyword]);

  // 新增配置
  const handleCreate = () => {
    setFormMode('create');
    setEditingConfigId(undefined);
    setFormVisible(true);
  };

  // 编辑配置
  const handleEdit = (record: SystemConfig) => {
    setFormMode('edit');
    setEditingConfigId(record.id);
    setFormVisible(true);
  };

  // 删除配置
  const handleDelete = async (id: number) => {
    try {
      await deleteConfig(id);
      message.success('删除配置成功');
      loadConfigs();
    } catch (error) {
      log.error('SystemConfigList', '删除配置失败:', error);
      message.error('删除配置失败');
    }
  };

  // 表单提交成功
  const handleFormSuccess = () => {
    setFormVisible(false);
    loadConfigs();
  };

  // 重置筛选条件
  const handleResetFilter = () => {
    setSelectedCategory('');
    setSearchKeyword('');
  };

  // 获取类型标签颜色
  const getTypeTagColor = (type: string) => {
    const colorMap: Record<string, string> = {
      string: 'blue',
      number: 'green',
      boolean: 'orange',
      json: 'purple',
    };
    return colorMap[type] || 'default';
  };

  // 表格列定义
  const columns: TableColumnsType<SystemConfig> = [
    {
      title: 'ID',
      dataIndex: 'id',
      key: 'id',
      width: 80,
    },
    {
      title: '分类',
      dataIndex: 'category',
      key: 'category',
      width: 120,
      render: (category: string) => (
        <Tag color="cyan">{category}</Tag>
      ),
    },
    {
      title: '配置名称',
      dataIndex: 'name',
      key: 'name',
      width: 150,
    },
    {
      title: '配置键',
      dataIndex: 'key',
      key: 'key',
      width: 200,
      render: (key: string) => (
        <code style={{ fontSize: '12px', background: '#f5f5f5', padding: '2px 4px', borderRadius: '2px' }}>
          {key}
        </code>
      ),
    },
    {
      title: '配置值',
      dataIndex: 'value',
      key: 'value',
      width: 150,
      ellipsis: true,
      render: (value: string, record) => {
        if (record.type === 'boolean') {
          return <Tag color={value === 'true' ? 'success' : 'error'}>{value}</Tag>;
        }
        return <span>{value}</span>;
      },
    },
    {
      title: '类型',
      dataIndex: 'type',
      key: 'type',
      width: 80,
      render: (type: string) => (
        <Tag color={getTypeTagColor(type)}>{type}</Tag>
      ),
    },
    {
      title: '描述',
      dataIndex: 'description',
      key: 'description',
      ellipsis: true,
    },
    {
      title: '状态',
      key: 'status',
      width: 80,
      render: (_, record) => (
        <Tag color={record.isEnabled ? 'success' : 'error'}>
          {record.isEnabled ? '启用' : '禁用'}
        </Tag>
      ),
    },
    {
      title: '修改时间',
      dataIndex: 'modifyTime',
      key: 'modifyTime',
      width: 180,
      render: (time: string) => time ? new Date(time).toLocaleString('zh-CN') : '-',
    },
    {
      title: '操作',
      key: 'action',
      width: 160,
      fixed: 'right',
      render: (_, record) => (
        <Space size="small">
          <Button
            variant="ghost"
            size="small"
            icon={<EditOutlined />}
            onClick={() => handleEdit(record)}
          >
            编辑
          </Button>
          <Popconfirm
            title="确认删除"
            description="确定要删除这个配置吗？此操作不可恢复。"
            onConfirm={() => handleDelete(record.id)}
            okText="确定"
            cancelText="取消"
          >
            <Button
              variant="danger"
              size="small"
              icon={<DeleteOutlined />}
            >
              删除
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div className="system-config-list-page">
      <div className="page-header">
        <h2>系统配置</h2>
        <Space>
          <Button
            icon={<ReloadOutlined />}
            onClick={loadConfigs}
          >
            刷新
          </Button>
          <Button
            variant="primary"
            icon={<PlusOutlined />}
            onClick={handleCreate}
          >
            新增配置
          </Button>
        </Space>
      </div>

      {/* 筛选条件 */}
      <div className="filter-bar">
        <Space wrap>
          <Select
            placeholder="选择分类"
            style={{ width: 150 }}
            value={selectedCategory || undefined}
            onChange={setSelectedCategory}
            allowClear
            options={categories.map(category => ({ label: category, value: category }))}
          />
          <Input
            placeholder="搜索配置名称、键或描述"
            style={{ width: 250 }}
            value={searchKeyword}
            onChange={(e) => setSearchKeyword(e.target.value)}
            prefix={<SearchOutlined />}
            allowClear
          />
          <Button onClick={handleResetFilter}>重置</Button>
        </Space>
      </div>

      <Table
        columns={columns}
        dataSource={filteredConfigs}
        rowKey="id"
        loading={loading}
        pagination={{
          showSizeChanger: true,
          showQuickJumper: true,
          showTotal: (total) => `共 ${total} 条`,
        }}
        scroll={{ x: 1400 }}
      />

      <SystemConfigForm
        visible={formVisible}
        mode={formMode}
        configId={editingConfigId}
        categories={categories}
        onCancel={() => setFormVisible(false)}
        onSuccess={handleFormSuccess}
      />
    </div>
  );
};