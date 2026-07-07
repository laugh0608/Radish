# P3-12-E7-A Console 正式后台密度与移动任务流方案

> 日期：2026-07-07（Asia/Shanghai）
>
> 状态：代码实现与真实页面复核已完成
>
> 上游审计：[P3-12-E7 Console / Public UI 与文案成熟度首批差距审计](/records/p3-12-e7-console-public-ui-copy-gap-audit-2026-07-07)
>
> 设计源：`Docs/frontend/design-sources/console-governance-workbench.pen` 与 [Console 治理工作台设计说明](/frontend/console-governance-workbench-design)

## 目标

`E7-A` 只处理 Console 正式后台的密度、移动任务流和可见文案成熟度，目标是让 `/console/` 从“管理入口集合”收敛为面向社区治理和运营的正式后台。

本批必须解决四类阻断：

- Dashboard 未对齐 Pencil `P04` 调度台，仍以旧指标卡和快速操作为主。
- 移动 Console 壳层仍是窄侧栏，未形成 `P14-P18` 所要求的移动治理任务入口。
- 代表页在移动端仍偏桌面表格压缩，任务对象、证据和动作优先级不够清楚。
- Console 可见文案仍混有英文内部标签、实现说明或调试口径。

## 实施范围

### E7-A1 Dashboard 调度台

将 `/console/` 首页改为正式治理调度台，而不是通用导航首页。

- 继续使用现有 Dashboard 数据、订单数据和权限信息，不新增 API。
- 首屏优先展示治理队列、订单 / 权益异常、文档治理、用户风险、系统运行状态和权限边界。
- 用紧凑指标带、队列分组、任务入口和二级说明替代大面积旧 `admin-feature-card`。
- “快速操作”改为小型命令组，避免成为首屏主体。
- 桌面端保持后台工作台密度，避免营销式大卡片和重复说明。

### E7-A2 移动 Console 壳层

移动端不做独立 Console App，也不复制完整桌面后台。

已确认执行的默认方案：

- 移动端采用“高频底栏 + 总览功能面板 + 更多全量入口”的混合结构。
- 底部任务导航只承接高频路径：`总览 / 治理 / 交易 / 权限 / 更多`。
- `总览` 页面提供类似 client 的“全部功能 / 功能面板”，但视觉和信息组织保持后台工作台口径。
- `更多` 打开完整 Console 功能面板，按现有路由分组展示全部可访问页面。
- 桌面端继续保留现有左侧导航和顶部栏，不改变后台主导航模型。
- 移动端底部栏每类入口只暴露当前阶段高频治理页面，低频深层管理继续通过“全部功能”、页面内入口或搜索进入。
- 当前 64px 窄侧栏不再作为移动端主要导航，因为它无法表达任务分类，也不适合触控连续操作。
- 不新增路由语义、权限 key 或后端接口；移动导航只复用现有路由和权限过滤。

本口径的关键判断：

- 底部栏不是全量分类，只是移动端的高频任务导航。
- “全部功能”才是完整 Console 入口，避免因底部栏宽度限制隐藏低频后台能力。
- `交易` 比 `资产` 更准确，覆盖商品、订单、胡萝卜和权益运营。
- `更多` 比 `运维` 更适合作为移动端全量入口；运维类页面保留在更多面板中的系统工具分组。

### E7-A3 代表页移动任务流压缩

本批选择代表页做成组压缩，不追求一次性重写全部 Console 页面。

- 内容治理 / 经验治理：移动端优先排列当前对象、证据摘要、处理动作和处理结果，再展示长列表。
- 订单 / 商品：移动端优先展示待处理状态、履约 / 权益异常和筛选摘要，表格只作为可横向滚动的数据面。
- 文档治理：移动端保留证据链、状态、处理说明和主要动作，弱化桌面专用的并排区域。
- 角色权限：移动端优先展示角色、风险说明、权限组摘要和保存状态，矩阵表只作为细节层。
- 已实现的 `ConsolePage`、任务 rail、表格和抽屉能力优先复用，不引入新的后台 UI 框架。

### E7-A4 Console 可见文案清理

清理 Console 页面中用户可见的内部术语和实现描述。

- 替换 `Case Desk`、全大写英文 eyebrow、`iframe`、`外部 Dashboard`、`API 尚未定义` 等不适合正式后台的表达。
- 技术细节只保留在二级提示、诊断复制或开发记录中，不作为按钮、标题或首屏说明。
- 保留必要的权限、审计、诊断和错误编号，但文案要从用户任务角度组织。
- 本批只处理 Console；Public、Auth 的术语和授权页信息层级进入 `E7-B / E7-D`。

## 停止线

本批不做以下事情：

- 不新增或修改后端 API、数据库、权限 key、审计模型、错误模型和运行时契约。
- 不启动独立移动 Console App。
- 不把低频后台页面强行塞进移动底部导航。
- 不处理 Public Web、Docs、Forum、Shop 或 Auth 授权页；这些归入后续 `E7-B / E7-C / E7-D`。
- 不执行 Gateway 真实页面 smoke，除非用户当轮明确说明前后端已经启动。

如果实现中发现必须改变接口、权限、数据模型或运行时行为，应停止代码推进，补充专题方案并重新确认。

## 建议实现顺序

1. 调整 Dashboard 为 Console 治理调度台，先解决 PC 和移动首屏密度。
2. 确认移动壳层方案后，改造移动端主导航入口。
3. 压缩代表页移动任务流，优先覆盖内容治理、订单 / 商品、文档治理和角色权限。
4. 集中清理 Console 可见文案和内部术语。

## 验证口径

代码批次完成后按风险分层验证：

- 必跑：`npm run build --workspace=radish.console`
- 必跑：`npm run check:repo-hygiene:changed`
- 必跑：`git diff --check`
- 若只改 CSS / TSX 且未触及后端，不默认跑 `dotnet test` 或完整 baseline。
- Gateway PC / mobile 真实页面复核只在用户当轮明确说明前后端已启动后执行。

## 已确认项

已确认：

- 移动 Console 采用 `总览 / 治理 / 交易 / 权限 / 更多` 底部高频导航。
- `总览` 和 `更多` 提供完整功能面板，不把全部后台分类塞进底部栏。
- 本批接受“代表页优先”的策略：先覆盖 Dashboard、移动壳层和四类代表页，不一次性重写全部 Console 页面。
- 已同步更新 `console-governance-workbench.pen` 中 `P14` 的底部 tab 和“全部功能”口径。

## 实现结果

本批已完成：

- `AdminLayout` 移动端隐藏旧 64px 侧栏，改为 `总览 / 治理 / 交易 / 权限 / 更多` 高频底栏。
- `更多` 打开完整 Console 功能面板，按现有 `routeMeta` 分组和当前账号权限过滤入口。
- `/console/` Dashboard 改为 Console 调度台：包含紧凑指标、优先处理队列、命令组、全部功能和最近订单。
- `总览` 页面承接完整功能面板，移动端可作为类似 client 的总入口，但保持后台工作台密度。
- 路由分组 `商业与资产` 改为 `交易与资产`，与移动底栏语义对齐。
- 代表页通用 `governance-task-flow` 在移动端改为横向任务摘要条，降低桌面卡片纵向堆叠感。
- Console 可见英文内部标识已成组清理：`Case Desk`、全大写 eyebrow、`外部 Dashboard`、`iframe`、`API 尚未定义` 等目标文案已替换为正式后台表达。

本批未做：

- 未新增 API、权限、数据库、审计、错误模型或运行时契约。
- 未启动独立移动 Console App。
- 未改动后端代码；不需要后端重启。

## 本地验证

- `npm run build --workspace=radish.console`：通过。
- `git diff --check`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `node Scripts/check-repo-hygiene.mjs Docs/records/p3-12-e7-console-public-ui-copy-gap-audit-2026-07-07.md Docs/records/p3-12-e7-a-console-density-mobile-task-flow-plan-2026-07-07.md`：通过。
- Pencil `snapshot_layout` 覆盖 `P14 - Mobile Console Hub - Routes & Alerts`：未发现布局问题。
- Gateway PC `1440x900` 真实页面复核：已通过种子管理员账号完成 Console 登录 / 授权回流，`/console/` 标题为 `Console 调度台 - Radish Console`，桌面左侧全量导航、调度台、全部功能面板和最近订单均正常显示。
- Gateway mobile `390x844` 真实页面复核：底部高频导航 `总览 / 治理 / 交易 / 权限 / 更多` 正常显示；关闭态不渲染更多面板；点击 `更多` 后完整功能面板按 `总览 / 交易与资产 / 内容与文档 / 治理与权限 / 系统工具` 展示。
- 浏览器控制台错误检查：Playwright CLI `console error` 返回 `Errors: 0`。
- 已优先尝试 in-app Browser 插件并完成登录进入 Console；后续简单 evaluate 调用连续超时，故使用 Playwright CLI 完成 PC / mobile 复核闭环。
