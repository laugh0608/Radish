import { useState, useEffect } from 'react';
import {
  Table,
  Button,
  AntInput as Input,
  AntSelect as Select,
  Space,
  Tag,
  Avatar,
  message,
  Modal,
  Popconfirm,
  type TableColumnsType,
} from '@radish/ui';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  SearchOutlined,
  ReloadOutlined,
  UserOutlined,
  LockOutlined,
  UnlockOutlined,
  KeyOutlined,
  LogoutOutlined,
} from '@radish/ui';
import {
  userManagementApi,
  getUserStatusDisplay,
  getUserStatusColor,
  UserStatus,
  type UserInfo,
  type UserListParams,
} from '../../api/userManagement';
import { log } from '../../utils/logger';
import './UserList.css';

export const UserList = () => {
  const [users, setUsers] = useState<UserInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [total, setTotal] = useState(0);
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  // 筛选条件
  const [keyword, setKeyword] = useState<string>('');
  const [status, setStatus] = useState<UserStatus | undefined>();
  const [role, setRole] = useState<string | undefined>();

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
        message.error(response.message || '获取���户列表失败');
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
  };

  // 更新用户状态
  const handleUpdateStatus = async (id: number, newStatus: UserStatus) => {
    try {
      const response = await userManagementApi.updateUserStatus(id, newStatus);

      if (response.ok) {
        message.success('用户状态更新成功');
        loadUsers();
      } else {
        message.error(response.message || '更新用户状态失败');
      }
    } catch (error) {
      log.error('UserList', '更新用户状态失败', error);
      message.error('更新用户状态失败');
    }
  };

  // 删除用户
  const handleDeleteUser = async (id: number) => {
    try {
      const response = await userManagementApi.deleteUser(id);

      if (response.ok) {
        message.success('用户删除成功');
        loadUsers();
      } else {
        message.error(response.message || '删除用户失败');
      }
    } catch (error) {
      log.error('UserList', '删除用户失败', error);
      message.error('删除用户失败');
    }
  };

  // 强制用户下线
  const handleForceLogout = async (id: number) => {
    try {
      const response = await userManagementApi.forceLogout(id);

      if (response.ok) {
        message.success('用户已强制下线');
      } else {
        message.error(response.message || '强制下线失败');
      }
    } catch (error) {
      log.error('UserList', '强制下线失败', error);
      message.error('强制下线失败');
    }
  };

  // 重置密码
  const handleResetPassword = (user: UserInfo) => {
    Modal.confirm({
      title: '重置密码',
      content: `确定要重置用户 "${user.userName}" 的密码吗？新密码将通过邮件发送给用户。`,
      onOk: async () => {
        try {
          // 生成临时密码
          const tempPassword = Math.random().toString(36).slice(-8);
          const response = await userManagementApi.resetPassword(user.id, tempPassword);

          if (response.ok) {
            message.success('密码重置成功，新密码已发送给用户');
          } else {
            message.error(response.message || '重置密码失败');
          }
        } catch (error) {
          log.error('UserList', '重置密码失败', error);
          message.error('重置密码失败');
        }
      },
    });
  };

  // 表格列定义
  const columns: TableColumnsType<UserInfo> = [
    {
      title: '用户',
      key: 'user',
      width: 200,
      render: (_, record) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <Avatar
            size="small"
            src={record.avatar}
            icon={<UserOutlined />}
          />
          <div>
            <div style={{ fontWeight: 500 }}>{record.userName}</div>
            {record.email && (
              <div style={{ fontSize: '12px', color: '#666' }}>{record.email}</div>
            )}
          </div>
        </div>
      ),
    },
    {
      title: '状态',
      dataIndex: 'status',
      key: 'status',
      width: 100,
      render: (status: UserStatus) => (
        <Tag color={getUserStatusColor(status)}>
          {getUserStatusDisplay(status)}
        </Tag>
      ),
    },
    {
      title: '角色',
      dataIndex: 'roles',
      key: 'roles',
      width: 150,
      render: (roles: string[]) => (
        <div>
          {roles.map((role) => (
            <Tag key={role} size="small">
              {role}
            </Tag>
          ))}
        </div>
      ),
    },
    {
      title: '登录次数',
      dataIndex: 'loginCount',
      key: 'loginCount',
      width: 100,
      align: 'center',
    },
    {
      title: '最后登录',
      dataIndex: 'lastLoginAt',
      key: 'lastLoginAt',
      width: 150,
      render: (lastLoginAt: string) =>
        lastLoginAt ? new Date(lastLoginAt).toLocaleString() : '从未登录',
    },
    {
      title: '创建时间',
      dataIndex: 'createdAt',
      key: 'createdAt',
      width: 150,
      render: (createdAt: string) => new Date(createdAt).toLocaleString(),
    },
    {
      title: '操作',
      key: 'actions',
      width: 200,
      fixed: 'right',
      render: (_, record) => (
        <Space size="small">
          <Button
            type="link"
            size="small"
            icon={<EditOutlined />}
            onClick={() => {
              // TODO: 打开编辑用户弹窗
              message.info('编辑用户功能待实现');
            }}
          >
            编辑
          </Button>

          {record.status === UserStatus.Normal ? (
            <Button
              type="link"
              size="small"
              icon={<LockOutlined />}
              onClick={() => handleUpdateStatus(record.id, UserStatus.Disabled)}
            >
              禁用
            </Button>
          ) : (
            <Button
              type="link"
              size="small"
              icon={<UnlockOutlined />}
              onClick={() => handleUpdateStatus(record.id, UserStatus.Normal)}
            >
              启用
            </Button>
          )}

          <Button
            type="link"
            size="small"
            icon={<KeyOutlined />}
            onClick={() => handleResetPassword(record)}
          >
            重置密码
          </Button>

          <Button
            type="link"
            size="small"
            icon={<LogoutOutlined />}
            onClick={() => handleForceLogout(record.id)}
          >
            强制下线
          </Button>

          <Popconfirm
            title="确定要删除这个用户吗？"
            description="删除后无法恢复，请谨慎操作。"
            onConfirm={() => handleDeleteUser(record.id)}
            okText="确定"
            cancelText="取消"
          >
            <Button
              type="link"
              size="small"
              danger
              icon={<DeleteOutlined />}
            >
              删除
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  // 初始化加载
  useEffect(() => {
    loadUsers();
  }, [pageIndex, pageSize]);

  // 重置时重新加载
  useEffect(() => {
    if (!keyword && status === undefined && role === undefined) {
      loadUsers();
    }
  }, [keyword, status, role]);

  return (
    <div className="user-list">
      {/* 筛选条件 */}
      <div className="user-list-filters">
        <Space wrap>
          <Input
            placeholder="搜索用户名或邮箱"
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            onPressEnter={handleSearch}
            style={{ width: 200 }}
            allowClear
          />

          <Select
            placeholder="用户状态"
            value={status}
            onChange={setStatus}
            style={{ width: 120 }}
            allowClear
          >
            <Select.Option value={UserStatus.Normal}>正常</Select.Option>
            <Select.Option value={UserStatus.Disabled}>禁用</Select.Option>
            <Select.Option value={UserStatus.Locked}>锁定</Select.Option>
          </Select>

          <Select
            placeholder="用户角色"
            value={role}
            onChange={setRole}
            style={{ width: 120 }}
            allowClear
          >
            <Select.Option value="System">系统管理员</Select.Option>
            <Select.Option value="Admin">管理员</Select.Option>
            <Select.Option value="User">普通用户</Select.Option>
          </Select>

          <Button
            type="primary"
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

          <Button
            type="primary"
            icon={<PlusOutlined />}
            onClick={() => {
              // TODO: 打开新增用户弹窗
              message.info('新增用户功能待实现');
            }}
          >
            新增用户
          </Button>
        </Space>
      </div>

      {/* 用户列表表格 */}
      <Table
        columns={columns}
        dataSource={users}
        rowKey="id"
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
          },
        }}
        scroll={{ x: 1200 }}
      />
    </div>
  );
};