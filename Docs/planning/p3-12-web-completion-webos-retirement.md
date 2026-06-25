# P3-12 Web 完全化与 WebOS 收束

> 状态：`P3-12-B1 代码侧主路径已完成；P3-12-C1 首轮 B1 直接残留清理已完成；P3-12-B2 完整个人中心 Web 化首批代码和正式链接语义补口已完成；P3-12-B3 论坛作者态 Web 化首批代码与小阶段验收已完成；P3-12-B4-1 正式 Web 文档作者入口首批代码已完成；P3-12-B4-2 Console 文档治理首批代码已完成；B4 / D1 阶段运行态 smoke 已完成；P3-12-B5 Web 功能总入口首批代码与 Gateway smoke 已完成；P3-12-B6 身份语义二次收口代码侧与启动前验证已完成；P3-12-D1 统一 UI 设计准备已启动；P3-12-D2 公开 Web 统一体验设计源 P01-P05 与说明已完成`
>
> 启动日期：2026-06-21（Asia/Shanghai）
>
> 本页承接 `P3-11` 暂缓 PR 后的下一条正式开发主线。快速入口仍以 [当前进行中](/planning/current) 为准；P3-11 收束决策见 [P3-11 阶段收束决策记录](/records/p3-11-stage-closure-decision-record-2026-06-21)，P3-12-A 盘点结论见 [P3-12-A WebOS 与 Web 功能资产盘点记录](/records/p3-12-a-webos-web-function-asset-inventory-2026-06-21)，B1 方案见 [P3-12-B1 账户资产与商城交易 Web 化方案](/records/p3-12-b1-account-shop-web-plan-2026-06-21)，C1 首轮清理见 [P3-12-C1 WebOS 残留入口清理记录](/records/p3-12-c1-webos-residual-cleanup-2026-06-21)，B2 方案见 [P3-12-B2 完整个人中心 Web 化方案](/records/p3-12-b2-personal-center-web-plan-2026-06-21)，B3 方案见 [P3-12-B3 论坛作者态 Web 化方案](/records/p3-12-b3-forum-author-web-plan-2026-06-21)，B4 方案见 [P3-12-B4 文档作者态归属裁决](/records/p3-12-b4-doc-author-ownership-plan-2026-06-22)，B4-2 设计见 [P3-12-B4-2 Console 文档治理设计](/records/p3-12-b4-2-console-doc-governance-design-2026-06-22)，阶段 smoke 见 [P3-12 B4 / D1 阶段运行态 Smoke 记录](/records/p3-12-b4-d1-stage-smoke-record-2026-06-22)，B5 设计见 [P3-12-B5 Web 功能总入口设计](/records/p3-12-b5-web-workbench-entry-design-2026-06-22)，B6 设计见 [P3-12-B6 身份语义二次收口设计](/records/p3-12-b6-identity-contract-convergence-design-2026-06-22)，D1 准备见 [P3-12-D1 统一 UI 设计准备记录](/records/p3-12-d1-unified-ui-design-prep-2026-06-22)，D2 设计源记录见 [P3-12-D2 公开 Web 统一体验设计源记录](/records/p3-12-d2-public-web-unified-design-source-2026-06-24)。

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

`P3-12-D1` 统一 UI 设计准备已启动，结论见 [P3-12-D1 统一 UI 设计准备记录](/records/p3-12-d1-unified-ui-design-prep-2026-06-22)。当前已完成页面矩阵、设计源拆分、停止线和后续执行顺序；用户确认前后端已启动后，B4 / D1 阶段 Gateway PC / mobile smoke 已补齐，结论见 [P3-12 B4 / D1 阶段运行态 Smoke 记录](/records/p3-12-b4-d1-stage-smoke-record-2026-06-22)。

`P3-12-D2` 公开 Web 统一体验设计源已补齐，结论见 [P3-12-D2 公开 Web 统一体验设计源记录](/records/p3-12-d2-public-web-unified-design-source-2026-06-24)，实现口径见 [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)。已创建 `Docs/frontend/design-sources/public-web-unified-experience.pen`，写入 `rx-*` 视觉变量，并补 `P01 - Public Web Shell Foundation`、`P02 - Discover Content Stream`、`P03 - Public Detail Reading`、`P04 - Public Collection Pages` 与 `P05 - Mobile Public Single Column`。本批仍停留在 Pencil 设计源和说明文档阶段，不进入 `radish.client` 视觉代码；下一步转入私域 / 作者态设计源与 Console 文档治理差异画板。

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
9. `P3-12-D1 / D2`：统一 UI 设计准备已启动，见 [P3-12-D1 统一 UI 设计准备记录](/records/p3-12-d1-unified-ui-design-prep-2026-06-22)；公开 Web 设计源已补 `P01-P05`，见 [P3-12-D2 公开 Web 统一体验设计源记录](/records/p3-12-d2-public-web-unified-design-source-2026-06-24) 与 [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)。后续进入私域 / 作者态设计源与 Console 文档治理差异画板。
10. `P3-12-E`：阶段完成后再进入正式版发布候选，不提前创建发布 tag。

## 当前不做

- 不恢复 `P3-11` PR 决策作为当前主线。
- 不把 P3-12 做成 WebOS 全量复制。
- 不启动 Flutter 完整能力套件。
- 不启动电子宠物经济扩展、推荐算法、ActivityPub / WebFinger、完整 PWA、完整 E2E 平台或完整可观测性平台。
- 不在没有 Pencil 设计稿和说明文档的情况下推进页面级 UI 美化专题。
