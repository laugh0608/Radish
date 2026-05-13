# 前端设计文档

> Radish 第一开发阶段以前端 **WebOS / 超级应用** 为主入口完成首版交付；第二开发阶段开始，前端策略正式演进为“公开内容壳层 + 桌面工作台壳层 + 未来原生客户端壳层”的多壳层分工。本文档描述当前桌面事实、演进方向与相关实现约束。

## 1. 设计理念

### 1.1 第一阶段核心概念：WebOS

**Radish 不是一个网站，而是一个运行在浏览器中的操作系统。**

```
用户访问 radish.client
        ↓
桌面系统（Desktop Shell）
        ↓
根据显示规则呈现应用图标
        ↓
匿名可直接打开公开应用
        ↓
登录后解锁聊天 / 个人能力
        ↓
[论坛] [文档] [商城] → 窗口模式
[控制台] → 外部应用
```

### 1.2 第二阶段定位：多壳层分工

截至 `2026-04-07`，当前官方定位已经从“所有能力统一走桌面入口”调整为：

- **公开内容壳层**
  - 面向公开浏览、分享传播、搜索流量与移动端访问
- **桌面工作台壳层**
  - 面向桌面端、已登录用户与高交互场景
- **未来原生客户端壳层**
  - 面向 Flutter 客户端，不复刻 WebOS 窗口系统
- **桌面安装包壳层**
  - 面向 Tauri + WebOS 桌面安装包，不重写原生 UI

当前决策以 [前端多壳层策略](/frontend/shell-strategy) 为准。

### 1.3 设计目标

1. **统一产品身份**：保持一个 Radish，而不是分裂成互不相认的多套前端
2. **权限控制**：公开应用匿名可访问，私有能力按登录态与权限分层控制
3. **桌面沉浸体验**：在需要工作台语义的场景保留桌面化交互（状态栏、Dock、窗口系统）
4. **内容直达能力**：公开内容不强制要求先进入桌面再打开窗口
5. **多端扩展性**：移动 Web 与 Flutter 可以复用数据、认证和主题语义，但不强求界面结构一致

### 1.4 当前边界

- 当前代码事实仍然以 `Desktop Shell + WindowManager` 为主
- `Clients/radish.flutter` 当前已完成 Android MVP 第一轮 RC 验收并给出 Go 结论：壳层登录态分发、公开 forum / docs / discover / profile 读取、forum detail / comment 只读阅读、detail 原地登录续接、已登录态最小 forum notification 回流、profile 复访、docs 搜索 / 内链、轻回应即时前插与局部成功 / 失败反馈均已落地；当前仍明确保持窄范围阅读与轻互动边界，不扩完整通知中心、系统通知栏推送、发帖、完整评论提交、点赞、投票或桌面治理能力
- Android / iOS 移动安装包继续以 Flutter 为主线；Capacitor Android 仅保留公开只读 React 页面复用的技术参考，不进入登录态移动端产品化路线
- Windows / macOS / Linux 桌面安装包走 `Tauri 壳 + WebOS 桌面工作台`：Tauri 承接系统窗口、系统浏览器 loopback 登录回跳、deep link 兼容和安装包承载，WebOS 继续承接 Dock、窗口系统和桌面业务体验；截至 `2026-05-05`，个人开发阶段安装、登录、覆盖安装、卸载与 `radish://` 协议注册清理已验证通过，签名、自动更新、生产 Auth、SmartScreen 与公开分发链路后置到真实对外分发前；Tauri 不是移动端替代方案，也不是原生 UI 重写路线
- WebOS 桌面工作台当前已补首批“继续使用”复访面板：桌面首页按最近应用、最近浏览、我的轻回应分组承接已登录用户的回到工作台场景；最近应用使用本地轻量记录，最近浏览与我的轻回应复用既有 API 与工作台打开能力；该面板不等于完整历史中心，不扩删除 / 清空、跨端同步或新的后端 API
- 公开内容壳层当前已完成 forum、docs、个人公开页、公开榜单与公开商城浏览五个首批入口，并继续补到 forum 公开分类、forum 公开搜索与 docs 公开搜索首批：`/forum`、`/forum/category/:categoryId`、`/forum/search`、`/forum/post/:postId`、`/docs`、`/docs/search`、`/docs/:slug`、`/u/:id`、`/leaderboard`、`/leaderboard/:type`、`/shop`、`/shop/products` 与 `/shop/product/:productId` 都已可直接进入公开阅读壳层
- 公开内容壳层当前已形成共享头部视觉基线：forum / docs / discover / leaderboard / shop / `u/:id` 在窄屏下统一使用品牌字、图标与按钮 token，避免同一公开壳层内继续出现专题主题色、图标色和主按钮色各自漂移
- `/discover` 当前已形成更明确的公开分发节奏：forum / docs / leaderboard / shop 四张摘要卡默认优先预览本页对应区块，同时保留明确的“直接进入公开页”动作，不再把整卡点击简单等同为专题直跳
- `/discover` 当前也会记住最近一次摘要预览或区块来源；从公开专题页顶部回到“社区发现”时，会优先回到上一次阅读区块，而不是每次都丢回页首
- 公开内容壳层当前仍保持分批只读阅读边界：forum 不承载发帖、评论提交、投票提交，文档阅读不承载编辑、发布、回收站或版本历史等桌面治理交互
- forum 公开分类、公开标签、公开结构化类型与公开搜索首批当前只承载分类 / 标签 / 类型上下文、关键词检索、帖子列表阅读、排序分页与详情回跳上下文；标签 SEO 深化仍放在后续规划
- 个人公开页首批当前只承载公开资料、公开统计、公开帖子与公开评论阅读；不把编辑资料、浏览记录、附件管理或完整关系链治理搬进公开壳层
- 个人公开页首屏当前也已开始补“公开主页阅读说明”这一类只读说明增强：优先解释基础资料、公开帖子 / 评论阅读与工作台边界，而不是把个人治理或账号历史动作误带进公开壳层
- 公开榜单首批当前只承载榜单切换、分页、登录用户“我的排名”增强，以及用户榜单跳转个人公开页；默认经验榜单页 `/leaderboard`（兼容 `/leaderboard/experience` 收口）当前会额外展示“经验体系公开展示”说明，但不把经验明细、商城详情、购买链路或其他工作台动作搬进公开壳层
- 公开榜单的非经验榜单当前也已开始补轻量只读说明：用户榜单会强调“公开比较 + 公开个人页跳转 + 不带账号明细”，商品榜单会强调“只读展示 + 不带购买 / 订单 / 背包流程”
- 公开商城浏览首批当前只承载首页、商品列表与商品详情阅读；不把购买确认、订单、背包、举报或其他“我的”动作搬进公开壳层
- 公开商城当前也已开始补结构化只读导览：`/shop` 与 `/shop/products` 会强调“先看内容 / 继续进入 / 不在这里”，商品详情则会明确“详情重点 / 继续去向 / 工作台边界”，避免把公开浏览误读成可直接购买
- 公开 docs 搜索当前也已开始补结构化只读导览：`/docs/search` 会强调“先看结果 / 继续进入 / 不在这里”，把关键词检索、结果回跳与桌面治理边界拆清楚
- 公开入口的图片展示当前继续沿附件运行时 URL 口径：商品、榜单与社区分发页若引用仍有效的业务附件，不应再因后台清理误删而退化成前端 404 坏图
- `/discover` 首屏摘要卡当前优先表达“整卡预览本页区块 + 独立按钮直达公开页”的双层动作关系；forum / docs / leaderboard / shop 分区推荐项在窄屏下也要保持一致的信息密度与留白节奏
- Flutter forum 当前的最小登录、回流与轻互动语义也已进一步明确：详情页允许匿名用户原地发起 OIDC 登录，并在浏览器回跳后继续保留当前 `postId / commentId` 上下文；已登录壳层可读取最新 forum 通知并回到帖子 / 评论上下文；轻回应发布成功只更新轻回应墙与局部反馈，不刷新正文或评论阅读位置；但评论提交、点赞、投票、编辑、完整通知中心与系统通知栏推送仍不在当前批次内
- 第二阶段前半程不立即推翻现有 WebOS 路由，而是采用增量迁移

## 2. 系统架构

> 说明：本节的大部分代码与结构图仍然描述 **当前桌面工作台壳层的真实实现**。公开内容壳层与未来 Flutter 客户端壳层的职责分工，请优先参考 [前端多壳层策略](/frontend/shell-strategy)。

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
│  继续使用：最近应用 | 最近浏览 | 我的轻回应          │
│                                                     │
│  ┌────────────────────────────────────────────┐    │
│  │ Dock：论坛(运行中) | 聊天室(运行中)          │    │
│  └────────────────────────────────────────────┘    │
└────────────────────────────────────────────────────┘
```

### 2.2 技术架构

```
Frontend/radish.client/
├── src/
│   ├── desktop/              # 桌面系统核心
│   │   ├── Shell.tsx         # 桌面外壳（容器）
│   │   ├── StatusBar.tsx     # 顶部状态栏
│   │   ├── Desktop.tsx       # 桌面图标网格
│   │   ├── components/       # 桌面分区组件（继续使用等）
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
│   │   ├── wiki/             # 文档应用（窗口）
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

### 3.2 权限控制

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

### 3.3 桌面继续使用入口

桌面首页当前不再只是应用图标启动器，还承担已登录用户回到工作台后的轻量续接入口：

- **最近应用**：由工作台打开 / 复用应用时写入浏览器本地存储，只记录适合复访的业务应用，并排除欢迎页、组件展示、控制台、API 文档等非业务续接入口。
- **最近浏览**：复用现有浏览记录接口与桌面打开能力，优先让用户回到最近 forum 阅读上下文。
- **我的轻回应**：复用现有轻回应回看接口与帖子跳转能力，帮助用户从桌面回到已参与的社区内容。

该入口的设计边界是“回到刚才的工作”，不是完整历史中心：当前不提供清空、删除、筛选、跨端同步、混合时间线或新的后端 API。

## 4. 窗口系统

### 4.1 窗口类型

| 类型 | 说明 | 适用场景 |
|------|------|---------|
| `window` | 可拖拽、调整大小的窗口 | 文档、论坛、聊天室、商城 |
| `fullscreen` | 全屏显示，隐藏桌面 | 预留给未来特别复杂的沉浸式应用 |
| `iframe` | 嵌入外部网页 | 展示型第三方工具 |

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

当前实际行为在上述基础上已经进一步收口为：

- 新开窗口会按当前视口与应用默认尺寸自动居中，而不是固定偏移开窗。
- 普通窗口在拖动、缩放、关闭、参数切换和最大化恢复时，都会把“正常态”的位置与尺寸持久化到本地存储。
- 窗口持久化键默认按 `appId` 生成；若窗口携带业务定位参数，则按 `appId + 稳定业务参数` 生成独立记忆位。
- `__navigationKey` 等纯导航刷新参数不会参与持久化键计算，避免通知跳转或内容刷新污染同类窗口布局。
- 历史位置在当前分辨率下越界时，会先做边界压缩后再恢复，确保窗口始终落在可见区域内。

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

## 6. 移动端适配（执行中：公开内容壳层首批已落地，完整移动壳层尚未实现）

> 截至 `2026-04-12`，仓库中的 `radish.client` 仍然是桌面 / WebOS 优先架构，当前并没有真正落地的完整 `MobileShell` 实现；但 forum 公开内容壳层已继续落到 `/forum`、`/forum/category/:categoryId`、`/forum/tag/:tagSlug`、`/forum/question`、`/forum/poll`、`/forum/lottery`、`/forum/search` 与 `/forum/post/:postId`。本节描述的是“已落地事实 + 后续移动 Web 规划方向”的组合口径。

### 6.1 当前现实

- 论坛等个别页面已有窗口内响应式处理，但这不等于真正的移动端产品形态
- 当前主入口仍然是桌面 Shell、Dock 与窗口系统
- 公开内容壳层当前已完成 forum、docs、个人公开页、公开榜单与公开商城浏览五个首批入口落地；帖子列表、分类直达、搜索直达、帖子详情、公开文档目录、个人公开页、公开榜单与公开商城入口都可以绕开桌面 Shell 直接进入公开阅读形态
- Android MVP 第一轮已完成后，前端多端形态不再按“Flutter 扩所有平台”或“React WebView 统一所有端”继续推进；当前设计分工固定为 Web 浏览器公开内容壳层、Flutter 移动原生安装包、Tauri + WebOS 桌面安装包
- Tauri 桌面壳默认入口已切到 `/desktop`，用于承载 WebOS 桌面工作台；个人开发阶段安装包验证已通过，正式公开分发事项后置；`/docs` 只作为公开内容壳层与早期 spike 样例，不作为桌面安装包正式默认体验
- 公开 forum 当前只冻结“列表 + 分类 + 标签 + 结构化类型列表 + 搜索 + 详情 + 轻回应墙展示 + 评论阅读”，并明确保持只读阅读边界
- 公开文档阅读当前只冻结“目录 + 搜索 + 正文阅读 + 复制公开链接 + 返回浏览态 + 文档内链跳转”，并明确保持只读阅读边界；当前已补齐返回目录滚动位置保持、搜索结果上下文回跳、详情页复制链接入口，以及旧 `__documents__` 文档链接继续落入公开 docs 壳层
- 公开榜单当前已开始补“经验体系公开展示”这一类只读说明增强：优先解释排行依据、等级含义与公开边界，而不是直接把桌面里的“我的经验明细”搬进公开壳层
- 如果直接把完整窗口系统压缩到手机宽度，交互成本和信息密度都会失衡

### 6.2 规划策略

- 移动端应进入独立的移动壳层或移动路由模式，而不是直接复用桌面窗口交互
- 第一批已先从公开内容浏览起步：forum 列表、分类直达、搜索直达、帖子详情、轻回应墙展示与评论阅读当前已进入公开内容壳层
- 个人公开页、公开榜单与公开商城浏览首批当前都已先接入公开内容壳层，更深的轻互动能力与商城购买链路仍按价值逐步接入，不一次性照搬桌面 App
- 登录后的重交互能力按价值逐步接入，不一次性照搬全部桌面 App

### 6.3 规划示意

```typescript
// 规划示意，非当前仓库实现
const Shell = () => {
  const isMobile = useMediaQuery('(max-width: 768px)');

  if (isMobile) {
    return <MobileShell />;
  }

  return <DesktopShell />;
};
```

### 6.4 移动端布局（规划）

```
移动端目标形态：

┌────────────────────────┐
│ 状态栏 / 顶部导航        │
├────────────────────────┤
│                        │
│   当前内容页面           │
│   （论坛 / 文档 / 我）   │
│                        │
├────────────────────────┤
│ Tab: 首页|论坛|消息|我   │
└────────────────────────┘
```

```typescript
// 规划示意，非当前仓库实现
const MobileShell = () => {
  const mobileRoutes = getMobileRoutes({
    isAuthenticated,
    userRoles,
    userPermissions
  });

  return (
    <div className="mobile-shell">
      <StatusBar />
      <Routes>{mobileRoutes}</Routes>
      <MobileTabBar />
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
| HTTP 客户端 | @radish/http (统一 API 客户端) |
| 认证 | OIDC (OpenIddict) + 统一认证服务 |

### 7.1 HTTP 客户端 (@radish/http)

**统一的 API 请求封装**，从 `@radish/ui` 中独立出来，专注于 HTTP 通信。

**核心特性**：
- 统一配置管理（baseUrl、timeout、token）
- 类型安全的 TypeScript 定义
- 自动添加 Bearer Token 认证
- 请求/响应/错误拦截器
- 超时控制和错误处理

**使用示例**：

```typescript
import { apiGet, apiPost, configureApiClient } from '@radish/http';

// 配置 API 客户端
configureApiClient({
  baseUrl: 'https://localhost:5000',
  timeout: 30000,
  getToken: () => localStorage.getItem('access_token'),
});

// 发送请求
const response = await apiGet<Product[]>('/api/v1/Shop/GetProducts', {
  withAuth: true,
});

if (response.ok && response.data) {
  console.log('商品列表:', response.data);
}
```

**详细文档**：参见 [@radish/http 包文档](./http-client.md)

### 7.2 认证服务

**统一的 OIDC 认证管理**，位于 `Frontend/radish.client/src/services/auth.ts`。

**核心方法**：
- `redirectToLogin()` - 跳转到 OIDC 登录页面
- `logout()` - 执行 OIDC 登出，清除本地 Token
- `hasAccessToken()` - 检查是否有有效的 access_token

**认证流程**：

```
用户访问 → 检查 Token → 未登录 → redirectToLogin()
                ↓
            已登录 → 加载桌面 → 打开应用
                ↓
        API 请求自动添加 Token (withAuth: true)
                ↓
        Token 过期 → 401 错误 → redirectToLogin()
```

**使用示例**：

```typescript
import { redirectToLogin, logout, hasAccessToken } from '@/services/auth';

// 检查登录状态
if (!hasAccessToken()) {
  redirectToLogin();
}

// 登出
const handleLogout = () => {
  logout();
};
```

**WebSocket 认证集成**：

```typescript
import { hasAccessToken } from '@/services/auth';

// 仅在已登录时建立 WebSocket 连接
if (hasAccessToken()) {
  const token = localStorage.getItem('access_token');
  const connection = new HubConnectionBuilder()
    .withUrl('/hubs/notification', {
      accessTokenFactory: () => token || '',
    })
    .build();
}
```

**详细文档**：参见 [认证服务统一指南](../guide/authentication-service.md)

### 7.3 附件与媒体协议

前端当前已经统一采用“`attachmentId` 为真值、URL 运行时解析”的媒体口径：

- 帖子、评论、Wiki 正文中的图片 / 文档链接统一保存为 `attachment://{id}`，而不是完整 URL。
- `MarkdownEditor` 与 `RichTextMarkdownEditor` 在上传成功后统一调用 `buildAttachmentMarkdownUrl()` 写回正文。
- `MarkdownRenderer`、论坛富文本工作区、聊天室图片预览等展示链路统一通过 `buildAttachmentAssetUrl()` / `resolveConfiguredMediaUrl()` 解析当前环境下的真实访问地址。
- `AttachmentVo.voUrl` / `voThumbnailUrl`、`StickerVo.voImageUrl` / `voThumbnailUrl`、`ProductVo.voIcon` / `voCoverImage`、`ChannelMessageVo.voImageUrl` / `voImageThumbnailUrl` 都属于运行时派生展示字段，不是前端回写数据库的依据。

推荐模式如下：

```typescript
import {
  buildAttachmentAssetUrl,
  buildAttachmentMarkdownUrl,
  parseAttachmentMarkdownUrl,
} from '@radish/ui';

const markdownUrl = buildAttachmentMarkdownUrl(result.attachmentId, {
  displayVariant: 'thumbnail',
  scalePercent: 60,
});

const parsed = parseAttachmentMarkdownUrl(markdownUrl);
const previewUrl = parsed
  ? buildAttachmentAssetUrl(parsed.attachmentId, parsed.displayVariant)
  : null;
```

这套约束的目标只有一个：前端可以跟随当前 `baseUrl`、Gateway、反向代理与部署域名自然切换，而不会把旧域名硬编码进业务数据。

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

### 8.1.1 当前主题实现落点

截至 `2026-03-22`，`radish.client` 的首轮主题实现已经从概念层进入代码落地，当前结构为：

```text
Frontend/radish.client/src/theme/
  theme.ts
  theme-tokens.css
  ThemeProvider.tsx
  useTheme.ts

Frontend/radish.client/src/stores/
  themeStore.ts
```

当前已接入的桌面壳层范围：

- `Shell`
- `Dock`
- `Desktop`
- `DesktopResumePanel`
- `DesktopWindow`
- `WelcomeApp`（当前已完成主题 token 接入、内容口径重写与游客安全打开）

当前已完成首轮文案资源化的高频范围：

- 桌面壳层语言切换入口、应用注册、桌面图标与窗口标题
- 商城首页、商品列表 / 详情、订单列表 / 详情、背包、购买弹窗与购买动作提示
- 论坛帖子列表、帖子详情讨论区、评论树、评论表单、评论卡片、帖子卡片，以及分类列表、帖子编辑 / 历史弹窗等边缘页
- 聊天室高频消息链路、重连状态提示与成员区核心文案
- 通知中心列表、空状态、已读 / 删除动作与共享通知卡片
- 个人中心高频模块：资料核心信息、浏览记录、关系链、帖子 / 评论列表、头像上传、附件列表、钱包与交易记录
- 文档应用主链：应用头部、文档目录、详情阅读与基础状态文案

当前已完成一轮体验重排的高频编辑链路：

- 论坛发帖入口已从传统表单弹窗重构为“创作器工作区”，支持 `Markdown / 富文本` 双模式输入、右侧帖子设置区、全屏创作与“富文本输入体验 + Markdown 统一存储”的编辑策略
- 论坛评论入口已重构为轻量讨论面板：评论编辑器采用紧凑卡片式结构，支持 `@提及`、贴图、图片、附件与预览切换；评论弹层外框也已单独收口，避免继续复用过厚的通用白板样式
- 正文中的图片 / 附件当前统一保存为 `attachment://{id}`，显示时再通过 `buildAttachmentAssetUrl()` 解析为当前环境可访问的资源地址，从业务层面解除对部署域名的强依赖

当前约束：

- 新增 UI 改造优先复用主题 Token，不再继续增加硬编码颜色；
- 主题切换必须由根级主题状态驱动，不在单页面各自维护；
- 桌面层、窗口层与 Dock 层必须单独控制定位与层级，避免视觉改造破坏基础交互布局；
- 页面级适配按“论坛 -> 聊天室 -> 商城 -> 通知中心 -> 个人中心 -> 文档”的高频顺序推进。

当前验证结论：

- 通知中心、个人中心与桌面壳层已完成首轮语言切换与主题切换烟雾验证；
- `validate:baseline --quick` 已通过；
- `npm run build --workspace=radish.client` 已在系统环境构建通过；
- 桌面余额 / 经验值状态文案、Dock 少量交互色与个人中心尾部样式已完成当前批次收口。
- 最新一轮手工联调已确认：`Shell` 的窗口层点击拦截已修复，Dock 顶部定位与状态按钮可读性已重新对齐，桌面图标已回到轻量图标 + 文案的列式排布，避免固定网格造成滚动列表感。
- 欢迎页当前已完成主题适配、长文案双语资源化与游客安全模式；未登录时也可直接打开，并展示游客 badge、说明与登录 CTA，不再依赖先拿到用户名。

### 8.1.2 当前 i18n 实现落点

截至 `2026-03-22`，`radish.client` 的 `i18n` 已从“仅有基础设施”进入“高频主链路首轮接入”阶段：

- 桌面层已提供可见的语言切换入口，并把应用注册、桌面图标与窗口标题统一收敛到翻译键；
- 商城已完成首页、商品、订单、背包、购买弹窗与购买动作提示的首轮资源化；
- 论坛已完成帖子列表、帖子详情讨论区、评论树、评论表单、评论卡片与帖子卡片的首轮资源化；
- 论坛发帖创作器与评论编辑器的高频模式切换、上传动作与讨论区主操作文案已继续保持在统一翻译键口径内；
- 聊天室已完成高频消息链路、重连 / 断线状态提示与成员区核心文案的首轮资源化；
- 通知中心已完成列表、空状态、加载态、已读 / 删除动作与共享通知组件的首轮资源化；
- 个人中心已完成资料核心信息、浏览记录、关系链、帖子 / 评论列表、头像上传、附件列表、钱包与交易记录的首轮资源化；
- 当前自动化验证已确认：`npm run type-check --workspace=radish.client` 通过，`validate:baseline --quick` 通过，`npm run build --workspace=radish.client` 在系统环境构建通过。

当前后续关注范围：

- 当前首轮主题 / i18n 主链已基本闭合，但仍需在更大范围首版联调中继续观察宿主层边缘页、失败兜底与次级操作；
- 论坛宿主层之外的少量残余回退文案与深层交互边角仍需在联调时顺手清理；
- 与主题联动的残余深层样式 token 仍需继续收口，避免出现“文案已国际化但样式仍绑定单语长度或硬编码颜色”的问题；
- 欢迎页已完成主题与主体长文案的双语资源化，后续重点转为观察语言切换下的排版稳定性与少量边角文案；
- 当前阶段口径已从“继续补高频主链”转为“待联调复核”。

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
// 使用 @radish/ui 封装的 Icon 组件（基于本地 Iconify JSON 集合）
import { Icon } from '@radish/ui/icon';

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
/forum/tag/community-news - 公开标签页（canonical slug）
/forum/question - 公开问答列表
/forum/poll - 公开投票列表
/forum/lottery - 公开抽奖列表
/forum/post/123 - 论坛帖子详情
/docs - 文档目录
/docs/getting-started - 文档详情
/u/123 - 公开个人页
/leaderboard - 公开榜单首页 / 默认经验榜单页（canonical；兼容 /leaderboard/experience 收口）
/leaderboard/hot-product - 公开榜单类型页
/forum/category/12 - 公开分类页
/shop - 公开商城首页
/shop/products - 公开商品列表
/shop/product/123 - 公开商品详情
/chat - 聊天室
/admin/apps - 后台应用管理
```

### 10.4 应用集成架构决策

#### 10.4.1 三种应用类型的选择标准

Radish WebOS 支持三种应用集成方式,选择标准如下:

| 应用类型 | 判断标准 | 适用场景 | 示例 |
|---------|---------|---------|------|
| **内置应用 (Built-in)** | - 无需独立部署<br>- 无复杂路由<br>- 可共享认证状态 | - 简单功能模块<br>- 用户高频使用<br>- 需要与桌面深度集成 | Forum(论坛)<br>Chat(聊天)<br>Settings(设置) |
| **窗口应用 (Window)** | - 需要统一权限与状态<br>- 支持固定文档与在线文档混合展示<br>- 支持编辑/导入/导出 | - 文档<br>- 论坛<br>- 设置 | Document(文档)<br>Forum(论坛) |
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
// Frontend/radish.client/src/desktop/AppRegistry.tsx

// ✅ 内置应用 - 论坛
{
  id: 'forum',
  name: '论坛',
  icon: 'mdi:forum',
  component: ForumApp, // React 组件
  type: 'window',
  defaultSize: { width: 1200, height: 800 }
}

// ✅ 内置窗口应用 - 文档
{
  id: 'document',
  name: '文档',
  icon: 'mdi:book-open-page-variant',
  component: WikiApp,
  type: 'window',
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
    : 'http://localhost:3100' // 直接访问
}
```

**共享组件策略**:

```
@radish/ui (共享 UI 组件库)
    ↓
┌───┴────┬─────────┬─────────┐
│        │         │         │
Client  Console   Shop    Document
```

- 基础组件(Button, Input, Modal)放在 `@radish/ui`
- 业务特定组件各自维护
- 通过 npm workspaces 实现热更新

### 10.5 公开内容 SEO 与分享基线

> 仅 `radish.client` 的公开内容壳层需要对搜索引擎和外链分享友好；`radish.console`、登录后 WebOS 工作台和治理类页面默认不做 SEO 要求。

#### 10.5.1 当前公开 URL 范围

- 公开内容壳层当前覆盖：
  - `/discover`
  - `/forum`、`/forum/search`、`/forum/category/:categoryId`、`/forum/tag/:tagSlug`、`/forum/question`、`/forum/poll`、`/forum/lottery`
  - `/forum/post/:postId`
  - `/docs`、`/docs/search`、`/docs/:slug`
  - `/u/:userId`
  - `/leaderboard`
  - `/shop`、`/shop/products`、`/shop/product/:productId`
- 登录后功能不进入公开 SEO 范围，例如发帖、编辑、订单、背包、设置、Console 与治理后台。
- 桌面工作台入口继续由 `/desktop` 承载；公开 URL 命中时优先渲染公开内容壳层，而不是完整 WebOS 桌面 Shell。

#### 10.5.2 当前 head 契约

`Frontend/radish.client/src/public/publicHead.ts` 是公开壳层的统一 head helper。公开页面进入、路由切换和详情数据加载后，应通过该 helper 统一维护：

- `document.title`
- `meta[name="description"]`
- `link[rel="canonical"]`
- `meta[property="og:title"]`
- `meta[property="og:description"]`
- `meta[property="og:url"]`
- 可选 `meta[property="og:image"]`

`Frontend/radish.client/index.html` 只提供 SPA 首包的中文语言、站点默认 title、description 与 Open Graph 基线；页面级标题、摘要、canonical 和分享 URL 由运行时 head helper 根据公开路由补齐。

#### 10.5.3 canonical 与分享入口

- canonical URL 当前统一以 `https://radishx.com` 作为公开域名 seed。
- `buildPublicCanonicalUrl()` 只接受规范化后的公开路径，不把登录后工作台路径写入 canonical。
- forum detail、shop detail 和 docs detail 都应复制 canonical 公共链接，而不是复制当前浏览器里可能带有临时状态、来源参数或工作台上下文的 URL。
- forum detail 当前仍使用 long 版 `/forum/post/:postId` 作为兼容 canonical；`P3-2` 之后若 `PostVo.VoPublicId` 可用，应逐步切到 `/forum/post/:postPublicId`，并继续兼容旧 long 链接。

#### 10.5.4 robots 与 sitemap seed

- `Frontend/radish.client/public/robots.txt` 当前作为静态抓取入口约束，允许公开内容壳层，禁止 Console、认证回调、桌面工作台和登录后治理类路径。
- `Frontend/radish.client/public/sitemap.xml` 当前是静态 seed，只包含公开入口和公开列表页，不包含动态详情页。
- 动态详情 sitemap、按热度 / 时间生成详情 URL、结构化数据和详情首包 HTML 可见性仍是后续专题，不并入当前 SPA 运行时 head 基线。

#### 10.5.5 SSR / SSG 后置边界

当前已落地的是“静态可见 + SPA 运行时一致”的公开内容基线，不要求立即改成 SSR / SSG。SSR / SSG、JSON-LD 和详情首包完整 HTML 属于后续增长专题，需要与后端查询、缓存、部署和爬虫策略一起评估。

若后续进入 SSR / SSG 专题，可选方向包括 Vite SSR、Next.js、Remix 或 Astro；但在正式立项前，前端不得假定存在 `start:ssr` 脚本或 Node SSR 服务。

#### 10.5.6 结构化数据后置示例

后续若为帖子详情页输出 JSON-LD，可参考：

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

## 11. 迭代与交付（导航）

- 里程碑、按周计划与当前进度：以 [开发路线图](/development-plan) 为准
- 具体周更与变更记录：以 [开发日志](/changelog/) 为准
- 本文档仅描述前端架构与设计约束；若迭代中出现影响架构的关键决策，请在本文追加“设计决策”小节并在开发日志中记录。

## 12. 构建拆包策略（manualChunks + 动态导入）

为降低首屏负载并提升 WebOS 子应用的按需加载体验，`radish.client` 采用如下策略：

### 12.1 动态导入（按应用懒加载）

- 在 `src/main.tsx` 中使用 `React.lazy + Suspense`，按入口场景懒加载 `App` / `Shell`。
- 在 `src/desktop/AppRegistry.tsx` 中将窗口应用改为懒加载注册，打开窗口时再下载对应子应用代码。
- 目标：避免一次性加载论坛、商城、个人中心、萝卜坑等所有应用资源。

### 12.2 手动分包（manualChunks）

- 在 `vite.config.ts` 中启用 `build.rollupOptions.output.manualChunks`。
- 将常见基础依赖与大体积依赖拆分为稳定 vendor chunk（如 React、i18n、window、markdown 等）。
- 对 WebOS 业务子应用按目录进行应用级 chunk 切分（如 `app-forum`、`app-shop`、`app-radish-pit`）。

### 12.3 结果与后续

- 构建结果已从“单一超大入口包”转为“入口小包 + 子应用懒加载包”结构。
- `ExperienceDetailApp` 已采用图表二级懒加载（`LineChart/PieChart`），将大图表依赖从应用主包中分离。
- 论坛应用新增二级懒加载：`PublishPostModal` / `EditPostModal` / `PostDetailContentView` 在触发时再加载；发帖弹窗内 `MarkdownEditor` 与预览 `MarkdownRenderer` 也改为按需加载。
- 论坛详情视图继续细分为 `forum-detail-view`（壳层）、`forum-detail-post`（正文）、`forum-detail-comments`（评论），并将 Markdown 生态依赖统一归入分包规则。
- `ProfileApp` 新增 Tab 内容按需加载与头像裁切弹窗懒加载，避免在个人页首屏静态打入附件/裁切相关代码。
- `@radish/ui/Icon` 改为加载 `mdi` 子集（`mdi-subset.json`）并按需异步注册，避免引入整份图标数据。
- `@radish/ui` 在 `package.json` 增加组件子路径导出（如 `icon`、`toast`、`modal`、`input`、`select`、`bar-chart`、`area-chart` 等），client 侧优先使用子路径导入，降低 barrel export 连带打包风险。
- 最新构建（2026-02-08）中，`app-profile` 已从约 `792.80 kB` 降至约 `59.12 kB`，`app-forum` 约 `42.30 kB`。
- 发帖弹窗主 chunk 进一步收敛：`forum-publish-modal` 从约 `341.12 kB` 降至约 `10.36 kB`，编辑器与渲染器拆至独立异步 chunk（`MarkdownEditor` / `MarkdownRenderer`）。
- 萝卜坑已完成页级拆分：`app-radish-pit` 约 `25.32 kB`，并拆出 `pit-transfer` / `pit-history` / `pit-security` / `pit-statistics` 独立 chunk。
- Showcase 已从桶导入迁移到子路径导入，`app-showcase` 从约 `751.21 kB` 降至约 `410.06 kB`。
- `forum-detail-view` 已从约 `349.00 kB` 拆分为：`forum-detail-view` 约 `5.49 kB`、`forum-detail-post` 约 `3.91 kB`、`forum-detail-comments` 约 `19.85 kB`。
- 当前已无超过 500k 的业务 chunk。

## 13. 参考资料

- Nebula OS 原型：`public/webos.html`
- 窗口拖拽：react-rnd
- macOS Big Sur 设计规范
- Windows 11 设计规范

---

> 本文档是 Radish 前端架构与 WebOS 交互范式的事实来源；里程碑/进度请以 [开发路线图](/development-plan) 与 [开发日志](/changelog/) 为准。
