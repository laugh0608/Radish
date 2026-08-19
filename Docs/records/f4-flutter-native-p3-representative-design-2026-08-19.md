# Flutter Native P3 代表设计记录

> 状态：代表稿已完成，等待项目所有者视觉确认
>
> 日期：2026-08-19（Asia/Shanghai）
>
> 活动设计源：`Docs/frontend/design-sources/radish-flutter-native-ui-v1.pen`

## 1. 本批结论

P3 已形成独立 Flutter Native 代表设计源，没有把 Flutter 页面追加到 Web / Console 的 `radish-web-family-ui-v1.pen`。两条产品线只通过 `Docs/` 中的产品边界、家族语义 token 与状态契约保持一致，各自维护页面结构、组件密度和平台交互。

本批冻结了 Flutter 的视觉基座、Discover 与 Forum Detail 的 compact / expanded 正式代表稿，以及主题选择、medium 导航差分和权益关键状态。当前尚未进入 P4：代表稿需要项目所有者确认后，才安装或接入本地字体资产、收口 Dart 共享组件并成组改造其他页面族。

## 2. 设计源边界

| 设计源 | 正式范围 | 不承载 |
| --- | --- | --- |
| `radish-web-family-ui-v1.pen` | Web Client、Console、WebOS 历史兼容入口的 Web 代表设计 | Flutter 原生页面与组件 |
| `radish-flutter-native-ui-v1.pen` | Flutter Native 的视觉基座、原生 Shell、代表页面、关键状态与自适应差分 | Web / Console 页面镜像 |

分离的原因不是视觉脱离，而是避免 Web DOM / WebOS 工作台结构与 Flutter NavigationBar / NavigationRail、触控密度、桌面输入和窗口行为在同一活动源中形成错误复用。跨端对齐只固定产品身份、语义角色、主题命名和业务状态，不复制页面。

## 3. 代表画板

| 分级 | 画板 | 尺寸 | 冻结内容 |
| --- | --- | --- | --- |
| Foundation | `FN-R1-F00 / Flutter 视觉基座与四主题` | `1440 × 1000` | 四主题、字体层级、密度、圆角、共享组件、三档断点、motion 与无障碍 |
| R1 | `FN-R1-P01 / Discover / Compact 390` | `390 × 844` | 单列发现流、焦点内容、紧凑业务入口、底部导航 |
| R1 | `FN-R1-P01 / Discover / Expanded 1440` | `1440 × 900` | 扩展 rail、焦点内容、论坛 / Docs / Shop 分区与原生壳层停止线 |
| R1 | `FN-R1-P02 / Forum Detail / Compact 390` | `390 × 844` | 连续阅读、来源与阅读元数据、轻回应、快速回复、底部导航 |
| R1 | `FN-R1-P02 / Forum Detail / Expanded 1440` | `1440 × 900` | 扩展主 rail、本页上下文 rail、受控正文宽度、快速回应与评论预览 |
| R2 | `FN-R2-F01 / Theme Selector + Medium Delta` | `1440 × 900` | Compact Bottom Sheet、Expanded Dialog、Medium `72px` rail、权益 loading / stale / error 与未拥有停止线 |

活动源另有四个 Flutter 专用组件母版：Button、State Chip、Navigation Item、State Slot。它们不从 Web Pencil 文件复制，也不构成另一套业务状态机。

## 4. 视觉与交互裁决

### 4.1 Typography

- UI、正文与数据文字：`Noto Sans SC`。
- 页面主标题和少量品牌强调：`Noto Serif SC`。
- 正文基准为 `15–16px`，常规标题为 `18–24px`，展示标题约 `32px`。
- Serif 只用于有限层级，不进入按钮、表单、导航和高密度数据面。

P3 只冻结字族方向。P4 才决定实际本地子集、Flutter asset 声明和回退链；生产包不得依赖运行时网络取字体。两套 Noto CJK 字体均采用 SIL Open Font License 1.1，正式接入时必须随资产保留对应许可证：[Noto Sans CJK license](https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE)、[Noto CJK repository](https://github.com/notofonts/noto-cjk)。

### 4.2 密度、表面与动效

- Compact 触控目标最低 `48px`；桌面高密度操作最低 `40px`。
- 圆角分为 `8 / 12 / 18` 三档；阅读面与连续列表不层层套卡片。
- 层级优先使用表面差、细边框和留白，阴影只用于 Dialog 等真实浮层。
- motion 固定 `120 / 200 / 280ms` 三档；`MediaQuery.disableAnimations` 下自定义位移与过渡归零，状态仍需完整可读。
- expanded 内容宽度受控，增加导航和上下文效率，不把 mobile 页面机械拉宽，也不复制 WebOS 多窗口桌面。

### 4.3 四主题与权益状态

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
| Forum Expanded | P2 为受控 reading pane 加右侧 `304px` context rail | 按代表稿复核主 rail、本页 context rail、受控正文与互动区的左右关系和密度 |

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

- 六个顶层画板和四个组件母版均已清除 `placeholder`。
- Pencil 原生 visitor 逐根检查未报告 clipping、collapse、overflow 或无效图标。
- Forum Compact、Forum Expanded 与 R2 状态板已完成截图复核；此前 Foundation、Discover Compact 与 Discover Expanded 也已完成截图复核。
- 设计源已通过 Pen 桌面应用原生保存。
- 本批没有修改 Flutter 运行时代码、API、数据库、依赖或平台工程，没有启动服务或执行真实 smoke。

## 8. 明日视觉审核与下一步（2026-08-20）

项目所有者需要确认以下视觉方向：

1. `Noto Sans SC + Noto Serif SC` 的正文 / 少量展示分工；
2. `8 / 12 / 18` 圆角、`48 / 40` 触达密度和克制表面层级；
3. Discover 与 Forum Detail 的 compact / expanded 信息结构；
4. 主题设置的 Bottom Sheet / Dialog 差分和权益失败关闭语义。

审核安排在 `2026-08-20`。确认后先进入 P4-A readiness，形成字体资产接入、许可证、包体积、回退链、依赖与 lockfile 影响说明并另行获得授权，再收口 Theme Foundation、共享组件、Shell、Discover 和 Forum Detail。未确认前不批量改造其他页面族。
