import { useCallback, useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
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
import { formatConsoleDateTime } from '@/utils/localeFormatters';
import '../adminFeature.css';
import './RoleList.css';

export const RoleList = () => {
  const { t, i18n } = useTranslation();
  useDocumentTitle(t('roles.documentTitle'));
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
  const loadRoles = useCallback(async () => {
    try {
      setLoading(true);
      const data = await getRoleList();
      setRoles(data);
    } catch (error) {
      log.error('RoleList', '加载角色列表失败:', error);
      message.error(t('roles.feedback.loadFailed'));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    if (!canViewRoles) {
      return;
    }

    void loadRoles();
  }, [canViewRoles, loadRoles]);

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
      message.success(t('roles.feedback.deleted'));
      loadRoles();
    } catch (error) {
      log.error('RoleList', '删除角色失败:', error);
      message.error(t('roles.feedback.deleteFailed'));
    }
  };

  // 启用/禁用角色
  const handleToggleStatus = async (voId: string, enabled: boolean) => {
    try {
      await toggleRoleStatus(voId, enabled);
      message.success(t(enabled ? 'roles.feedback.enabled' : 'roles.feedback.disabled'));
      loadRoles();
    } catch (error) {
      log.error('RoleList', '操作失败:', error);
      message.error(t('roles.feedback.toggleFailed'));
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
      [-1]: t('roles.scope.none'),
      [1]: t('roles.scope.custom'),
      [2]: t('roles.scope.department'),
      [3]: t('roles.scope.departmentAndChildren'),
      [4]: t('roles.scope.self'),
      [9]: t('roles.scope.all'),
    };
    return scopeMap[scope] || t('roles.scope.unknown');
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
      title: t('roles.table.name'),
      dataIndex: 'voRoleName',
      key: 'voRoleName',
      width: 150,
    },
    {
      title: t('roles.table.description'),
      dataIndex: 'voRoleDescription',
      key: 'voRoleDescription',
      ellipsis: true,
    },
    {
      title: t('roles.table.sort'),
      dataIndex: 'voOrderSort',
      key: 'voOrderSort',
      width: 80,
    },
    {
      title: t('roles.table.scope'),
      dataIndex: 'voAuthorityScope',
      key: 'voAuthorityScope',
      width: 120,
      render: (scope: number) => getAuthorityScopeText(scope),
    },
    {
      title: t('roles.table.status'),
      key: 'status',
      width: 100,
      render: (_, record) => (
        <Tag color={record.voIsEnabled ? 'success' : 'error'}>
          {t(record.voIsEnabled ? 'roles.status.enabled' : 'roles.status.disabled')}
        </Tag>
      ),
    },
    {
      title: t('roles.table.createdAt'),
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (time: string) => formatConsoleDateTime(time, i18n.resolvedLanguage ?? i18n.language),
    },
    {
      title: t('roles.table.createdBy'),
      dataIndex: 'voCreateBy',
      key: 'voCreateBy',
      width: 120,
    },
    {
      title: t('roles.table.actions'),
      key: 'action',
      width: 380,
      fixed: 'right',
      render: (_, record) => (
        <Space size="small" wrap>
          {canEditRole ? (
            <Button
              variant="ghost"
              size="small"
              icon={<SafetyOutlined />}
              onClick={() => navigate(`/roles/${record.voId}/permissions`)}
            >
              {t('roles.actions.permissions')}
            </Button>
          ) : null}
          {canEditRole ? (
            <Button
              variant="ghost"
              size="small"
              icon={<EditOutlined />}
              onClick={() => handleEdit(record)}
            >
              {t('roles.actions.edit')}
            </Button>
          ) : null}
          {canToggleRole ? (
            <Button
              variant={record.voIsEnabled ? 'danger' : 'primary'}
              size="small"
              icon={record.voIsEnabled ? <CloseOutlined /> : <CheckOutlined />}
              onClick={() => handleToggleStatus(record.voId, !record.voIsEnabled)}
            >
              {t(record.voIsEnabled ? 'roles.actions.disable' : 'roles.actions.enable')}
            </Button>
          ) : null}
          {canDeleteRole ? (
            <Popconfirm
              title={t('roles.delete.title')}
              description={t('roles.delete.description')}
              onConfirm={() => handleDelete(record.voId)}
              okText={t('roles.common.confirm')}
              cancelText={t('roles.common.cancel')}
            >
              <Button
                variant="danger"
                size="small"
                icon={<DeleteOutlined />}
              >
                {t('roles.actions.delete')}
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
        eyebrow={t('roles.page.eyebrow')}
        title={t('roles.page.title')}
        description={t('roles.page.description')}
        icon={<SafetyOutlined />}
        status={(
          <ConsoleStatusChip tone={canViewRoles ? 'success' : 'danger'}>
            {t(canViewRoles ? 'roles.page.viewable' : 'roles.page.forbidden')}
          </ConsoleStatusChip>
        )}
        actions={(
          <div className="role-list-header-actions">
            <Button
              icon={<ReloadOutlined />}
              onClick={loadRoles}
            >
              {t('roles.actions.refresh')}
            </Button>
            {canCreateRole ? (
              <Button
                variant="primary"
                icon={<PlusOutlined />}
                onClick={handleCreate}
              >
                {t('roles.actions.create')}
              </Button>
            ) : null}
          </div>
        )}
      />

      <ConsoleMetricGrid label={t('roles.metrics.ariaLabel')}>
        <ConsoleMetricCard label={t('roles.metrics.total')} value={roles.length} description={t('roles.metrics.totalDescription')} tone="info" />
        <ConsoleMetricCard label={t('roles.metrics.enabled')} value={enabledRoles} description={t('roles.metrics.enabledDescription')} tone="success" />
        <ConsoleMetricCard label={t('roles.metrics.disabled')} value={roles.length - enabledRoles} description={t('roles.metrics.disabledDescription')} />
        <ConsoleMetricCard label={t('roles.metrics.custom')} value={customScopeRoles} description={t('roles.metrics.customDescription')} tone="warning" />
      </ConsoleMetricGrid>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title={t('roles.list.title')}
            description={t('roles.list.description')}
            meta={(
              <ConsoleStatusChip tone={loading ? 'info' : 'neutral'}>
                {t(loading ? 'roles.status.loading' : 'roles.status.all')}
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
                showTotal: (total) => t('roles.count.roles', { count: total }),
              }}
              scroll={{ x: 1200 }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>{t('roles.summary.title')}</h3>
          <p className="admin-feature-subtle">{t('roles.summary.description')}</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('roles.summary.scope')}</span>
              <span className="admin-table-summary__value">{t('roles.summary.allRoles')}</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('roles.summary.permissionConfig')}</span>
              <span className="admin-table-summary__value">
                {t(canEditRole ? 'roles.summary.permissionWritable' : 'roles.summary.permissionReadOnly')}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('roles.summary.toggle')}</span>
              <span className="admin-table-summary__value">
                {t(canToggleRole ? 'roles.summary.toggleWritable' : 'roles.summary.toggleReadOnly')}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">{t('roles.summary.delete')}</span>
              <span className="admin-table-summary__value">
                {t(canDeleteRole ? 'roles.summary.deleteWritable' : 'roles.summary.deleteReadOnly')}
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
