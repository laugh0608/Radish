# P3-12 Web 完全化与 WebOS 收束

> 状态：`P3-12-B1 代码侧主路径已完成；P3-12-C1 首轮 B1 直接残留清理已完成；P3-12-B2 完整个人中心 Web 化首批代码和正式链接语义补口已完成；P3-12-B3 论坛作者态 Web 化首批代码与小阶段验收已完成；P3-12-B4-1 正式 Web 文档作者入口首批代码已完成；P3-12-B4-2 Console 文档治理首批代码已完成；B4 / D1 阶段运行态 smoke 已完成；P3-12-B5 Web 功能总入口首批代码与 Gateway smoke 已完成；P3-12-B6 身份语义二次收口代码侧与启动前验证已完成；P3-12-D1-D13 已完成设计准备、client 视觉实现首轮与私域 / 作者态成组验收；P3-12-D14-D35 已完成 radish.console 首轮视觉迁移、静态收口、局部运行态复核、数据补验和表格交互代码侧治理；P3-12-D36-D64 已完成差距口径、设计源差距矩阵、边界裁决、阶段验收清单、Gateway PC / mobile 阶段验收、纠偏、页面开发缺口源码核对、Docs 作者态真实动作收口、Public / Private 主路径代码侧复核、Console 深层管理动作复核、移动响应式抽样、Pencil UI 实现完成度复核、UI 实现证据收口、候选前启动前验证、Gateway 真实页面复核、D49 后口径纠偏、Public Web 当前发布前范围首轮实现、D62 Private / Author 当前发布前页面族首批实现、D63 Console 治理 / 商业 / 文档 / 用户 / 权限矩阵首批实现、成组静态收口及 Gateway 成组复核和 D64 候选前集中验收准备；下一步进入 D65 UI 专题候选前验证执行，不进入 P3-12-E`
>
> 启动日期：2026-06-21（Asia/Shanghai）
>
> 本页承接 `P3-11` 暂缓 PR 后的下一条正式开发主线。快速入口仍以 [当前进行中](/planning/current) 为准；P3-11 收束决策见 [P3-11 阶段收束决策记录](/records/p3-11-stage-closure-decision-record-2026-06-21)，P3-12-A 盘点结论见 [P3-12-A WebOS 与 Web 功能资产盘点记录](/records/p3-12-a-webos-web-function-asset-inventory-2026-06-21)，B1 方案见 [P3-12-B1 账户资产与商城交易 Web 化方案](/records/p3-12-b1-account-shop-web-plan-2026-06-21)，C1 首轮清理见 [P3-12-C1 WebOS 残留入口清理记录](/records/p3-12-c1-webos-residual-cleanup-2026-06-21)，B2 方案见 [P3-12-B2 完整个人中心 Web 化方案](/records/p3-12-b2-personal-center-web-plan-2026-06-21)，B3 方案见 [P3-12-B3 论坛作者态 Web 化方案](/records/p3-12-b3-forum-author-web-plan-2026-06-21)，B4 方案见 [P3-12-B4 文档作者态归属裁决](/records/p3-12-b4-doc-author-ownership-plan-2026-06-22)，B4-2 设计见 [P3-12-B4-2 Console 文档治理设计](/records/p3-12-b4-2-console-doc-governance-design-2026-06-22)，阶段 smoke 见 [P3-12 B4 / D1 阶段运行态 Smoke 记录](/records/p3-12-b4-d1-stage-smoke-record-2026-06-22)，B5 设计见 [P3-12-B5 Web 功能总入口设计](/records/p3-12-b5-web-workbench-entry-design-2026-06-22)，B6 设计见 [P3-12-B6 身份语义二次收口设计](/records/p3-12-b6-identity-contract-convergence-design-2026-06-22)，D1 准备见 [P3-12-D1 统一 UI 设计准备记录](/records/p3-12-d1-unified-ui-design-prep-2026-06-22)，D2 设计源记录见 [P3-12-D2 公开 Web 统一体验设计源记录](/records/p3-12-d2-public-web-unified-design-source-2026-06-24)，D3 设计源记录见 [P3-12-D3 私域与作者态 Web 工作流设计源记录](/records/p3-12-d3-private-web-workflows-design-source-2026-06-25)，D4 共享基座记录见 [P3-12-D4 Web UI 共享基座设计源记录](/records/p3-12-d4-web-ui-foundation-design-source-2026-06-25)，D5 Console 设计源记录见 [P3-12-D5 Console 治理工作台设计源重构记录](/records/p3-12-d5-console-governance-workbench-redesign-2026-06-27)，D6 Console 实现前盘点见 [P3-12-D6 Console 视觉代码实现前盘点](/records/p3-12-d6-console-visual-code-prep-2026-06-27)，D7 移动导航统一见 [P3-12-D7 移动导航设计源统一记录](/records/p3-12-d7-mobile-navigation-design-source-alignment-2026-06-28)，D8 client 首批实现见 [P3-12-D8 radish.client 视觉实现首批记录](/records/p3-12-d8-radish-client-visual-first-implementation-2026-06-28)，D14-D41 Console 与 UI 专题记录见 [P3-12-D14 radish.console 视觉代码实现首批记录](/records/p3-12-d14-radish-console-visual-first-implementation-2026-06-29)、[P3-12-D21 表格视觉成组静态收口记录](/records/p3-12-d21-radish-console-table-visual-static-closure-2026-06-30)、[P3-12-D31 阶段运行态复核记录](/records/p3-12-d31-radish-console-stage-smoke-2026-06-30)、[P3-12-D35 表格交互代码侧收口记录](/records/p3-12-d35-radish-console-table-interaction-code-closure-2026-06-30)、[P3-12-D37 UI 设计源差距矩阵记录](/records/p3-12-d37-ui-design-source-gap-matrix-2026-07-01)、[P3-12-D38 UI 边界裁决与阶段验收清单](/records/p3-12-d38-ui-boundary-and-stage-acceptance-plan-2026-07-01)、[P3-12-D39 Gateway PC / Mobile 阶段验收记录](/records/p3-12-d39-gateway-pc-mobile-stage-acceptance-2026-07-01)、[P3-12-D40 UI 专题退出判断修正](/records/p3-12-d40-ui-topic-exit-decision-2026-07-01) 与 [P3-12-D41 页面开发缺口源码核对记录](/records/p3-12-d41-page-development-gap-source-audit-2026-07-01)。

## 背景判断

`P3-10` 已把根路径和默认浏览器体验切向纯 Web，并完成公开入口、登录恢复、来源返回、系统设置、写操作可靠性和论坛内容写入可靠性等基础治理。`P3-11` 已完成发布候选整备矩阵与轻量复访审计，未发现必须立即回修的阻断；用户明确暂缓 PR 和发布。

下一阶段不应继续停留在合并材料上，也不应把 Flutter 或 WebOS 作为第一顺位。当前更重要的是让纯 Web 在 PC / mobile 视图中具备正式版完整用户路径，然后再进入正式版发布候选。

## 阶段目标

1. **Web 正式版主路径完全化**
   - PC / mobile 浏览器成为正式版默认产品形态。
   - 公开浏览、登录、通知、消息、个人状态、论坛发布与互动、商城购买、订单、背包、资产流水、宠物基础操作和必要治理入口形成完整路径。
   - Flutter 暂时后移，等 Web 正式版主路径稳定后再承接受控移动原生增强。
2. **WebOS 收束**
   - `/desktop` 保留为历史兼容入口和既有能力维护线。
   - 新功能默认不进入 WebOS。
   - 迁移产品能力，不迁移窗口系统、Dock、桌面装饰、窗口几何记忆等 WebOS 形态能力。
3. **正式版发布准备**
   - P3-12 功能迁移、WebOS 残留清扫和 UI 专题退出条件完成后，再进入正式版发布候选。
   - 正式发布前再恢复 `dev -> master` PR、tag、发布记录、部署 smoke 和必要回滚材料。

## 子专题

### `P3-12-A` 功能资产盘点与迁移矩阵

先做只读盘点，不直接写代码。

盘点对象：

- WebOS `/desktop` 已有能力。
- 纯 Web 已有公开 / 私域入口。
- Console 必要治理入口。
- 当前 Web 正式版缺口。

每个能力按以下分类：

- `正式版必需`：必须进入 PC / mobile Web 主路径。
- `发布前建议`：不阻断正式版，但影响完整体验。
- `WebOS 保留`：只作为 `/desktop` 历史入口维护，不迁移。
- `后置评审`：需要单独产品或架构专题，不进入 P3-12 首批。

P3-12-A 已于 2026-06-21 完成只读盘点。结论：

- 浏览器根路径和公开阅读路径已基本 Web 化，`/desktop` 继续作为历史入口保留。
- P3-12-A 盘点时 `/me` 完整钱包入口仍指向 `/desktop?app=radish-pit`，公开商城购买仍回桌面商城窗口；当前 B1 已把 `/me` 完整钱包替换到 `/me/assets/transactions`，并把公开商品详情购买替换为 `/shop/product/:productId?intent=purchase` 登录回流与 `/shop/order/:orderId` 成功回流。
- 账户资产、商城购买、订单、库存和资产流水应作为 `P3-12-B1` 第一开发组。
- 个人中心完整内容已进入 `P3-12-B2`，论坛作者态和文档作者态按矩阵继续排队；WebOS 残留清扫只在已具备替代 Web 路径后按阻断程度回拉。

### `P3-12-B` 功能迁移专题

目标是补齐 Web 正式版主路径，而不是照搬 WebOS 应用壳。

`P3-12-B1` 已完成账户资产与商城交易 Web 化方案梳理和代码侧主路径，结论见 [P3-12-B1 账户资产与商城交易 Web 化方案](/records/p3-12-b1-account-shop-web-plan-2026-06-21)。首批代码已完成路由 / 登录回流契约、商城私域正式 Web 入口、资产正式入口和公开购买回流：新增正式 Web 资产与商城交易 return path，收窄 `/shop/*` 公开路由识别，接入 `/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/order/:orderId`、`/shop/inventory`，公开商品详情购买从 `/desktop` 回跳替换为正式 Web 登录回流，购买成功后进入订单详情，纯 Web 通知页订单目标也改到正式 Web 订单路由。定向契约测试、`radish.client` type-check / build 和 `git diff --check` 已通过；`P3-12-C1` 已完成 B1 直接残留首轮清理，真实 PC / mobile Gateway 复核放到小阶段准备验收时，在用户确认服务已启动后集中执行；统一 UI 设计放到页面迁移齐后的 `P3-12-D`。

`P3-12-B2` 已完成完整个人中心 Web 化首批代码和正式链接语义补口，结论见 [P3-12-B2 完整个人中心 Web 化方案](/records/p3-12-b2-personal-center-web-plan-2026-06-21)。首批代码已完成 `/me/content`、`/me/history`、`/me/attachments`、`/me/experience` 的正式 Web 路由、登录回流、API helper 收口和页面接入；我的内容与浏览历史列表已补真实公开 `href`，关注关系以既有 `/circle` 为权威入口，`/me` 只提供个人中心联动。

`P3-12-B3` 已完成论坛作者态 Web 化首批代码和 B3 小阶段验收，结论见 [P3-12-B3 论坛作者态 Web 化方案](/records/p3-12-b3-forum-author-web-plan-2026-06-21)。首批代码已接入 `/forum/compose`，扩展 `/forum/post/:postId?intent=answer|edit|history`，并把发帖、问答回答 / 采纳、作者帖子编辑和帖子编辑历史查看收口到正式 Web 路由与登录回流；`clientSubmissionId` 继续复用论坛写入可靠性治理，WebOS 三栏工作台、Dock、窗口参数和 `openApp` 语义不进入正式 Web。2026-06-22 已补 Gateway PC `1920x1080` 与移动 `390x844` CSS 视口 smoke，公开论坛列表、发帖登录回流、公开详情 canonical、`edit/history` return path 均通过；随后使用 Browser 插件和种子账号 `admin` 补验已登录发帖、作者编辑、编辑历史与问答回答提交成功态，三类 `ContentSubmissionRecord` 均为 `Succeeded`。

`P3-12-B4` 已完成文档作者态归属裁决，结论见 [P3-12-B4 文档作者态归属裁决](/records/p3-12-b4-doc-author-ownership-plan-2026-06-22)。`B4-1` 正式 Web 作者入口首批代码已完成：公开 `/docs` 保持阅读、搜索、正文内链和分享；新增 `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id` 承接登录态创建、编辑和版本回看；发布、撤回、归档、恢复、受限可见性、角色 / 权限配置、导入导出和回滚等治理动作归 Console；WebOS `WikiApp` 只作为 `/desktop` 历史维护入口保留。`B4-2` Console 文档治理首批代码已完成：新增 `/console/documents` 对应内部路由 `/documents`、治理专用读取 / 权限策略 API、Console 权限键、资源种子和权限覆盖矩阵；正文创建 / 编辑继续归正式 Web 作者入口，公开 `/docs` 继续只读。

文档本地源与在线编辑冲突不进入 P3-12 当前开发批次，后续按 [文档本地源与在线编辑冲突治理](/planning/document-local-source-conflict-governance) 单独评审；当前仍保持固定文档只读、在线文档数据库写入权威、Markdown 导入作为在线副本的稳定口径。

`P3-12-D1` 统一 UI 设计准备已启动，结论见 [P3-12-D1 统一 UI 设计准备记录](/records/p3-12-d1-unified-ui-design-prep-2026-06-22)。当前已完成页面矩阵、设计源拆分、停止线和后续执行顺序；用户确认前后端已启动后，B4 / D1 阶段 Gateway PC / mobile smoke 已补齐，结论见 [P3-12 B4 / D1 阶段运行态 Smoke 记录](/records/p3-12-b4-d1-stage-smoke-record-2026-06-22)。

`P3-12-D2` 公开 Web 统一体验设计源已扩展并收口，结论见 [P3-12-D2 公开 Web 统一体验设计源记录](/records/p3-12-d2-public-web-unified-design-source-2026-06-24)，实现口径见 [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)。`public-web-unified-experience.pen` 当前包含 `P01-P16`，覆盖公开首页、发现流、论坛列表 / 详情、紧凑评论树、轻回应、公开聊天室、文档列表 / 详情、商城、榜单、公开主页和移动公开任务流；已按用户反馈收紧 P03 / P04 / P07 / P15 与移动页信息密度。本批仍停留在 Pencil 设计源和说明文档阶段，不进入 `radish.client` 视觉代码。

`P3-12-D3` 私域与作者态 Web 工作流设计源已完成并二次重构为真实路由驱动的业务页面族，结论见 [P3-12-D3 私域与作者态 Web 工作流设计源记录](/records/p3-12-d3-private-web-workflows-design-source-2026-06-25)，实现口径见 [私域与作者态 Web 工作流设计说明](/frontend/private-web-workflows-design)。当前 `private-web-workflows.pen` 包含 `P01-P30`，覆盖 `/workbench`、`/me` 系列、资产流水、订单、背包、通知、消息、圈子、宠物、论坛作者态、Docs 作者态和移动端 10 个单任务页面。

`P3-12-D4` Web UI 共享基座设计源已完成并继续作为跨设计源约束，结论见 [P3-12-D4 Web UI 共享基座设计源记录](/records/p3-12-d4-web-ui-foundation-design-source-2026-06-25)，实现口径见 [Web UI 共享基座设计说明](/frontend/web-ui-foundation-design)。`web-ui-foundation.pen` 当前包含 `F01-F02`，覆盖 public / private header 合法变体、按钮 / pill、卡片 / rail、状态槽、移动 shell / tab、client 公共壳层组件契约和跨设计源同步规则；`P3-12-D7` 已将移动底栏样式统一为浮动胶囊栏，并把 `/workbench` 固定为导航无法展示功能的承接入口。

`P3-12-D5` Console 治理工作台设计源已完成重构，结论见 [P3-12-D5 Console 治理工作台设计源重构记录](/records/p3-12-d5-console-governance-workbench-redesign-2026-06-27)，实现口径见 [Console 治理工作台设计端点](/frontend/console-governance-workbench-design)。`console-governance-workbench.pen` 当前包含 `P00-P18`，覆盖公共 Console 壳层、浅色图标侧栏、内容审核、经验台账、治理调度、表格 CRUD、设置策略、商业运营、文档治理、权限矩阵、运维任务和移动端 Console 任务流参考；移动端 Console 底栏统一为 `总览 / 治理 / 资产 / 权限 / 运维`。

`P3-12-D6` Console 视觉代码实现前盘点已完成，结论见 [P3-12-D6 Console 视觉代码实现前盘点](/records/p3-12-d6-console-visual-code-prep-2026-06-27)。该盘点只读确认 `radish.console` 的壳层与结构组件首批实现边界；按当前设计完整性判断，下一步优先进入 `radish.client` 视觉实现，Console 代码实现后移承接。

`P3-12-D8` `radish.client` 视觉实现首批已完成，结论见 [P3-12-D8 radish.client 视觉实现首批记录](/records/p3-12-d8-radish-client-visual-first-implementation-2026-06-28)。本批新增共享 `WebShellHeader` / `WebStateSlot`，首批接入私域复访页、作者态页、公开 forum 状态入口和 discover / docs / leaderboard / shop / profile 公开状态槽；补公开内容宽度 token 与移动底部导航留白。`radish.client` 构建、仓库卫生检查和 `git diff --check` 通过；真实 Gateway PC / mobile smoke 待阶段验收或用户明确要求时，在用户确认前后端已启动后执行。下一步继续 `radish.client` 第二批视觉实现，优先私域 / 作者态真实数据面、任务流和移动单列节奏。

`P3-12-D9` `radish.client` 私域交易数据面视觉实现已完成，结论见 [P3-12-D9 radish.client 私域交易数据面视觉实现记录](/records/p3-12-d9-radish-client-private-transaction-visual-implementation-2026-06-29)。本批覆盖 `/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/order/:orderId` 和 `/shop/inventory`，补共享状态槽、交易摘要、订单 / 背包卡片密度与移动单列任务流；`radish.client` 构建、仓库卫生检查和 `git diff --check` 通过。下一步继续通知 / 消息、圈子 / 宠物、论坛作者态和 Docs 作者态视觉实现。

`P3-12-D10` `radish.client` 通知与消息任务面视觉实现已完成，结论见 [P3-12-D10 radish.client 通知与消息任务面视觉实现记录](/records/p3-12-d10-radish-client-notification-message-visual-implementation-2026-06-29)。本批覆盖 `/notifications` 与 `/messages`，增加私域任务摘要、入口级状态槽容器、Web 宽高约束和移动单列布局；通知中心、聊天协议和 SignalR 逻辑保持不变。下一步继续圈子 / 宠物、论坛作者态和 Docs 作者态视觉实现。

`P3-12-D11` `radish.client` 圈子与宠物任务面视觉实现已完成，结论见 [P3-12-D11 radish.client 圈子与宠物任务面视觉实现记录](/records/p3-12-d11-radish-client-circle-pet-visual-implementation-2026-06-29)。本批覆盖 `/circle` 与 `/pet`，补私域摘要卡、共享状态槽容器、成长 / 流水 / 公开状态指标和移动单列任务流；关注关系、公开来源返回、宠物动作幂等和后端契约保持不变。下一步继续论坛作者态和 Docs 作者态视觉实现。

`P3-12-D12` `radish.client` 作者态任务面视觉实现已完成，结论见 [P3-12-D12 radish.client 作者态任务面视觉实现记录](/records/p3-12-d12-radish-client-author-workflow-visual-implementation-2026-06-29)。本批覆盖 `/forum/compose` 和 Docs 作者台，补发帖作者任务摘要、Docs 作者任务摘要和共享状态槽；论坛发布器、提交幂等、Docs 保存 / 修订、Markdown 编辑器、权限判断和后端契约保持不变。下一步进入 D9-D12 第二批收口检查与阶段验收准备。

`P3-12-D13` `radish.client` 私域 / 作者态第二批视觉收口检查与 D9-D13 成组 Gateway PC / mobile smoke 已完成，结论见 [P3-12-D13 radish.client 私域 / 作者态第二批视觉收口检查记录](/records/p3-12-d13-radish-client-private-author-visual-closure-2026-06-29)。本批移除通知 / 消息 / 圈子 / 宠物状态槽外层重复卡片，统一论坛发帖和 Docs 作者摘要卡 `8px` 半径；D9-D12 涉及的业务契约保持不变，真实联调当前未发现阻断级页面问题。下一步根据整体计划判断是否进入 Console 视觉代码实现，或先补 `radish.client` 后续真实使用暴露的同类问题。

`P3-12-D14` `radish.console` 视觉代码实现首批已完成，结论见 [P3-12-D14 radish.console 视觉代码实现首批记录](/records/p3-12-d14-radish-console-visual-first-implementation-2026-06-29)。本批扩展路由分组、侧栏 icon 和排序元数据，`AdminLayout` 侧栏按总览 / 商业与资产 / 内容与文档 / 治理与权限 / 系统工具分组渲染；新增 `ConsolePageHeader`、`ConsoleStatusChip`、`ConsoleMetricGrid`、`ConsoleMetricCard`、`ConsoleToolbar`，并将 `SystemConfigList` 页头、指标和筛选工具条迁入语义组件。系统设置 API、权限、表单字段、上传行为和编辑动作保持不变。下一步继续迁移订单 / 用户等高频表格代表页，验证组件在复杂筛选、批量操作和权限反馈中的复用边界。

`P3-12-D15-D35` `radish.console` 表格、治理页面与复杂页面边界已连续推进：订单、用户、商品三类高频表格代表页已迁入 D14 语义组件；文档治理页先固定首屏区块边界，再迁移页头、指标和筛选区；标签 / 分类普通列表已完成同口径迁移并补 Gateway PC / 移动 CSS 视图真实联调；贴纸分组 / 分组表情列表已确认按普通 CRUD 外层迁移，不拆媒体资产工作台；D21 已完成 D14-D20 成组静态收口；D22-D30 已完成角色权限、治理工作台、系统工具、深层表单和详情 / 抽屉静态收口；D31-D32 已完成阶段运行态复核与数据补验；D33-D35 已完成操作列换行、运维 / 治理静态残留、Dashboard 最近订单、用户详情内嵌表格、系统设置历史、文档版本弹窗和贴纸批量上传表格滚动 / 分页布局收口。D14-D35 是 Console 首轮迁移与代码侧治理，不等同于 `P00-P18` 完整同步。

`P3-12-D36-D64` 已完成 UI 专题差距口径、四类设计源差距矩阵、边界裁决、阶段验收清单、Gateway PC / mobile 阶段验收、纠偏、页面开发缺口源码核对、Docs 作者态真实动作代码侧补漏、Public / Private 主路径代码侧复核、Console 深层管理动作复核、移动响应式抽样、Pencil UI 实现完成度复核、UI 实现证据收口、候选前启动前验证、Gateway 真实页面复核、D49 后口径纠偏、`radish.client` 私域 / 作者态移动任务流 UI 对齐首批、Public Web 移动公开任务流 UI 对齐、Public Web `P01-P14` 首轮实现、Private / Author Pencil 页面与 Gateway 真实页面对齐收口、Console 响应式后台 UI 差距回拉、Console Gateway 成组真实页面复核、Console 深层交互 / 真实数据态复核、UI 专题候选前集中验收准备、UI 专题候选前验证执行、Pencil 逐页 UI 与功能缺口复盘、D62 Private / Author 当前发布前页面族首批实现、D63 Console 治理 / 商业 / 文档 / 用户 / 权限矩阵首批实现、成组静态收口和 Gateway 成组复核，以及 D64 候选前集中验收准备：D49 / D59 只能证明当前已实现页面无阻断，不能证明整个 app 已按 Pencil 设计稿完成 UI 实现，也不能直接进入 `P3-12-E`。D60 已将后续顺序拆为 `D61 Public Web`、`D62 Private / Author`、`D63 Console`，D64 已整理重新实现后的证据与验证清单；后续进入 D65 UI 专题候选前验证执行。

`P3-12-B5` 已完成 Web 功能总入口首批代码与 Gateway PC / mobile smoke，结论见 [P3-12-B5 Web 功能总入口设计](/records/p3-12-b5-web-workbench-entry-design-2026-06-22)。`/messages` 已作为正式 Web 消息 / 聊天入口存在，本批不重做聊天室；`/workbench` 已作为正式 Web 功能地图接入公共壳层，公共壳层“工作台”指向 `/workbench`，`/desktop` 降级为“桌面版 / WebOS 历史入口”功能项。

`P3-12-B6` 身份语义二次收口设计已确认，`B6-1 身份基础与注册登录`、`B6-2 公开展示与前端状态收敛`、`B6-3 展示名变更治理`、`B6-4 PublicIndex 保留号治理`、`B6-5 种子与 DbMigrate 收口`、`B6-6 验证与阶段验收` 的代码侧与启动前验证已完成，结论见 [P3-12-B6 身份语义二次收口设计](/records/p3-12-b6-identity-contract-convergence-design-2026-06-22)。本专题从 B5 拆出独立推进：登录凭证改为邮箱 + 密码，注册必须填写 `DisplayName`；`DisplayName` 只允许中文、英文字母和数字，`DisplayHandle = DisplayName#PublicIndex` 作为用户可见唯一身份、搜索和艾特主键；`PublicId` 只用于 URL、分享和前后端传递；`LoginName`、`UserRealName` 公开回退和 `usr_...` 普通资料页展示必须退场。当前项目尚未上线且无正式数据库，B6 按破坏性 schema 收口处理，不为旧本地 SQLite 编写兼容迁移。B6-5 已物理移除 `LoginName` / `UserRealName` 后端字段、个人资料真实姓名输入、登录名系统设置和 DbMigrate 旧身份回填逻辑，并将开发默认账号固定为 `system/admin/test@radishx.com` 与 `PublicIndex=1/2/3`；`validate:baseline`、`validate:identity`、`validate:baseline:host` 均已通过，Gateway PC / mobile 页面 smoke 待用户明确前后端已启动后再补。邮箱白名单、关注备注、人工分配 PublicIndex、修改原因和审计动作继续后置。

优先候选：

- 账户资产与商城交易：余额、流水、购买、订单、背包 / 权益和商城回流。
- 账号与个人状态：登录后个人资料、成长、资产摘要、最近访问和设置入口。
- 通知与消息：通知目标跳转、会话复访、未读状态和登录恢复。
- 论坛与互动：发帖、评论、回答、编辑、轻回应、来源返回和参与反馈。
- 电子宠物：领取、命名、基础照顾、状态反馈和个人页摘要。
- Console 必要治理：发布候选或正式版阻断级排障入口。

不迁移：

- WebOS Dock、窗口系统、桌面背景、窗口几何记忆、桌面 app 外壳。
- WebOS 桌面形态和完整移动商城，除非功能资产盘点明确判定为正式版必需。
- 完整钱包和完整个人中心只迁移已判定为正式版必需的 Web 主路径；转账、支付口令、安全设置、资料编辑深水区等高风险能力继续拆专题评审。

### `P3-12-C` WebOS 残留清扫专题

目标是断开 WebOS 对默认产品路径的影响。

范围：

- 默认入口、导航文案、旧链接、文档口径和工作台优先的路由假设。
- 把 WebOS 专属代码和说明隔离到 `/desktop` 维护线。
- 先断开默认产品路径，再按验证覆盖逐步删除死代码。

不做：

- 不为了“清爽”大删仍被 `/desktop` 使用的代码。
- 不在没有替代 Web 路径时移除历史能力。

### `P3-12-D` UI 设计与美化专题

目标是让 Web 正式版在 PC / mobile 视图中具备统一、成熟、可发布的视觉与交互质量。

硬性顺序：

1. 统一使用 Pencil 先完成页面级或跨页面设计稿。
2. 根据设计稿更新对应设计 / 说明文档，明确页面信息架构、响应式行为、组件边界和验证口径。
3. 再进入代码实现。
4. 实现后用 PC / mobile 视图复核，必要时回到 Pencil 和说明文档修正。

适用范围：

- 页面级重设计。
- 跨页面视觉体系调整。
- 导航、布局、关键状态和核心操作流的体验重塑。
- 端点级视觉治理和正式版美化批次。

不适用范围：

- 明确 bug 修复、错别字、低风险文案、单点状态修正或不改变视觉体系的小修。
- 这些小修仍可直接实施，但不能借此绕过 P3-12-D 的页面级设计约束。

### `P3-12-E` 正式版发布候选专题

P3-12 功能迁移、残留清扫、UI 设计实现和 D36+ 确认的退出条件完成后，再进入发布候选。

验收入口：

- `validate:baseline`
- `validate:identity`
- `validate:baseline:host`
- PC / mobile Gateway 真实页面复核
- 必要 Flutter 回归
- 若已经存在正式数据库，则补数据库发布 SQL 审核；上线前本地 SQLite 阶段不维护历史发布脚本
- `dev -> master` PR 范围、验证结论、剩余风险和回滚说明

## 首批顺序

1. `P3-12-A`：建立功能资产盘点与迁移矩阵。已完成，见 [P3-12-A WebOS 与 Web 功能资产盘点记录](/records/p3-12-a-webos-web-function-asset-inventory-2026-06-21)。
2. `P3-12-B1`：推进账户资产与商城交易 Web 化，方案、路由 / 登录回流契约、商城私域正式 Web 入口、资产正式入口、公开购买回流和公开商城 `/desktop` 回跳替换已完成，见 [P3-12-B1 账户资产与商城交易 Web 化方案](/records/p3-12-b1-account-shop-web-plan-2026-06-21)。
3. `P3-12-C1`：清理与 B1 直接冲突的 WebOS 残留入口，只处理默认产品路径仍误回 `/desktop` 的链接、文案和路由假设；首轮见 [P3-12-C1 WebOS 残留入口清理记录](/records/p3-12-c1-webos-residual-cleanup-2026-06-21)。当前停止主动扩展 C1，只在阶段验收或新增阻断命中时回拉。
4. `P3-12-B2`：推进完整个人中心 Web 化，覆盖我的内容、完整浏览历史、附件管理和经验详情；首批正式 Web 路由、页面接入和正式链接语义补口已完成，方案与验证口径见 [P3-12-B2 完整个人中心 Web 化方案](/records/p3-12-b2-personal-center-web-plan-2026-06-21)。
5. `P3-12-B3`：推进论坛作者态 Web 化，覆盖发帖、编辑、回答、历史查看和作者反馈；首批正式 Web 路由、登录回流、代码接入和小阶段验收已完成，见 [P3-12-B3 论坛作者态 Web 化方案](/records/p3-12-b3-forum-author-web-plan-2026-06-21)。
6. `P3-12-B4`：文档作者态归属裁决与 `B4-1` 正式 Web 作者入口首批代码已完成，方案与验证口径见 [P3-12-B4 文档作者态归属裁决](/records/p3-12-b4-doc-author-ownership-plan-2026-06-22)；`B4-2` Console 文档治理首批代码已完成，见 [P3-12-B4-2 Console 文档治理设计](/records/p3-12-b4-2-console-doc-governance-design-2026-06-22)。B4 / D1 阶段运行态 smoke 已完成。
7. `P3-12-B5`：Web 功能总入口设计、`/workbench` 页面、公共壳层入口调整和 PC / mobile Gateway smoke 已完成，见 [P3-12-B5 Web 功能总入口设计](/records/p3-12-b5-web-workbench-entry-design-2026-06-22)。
8. `P3-12-B6`：身份语义二次收口设计已确认，`B6-1` 至 `B6-6` 代码侧与启动前验证已完成，见 [P3-12-B6 身份语义二次收口设计](/records/p3-12-b6-identity-contract-convergence-design-2026-06-22)。Gateway PC / mobile 页面 smoke 待用户明确前后端已启动后补验。
9. `P3-12-D1-D7`：统一 UI 设计准备、公开 Web 设计源、私域 / 作者态设计源、Web UI 共享基座、Console 治理工作台设计源、Console 视觉实现前盘点、client private 业务设计源矩阵和移动导航设计源统一已完成。
10. `P3-12-D8`：`radish.client` 视觉实现首批已完成，共享壳层、状态槽、公开内容宽度 token 和移动底部留白已进入代码。
11. `P3-12-D9`：`radish.client` 私域交易数据面视觉实现已完成，资产流水、订单列表、订单详情和背包入口补共享状态槽、数据卡片和移动单列任务流；下一步继续通知 / 消息、圈子 / 宠物、论坛作者态和 Docs 作者态视觉实现，Console 代码实现后移。
12. `P3-12-D10`：`radish.client` 通知与消息任务面视觉实现已完成，通知 / 消息入口补任务摘要、状态槽容器和移动单列布局；下一步继续圈子 / 宠物、论坛作者态和 Docs 作者态视觉实现。
13. `P3-12-D11`：`radish.client` 圈子与宠物任务面视觉实现已完成，圈子 / 宠物入口补私域摘要、指标卡、状态槽容器和移动单列任务流；下一步继续论坛作者态和 Docs 作者态视觉实现。
14. `P3-12-D12`：`radish.client` 作者态任务面视觉实现已完成，论坛发帖和 Docs 作者台补作者任务摘要与共享状态槽；下一步进入 D9-D12 第二批收口检查与阶段验收准备。
15. `P3-12-D13`：`radish.client` 私域 / 作者态第二批视觉收口检查与 D9-D13 成组 Gateway PC / mobile smoke 已完成，状态槽外层重复卡片和作者摘要卡半径分叉已收敛。
16. `P3-12-D14`：`radish.console` 视觉代码实现首批已完成，侧栏分组、页面语义组件和系统设置代表页已落地。
17. `P3-12-D15-D31`：`radish.console` 订单、用户、商品、文档治理首屏、标签 / 分类、贴纸类页面、角色权限页面、内容 / 经验治理工作台外层语义、点名内部区块样式迁移、治理页面成组静态收口、系统工具 / 运维外壳收口、阶段静态收口、深层表单静态收口、详情 / 抽屉静态收口和阶段运行态复核已完成，D19 与 D31 已补 Gateway PC / 移动 CSS 视图真实联调，D22 已完成复杂 Console 页面类型边界评估，D27 已确认 `/hangfire` 仍为外部 Dashboard 承载。
18. `P3-12-D32`：`radish.console` 数据补验与 Auth 静态根目录收口已完成，订单详情、分组表情列表和批量上传弹窗已覆盖 Gateway PC / mobile CSS 视图，Auth `wwwroot` 缺失告警和贴纸弹窗 AntD `Alert.message` 告警已收口。
19. `P3-12-D33`：表格可读性首批代码侧收口已完成，固定右侧操作列按钮换行和贴纸排序输入样式残留已收敛；Gateway 真实视口扫描并入成组验收。
20. `P3-12-D34`：运维与治理表格静态收口已完成，应用、系统设置、萝卜币、经验和内容治理目标目录的 inline 样式、硬编码色和未换行小按钮组扫描清零。
21. `P3-12-D35`：表格交互代码侧收口已完成，Dashboard 最近订单、用户详情内嵌表格、系统设置历史、文档版本弹窗和贴纸批量上传表格已补滚动 / 分页布局约束。
22. `P3-12-D36`：整理 UI 专题剩余差距与退出标准，按 public / private / foundation / console 设计源建立代码页面族对齐矩阵，标注已落地、首轮落地、需继续代码治理和需运行态观察的缺口，记录见 [P3-12-D36 UI 专题差距与退出标准整理记录](/records/p3-12-d36-ui-topic-gap-and-exit-criteria-2026-06-30)。
23. `P3-12-D37`：完成四类设计源差距矩阵，按每个画板列出真实路由 / 页面族、代码状态、验证证据和剩余动作，记录见 [P3-12-D37 UI 设计源差距矩阵记录](/records/p3-12-d37-ui-design-source-gap-matrix-2026-07-01)。
24. `P3-12-D38`：完成 UI 边界裁决与阶段验收清单，公开首页由 `/discover` 承接，公开聊天室、内部调度中心、内部 Jobs 平台和独立移动 Console 应用后置，记录见 [P3-12-D38 UI 边界裁决与阶段验收清单](/records/p3-12-d38-ui-boundary-and-stage-acceptance-plan-2026-07-01)。
25. `P3-12-D39`：完成 Gateway PC / mobile 阶段验收，public / private / author / console 代表页面和 Console 只读交互补验通过，记录见 [P3-12-D39 Gateway PC / Mobile 阶段验收记录](/records/p3-12-d39-gateway-pc-mobile-stage-acceptance-2026-07-01)。
26. `P3-12-D40`：完成 UI 专题退出判断修正，确认不能以 D39 代表页面验收替代设计源页面全量开发，记录见 [P3-12-D40 UI 专题退出判断修正](/records/p3-12-d40-ui-topic-exit-decision-2026-07-01)。
27. `P3-12-D41`：页面开发缺口源码核对已完成，记录见 [P3-12-D41 页面开发缺口源码核对记录](/records/p3-12-d41-page-development-gap-source-audit-2026-07-01)；本批同步修正 Docs 作者列表不可编辑文档只读状态表达。
28. `P3-12-D42`：Docs 作者态真实动作代码侧补漏已完成，记录见 [P3-12-D42 Docs 作者态真实动作收口记录](/records/p3-12-d42-docs-author-action-closure-2026-07-02)；编辑页与修订页已补公开阅读回跳，编辑页修订记录改为作者台内部导航，目录异步加载不再重置草稿。
29. `P3-12-D43`：Public / Private 主路径代码侧真实数据态复核已完成，记录见 [P3-12-D43 Public / Private 主路径真实数据态收口记录](/records/p3-12-d43-public-private-data-state-closure-2026-07-02)；公开论坛详情工作区动作已补回答、轻回应、编辑、历史和评论真实 intent `href`，商城私域与个人中心主路径链接契约已复核。
30. `P3-12-D44`：Console 深层管理动作复核已完成，记录见 [P3-12-D44 Console 深层管理动作复核记录](/records/p3-12-d44-console-deep-action-review-2026-07-02)；商品详情编辑、订单详情失败重试、角色权限保存、文档治理访问策略 / 回滚 / 导入导出和系统设置 favicon / 编辑抽屉已对齐权限态。
31. `P3-12-D45`：移动响应式抽样已完成，记录见 [P3-12-D45 移动响应式抽样记录](/records/p3-12-d45-mobile-responsive-sampling-2026-07-02)；Gateway 移动 CSS 视口已覆盖 public / private / author / console 主路径，系统设置品牌卡片横向溢出已收口。
32. `P3-12-D46`：Pencil UI 实现完成度复核与剩余实现清单已完成，记录见 [P3-12-D46 Pencil UI 实现完成度复核与剩余实现清单](/records/p3-12-d46-pencil-ui-implementation-completion-review-2026-07-02)；当前发布前范围未命中新的页面级 UI 实现缺口，公开聊天室、内部调度、内部 Jobs 和独立移动 Console 继续后置。
33. `P3-12-D47`：UI 实现证据收口与候选前验证清单准备已完成，记录见 [P3-12-D47 UI 实现证据收口与候选前验证清单](/records/p3-12-d47-ui-evidence-and-candidate-validation-checklist-2026-07-02)；本批整理 D36-D46 证据、工具限制、候选前自动化入口和真实 Gateway 复核约束。
34. `P3-12-D48`：UI 候选前验证执行准备已完成，记录见 [P3-12-D48 UI 候选前验证执行准备](/records/p3-12-d48-ui-candidate-preflight-validation-2026-07-02)；`validate:ci -- --report`、`validate:baseline`、`validate:identity` 与 `validate:baseline:host -- --report` 均通过，当前具备进入真实 Gateway 页面复核的前置条件。
35. `P3-12-D49`：UI 候选前 Gateway 真实页面复核已完成，记录见 [P3-12-D49 UI 候选前 Gateway 真实页面复核](/records/p3-12-d49-ui-candidate-gateway-smoke-2026-07-02)；`check:host-runtime -- --details --report` 通过，Gateway PC `1920x1080` 与 mobile `390x844 @ DPR 3` 覆盖 public / private / author / console 代表路径，未发现当前已实现页面的阻断级问题。
36. `P3-12-D50`：UI 实现缺口复盘与下一批实现排序已完成，记录见 [P3-12-D50 UI 实现缺口复盘与下一批实现排序](/records/p3-12-d50-ui-gap-recheck-and-next-implementation-order-2026-07-02)；确认 D49 不能作为 UI 专题退出结论，下一顺位继续留在 D 专题做 UI 实现。
37. `P3-12-D51`：`radish.client` 私域 / 作者态移动任务流 UI 对齐首批已完成，记录见 [P3-12-D51 radish.client 私域 / 作者态移动任务流 UI 对齐首批](/records/p3-12-d51-radish-client-private-author-mobile-ui-first-closure-2026-07-02)；资产、订单 / 背包、通知、消息、圈子、宠物和 Docs 作者态移动摘要与任务列表密度已按 `P21-P30` 首批收紧。
38. `P3-12-D52`：`radish.client` Public Web 移动公开任务流 UI 对齐已完成，记录见 [P3-12-D52 radish.client Public Web 移动公开任务流 UI 对齐](/records/p3-12-d52-radish-client-public-mobile-ui-alignment-2026-07-02)；本批覆盖 `/discover`、`/forum`、论坛详情、`/docs`、Docs 详情、`/shop`、商品详情、`/leaderboard` 和公开主页，收紧移动摘要、卡片 / 列表、状态槽、筛选 / tab、底部空间和横向溢出风险。
39. `P3-12-D53`：Public Web Pencil 首轮真实页面对齐收口已完成，记录见 [P3-12-D53 Public Web Pencil 首轮真实页面对齐收口](/records/p3-12-d53-public-web-pencil-first-alignment-closure-2026-07-02)；本批通过 Pencil MCP 读取公开设计源，并在 Gateway mobile 真实页面中修正说明卡前置压住真实内容、论坛详情说明插队和商品详情移动首屏信息后移问题。
40. `P3-12-D54`：Private / Author Pencil 首轮页面对齐复核已完成，记录见 [P3-12-D54 Private / Author Pencil 首轮页面对齐复核](/records/p3-12-d54-private-author-pencil-first-alignment-2026-07-04)；本批通过 Pencil MCP 读取私域 / 作者态设计源并抽查 `P22 / P23`，收紧 `/me` 移动个人状态入口、摘要卡和内容 tab 密度，随后使用 Gateway 覆盖 `/workbench`、`/me` 系列、资产 / 订单 / 背包、通知、消息、圈子、宠物、论坛作者态和 Docs 作者态 PC / mobile 视图，未命中新阻断。
41. `P3-12-D55`：Console 响应式后台 UI 差距回拉已完成，记录见 [P3-12-D55 Console 响应式后台 UI 差距回拉](/records/p3-12-d55-console-responsive-ui-gap-pullback-2026-07-04)；本批按 Console `P07 / P08 / P14-P17` 移动验收参考补强共享页头、指标、工具条、表格筛选、治理工作台和权限矩阵的窄屏收缩边界，业务 API、权限、路由和提交载荷保持不变。
42. `P3-12-D56`：Console Gateway 成组真实页面复核与证据收口已完成，记录见 [P3-12-D56 Console Gateway 成组真实页面复核与证据收口](/records/p3-12-d56-console-gateway-grouped-page-smoke-2026-07-04)；本批覆盖 Console 代表页 PC `1920x1080` 与 mobile `390x844` CSS 视图，确认全局横向溢出、页头 / 工具条 / 筛选控件、治理工作台、权限矩阵、表格局部滚动和弹窗可用性，并修复文档治理移动端固定列遮挡操作按钮的问题。
43. `P3-12-D57`：Console 深层交互与真实数据态复核已完成，记录见 [P3-12-D57 Console 深层交互与真实数据态复核记录](/records/p3-12-d57-console-deep-interaction-data-state-review-2026-07-04)；本批覆盖商品 / 订单详情、商品编辑、系统设置历史、文档版本治理、角色权限矩阵和内容治理手动动作区的 PC / mobile 真实视图，当前未发现新的 Console 真实 UI 问题。
44. `P3-12-D58`：UI 专题候选前集中验收准备已完成，记录见 [P3-12-D58 UI 专题候选前集中验收准备](/records/p3-12-d58-ui-candidate-acceptance-prep-2026-07-04)；本批整理 public / private / console 三类证据、剩余限制和 D59 验证清单，不执行真实 Gateway 页面联调。
45. `P3-12-D59`：UI 专题候选前验证执行已完成，记录见 [P3-12-D59 UI 专题候选前验证执行](/records/p3-12-d59-ui-candidate-validation-execution-2026-07-04)；本批刷新 `validate:ci`、`validate:baseline`、`validate:identity`、`validate:baseline:host`、`check:host-runtime` 和 Gateway public / private / console PC、mobile CSS 总复核，当前未发现新的真实 UI 问题。
46. `P3-12-D60`：Pencil 逐页 UI 与功能缺口复盘已完成，记录见 [P3-12-D60 Pencil 逐页 UI 与功能缺口复盘](/records/p3-12-d60-pencil-page-ui-and-function-gap-review-2026-07-04)；本批纠正 D59 后续口径，确认 D59 smoke 不能证明页面接近 Pencil 设计稿或功能缺口已补齐。
47. `P3-12-D61`：Public Web Pencil 逐页实现当前发布前范围已完成，记录见 [P3-12-D61 Public Web `/discover` Pencil 首批实现记录](/records/p3-12-d61-public-web-discover-pencil-first-implementation-2026-07-04)；`P01-P14` 已覆盖公开发现、论坛、文档、商城、榜单、公开主页和移动公开任务流，`P15-P16` 公开聊天室 / 移动聊天回复流继续作为 Public 小专题内产品与 API 后置缺口。
48. `P3-12-D62`：Private / Author `/workbench` Pencil 首批实现已完成，记录见 [P3-12-D62 Private / Author Workbench 首批实现记录](/records/p3-12-d62-private-author-workbench-first-implementation-2026-07-04)；本批通过 Pencil MCP 抽查 `P01 / P21`，补 `/workbench` 继续处理队列、私域状态 rail 和 private 移动底栏承接。
49. `P3-12-D62`：`/me` 内容历史复访组首批实现已完成，记录见 [P3-12-D62 `/me` 内容历史复访组首批实现记录](/records/p3-12-d62-me-content-history-first-implementation-2026-07-05)；本批通过 Pencil MCP 抽查 `P03-P06 / P23`，补 `/me/content`、`/me/history`、`/me/attachments`、`/me/experience` 的私域子页任务面、指标、当前页预览 rail、公开详情来源返回、附件归属和经验流水上下文。
50. `P3-12-D62`：资产 / 订单 / 背包页面族首批实现已完成，记录见 [P3-12-D62 资产 / 订单 / 背包页面族首批实现记录](/records/p3-12-d62-assets-orders-inventory-first-implementation-2026-07-05)；本批通过 Pencil MCP 抽查 `P07-P11 / P24-P25`，补 `/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/order/:orderId`、`/shop/inventory` 的资产任务面、流水解释 rail、订单状态分组、订单详情状态侧栏、背包权益状态和来源回流。
51. `P3-12-D62`：通知 / 消息页面族首批实现已完成，记录见 [P3-12-D62 通知 / 消息页面族首批实现记录](/records/p3-12-d62-notifications-messages-first-implementation-2026-07-05)；本批通过 Pencil MCP 抽查 `P12-P13 / P26-P27`，补 `/notifications` 的通知范围汇总、目标分流 rail 和未读处理队列，补 `/messages` 的会话上下文 rail、路由恢复状态、会话队列和通知回流动作。
52. `P3-12-D62`：圈子 / 宠物页面族首批实现已完成，记录见 [P3-12-D62 圈子 / 宠物页面族首批实现记录](/records/p3-12-d62-circle-pet-first-implementation-2026-07-05)；本批通过 Pencil MCP 抽查 `P14-P15 / P28-P29`，补 `/circle` 的关系上下文 rail、当前页预览和公开路由边界，补 `/pet` 的照护状态 rail、优先动作队列和公开资料边界。
53. `P3-12-D62`：论坛 / Docs 作者页面族首批实现已完成，记录见 [P3-12-D62 论坛 / Docs 作者页面族首批实现记录](/records/p3-12-d62-forum-docs-author-first-implementation-2026-07-05)；本批通过 Pencil MCP 抽查 `P16-P20 / P30`，补 `/forum/compose` 作者发布上下文 rail、论坛详情作者模式 rail，以及 `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id` 的作者任务 rail。
54. `P3-12-D63`：Console 治理工作台首批实现已完成，记录见 [P3-12-D63 Console 治理工作台首批实现记录](/records/p3-12-d63-console-governance-workbench-first-implementation-2026-07-05)；本批通过 Pencil MCP 抽查 `P02-P03 / P07-P08`，补内容审核任务流、目标证据卷宗、最近治理留痕上下文、经验台账任务流和台账证据 / 动作 rail。
55. `P3-12-D63`：Console 商业运营页面族首批实现已完成，记录见 [P3-12-D63 Console 商业运营页面族首批实现记录](/records/p3-12-d63-console-commerce-ops-first-implementation-2026-07-05)；本批通过 Pencil MCP 抽查 `P10`，补 `/console/products` 商品运营任务流、库存 / 售卖 / 订单回流摘要和 `/console/orders` 订单运营任务流、失败优先上下文与商业动作 rail。
56. `P3-12-D63`：Console 文档治理页面族首批实现已完成，记录见 [P3-12-D63 Console 文档治理页面族首批实现记录](/records/p3-12-d63-console-document-governance-first-implementation-2026-07-05)；本批通过 Pencil MCP 抽查 `P11`，补 `/console/documents` 文档治理任务流、发布 / 访问 / 版本上下文和文档证据 rail。
57. `P3-12-D63`：Console 用户管理表格 CRUD 首批实现已完成，记录见 [P3-12-D63 Console 用户管理表格 CRUD 首批实现记录](/records/p3-12-d63-console-user-management-table-crud-first-implementation-2026-07-05)；本批通过 Pencil MCP 抽查 `P05`，补 `/console/users` 用户对象任务流、状态队列、公开索引 / 更新时间列、选中对象摘要 rail 和详情 / 筛选回流。
58. `P3-12-D63`：Console 权限矩阵页面族首批实现已完成，记录见 [P3-12-D63 Console 权限矩阵页面族首批实现记录](/records/p3-12-d63-console-rbac-permission-matrix-first-implementation-2026-07-05)；本批通过 Pencil MCP 抽查 `P12`，补 `/console/roles/:roleId/permissions` 授权快照任务流、权限矩阵摘要、实时权限预览和授权证据 rail。
59. `P3-12-D63`：Console 成组静态收口与后置缺口整理已完成，记录见 [P3-12-D63 Console 成组静态收口与后置缺口整理记录](/records/p3-12-d63-console-grouped-static-closure-and-deferred-gap-review-2026-07-05)；本批确认 D63 目标页没有目标样式 / 直接请求 / 控制台日志残留，独立移动 Console、内部 Jobs 平台和新的治理 API 继续后置。
60. `P3-12-D63`：Console Gateway 成组真实页面复核已完成，记录见 [P3-12-D63 Console Gateway 成组真实页面复核记录](/records/p3-12-d63-console-gateway-grouped-smoke-2026-07-05)；本批覆盖 Gateway 登录回流、Console 七个 D63 目标页的 PC `1920x1080` 与移动 `390x844` CSS 视口，未发现阻断级页面问题。
61. `P3-12-D64`：UI 专题候选前集中验收准备已完成，记录见 [P3-12-D64 UI 专题候选前集中验收准备](/records/p3-12-d64-ui-candidate-acceptance-prep-2026-07-05)；本批汇总 D61-D63 重新实现后的 Public Web、Private / Author 和 Console 证据、剩余限制与 D65 验证清单，不执行新的真实 Gateway 页面联调。
62. `P3-12-E`：后置到 P3-12-D UI 实现与功能缺口真实完成之后，不提前创建发布 tag。

## 当前不做

- 不恢复 `P3-11` PR 决策作为当前主线。
- 不把 P3-12 做成 WebOS 全量复制。
- 不启动 Flutter 完整能力套件。
- 不启动电子宠物经济扩展、推荐算法、ActivityPub / WebFinger、完整 PWA、完整 E2E 平台或完整可观测性平台。
- 不在没有 Pencil 设计稿和说明文档的情况下推进页面级 UI 美化专题。
