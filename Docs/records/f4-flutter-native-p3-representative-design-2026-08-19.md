# Flutter Native P3 代表设计记录

> 状态：三轮审阅修订已获项目所有者确认，`P3` 关闭
>
> 日期：2026-08-19；三轮审阅修订：2026-08-23（Asia/Shanghai）
>
> 活动设计源：`Docs/frontend/design-sources/radish-flutter-native-ui-v1.pen`

## 1. 本批结论

P3 已形成独立 Flutter Native 代表设计源，没有把 Flutter 页面追加到 Web / Console 的 `radish-web-family-ui-v1.pen`。独立设计源只表示实现与交互 owner 独立，不表示视觉体系独立：Flutter 以正式 Web 的品牌栏、导航语法、内容层级和主题身份为视觉基准，再按安全区、系统返回、触控、键鼠、焦点和窗口等级做原生适配。

本批冻结了 Flutter 的视觉基座、Discover 与 Forum Detail 的 compact / expanded 正式代表稿，以及主题选择、medium 导航差分和权益关键状态。项目所有者已于 `2026-08-23` 确认第三轮横向审阅稿，`P3` 退出门禁关闭；下一步先进入 `P4-A` readiness，不直接安装字体或修改依赖。

## 2. 设计源边界

| 设计源 | 正式范围 | 不承载 |
| --- | --- | --- |
| `radish-web-family-ui-v1.pen` | Web Client、Console、WebOS 历史兼容入口的 Web 代表设计 | Flutter 原生页面与组件 |
| `radish-flutter-native-ui-v1.pen` | Flutter Native 的视觉基座、原生 Shell、代表页面、关键状态与自适应差分 | 未经 Flutter token、壳层和交互边界适配的 Web / Console 页面镜像 |

分离的原因不是视觉脱离，而是避免 Web DOM / WebOS 工作台结构与 Flutter 触控密度、桌面输入和窗口行为在同一活动源中形成错误复用。跨端对齐固定产品身份、语义角色、主题命名、业务状态以及正式 Web 壳层的视觉语法；Flutter 不复制 DOM，但也不另建一套全局导航外观。

## 3. 代表画板

| 分级 | 画板 | 尺寸 | 冻结内容 |
| --- | --- | --- | --- |
| Foundation | `FN-R1-F00 / Flutter 视觉基座与四主题` | `1440 × 1000` | 四主题、字体层级、密度、圆角、共享组件、三档断点、motion 与无障碍 |
| R1 | `FN-R1-P01 / Discover / Compact 390` | `390 × 1571` 长页 | Web 完整发现流、焦点讨论、社区脉搏、活动时间线、知识主题、安全区与胶囊底栏 |
| R1 | `FN-R1-P01 / Discover / Expanded 1440` | `1440 × 900` | Web 家族顶部全局栏、`904px` 主讨论区与右侧社区洞察区 |
| R1 | `FN-R1-P02 / Forum Detail / Compact 390` | `390 × 2622` 长页 | 连续正文、来源与阅读元数据、轻回应、快速回复、完整回帖、安全区与胶囊底栏 |
| R1 | `FN-R1-P02 / Forum Detail / Expanded 1440` | `1440 × 2244` 长页 | Web 家族顶部全局栏、`220 / 820 / 250` 社区导航—连续正文—线程索引三栏及完整回帖 |
| R2 | `FN-R2-F01 / Theme Selector + Medium Delta` | `1440 × 900` | Compact Bottom Sheet、Expanded Dialog、Medium `72px` 页内栏、权益 loading / stale / error 与未拥有停止线 |

活动源共有七个 Flutter 专用组件母版：Button、State Chip、State Slot、Mobile Tab Item，以及按正式 Web 几何建立的 Expanded Header、Mobile Header、Mobile Tab Bar。它们不跨文件引用 Web 组件，也不构成另一套业务状态机。

## 4. 视觉与交互裁决

### 4.1 壳层与响应式布局

- Compact 保留系统安全区与原生返回语义；顶部品牌栏采用 Web 的 `64px` 内容高度，五项底栏复用 `358 × 64`、`52px` tab、`18px` Lucide 图标和 `11px` 标签的几何，不再使用铺满屏宽的默认 Material `NavigationBar` 外观。
- Compact 代表稿以完整长页展开所有正文和列表，避免用固定视口隐藏后续信息；Flutter 运行时仍由中间正文滚动区承载长内容，Header 与底部导航按系统安全区固定。
- Expanded 使用 Web 的 `68px` 顶部全局栏：`210px` 品牌区、`36px` 独立导航项、弹性留白、`278 × 38` 搜索、主操作、通知与账户动作保持同序；不再设置 Flutter 独有的永久全局 `NavigationRail`。
- 页面侧栏只表达当前任务上下文。Discover 使用 `904px` 主讨论区和右侧社区洞察区；Forum Detail 使用 `220 / 820 / 250` 社区导航—连续正文—线程索引三栏，不形成第二套应用导航。
- Medium 可使用折叠页内栏承载列表—详情关系，但全局产品导航仍继承 Web 家族语法。
- 常驻 Gateway / 登录状态条从代表页移除；连接、stale 与错误只在相关任务上下文中按需出现。

### 4.2 Typography

- UI、正文与数据文字：`Noto Sans SC`。
- 页面主标题和少量品牌强调：`Noto Serif SC`。
- 正文基准为 `15–16px`，常规标题为 `18–24px`，展示标题约 `32px`。
- Serif 只用于有限层级，不进入按钮、表单、导航和高密度数据面。

P3 只冻结字族方向。P4 才决定实际本地子集、Flutter asset 声明和回退链；生产包不得依赖运行时网络取字体。两套 Noto CJK 字体均采用 SIL Open Font License 1.1，正式接入时必须随资产保留对应许可证：[Noto Sans CJK license](https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE)、[Noto CJK repository](https://github.com/notofonts/noto-cjk)。

### 4.3 密度、表面与动效

- Compact 触控目标最低 `48px`；桌面高密度操作最低 `40px`。
- 圆角分为 `8 / 12 / 18` 三档；阅读面与连续列表不层层套卡片。
- 层级优先使用表面差、细边框和留白，阴影只用于 Dialog 等真实浮层。
- motion 固定 `120 / 200 / 280ms` 三档；`MediaQuery.disableAnimations` 下自定义位移与过渡归零，状态仍需完整可读。
- expanded 内容宽度受控，增加导航和上下文效率，不把 mobile 页面机械拉宽，也不复制 WebOS 多窗口桌面。

### 4.4 四主题与权益状态

Pencil 主题轴使用 `default / guofeng / dark-night / sakura`，分别映射产品主题 `default / guofeng / theme-dark-night / theme-sakura`。页面只消费语义 token，不按主题 ID 分叉布局。

- 内建主题可即时预览，确认后再写入本地偏好。
- 付费主题的拥有、激活与失效继续由 Shop entitlement 判定。
- loading 保持当前可信主题，避免闪回默认主题。
- stale 允许最近一次已拥有主题继续显示，并显式标记待同步。
- error 保留可读错误与重试入口，不创建本地解锁。
- 未拥有主题只解释来源并引导商店，不在设置弹层内实现购买或伪激活。

## 5. P2 当前代码与 P3 代表稿差分

P3 是下一实现阶段的视觉契约，不代表以下行为已经在 P2 代码中落地。日终代码反查固定这些差分，P4 必须按确认结果显式关闭：

| 领域 | P2 当前代码事实 | P3 目标 |
| --- | --- | --- |
| 字体 | `ThemeData` 仍使用系统字体，没有字体 asset 或 `google_fonts` | `Noto Sans SC` 承载 UI / 正文，`Noto Serif SC` 只做有限展示强调；本地资产和回退链经授权接入 |
| 主题确认 | `radish_theme_selector.dart` 点击主题项会直接调用 `selectTheme`，内建主题立即写偏好，权益主题立即尝试激活 | 选择先形成可撤销预览，用户显式确认后再持久化或调用权益动作 |
| motion | `MaterialApp.themeAnimationDuration` 当前为 `180ms`，`RadishMotion` 只负责在 `disableAnimations` 下归零调用方时长 | 统一收口 `120 / 200 / 280ms` token，同时保持 reduced-motion 归零 |
| Discover | P2 已有 compact 单列、medium / expanded 双列，但仍以通用 context / section card 组合为主 | 按代表稿收紧焦点内容、连续列表、Docs / Shop 次区和表面密度 |
| Shell | P2 compact 使用默认全宽 `NavigationBar`，medium / expanded 使用全局 `NavigationRail` | Compact 改为安全区内悬浮胶囊底栏；expanded 改为 Web 家族顶部全局栏；medium 只保留必要页内栏 |
| Forum Expanded | P2 为受控 reading pane 加右侧 `304px` context rail | 顶部全局栏下采用 `220 / 820 / 250` 社区导航—连续正文—线程索引三栏，左右栏均为页面级上下文 |

视觉审核若调整上述目标，以审核后的代表稿和本记录更新为准；不得反向修改 P2 历史实现记录来伪装差分已经落地。

## 6. R3 继承路径

| 页面族 | 继承来源 | 实现时必须补的真实复核 |
| --- | --- | --- |
| Profile / 我的、公开主页、最近访问 | Shell + Discover 摘要层级 + State Slot | 长文本、局部来源失败、登录 / 退出与来源返回 |
| Forum 列表 / 发帖 | Shell + Discover 列表密度 + Forum 互动区 | 分页、草稿、提交中、失败重试与键盘避让 |
| Docs 目录 / 正文 | Shell + Forum 连续阅读面 | 目录切换、链接 handoff、长 Markdown 与桌面滚动 |
| Shop 浏览 / 交易回流 | Discover 业务入口 + Theme entitlement 状态 | 购买确认、订单回流、失效权益与结构化错误 |
| 订单 / 背包、Wallet、Experience、Leaderboard | Shell + Discover 摘要 + State Slot | 真实数据密度、空态、stale / unavailable 与窗口缩放 |

这些页面不再新增完整 Pencil 镜像；P4 / P5 按继承来源实现，并通过真实 compact / expanded 截图与对应自动化复核差异。

## 7. 静态复核

- 2026-08-23 将七个组件母版保留在顶部组件带，六张正式板统一放到 `y = 220` 的横向审阅带，顺序为 Foundation → Discover compact / expanded → Forum Detail compact / expanded → Theme / Medium；移动端与 PC 端按页面族紧邻成对，画板之间无重叠。
- 六个顶层画板和七个组件母版均已清除 `placeholder`，六板全部显式使用 `guofeng` 画板主题；修复了 Forum Expanded 漏配主题而显示默认配色的问题。
- 第三轮导入产生的 Web 变量已全部映射回 Flutter 的 `theme` 轴与语义 token，额外 `productTheme` 轴、重复变量和硬编码 Geist 字族均已清理；页面文字只引用 `$font-body / $font-display`。
- Pencil 原生 visitor 逐根检查未报告 clipping、collapse 或 overflow；六个顶层画板均重新完成截图复核，四张代表页无未知 token、无 placeholder、无画布重叠。
- 设计源已通过 Pen 桌面应用原生保存。
- 本批没有修改 Flutter 运行时代码、API、数据库、依赖或平台工程，没有启动服务或执行真实 smoke。

## 8. 三轮审阅修订与下一步（2026-08-23）

首轮审阅确认了三个问题：画板排布散乱、Forum Expanded 漏用国风主题，以及 Flutter compact 底栏和 expanded 顶栏 / 侧栏与正式 Web 差异过大。本轮据此完成以下修订：

1. 画布按“组件—Foundation—Discover 成对画板—Forum Detail 成对画板—主题 / Medium 状态”重排；
2. 六个顶层画板显式统一为 `guofeng`，不再依赖继承默认值；
3. Compact 改为 Web 家族品牌栏与安全区悬浮胶囊底栏；
4. Expanded 删除永久全局侧栏，改为 Web 家族顶部全局栏，侧栏仅保留页面上下文；
5. 通过三种低保真壳层研究选择“顶部全局栏 + 页面级双栏 / 三栏”，研究稿在裁决后已从活动源清除。

第二轮审阅指出首轮仍只对齐了概念，没有继承正式 Web 的真实组件结构。本轮直接读取 `radish-web-family-ui-v1.pen` 的 Web Header、Mobile Tab Bar、Discover 与 Forum Detail 代表页，并完成以下纠正：

1. 新增可复用 Web-Family Expanded Header、Mobile Header 与 Mobile Tab Bar，页面不再各自拼装近似壳层；
2. PC Header 按 Web 的 `68px` 高度、`28px` 水平内距、独立导航项、搜索、主操作、通知和账户顺序重建，删除错误的居中导航大胶囊；
3. Mobile Tab Bar 按 `358 × 64`、无阴影、细描边、`52px` tab 和“发现 / 论坛 / 聊天 / 更多 / 我的”重建，并改用与 Web 一致的 Lucide 线性图标；
4. Discover Compact 补回搜索—筛选—介绍—焦点内容顺序，Expanded 改为 `904px` 主讨论区加社区洞察区；
5. Forum Compact 补回社区 / 本帖紧凑入口，Expanded 按 Web 的 `220 / 820 / 250` 社区导航—连续正文—线程索引职责重建。

第三轮审阅指出页面壳层虽已接近 Web，但正文信息仍被重新概括得过少，固定高度内出现明显空白。本轮因此不再手工补几张卡片，而是把 Web Discover 与 Forum Detail 的 PC / mobile 四张正式代表页作为可编辑母版复制到 Flutter 活动源，再完成以下原生化收口：

1. 保留 Web 的完整信息架构和内容数量，Discover mobile 展开到 `1571px`，Forum mobile 展开到 `2622px`，Forum PC 展开到 `2244px`，不再用空白或裁切代替真实滚动内容；
2. PC 继续复用 Flutter 文件内的 Web-Family Expanded Header，mobile 继续复用 Mobile Header 与 Mobile Tab Bar，并补 `24px` 原生安全区；
3. Web 导入的 `productTheme`、重复颜色变量、`radius-pill` 和 Geist / Inter 字族全部映射回 Flutter `theme` 轴、既有语义 token 与 `Noto Sans SC / Noto Serif SC`；
4. 四张旧的稀疏代表页被母版适配稿直接替换，临时导入节点和重复变量已经清除，没有同时保留两套页面；
5. 六张正式板按 Foundation → Discover Mobile / PC → Forum Mobile / PC → Theme / Medium 横向排布，visitor、截图、token、字体、placeholder、画板重叠与边界检查全部通过。

项目所有者已确认本轮视觉方向，`P3` 正式关闭。下一步进入 `P4-A` readiness，形成字体资产接入、许可证、包体积、回退链、依赖与 lockfile 影响说明并另行获得授权，再收口 Theme Foundation、共享组件、Shell、Discover 和 Forum Detail；本次确认不自动授权包安装、依赖更新或批量页面改造。
