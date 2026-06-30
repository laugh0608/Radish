import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  Table,
  Button,
  Space,
  Tag,
  message,
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
  SafetyOutlined,
} from '@radish/ui';
import { getRoleList, deleteRole, toggleRoleStatus, type RoleVo } from '@/api/roleApi';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import { RoleForm } from './RoleForm';
import { log } from '@/utils/logger';
import '../adminFeature.css';
import './RoleList.css';

export const RoleList = () => {
  useDocumentTitle('角色管理');
  const navigate = useNavigate();
  const [roles, setRoles] = useState<RoleVo[]>([]);
  const [loading, setLoading] = useState(false);
  const [formVisible, setFormVisible] = useState(false);
  const [formMode, setFormMode] = useState<'create' | 'edit'>('create');
  const [editingRoleId, setEditingRoleId] = useState<string>();
  const canViewRoles = usePermission(CONSOLE_PERMISSIONS.rolesView);
  const canCreateRole = usePermission(CONSOLE_PERMISSIONS.rolesCreate);
  const canEditRole = usePermission(CONSOLE_PERMISSIONS.rolesEdit);
  const canToggleRole = usePermission(CONSOLE_PERMISSIONS.rolesToggle);
  const canDeleteRole = usePermission(CONSOLE_PERMISSIONS.rolesDelete);
  const enabledRoles = roles.filter((role) => role.voIsEnabled).length;
  const customScopeRoles = roles.filter((role) => role.voAuthorityScope === 1).length;

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
    if (!canViewRoles) {
      return;
    }

    void loadRoles();
  }, [canViewRoles]);

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
  const handleDelete = async (voId: string) => {
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
  const handleToggleStatus = async (voId: string, enabled: boolean) => {
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
      width: 380,
      fixed: 'right',
      render: (_, record) => (
        <Space size="small">
          {canEditRole ? (
            <Button
              variant="ghost"
              size="small"
              icon={<SafetyOutlined />}
              onClick={() => navigate(`/roles/${record.voId}/permissions`)}
            >
              权限配置
            </Button>
          ) : null}
          {canEditRole ? (
            <Button
              variant="ghost"
              size="small"
              icon={<EditOutlined />}
              onClick={() => handleEdit(record)}
            >
              编辑
            </Button>
          ) : null}
          {canToggleRole ? (
            <Button
              variant={record.voIsEnabled ? 'danger' : 'primary'}
              size="small"
              icon={record.voIsEnabled ? <CloseOutlined /> : <CheckOutlined />}
              onClick={() => handleToggleStatus(record.voId, !record.voIsEnabled)}
            >
              {record.voIsEnabled ? '禁用' : '启用'}
            </Button>
          ) : null}
          {canDeleteRole ? (
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
          ) : null}
        </Space>
      ),
    },
  ];
  return (
    <div className="admin-feature-page role-list-page">
      <ConsolePageHeader
        eyebrow="RBAC PERMISSIONS"
        title="角色管理"
        description="维护后台角色、权限范围和权限配置入口。"
        icon={<SafetyOutlined />}
        status={(
          <ConsoleStatusChip tone={canViewRoles ? 'success' : 'danger'}>
            {canViewRoles ? '可查看' : '无权限'}
          </ConsoleStatusChip>
        )}
        actions={(
          <div className="role-list-header-actions">
            <Button
              icon={<ReloadOutlined />}
              onClick={loadRoles}
            >
              刷新
            </Button>
            {canCreateRole ? (
              <Button
                variant="primary"
                icon={<PlusOutlined />}
                onClick={handleCreate}
              >
                新增角色
              </Button>
            ) : null}
          </div>
        )}
      />

      <ConsoleMetricGrid label="角色列表指标">
        <ConsoleMetricCard label="全部角色" value={roles.length} description="当前已加载角色数量" tone="info" />
        <ConsoleMetricCard label="启用角色" value={enabledRoles} description="可参与授权的角色" tone="success" />
        <ConsoleMetricCard label="禁用角色" value={roles.length - enabledRoles} description="当前不可用角色" />
        <ConsoleMetricCard label="自定义权限" value={customScopeRoles} description="自定义权限范围角色" tone="warning" />
      </ConsoleMetricGrid>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title="角色列表"
            description="角色权限配置会跳转到独立权限页，当前列表仅承载角色基础信息和启停状态。"
            meta={(
              <ConsoleStatusChip tone={loading ? 'info' : 'neutral'}>
                {loading ? '加载中' : '全部角色'}
              </ConsoleStatusChip>
            )}
          />

          <section className="admin-table-panel">
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
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>权限摘要</h3>
          <p className="admin-feature-subtle">用于核对当前角色范围和可执行权限动作。</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">角色范围</span>
              <span className="admin-table-summary__value">全部角色</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">权限配置</span>
              <span className="admin-table-summary__value">
                {canEditRole ? '可进入权限配置' : '无权限配置权限'}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">启停权限</span>
              <span className="admin-table-summary__value">
                {canToggleRole ? '可启用 / 禁用' : '仅可查看启停状态'}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">删除权限</span>
              <span className="admin-table-summary__value">
                {canDeleteRole ? '可删除角色' : '不可删除角色'}
              </span>
            </div>
          </div>
        </aside>
      </div>

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
