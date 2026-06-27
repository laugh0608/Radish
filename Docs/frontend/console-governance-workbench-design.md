# Console 治理工作台设计端点

> 状态：`P3-12-D5` 已完成 Console 治理设计源 `P00-P18` 设计收口；当前作为后续 `radish.console` 视觉实现参考，代码实现前需先完成现有壳层 / token / 页面类型盘点
>
> 首次日期：2026-05-24（Asia/Shanghai）
>
> 最近更新：2026-06-27（Asia/Shanghai）
>
> 适用范围：`radish.console` 公共壳层、侧栏、顶栏、工具条、表格 CRUD、治理工作台、设置策略、文档治理、权限矩阵、运维任务和移动端 Console 任务流。后续按设计稿编号和页面类型逐步对齐，不直接重写 Console 全站。
>
> `2026-06-27` 更新：按 `web-ui-foundation.pen` 的共享基座口径，重构并扩展 `console-governance-workbench.pen` 至 `P00-P18`。当前已补公共 Console 壳层规范、真实路由密度侧栏、浅色图标导航、PC 页面类型矩阵和 7 张移动任务流；Console 仍保持后台工具密度，不套用公开 Web 的阅读型布局。

## 目标

把 Console 从零散后台页面收束为一套可复用的高密度管理产品结构：

- 公共壳层：浅色图标侧栏、84 高顶栏、状态 chip、主操作、页面工具条。
- 治理工作台：举报队列、目标快照、回看状态、审核动作、手动禁言 / 封禁、治理日志。
- 经验治理：用户经验概览、异常观察、经验流水、复核结论、冻结 / 解冻、管理员调整、等级配置。
- 后台功能页：总览调度、表格 CRUD、设置策略、商业订单、文档治理、权限矩阵和运维任务。
- 移动端 Console：不压缩 PC 三栏和表格，改为路由 Hub、任务卡、详情动作和底部 tab。

目标不是统一视觉表皮，而是降低管理员在“定位路由 → 筛选对象 → 查看证据 / 详情 → 执行动作 → 留痕复核”之间来回滚动和跳转的成本。

## 设计源文件

目标源文件：

```text
Docs/frontend/design-sources/console-governance-workbench.pen
```

规则：

- `.pen` 文件必须通过 Pencil 工具创建和修改，不能用普通文本工具读写。
- Markdown 只承载可检索的设计约束、页面结构和实现拆分，不替代 `.pen` 视觉源文件。

当前画板编号：

| 编号 | 画板 | 用途 |
| --- | --- | --- |
| `P00` | `Console Shell Common Components` | 公共 Console 壳层规范：`ConsoleShell`、`ConsoleSidebar`、`ConsoleTopbar`、`PageToolbar`、`MobileShell` |
| `P01` | `Console Shell Foundation - Layout System` | Console 专用纸感壳层、侧栏、84 高命令栏、指标、表格样板、动作层级和状态槽 |
| `P02` | `Console Content Moderation - Review Desk` | 内容审核队列、目标证据、治理动作和最近留痕三栏工作台 |
| `P03` | `Console Experience Governance - Ledger Desk` | 经验观察候选、用户摘要、趋势证据、流水定位和复核动作 |
| `P04` | `Console Governance Overview - Dispatch Center` | 文档、内容、经验等跨模块治理负载和今日分派中心 |
| `P05` | `Console Table CRUD - User Management` | 高频对象管理页，保留工具条、表格和选中对象摘要侧栏 |
| `P06` | `Console Settings - Governance Policy` | 设置 / 权限 / 配置型页面，采用分组导航、设置列和影响范围侧栏 |
| `P07` | `Mobile Content Moderation - Review Flow` | 移动端内容审核单列流程、纸感状态槽和底部 tab |
| `P08` | `Mobile Experience Governance - Ledger Flow` | 移动端经验复核单列流程、趋势 / 流水和底部 tab |
| `P09` | `Console Full Navigation & Permission IA` | 真实 Console 路由分组、功能覆盖矩阵和权限信息架构 |
| `P10` | `Console Commerce Operations - Products & Orders` | 商品、订单、胡萝卜等交易 / 资产运营代表页 |
| `P11` | `Console Document Governance - Publishing & Access` | 文档治理、发布、访问策略和版本回滚代表页 |
| `P12` | `Console RBAC Permission Matrix` | 角色列表、权限矩阵、高危授权和审计上下文 |
| `P13` | `Console Operations Tools - System Config & Jobs` | 系统配置、定时任务、失败重试和运行审计代表页 |
| `P14` | `Mobile Console Hub - Routes & Alerts` | 移动端 Console 路由 Hub、告警和待办队列 |
| `P15` | `Mobile Commerce Operations - Order Flow` | 移动端订单 / 商品任务卡和履约动作 |
| `P16` | `Mobile Document Governance - Publish Flow` | 移动端文档发布、访问策略和回滚动作 |
| `P17` | `Mobile RBAC Permission - Approval Flow` | 移动端角色权限审批，矩阵转为权限分组确认 |
| `P18` | `Mobile Operations Jobs - Retry Flow` | 移动端任务失败重试、配置覆盖和审计流 |

## P3-12-D5 重构口径

本轮重构不是继续沿用旧 `Case Desk` 模板换色，而是把 Console 业务源对齐 Web UI 共享基座后重新确定页面类型：

- `P00` 固定公共壳层组件边界：`ConsoleShell`、`ConsoleSidebar`、`ConsoleTopbar`、`PageToolbar` 和 `MobileShell` 必须作为后续实现输入。
- `P01` 先固定 Console 专用 shell：左侧治理导航、顶部命令栏、搜索、登录态、主动作、指标、表格、动作按钮和状态槽。
- `P04` 调度总览突出跨模块治理负载和今日行动，不再作为普通表格页。
- `P05` 表格 CRUD 保持对象管理密度，主区域是表格，侧栏是选中对象摘要和权限 / 审计线索。
- `P06` 设置页采用“分组导航 + 设置项 + 影响范围”，避免继续使用表格 CRUD 结构。
- `P02 / P03` 只在治理工作台场景使用队列、证据、动作和留痕结构。
- `P07 / P08 / P14-P18` 移动端保持单列任务流，图标和文字上下排列的底部 tab 与共享基座一致。
- `P09-P13` 覆盖真实 Console 的主要后台页面类型，不只停留在内容 / 经验治理。
- `P09-P13` 侧栏必须使用浅色图标导航，不能分叉为黑色运维侧栏。

Console 允许比公开 Web 和私域 Web 更高信息密度，但不得自行分叉以下共享规则：

- `rx-*` 语义 token、纸色底、弱边框和低饱和状态色。
- 图标化导航、主次按钮、危险动作、状态 pill 和恢复入口。
- 加载、空态、错误、权限限制等状态槽必须说明原因。
- PC 端横向利用宽度，移动端纵向组织流程，不把 PC 三栏直接压进手机。

## 公共壳层规范

`P00` 是后续 `radish.console` 视觉实现的公共输入，不是普通业务页：

- `ConsoleShell`：PC 固定为 `300px` 浅色侧栏 + `84px` 顶栏 + 主内容容器；页面 padding 和背景由壳层统一提供。
- `ConsoleSidebar`：承载真实路由分组、图标、badge、active、搜索入口；PC 页面必须保留图标。
- `ConsoleTopbar`：承载面包屑、页面标题、状态 chip、主操作和二级工具区；避免营销式大标题。
- `PageToolbar`：承载筛选、批处理、导出、刷新等业务动作；属于页面内容，但高度、按钮和状态样式沿用公共规则。
- `MobileShell`：不复刻 PC 侧栏；使用顶部状态区、单列内容、底部 tab、路由 sheet 和任务详情动作。

首批代码实现应先对齐公共壳层，再推进业务页面。否则表格页、治理页和设置页会继续各自复制侧栏、顶栏、按钮和状态样式。

## 页面类型覆盖

PC 页面按类型分组，不按路由逐页复制设计稿：

| 类型 | 对应画板 | 代码落地重点 |
| --- | --- | --- |
| 公共壳层 / 导航 | `P00 / P01 / P09` | `AdminLayout`、侧栏路由组、顶栏、状态 chip、页面容器 |
| 治理工作台 | `P02 / P03` | 内容治理、经验治理的队列 / 证据 / 动作 / 留痕结构 |
| 调度总览 | `P04` | Dashboard 治理负载、任务分派、跨模块状态 |
| 表格 CRUD | `P05 / P10 / P11` | 用户、商品、订单、文档等表格 + 右侧对象上下文 |
| 设置 / 权限 | `P06 / P12` | 设置分组、影响范围、权限矩阵、高危确认 |
| 运维工具 | `P13` | 系统配置、定时任务、失败重试、运行审计 |

移动端页面按任务流分组：

| 类型 | 对应画板 | 代码落地重点 |
| --- | --- | --- |
| 移动 Console Hub | `P14` | 路由分组、告警、待办队列 |
| 移动治理 | `P07 / P08` | 内容 / 经验复核任务流和无异常决策 |
| 移动商业 / 文档 / 权限 / 运维 | `P15-P18` | PC 表格和矩阵转为任务卡、分组详情和底部动作 |

## 当前页面盘点

| 页面 | 文件 | 当前规模 | 主要结构 | 问题类型 |
| --- | --- | ---: | --- | --- |
| 内容治理 | `Frontend/radish.console/src/pages/Moderation/ModerationPage.tsx` | 约 `1760` 行 | 举报审核队列、手动治理动作、治理动作日志、审核弹窗 | 单页承载过多状态、表格动作区过宽、筛选 / 详情 / 动作缺少稳定工作台分区 |
| 经验治理 | `Frontend/radish.console/src/pages/Experience/ExperienceAdminPage.tsx` | 约 `1650` 行 | 用户查询、经验概览、每日统计、异常摘要、流水、复核、调经验、冻结、等级配置 | 业务链路长、inline 样式多、治理观察和执行动作混排、配置类内容与人工复核混在同页 |

`P3-8-C1` 已完成首批结构治理：

- `ModerationPage.tsx` 已拆出 helper、列定义和手动治理动作区，主文件降至 `843` 行。
- `ExperienceAdminPage.tsx` 已拆出 helper、列定义、用户查询摘要、观察摘要、复核区、流水区、治理动作表单、页头和等级配置，主文件降至 `712` 行。
- 内容治理和经验治理已接入治理工作台布局承载，保持 API、权限、表单字段、数据契约和治理语义不变。

已可复用基础：

- `@radish/ui` 已提供 `Button`、`Table`、`Form`、`Tag`、`Modal`、`DatePicker` 等基础控件。
- `adminFeature.css` 已提供功能页容器、卡片、标题区、提示条、指标卡、表单栅格。
- `Docs/frontend/console-style-guide.md` 已定义 Console 局部 token 和样式分层。

`P3-8-C2` 已完成首批设计实现对齐：

- `P01` 壳层基座已落到 `AdminLayout`、`Breadcrumb`、`index.css` 与 `adminFeature.css` 的局部 token 和结构收敛。
- `P04` 调度总览已落到 `Dashboard`。
- `P05` 表格 CRUD 已落到 `UserList / TagList / CategoryList / SystemConfigList / RoleList / Applications / StickerGroupList / StickerList / ProductList / OrderList`。
- `P06` 设置 / 配置型页面已落到 `Settings / UserProfile`，并扩展到 `CoinAdminPage` 工具型、`RolePermissionPage` 权限配置型和 `UserDetail` 详情型页面试点。
- 上述迁移均保持 API、权限、表单字段、数据契约和业务语义不变，不把所有页面硬套为同一布局。

## 工作台信息架构

Console 治理工作台按四段组织：

1. **筛选与队列**
   - 固定在主内容左侧或顶部首屏。
   - 内容治理展示举报队列；经验治理展示用户搜索结果 / 异常候选。
   - 支持状态、类型、风险等级、回看状态、时间、关键词等筛选。
2. **目标详情与证据**
   - 展示被举报 / 被观察对象的快照、来源、回看状态、公开地址、相关用户。
   - 内容治理重点是目标快照和举报原因。
   - 经验治理重点是近 7 / 30 天趋势、规则命中、经验流水定位。
3. **动作区**
   - 将审核、禁言、封禁、解除、冻结、解冻、复核结论作为同一类“治理动作”承载。
   - 动作区需要明确权限禁用、必填项、持续时间、原因说明和提交反馈。
4. **留痕与回跳**
   - 动作执行后必须能回到队列 / 目标 / 日志。
   - 日志区按目标用户、来源举报单、动作类型、状态和关键词筛选。

## 桌面布局建议

优先设计 `1440px` 桌面宽度，Console 不做移动优先。

```text
┌─────────────────────────────────────────────────────────────┐
│ 标题 / 当前端点 / 刷新 / 记录动作                            │
├─────────────────────────────────────────────────────────────┤
│ 内容举报 | 经验异常 | 动作留痕                                │
├───────────────┬─────────────────────────┬───────────────────┤
│ 筛选 + 队列    │ 目标详情 + 证据           │ 动作 + 留痕摘要     │
│               │                         │                   │
│ 表格 / 候选    │ 快照 / 趋势 / 流水         │ 审核 / 冻结 / 备注  │
└───────────────┴─────────────────────────┴───────────────────┘
```

建议尺寸：

- 左侧队列区：`520-600px`
- 中间详情区：剩余宽度优先
- 右侧动作区：`300-360px`
- 小于 `1200px` 时退化为上下结构：队列、详情、动作依次堆叠

## 内容治理端点

首屏应保留：

- 顶部摘要：待审核、目标已降级、今日处理、生效中动作。
- 队列筛选：审核状态、目标类型、举报原因、回看状态、关键词。
- 队列表格：举报单、目标、举报人、原因、状态、创建时间、主操作。
- 详情区：目标快照、目标用户、举报详情、回看状态、打开目标。
- 动作区：审核结果、治理动作、持续时间、备注。
- 日志摘要：来源举报单、目标用户、生效状态、快速解除。

当前不做：

- 不新增后端字段。
- 不改变审核 / 禁言 / 封禁 API。
- 不把所有日志完整搬到首屏，只保留当前目标相关摘要和跳转入口。

## 经验治理端点

首屏应保留：

- 用户查询与当前用户摘要：等级、经验、冻结状态、更新时间。
- 观察摘要：近 7 / 30 天窗口、异常规则、治理建议。
- 证据区：每日统计、经验流水筛选、命中规则定位。
- 动作区：复核结论、冻结 / 解冻、管理员调整。
- 留痕区：最近治理动作、复核备注、冻结原因。

建议拆分：

- `等级配置` 从人工治理工作台中降级为独立配置段或二级页，不放在复核首屏。
- `管理员调经验` 作为高风险动作，放入动作区并要求原因，不与观察摘要混排。
- `经验流水` 保持表格，但由异常规则和每日统计一键定位筛选。

当前不做：

- 不改经验规则、冻结语义、流水类型或等级配置 API。
- 不新增自动冻结或自动扣经验。
- 不把经验发放主流程纳入 Console 设计批次。

## 实现拆分顺序

`P3-8-C1 Console 治理工作台结构基座` 已完成：

1. 在 Console 侧新增通用治理工作台布局 CSS 结构：
   - `governance-workbench`
   - `governance-workbench__queue`
   - `governance-workbench__detail`
   - `governance-workbench__actions`
2. 已在 `ModerationPage` 做结构替换：
   - 保留现有 API、权限、表单字段、表格列和动作逻辑。
   - 只调整队列、手动动作和日志的布局承载。
   - 移除当前页面可见的硬编码颜色和重复 inline 间距。
3. 已继续处理 `ExperienceAdminPage`：
   - 用户查询 / 观察摘要 / 复核动作 / 流水 / 调整动作 / 等级配置已拆为局部组件。
   - 等级配置仍保持原字段和 API，不纳入经验发放主流程治理。

`P3-8-C2 Console 设计稿到实现的对齐试点` 已完成首批高频页面覆盖：

1. 先复核 `P01 / P04 / P05 / P06` 与当前 Console 壳层、Dashboard、列表页和设置页的差距。
2. 优先沉淀 `AdminLayout`、`adminFeature.css` 和 `--console-*` token 的可复用视觉基座。
3. 选择一个低风险列表 / 设置 / 总览页面做试点，不一次性改完整个 Console，不把所有页面硬套成 `P02` 或 `P03` 的工作台结构。

当前下一批建议：

1. 先完成代码侧盘点：
   - `radish.console`：`AdminLayout`、路由元数据、`index.css`、`adminFeature.css`、页面工具条、表格页、设置页和治理页。
   - `@radish/ui`：按钮、表格、标签、状态、弹窗、表单控件是否足以承接 P00 公共壳层。
   - `radish.client`：只核对共享 token 与移动 tab 口径，不把 Console 设计反向套到公开 / 私域 Web。
2. 第一批代码优先落公共壳层：
   - `ConsoleShell / ConsoleSidebar / ConsoleTopbar / PageToolbar` 的结构和 token。
   - 真实 Console 路由侧栏统一为浅色图标导航。
   - 保持 API、权限、表单字段、路由权限和业务语义不变。
3. 第二批再按页面类型推进：
   - 表格 CRUD：用户 / 商品 / 订单 / 文档等对象管理页。
   - 设置 / 权限：系统设置、角色权限、权限矩阵。
   - 治理工作台：内容治理、经验治理。
   - 运维工具：定时任务、系统配置、运行审计。
4. 移动端实现不把 PC 表格硬缩，按 `P14-P18` 的任务卡、分组详情和底部动作重排。

验证入口：

```bash
npm run type-check --workspace=radish.console
npm run check:repo-hygiene:changed
git diff --check
```

## 验收标准

- 治理人员能在首屏判断：当前队列、当前目标、可执行动作、最近留痕。
- 内容治理和经验治理共享同一套页面结构语言，但不强行共享业务组件。
- Console PC 页面共享同一套浅色图标侧栏和顶栏，`P13` 运维页不得使用独立黑色侧栏。
- 移动端覆盖路由 Hub、治理、商业、文档、权限和运维代表流，不只保留两张治理页。
- 不改变 API、权限、治理动作语义、经验规则或冻结语义。
- 页面新增样式优先使用 `--console-*` token 和 `adminFeature.css` 既有结构。
- 按公共壳层、页面类型和移动任务流分批进入实现，避免把所有页面硬套为同一工作台结构。
