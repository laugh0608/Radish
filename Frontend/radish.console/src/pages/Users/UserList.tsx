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
} from '@radish/ui';
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
  getUserStatusDisplay,
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

function formatUserTime(value?: string | null) {
  if (!value) {
    return '-';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString('zh-CN');
}

function getListItemStatus(user: UserListItem) {
  return user.voIsEnable ? UserStatus.Normal : UserStatus.Disabled;
}

export const UserList = () => {
  useDocumentTitle('用户管理');
  const navigate = useNavigate();
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [total, setTotal] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const canViewUsers = usePermission(CONSOLE_PERMISSIONS.usersView);

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
    ? resolveVisibleUserDisplayName(selectedUser, selectedUser.uuid ? `用户 ${selectedUser.uuid}` : '未知用户')
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
        message.error(response.message || '获取用户列表失败');
      }
    } catch (error) {
      log.error('UserList', '加载用户列表失败', error);
      message.error('加载用户列表失败');
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
      title: '用户',
      key: 'user',
      width: 260,
      render: (_, record) => (
        (() => {
          const displayName = resolveVisibleUserDisplayName(record, record.uuid ? `用户 ${record.uuid}` : '未知用户');
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
      title: '状态',
      key: 'status',
      width: 100,
      render: (_, record) => {
        const currentStatus = getListItemStatus(record);
        return (
          <Tag color={getUserStatusColor(currentStatus)}>
            {getUserStatusDisplay(currentStatus)}
          </Tag>
        );
      },
    },
    {
      title: '公开索引',
      key: 'publicIndex',
      width: 120,
      render: (_, record) => record.voPublicIndex ? `#${record.voPublicIndex}` : '-',
    },
    {
      title: '创建时间',
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (voCreateTime: string) => formatUserTime(voCreateTime),
    },
    {
      title: '更新时间',
      dataIndex: 'voUpdateTime',
      key: 'voUpdateTime',
      width: 180,
      render: (voUpdateTime?: string | null) => formatUserTime(voUpdateTime),
    },
    {
      title: '操作',
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
              查看详情
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
        eyebrow="用户运营"
        title="用户管理"
        description="查看账号状态、定位用户详情，并为后续治理动作提供入口。"
        icon={<TeamOutlined />}
        status={(
          <ConsoleStatusChip tone={canViewUsers ? 'success' : 'danger'}>
            {canViewUsers ? '可查看' : '无权限'}
          </ConsoleStatusChip>
        )}
      />

      <ConsoleMetricGrid label="用户列表指标">
        <ConsoleMetricCard label="当前结果" value={total} description="当前筛选后的账号数量" tone="info" />
        <ConsoleMetricCard label="本页账号" value={users.length} description="当前页可见账号" />
        <ConsoleMetricCard label="本页启用" value={enabledUsers} description="当前页启用账号" tone="success" />
        <ConsoleMetricCard label="本页禁用" value={disabledUsers} description="当前页禁用账号" tone={disabledUsers > 0 ? 'warning' : 'success'} />
      </ConsoleMetricGrid>

      <section className="governance-task-flow" aria-label="用户对象管理任务流">
        <div className="governance-task-flow__item">
          <span>1</span>
          <strong>查询范围</strong>
          <p>{total} 个匹配账号，当前页 {users.length} 个；关键词、状态和角色筛选先限定对象池。</p>
        </div>
        <div className="governance-task-flow__item">
          <span>2</span>
          <strong>状态检查</strong>
          <p>当前页 {enabledUsers} 个正常、{disabledUsers} 个禁用；账号冻结等深层动作回到治理工作台处理。</p>
        </div>
        <div className="governance-task-flow__item">
          <span>3</span>
          <strong>对象详情</strong>
          <p>{selectedUser ? `${selectedUserName} ${selectedUserHandle ? selectedUserHandle : ''}` : '选择用户后查看详情'}，详情页承接资产、经验和订单回流。</p>
        </div>
        <div className="governance-task-flow__item">
          <span>4</span>
          <strong>审计边界</strong>
          <p>当前列表只呈现账号对象线索；完整审计日志与高风险状态变更不在本批扩展。</p>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <ConsoleToolbar
            title="筛选用户"
            description="按展示名、公开句柄、邮箱、状态或角色定位账号。"
            meta={(
              <ConsoleStatusChip tone={activeFilterCount > 0 ? 'info' : 'neutral'}>
                {activeFilterCount > 0 ? `${activeFilterCount} 个条件` : '未筛选'}
              </ConsoleStatusChip>
            )}
          >
            <div className="admin-table-toolbar__filters">
              <Input
                className="user-list-filter-input"
                placeholder="搜索展示名、公开句柄或邮箱"
                value={keyword}
                onChange={(e) => setKeyword(e.target.value)}
                onPressEnter={handleSearch}
                allowClear
              />

              <Select
                className="user-list-filter-select user-list-filter-select--status"
                placeholder="用户状态"
                value={status}
                onChange={setStatus}
                allowClear
              >
                <Select.Option value={UserStatus.Normal}>正常</Select.Option>
                <Select.Option value={UserStatus.Disabled}>禁用</Select.Option>
                <Select.Option value={UserStatus.Locked}>锁定</Select.Option>
              </Select>

              <Select
                className="user-list-filter-select"
                placeholder="用户角色"
                value={role}
                onChange={setRole}
                allowClear
              >
                <Select.Option value="System">系统管理员</Select.Option>
                <Select.Option value="Admin">管理员</Select.Option>
                <Select.Option value="User">普通用户</Select.Option>
              </Select>

              <Button
                variant="primary"
                icon={<SearchOutlined />}
                onClick={handleSearch}
              >
                搜索
              </Button>

              <Button
                icon={<ReloadOutlined />}
                onClick={handleReset}
              >
                重置
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
                  `第 ${range[0]}-${range[1]} 条，共 ${total} 条`,
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

        <aside className="admin-table-aside" aria-label="用户对象摘要">
          <div className="admin-feature-rail__header">
            <div>
              <span className="admin-feature-rail__eyebrow">当前对象</span>
              <h3>用户对象摘要</h3>
            </div>
            <ConsoleStatusChip tone={canViewUsers ? 'success' : 'danger'}>
              {canViewUsers ? '可查看' : '无权限'}
            </ConsoleStatusChip>
          </div>
          <p className="admin-feature-subtle">用于核对当前查询范围、选中用户和后续对象治理入口。</p>
          <div className="admin-feature-rail__list">
            <div className="admin-feature-rail__item">
              <span>查询范围</span>
              <strong>{activeFilterCount > 0 ? `${activeFilterCount} 个筛选条件` : '全部用户'}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>分页规模</span>
              <strong>{pageSize} 条 / 页，第 {pageIndex} 页</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>状态队列</span>
              <strong>{disabledUsers > 0 ? `${disabledUsers} 个禁用账号需要关注` : '当前页无禁用账号'}</strong>
            </div>
            <div className="admin-feature-rail__item">
              <span>可执行动作</span>
              <strong>{canViewUsers ? '查看用户详情' : '无用户查看权限'}</strong>
            </div>
          </div>
          {selectedUser ? (
            <>
              <div className="admin-feature-rail__callout">
                <span>当前用户</span>
                <strong>{selectedUserName}</strong>
                <p>{selectedUserHandle || selectedUser.voUserEmail || selectedUser.uuid}</p>
                <Space wrap>
                  <Tag color={getUserStatusColor(getListItemStatus(selectedUser))}>
                    {getUserStatusDisplay(getListItemStatus(selectedUser))}
                  </Tag>
                  {selectedUser.voPublicIndex ? <Tag color="blue">#{selectedUser.voPublicIndex}</Tag> : null}
                  {selectedUser.voIsDeleted ? <Tag color="default">已删除</Tag> : null}
                </Space>
              </div>
              <div className="admin-feature-rail__list">
                <div className="admin-feature-rail__item">
                  <span>邮箱</span>
                  <strong>{selectedUser.voUserEmail || '-'}</strong>
                </div>
                <div className="admin-feature-rail__item">
                  <span>创建时间</span>
                  <strong>{formatUserTime(selectedUser.voCreateTime)}</strong>
                </div>
                <div className="admin-feature-rail__item">
                  <span>更新时间</span>
                  <strong>{formatUserTime(selectedUser.voUpdateTime)}</strong>
                </div>
              </div>
              <div className="admin-feature-rail__actions">
                <Button
                  variant="ghost"
                  size="small"
                  icon={<EditOutlined />}
                  onClick={() => navigate(`/users/${selectedUser.uuid}`)}
                >
                  打开用户详情
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
                  设为筛选
                </Button>
              </div>
              <div className="admin-feature-inline-context">
                <strong>权限与风险线索</strong>
                <span>用户状态写入、资产、经验和治理动作继续进入对应专页，不在用户列表直接扩展。</span>
              </div>
            </>
          ) : (
            <p className="admin-feature-rail__empty">当前页暂无用户，调整筛选条件后会形成对象摘要。</p>
          )}
        </aside>
      </div>
    </div>
  );
};
