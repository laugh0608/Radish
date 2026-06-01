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
  UserStatus,
  type UserListParams,
} from '../../api/userManagement';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import type { UserListItem } from '../../types/user';
import { getAvatarUrl } from '@/config/env';
import { log } from '../../utils/logger';
import '../adminFeature.css';
import './UserList.css';

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

  // 加载用户列表
  const loadUsers = async () => {
    try {
      setLoading(true);

      const params: UserListParams = {
        pageIndex,
        pageSize,
        keyword: keyword || undefined,
        status,
        role,
      };

      const response = await userManagementApi.getUserList(params);

      if (response.ok && response.data) {
        setUsers(response.data.items);
        setTotal(response.data.total);
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
    loadUsers();
  };

  // 重置筛选条件
  const handleReset = () => {
    setKeyword('');
    setStatus(undefined);
    setRole(undefined);
    setPageIndex(1);
    // 重置后重新加载
    setTimeout(() => loadUsers(), 0);
  };

  // 表格列定义
  const columns: TableColumnsType<UserListItem> = [
    {
      title: '用户',
      key: 'user',
      width: 260,
      render: (_, record) => (
        <div className="user-list-identity">
          <Avatar
            size="small"
            src={getAvatarUrl(record.voAvatarThumbnailUrl || record.voAvatarUrl)}
            icon={<UserOutlined />}
          />
          <div className="user-list-identity__content">
            <div className="user-list-identity__name">{record.voUserName}</div>
            {record.voUserEmail && (
              <div className="user-list-identity__email">{record.voUserEmail}</div>
            )}
          </div>
        </div>
      ),
    },
    {
      title: '登录名',
      dataIndex: 'voLoginName',
      key: 'voLoginName',
      width: 180,
    },
    {
      title: '状态',
      key: 'status',
      width: 100,
      render: (_, record) => (
        <Tag color={record.voIsEnable ? 'success' : 'error'}>
          {record.voIsEnable ? '启用' : '禁用'}
        </Tag>
      ),
    },
    {
      title: '创建时间',
      dataIndex: 'voCreateTime',
      key: 'voCreateTime',
      width: 180,
      render: (voCreateTime: string) => new Date(voCreateTime).toLocaleString(),
    },
    {
      title: '操作',
      key: 'actions',
      width: 160,
      render: (_, record) => (
        <Space size="small">
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
  }, [canViewUsers]);
  return (
    <div className="admin-feature-page user-list">
      <section className="admin-feature-card">
        <div className="admin-feature-header">
          <div>
            <h2>
              <TeamOutlined /> 用户管理
            </h2>
            <p className="admin-feature-subtle">查看账号状态、定位用户详情，并为后续治理动作提供入口。</p>
          </div>
          <Tag>{canViewUsers ? '可查看' : '无权限'}</Tag>
        </div>
      </section>

      <section className="admin-feature-metrics" aria-label="用户列表指标">
        <div className="admin-feature-metric">
          当前结果
          <strong>{total}</strong>
        </div>
        <div className="admin-feature-metric">
          本页账号
          <strong>{users.length}</strong>
        </div>
        <div className="admin-feature-metric">
          本页启用
          <strong>{enabledUsers}</strong>
        </div>
      </section>

      <div className="admin-table-layout">
        <main className="admin-table-main">
          <section className="admin-table-toolbar" aria-label="用户筛选">
            <div className="admin-table-toolbar__title">
              <span>筛选用户</span>
              <Tag>{activeFilterCount > 0 ? `${activeFilterCount} 个条件` : '未筛选'}</Tag>
            </div>
            <div className="admin-table-toolbar__filters">
              <Input
                className="user-list-filter-input"
                placeholder="搜索用户名或邮箱"
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
          </section>

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
                  setPageIndex(page);
                  setPageSize(size || 20);
                  // 分页变化后重新加载
                  setTimeout(() => loadUsers(), 0);
                },
              }}
              scroll={{ x: 880 }}
            />
          </section>
        </main>

        <aside className="admin-table-aside">
          <h3>列表摘要</h3>
          <p className="admin-feature-subtle">用于核对当前查询范围和后续对象治理入口。</p>
          <div className="admin-table-summary">
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">查询范围</span>
              <span className="admin-table-summary__value">
                {activeFilterCount > 0 ? `${activeFilterCount} 个筛选条件` : '全部用户'}
              </span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">分页规模</span>
              <span className="admin-table-summary__value">{pageSize} 条 / 页</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">当前页</span>
              <span className="admin-table-summary__value">第 {pageIndex} 页</span>
            </div>
            <div className="admin-table-summary__item">
              <span className="admin-table-summary__label">可执行动作</span>
              <span className="admin-table-summary__value">
                {canViewUsers ? '查看用户详情' : '无用户查看权限'}
              </span>
            </div>
          </div>
        </aside>
      </div>
    </div>
  );
};
