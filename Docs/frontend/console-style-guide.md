# Console 样式与 Token 使用说明

> 入口页：[前端设计文档](/frontend/design)  
> 最后更新：2026-05-24

本文说明 `radish.console` 后续新增或改动页面时的局部样式口径。Console 后续视觉方向以 `Docs/frontend/design-sources/console-governance-workbench.pen` 中的 `Case Desk` 系列画板为当前设计基准。

## 1. 当前边界

- Console 仍是独立后台入口，不嵌入 WebOS 窗口。
- `@radish/ui` 已提供按钮、表格、表单、弹窗、确认框、骨架屏、图标、Toast 等基础能力；新增页面不应再创建重复的本地基础控件。
- Console 局部样式先以 `index.css` 中的 `--console-*` CSS 变量承接主题 token，再由 `AdminLayout.css` 与 `adminFeature.css` 消费。
- 新增 / 明显改动页面优先按 `Case Desk` 风格收敛；历史页面不要求一次性改写，但进入重做或大幅调整时应对齐该方向。
- `radish.client` 后续重新设计时可以参考 Console 的低饱和配色、侧栏节奏、按钮层级和工作台信息组织，但不直接照搬 Console 的管理后台结构。

## 2. Case Desk 视觉方向

`Case Desk` 是 Console 当前推荐的工作台风格，目标是从旧式深色后台和普通表格页，收敛到更克制、更有层级的低饱和运营工具界面。

核心特征：

- **低饱和底色**：页面背景使用暖灰 / 纸色，侧栏和面板使用相近浅色分层，不使用大面积深蓝、亮橙或高饱和渐变。
- **轻侧栏**：侧栏负责稳定导航和少量上下文摘要，选中态使用浅底、细边框、图标强调，不用整块强色高亮。
- **主焦点清晰**：工作台页面应有一个明确主焦点，例如当前案件、当前对象、当前配置分组；指标、列表和辅助信息围绕主焦点服务。
- **按钮分级**：主动作使用深墨色或明确业务主色；危险动作使用低饱和红；成功动作使用低饱和绿；次动作使用描边或浅底。
- **标签克制**：状态标签使用浅底 + 深字，小面积表达风险、状态和分类，不抢占标题与操作按钮的视觉权重。
- **边框替代重阴影**：面板主要靠细边框、轻底色和少量阴影区分层级，避免卡片堆叠过厚。
- **高频信息可扫描**：列表、表格、设置项保持稳定行高和固定列宽，重点字段靠字重和位置突出，而不是靠大量颜色。

当前设计稿中的基准画板：

- `Console Shell Foundation - Layout System`：后台壳层、侧栏、顶栏和页面结构基座。
- `Console Content Moderation - Review Desk`：内容审核 / 证据复核工作台。
- `Console Experience Governance - Ledger Desk`：经验等级 / 台账治理工作台。
- `Console Governance Overview - Dispatch Center`：跨模块治理负载 / 调度总览页。
- `Console Table CRUD - User Management`：普通表格 CRUD 页面。
- `Console Settings - Governance Policy`：设置 / 权限 / 配置型页面。

已完成的首批实现落点：

- 壳层与通用基座：`AdminLayout`、`Breadcrumb`、`index.css`、`adminFeature.css`。
- 调度总览：`Dashboard`。
- 表格 CRUD：`UserList`、`TagList`、`CategoryList`、`SystemConfigList`、`RoleList`、`Applications`、`StickerGroupList`、`StickerList`、`ProductList`、`OrderList`。
- 设置 / 详情 / 配置扩展：`Settings`、`UserProfile`、`UserDetail`、`RolePermissionPage`、`CoinAdminPage`。

这些页面按页面类型复用视觉语言，但保留原 API、权限、表单字段、数据契约和业务语义。

## 2.1 页面类型实现口径

进入 Console 页面开发时，先按页面职责选择结构：

| 页面职责 | 首选结构 | 主要复用 |
|----------|----------|----------|
| 高频对象管理 | 指标条、工具条、表格、右侧摘要 | `admin-feature-metrics`、`admin-feature-toolbar`、`admin-feature-main` |
| 设置 / 个人资料 / 策略 | 左侧分组导航、中间设置列、右侧影响范围 | `admin-settings-layout`、`admin-setting-section`、`admin-settings-aside` |
| 调度总览 | 总览指标、快捷操作、最近事项、右侧入口 | `admin-overview-*`、`admin-dispatch-*` |
| 详情页 | 标题卡、指标、详情分区、右侧摘要 | `admin-detail-*` |
| 工具型页面 | 查询工具条、主操作区、说明摘要 | `admin-tool-*` 或页面局部类 |
| 治理工作台 | 队列、证据详情、动作留痕 | `governance-workbench-*` |

选择页面类型后，页面 CSS 只补不可复用的业务状态和局部排版。不要把表格 CRUD 页改成治理工作台，也不要把详情页拆成一组互相嵌套的卡片。

## 3. 页面类型

不同 Console 页面应复用同一视觉语言，但信息结构按页面类型区分：

| 页面类型 | 推荐结构 | 典型用途 |
|----------|----------|----------|
| 工作台 / 治理页 | 窄队列 + 主详情 + 决策面板 | 内容治理、经验治理、人工复核 |
| 表格 CRUD 页 | 指标条 + 筛选工具栏 + 表格 + 对象摘要侧栏 | 用户、订单、商品、角色 |
| 设置 / 权限页 | 分组导航 + 居中设置列 + 影响范围侧栏 | 系统配置、权限策略、告警策略 |
| 仪表盘页 | 关键指标 + 待处理事项 + 趋势或排行 | 总览、运营监控 |

不要把所有页面都做成卡片堆叠，也不要把某一个优秀页面当作全站模板逐页复刻；表格页仍应是表格，设置页仍应是行级设置，治理页才使用案件处理台结构。页面可以共享配色、侧栏节奏、按钮层级和 token，但布局应按具体功能自主组织。

## 4. Token 使用

新增 Console 样式优先使用以下局部变量，并逐步补齐 `Case Desk` 风格需要的语义：

| 变量类型 | 示例 | 用途 |
|----------|------|------|
| 背景 | `--console-bg-app`、`--console-bg-surface`、`--console-bg-muted` | 页面底色、卡片、弱背景 |
| 文本 | `--console-text-primary`、`--console-text-secondary`、`--console-text-on-dark` | 标题、正文、次要说明、深色区域文字 |
| 边框与阴影 | `--console-border-subtle`、`--console-shadow-soft`、`--console-shadow-card` | 面板边界、顶部栏、功能卡片 |
| 品牌与状态 | `--console-brand-primary`、`--console-info`、`--console-warning-*` | 主色、信息提示、警示说明 |
| 圆角 | `--console-radius-panel`、`--console-radius-control` | 页面面板、按钮 / 输入等控件容器 |

确需新增颜色时，先判断是否能映射到既有 `--theme-*` 语义 token；只有 Ant 状态色或 Console 独有治理提示确实无法复用时，才在 `index.css` 中集中新增 `--console-*` 变量。

`Case Desk` 推荐的语义颜色应覆盖：

- app background：暖灰 / 纸色页面底。
- navigation background：浅暖灰侧栏底。
- surface：主面板白纸色。
- surface muted：弱面板 / 输入区底色。
- text primary / secondary / muted：墨色、正文灰、弱说明。
- action primary：深墨色主按钮。
- action danger：低饱和红。
- action success：低饱和绿。
- action info：低饱和蓝灰。
- action warning：低饱和琥珀。

## 5. 页面样式分层

- `index.css`：只放 Console 根级 token、全局 box model、`body` 与 `#root` 基础样式。
- `AdminLayout.css`：只放后台壳层、侧边栏、顶部栏、内容区等布局样式。
- `adminFeature.css`：承接通用功能页结构，例如功能页容器、卡片、标题区、banner、指标网格、表单栅格。
- 具体页面 CSS：只放该页面不可复用的布局或业务状态样式，避免复制 `adminFeature.css` 已有结构。

## 6. 开发规则

- 新增页面优先从 `@radish/ui` 导入已有组件和 Ant alias。
- 新增硬编码颜色前，先查 `--console-*` 和 `--theme-*` 是否已有对应语义。
- 不在 JSX 里扩散 inline 色值、阴影和圆角；需要复用时放入 CSS 变量或 `adminFeature.css`。
- 不为了统一而改动无关历史页面；只有新增页面、可见缺陷修复、明确反馈触达或已进入重做范围的页面才顺带收敛。
- Console 样式治理不改变公开内容壳层、WebOS 桌面、Tauri 壳或 `radish.client` 主题切换规则。
- 从设计稿进入实现时，先沉淀 `--console-*` token 和通用壳层 / 按钮 / 标签 / 面板样式，再按页面类型拆局部 CSS，避免每页复制一套颜色和圆角。
