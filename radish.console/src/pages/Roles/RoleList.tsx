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
  type TableColumnsType,
} from '@radish/ui';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  ReloadOutlined,
  CheckOutlined,
  CloseOutlined,
} from '@radish/ui';
import { getRoleList, deleteRole, toggleRoleStatus, type RoleVo } from '@/api/roleApi';
import { RoleForm } from './RoleForm';
import { log } from '@/utils/logger';
import './RoleList.css';

export const RoleList = () => {
  useDocumentTitle('角色管理');
  const [roles, setRoles] = useState<RoleVo[]>([]);
  const [loading, setLoading] = useState(false);
  const [formVisible, setFormVisible] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [editingRoleId, setEditingRoleId] = useState<number>();

  // 加载角色列表
  const loadRoles = async () => {
    try {
      setLoading(true);
      const data = await getRoleList();
      setRoles(data);
    } catch (error) {
      log.error('RoleList', '加载角色列表失败:', error);
      message.error('加载角色列表失败');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadRoles();
  }, []);

  // 新增角色
  const handleCreate = () => {
    setFormMode('create');
    setEditingRoleId(undefined);
    setFormVisible(true);
  };

  // 编辑角色
  const handleEdit = (record: RoleVo) => {
    setFormMode('edit');
    setEditingRoleId(record.voId);
    setFormVisible(true);
  };

  // 删除角色
  const handleDelete = async (voId: number) => {
    try {
      await deleteRole(voId);
      message.success('删除角色成功');
      loadRoles();
    } catch (error) {
      log.error('RoleList', '删除角色失败:', error);
      message.error('删除角色失败');
    }
  };

  // 启用/禁用角色
  const handleToggleStatus = async (voId: number, enabled: boolean) => {
    try {
      await toggleRoleStatus(voId, enabled);
      message.success(enabled ? '启用角色成功' : '禁用角色成功');
      loadRoles();
    } catch (error) {
      log.error('RoleList', '操作失败:', error);
      message.error('操作失败');
    }
  };

  // 表单提交成功
  const handleFormSuccess = () => {
    setFormVisible(false);
    loadRoles();
  };

  // 权限范围映射
  const getAuthorityScopeText = (scope: number) => {
    const scopeMap: Record<number, string> = {
      [-1]: '无任何权限',
      [1]: '自定义权限',
      [2]: '本部门',
      [3]: '本部门及以下',
      [4]: '仅自己',
      [9]: '全部',
    };
    return scopeMap[scope] || '未知';
  };

  // 表格列定义（使用 vo 前缀字段）
  const columns: TableColumnsType<RoleVo> = [
    {
      title: 'ID',
      dataIndex: 'voId',
      key: 'voId',
      width: 80,
    },
    {
      title: '角色名称',
      dataIndex: 'voRoleName',
      key: 'voRoleName',
      width: 150,
    },
    {
      title: '角色描述',
      dataIndex: 'voRoleDescription',
      key: 'voRoleDescription',
      ellipsis: true,
    },
    {
      title: '排序',
      dataIndex: 'voOrderSort',
      key: 'voOrderSort',
      width: 80,
    },
    {
      title: '权限范围',
      dataIndex: 'voAuthorityScope',
      key: 'voAuthorityScope',
      width: 120,
      render: (scope: number) => getAuthorityScopeText(scope),
    },
    {
      title: '状态',
      key: 'status',
      width: 100,
      render: (_, record) => (
        <Tag color={record.voIsEnabled ? 'success' : 'error'}>
          {record.voIsEnabled ? '启用' : '禁用'}
        </Tag>
      ),
    },
    {
      title: '创建时间',
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (time: string) => time ? new Date(time).toLocaleString('zh-CN') : '-',
    },
    {
      title: '创建者',
      dataIndex: 'voCreateBy',
      key: 'voCreateBy',
      width: 120,
    },
    {
      title: '操作',
      key: 'action',
      width: 280,
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
          <Button
            variant={record.voIsEnabled ? 'danger' : 'primary'}
            size="small"
            icon={record.voIsEnabled ? <CloseOutlined /> : <CheckOutlined />}
            onClick={() => handleToggleStatus(record.voId, !record.voIsEnabled)}
          >
            {record.voIsEnabled ? '禁用' : '启用'}
          </Button>
          <Popconfirm
            title="确认删除"
            description="确定要删除这个角色吗？此操作不可恢复。"
            onConfirm={() => handleDelete(record.voId)}
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
    <div className="role-list-page">
      <div className="page-header">
        <h2>角色管理</h2>
        <Space>
          <Button
            icon={<ReloadOutlined />}
            onClick={loadRoles}
          >
            刷新
          </Button>
          <Button
            variant="primary"
            icon={<PlusOutlined />}
            onClick={handleCreate}
          >
            新增角色
          </Button>
        </Space>
      </div>

      <Table
        columns={columns}
        dataSource={roles}
        rowKey="voId"
        loading={loading}
        pagination={{
          showSizeChanger: true,
          showQuickJumper: true,
          showTotal: (total) => `共 ${total} 条`,
        }}
        scroll={{ x: 1200 }}
      />

      <RoleForm
        visible={formVisible}
        mode={formMode}
        roleId={editingRoleId}
        onCancel={() => setFormVisible(false)}
        onSuccess={handleFormSuccess}
      />
    </div>
  );
};
