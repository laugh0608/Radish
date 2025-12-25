# 前端设计文档

> Radish 采用 **超级应用（Super App）/ WebOS** 架构，提供类似操作系统的桌面体验。用户登录后看到桌面，双击应用图标即可打开论坛、聊天室、商城、后台管理等不同应用。本文档描述整体架构、技术栈和实现方案。

## 1. 设计理念

### 1.1 核心概念：WebOS

**Radish 不是一个网站，而是一个运行在浏览器中的操作系统。**

```
用户访问 radish.client
        ↓
    统一登录 (OIDC)
        ↓
   桌面系统（Desktop Shell）
        ↓
根据角色显示应用图标
        ↓
双击图标 → 打开应用
        ↓
[论坛] [聊天室] [商城] → 窗口模式
[后台管理] [文档] → 全屏/iframe 模式
```

### 1.2 设计目标

1. **统一入口**：所有功能通过桌面访问，无需记忆多个 URL
2. **权限控制**：根据用户角色动态显示可用应用
3. **沉浸体验**：桌面化交互（状态栏、Dock、窗口系统）
4. **无缝切换**：应用间切换无需重新登录
5. **扩展性强**：新增功能只需注册新应用

## 2. 系统架构

### 2.1 整体结构

```
┌────────────────────────────────────────────────────┐
│               Radish Desktop Shell                  │
│  ┌────────────────────────────────────────────┐    │
│  │ 状态栏：用户 | IP | 消息 | 系统状态         │    │
│  └────────────────────────────────────────────┘    │
│                                                     │
│  桌面应用图标（基于权限显示）：                       │
│  ┌─────┐  ┌─────┐  ┌─────┐  ┌─────┐              │
│  │论坛 │  │聊天 │  │商城 │  │文档 │              │
│  │ 📝  │  │ 💬  │  │ 🛒  │  │ 📄  │              │
│  └─────┘  └─────┘  └─────┘  └─────┘              │
│  ┌─────┐  ┌─────┐                                 │
│  │后台 │  │游戏 │  ... (更多应用)                  │
│  │ ⚙️  │  │ 🎮  │                                │
│  └─────┘  └─────┘                                 │
│  ↑ 仅管理员可见                                      │
│                                                     │
│  ┌────────────────────────────────────────────┐    │
│  │ Dock：论坛(运行中) | 聊天室(运行中)          │    │
│  └────────────────────────────────────────────┘    │
└────────────────────────────────────────────────────┘
```

### 2.2 技术架构

```
radish.client/
├── src/
│   ├── desktop/              # 桌面系统核心
│   │   ├── Shell.tsx         # 桌面外壳（容器）
│   │   ├── StatusBar.tsx     # 顶部状态栏
│   │   ├── Desktop.tsx       # 桌面图标网格
│   │   ├── Dock.tsx          # 底部 Dock 栏
│   │   ├── WindowManager.tsx # 窗口管理器
│   │   ├── AppRegistry.tsx   # 应用注册表
│   │   └── types.ts          # 类型定义
│   │
│   ├── apps/                 # 子应用（各功能模块）
│   │   ├── forum/            # 论坛应用
│   │   │   ├── ForumApp.tsx  # 应用入口
│   │   │   ├── pages/        # 页面
│   │   │   ├── components/   # 组件
│   │   │   └── routes.tsx    # 路由
│   │   │
│   │   ├── chat/             # 聊天室应用
│   │   ├── shop/             # 商城应用
│   │   ├── admin/            # 后台管理应用
│   │   ├── docs/             # 文档应用（iframe）
│   │   └── games/            # 游戏应用（示例）
│   │
│   ├── widgets/              # 桌面小部件
│   │   ├── DesktopWindow.tsx # 窗口组件
│   │   ├── AppIcon.tsx       # 应用图标
│   │   └── Notification.tsx  # 通知组件
│   │
│   ├── shared/               # 共享代码
│   │   ├── ui/               # 基础 UI 组件
│   │   ├── api/              # API 客户端
│   │   ├── auth/             # 认证逻辑
│   │   ├── hooks/            # 通用 Hooks
│   │   └── utils/            # 工具函数
│   │
│   └── stores/               # 全局状态
│       ├── windowStore.ts    # 窗口状态
│       ├── dockStore.ts      # Dock 状态
│       └── userStore.ts      # 用户状态
```

## 3. 应用注册系统

### 3.1 应用注册表

所有应用在 `AppRegistry.tsx` 中注册：

```typescript
// desktop/AppRegistry.tsx
export interface AppDefinition {
  id: string;
  name: string;
  icon: string;
  description?: string;
  component?: React.ComponentType;
  type: 'window' | 'fullscreen' | 'iframe';
  defaultSize?: { width: number; height: number };
  url?: string; // for iframe
  requiredRoles: string[]; // 权限控制
  category?: string; // 分类
}

export const appRegistry: AppDefinition[] = [
  // === 内容应用 ===
  {
    id: 'forum',
    name: '论坛',
    icon: '📝',
    description: '社区讨论与内容分享',
    component: ForumApp,
    type: 'window',
    defaultSize: { width: 1200, height: 800 },
    requiredRoles: ['User'],
    category: 'content'
  },
  {
    id: 'chat',
    name: '聊天室',
    icon: '💬',
    description: '实时交流',
    component: ChatApp,
    type: 'window',
    defaultSize: { width: 800, height: 600 },
    requiredRoles: ['User'],
    category: 'social'
  },
  {
    id: 'shop',
    name: '商城',
    icon: '🛒',
    description: '积分商城',
    component: ShopApp,
    type: 'fullscreen', // 全屏体验更好
    requiredRoles: ['User'],
    category: 'commerce'
  },

  // === 管理应用 ===
  {
    id: 'admin',
    name: '后台管理',
    icon: '⚙️',
    description: '系统管理控制台',
    component: AdminApp,
    type: 'fullscreen',
    requiredRoles: ['Admin', 'System'],
    category: 'admin'
  },

  // === 工具应用 ===
  {
    id: 'docs',
    name: 'API 文档',
    icon: '📄',
    description: 'Scalar API 文档',
    type: 'iframe',
    url: 'https://localhost:5000/scalar',
    defaultSize: { width: 1400, height: 900 },
    requiredRoles: ['Developer', 'Admin'],
    category: 'tools'
  },

  // === 第三方应用（示例） ===
  {
    id: 'game-example',
    name: '小游戏',
    icon: '🎮',
    component: GameApp,
    type: 'window',
    defaultSize: { width: 600, height: 600 },
    requiredRoles: ['User'],
    category: 'entertainment'
  }
];
```

### 3.2 权限控制

桌面根据用户角色过滤可见应用：

```typescript
// desktop/Desktop.tsx
const Desktop = () => {
  const { user } = useAuth();

  // 过滤用户有权限的应用
  const visibleApps = appRegistry.filter(app =>
    app.requiredRoles.some(role => user.roles?.includes(role))
  );

  return (
    <div className="desktop-grid">
      {visibleApps.map(app => (
        <AppIcon
          key={app.id}
          app={app}
          onDoubleClick={() => openApp(app.id)}
          onContextMenu={(e) => showContextMenu(e, app.id)}
        />
      ))}
    </div>
  );
};
```

## 4. 窗口系统

### 4.1 窗口类型

| 类型 | 说明 | 适用场景 |
|------|------|---------|
| `window` | 可拖拽、调整大小的窗口 | 论坛、聊天室等小应用 |
| `fullscreen` | 全屏显示，隐藏桌面 | 商城、后台管理等复杂应用 |
| `iframe` | 嵌入外部网页 | API 文档、第三方工具 |

### 4.2 窗口管理器

```typescript
// desktop/WindowManager.tsx
export const WindowManager = () => {
  const { openWindows } = useWindowStore();

  return (
    <>
      {openWindows.map(window => {
        const app = appRegistry.find(a => a.id === window.appId);

        if (app.type === 'fullscreen') {
          return (
            <FullscreenApp
              key={window.id}
              onClose={() => closeWindow(window.id)}
            >
              <app.component />
            </FullscreenApp>
          );
        }

        return (
          <DesktopWindow
            key={window.id}
            title={app.name}
            icon={app.icon}
            defaultSize={app.defaultSize}
            onClose={() => closeWindow(window.id)}
            onMinimize={() => minimizeWindow(window.id)}
            zIndex={window.zIndex}
          >
            {app.type === 'iframe' ? (
              <iframe src={app.url} className="w-full h-full" />
            ) : (
              <app.component />
            )}
          </DesktopWindow>
        );
      })}
    </>
  );
};
```

### 4.3 窗口状态管理

```typescript
// stores/windowStore.ts
interface Window {
  id: string;
  appId: string;
  zIndex: number;
  isMinimized: boolean;
  position?: { x: number; y: number };
  size?: { width: number; height: number };
}

export const useWindowStore = create<WindowStore>((set) => ({
  openWindows: [],

  openApp: (appId: string) => set(state => {
    // 如果已打开，聚焦窗口
    const existing = state.openWindows.find(w => w.appId === appId);
    if (existing) {
      return { openWindows: bringToFront(existing.id, state.openWindows) };
    }

    // 创建新窗口
    const newWindow = {
      id: nanoid(),
      appId,
      zIndex: getMaxZIndex(state.openWindows) + 1,
      isMinimized: false
    };

    return { openWindows: [...state.openWindows, newWindow] };
  }),

  closeWindow: (windowId: string) => set(state => ({
    openWindows: state.openWindows.filter(w => w.id !== windowId)
  })),

  minimizeWindow: (windowId: string) => set(state => ({
    openWindows: state.openWindows.map(w =>
      w.id === windowId ? { ...w, isMinimized: true } : w
    )
  }))
}));
```

## 5. 子应用开发

### 5.1 论坛应用示例

```typescript
// apps/forum/ForumApp.tsx
export const ForumApp = () => {
  return (
    <div className="forum-app h-full flex flex-col">
      <ForumHeader />
      <div className="flex-1 overflow-hidden">
        <Routes>
          <Route path="/" element={<PostList />} />
          <Route path="/post/:id" element={<PostDetail />} />
          <Route path="/create" element={<CreatePost />} />
          <Route path="/category/:id" element={<CategoryView />} />
        </Routes>
      </div>
    </div>
  );
};

// apps/forum/pages/PostList.tsx
const PostList = () => {
  const { data } = useQuery({
    queryKey: ['posts'],
    queryFn: () => api.getPosts()
  });

  return (
    <div className="post-list">
      {data?.items.map(post => (
        <PostCard key={post.id} post={post} />
      ))}
    </div>
  );
};
```

### 5.2 后台管理应用

```typescript
// apps/admin/AdminApp.tsx
import { Layout, Menu } from 'antd';

export const AdminApp = () => {
  return (
    <Layout className="h-full">
      <Layout.Sider>
        <Menu
          items={[
            { key: 'dashboard', icon: <DashboardOutlined />, label: '仪表盘' },
            { key: 'apps', icon: <AppstoreOutlined />, label: '应用管理' },
            { key: 'users', icon: <UserOutlined />, label: '用户管理' },
            { key: 'roles', icon: <TeamOutlined />, label: '角色管理' }
          ]}
        />
      </Layout.Sider>
      <Layout.Content>
        <Routes>
          <Route path="/" element={<Dashboard />} />
          <Route path="/apps" element={<AppManagement />} />
          <Route path="/users" element={<UserManagement />} />
          <Route path="/roles" element={<RoleManagement />} />
        </Routes>
      </Layout.Content>
    </Layout>
  );
};
```

## 6. 移动端适配

### 6.1 响应式策略

```typescript
// desktop/Shell.tsx
const Shell = () => {
  const isMobile = useMediaQuery('(max-width: 768px)');

  if (isMobile) {
    return <MobileShell />;
  }

  return <DesktopShell />;
};
```

### 6.2 移动端布局

```
移动端自动转换为 Tab 导航：

┌────────────────────────┐
│ 状态栏                  │
├────────────────────────┤
│                        │
│   当前应用内容           │
│                        │
│                        │
├────────────────────────┤
│ Tab: 论坛|聊天|商城|我  │
└────────────────────────┘
```

```typescript
// desktop/MobileShell.tsx
const MobileShell = () => {
  const { user } = useAuth();
  const visibleApps = appRegistry.filter(app =>
    app.requiredRoles.some(role => user.roles?.includes(role))
  );

  return (
    <div className="mobile-shell">
      <StatusBar />
      <Routes>
        {visibleApps.map(app => (
          <Route
            key={app.id}
            path={`/${app.id}/*`}
            element={<app.component />}
          />
        ))}
      </Routes>
      <MobileTabBar apps={visibleApps} />
    </div>
  );
};
```

## 7. 技术栈

| 层级 | 技术选型 |
|------|---------|
| 框架 | React 19 + TypeScript |
| 构建 | Vite (Rolldown) |
| 路由 | TanStack Router |
| 状态管理 | Zustand (窗口/Dock) + TanStack Query (数据) |
| UI 框架 | TailwindCSS + 自研组件 |
| 窗口拖拽 | react-rnd |
| 动效 | Framer Motion |
| 后台组件 | Ant Design (仅 admin 应用使用) |
| 表单 | React Hook Form + Zod |
| 国际化 | react-i18next |

## 8. 设计系统

### 8.1 Design Tokens

```typescript
// shared/config/tokens.ts
export const tokens = {
  colors: {
    desktop: {
      background: '#1a1a2e',
      foreground: '#eee'
    },
    primary: '#00adb5',
    secondary: '#393e46'
  },
  spacing: {
    dock: 64,
    statusBar: 40,
    appIconGap: 24
  },
  borderRadius: {
    window: 12,
    appIcon: 16
  },
  shadows: {
    window: '0 8px 32px rgba(0,0,0,0.3)',
    appIcon: '0 2px 8px rgba(0,0,0,0.2)'
  }
};
```

### 8.2 基础组件

| 组件 | 说明 | 用途 |
|------|------|------|
| Button | 统一按钮 | 所有应用 |
| Input | 统一输入框 | 所有应用 |
| Modal | 统一弹窗 | 所有应用 |
| Card | 卡片容器 | 论坛、商城 |
| ProTable | 高级表格 | 后台管理 |
| ProForm | 高级表单 | 后台管理 |

### 8.3 图标系统

```typescript
// 使用 Iconify 统一图标
import { Icon } from '@iconify/react';

<Icon icon="mdi:forum" />
<Icon icon="mdi:chat" />
<Icon icon="mdi:cart" />
```

### 8.4 UI 组件资源库

**推荐资源：Uiverse Galaxy**

[Uiverse Galaxy](https://github.com/uiverse-io/galaxy) 是全球最大的开源 UI 组件库之一，包含 **3500+ 个社区驱动的 UI 元素**，可作为前端开发和后台管理系统的重要参考资源。

**核心特点：**

- **海量组件**：3500+ 个精心设计的 UI 元素，涵盖按钮、卡片、加载器、导航栏、输入框、切换开关、价格表等
- **双格式支持**：每个组件提供纯 CSS 和 Tailwind CSS 两种实现方式
- **社区驱动**：由全球设计师贡献，每个组件都经过人工审核
- **MIT 许可**：完全免费，可用于商业项目
- **即取即用**：所有组件可直接复制代码使用，无需安装依赖

**使用场景：**

1. **桌面系统组件**：为 Radish 的 Desktop Shell、Dock、StatusBar 等核心组件寻找设计灵感
2. **论坛应用**：获取帖子卡片、点赞按钮、评论框等社区交互组件
3. **商城应用**：参考商品卡片、价格标签、购买按钮等电商组件
4. **后台管理**：寻找表格、表单、统计卡片等管理界面组件
5. **加载与反馈**：使用各种创意加载器、进度条、Toast 通知组件

**集成方式：**

```typescript
// 方式一：直接复制组件代码到项目中
// shared/ui/Button/GlowButton.tsx
export const GlowButton = ({ children, onClick }) => {
  return (
    <button
      onClick={onClick}
      className="px-6 py-3 bg-gradient-to-r from-blue-500 to-purple-600
                 text-white rounded-lg shadow-lg hover:shadow-2xl
                 transition-all duration-300 hover:scale-105"
    >
      {children}
    </button>
  );
};

// 方式二：使用 Tailwind 版本的组件样式
// 访问 https://uiverse.io 搜索组件，复制 Tailwind 类名
```

**推荐组件类型：**

| 组件类型 | 数量 | 适用场景 |
|---------|------|---------|
| Buttons | 800+ | 主操作、次要操作、图标按钮 |
| Cards | 600+ | 内容卡片、信息面板、商品卡片 |
| Loaders | 500+ | 页面加载、数据加载、骨架屏 |
| Inputs | 400+ | 文本输入、搜索框、标签输入 |
| Checkboxes | 300+ | 多选框、切换开关、单选按钮 |
| Forms | 200+ | 登录表单、注册表单、设置表单 |

**注意事项：**

1. **样式兼容性**：复制组件时注意检查是否与项目的 Tailwind 配置兼容
2. **可访问性**：部分组件可能缺少无障碍属性，使用时需补充 ARIA 标签
3. **性能考虑**：动画较多的组件需注意性能影响，必要时使用 `will-change` 优化
4. **主题适配**：组件可能需要调整颜色以匹配 Radish 的 Design Tokens
5. **响应式**：部分组件需要手动添加移动端适配

**资源链接：**

- GitHub 仓库：https://github.com/uiverse-io/galaxy
- 在线浏览：https://uiverse.io
- 组件分类：https://uiverse.io/all

**开发建议：**

- 在设计新组件前，先浏览 Uiverse 寻找灵感
- 复制组件后进行二次定制，使其符合 Radish 设计规范
- 对于高频使用的组件（如按钮、输入框），封装为项目标准组件
- 在 Storybook 中记录引用的 Uiverse 组件来源，便于后续维护

## 9. 性能优化

### 9.1 应用懒加载

```typescript
// desktop/AppRegistry.tsx
const ForumApp = lazy(() => import('@/apps/forum/ForumApp'));
const ChatApp = lazy(() => import('@/apps/chat/ChatApp'));
const ShopApp = lazy(() => import('@/apps/shop/ShopApp'));
const AdminApp = lazy(() => import('@/apps/admin/AdminApp'));
```

### 9.2 窗口虚拟化

只渲染可见窗口，最小化的窗口不渲染内容：

```typescript
{openWindows.map(window => (
  window.isMinimized ? (
    <MinimizedPlaceholder key={window.id} />
  ) : (
    <DesktopWindow key={window.id} {...window} />
  )
))}
```

### 9.3 数据缓存

```typescript
// 使用 TanStack Query 缓存
const { data } = useQuery({
  queryKey: ['posts'],
  queryFn: fetchPosts,
  staleTime: 5 * 60 * 1000, // 5分钟
  cacheTime: 30 * 60 * 1000 // 30分钟
});
```

## 10. 开发规范

### 10.1 新增应用

1. 在 `apps/` 下创建应用目录
2. 创建 `{App}App.tsx` 入口文件
3. 在 `AppRegistry.tsx` 注册应用
4. 配置权限和窗口类型

### 10.2 应用间通信

```typescript
// 使用 EventBus 或全局状态
import { eventBus } from '@/shared/eventBus';

// 论坛应用发送消息
eventBus.emit('new-message', { count: 5 });

// 状态栏监听消息
eventBus.on('new-message', ({ count }) => {
  showNotification(`您有 ${count} 条新消息`);
});
```

### 10.3 路由规范

```
桌面路由：/
应用路由：/{appId}/*

示例：
/ - 桌面
/forum - 论坛首页
/forum/post/123 - 论坛帖子详情
/chat - 聊天室
/admin/apps - 后台应用管理
```

### 10.4 应用集成架构决策

#### 10.4.1 三种应用类型的选择标准

Radish WebOS 支持三种应用集成方式,选择标准如下:

| 应用类型 | 判断标准 | 适用场景 | 示例 |
|---------|---------|---------|------|
| **内置应用 (Built-in)** | - 无需独立部署<br>- 无复杂路由<br>- 可共享认证状态 | - 简单功能模块<br>- 用户高频使用<br>- 需要与桌面深度集成 | Forum(论坛)<br>Chat(聊天)<br>Settings(设置) |
| **嵌入应用 (Embedded)** | - 展示型内容<br>- 无需认证或简单认证<br>- 被动浏览为主 | - 文档站<br>- 帮助中心<br>- 静态内容展示 | Docs(文档站)<br>Help(帮助) |
| **外部应用 (External)** | - 完整的 SPA<br>- 有 OIDC 认证流程<br>- 复杂路由系统<br>- 需要独立访问 | - 管理后台<br>- 复杂业务系统<br>- 需要独立部署的模块 | Console(管理控制台)<br>Shop(商城) |

#### 10.4.2 为什么 Console 不能嵌入 WebOS?

**技术限制**:

1. **OIDC 认证流程冲突**
   ```
   OIDC 标准流程:
   1. 用户点击登录 → 跳转到 Auth Server
   2. Auth Server 认证成功 → 重定向到 redirect_uri
   3. 应用处理回调 → 获取 token

   在 iframe 中的问题:
   - redirect_uri 无法指向 iframe 内部的 URL
   - 认证服务器无法将用户重定向到 iframe
   - token 存储在 iframe 的 localStorage,父页面无法访问
   ```

2. **路由系统冲突**
   ```
   浏览器地址栏: https://localhost:5000/ (WebOS 的地址)
   Console 内部路由: /dashboard, /users, /settings

   问题:
   - Console 的路由无法反映在地址栏中
   - 用户刷新页面会回到 WebOS 首页
   - 无法分享 Console 内部页面的链接
   - Console 使用的 React Router 无法正常工作
   ```

3. **Gateway 路径剥离导致的混乱**
   ```
   Gateway 配置: /console/dashboard → 剥离前缀 → /dashboard
   Console 认为: 自己在根路径 /
   实际位置: 在 /console/ 下
   iframe 中: 地址栏显示 https://localhost:5000/ (父页面)

   结果: Console 的所有绝对路径引用都会指向错误位置
   ```

4. **用户体验问题**
   ```
   外层: WebOS 窗口系统(可拖动、最小化)
   内层: Console 自己的 UI(导航栏、侧边栏)

   用户困惑:
   - 双层标题栏(WebOS 窗口标题 + Console 标题)
   - 双层滚动条(窗口滚动 + 内容滚动)
   - 操作冲突(窗口拖动 vs 内容交互)
   ```

**架构理由**:

1. **关注点分离 (Separation of Concerns)**
   - Client: 面向 C 端用户,强调易用性和娱乐性
   - Console: 面向管理员,强调功能性和数据安全

2. **权限隔离 (Security Isolation)**
   - 普通用户不应加载管理功能的代码(减少攻击面)
   - 管理功能需要更严格的审计和安全检查

3. **部署灵活性 (Deployment Flexibility)**
   - Client 可部署到公网 CDN(高速访问)
   - Console 可部署到内网(安全隔离)
   - 各自独立扩容和维护

4. **开发独立性 (Development Independence)**
   - Client 团队和 Console 团队可并行开发
   - 代码冲突减少,发版互不影响
   - 可采用不同的技术栈和 UI 库

5. **代码体积控制 (Bundle Size Optimization)**
   - Client 打包体积应尽可能小(普通用户)
   - Console 可以稍大(管理员使用频率低)
   - 避免普通用户下载用不到的管理功能代码

#### 10.4.3 应用集成最佳实践

**添加新应用时的决策流程**:

```typescript
// 决策树
if (应用需要 OIDC 认证 && 有复杂路由) {
  使用 type: 'external'
  在新标签页打开
} else if (应用是展示型 && 无复杂交互) {
  使用 type: 'iframe'
  嵌入 WebOS 窗口
} else {
  使用 type: 'window'
  作为内置应用开发
}
```

**实现示例**:

```typescript
// radish.client/src/desktop/AppRegistry.tsx

// ✅ 内置应用 - 论坛
{
  id: 'forum',
  name: '论坛',
  icon: 'mdi:forum',
  component: ForumApp, // React 组件
  type: 'window',
  defaultSize: { width: 1200, height: 800 }
}

// ✅ 嵌入应用 - 文档站
{
  id: 'docs',
  name: '文档中心',
  icon: 'mdi:book-open-page-variant',
  component: () => null, // iframe 不需要组件
  type: 'iframe',
  url: 'http://localhost:3100/docs/',
  defaultSize: { width: 1200, height: 800 }
}

// ✅ 外部应用 - 管理控制台
{
  id: 'console',
  name: '控制台',
  icon: 'mdi:console',
  component: () => null, // external 不需要组件
  type: 'external',
  externalUrl: typeof window !== 'undefined' &&
    window.location.origin.includes('localhost:5000')
    ? '/console/' // 通过 Gateway
    : 'http://localhost:3200', // 直接访问
  requiredRoles: ['Admin']
}
```

**共享组件策略**:

```
@radish/ui (共享 UI 组件库)
    ↓
┌───┴────┬─────────┬─────────┐
│        │         │         │
Client  Console   Shop    Docs
```

- 基础组件(Button, Input, Modal)放在 `@radish/ui`
- 业务特定组件各自维护
- 通过 npm workspaces 实现热更新

### 10.5 WebOS 与 SEO 适配规划

> 仅 WebOS（radish.client）需要对搜索引擎友好，用于公开帖子列表/详情；其他前端项目（radish.console、radish.docs）默认不做 SEO 要求。此处只记录前端视角的规划，真正启用时需要与部署文档的 Docker 方案配合。

#### 10.5.1 URL 与路由规划

- 公开内容（需 SEO）：
  - 帖子列表：`/forum`、`/forum/category/{id}`、`/forum/tag/{tag}`
  - 帖子详情：`/forum/post/{id}` 或 `/forum/post/{id}-{slug}`
- 登录后功能（不要求 SEO）：
  - 发帖/编辑：`/forum/create`、`/forum/edit/{id}`
  - 用户中心：`/me`、`/settings` 等
- 桌面 Shell 与应用路由关系：
  - 桌面仍然挂在 `/` 路径；
  - 对于搜索引擎访问 `/forum`、`/forum/post/*` 等路径时，可以直接渲染论坛应用而不是完整桌面壳，以减少噪音并提升首屏内容密度。

#### 10.5.2 SSR/SSG 与 hydrate 策略（前端视角）

- WebOS 论坛相关路由建议支持：
  - 服务端渲染（SSR）：
    - 在服务器端拉取帖子数据，返回完整 HTML（标题、正文摘要、首屏列表等）；
    - 浏览器加载 JS 后再对页面进行 hydrate，继续以 SPA 方式运行。
  - 或静态站点生成（SSG）：
    - 对热门帖子、专题页预生成静态 HTML；
    - 通过前端路由和 API 实现增量内容加载。
- 具体实现可以基于：
  - Vite SSR
  - Next.js / Remix
  - Astro 等支持 React 的 SSG/SSR 框架
- 文档层面只要求：
  - `/forum`、`/forum/post/*` 等路由在**服务端就具备完整 HTML 内容**，而不是只有空 `div#root`。

#### 10.5.3 SEO 元信息与结构化数据

- 每个可索引页面需要具备：
  - `<title>`：`{帖子标题} - Radish`；
  - `<meta name="description" content="{摘要或首段内容}">`；
  - `<link rel="canonical" href="https://radish.com/forum/post/{id}">`；
  - 根据需要配置 `og:title` / `og:description` / `og:image` 等社交分享 meta。
- 可选：为帖子详情页输出 JSON-LD 结构化数据：

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "DiscussionForumPosting",
  "headline": "{帖子标题}",
  "datePublished": "{ISO 时间}",
  "author": {
    "@type": "Person",
    "name": "{作者昵称}"
  }
}
</script>
```

- 上述 meta 与 JSON-LD 可以在 SSR 阶段注入，也可以通过前端渲染时在 `<head>` 中动态更新（优先推荐 SSR 注入）。

#### 10.5.4 sitemap 与 robots 规范

- 建议由后端或独立任务生成 sitemap：
  - 包含主要列表页：`/forum`、`/forum/category/*`、`/forum/tag/*`；
  - 包含一部分帖子详情页：可以按时间/热门程度取 Top N，避免 sitemap 过大；
  - 将 sitemap 暴露为：`/sitemap.xml` 或 `/sitemap-forum.xml`。
- `robots.txt` 规划：
  - 允许搜索引擎抓取论坛公开路径：`/forum`、`/forum/post/*` 等；
  - 显式禁止与登录、设置相关的路径：`/me`、`/settings`、`/admin/*` 等。

> 具体 sitemap 和 robots 的生成/托管位置建议在后端与部署文档中补充，这里只约束前端 URL 与可索引页面的范围。

## 11. 迭代计划

### 阶段一：M4（桌面系统基础）

- [x] Desktop Shell 骨架（Shell、StatusBar、Desktop、Dock 已实现）
- [x] 应用注册系统（AppRegistry.tsx 已实现，支持权限、类型、分类）
- [x] 窗口管理器（窗口/全屏/iframe）（WindowManager.tsx + DesktopWindow.tsx 已实现 react-rnd）
- [x] 权限控制（基于 requiredRoles 的应用过滤，支持角色级访问控制）
- [ ] 论坛应用（MVP）（仅实现了 WelcomeApp 作为演示应用）
- [x] 用 WebOS 桌面 Shell 取代当前 `src/App.tsx` Demo 页（已通过 main.tsx URL 参数实现：默认 Shell，?demo 访问原页面，?showcase 查看组件库）

### 阶段二：M5（核心应用）

- [ ] 聊天室应用
- [ ] 商城应用
- [ ] 后台管理应用（应用管理模块）
- [ ] Dock 运行指示
- [ ] 窗口动画

### 阶段三：M6-M7（增强体验）

- [ ] 移动端适配
- [ ] 快捷键支持
- [ ] 桌面小部件
- [ ] 多桌面/工作区
- [ ] 性能优化

### 阶段四：M8+（扩展生态）

- [ ] 第三方应用接入
- [ ] 应用商店
- [ ] React Native 版本
- [ ] 插件系统

## 12. 参考资料

- Nebula OS 原型：`public/webos.html`
- 窗口拖拽：react-rnd
- macOS Big Sur 设计规范
- Windows 11 设计规范

---

> 本文档是 Radish 前端架构的唯一事实来源，其他文档仅保留摘要与引用。
