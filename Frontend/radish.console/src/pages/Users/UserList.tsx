import { useCallback, useEffect, useRef, useState } from 'react';
import { useLocation, useNavigate, useSearchParams } from 'react-router';
import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import {
  Avatar,
  BottomSheet,
  Button,
  Space,
  Table,
  Tag,
  message,
  AntInput as Input,
  AntSelect as Select,
  type TableColumnsType,
  formatLocalizedDateTime,
} from '@radish/ui';
import { ReloadOutlined, SearchOutlined, TeamOutlined, UserOutlined } from '@radish/ui';
import { useTranslation } from 'react-i18next';
import { userManagementApi } from '@/api/userManagement';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import {
  ConsoleMetricCard,
  ConsoleMetricGrid,
  ConsolePageHeader,
  ConsoleResourceList,
  ConsoleStatusChip,
  ConsoleToolbar,
} from '@/components/ConsolePage';
import { getAvatarUrl } from '@/config/env';
import type { UserListItem } from '@/types/user';
import { formatConsoleNumber } from '@/utils/localeFormatters';
import { log } from '@/utils/logger';
import { resolveVisibleUserDisplayName, resolveVisibleUserHandle } from '@/utils/userIdentityDisplay';
import {
  buildUserDetailPath,
  DEFAULT_USER_LIST_QUERY,
  parseUserListQuery,
  serializeUserListQuery,
  type UserListQuery,
  type UserStatusFilter,
} from './userListUrlState';
import '../adminFeature.css';
import './UserList.css';

type ResourceReadState = 'loading' | 'ready' | 'unavailable' | 'stale';

function formatUserTime(value: string | null | undefined, language: string) {
  return value ? formatLocalizedDateTime(value, language) : '-';
}

export const UserList = () => {
  const { t, i18n } = useTranslation();
  const language = i18n.resolvedLanguage ?? i18n.language;
  useDocumentTitle(t('console.route.users'));
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const [query, setQuery] = useState<UserListQuery>(() => parseUserListQuery(searchParams));
  const [keywordDraft, setKeywordDraft] = useState(query.keyword);
  const [statusDraft, setStatusDraft] = useState<UserStatusFilter>(query.status);
  const [roleDraft, setRoleDraft] = useState(query.roleName);
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [readState, setReadState] = useState<ResourceReadState>('loading');
  const [filterSheetOpen, setFilterSheetOpen] = useState(false);
  const requestSequence = useRef(0);
  const snapshotQueryKey = useRef<string | undefined>(undefined);
  const canViewUsers = usePermission(CONSOLE_PERMISSIONS.usersView);

  useEffect(() => {
    const nextSearch = serializeUserListQuery(query);
    if (nextSearch.toString() !== searchParams.toString()) {
      setSearchParams(nextSearch, { replace: true });
    }
  }, [query, searchParams, setSearchParams]);

  const activeFilterCount = [
    query.keyword ? 'keyword' : undefined,
    query.status !== 'all' ? 'status' : undefined,
    query.roleName ? 'role' : undefined,
  ].filter(Boolean).length;
  const enabledUsers = users.filter((user) => user.voIsEnable).length;
  const disabledUsers = users.length - enabledUsers;
  const pageCount = Math.max(1, Math.ceil(total / query.pageSize));
  const selectedUser = users.find((user) => !user.voIsEnable) ?? users[0] ?? null;
  const serializedQuery = serializeUserListQuery(query).toString();
  const currentReturnTo = `${location.pathname}${serializedQuery ? `?${serializedQuery}` : ''}`;

  const loadUsers = useCallback(async () => {
    const requestId = requestSequence.current + 1;
    const queryKey = JSON.stringify(query);
    const hasCurrentSnapshot = snapshotQueryKey.current === queryKey;
    requestSequence.current = requestId;

    try {
      setLoading(true);
      setReadState('loading');
      if (!hasCurrentSnapshot) {
        setUsers([]);
        setTotal(0);
      }

      const response = await userManagementApi.getUserList({
        pageIndex: query.pageIndex,
        pageSize: query.pageSize,
        keyword: query.keyword || undefined,
        isEnabled: query.status === 'all' ? undefined : query.status === 'enabled',
        roleName: query.roleName || undefined,
      });
      if (requestSequence.current !== requestId) return;
      if (!response.ok || !response.data) {
        throw new Error(response.message || t('users.list.loadFailed'));
      }

      const responsePageCount = Math.max(1, Math.ceil(response.data.total / response.data.pageSize));
      if (response.data.items.length === 0 && response.data.total > 0 && query.pageIndex > responsePageCount) {
        setQuery((current) => ({ ...current, pageIndex: responsePageCount }));
        return;
      }

      setUsers(response.data.items);
      setTotal(response.data.total);
      snapshotQueryKey.current = queryKey;
      setReadState('ready');
    } catch (error) {
      if (requestSequence.current !== requestId) return;
      log.error('UserList', '加载用户列表失败', error);
      setReadState(hasCurrentSnapshot ? 'stale' : 'unavailable');
      message.error(error instanceof Error ? error.message : t('users.list.loadFailed'));
    } finally {
      if (requestSequence.current === requestId) setLoading(false);
    }
  }, [query, t]);

  useEffect(() => {
    if (canViewUsers) void loadUsers();
  }, [canViewUsers, loadUsers]);

  const handleApplyFilters = () => {
    setQuery((current) => ({
      ...current,
      pageIndex: 1,
      keyword: keywordDraft.trim(),
      status: statusDraft,
      roleName: roleDraft.trim(),
    }));
    setFilterSheetOpen(false);
  };

  const handleResetFilters = () => {
    setKeywordDraft('');
    setStatusDraft('all');
    setRoleDraft('');
    setQuery((current) => ({ ...DEFAULT_USER_LIST_QUERY, pageSize: current.pageSize }));
    setFilterSheetOpen(false);
  };

  const handlePageChange = (pageIndex: number, pageSize = query.pageSize) => {
    setQuery((current) => ({ ...current, pageIndex, pageSize }));
  };

  const openDetail = (userId: string) => {
    navigate(buildUserDetailPath(userId, currentReturnTo));
  };

  const renderIdentity = (record: UserListItem) => {
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
          {displayHandle ? <div className="user-list-identity__email">{displayHandle}</div> : null}
          {record.voUserEmail ? <div className="user-list-identity__email">{record.voUserEmail}</div> : null}
        </div>
      </div>
    );
  };

  const columns: TableColumnsType<UserListItem> = [
    { title: t('users.list.column.user'), key: 'user', width: 280, render: (_, record) => renderIdentity(record) },
    {
      title: t('users.list.column.status'),
      key: 'status',
      width: 100,
      render: (_, record) => (
        <Tag color={record.voIsEnable ? 'success' : 'error'}>
          {t(record.voIsEnable ? 'users.common.enabled' : 'users.common.disabled')}
        </Tag>
      ),
    },
    {
      title: t('users.list.column.roles'),
      key: 'roles',
      width: 220,
      render: (_, record) => (
        <div className="user-roles">
          {record.voRoleNames.length > 0
            ? record.voRoleNames.map((roleName) => <Tag key={roleName}>{roleName}</Tag>)
            : <span className="admin-feature-subtle">{t('users.list.noRoles')}</span>}
        </div>
      ),
    },
    { title: t('users.list.column.publicIndex'), key: 'publicIndex', width: 120, render: (_, record) => record.voPublicIndex ? `#${record.voPublicIndex}` : '-' },
    { title: t('users.list.column.createdAt'), dataIndex: 'voCreateTime', key: 'voCreateTime', width: 180, render: (value: string) => formatUserTime(value, language) },
    { title: t('users.list.column.updatedAt'), dataIndex: 'voUpdateTime', key: 'voUpdateTime', width: 180, render: (value?: string | null) => formatUserTime(value, language) },
    {
      title: t('users.list.column.action'),
      key: 'actions',
      width: 140,
      render: (_, record) => <Button size="small" onClick={() => openDetail(record.uuid)}>{t('users.list.action.viewDetail')}</Button>,
    },
  ];

  const filterControls = (
    <div className="console-resource-filter-controls user-list-filter-controls">
      <Input
        allowClear
        placeholder={t('users.list.filter.keyword')}
        prefix={<SearchOutlined />}
        value={keywordDraft}
        onChange={(event) => setKeywordDraft(event.target.value)}
        onPressEnter={handleApplyFilters}
      />
      <Select
        value={statusDraft}
        onChange={(value) => setStatusDraft(value)}
        options={[
          { label: t('users.list.status.all'), value: 'all' },
          { label: t('users.common.enabled'), value: 'enabled' },
          { label: t('users.common.disabled'), value: 'disabled' },
        ]}
      />
      <Input
        allowClear
        placeholder={t('users.list.filter.role')}
        value={roleDraft}
        onChange={(event) => setRoleDraft(event.target.value)}
        onPressEnter={handleApplyFilters}
      />
      <div className="console-resource-filter-controls__actions">
        <Button onClick={handleResetFilters}>{t('users.list.reset')}</Button>
        <Button variant="primary" onClick={handleApplyFilters}>{t('users.list.search')}</Button>
      </div>
    </div>
  );

  const readNotice = readState === 'stale' || readState === 'unavailable' ? (
    <div className={`console-resource-list-notice console-resource-list-notice--${readState}`} role="alert">
      <div>
        <strong>{t(readState === 'stale' ? 'users.list.staleTitle' : 'users.list.unavailableTitle')}</strong>
        <span>{t(readState === 'stale' ? 'users.list.staleDescription' : 'users.list.unavailableDescription')}</span>
      </div>
      <Button size="small" onClick={() => void loadUsers()}>{t('users.list.retry')}</Button>
    </div>
  ) : null;

  return (
    <div className="admin-feature-page user-list">
      <ConsolePageHeader
        eyebrow={t('users.list.eyebrow')}
        title={t('users.list.title')}
        description={t('users.list.description')}
        icon={<TeamOutlined />}
        status={<ConsoleStatusChip tone={canViewUsers ? 'success' : 'danger'}>{t(canViewUsers ? 'users.common.viewable' : 'users.common.noPermission')}</ConsoleStatusChip>}
        actions={<Button icon={<ReloadOutlined />} disabled={!canViewUsers} onClick={() => void loadUsers()}>{t('users.list.retry')}</Button>}
      />

      <ConsoleMetricGrid label={t('users.list.metrics.label')}>
        <ConsoleMetricCard label={t('users.list.metrics.results')} value={formatConsoleNumber(total, language)} description={t('users.list.metrics.resultsDescription')} tone="info" />
        <ConsoleMetricCard label={t('users.list.metrics.page')} value={formatConsoleNumber(users.length, language)} description={t('users.list.metrics.pageDescription')} />
        <ConsoleMetricCard label={t('users.list.metrics.enabled')} value={formatConsoleNumber(enabledUsers, language)} description={t('users.list.metrics.enabledDescription')} tone="success" />
        <ConsoleMetricCard label={t('users.list.metrics.disabled')} value={formatConsoleNumber(disabledUsers, language)} description={t('users.list.metrics.disabledDescription')} tone="warning" />
      </ConsoleMetricGrid>

      <ConsoleResourceList
        toolbar={<ConsoleToolbar title={t('users.list.toolbar.title')} description={t('users.list.toolbar.description')} meta={<ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>{activeFilterCount > 0 ? t('users.list.filterCount', { count: activeFilterCount }) : t('users.list.noFilters')}</ConsoleStatusChip>}>{filterControls}</ConsoleToolbar>}
        mobileToolbar={(
          <div className="console-resource-mobile-summary">
            <div className="console-resource-mobile-summary__copy">
              <strong>{t('users.list.mobile.results', { count: total })}</strong>
              <span>{activeFilterCount > 0 ? t('users.list.filterCount', { count: activeFilterCount }) : t('users.list.noFilters')}</span>
            </div>
            <div className="console-resource-mobile-summary__actions">
              <Button size="small" icon={<SearchOutlined />} onClick={() => setFilterSheetOpen(true)}>{t('users.list.mobile.filter')}</Button>
              <Button size="small" icon={<ReloadOutlined />} onClick={() => void loadUsers()}>{t('users.list.retry')}</Button>
            </div>
          </div>
        )}
        desktopList={(
          <section className="admin-table-panel">
            {readNotice}
            <Table<UserListItem>
              rowKey="uuid"
              columns={columns}
              dataSource={users}
              loading={loading}
              pagination={{
                current: query.pageIndex,
                pageSize: query.pageSize,
                total,
                showSizeChanger: true,
                showQuickJumper: true,
                showTotal: (count) => t('users.list.accountCount', { count }),
                onChange: handlePageChange,
              }}
              scroll={{ x: 1300 }}
            />
          </section>
        )}
        mobileList={(
          <>
            {readNotice}
            {loading && users.length === 0 ? <div className="console-resource-mobile-loading">{t('users.list.mobile.loading')}</div> : null}
            {readState === 'ready' && users.length === 0 ? <div className="console-resource-mobile-empty"><strong>{t('users.list.mobile.emptyTitle')}</strong><span>{t('users.list.mobile.emptyDescription')}</span></div> : null}
            {users.map((record) => (
              <article className="console-resource-mobile-card user-list-mobile-card" key={record.uuid}>
                <div className="console-resource-mobile-card__header">
                  {renderIdentity(record)}
                  <Tag color={record.voIsEnable ? 'success' : 'error'}>{t(record.voIsEnable ? 'users.common.enabled' : 'users.common.disabled')}</Tag>
                </div>
                <div className="console-resource-mobile-card__facts">
                  <div className="console-resource-mobile-card__fact"><span>{t('users.list.column.publicIndex')}</span><strong>{record.voPublicIndex ? `#${record.voPublicIndex}` : '-'}</strong></div>
                  <div className="console-resource-mobile-card__fact"><span>{t('users.list.column.roles')}</span><strong>{record.voRoleNames.join(', ') || t('users.list.noRoles')}</strong></div>
                  <div className="console-resource-mobile-card__fact"><span>{t('users.list.column.updatedAt')}</span><strong>{formatUserTime(record.voUpdateTime, language)}</strong></div>
                </div>
                <div className="console-resource-mobile-card__footer">
                  <span className="admin-feature-subtle">#{record.uuid}</span>
                  <Button size="small" onClick={() => openDetail(record.uuid)}>{t('users.list.action.viewDetail')}</Button>
                </div>
              </article>
            ))}
            {users.length > 0 ? (
              <div className="console-resource-mobile-pagination">
                <Button size="small" disabled={query.pageIndex <= 1 || loading} onClick={() => handlePageChange(query.pageIndex - 1)}>{t('users.list.mobile.previous')}</Button>
                <span>{t('users.list.mobile.page', { current: query.pageIndex, total: pageCount })}</span>
                <Button size="small" disabled={query.pageIndex >= pageCount || loading} onClick={() => handlePageChange(query.pageIndex + 1)}>{t('users.list.mobile.next')}</Button>
              </div>
            ) : null}
          </>
        )}
        context={(
          <>
            <h3>{t('users.list.summary.title')}</h3>
            <p className="admin-feature-subtle">{t('users.list.summary.description')}</p>
            <div className="admin-table-summary">
              <div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('users.list.summary.scope')}</span><span className="admin-table-summary__value">{activeFilterCount > 0 ? t('users.list.filterCount', { count: activeFilterCount }) : t('users.list.summary.allUsers')}</span></div>
              <div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('users.list.summary.pagination')}</span><span className="admin-table-summary__value">{t('users.list.summary.paginationValue', { pageSize: query.pageSize, page: query.pageIndex })}</span></div>
              <div className="admin-table-summary__item"><span className="admin-table-summary__label">{t('users.list.summary.statusQueue')}</span><span className="admin-table-summary__value">{disabledUsers > 0 ? t('users.list.summary.disabledAttention', { count: disabledUsers }) : t('users.list.summary.noDisabled')}</span></div>
            </div>
            {selectedUser ? (
              <div className="admin-feature-inline-context">
                <strong>{resolveVisibleUserDisplayName(selectedUser, t('users.common.userFallback', { id: selectedUser.uuid }))}</strong>
                <span>{selectedUser.voUserEmail}</span>
                <Space wrap><Button size="small" onClick={() => openDetail(selectedUser.uuid)}>{t('users.list.summary.openDetail')}</Button></Space>
              </div>
            ) : <p className="admin-feature-rail__empty">{t('users.list.summary.empty')}</p>}
          </>
        )}
      />

      <BottomSheet
        isOpen={filterSheetOpen}
        onClose={() => setFilterSheetOpen(false)}
        closeLabel={t('users.list.mobile.closeFilters')}
        title={t('users.list.toolbar.title')}
        height="auto"
        className="console-resource-filter-sheet"
      >
        {filterControls}
      </BottomSheet>
    </div>
  );
};
