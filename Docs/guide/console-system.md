# Console 管理后台系统设计方案

> 版本：v1.9 | 最后更新：2026-06-01 | 状态：Console 权限治理、首批页面类型视觉基座和外部 LongId 字符串安全已收口，后续新增或明显改动页面按页面类型小步对齐

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
- 审核台仍以人工审核、禁言 / 封禁联动和治理日志为主；批量治理、敏感词、自动策略仍后置
- 商城治理当前已支持商品详情 / 订单详情真实加载、商品删除拦截，以及订单 / 商品 / 用户之间的治理回跳，优先服务资产与订单人工追查

**当前页面体验边界**：

- Console 仍是独立后台入口，不嵌入 WebOS 窗口，也不复刻公开内容壳层。
- 当前视觉方向采用 `Case Desk`：低饱和暖灰 / 纸色底、轻侧栏、克制边框、清晰按钮层级和可扫描的工作台信息密度。
- 普通列表页优先使用“指标条 + 筛选 / 工具条 + 表格 + 右侧摘要栏”；设置 / 配置页优先使用“分组导航 + 设置列 + 影响范围摘要”；治理页才使用“队列 / 详情 / 动作”的工作台结构。
- 已对齐的首批页面包括 `Dashboard`、`Settings`、`UserList`、`TagList`、`CategoryList`、`SystemConfigList`、`RoleList`、`Applications`、`StickerGroupList`、`StickerList`、`ProductList`、`OrderList`、`CoinAdminPage`、`RolePermissionPage`、`UserDetail` 与 `UserProfile`。
- 上述页面迁移只调整视觉结构与信息组织，不改变 API、权限、表单字段、数据契约或业务语义。

### 1.3 当前阶段定义

当前 Console 处于：

- **所属里程碑**：`P3-8 多端功能补全与 UI 设计治理`
- **当前主线**：`Console 设计稿到实现的页面类型对齐`
- **阶段状态**：已完成角色授权页、资源映射、真实接口收口、`console.access` 入口权限收口、Console 权限种子一致性校验，以及首批高频后台页面的页面类型基座迁移；后续只对真实新增页面、接口、授权裂缝或明确进入改造范围的页面增量补矩阵、种子与视觉基座

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
