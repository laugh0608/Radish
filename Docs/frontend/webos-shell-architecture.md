# WebOS 应用注册与窗口系统

> 本文从 [前端设计文档](/frontend/design) 拆出，承载专题细节；设计入口只保留当前结论、边界和导航。

### 3. 应用注册系统

#### 3.1 应用注册表

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
  requiredRoles?: string[]; // 访问控制，可选
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
    type: 'window',
    category: 'commerce'
  },

  // === 管理应用 ===
  {
    id: 'console',
    name: '控制台',
    icon: 'mdi:console',
    description: '系统管理控制台',
    component: () => null,
    type: 'external',
    externalUrl: '/console/',
    category: 'system'
  },

  // === 文档应用 ===
  {
    id: 'document',
    name: '文档',
    icon: 'mdi:notebook-edit-outline',
    description: '固定文档与在线文档统一入口',
    component: WikiApp,
    type: 'window',
    defaultSize: { width: 1280, height: 820 },
    category: 'content'
  }
];
```

#### 3.2 权限控制

当前桌面采用“图标可见性”和“打开权限”分离的策略：

- 常规应用图标默认在桌面可见，避免匿名用户误以为平台只剩下少数入口；
- `console` 当前仅在管理员角色，或同时具备 `console.access + 至少一个真实 Console 页面访问权限` 时可见；
- 桌面提供全局右键菜单（ContextMenu），支持匿名和登录用户进行刷新、切换国风主题、查看关于等快捷操作；
- 真正需要登录才能打开的应用当前只保留：
  - `chat`
  - `profile`
  - `radish-pit`
  - `notification`
  - `experience-detail`
- 其余桌面应用默认允许匿名打开公开内容，例如：
  - `welcome`
  - `showcase`
  - `document`
  - `forum`
  - `leaderboard`
  - `shop`

对应的访问矩阵如下：

| 应用 | 桌面图标 | 未登录打开 | 已登录打开 |
|------|----------|------------|------------|
| 欢迎 / 组件库 / 文档 / 论坛 / 排行榜 / 商城 | 可见 | 可打开公开内容 | 可打开 |
| 聊天室 / 个人主页 / 萝卜坑 / 通知中心 / 等级 | 可见 | 拦截并提示登录 | 可打开 |
| 控制台 | 仅管理员或具备 `console.access + 至少一个真实 Console 页面访问权限` 时可见 | 不可见 | 命中权限后可打开 |

实现上不再简单按“角色过滤所有图标”，而是分别处理桌面展示与实际访问：

```typescript
import { canAccessApp, getVisibleAppsForUser } from './appAccess';

const visibleApps = getVisibleAppsForUser(appRegistry, {
  isAuthenticated,
  userRoles,
  userPermissions
});

const canOpen = canAccessApp(app, {
  isAuthenticated,
  userRoles,
  userPermissions
});
```

#### 3.3 桌面继续使用入口

桌面首页当前不再只是应用图标启动器，还承担已登录用户回到工作台后的轻量续接入口：

- **最近应用**：由工作台打开 / 复用应用时写入浏览器本地存储，只记录适合复访的业务应用，并排除欢迎页、组件展示、控制台、API 文档等非业务续接入口。
- **最近浏览**：复用现有浏览记录接口与桌面打开能力，优先让用户回到最近 forum 阅读上下文。
- **我的轻回应**：复用现有轻回应回看接口与帖子跳转能力，帮助用户从桌面回到已参与的社区内容。

该入口的设计边界是“回到刚才的工作”，不是完整历史中心：当前不提供清空、删除、筛选、跨端同步、混合时间线或新的后端 API。

### 4. 窗口系统

#### 4.1 窗口类型

| 类型 | 说明 | 适用场景 |
|------|------|---------|
| `window` | 可拖拽、调整大小的窗口 | 文档、论坛、聊天室、商城 |
| `fullscreen` | 全屏显示，隐藏桌面 | 预留给未来特别复杂的沉浸式应用 |
| `iframe` | 嵌入外部网页 | 展示型第三方工具 |

#### 4.2 窗口管理器

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

#### 4.3 窗口状态管理

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

当前实际行为在上述基础上已经进一步收口为：

- 新开窗口会按当前视口与应用默认尺寸自动居中，而不是固定偏移开窗。
- 普通窗口在拖动、缩放、关闭、参数切换和最大化恢复时，都会把“正常态”的位置与尺寸持久化到本地存储。
- 窗口持久化键默认按 `appId` 生成；若窗口携带业务定位参数，则按 `appId + 稳定业务参数` 生成独立记忆位。
- `__navigationKey` 等纯导航刷新参数不会参与持久化键计算，避免通知跳转或内容刷新污染同类窗口布局。
- 历史位置在当前分辨率下越界时，会先做边界压缩后再恢复，确保窗口始终落在可见区域内。
