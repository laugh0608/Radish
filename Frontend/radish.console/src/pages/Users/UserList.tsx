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
} from '@radish/ui';
import {
  userManagementApi,
  UserStatus,
  type UserListParams,
} from '../../api/userManagement';
import { CONSOLE_PERMISSIONS } from '@/constants/permissions';
import { usePermission } from '@/hooks/usePermission';
import type { UserListItem } from '../../types/user';
import { log } from '../../utils/logger';
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
    // 重置后重新加载
    setTimeout(() => loadUsers(), 0);
  };

  // 表格列定义
  const columns: TableColumnsType<UserListItem> = [
    {
      title: '用户',
      key: 'user',
      width: 200,
      render: (_, record) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <Avatar
            size="small"
            icon={<UserOutlined />}
          />
          <div>
            <div style={{ fontWeight: 500 }}>{record.voUserName}</div>
            {record.voUserEmail && (
              <div style={{ fontSize: '12px', color: '#666' }}>{record.voUserEmail}</div>
            )}
          </div>
        </div>
      ),
    },
    {
      title: '登录名',
      dataIndex: 'voLoginName',
      key: 'voLoginName',
      width: 150,
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
      width: 150,
      render: (voCreateTime: string) => new Date(voCreateTime).toLocaleString(),
    },
    {
      title: '操作',
      key: 'actions',
      width: 200,
      fixed: 'right',
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
        </Space>
      </div>

      {/* 用户列表表格 */}
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
        scroll={{ x: 1200 }}
      />
    </div>
  );
};
