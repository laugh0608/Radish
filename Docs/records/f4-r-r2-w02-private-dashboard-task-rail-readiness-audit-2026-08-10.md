# F4-R R2-W02 Private 仪表 / 任务侧栏设计前代码事实与能力覆盖审计

> 日期：2026-08-10（Asia/Shanghai）
>
> 状态：readiness 已完成；五组窄前端能力门禁已由[实现批](/records/f4-r-r2-w02-private-dashboard-task-rail-capability-gate-implementation-2026-08-10)关闭
>
> 范围：正式 Web 的 Notifications、Me、Circle、Pet、Private Shop 与 Workbench；反查 WebOS 历史实现、共享 API / Store、PC / mobile 主次顺序和既有表单

## 1. 结论

`R2-W02` 继续保持 R2，评分仍为 `1/1/1/2/1/0 = 6`。六个入口均已有正式 Web 路由、真实数据来源和可继承壳层，不需要升级为新的 R1 页面类型，也不需要新增 API、数据库、权限、LongId、事务或移动壳层。

本专题应继承：

- `R1-F01` 的正式 Client Header、主题、PC / mobile 壳层和状态槽；
- `R1-W01` 的“主任务优先、上下文按需、mobile 单任务顺序”原则；
- 现有正式 Web 路由、共享 API / Store 与已有表单，不把 WebOS 窗口、Dock 或桌面结构迁回浏览器主线；
- `private-web-workflows-design` 已冻结的 Private 页面边界，不把转账、资产安全、公开经验详情、完整移动商城或宠物经济扩展带入 F4-R。

审计发现的五组窄前端门禁——权威读取状态、未保存草稿保护、Pet 模糊结果重试幂等、Private Shop 结构化错误 / 本地化，以及超限的 Me 页面容器——已于同日由[能力门禁实现批](/records/f4-r-r2-w02-private-dashboard-task-rail-capability-gate-implementation-2026-08-10)在现有前端边界内关闭。

## 2. 正式路由与能力覆盖

| 页面 | 正式 Web 主任务 | 权威来源与已有能力 | WebOS 反查 | 裁决 |
| --- | --- | --- | --- | --- |
| Notifications `/notifications` | 读取通知、筛选、已读处理与偏好设置 | 共享 `NotificationCenter`、`notificationStore`、汇总 revision、分类、实时连接与偏好 API | WebOS Notification 同样复用共享通知中心，没有正式 Web 缺失的独占能力 | 保持正式 Web；通知列表是主任务，汇总和连接 / 偏好是上下文 |
| Me `/me` 及私有子路由 | 私有身份总览、经验 / 资产 / 回访 / 宠物摘要和现有子任务 | 公开资料、经验、资产、浏览记录、宠物、收藏、申诉等既有 API；URL 保留来源返回 | WebOS Profile、ExperienceDetail、RadishPit 的有价值能力已由正式 Me 或既有正式子路由承接；转账、资产安全与统计继续脱离 F4-R | 保持正式 Web；不恢复 WebOS 面板，也不扩展新业务 |
| Circle `/circle/feed|following|followers` | 查看关注动态、我关注的人和关注我的人 | 关注摘要、稳定分页、关系失效订阅、公开身份与来源返回 | 无应迁回正式 Web 的 WebOS 独占实现 | 保持三条真实任务；关系列表 / feed 是主任务，计数与说明为摘要 |
| Pet `/pet` | 查看宠物状态、执行服务端允许的照料动作、编辑现有资料 | Pet Profile、照料动作、日志、冷却 / 每日次数、资料更新与幂等键字段 | 无 WebOS 独占实现 | 保持现有 Pet 能力；不进入经济、商城或社交扩展 |
| Private Shop `/shop/orders`、`/shop/order/:id`、`/shop/inventory` | 回看订单、订单详情与背包 | 共享 Shop hooks / pages、正式鉴权回跳、字符串 LongId、订单与库存 API | WebOS Shop 复用相同商城能力，没有另一套权威模型 | 只设计 Private 任务差异，不重做 Public 商品浏览或购买链路 |
| Workbench `/workbench` | 从低频能力地图继续当前任务，并查看跨模块状态 | 通知分类、频道未读、正式 / 私有 / Console / legacy 访问矩阵、本地草稿和 `Promise.allSettled` 失败摘要 | 无 WebOS Workbench 独占实现；legacy 只作历史入口标识 | 继续作为低频能力地图；继续队列为主，状态轨和全量地图为辅助 |

六个入口都由 `BrowserAppRouter` 注册为正式浏览器入口。现有 API / Store 足以支撑本轮设计，不应为仪表视觉另造聚合端点或第二套状态真相源。

## 3. 设计前必须关闭的窄前端门禁

### 3.1 权威读取状态不能伪装成真实零值或真实空对象

当前存在四处同类问题：

1. Notifications 已有 `idle / loading / ready / error` 的 Store 状态，但页面只读取计数；首次读取前或失败后仍显示 `0 / 0 / —`，无法区分“权威零值”和“尚未取得权威结果”。
2. Circle 把关注汇总初始化为两个 `0`，`getMyFollowSummary()` 失败只写日志，页面继续显示真实零值外观。
3. Pet 初次读取失败时仍保持 `pet = null`，错误条下方会渲染“领取宠物”表单；这把“服务不可用”误表现为“权威确认未领取”。刷新失败保留旧数据时也没有 stale 标记。
4. Me 会记录 `errors.profile`，但身份区没有消费该状态；资料权威读取失败时仍由本地身份回退拼出完整表面，用户无法区分资料可用与局部不可用。

门禁方案：按页面现有状态边界显式区分 `loading / ready / unavailable / stale`，只在 `ready` 后渲染真实零值或“未领取”；保留旧权威快照时标注 stale 并提供局部重试。此次不引入全局状态框架，只在已有 Store / 页面数据结构中补足最窄状态。

### 3.2 已有表单需要统一的未保存离开保护

- Notifications 已能计算 `preferencesDirty`，但该值只控制保存按钮；路由离开或刷新会静默丢失偏好草稿。
- Pet 的名称 / 公开状态编辑没有 dirty 判定，也没有路由离开或 `beforeunload` 保护。

门禁方案：复用现有 `useBrowserNavigationLock`，并沿用项目已有 `beforeunload` 模式；只保护已经产生有效变更的通知偏好与 Pet 资料草稿，保存成功或权威重新装载后解除。领取名称仍是一次性提交输入，不扩为新的持久草稿系统。

### 3.3 Pet 模糊结果重试必须复用幂等键

Pet 照料请求每次点击都会生成新的 `idempotencyKey`。失败后页面异步刷新权威状态，但立即允许再次点击；如果服务端已经提交、响应丢失且随后的权威刷新也失败，用户重试会使用新键并可能重复执行照料。

门禁方案：按照料动作保留待裁决幂等键。明确成功后清除；模糊失败时先以同键保留重试能力并触发权威对账，只有权威快照证明结果已经落地或可安全开始新动作时才生成新键。该方案只修改前端重试编排，不改变服务端 Pet API 或幂等契约。

### 3.4 Private Shop 必须保留结构化错误并完成固定文案本地化

正式 Private Shop 复用的 `useShopData` 在订单、订单详情、背包等读取失败时将响应压成普通 `Error`，丢失 `httpStatus / code / messageKey / traceId`；同一共享 Hook 还包含多处中文硬编码 fallback。页面因此只能显示 `error.message`，无法可靠区分 unavailable、重试和诊断信息。

门禁方案：复用 `@radish/http` 当前结构化响应与 Client 既有错误转换方式，在共享 Shop 数据层保留结构化字段；固定 fallback 统一经过 `t(...)`。本批只收口错误契约和状态表达，不改变商城能力、订单状态或购买流程。

### 3.5 Me 容器必须先恢复到项目文件上限内

`MeApp.tsx` 当前 `1816` 行，已超过项目 `1500` 行硬上限，并同时承担仪表聚合、路由判断、子页编排和大段渲染。继续在该文件叠加 R2-W02 视觉实现会扩大维护风险。

门禁方案：按真实职责拆出仪表数据 Hook / 主仪表视图和已有子页编排，保留现有路由、请求顺序、来源返回和 UI 行为；不新增转发型抽象，不借机重构无关 Me 子页。

## 4. PC / mobile 代表设计输入

### 4.1 共同顺序

PC 允许“主任务 + 辅助轨”，但摘要必须靠近它所解释的任务。Mobile 顺序固定为：

1. 身份 / 当前上下文与必要状态；
2. 当前主任务和直接动作；
3. 与主任务直接相关的紧凑摘要；
4. 偏好、历史、说明、跨模块入口等辅助内容。

当前 Me、Circle、Pet 在收窄后使用 `order: -1` 把整条辅助轨移到主内容之前。代表设计不得机械继承这一顺序；只有 Pet 的“当前最优先照料动作”可以作为主状态的一部分提前，完整 rail 仍应后置或按需展开。Notifications、Private Shop 与 Workbench 当前 DOM 更接近主任务优先，可继续沿用但需压缩摘要重复。

### 4.2 六页局部差异

| 页面 | PC 关键区块 | Mobile 排序 / 折叠输入 |
| --- | --- | --- |
| Notifications | 通知中心主轴；未读 / occurrence / revision 紧凑摘要；连接与偏好辅助轨 | 列表与筛选先于完整偏好；偏好进入后置区或按需层，不让三张大指标卡占据首屏 |
| Me | 私有身份与首要回访任务；摘要按经验、资产、回访、宠物真实状态分组 | 当前任务先于 `subPageRail`；子路由导航保持可达，辅助说明和跨模块入口后置 |
| Circle | feed / following / followers 主列表；关系计数紧凑化 | tabs 与列表先于整条 `circleRail`；计数可贴近标题，说明 / 论坛入口后置 |
| Pet | 宠物状态、服务端允许的照料动作和资料编辑；日志与上下文为辅助 | 当前状态和直接照料动作优先；完整 `petRail` 不再整体前置，避免与主动作网格重复 |
| Private Shop | 订单 / 详情 / 背包真实任务；私有导航只作为任务切换 | 保持连续单任务页面；不把 Public 商城 Header、分类或推荐内容复制进私有任务 |
| Workbench | 继续队列为主；状态轨次之；完整能力地图最后 | 队列保持首要内容；状态轨在同步不完整时给出明确提示，能力地图按低频入口继续后置 |

## 5. 停止线与后续顺位

本 readiness 自身未修改 Pencil、运行时代码、API、数据库、权限或业务范围，也未启动服务 / 浏览器。五组门禁已经通过同日实现批和 Client 定向验证关闭；下一步等待 Pencil 可用与当前任务授权，再裁决 `R2-W02` 的局部代表区块。当前继续不占用 Pencil，也不提前实施六个页面的视觉重构或任何 R3 页面。

## 6. 主要证据

- `Docs/planning/current.md`
- `Docs/frontend/private-web-workflows-design.md`
- `Docs/frontend/f4-r-representative-page-audit.md`
- `Docs/records/f4-r-formal-web-capability-coverage-audit-2026-08-06.md`
- `Docs/records/f4-r-r1-w01-messages-readiness-audit-2026-08-08.md`
- `Frontend/radish.client/src/bootstrap/BrowserAppRouter.tsx`
- `Frontend/radish.client/src/bootstrap/browserNavigationLock.tsx`
- `Frontend/radish.client/src/stores/notificationStore.ts`
- `Frontend/radish.client/src/notifications/NotificationsApp.tsx`
- `Frontend/radish.client/src/circle/CircleApp.tsx`
- `Frontend/radish.client/src/me/MeApp.tsx`
- `Frontend/radish.client/src/pet/PetApp.tsx`
- `Frontend/radish.client/src/apps/shop/hooks/useShopData.ts`
- `Frontend/radish.client/src/workbench/WorkbenchApp.tsx`
- `Frontend/radish.client/src/desktop/AppRegistry.tsx`
