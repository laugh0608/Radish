# Console 技术架构

> 本文档是 [Console 管理后台系统设计方案](/guide/console-system) 的第 4 章

## 4. 技术架构

### 4.1 前端架构

#### 4.1.1 技术栈

| 技术 | 用途 |
|------|------|
| React 19 | UI 框架 |
| TypeScript | 类型系统 |
| React Router | 路由系统 |
| Vite / Rolldown | 构建工具 |
| `@radish/ui` | 统一 UI 组件与无宿主语言状态的共享展示能力 |
| `@radish/http` | 统一 API 客户端与结构化错误能力 |

#### 4.1.2 当前目录关注点

```text
Frontend/radish.console/src/
├── api/                # 后台 API 封装
├── components/         # 共享布局与通用组件
├── constants/          # Console 权限常量等
├── hooks/              # usePermission、标题等 Hook
├── i18n/               # Console 语言状态 Provider
├── locales/            # zh / en 分业务域资源与 registry
├── pages/              # 业务页面
├── router/             # 路由定义、权限元数据、RouteGuard
├── services/           # token / 认证上下文能力
└── utils/              # logger 等工具
```

#### 4.1.3 路由与权限元数据

当前 Console 已切换到 **React Router + 路由元数据驱动**：

- 路由定义中声明页面访问权限
- `RouteGuard` 统一处理页面访问边界
- 菜单和全局搜索复用同一份路由元数据
- 页面内部不再重复写页面级“无权限占位返回”

这解决了过去“菜单隐藏了，但搜索或直链还能进入”的裂缝。

#### 4.1.4 页面内部权限职责

页面内部当前只负责两类判断：

1. **按钮/操作级可见性**
   - 例如编辑、删除、重试、批量上传
2. **无访问权限时停止请求**
   - 避免仅由路由层兜底，但页面 effect 仍先发请求

#### 4.1.5 页面结构与样式分层

Console 当前页面结构按“页面类型”选择布局基座，而不是所有页面共用同一个模板：

| 页面类型 | 推荐结构 | 当前典型页面 |
| --- | --- | --- |
| 调度总览 | 顶部指标 + 快捷操作 + 最近事项 + 右侧调度入口 | `Dashboard` |
| 表格 CRUD | 指标条 + 筛选 / 工具条 + 表格 + 右侧摘要栏 | `UserList`、`TagList`、`CategoryList`、`RoleList`、`Applications`、`StickerGroupList`、`StickerList`、`Products`、`Orders` |
| 设置 / 配置 | 分组导航 + 设置列 + 影响范围摘要 | `Settings`、`UserProfile`、`SystemConfigList` |
| 权限配置 | 页面标题 + 授权指标 + 主配置树 + 权限预览 | `RolePermissionPage` |
| 详情页 | 标题卡 + 指标 + 详情分区 + 右侧摘要 | `UserDetail`、`OrderDetail`、`ProductDetail` |
| 工具型页面 | 查询工具条 + 主操作区 + 右侧说明 / 摘要 | `CoinAdminPage` |
| 治理工作台 | 队列 / 证据详情 / 动作留痕 | `ModerationPage`、`ExperienceAdminPage`、`DocumentGovernancePage` |

样式分层遵循：

- `index.css`：Console 根级 `--console-*` token、全局 box model 和基础背景。
- `AdminLayout.css`：后台壳层、侧栏、顶栏、内容区和响应式承载。
- `adminFeature.css`：功能页通用结构，例如页面容器、卡片、标题区、指标、工具条、表格布局、表格滚动区、分页换行、设置布局、详情布局和摘要栏。
- `adminForm.css`：商品、分类、贴纸和贴纸分组等深层表单的上传预览、隐藏输入、全宽 / 半宽控件、弱提示和弹窗 footer 动作区。
- `routerComponents.css`：路由认证中、无 Console 权限和懒加载状态。
- 页面 CSS：仅放该页面不可复用的业务布局或特殊状态。

新增或明显改动页面时，应先判断页面类型，再复用 `AdminLayout`、`Breadcrumb`、`adminFeature.css` 与 `--console-*` token；只有业务信息结构确实不同，才在页面 CSS 中补局部样式。页面结构调整不得改变 API、权限、表单字段、数据契约或业务语义。

#### 4.1.6 表格与弹层布局约束

Console 表格基于 Ant Table / `@radish/ui` 能力承载，但页面层必须负责容器和响应式边界：

- 主表格页放入 `admin-table-panel`，需要右侧摘要时使用 `admin-table-layout` / `admin-table-aside`。
- Dashboard、详情页、弹窗、抽屉或批量上传流程中的内嵌表格放入 `admin-table-scroll-region` 或页面等价滚动容器，避免撑破父级。
- 固定右侧操作列必须给稳定宽度，操作按钮组使用可换行布局；中宽 PC 和移动窄屏下不允许按钮互相遮挡。
- 分页器允许换行；筛选工具条和分页在窄屏下可以上下排列，但不能覆盖表格内容。
- 表格列里的状态色、金额正负、弱文本和预格式化备注通过 CSS class 与 `--console-*` token 表达，不在 render 函数内写 inline 色值。

#### 4.1.7 多语言与展示层契约

Console 独立维护 `core / shell / dashboard / users / moderation / orders / products / settings / documents` 中英文资源，不引用 Client 资源文件。路由标题、面包屑、全局搜索和业务页共享同一 registry，并通过 `radish_lang` 响应同设备语言偏好。

业务页面应把展示映射收口到可测试的 presentation helper：稳定枚举、Code 或 key 决定状态、筛选与控制流，`vo*Display` 只作兼容回退；未知稳定词元保留原值。用户资料、商品内容、订单备注、治理原因、系统设置值和文档正文等配置型或人工内容保持原文。日期、数字、金额和英文数量规则按当前 locale 展示，外部 LongId 始终保持字符串。

### 4.2 API 客户端与特殊上传场景

#### 4.2.1 统一 API 客户端

普通 API 调用统一使用 `@radish/http` / `@radish/ui` 提供的客户端，不再维护 Console 自定义 fetch 封装。

统一收益包括：

- token 注入一致
- 响应解析一致
- 错误处理口径一致
- baseUrl 配置一致

业务 API helper 在失败时使用 `createApiResponseError` 抛出 `ApiResponseError`，保留 `httpStatus / code / messageKey / traceId`。页面控制流读取 status、稳定 `Code` 或结构化数据状态；提示优先使用本地 `MessageKey`，缺键时显示服务端安全 `MessageInfo`，禁止匹配中英文消息判断权限、not-found 或并发冲突。

#### 4.2.2 特殊场景：上传进度

像 `Sticker` 图片上传这类需要上传进度的场景，允许使用 `XMLHttpRequest`，但必须：

- 从 `getApiClientConfig()` 获取 `baseUrl` 与 token
- 只在上传等确有必要的场景使用
- 不再额外复制一套普通 HTTP 客户端逻辑

### 4.3 后端权限快照组装

后端当前通过以下步骤组装 Console 权限：

```text
CurrentUser roles
  ↓
System/Admin 默认权限全集
  ↓
RoleModulePermission 查询角色资源
  ↓
ApiModule.LinkUrl
  ↓
ConsolePermissions.GetPermissionsByApiUrl(...)
  ↓
CurrentUserVo.VoPermissions
```

### 4.4 `DbMigrate` 在权限治理中的职责

`DbMigrate` 当前承担两类工作：

1. 创建 Console 已依赖的 `ApiModule`
2. 为默认角色补齐 `RoleModulePermission` 种子

因此，新增一个 Console 能力时，至少要检查四处是否一致：

- 路由/页面是否真实调用
- 前端权限常量是否存在
- `ConsolePermissions` 是否有 URL 映射
- `DbMigrate` 是否有资源与默认授权种子

### 4.5 特殊入口

#### 4.5.1 Hangfire

`Hangfire` 不属于普通 React 页面资源，但它已经被纳入 Console 权限治理：

- 资源映射：`/hangfire(/.*)?`
- 权限键：`console.hangfire.view`
- 校验方式：`HangfireAuthorizationFilter` 显式消费当前用户权限快照
- React 侧通过 `SystemTools/HangfirePage` 承载外层页头、指标和 iframe 容器；该页面只负责受保护 Dashboard 外壳，不扩展为项目内任务队列、失败重试或运行审计平台

这类入口后续继续沿用“显式校验权限快照”的策略。

---

## 相关文档

- [Console 权限治理 V1](/guide/console-permission-governance)
- [Console 核心概念](/guide/console-core-concepts)
- [Console 样式与 Token 使用说明](/frontend/console-style-guide)
