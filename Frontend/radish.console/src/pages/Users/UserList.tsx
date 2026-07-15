import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  Table,
  Button,
  AntInput as Input,
  AntSelect as Select,
  Space,
  Tag,
  Avatar,
  message,
  type TableColumnsType,
  formatLocalizedDateTime,
} from '@radish/ui';
import { useTranslation } from 'react-i18next';
import {
  EditOutlined,
  SearchOutlined,
  ReloadOutlined,
  UserOutlined,
  TeamOutlined,
} from '@radish/ui';
import {
  userManagementApi,
  getUserStatusColor,
  UserStatus,
  type UserListParams,
} from '../../api/userManagement';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import type { UserListItem } from '../../types/user';
import { getAvatarUrl } from '@/config/env';
import { log } from '../../utils/logger';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import '../adminFeature.css';
import './UserList.css';

function formatUserTime(value: string | null | undefined, language: string) {
  if (!value) {
    return '-';
  }

  return formatLocalizedDateTime(value, language);
}

function getListItemStatus(user: UserListItem) {
  return user.voIsEnable ? UserStatus.Normal : UserStatus.Disabled;
}

export const UserList = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('console.route.users'));
  const navigate = useNavigate();
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [total, setTotal] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const canViewUsers = usePermission(CONSOLE_PERMISSIONS.usersView);
  const getUserStatusLabel = (currentStatus: UserStatus) => {
    switch (currentStatus) {
      case UserStatus.Normal:
        return t('users.list.status.normal');
      case UserStatus.Disabled:
        return t('users.list.status.disabled');
      case UserStatus.Locked:
        return t('users.list.status.locked');
      default:
        return t('users.list.status.unknown');
    }
  };

  // 筛选条件
  const [keyword, setKeyword] = useState<string>('');
  const [status, setStatus] = useState<UserStatus | undefined>();
  const [role, setRole] = useState<string | undefined>();
  const activeFilterCount = [
    keyword.trim() ? 'keyword' : undefined,
    status !== undefined ? 'status' : undefined,
    role,
  ].filter(Boolean).length;
  const enabledUsers = users.filter((user) => user.voIsEnable).length;
  const disabledUsers = users.length - enabledUsers;
  const selectedUser = users.find((user) => !user.voIsEnable) ?? users[0] ?? null;
  const selectedUserName = selectedUser
    ? resolveVisibleUserDisplayName(
        selectedUser,
        selectedUser.uuid
          ? t('users.common.userFallback', { id: selectedUser.uuid })
          : t('common.unknownUser'),
      )
    : '';
  const selectedUserHandle = selectedUser ? resolveVisibleUserHandle(selectedUser, selectedUserName) : '';

  // 加载用户列表
  const loadUsers = async (
    targetPageIndex = pageIndex,
    targetPageSize = pageSize,
    targetKeyword = keyword,
    targetStatus = status,
    targetRole = role
  ) => {
    try {
      setLoading(true);

      const params: UserListParams = {
        pageIndex: targetPageIndex,
        pageSize: targetPageSize,
        keyword: targetKeyword.trim() || undefined,
        status: targetStatus,
        role: targetRole,
      };

      const response = await userManagementApi.getUserList(params);

      if (response.ok && response.data) {
        setUsers(response.data.items);
        setTotal(response.data.total);
        setPageIndex(response.data.pageIndex || targetPageIndex);
        setPageSize(response.data.pageSize || targetPageSize);
      } else {
        message.error(response.message || t('users.list.loadFailed'));
      }
    } catch (error) {
      log.error('UserList', '加载用户列表失败', error);
      message.error(t('users.list.loadFailed'));
    } finally {
      setLoading(false);
    }
  };

  // 搜索用户
  const handleSearch = () => {
    setPageIndex(1);
    void loadUsers(1, pageSize);
  };

  // 重置筛选条件
  const handleReset = () => {
    setKeyword('');
    setStatus(undefined);
    setRole(undefined);
    setPageIndex(1);
    void loadUsers(1, pageSize, '', undefined, undefined);
  };

  // 表格列定义
  const columns: TableColumnsType<UserListItem> = [
    {
      title: t('users.list.column.user'),
      key: 'user',
      width: 260,
      render: (_, record) => (
        (() => {
          const displayName = resolveVisibleUserDisplayName(
            record,
            record.uuid ? t('users.common.userFallback', { id: record.uuid }) : t('common.unknownUser'),
          );
          const displayHandle = resolveVisibleUserHandle(record, displayName);

          return (
            <div className="user-list-identity">
              <Avatar
                size="small"
                src={getAvatarUrl(record.voAvatarThumbnailUrl || record.voAvatarUrl)}
                icon={<UserOutlined />}
              />
              <div className="user-list-identity__content">
                <div className="user-list-identity__name">{displayName}</div>
                {displayHandle && (
                  <div className="user-list-identity__email">{displayHandle}</div>
                )}
                {record.voUserEmail && (
                  <div className="user-list-identity__email">{record.voUserEmail}</div>
                )}
              </div>
            </div>
          );
        })()
      ),
    },
    {
      title: t('users.list.column.status'),
      key: 'status',
      width: 100,
      render: (_, record) => {
        const currentStatus = getListItemStatus(record);
        return (
          <Tag color={getUserStatusColor(currentStatus)}>
            {getUserStatusLabel(currentStatus)}
          </Tag>
        );
      },
    },
    {
      title: t('users.list.column.publicIndex'),
      key: 'publicIndex',
      width: 120,
      render: (_, record) => record.voPublicIndex ? `#${record.voPublicIndex}` : '-',
    },
    {
      title: t('users.list.column.createdAt'),
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (voCreateTime: string) => formatUserTime(voCreateTime, language),
    },
    {
      title: t('users.list.column.updatedAt'),
      dataIndex: 'voUpdateTime',
      key: 'voUpdateTime',
      width: 180,
      render: (voUpdateTime?: string | null) => formatUserTime(voUpdateTime, language),
    },
    {
      title: t('users.list.column.action'),
      key: 'actions',
      width: 160,
      render: (_, record) => (
        <Space size="small" wrap>
          {canViewUsers ? (
            <Button
              variant="ghost"
              size="small"
              icon={<EditOutlined />}
              onClick={() => navigate(`/users/${record.uuid}`)}
            >
              {t('users.list.action.viewDetail')}
            </Button>
          ) : null}
        </Space>
      ),
    },
  ];

  // 初始化加载
  useEffect(() => {
    if (!canViewUsers) {
      return;
    }

    void loadUsers();
    // User list initial loading is permission-scoped; searches and pagination pass explicit state into loadUsers.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [canViewUsers]);
  return (
    <div className="admin-feature-page user-list">
      <ConsolePageHeader
        eyebrow={t('users.list.eyebrow')}
        title={t('users.list.title')}
        description={t('users.list.description')}
        icon={<TeamOutlined />}
        status={(
          <ConsoleStatusChip tone={canViewUsers ? 'success' : 'danger'}>
            {t(canViewUsers ? 'users.common.viewable' : 'users.common.noPermission')}
          </ConsoleStatusChip>
        )}
      />

      <ConsoleMetricGrid label={t('users.list.metrics.label')}>
        <ConsoleMetricCard label={t('users.list.metrics.results')} value={total} description={t('users.list.metrics.resultsDescription')} tone="info" />
        <ConsoleMetricCard label={t('users.list.metrics.page')} value={users.length} description={t('users.list.metrics.pageDescription')} />
        <ConsoleMetricCard label={t('users.list.metrics.enabled')} value={enabledUsers} description={t('users.list.metrics.enabledDescription')} tone="success" />
        <ConsoleMetricCard label={t('users.list.metrics.disabled')} value={disabledUsers} description={t('users.list.metrics.disabledDescription')} tone={disabledUsers > 0 ? 'warning' : 'success'} />
      </ConsoleMetricGrid>

      <section className="governance-task-flow" aria-label={t('users.list.flow.label')}>
        <div className="governance-task-flow__item">
          <span>1</span>
          <strong>{t('users.list.flow.scopeTitle')}</strong>
          <p>{t('users.list.flow.scope', { total, visible: users.length })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>2</span>
          <strong>{t('users.list.flow.statusTitle')}</strong>
          <p>{t('users.list.flow.status', { enabled: enabledUsers, disabled: disabledUsers })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>3</span>
          <strong>{t('users.list.flow.detailTitle')}</strong>
          <p>{t('users.list.flow.detail', {
            user: selectedUser
              ? `${selectedUserName} ${selectedUserHandle || ''}`.trim()
              : t('users.list.flow.selectUser'),
          })}</p>
        </div>
        <div className="governance-task-flow__item">
          <span>4</span>
          <strong>{t('users.list.flow.auditTitle')}</strong>
          <p>{t('users.list.flow.audit')}</p>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title={t('users.list.toolbar.title')}
            description={t('users.list.toolbar.description')}
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? t('users.list.filterCount', { count: activeFilterCount }) : t('users.list.noFilters')}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Input
                className="user-list-filter-input"
                placeholder={t('users.list.filter.keyword')}
                value={keyword}
                onChange={(e) => setKeyword(e.target.value)}
                onPressEnter={handleSearch}
                allowClear
              />

              <Select
                className="user-list-filter-select user-list-filter-select--status"
                placeholder={t('users.list.filter.status')}
                value={status}
                onChange={setStatus}
                allowClear
              >
                <Select.Option value={UserStatus.Normal}>{t('users.list.status.normal')}</Select.Option>
                <Select.Option value={UserStatus.Disabled}>{t('users.list.status.disabled')}</Select.Option>
                <Select.Option value={UserStatus.Locked}>{t('users.list.status.locked')}</Select.Option>
              </Select>

              <Select
                className="user-list-filter-select"
                placeholder={t('users.list.filter.role')}
                value={role}
                onChange={setRole}
                allowClear
              >
                <Select.Option value="System">{t('users.list.role.system')}</Select.Option>
                <Select.Option value="Admin">{t('users.list.role.admin')}</Select.Option>
                <Select.Option value="User">{t('users.list.role.user')}</Select.Option>
              </Select>

              <Button
                variant="primary"
                icon={<SearchOutlined />}
                onClick={handleSearch}
              >
                {t('users.list.search')}
              </Button>

              <Button
                icon={<ReloadOutlined />}
                onClick={handleReset}
              >
                {t('users.list.reset')}
              </Button>
            </div>
          </ConsoleToolbar>

          <section className="admin-table-panel">
            <Table
              columns={columns}
              dataSource={users}
              rowKey="uuid"
              loading={loading}
              pagination={{
                current: pageIndex,
                pageSize,
                total,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (total, range) =>
                  t('users.list.pagination', { start: range[0], end: range[1], total }),
                onChange: (page, size) => {
                  const nextPageSize = size || 20;
                  setPageIndex(page);
                  setPageSize(nextPageSize);
                  void loadUsers(page, nextPageSize);
                },
              }}
              scroll={{ x: 1100 }}
            />
          </section>
        </main>

        <aside className="admin-table-aside" aria-label={t('users.list.summary.label')}>
          <div className="admin-feature-rail__header">
            <div>
              <span className="admin-feature-rail__eyebrow">{t('users.list.summary.eyebrow')}</span>
              <h3>{t('users.list.summary.label')}</h3>
            </div>
            <ConsoleStatusChip tone={canViewUsers ? 'success' : 'danger'}>
              {t(canViewUsers ? 'users.common.viewable' : 'users.common.noPermission')}
            </ConsoleStatusChip>
          </div>
          <p className="admin-feature-subtle">{t('users.list.summary.description')}</p>
          <div className="admin-feature-rail__list">
            <div className="admin-feature-rail__item">
              <span>{t('users.list.summary.scope')}</span>
              <strong>{activeFilterCount > 0 ? t('users.list.summary.filterCount', { count: activeFilterCount }) : t('users.list.summary.allUsers')}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>{t('users.list.summary.pagination')}</span>
              <strong>{t('users.list.summary.paginationValue', { pageSize, page: pageIndex })}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>{t('users.list.summary.statusQueue')}</span>
              <strong>{disabledUsers > 0 ? t('users.list.summary.disabledAttention', { count: disabledUsers }) : t('users.list.summary.noDisabled')}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>{t('users.list.summary.actions')}</span>
              <strong>{t(canViewUsers ? 'users.list.summary.viewDetail' : 'users.list.summary.noViewPermission')}</strong>
            </div>
          </div>
          {selectedUser ? (
            <>
              <div className="admin-feature-rail__callout">
                <span>{t('users.list.summary.currentUser')}</span>
                <strong>{selectedUserName}</strong>
                <p>{selectedUserHandle || selectedUser.voUserEmail || selectedUser.uuid}</p>
                <Space wrap>
                  <Tag color={getUserStatusColor(getListItemStatus(selectedUser))}>
                    {getUserStatusLabel(getListItemStatus(selectedUser))}
                  </Tag>
                  {selectedUser.voPublicIndex ? <Tag color="blue">#{selectedUser.voPublicIndex}</Tag> : null}
                  {selectedUser.voIsDeleted ? <Tag color="default">{t('users.list.status.deleted')}</Tag> : null}
                </Space>
              </div>
              <div className="admin-feature-rail__list">
                <div className="admin-feature-rail__item">
                  <span>{t('users.list.summary.email')}</span>
                  <strong>{selectedUser.voUserEmail || '-'}</strong>
                </div>
                <div className="admin-feature-rail__item">
                  <span>{t('users.list.summary.createdAt')}</span>
                  <strong>{formatUserTime(selectedUser.voCreateTime, language)}</strong>
                </div>
                <div className="admin-feature-rail__item">
                  <span>{t('users.list.summary.updatedAt')}</span>
                  <strong>{formatUserTime(selectedUser.voUpdateTime, language)}</strong>
                </div>
              </div>
              <div className="admin-feature-rail__actions">
                <Button
                  variant="ghost"
                  size="small"
                  icon={<EditOutlined />}
                  onClick={() => navigate(`/users/${selectedUser.uuid}`)}
                >
                  {t('users.list.summary.openDetail')}
                </Button>
                <Button
                  variant="ghost"
                  size="small"
                  icon={<SearchOutlined />}
                  onClick={() => {
                    const nextKeyword = selectedUser.voUserEmail || selectedUserHandle || selectedUserName;
                    setKeyword(nextKeyword);
                    setPageIndex(1);
                    void loadUsers(1, pageSize, nextKeyword, status, role);
                  }}
                >
                  {t('users.list.summary.useAsFilter')}
                </Button>
              </div>
              <div className="admin-feature-inline-context">
                <strong>{t('users.list.summary.riskTitle')}</strong>
                <span>{t('users.list.summary.riskDescription')}</span>
              </div>
            </>
          ) : (
            <p className="admin-feature-rail__empty">{t('users.list.summary.empty')}</p>
          )}
        </aside>
      </div>
    </div>
  );
};
