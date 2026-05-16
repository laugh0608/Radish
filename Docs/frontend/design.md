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
- Android / iOS 移动安装包继续以 Flutter 为主线；Capacitor Android spike 已清理出当前代码，只保留历史记录作为公开只读 React 页面复用的技术参考，不进入登录态移动端产品化路线
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

当前口径：

- WebOS 应用统一通过应用注册表声明图标、窗口类型、权限和入口组件
- 匿名可打开公开应用；登录后解锁聊天、个人中心等用户能力
- Console 仍作为外部后台入口，不嵌入 WebOS 窗口
- 最近应用、最近浏览、我的轻回应共同承接“继续使用”入口

应用注册、权限控制和继续使用入口细节见 [WebOS 应用注册与窗口系统](/frontend/webos-shell-architecture)。

## 4. 窗口系统

当前口径：

- 窗口状态集中管理，避免各应用自行维护窗口生命周期
- 普通窗口、工具窗口、外部应用入口按场景区分
- 最小化窗口不渲染内容，避免无意义资源占用

窗口类型、窗口管理器和状态管理细节见 [WebOS 应用注册与窗口系统](/frontend/webos-shell-architecture)。

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

当前技术栈总览：

- React 19 + Vite（Rolldown）+ TypeScript
- npm workspaces 管理 `radish.http`、`radish.client`、`radish.console`、`radish.ui`
- API 客户端统一使用 `@radish/http`
- WebOS 桌面工作台、公开内容壳层、Console 后台和 Tauri 桌面壳按职责分工推进

专题细节见 [前端技术栈细节](/frontend/technical-stack)、[@radish/http](/frontend/http-client) 与 [前端 workspace 开发指南](/frontend/development)。

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

### 8.1.1 当前主题与 i18n 落地

当前口径：

- `radish.client` 主题状态由根级主题能力驱动
- 新增 UI 改造优先复用语义 token，不继续扩硬编码颜色
- 高频桌面壳层、商城、论坛、聊天、通知、个人中心和文档应用已完成首轮主题 / i18n 接入
- 后续只在真实联调中处理残余边角，不在设计入口继续追加流水

主题与 i18n 落地细节见 [前端主题与 i18n 落地记录](/frontend/theme-i18n-implementation)、[视觉主题规范](/frontend/visual-theme-spec) 与 [视觉色彩参考](/frontend/visual-color-reference)。

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

组件资源库不再在设计入口中维护完整清单。当前入口只约束：

- 共享组件优先沉淀到 `@radish/ui`
- 业务壳层组件留在对应 workspace
- 视觉 token 与主题口径以 [视觉主题规范](/frontend/visual-theme-spec) 和 [视觉色彩参考](/frontend/visual-color-reference) 为准

组件库细节见 [@radish/ui 组件库](/frontend/ui-library)、[组件开发指南](/frontend/components) 与 [UI 组件资源库专题](/frontend/ui-component-resource-library)。

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

当前集成结论：

- WebOS 内置应用用于高频工作台能力
- Console 保持独立后台，不嵌入 WebOS
- 外部工具通过受控跳转进入，不在桌面壳内强行 iframe 化

完整决策依据、选择标准和最佳实践见 [前端应用集成架构决策](/frontend/app-integration)。

### 10.5 公开内容 SEO 与分享基线

当前公开内容基线：

- 公开路由输出运行时 head 与 canonical
- forum / shop 详情提供 canonical 复制入口
- `robots.txt` 与 `sitemap.xml` 先保留 seed 级入口
- 动态 sitemap、结构化数据、SSR / SSG 后置到真实增长需要出现后再评估

完整 URL 范围、head 契约和后置边界见 [公开内容 SEO 与分享基线](/frontend/public-seo-sharing)。

## 11. 迭代与交付（导航）

- 里程碑、按周计划与当前进度：以 [开发路线图](/development-plan) 为准
- 具体周更与变更记录：以 [开发日志](/changelog/) 为准
- 本文档仅描述前端架构与设计约束；若迭代中出现影响架构的关键决策，请在本文追加“设计决策”小节并在开发日志中记录。

## 12. 构建拆包策略

当前策略：

- 应用入口继续按动态导入懒加载
- 第三方库和高频应用按 `manualChunks` 拆分
- chunk size warning 只按真实加载性能和复用收益治理，不为消除提示做无边界拆分

拆包细节、当前结果和后续边界见 [前端构建拆包策略](/frontend/build-chunking)。

## 13. 参考资料

- Nebula OS 原型：`public/webos.html`
- 窗口拖拽：react-rnd
- macOS Big Sur 设计规范
- Windows 11 设计规范

---

> 本文档是 Radish 前端架构与 WebOS 交互范式的事实来源；里程碑/进度请以 [开发路线图](/development-plan) 与 [开发日志](/changelog/) 为准。
