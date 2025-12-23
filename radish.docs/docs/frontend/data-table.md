# DataTable 组件

## 概述

`DataTable` 是 `@radish/ui` 提供的统一数据表格组件，基于 Ant Design Table 封装，支持普通数组和分页数据两种数据源，自动处理分页、loading 和 empty 状态。

## 特性

- ✅ 支持普通数组和分页数据（`PagedResponse<T>`）
- ✅ 自动识别并配置分页
- ✅ 统一的 loading 状态
- ✅ 自定义 empty 状态
- ✅ 完整的 TypeScript 类型支持
- ✅ 继承 Ant Design Table 所有功能

## 安装

`DataTable` 已包含在 `@radish/ui` 中：

```bash
npm install  # 在项目根目录
```

## 快速开始

### 基础用法

```typescript
import { DataTable } from '@radish/ui';
import type { TableColumnsType } from '@radish/ui';

interface User {
  id: string;
  name: string;
  email: string;
  role: string;
}

const columns: TableColumnsType<User> = [
  {
    title: 'ID',
    dataIndex: 'id',
    key: 'id',
  },
  {
    title: '姓名',
    dataIndex: 'name',
    key: 'name',
  },
  {
    title: '邮箱',
    dataIndex: 'email',
    key: 'email',
  },
  {
    title: '角色',
    dataIndex: 'role',
    key: 'role',
  },
];

export function UserList() {
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(false);

  return (
    <DataTable
      columns={columns}
      dataSource={users}
      loading={loading}
      rowKey="id"
    />
  );
}
```

### 分页数据

```typescript
import { DataTable } from '@radish/ui';
import type { PagedResponse } from '@radish/ui';

export function UserList() {
  const [pagedData, setPagedData] = useState<PagedResponse<User>>();
  const [loading, setLoading] = useState(false);

  const loadUsers = async (page: number, pageSize: number) => {
    setLoading(true);
    try {
      const result = await userApi.getUsers({ page, pageSize });
      if (result.ok && result.data) {
        setPagedData(result.data);
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadUsers(1, 20);
  }, []);

  return (
    <DataTable
      columns={columns}
      dataSource={pagedData}
      loading={loading}
      onPageChange={(page, pageSize) => loadUsers(page, pageSize)}
      rowKey="id"
    />
  );
}
```

## API

### DataTableProps

继承自 Ant Design `TableProps`，并扩展以下属性：

| 属性 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `columns` | `TableColumnsType<T>` | - | 列配置（必需） |
| `dataSource` | `T[]` \| `PagedResponse<T>` | - | 数据源（普通数组或分页数据） |
| `loading` | `boolean` | `false` | 是否加载中 |
| `emptyText` | `string` | `"暂无数据"` | 空状态描述 |
| `showPagination` | `boolean` | `true` | 是否显示分页 |
| `currentPage` | `number` | - | 当前页码（受控模式） |
| `pageSize` | `number` | - | 每页数量（受控模式） |
| `onPageChange` | `(page: number, pageSize: number) => void` | - | 分页变化回调 |
| `rowKey` | `string` \| `(record: T) => string` | `"id"` | 行的唯一键字段名 |

### PagedResponse

```typescript
interface PagedResponse<T> {
  page: number;       // 当前页码
  pageSize: number;   // 每页数量
  dataCount: number;  // 总数据量
  pageCount: number;  // 总页数
  data: T[];          // 数据列表
}
```

## 完整示例

### Console 应用示例

```typescript
// src/pages/Applications/Applications.tsx
import { useState, useEffect } from 'react';
import {
  DataTable,
  AntButton,
  Space,
  Tag,
  Popconfirm,
  message,
} from '@radish/ui';
import type { TableColumnsType } from '@radish/ui';
import {
  EditOutlined,
  DeleteOutlined,
  PlusOutlined,
  ReloadOutlined,
} from '@radish/ui';
import { clientApi } from '../../api/clients';
import type { OidcClient } from '../../types/oidc';

export const Applications = () => {
  const [clients, setClients] = useState<OidcClient[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    void loadClients();
  }, []);

  const loadClients = async () => {
    setLoading(true);
    try {
      const result = await clientApi.getClients({ page: 1, pageSize: 100 });
      if (result.ok && result.data) {
        setClients(result.data.data);
      } else {
        message.error(result.message || '加载失败');
      }
    } catch (error) {
      message.error('加载失败');
      console.error(error);
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id: string) => {
    const result = await clientApi.deleteClient(id);
    if (result.ok) {
      message.success('删除成功');
      await loadClients();
    }
  };

  const columns: TableColumnsType<OidcClient> = [
    {
      title: '客户端 ID',
      dataIndex: 'clientId',
      key: 'clientId',
      width: 200,
    },
    {
      title: '显示名称',
      dataIndex: 'displayName',
      key: 'displayName',
    },
    {
      title: '类型',
      dataIndex: 'type',
      key: 'type',
      render: (type: string) => (
        <Tag color={type === 'public' ? 'green' : 'blue'}>
          {type === 'public' ? '公开' : '机密'}
        </Tag>
      ),
    },
    {
      title: '授权类型',
      dataIndex: 'grantTypes',
      key: 'grantTypes',
    },
    {
      title: '操作',
      key: 'action',
      fixed: 'right',
      width: 180,
      render: (_, record) => (
        <Space size="small">
          <AntButton
            type="link"
            icon={<EditOutlined />}
            onClick={() => handleEdit(record)}
          >
            编辑
          </AntButton>
          <Popconfirm
            title="确定要删除这个客户端吗？"
            onConfirm={() => handleDelete(record.id)}
            okText="确定"
            cancelText="取消"
          >
            <AntButton
              type="link"
              danger
              icon={<DeleteOutlined />}
            >
              删除
            </AntButton>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <Space>
          <AntButton
            type="primary"
            icon={<PlusOutlined />}
            onClick={handleCreate}
          >
            新建客户端
          </AntButton>
          <AntButton
            icon={<ReloadOutlined />}
            onClick={() => void loadClients()}
          >
            刷新
          </AntButton>
        </Space>
      </div>

      <DataTable
        columns={columns}
        dataSource={clients}
        loading={loading}
        rowKey="id"
        scroll={{ x: 1200 }}
      />
    </div>
  );
};
```

### 分页表格示例

```typescript
import { useState, useEffect } from 'react';
import { DataTable, message } from '@radish/ui';
import type { PagedResponse, TableColumnsType } from '@radish/ui';
import { userApi } from './api/users';
import type { User } from './types/user';

export function UserList() {
  const [pagedData, setPagedData] = useState<PagedResponse<User>>();
  const [loading, setLoading] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const loadUsers = async (page: number, size: number) => {
    setLoading(true);
    try {
      const result = await userApi.getUsers({ page, pageSize: size });
      if (result.ok && result.data) {
        setPagedData(result.data);
        setCurrentPage(page);
        setPageSize(size);
      } else {
        message.error(result.message || '加载失败');
      }
    } catch (error) {
      message.error('加载失败');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadUsers(1, 20);
  }, []);

  const columns: TableColumnsType<User> = [
    {
      title: 'ID',
      dataIndex: 'id',
      key: 'id',
      width: 80,
    },
    {
      title: '用户名',
      dataIndex: 'username',
      key: 'username',
    },
    {
      title: '邮箱',
      dataIndex: 'email',
      key: 'email',
    },
    {
      title: '角色',
      dataIndex: 'role',
      key: 'role',
    },
  ];

  return (
    <DataTable
      columns={columns}
      dataSource={pagedData}
      loading={loading}
      currentPage={currentPage}
      pageSize={pageSize}
      onPageChange={loadUsers}
      rowKey="id"
    />
  );
}
```

### 自定义渲染

```typescript
import { DataTable, Tag, Avatar, Space, AntButton } from '@radish/ui';
import type { TableColumnsType } from '@radish/ui';

const columns: TableColumnsType<User> = [
  {
    title: '用户',
    key: 'user',
    render: (_, record) => (
      <Space>
        <Avatar src={record.avatar}>{record.name[0]}</Avatar>
        <div>
          <div>{record.name}</div>
          <div style={{ color: '#999', fontSize: 12 }}>{record.email}</div>
        </div>
      </Space>
    ),
  },
  {
    title: '角色',
    dataIndex: 'roles',
    key: 'roles',
    render: (roles: string[]) => (
      <>
        {roles.map(role => (
          <Tag key={role} color="blue">{role}</Tag>
        ))}
      </>
    ),
  },
  {
    title: '状态',
    dataIndex: 'status',
    key: 'status',
    render: (status: string) => {
      const colorMap: Record<string, string> = {
        active: 'green',
        inactive: 'red',
        pending: 'orange',
      };
      return <Tag color={colorMap[status]}>{status}</Tag>;
    },
  },
  {
    title: '操作',
    key: 'action',
    fixed: 'right',
    render: (_, record) => (
      <Space size="small">
        <AntButton type="link">查看</AntButton>
        <AntButton type="link">编辑</AntButton>
        <AntButton type="link" danger>删除</AntButton>
      </Space>
    ),
  },
];
```

## 高级用法

### 行选择

```typescript
import { useState } from 'react';
import { DataTable, message } from '@radish/ui';

export function SelectableTable() {
  const [selectedRowKeys, setSelectedRowKeys] = useState<React.Key[]>([]);

  const rowSelection = {
    selectedRowKeys,
    onChange: (newSelectedRowKeys: React.Key[]) => {
      setSelectedRowKeys(newSelectedRowKeys);
    },
  };

  const handleBatchDelete = async () => {
    if (selectedRowKeys.length === 0) {
      message.warning('请选择要删除的行');
      return;
    }

    // 批量删除逻辑
    // await batchDeleteUsers(selectedRowKeys as string[]);
    message.success(`已删除 ${selectedRowKeys.length} 条记录`);
    setSelectedRowKeys([]);
  };

  return (
    <div>
      <div style={{ marginBottom: 16 }}>
        <AntButton
          danger
          disabled={selectedRowKeys.length === 0}
          onClick={handleBatchDelete}
        >
          批量删除 ({selectedRowKeys.length})
        </AntButton>
      </div>

      <DataTable
        columns={columns}
        dataSource={users}
        rowSelection={rowSelection}
        rowKey="id"
      />
    </div>
  );
}
```

### 自定义分页

```typescript
<DataTable
  columns={columns}
  dataSource={pagedData}
  onPageChange={(page, pageSize) => {
    loadUsers(page, pageSize);
  }}
  rowKey="id"
  pagination={{
    showSizeChanger: true,
    showQuickJumper: true,
    showTotal: (total) => `共 ${total} 条记录`,
    pageSizeOptions: ['10', '20', '50', '100'],
  }}
/>
```

### 禁用分页

```typescript
<DataTable
  columns={columns}
  dataSource={users}
  showPagination={false}
  rowKey="id"
/>
```

### 固定列和表头

```typescript
<DataTable
  columns={columns}
  dataSource={users}
  rowKey="id"
  scroll={{
    x: 1500,  // 横向滚动
    y: 500,   // 固定表头
  }}
/>
```

## 最佳实践

### 1. 使用 rowKey

**推荐：** 始终指定 `rowKey`

```typescript
// ✅ 指定 rowKey
<DataTable
  columns={columns}
  dataSource={users}
  rowKey="id"
/>

// ✅ 使用函数
<DataTable
  columns={columns}
  dataSource={users}
  rowKey={(record) => record.userId}
/>
```

**不推荐：** 省略 rowKey

```typescript
// ❌ 可能导致渲染问题
<DataTable
  columns={columns}
  dataSource={users}
/>
```

### 2. 分页数据处理

**推荐：** 使用 `PagedResponse<T>` 类型

```typescript
// ✅ 类型安全
const [pagedData, setPagedData] = useState<PagedResponse<User>>();

<DataTable
  dataSource={pagedData}
  onPageChange={(page, pageSize) => loadUsers(page, pageSize)}
/>
```

### 3. Loading 状态

**推荐：** 始终管理 loading 状态

```typescript
// ✅ 提供 loading 状态
const [loading, setLoading] = useState(false);

const loadUsers = async () => {
  setLoading(true);
  try {
    // ...
  } finally {
    setLoading(false);
  }
};

<DataTable
  dataSource={users}
  loading={loading}
/>
```

### 4. 列宽度

**推荐：** 为重要列指定宽度

```typescript
// ✅ 指定列宽度
const columns: TableColumnsType<User> = [
  {
    title: 'ID',
    dataIndex: 'id',
    key: 'id',
    width: 80,  // 固定宽度
  },
  {
    title: '操作',
    key: 'action',
    fixed: 'right',
    width: 180,
  },
];
```

## 常见问题

### Q: 如何自定义空状态？

**A:** 使用 `emptyText` 属性：

```typescript
<DataTable
  columns={columns}
  dataSource={users}
  emptyText="没有找到用户"
/>
```

### Q: 如何禁用分页？

**A:** 设置 `showPagination={false}`：

```typescript
<DataTable
  columns={columns}
  dataSource={users}
  showPagination={false}
/>
```

### Q: 如何自定义分页配置？

**A:** 直接传递 Ant Design 的 `pagination` 属性（会覆盖默认配置）：

```typescript
<DataTable
  columns={columns}
  dataSource={users}
  pagination={{
    position: ['bottomCenter'],
    showSizeChanger: false,
    pageSize: 50,
  }}
/>
```

### Q: 为什么分页不起作用？

**A:** 确保：
1. `dataSource` 是 `PagedResponse<T>` 类型
2. 提供了 `onPageChange` 回调
3. 在回调中正确加载新数据

### Q: 如何实现表格排序？

**A:** 在列配置中添加 `sorter`：

```typescript
const columns: TableColumnsType<User> = [
  {
    title: '姓名',
    dataIndex: 'name',
    key: 'name',
    sorter: (a, b) => a.name.localeCompare(b.name),
  },
];
```

## 相关文档

- [UI 组件库概览](./ui-library.md)
- [API 客户端使用指南](./api-client.md)
- [Ant Design Table 文档](https://ant.design/components/table-cn)
