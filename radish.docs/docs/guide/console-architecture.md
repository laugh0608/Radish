# Console 技术架构

> 本文档是 [Console 管理后台系统设计方案](/guide/console-system) 的第 4 章

## 4. 技术架构

### 4.1 前端架构

#### 4.1.1 技术栈

| 技术 | 版本 | 用途 |
|------|------|------|
| React | 19.1.1 | UI 框架 |
| TypeScript | 5.9.3 | 类型系统 |
| Vite | 7.1.14 (rolldown-vite) | 构建工具 |
| @radish/ui | * | UI 组件库 (基于 Ant Design) |

#### 4.1.2 项目结构

```
radish.console/
├── src/
│   ├── api/                    # API 客户端
│   │   └── clients.ts          # OIDC 客户端 API
│   ├── components/             # 通用组件
│   │   └── AdminLayout/        # 后台布局组件
│   ├── pages/                  # 页面组件
│   │   ├── Dashboard/          # 仪表盘
│   │   ├── Applications/       # 应用管理
│   │   ├── Login/              # 登录页
│   │   └── ...                 # 其他页面
│   ├── hooks/                  # 自定义 Hooks
│   ├── utils/                  # 工具函数
│   ├── types/                  # TypeScript 类型定义
│   │   └── oidc.ts             # OIDC 相关类型
│   ├── App.tsx                 # 应用入口
│   └── main.tsx                # 渲染入口
├── public/                     # 静态资源
├── package.json
├── tsconfig.json
└── vite.config.ts
```

#### 4.1.3 路由设计

Console 采用 **客户端路由** (基于状态切换)：

```typescript
// 当前实现：基于状态的路由
type MenuItem = 'dashboard' | 'applications' | 'users' | 'roles' | 'hangfire';

const [currentMenu, setCurrentMenu] = useState<MenuItem>('dashboard');

// 未来优化：使用 React Router
// /console/                    -> Dashboard
// /console/applications        -> Applications
// /console/users               -> Users
// /console/roles               -> Roles
// /console/hangfire            -> Hangfire
```

#### 4.1.4 组件设计

**布局组件** (`AdminLayout`)：
```typescript
interface AdminLayoutProps {
  selectedKey?: string;          // 当前选中菜单
  onMenuClick?: (key: string) => void;  // 菜单点击回调
  user?: { name: string; avatar?: string };  // 用户信息
  onUserMenuClick?: (key: string) => void;   // 用户菜单回调
  children: ReactNode;           // 内容区域
}
```

**页面组件规范**：
```typescript
// 页面组件目录结构
pages/
└── PageName/
    ├── PageName.tsx            # 页面主组件
    ├── PageName.css            # 页面样式
    └── index.ts                # 导出入口

// 页面组件示例
export const PageName = () => {
  const [data, setData] = useState([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    void loadData();
  }, []);

  const loadData = async () => {
    // 加载数据逻辑
  };

  return (
    <div className="page-name">
      {/* 页面内容 */}
    </div>
  );
};
```

---

### 4.2 认证集成

#### 4.2.1 OIDC 认证流程

Console 使用 **Authorization Code Flow** 进行认证：

```
1. 用户访问 /console/
   ↓
2. 检查 localStorage 中的 access_token
   ↓ (无 token)
3. 跳转到 Auth Server 登录页
   GET /connect/authorize?
     client_id=radish-console&
     response_type=code&
     redirect_uri=https://localhost:5000/console/callback&
     scope=openid profile email
   ↓
4. 用户输入用户名密码
   ↓
5. Auth Server 验证成功，重定向到回调页面
   https://localhost:5000/console/callback?code=xxx
   ↓
6. Console 回调页面使用 code 换取 token
   POST /connect/token
   Body: grant_type=authorization_code&
         client_id=radish-console&
         code=xxx&
         redirect_uri=https://localhost:5000/console/callback
   ↓
7. 保存 access_token 和 refresh_token 到 localStorage
   ↓
8. 跳转到 Console 首页
```

#### 4.2.2 Token 管理

```typescript
// Token 存储
localStorage.setItem('access_token', token);
localStorage.setItem('refresh_token', refreshToken);

// Token 读取
const token = localStorage.getItem('access_token');

// API 请求携带 Token
fetch(url, {
  headers: {
    'Authorization': `Bearer ${token}`
  }
});

// Token 过期处理
// 1. API 返回 401 -> 尝试使用 refresh_token 刷新
// 2. 刷新失败 -> 清除 token，跳转登录页
```

#### 4.2.3 Single Sign-Out

```typescript
const handleLogout = () => {
  // 1. 清除本地 Token
  localStorage.removeItem('access_token');
  localStorage.removeItem('refresh_token');

  // 2. 重定向到 OIDC endsession endpoint
  const logoutUrl = new URL(`${authServerBaseUrl}/connect/endsession`);
  logoutUrl.searchParams.set('post_logout_redirect_uri', postLogoutRedirectUri);
  logoutUrl.searchParams.set('client_id', 'radish-console');

  window.location.href = logoutUrl.toString();
};
```

---

### 4.3 API 设计

#### 4.3.1 API 客户端封装

```typescript
// api/base.ts
interface ApiResponse<T = any> {
  ok: boolean;
  message?: string;
  data?: T;
}

async function request<T>(
  url: string,
  options?: RequestInit
): Promise<ApiResponse<T>> {
  const token = localStorage.getItem('access_token');

  const response = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      'Authorization': token ? `Bearer ${token}` : '',
      ...options?.headers,
    },
  });

  if (response.status === 401) {
    // Token 过期，跳转登录
    window.location.href = '/console/';
    throw new Error('Unauthorized');
  }

  const data = await response.json();
  return data;
}
```

#### 4.3.2 API 模块示例

```typescript
// api/clients.ts
export const clientApi = {
  // 获取客户端列表
  getClients: (params: { page: number; pageSize: number }) =>
    request<PagedResult<OidcClient>>('/api/v1/OidcClient/GetList', {
      method: 'POST',
      body: JSON.stringify(params),
    }),

  // 创建客户端
  createClient: (data: CreateClientRequest) =>
    request<{ clientId: string; clientSecret: string }>(
      '/api/v1/OidcClient/Create',
      {
        method: 'POST',
        body: JSON.stringify(data),
      }
    ),

  // 更新客户端
  updateClient: (id: string, data: UpdateClientRequest) =>
    request('/api/v1/OidcClient/Update', {
      method: 'PUT',
      body: JSON.stringify({ id, ...data }),
    }),

  // 删除客户端
  deleteClient: (id: string) =>
    request(`/api/v1/OidcClient/Delete/${id}`, {
      method: 'DELETE',
    }),

  // 重置密钥
  resetClientSecret: (id: string) =>
    request<{ clientSecret: string }>(
      `/api/v1/OidcClient/ResetSecret/${id}`,
      {
        method: 'POST',
      }
    ),
};
```

---

### 4.4 状态管理

#### 4.4.1 全局状态

```typescript
// 使用 Context + useReducer
interface AppState {
  user: User | null;
  permissions: string[];
  isLoading: boolean;
}

type AppAction =
  | { type: 'SET_USER'; payload: User }
  | { type: 'SET_PERMISSIONS'; payload: string[] }
  | { type: 'SET_LOADING'; payload: boolean };

const AppContext = createContext<{
  state: AppState;
  dispatch: Dispatch<AppAction>;
} | null>(null);
```

#### 4.4.2 页面状态

```typescript
// 使用 useState 管理页面状态
const [data, setData] = useState<T[]>([]);
const [loading, setLoading] = useState(false);
const [pagination, setPagination] = useState({ page: 1, pageSize: 10 });
```

---

### 4.5 样式方案

#### 4.5.1 CSS Modules

```typescript
// 使用普通 CSS + className
import './PageName.css';

<div className="page-name">
  <div className="page-header">...</div>
</div>
```

#### 4.5.2 主题定制

```css
/* 基于 @radish/ui 的主题变量 */
:root {
  --primary-color: #1890ff;
  --success-color: #52c41a;
  --warning-color: #faad14;
  --error-color: #f5222d;
}
```

---

## 相关文档

- [核心概念](/guide/console-core-concepts) - 权限模型和实体定义
- [功能模块](/guide/console-modules) - 详细功能说明
- [实施计划](/guide/console-roadmap) - 开发计划
- [认证系统](/guide/authentication) - OIDC 认证详解
