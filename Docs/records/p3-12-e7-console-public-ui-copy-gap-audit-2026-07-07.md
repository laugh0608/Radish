# P3-12-E7 Console / Public UI 与文案成熟度首批差距审计

> 日期：2026-07-07（Asia/Shanghai）
>
> 主线：`P3-12-E 正式产品成熟度与质量硬化`
>
> 性质：只读审计与修复排序。本轮不改接口、后端行为、权限、审计、错误模型或运行时契约，不执行真实 Gateway smoke，不进入 `P3-12-F`。

## 输入范围

- 规划入口：[当前进行中](/planning/current)、[P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)、[P3-12-E 正式产品成熟度与质量硬化](/planning/p3-12-product-maturity-quality-hardening)。
- Console 设计口径：[Console 治理工作台设计说明](/frontend/console-governance-workbench-design)。
- Pencil 设计源：通过 Pencil MCP 读取 `Docs/frontend/design-sources/console-governance-workbench.pen` 的 `P00-P18`，未直接读写 `.pen` 文件。
- 代码静态审计：`Frontend/radish.console/src`、`Frontend/radish.client/src/i18n.ts`、`Frontend/radish.client/src/public`、`Radish.Auth/Views/Authorization/Consent.cshtml`。

## 结论

`P3-12-E7` 继续有效，不能进入 `P3-12-F`。今天的首批审计确认：问题不是单个截图或单页文案，而是三个成组缺口。

1. Console 当前 PC 代表页多数已有 D63 任务面，但 `/console/` Dashboard 仍是旧 `admin-feature` 总览结构，未对齐 `P04` 调度中心和 D14 语义组件。
2. Console 移动端仍是折叠左侧栏模型，未落到 `P14-P18` 的底部任务 tab、路由 Hub 和单列治理任务流。
3. Public / Docs / Forum / Shop / Auth 仍有一批用户可见的内部术语和实现说明文案，集中在 `公开壳层`、`公开路由`、`正式 Web 私域路由`、`桌面工作台`、`reaction`、`PUBLIC / docs`、`Client ID / scope` 等词簇。

因此下一批不应零散修单个文案，应按 `E7-A Console`、`E7-B Public 术语清理`、`E7-C Public / Docs / Forum / Shop 信息密度`、`E7-D Auth 授权页层级` 分组推进。

## Console 与 Pencil 差距矩阵

| 页面族 | 设计源目标 | 当前实现状态 | 判断 | 下一步 |
| --- | --- | --- | --- | --- |
| 公共壳层 `P00 / P01 / P09` | 纸色 Console shell、浅色图标侧栏、84 高命令栏、全局搜索、权限过滤、路由分组。 | `AdminLayout` 已有 300px 侧栏、84px 顶栏、路由分组和全局搜索；但品牌副标题仍显示 `Case Desk`，移动端仍折叠左侧栏。 | 部分满足。`Case Desk` 属旧模板残留，移动模型不符合设计源。 | 并入 `E7-A` 修复。 |
| Dashboard / 调度总览 `P04` | 高密度 Dispatch Center：指标、跨模块治理负载、今日处理队列和任务分派。 | `Dashboard.tsx` 仍使用 `admin-feature-card`、`admin-feature-metrics`、快速操作、最近订单和右侧调度列表；未使用 `ConsolePageHeader / ConsoleMetricGrid / ConsoleToolbar`。 | 阻断级。当前更像入口卡片页，不像正式 Console 总览。 | `E7-A1` 优先重构 Dashboard 为高密度调度页。 |
| 内容治理 `P02 / P07` | PC 三栏：举报队列、证据卷宗、处置动作 / 留痕；移动单列：当前案件、证据矩阵、处理动作、审计快照。 | PC 已有工作台、证据 rail、处理动作和日志；新增 `Mobile Governance` 说明块，但移动仍依赖同一页面纵向堆叠和折叠侧栏。 | PC 基本满足，移动缺口明显。 | `E7-A2` 先做移动处理顺序真实承载，不只保留说明块。 |
| 经验治理 `P03 / P08` | 用户摘要、趋势证据、流水定位、复核动作；移动端单列 Ledger flow。 | PC 已有指标、任务流和经验工作台；移动端仍是响应式堆叠，没有底部任务 tab 与独立 Ledger flow。 | PC 部分满足，移动缺口明显。 | 并入 `E7-A2`，与内容治理成组处理。 |
| 商业运营 `P10 / P15` | 商品 / 订单任务面，库存、售卖、失败订单和履约动作优先。 | `ProductList`、`OrderList` 已有指标、任务流、筛选、表格和右侧摘要；移动端会把四个任务卡、筛选和表格纵向堆叠。 | PC 基本满足，移动效率待治理。 | `E7-A3` 在移动端压缩任务流和筛选首屏。 |
| 文档治理 `P11 / P16` | 文档队列、详情证据、访问策略、版本回滚，移动端发布 / 策略 / 回滚流程。 | `DocumentGovernancePage` 已有任务流、筛选、表格和治理详情 rail；仍有较长解释性文案。 | 发布前建议。 | `E7-A3` 压缩说明，保留动作和证据。 |
| 权限矩阵 `P12 / P17` | 角色列表、权限矩阵、授权证据和高危授权提示；移动端按权限分组确认。 | `RolePermissionPage` 已有矩阵、预览、授权 rail；但 `RBAC MATRIX`、接口映射和权限键直接占据显眼位置。 | 发布前建议。后台可保留技术细节，但首屏应优先角色、变更和风险。 | `E7-A3` 重新分层，把技术详情放入二级区域。 |
| 运维工具 `P13 / P18` | 当前代表页可承接受保护 Hangfire 外壳，完整 Jobs 平台后置。 | `HangfirePage` 明示 `外部 Dashboard`、`iframe`、`内部任务台 未启用`、`任务队列、重试和审计 API 尚未定义`。 | 发布前建议。文案过于实现说明。 | `E7-A4` 改成正式运维边界语言，不暴露未定义 API 作为主信息。 |
| 移动 Console `P14-P18` | 底部 `总览 / 治理 / 资产 / 权限 / 运维`，路由 Hub、优先队列、单列任务卡。 | `AdminLayout` 在 `max-width: 768px` 下将侧栏折叠为 64px，仍保留左侧图标导航；没有底部任务 tab 和移动 Hub。 | 阻断级。当前不能证明移动治理视图成熟。 | `E7-A2` 需要先做方案再改代码。 |

## Public / Docs / Forum / Shop / Auth 术语矩阵

| 页面族 | 静态命中 | 判断 | 下一步 |
| --- | --- | --- | --- |
| Discover | `公开入口`、`可继续进入`、统计 / 摘要式状态文案。 | 阻断进入 F。发现页仍有入口验收感，不像社区首页。 | `E7-B1` 先清术语，`E7-C1` 再评估首屏密度。 |
| Docs Public | `公开 docs`、`公开壳层`、`桌面治理流程`、`公开 docs 阅读工具`。 | 阻断进入 F。阅读页仍解释路由和治理边界。 | `E7-B1` 替换为读者任务语言；`E7-C2` 压缩目录 / rail。 |
| Forum Public | `reaction`、`公开壳层`、`桌面工作台交互`、`公开路由`。 | 阻断进入 F。正式讨论页不应暴露英文技术词和路由解释。 | `E7-B1` 把 `reaction` 统一改为 `表情回应 / 贴纸回应` 等产品词。 |
| Shop Public | `正式 Web 私域路由`、`私域路由`、`公开壳层`、购买回流说明过长。 | 阻断进入 F。商城页仍像迁移说明。 | `E7-B1` 先改文案，`E7-C3` 再压缩说明卡。 |
| Leaderboard / Profile / Circle / Notifications | `公开路由只保留`、`路由边界`、`路由规则`、`账号私域路由`。 | 发布前建议。范围边界应保留，但不能以路由术语表达。 | `E7-B2` 统一术语表，替换为“公开可见 / 仅本人可见 / 登录后继续”等用户语言。 |
| Auth Consent | `Client ID`、`scope-code`、开发方 / 回调来源作为显眼首屏信息。 | 发布前必须审计。透明度有价值，但首屏应优先身份、应用信任、权限含义和确认动作。 | `E7-D` 降级技术标识为二级详情，保留必要可查性。 |

## 阻断级缺口清单

1. `/console/` Dashboard 未对齐 `P04`，仍是低密度入口卡片和快速操作页。
2. Console 移动端未对齐 `P14-P18`，仍是折叠左侧栏和桌面页面纵向堆叠。
3. Public / Docs / Forum / Shop 仍批量存在内部术语、路由解释和实现说明。
4. Forum 公开详情仍使用 `reaction` 作为用户可见术语。
5. Auth 授权页把 `Client ID` 和原始 scope 放在高可见区域，影响普通用户确认效率。

## 成组修复顺位

### `E7-A Console 正式后台密度与移动任务流`

目标：先修 Console，不新增 API、不改权限，只改页面结构、文案层级和响应式承载。

建议分批：

- `E7-A1`：Dashboard 对齐 `P04`。迁入 `ConsolePageHeader / ConsoleMetricGrid / ConsoleToolbar`，把“快速操作”降级为命令区，主区优先展示治理 / 订单 / 用户 / 文档待处理队列和当前账号可行动项。
- `E7-A2`：移动 Console 壳层方案。基于已有 Pencil `P14-P18`，先确认底部 tab / 路由 Hub 与现有权限路由的关系，再改 `AdminLayout`，避免直接在移动端复制完整桌面表格。
- `E7-A3`：代表页任务流压缩。内容治理、经验治理、订单、商品、文档、权限矩阵在移动端减少四卡堆叠，把筛选、当前对象、证据和主动作前置。
- `E7-A4`：Console 可见文案清理。处理 `Case Desk`、全大写英文 eyebrow、`iframe`、`外部 Dashboard`、`API 尚未定义` 等实现说明。

### `E7-B Public 术语清理`

目标：先清用户可见内部词，不重做页面布局。

建议分批：

- `E7-B1`：集中处理 `i18n.ts` 中 `公开壳层 / 正式 Web 私域路由 / 私域路由 / 桌面工作台 / 公开 docs / reaction`。
- `E7-B2`：建立正式产品术语表，使用 `公开可见`、`登录后继续`、`仅本人可见`、`表情回应`、`订单与背包`、`返回来源` 等用户语言替代路由语言。

### `E7-C Public 信息密度与首屏任务`

目标：在术语清理后，按页面族处理低密度大卡片和移动首屏问题。

建议顺序：

1. Discover：先让社区内容流和可行动分类成为首屏主角。
2. Docs：压缩目录说明和 rail，突出搜索、目录和正文阅读。
3. Forum detail：让讨论脉络、评论、轻回应和登录后参与动作优先。
4. Shop：压缩迁移说明，突出商品、分类、价格、库存和购买入口。

### `E7-D Auth 授权页信息层级`

目标：保留安全透明度，但降低技术标识的首屏权重。

建议处理：

- `Client ID`、回调来源和原始 scope 改为“详情 / 技术信息”区域。
- 首屏优先展示应用名、授权身份、权限含义、返回目标和允许 / 拒绝动作。

## 验证口径

本轮为只读审计和文档更新，不执行前端构建、后端测试或真实 Gateway smoke。

后续批次验证建议：

- `E7-A` Console 代码：`npm run build --workspace=radish.console`，必要时补 Console PC / mobile Gateway smoke。
- `E7-B / E7-C` client 代码：`npm run build --workspace=radish.client`，Public 页面改动后补 PC / mobile 真实页面复核。
- `E7-D` Auth Razor：`dotnet build Radish.slnx -c Debug` 或更小范围 Auth 构建；真实授权页复核需用户确认宿主已启动。
- 文档 / 规划更新：`npm run check:repo-hygiene:changed` 与 `git diff --check`。
