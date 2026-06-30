# P3-12 Web 完全化与 WebOS 收束

> 状态：`P3-12-B1 代码侧主路径已完成；P3-12-C1 首轮 B1 直接残留清理已完成；P3-12-B2 完整个人中心 Web 化首批代码和正式链接语义补口已完成；P3-12-B3 论坛作者态 Web 化首批代码与小阶段验收已完成；P3-12-B4-1 正式 Web 文档作者入口首批代码已完成；P3-12-B4-2 Console 文档治理首批代码已完成；B4 / D1 阶段运行态 smoke 已完成；P3-12-B5 Web 功能总入口首批代码与 Gateway smoke 已完成；P3-12-B6 身份语义二次收口代码侧与启动前验证已完成；P3-12-D1-D13 已完成设计准备、client 视觉实现与私域 / 作者态成组验收；P3-12-D14-D30 已完成 radish.console 壳层首批、表格代表页、文档治理首屏、标签 / 分类普通列表、贴纸类列表迁移、成组静态收口、复杂页面类型边界评估、角色权限、治理工作台外层语义、点名内部区块样式迁移、治理页面成组静态收口、系统工具 / 运维外壳收口、阶段静态收口、深层表单静态收口和详情 / 抽屉静态收口；当前下一顺位是阶段真实验收或低收益静态扫尾评估`
>
> 启动日期：2026-06-21（Asia/Shanghai）
>
> 本页承接 `P3-11` 暂缓 PR 后的下一条正式开发主线。快速入口仍以 [当前进行中](/planning/current) 为准；P3-11 收束决策见 [P3-11 阶段收束决策记录](/records/p3-11-stage-closure-decision-record-2026-06-21)，P3-12-A 盘点结论见 [P3-12-A WebOS 与 Web 功能资产盘点记录](/records/p3-12-a-webos-web-function-asset-inventory-2026-06-21)，B1 方案见 [P3-12-B1 账户资产与商城交易 Web 化方案](/records/p3-12-b1-account-shop-web-plan-2026-06-21)，C1 首轮清理见 [P3-12-C1 WebOS 残留入口清理记录](/records/p3-12-c1-webos-residual-cleanup-2026-06-21)，B2 方案见 [P3-12-B2 完整个人中心 Web 化方案](/records/p3-12-b2-personal-center-web-plan-2026-06-21)，B3 方案见 [P3-12-B3 论坛作者态 Web 化方案](/records/p3-12-b3-forum-author-web-plan-2026-06-21)，B4 方案见 [P3-12-B4 文档作者态归属裁决](/records/p3-12-b4-doc-author-ownership-plan-2026-06-22)，B4-2 设计见 [P3-12-B4-2 Console 文档治理设计](/records/p3-12-b4-2-console-doc-governance-design-2026-06-22)，阶段 smoke 见 [P3-12 B4 / D1 阶段运行态 Smoke 记录](/records/p3-12-b4-d1-stage-smoke-record-2026-06-22)，B5 设计见 [P3-12-B5 Web 功能总入口设计](/records/p3-12-b5-web-workbench-entry-design-2026-06-22)，B6 设计见 [P3-12-B6 身份语义二次收口设计](/records/p3-12-b6-identity-contract-convergence-design-2026-06-22)，D1 准备见 [P3-12-D1 统一 UI 设计准备记录](/records/p3-12-d1-unified-ui-design-prep-2026-06-22)，D2 设计源记录见 [P3-12-D2 公开 Web 统一体验设计源记录](/records/p3-12-d2-public-web-unified-design-source-2026-06-24)，D3 设计源记录见 [P3-12-D3 私域与作者态 Web 工作流设计源记录](/records/p3-12-d3-private-web-workflows-design-source-2026-06-25)，D4 共享基座记录见 [P3-12-D4 Web UI 共享基座设计源记录](/records/p3-12-d4-web-ui-foundation-design-source-2026-06-25)，D5 Console 设计源记录见 [P3-12-D5 Console 治理工作台设计源重构记录](/records/p3-12-d5-console-governance-workbench-redesign-2026-06-27)，D6 Console 实现前盘点见 [P3-12-D6 Console 视觉代码实现前盘点](/records/p3-12-d6-console-visual-code-prep-2026-06-27)，D7 移动导航统一见 [P3-12-D7 移动导航设计源统一记录](/records/p3-12-d7-mobile-navigation-design-source-alignment-2026-06-28)，D8 client 首批实现见 [P3-12-D8 radish.client 视觉实现首批记录](/records/p3-12-d8-radish-client-visual-first-implementation-2026-06-28)，D14-D30 Console 视觉实现记录见 [P3-12-D14 radish.console 视觉代码实现首批记录](/records/p3-12-d14-radish-console-visual-first-implementation-2026-06-29)、[P3-12-D15 订单表格代表页视觉迁移记录](/records/p3-12-d15-radish-console-order-table-visual-migration-2026-06-29)、[P3-12-D16 用户表格代表页视觉迁移记录](/records/p3-12-d16-radish-console-user-table-visual-migration-2026-06-29)、[P3-12-D17 商品表格代表页视觉迁移记录](/records/p3-12-d17-radish-console-product-table-visual-migration-2026-06-29)、[P3-12-D18 文档治理页区块边界与首批语义迁移记录](/records/p3-12-d18-radish-console-document-governance-visual-boundary-2026-06-29)、[P3-12-D19 标签与分类列表视觉迁移记录](/records/p3-12-d19-radish-console-taxonomy-list-visual-migration-2026-06-29)、[P3-12-D20 贴纸列表视觉迁移记录](/records/p3-12-d20-radish-console-sticker-visual-migration-2026-06-30)、[P3-12-D21 表格视觉成组静态收口记录](/records/p3-12-d21-radish-console-table-visual-static-closure-2026-06-30)、[P3-12-D22 Console 复杂页面类型边界评估记录](/records/p3-12-d22-console-complex-page-boundary-2026-06-30)、[P3-12-D23 角色权限外层语义迁移记录](/records/p3-12-d23-radish-console-role-permission-visual-migration-2026-06-30)、[P3-12-D24 治理工作台外层语义收口记录](/records/p3-12-d24-radish-console-governance-workbench-outer-visual-convergence-2026-06-30)、[P3-12-D25 治理工作台内部区块样式收口记录](/records/p3-12-d25-radish-console-governance-workbench-internal-style-convergence-2026-06-30)、[P3-12-D26 治理页面成组静态收口记录](/records/p3-12-d26-radish-console-governance-static-closure-2026-06-30)、[P3-12-D27 系统工具与运维外壳收口记录](/records/p3-12-d27-radish-console-system-ops-shell-2026-06-30)、[P3-12-D28 阶段静态收口记录](/records/p3-12-d28-radish-console-stage-static-closure-2026-06-30)、[P3-12-D29 深层表单静态收口记录](/records/p3-12-d29-radish-console-deep-form-static-closure-2026-06-30) 与 [P3-12-D30 详情 / 抽屉静态收口记录](/records/p3-12-d30-radish-console-detail-drawer-static-closure-2026-06-30)。

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
   - P3-12 完成后进入正式版发布候选，而不是继续无限扫尾。
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

`P3-12-D15-D30` `radish.console` 表格、治理页面与复杂页面边界已连续推进：订单、用户、商品三类高频表格代表页已迁入 D14 语义组件；文档治理页先固定首屏区块边界，再迁移页头、指标和筛选区；标签 / 分类普通列表已完成同口径迁移并补 Gateway PC / 移动 CSS 视图真实联调；贴纸分组 / 分组表情列表已确认按普通 CRUD 外层迁移，不拆媒体资产工作台；D21 已完成 D14-D20 成组静态收口；D22 已确认角色权限归入 `P12`，内容治理归入 `P02`，经验治理归入 `P03`，系统设置与 Hangfire 外壳归入 `P13`；D23 已完成 `RoleList` 与 `RolePermissionPage` 外层语义迁移；D24 已完成内容治理与经验治理工作台外层语义收口；D25 已完成点名内部区块样式收口首批；D26 已完成角色权限、内容治理和经验治理页面成组静态收口；D27 已将 `/hangfire` 从路由临时组件迁入 `SystemTools/HangfirePage`，外层接入 Console 语义页头、指标和状态组件；D28 已将路由认证 / 加载状态旧 inline 样式迁入 CSS；D29 已将商品、分类、贴纸和贴纸分组表单的上传预览、隐藏输入、宽度规则、弱提示文本和弹窗 footer 样式迁入共享 CSS；D30 已将订单 / 商品详情、文档治理抽屉和贴纸批量上传提示色的剩余样式残留迁入 CSS 与 Console token。下一步若准备阶段验收，则在用户确认前后端已启动后做 Gateway PC / mobile 复核；若继续静态治理，先按目标扫描评估收益。

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

P3-12 功能迁移、残留清扫和 UI 设计实现完成后，再进入发布候选。

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
17. `P3-12-D15-D30`：`radish.console` 订单、用户、商品、文档治理首屏、标签 / 分类、贴纸类页面、角色权限页面、内容 / 经验治理工作台外层语义、点名内部区块样式迁移、治理页面成组静态收口、系统工具 / 运维外壳收口、阶段静态收口、深层表单静态收口和详情 / 抽屉静态收口已完成，D19 已补 Gateway PC / 移动 CSS 视图真实联调，D22 已完成复杂 Console 页面类型边界评估，D27 已确认 `/hangfire` 仍为外部 Dashboard 承载。
18. `P3-12-D31`：若进入阶段真实验收，先确认前后端已启动后覆盖 Gateway PC / mobile；若继续静态治理，先按目标扫描评估收益。
19. `P3-12-E`：阶段完成后再进入正式版发布候选，不提前创建发布 tag。

## 当前不做

- 不恢复 `P3-11` PR 决策作为当前主线。
- 不把 P3-12 做成 WebOS 全量复制。
- 不启动 Flutter 完整能力套件。
- 不启动电子宠物经济扩展、推荐算法、ActivityPub / WebFinger、完整 PWA、完整 E2E 平台或完整可观测性平台。
- 不在没有 Pencil 设计稿和说明文档的情况下推进页面级 UI 美化专题。
