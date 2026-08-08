# F4-R C-1 代表页代码事实审计

> 日期：2026-07-30；2026-08-08 更新（Asia/Shanghai）
>
> 状态：C-1A 与共享组件 / 主题基座已完成；C-1B 的 `R1-P01 / R1-P02 / R1-A01` 已关闭，`R1-W01` 设计前能力门禁已通过并进入下一顺位
>
> 范围：正式 Web（Public、Private / Author）与 Console；不含 WebOS 新功能、Tauri 和 Flutter 画板

## 1. 结论

当前代码不支持按路由逐页维护 Pencil，也不支持把 Console 所有后台页面压缩成同一种代表类型。

C-1A 最终裁决为：

- `7` 个 R1：`1` 个共享基础矩阵和 `6` 个完整页面类型；
- `4` 个 R2：只维护关键区块、状态或响应式差异；
- 其余正式路由按明确继承关系进入 R3，直接实现后用真实页面截图复核；
- Console 普通表格 / 明细与案件治理 / 审计拆成两个 R1，它们不再共用一张完整代表画板；
- 功能、按钮、权限、文案和状态机继续服从专题与代码，以下裁决只定义本批视觉与响应式设计投入。

## 2. 审计依据

### 2.1 路由与壳层

- Client 正式入口以 `Frontend/radish.client/src/bootstrap/entryRoute.ts` 和 `BrowserAppRouter.tsx` 为准，包含 Public、Private、Author 与登录回流入口。
- Public 当前覆盖 Discover、Forum 列表 / 搜索 / 标签 / 详情 / 发布、Docs 列表 / 详情、Shop、排行榜、公开主页与 Legal。
- Private 当前覆盖 Messages、Notifications、Me、Circle、Pet、Private Shop 与 Workbench。
- Author 当前覆盖 Docs Mine、Compose、Edit 与 Revisions。
- Console 以 `Frontend/radish.console/src/router/routes.ts`、`routeMeta.ts` 和 `AdminLayout.tsx` 为准，覆盖普通资源表格、订单、权限、设置、治理与审计页面。

### 2.2 共享结构

- `WebShellHeader` 同时承载 Public / Private PC 顶栏和 mobile 底部导航，已被多数正式 Web 页面复用。
- `WebStateSlot` 是跨 Public / Private 的加载、空态、错误和不可用状态入口。
- `AdminLayout`、`ConsolePage` 和 `adminFeature.css` 已形成 Console 导航、页面头、指标、表格、侧栏和响应式基线。
- 四主题、双语和 PC / mobile 是共享契约，不为每个路由复制画板。

### 2.3 代码证明存在的页面类型

| 代码事实 | 结构判断 |
| --- | --- |
| `PublicDiscoverApp` 为内容流 + 辅助侧栏，mobile 转为单列、横向筛选与任务条 | Public 内容流 |
| `PublicForumDetail` 同时包含阅读、回答、评论、Reaction、版本、赞赏和收藏状态 | Public 详情与互动 |
| `DocsAuthorApp` 包含列表、编辑、修订、冲突和协作状态 | Author 编辑 / 发布 |
| `ChatApp` 在 PC 使用列表—详情工作台，mobile 按会话焦点切换列表和主区 | Private 消息工作台 |
| `OrderList` 使用指标、筛选、高密度表格、上下文侧栏与详情 / 重试动作 | Console 表格 / 明细 |
| `ModerationPage` 使用案件队列、证据、决定、纠正动作、事件时间线和版本冲突 | Console 案件治理 / 审计 |

`OrderList` 与 `ModerationPage` 的主任务、信息密度、动作风险和 mobile 转换均不同，因此原候选“Console 表格治理与详情 / 审计组合页”必须拆分。

## 3. 评分说明

评分维度按[代表页协作流程](/frontend/pencil-representative-page-workflow)执行：

- `B`：业务关键度；
- `L`：布局独特性；
- `S`：状态复杂度；
- `R`：复用影响面；
- `M`：响应式风险；
- `U`：现状不确定性。

自动升级条件和评分只判断“本批目标视觉是否需要建立或改变该交互契约”。页面虽然已有高风险动作，但本批明确不改变其行为且能完整继承既有契约时，不因功能本身机械升级；关键动作区仍需纳入 R2 / R3 继承与真实页面复核。

## 4. R1 完整代表类型

| 编号 | 代表类型与真实锚点 | B/L/S/R/M/U | 总分 | 自动升级依据 | 活动设计源 |
| --- | --- | --- | --- | --- | --- |
| `R1-F01` | 共享组件、状态、主题与壳层矩阵 | `2/2/2/2/2/1` | `11` | 改变跨 Public、Private / Author、Console 复用契约 | `radish-web-family-ui-v1.pen`（已完成） |
| `R1-P01` | Public 内容流；锚点 `/discover` | `2/2/1/2/2/1` | `10` | 建立家族化内容流和 Public 响应式语法 | `radish-web-family-ui-v1.pen`（PC / mobile、实现与运行态已关闭） |
| `R1-P02` | Public 详情与互动；锚点 `/forum/post/:id` | `2/2/2/2/2/1` | `11` | 核心写操作、多状态、跨 PC / mobile 互动模型 | `radish-web-family-ui-v1.pen`（PC / mobile、实现与运行态已关闭） |
| `R1-A01` | Author 编辑 / 协作 / 提交审核；锚点 `/docs/edit/:id` | `2/2/2/1/2/1` | `10` | 草稿、冲突、修订、协作与高价值写操作 | `radish-web-family-ui-v1.pen`（PC / mobile、实现与运行态已关闭） |
| `R1-W01` | Private 消息列表—详情；锚点 `/messages` | `2/2/2/1/2/1` | `10` | 实时状态，mobile 在列表与会话间切换交互模型 | `radish-web-family-ui-v1.pen`（readiness 与能力门禁已完成） |
| `R1-C01` | Console 表格 / 明细；锚点 `/console/orders` | `2/1/2/2/2/1` | `10` | 高密度表格与 mobile 收敛规则影响多个资源页 | `radish-web-family-ui-v1.pen` |
| `R1-C02` | Console 案件治理 / 审计；锚点 `/console/moderation` | `2/2/2/2/2/1` | `11` | 权限、证据、多步骤决定、纠正动作与冲突 | `radish-web-family-ui-v1.pen` |

R1 只在唯一活动设计源中维护必要代表设计，不为主题、locale、权限和每个状态复制完整页面。业务类型必须提供可按真实尺寸独立审核的 PC / mobile 顶层业务画板；关键状态与辅助说明可单独成板，复合说明板中的嵌入缩略图不计作业务页面完成。原四个按领域拆分的 `.pen` 只读留档，不再作为当前画板落点。

`R1-P01` 的稳定代码事实现为：`/discover` 只消费统一 `PublicDiscoverFeedVo`，由服务端按 snapshot cutoff 与稳定 keyset 游标合并 `ChannelSummary / MemberActivity / HighlightedComment / Post / Question`；Channel 摘要必须经 Console 显式 opt-in，页面不再自行拼装帖子、热门标签、Wiki 或商品列表。分类、`#标签` 与结构化状态仍由 Forum 列表 / 详情分层表达，不挤入紧凑混合轨道。`2026-08-04`—`08-05` 已完成公开能力审计、PC / mobile 视觉确认、代码实现与 Gateway 阶段性运行态复核，最终结构以国风暖白表面、无衬线层级、克制圆角阴影、灰玉品牌语义、墨蓝操作语义和内嵌数据反馈建立现代自然紧凑气质；活动设计源只保留正式 PC / mobile 和必要母版。匿名 / 登录回流、双语、代表主题、Console 治理只读路径与横向溢出检查均通过，运行态发现的 Int64 wire contract 偏差已在统一 HTTP 契约层修正。详见[结构研究记录](/records/f4-r-r1-p01-public-discover-structure-study-2026-08-04)、[设计记录](/records/f4-r-r1-p01-mobile-public-read-model-design-2026-08-05)与[实现记录](/records/f4-r-r1-p01-public-discover-implementation-2026-08-05)。全局品牌 token 尚未机械替换，继续由共享主题批治理。

`R1-P02` 已于 `2026-08-05` 完成[代码事实与设计边界审计](/records/f4-r-r1-p02-public-detail-interaction-audit-2026-08-05)，并于 `2026-08-06` 完成[正式 Web 能力覆盖复核](/records/f4-r-formal-web-capability-coverage-audit-2026-08-06)。随后已完成 PC / mobile 正式代表设计、成组实现和 Gateway 运行态验收；普通帖子正式 Web 已承接帖子 / 回帖点赞、reaction、赞赏和两级回帖，详见[实现记录](/records/f4-r-r1-p02-public-detail-implementation-2026-08-08)。

`R1-A01` 已于 `2026-08-08` 完成[设计前代码事实与能力覆盖门禁](/records/f4-r-r1-a01-author-readiness-audit-2026-08-08)。正式代表身份固定为“登录普通 Owner + Custom 已发布文档 + `Editing` 活跃共享草稿 + Accepted Editor”；普通 Author Revision 读取、终态审核证据、写响应证据和 Apply 基准版本 CAS 随后已按[能力门禁修复记录](/records/f4-r-r1-a01-author-capability-gate-implementation-2026-08-08)闭合。同日完成 PC / mobile 正式代表设计、页面实现与 Gateway 运行态验收；标题 / Markdown 正文主轴、`320px` context rail、Mobile Bottom Sheet、权限停止线与临时数据清理均已关闭，详见[成组实现记录](/records/f4-r-r1-a01-author-editor-implementation-2026-08-08)。

`R1-W01` 已于 `2026-08-08` 完成[消息工作区设计前代码事实与能力覆盖门禁](/records/f4-r-r1-w01-messages-readiness-audit-2026-08-08)。正式 `/messages` 与 WebOS 共用同一 Chat 核心，主体能力没有迁移缺口；举报 ACL / LongId、失败重试幂等和历史错误契约已由同日[能力门禁修复](/records/f4-r-r1-w01-messages-capability-gate-implementation-2026-08-08)闭合，撤回能力证据与频道角色权限另需裁决。R1-A01 关闭后，R1-W01 已切换为下一正式代表设计顺位；Pencil 可用前不提前制作画板。

## 5. R2 局部设计类型

| 编号 | 局部类型 | B/L/S/R/M/U | 总分 | 只需确认的差异 | 继承来源 |
| --- | --- | --- | --- | --- | --- |
| `R2-P03` | Public 只读详情变体 | `1/1/1/2/1/0` | `6` | 元数据、目录 / 购买 / 关注 CTA、长内容与 mobile 顺序 | `R1-P02`、`R1-F01` |
| `R2-W02` | Private 仪表 / 任务侧栏 | `1/1/1/2/1/0` | `6` | 主任务、状态摘要、辅助侧栏在 mobile 的排序与折叠 | `R1-F01`、`R1-W01` |
| `R2-A02` | Author 列表、修订与 Forum 发布差异 | `1/1/2/1/1/0` | `6` | 修订时间线、列表操作、发布器关键状态，不复制完整编辑页 | `R1-A01`、`R1-P02` |
| `R2-C03` | Console 设置与权限矩阵 | `1/1/2/1/1/0` | `6` | 分区、权限矩阵、危险确认和 mobile 只读 / 低风险边界 | `R1-C01`、`R1-C02` |

R2 交付物可以是关键区块、状态带或交互序列；若设计时发现新壳层、结构冲突或 mobile 新交互模型，再升级为 R1。

## 6. R3 路由继承关系

| 路由族 | 继承规则 | 允许的局部差异 |
| --- | --- | --- |
| Forum 列表 / 搜索 / 标签 / 类型流、Docs 列表 / 搜索、Shop 列表、排行榜 | `R1-P01` + `R2-P03` | 筛选项、卡片字段、排序、分页和业务 CTA |
| Docs 详情、商品详情、公开主页、Legal | `R1-P02` + `R2-P03` | 只读内容结构、目录、价格、关系状态或法务长文本 |
| Notifications、Me、Circle、Pet、Private Shop、Workbench | `R1-F01` + `R2-W02` | 业务卡片、指标、状态摘要和已有表单 |
| Docs Mine / Revisions、Forum Compose | `R1-A01` + `R2-A02` | 列表操作、修订信息和 Forum 字段 |
| Applications、Products、Users、Categories、Tags、Stickers、Coins | `R1-C01` | 字段、筛选、批量动作、详情内容和权限 |
| Documents、Experience、Appeals | `R1-C01` + `R1-C02` | 资源表格、治理详情、事件或复核动作 |
| Roles / Permissions、System Config、Settings、Profile | `R1-C01` + `R2-C03` | 表单分区、权限单元和危险确认 |
| 登录、OIDC 回流、Not Found 等边界页 | `R1-F01` | 认证文案、错误原因和返回动作 |

每个 R3 实现批次仍需写明代表来源、局部差异、关键状态与 mobile 转换。发现继承不成立时停止扩张并升级分级。

## 7. 本批不纳入代表设计

- WebOS `/desktop` 只保留历史兼容，不承接 F4-R 新页面类型。
- Tauri 暂时弃用，不进入 UI、实现或验收矩阵。
- Flutter 等 Web 视觉契约稳定后，按原生高价值路径重新评分，不复制 Web 画板。
- Console `theme-test` 是内部验证入口，Hangfire 是外部运维工作面，不作为产品代表页。
- C-1A 不改变路由、功能、权限、API 或运行时行为，也不启动服务或执行真实 smoke。

## 8. C-1B 执行顺序

1. `R1-F01` 已在 `radish-web-family-ui-v1.pen` 完成，固定共享契约；`R1-P01` 历史失败稿只由 Git 留档，最终 PC / mobile 已确认为正式视觉基准。
2. 参考 `13 / 16 / 18 / 27` 的结构转译，以及帖子之外公开实体、API、路由、权限与失败边界审计已经完成。
3. `R1-P01` 的统一公开读模型、Channel opt-in 治理、`@radish/http`、页面结构、双语资源和静态测试已经成组实现，代码侧构建与定向回归通过。
4. `R1-P01` 阶段性 PC / mobile 真实截图与交互复核已通过并关闭；未暴露需要回写 Pencil 的共享结构偏差。
5. `R1-P02` 的 PC / mobile 正式代表设计、成组实现和 Gateway 运行态验收已关闭。
6. `R1-A01` readiness、能力修复、PC / mobile 正式代表设计、页面实现和 Gateway 运行态验收已完成并关闭。
7. 后续设计轨切换为 `R1-W01 → R1-C01 → R1-C02`；R2 只补必要差异，不扩为路由镜像。
8. Pencil 可用后直接按既有 readiness 与能力门禁进入 R1-W01 PC 正式代表设计，不重新全量审计；确认后再继续 Mobile 与必要关键状态区。
9. 每完成一个 R1，先对照当前代码确认功能与文案，再进入对应共享组件和代表页实现；代表页代码落地并完成截图复核后，以复核结果驱动 R3 成组实现。

进入后续 R1 / R2 前，代码事实核对必须同时回答正式 Web 是否承接了仍有价值的既有能力；不得把 WebOS 历史来源排除在设计矩阵之外后，又把正式 Web 的暂时缺口当作产品停止线。F4-R 完成后的视觉工作默认继承本批家族 UI 基线进行优化更新，除非新产品形态或结构性冲突经过明确裁决，不从零重新设计。
