# Console 管理后台系统设计方案

> 版本：v1.16 | 最后更新：2026-06-30 | 状态：Console 权限治理、页面类型视觉基座、表格交互布局、外部 LongId 字符串安全、角色授权保存守护、系统设置 / 商品管理版本语义、用户排障到内容治理、商品 / 订单 / 流水排障回流、移动壳层、会话刷新窗口和系统设置治理入口已收口，后续新增或明显改动页面按页面类型小步对齐

## 拆分说明

Console 管理后台系统文档较长，为便于阅读已拆分为多篇文档；章节编号保持不变。

## 文档导航

- [2. 核心概念](/guide/console-core-concepts) - Console 角色定位、权限快照、资源映射模型
- [3. 功能模块](/guide/console-modules) - 模块状态、页面边界、权限归属
- [4. 技术架构](/guide/console-architecture) - React Router、RouteGuard、权限快照与 API 集成
- [5. 实施计划](/guide/console-roadmap) - 当前阶段、收口策略与后续治理入口
- [Console 权限治理 V1](/guide/console-permission-governance) - 当前最重要的治理专题文档
- [Console 权限覆盖矩阵](/guide/console-permission-coverage-matrix) - 路由 / 前端权限 / 后端映射 / 种子的对照表
- [Console 权限 / 菜单 / 按钮管理一期设计方案](/guide/console-authorization-phase1) - `Console-ext` 第一阶段的专题设计文档
- [系统设置治理专题](/guide/system-settings-governance) - 设置定义、默认值、覆盖值、风险等级、二次确认与审计规则

---

## 1. 系统概述

Console 管理后台是 Radish 社区的统一管理平台，面向具备授权的后台治理角色提供管理能力。当前阶段的重点不是继续横向扩张为独立后台群，而是在同一套 Console 内把既有管理页面的权限边界、前后端授权口径、资源种子、Console 入口权限和页面类型视觉基座稳定收口，并把分类、内容治理、胡萝卜、经验等级、商城治理、应用 / 表情管理等已落地后台能力纳入统一模型。

当前认证接入方式已经明确为：Console 通过独立的 `radish-console` OIDC 客户端接入 Auth，拥有独立的授权回调与登出回调链路；它不再沿用早期“作为 `radish-client` 子应用复用认证”的旧口径。

### 1.1 核心设计原则

- **安全优先**：前端负责可见性，后端负责最终授权，特殊入口按权限快照而非角色硬编码放行
- **入口与能力分离**：`console.access` 是 Console 入口标记，但不再单独构成可见性；普通角色必须同时具备至少一个真实 Console 页面访问权限才允许进入
- **边界清晰**：只把真实已落地能力纳入权限模型，不为未完成能力保留伪入口
- **文档同源**：规划、架构、README、开发日志对当前进展使用同一口径
- **渐进收口**：优先补齐真实页面依赖的资源映射与种子，而不是一次性设计完整后台权限平台
- **后台统一承载**：举报审核、治理日志、分类管理、标签管理、胡萝卜管理、经验等级等管理功能统一集成在同一 Console 内，不额外拆独立 App
- **页面类型优先**：新增或明显改动页面按“工作台 / 表格 CRUD / 设置配置 / 调度总览 / 详情 / 工具型”选择布局基座，不把所有后台页面硬套成同一个模板
- **外部 ID 字符串安全**：用户 ID、订单 ID、商品 ID、通知 ID 等服务端 long 标识在前端按字符串传递，不进入 JavaScript `number` 精度域

### 1.2 当前业务边界

**当前已形成闭环的模块**：

- `Dashboard`
- `Applications`
- `Users`
- `Roles`
- `Categories`
- `Products`
- `Orders`
- `Tags`
- `Stickers`
- `Moderation`
- `Coins`
- `Experience`
- `SystemConfig`
- `Hangfire`

**当前重点不是继续扩张的新模块**：

- 审计日志中心
- 系统监控总览
- 权限配置后台
- 独立的权限树编辑器

**当前明确按“首版已接入”收口的治理边界**：

- 内容治理当前已集成到 Console 的 `Moderation` 页面，不单独拆治理 App
- 当前接入对象包含：`Post`、`Comment`、`PostQuickReply`、`ChatMessage`、`Product`
- 审核台当前已支持聊天消息定位、帖子评论 / 轻回应回看与失效降级，并区分“举报创建时快照”与“当前可回看状态”
- 审核队列、手动治理表单与治理动作日志当前已形成同页工作流，可从举报单或历史动作一键带入目标用户、来源举报单与解除动作建议，并直接查看目标用户当前生效中的禁言 / 封禁状态
- 用户详情页当前可带目标用户跳转到内容治理工作台；`Moderation` 通过 URL 状态承接用户过滤，便于从用户排障继续查看该用户相关举报、手动治理和治理动作日志
- 审核队列与治理动作日志的筛选应在服务层组合数据库表达式；关键词、状态、目标用户、来源举报单和动作类型等条件不要回退到前端或内存过滤，避免 SQLite / PostgreSQL 行为漂移
- 审核台仍以人工审核、禁言 / 封禁联动和治理日志为主；批量治理、敏感词、自动策略仍后置
- 商城治理当前已支持商品详情 / 订单详情真实加载、商品删除拦截，以及订单 / 商品 / 用户 / 扣款流水之间的治理回跳，优先服务资产与订单人工追查
- 商品编辑、上架和下架当前使用 `Product.Version` 乐观并发语义；Console 请求携带 `ExpectedVersion`，服务端版本不匹配时拒绝覆盖并要求刷新。
- 商品、订单和胡萝卜流水排障回流使用同源相对 `returnTo`，关闭详情、分页、筛选和重置时保留合法来源；外部 LongId 查询参数保持字符串
- 角色授权保存中会禁用保存入口并阻止重复提交，避免同一旧 `expectedModifyTime` 被连续请求触发伪并发冲突
- 系统设置页当前只展示代码注册的设置定义，保存覆盖值或恢复默认时提交 `ExpectedVersion`，并写入修改原因 / 确认参数和变更审计；Low / Medium 可按后端规则编辑，High / Critical 不开放编辑
- 审核队列处理只允许从 `Pending` 状态条件更新；同一举报单被其他管理员先处理后，后续提交会被拒绝并提示刷新审核队列。

**当前页面体验边界**：

- Console 仍是独立后台入口，不嵌入 WebOS 窗口，也不复刻公开内容壳层。
- 当前视觉方向采用 `Case Desk`：低饱和暖灰 / 纸色底、轻侧栏、克制边框、清晰按钮层级和可扫描的工作台信息密度。
- 普通列表页优先使用“指标条 + 筛选 / 工具条 + 表格 + 右侧摘要栏”；设置 / 配置页优先使用“分组导航 + 设置列 + 影响范围摘要”；治理页才使用“队列 / 详情 / 动作”的工作台结构。
- `AdminLayout` 在移动端继续保持 Console 独立后台形态：窄屏侧栏收敛为 64px，不覆盖主内容；业务页需要自行保证表格、工具条和详情摘要在剩余宽度内可滚动或折叠。
- 表格 CRUD、治理流水、详情内嵌表格和弹窗 / 抽屉表格统一采用局部横向滚动、操作区换行和分页换行策略；不能通过扩大页面最小宽度、固定超宽弹窗或压缩按钮文字来掩盖布局问题。
- Console access token / refresh token 由统一 `tokenService` 管理；保存 token 时写入 `expires_at` 与 `refresh_at`，自动刷新依据动态缓冲窗口触发，页面层不应再维护独立刷新计时。
- 已对齐的当前页面包括 `Dashboard`、`Settings`、`UserList`、`TagList`、`CategoryList`、`SystemConfigList`、`RoleList`、`Applications`、`StickerGroupList`、`StickerList`、`ProductList`、`OrderList`、`CoinAdminPage`、`RolePermissionPage`、`UserDetail`、`UserProfile`、`ModerationPage`、`ExperienceAdminPage`、`DocumentGovernancePage`、`OrderDetail`、`ProductDetail` 与 `HangfirePage` 外壳。
- 上述页面迁移只调整视觉结构与信息组织，不改变 API、权限、表单字段、数据契约或业务语义。

### 1.3 当前阶段定义

当前 Console 处于：

- **所属里程碑**：`P3-12 Web 完全化与 WebOS 收束 / D 统一 UI 设计专题`
- **当前主线**：`Console 设计稿到实现的页面类型对齐与差距矩阵复核`
- **阶段状态**：已完成角色授权页、资源映射、真实接口收口、`console.access` 入口权限收口、Console 权限种子一致性校验、高频后台页面的页面类型基座迁移、治理工作台外层 / 内部样式收口、系统工具外壳、深层表单、详情 / 抽屉和表格交互布局治理；后续先按设计源 / 页面族 / 验收项建立差距矩阵，再决定成组代码治理或运行态验收

### 1.4 推荐阅读顺序

如果是首次接手 Console 权限治理，建议按以下顺序阅读：

1. [Console 核心概念](/guide/console-core-concepts)
2. [Console 技术架构](/guide/console-architecture)
3. [Console 功能模块](/guide/console-modules)
4. [Console 权限治理 V1](/guide/console-permission-governance)
5. [Console 权限 / 菜单 / 按钮管理一期设计方案](/guide/console-authorization-phase1)
6. [Console 样式与 Token 使用说明](/frontend/console-style-guide)
7. [当前进行中](/planning/current)

### 1.5 默认测试角色边界

- 默认 `Test` 角色当前按“普通用户基线”收口，只保留 `/api/v1/User/GetUserByHttpContext` 所需的最小登录态 API 权限
- `Test` 角色不再保留任何 `RoleConsoleResource` 授权，因此不会出现在 WebOS 的 Console 入口判定中
- `Test` 角色不具备 `Tags`、`Moderation`、`Coins`、`Experience` 等后台页面权限，也不应拥有任意 Console 菜单可见性
- 当前用户权限快照会额外忽略 `Test` 角色残留的 Console 授权记录；即使历史库还没清理干净，也不应再把 Console 权限回传给前端
- 登录签发角色 Claim 时也会过滤已软删 / 已禁用角色，避免历史解绑角色继续混入新 Token
- 若历史开发库已残留旧授权记录，需要重新执行种子或手工清理后再验证 `Test` 角色的入口收口结果
