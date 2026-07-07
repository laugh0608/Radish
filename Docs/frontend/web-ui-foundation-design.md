# Web UI 共享基座设计说明

> 日期：2026-06-25（Asia/Shanghai）
>
> 更新：2026-06-28（Asia/Shanghai）：`F01 / F02` 的移动底栏已统一为 5 项以内浮动胶囊样式；Public 使用 `发现 / 论坛 / 文档 / 工作台 / 我的`，Private 使用 `工作台 / 资产 / 创作 / 消息 / 我的`，Console 业务源同步使用 `总览 / 治理 / 资产 / 权限 / 运维`。`/workbench` 固定作为正式 Web 功能地图，承接导航无法展示的功能入口。
>
> 更新：2026-06-28（Asia/Shanghai）：`P3-12-D8` 已完成 `radish.client` 首批代码对齐，新增共享 `WebShellHeader` / `WebStateSlot`，并把公开状态槽、公开内容宽度 token 和移动底部留白接入代码。
>
> 更新：2026-06-29（Asia/Shanghai）：`P3-12-D9-D13` 已完成 `radish.client` 私域 / 作者态第二批视觉实现与成组验收；`P3-12-D14-D19` 已开始将 Console 侧栏分组、页面语义组件和表格代表页迁入代码。
>
> 更新：2026-06-30（Asia/Shanghai）：`P3-12-D14-D35` 已完成 Console 首轮视觉迁移、静态收口、局部运行态复核和表格交互代码侧治理；D36+ 将复核 `F01-F02` 与 public / private / console 代码实现的共享结构漂移。
>
> 更新：2026-07-01（Asia/Shanghai）：D38 未新增共享组件变体；`/workbench` 继续承接 public / private 低频入口，移动 Console 仍按响应式 Console 验收，不拆独立移动应用壳层。
>
> 更新：2026-07-04（Asia/Shanghai）：D61 / D62 后 `WebShellHeader` 已同时承接 Public 当前发布前页面族和 Private `/workbench` 首批工作台；页面可通过 `mobileNavItems` 覆盖移动底栏，但只能用于明确页面族承接，不能让公开底栏长期覆盖私域工作台。
>
> 更新：2026-07-07（Asia/Shanghai）：`P3-12-E7-D` 已在 `web-ui-foundation.pen` 新增并四次收紧 `E7-D - Auth Consent Information Hierarchy` 画板，固定 Auth 授权确认页具体决策卡的请求应用、账号状态、返回位置、权限用途、敏感边界、技术信息下沉、动作区安全距离和移动端确认动作层级。
>
> 状态：共享基座设计源 `F01-F03` 与 `E7-D` Auth 授权确认参考画板已创建；`radish.client` Public `P01-P14`、Private `/workbench`、Console 首轮代表页和 Auth 授权页均已完成当前阶段代码对齐或参考画板收口，后续继续按 D62 / D63 / E7 页面族治理跨源一致性

## 设计源

```text
Docs/frontend/design-sources/web-ui-foundation.pen
```

画板：

| 画板 | 职责 |
| --- | --- |
| `F01 - Web UI Foundation` | 共享 token、public / private header 合法变体、按钮 / pill、卡片 / rail、状态槽、移动 shell / tab 和同步规则 |
| `F02 - Client Shell Common Components` | client 公共壳层组件契约，覆盖 PublicShell、PrivateShell、MobileShell、StateSlot、RouteSource 和 public / private 真实路由族 |
| `F03 - Radish WebOS Liquid Glass Navigation Concept` | WebOS 历史导航概念留档，不作为 P3-12 正式 Web 默认体验入口 |
| `E7-D - Auth Consent Information Hierarchy` | Auth 授权确认页具体决策卡参考，覆盖请求应用、当前账号、返回位置、权限用途、敏感边界、技术信息下沉、动作区安全距离和移动端确认动作 |

## 目标

- 解决多 `.pen` 业务设计源之间 header、按钮、卡片和状态槽样式分叉的问题。
- 保留 `public-web-unified-experience.pen`、`private-web-workflows.pen` 和 `console-governance-workbench.pen` 的端点边界。
- 让业务设计源复制共享样式样板，而不是各自重新发明基础组件。
- 在进入视觉代码实现前，先固定跨端点的视觉契约。

## 分层方案

### 共享基座

`web-ui-foundation.pen` 只承载共享样板和合法变体，不承载业务页面。

当前包含：

- Public header 合法变体。
- Private header 合法变体。
- 84 高 PC 纸感横匾 header、横排图标 nav rail、激活态 pill 和身份 action rail。
- PC 横排图标按钮、主按钮、次按钮、激活 pill、普通 pill 和状态 pill。
- 内容卡片、右侧 rail、加载 / 空态 / 错误 / 权限状态槽。
- 移动 Web shell 与图标上 / 文字下的底部 tab 样板；底栏为左右 inset、64px 高、真胶囊端点和柔和品牌色激活态。
- Client 公共壳层组件契约：PublicShell、PrivateShell、MobileShell、StateSlot、RouteSource、PC header 解剖、移动端底部 tab 和 public / private / commerce / author 路由族覆盖。
- 跨设计源同步规则。

### 业务设计源

业务 `.pen` 文件继续按端点拆分：

- `public-web-unified-experience.pen`：公开 Web 阅读 / 浏览端点。
- `private-web-workflows.pen`：登录态私域与作者态端点。
- `console-governance-workbench.pen`：Console 治理端点。

业务源可以有不同信息架构、导航项和页面密度，但不得自行分叉共享样式。

### 评审看板

后续如需阶段级横向审阅，可按需新增轻量 `web-ui-review-board.pen`，只放关键画板截图或代表性 frame，不承载完整编辑源。

当前暂不创建评审看板，避免过早增加同步成本。

## 必须一致

- `rx-*` 变量名称和取值。
- Header 高度、品牌区、Radish 标识、字体层级、nav 图标和 nav rail / pill 形态。
- PC nav 使用图标左、文字右的横排结构；移动端 tab 使用图标上、文字下的纵排结构。
- 主按钮 / 次按钮 / 激活态 / 普通筛选 pill 的图标、尺寸、主次层级和低饱和状态色。
- 卡片圆角、弱边框、纸色底、内容元信息、rail 信息密度和动作入口。
- 状态槽的加载、空态、错误和权限限制表达方式，包括原因说明、重试或登录恢复入口。
- 移动端底部 tab 的高度、胶囊形态、图标 / 文案层级、激活态和左右 inset。
- Web 功能入口规则：`/workbench` 是正式 Web 功能地图，PC header 和移动底栏只放高频入口；公开、私域或后台的低频功能不继续挤进一级导航。
- Client 公共壳层的职责边界：公开页负责阅读 / 浏览 / 登录参与，私域页负责身份 / 复访 / 作者任务，移动端负责单列任务流和底部 tab，不回退为 WebOS Dock 或窗口系统。

## 允许差异

- Public header 的导航项、登录动作和 `/workbench` 入口。
- Private header 的登录态身份、设置动作、消息 / 资产 / 创作入口。
- Console 的表格密度、治理工具条、权限 / 审计状态表达。
- Public / Private / Console 的移动底栏导航项和激活语义可以不同，但必须沿用同一浮动胶囊样式。
- 不同端点的 dominant region：公开阅读、私域复访、作者创作和 Console 治理可以有不同信息密度。

## 同步规则

1. 改共享样式时，先修改 `web-ui-foundation.pen`。
2. 再同步到受影响的业务设计源。
3. 再更新对应设计说明和记录。
4. 不允许只在某个业务 `.pen` 临时修改 header、按钮、卡片或状态槽样式。
5. 如果业务端点确实需要新变体，先把它加入共享基座并说明适用范围。

## 代码对齐状态

`P3-12-D8` 已完成首批 `radish.client` 对齐：

- 新增 `Frontend/radish.client/src/components/web-shell/WebShellHeader.tsx` 与 `WebStateSlot.tsx`。
- 私域复访页、作者态页和公开 forum 状态入口已开始复用共享壳层 / 状态槽节奏。
- `discover / docs / leaderboard / shop / profile` 公开状态卡已统一到 `WebStateSlot`。
- 公开页面宽度已抽象为 `--rx-content-max-width`、`--rx-content-reading-width` 和 `--rx-content-narrow-width`。
- 公开移动单列页面已补底部导航安全留白。

`P3-12-D9-D13` 已继续完成 `radish.client` 私域 / 作者态首轮落地：

- 资产、订单、背包、通知、消息、圈子、宠物、论坛作者态和 Docs 作者态已按本说明接入状态槽、摘要节奏和移动单列任务流。
- D13 已收口重复卡片和摘要卡圆角分叉，并完成 Gateway PC / mobile 成组验收。

`P3-12-D14-D35` 已完成 Console 侧首轮代码落地与阶段治理：

- `radish.console` 侧栏已按总览 / 商业与资产 / 内容与文档 / 治理与权限 / 系统工具分组。
- 新增 `ConsolePageHeader`、`ConsoleStatusChip`、`ConsoleMetricGrid`、`ConsoleMetricCard`、`ConsoleToolbar`，用于承接 Console 页头、指标和筛选工具条。
- 系统设置、订单、用户、商品、文档治理首屏、标签 / 分类、贴纸类、角色权限、内容 / 经验治理、系统工具、深层表单、详情 / 抽屉和表格交互已完成首轮迁移或代码侧治理；迁移不改变 API、权限、表单字段或业务动作。

后续代码实现继续优先复用这些结构；如发现新共享变体，先回到本说明和设计源确认边界。D36+ 需要补齐设计源与代码实现的差距矩阵后，再判断是否进入成组实现或运行态验收。

`P3-12-D61-D62` 已继续收紧 client 共享壳层使用边界：

- Public Web `P01-P14` 当前发布前页面族已复用共享 public header 与移动底栏；`/discover`、forum、docs、shop、leaderboard、profile 和 public workbench 承接均保留真实 `href`。
- `PublicShellHeader` 可以透传 `mobileNavItems`，用于公开移动任务流覆盖底栏；覆盖必须和当前页面族一致，不能作为长期绕过 private 默认底栏的手段。
- `/workbench` 在 D62 回到 `variant="private"` 的默认移动底栏：`工作台 / 资产 / 创作 / 消息 / 我的`。公开商城、榜单和其他低频公开入口由工作台内容区承接。
- 新增工作台继续处理队列和状态 rail 属于页面内容层，不新增共享 header 或状态槽变体。

## radish.client 组件使用口径

### `WebShellHeader`

位置：

```text
Frontend/radish.client/src/components/web-shell/WebShellHeader.tsx
```

职责：

- 承接 public / private PC header。
- 承接 public / private 移动底栏。
- 保留真实 `href`，普通左键点击可由页面注入的 `onClick` 拦截为应用内导航；辅助点击、新开标签和复制链接仍依赖真实 URL。
- 自动为页面添加 `radishWebShellWithMobileNav` body class，用于移动底栏全局留白。

默认导航：

| variant | PC 导航 | 移动底栏 |
| --- | --- | --- |
| `public` | `发现 / 论坛 / 文档 / 榜单 / 商城` | `发现 / 论坛 / 文档 / 工作台 / 我的` |
| `private` | `工作台 / 我的状态 / 资产 / 创作 / 消息` | `工作台 / 资产 / 创作 / 消息 / 我的` |

使用规则：

- 页面可传 `navItems`、`actionItems`、`mobileNavItems` 调整当前业务动作，但不能另写平行 header。
- `activeKey` 可显式传入；不传时由当前 pathname 推导。
- Public / private 的低频入口统一由 `/workbench` 承接，不继续把移动底栏挤到 5 项以上。

### `WebStateSlot`

位置：

```text
Frontend/radish.client/src/components/web-shell/WebStateSlot.tsx
```

职责：

- 承接加载、空态、错误、未找到、权限限制、登录恢复和普通信息状态。
- 状态动作支持 `href` 和 `onClick`；会导航的动作优先提供真实 `href`。
- `compact` 用于列表内或 rail 内的轻量状态，不替代完整页面状态。
- `meta` 用于补来源、目标、边界说明等少量结构化信息，不承载长篇实现说明。

允许 tone：

```text
loading / empty / error / notFound / permission / auth / info
```

### 相关 token

当前公开内容宽度与移动底栏留白使用：

```css
--rx-content-max-width
--rx-content-reading-width
--rx-content-narrow-width
--rx-mobile-shell-offset
```

页面级 CSS 应优先使用这些语义 token。确实需要新增宽度或壳层 token 时，先补本说明，再进入代码。

## Pencil 工作流限制

- Pencil 当前活动窗口一次只打开一个 `.pen`；写入操作必须以当前活动窗口中已打开的目标文件为准。
- MCP 工具可以通过 `filePath` 指定读取、检查和截图不同文件，但不要假设写入会可靠落到非活动窗口文件。
- 切换 `.pen` 前必须在 Pencil 内手动保存当前文件；未保存时切换文件可能丢失更改，或让后续写入误落到上一活动文件。
- 由于 Pencil 组件不能跨文件实时引用，当前方案不是实时组件库，而是“共享样板 + 文档约束 + 按需同步”。
- `.pen` 文件只通过 Pencil 创建、读取和修改；新设计源可从 `empty-design-source-template.pen` 复制后再用 Pencil 写入内容。

## 当前不做

- 不把所有页面合并进一个巨型 `.pen`。
- 不创建跨文件实时组件库。
- 不把 D8 首批实现扩大为 Console 或全量页面重写。
- 不把 Console 语义组件反向套到公开 / 私域 Web；Console 仍按治理和表格密度独立承接。
- 不借共享基座重做 public / private / console 全量画板。
- 不把 `F02` 当作业务页面；public / private 仍需在各自业务设计源补齐具体页面族和移动任务流。
