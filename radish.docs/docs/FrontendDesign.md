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
    url: 'https://localhost:7110/scalar',
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

## 11. 迭代计划

### 阶段一：M4（桌面系统基础）

- [x] Desktop Shell 骨架
- [ ] 应用注册系统
- [ ] 窗口管理器（窗口/全屏/iframe）
- [ ] 权限控制
- [ ] 论坛应用（MVP）

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
