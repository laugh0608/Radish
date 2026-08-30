# 设计源文件目录

本目录用于放置由 Pencil 管理的 `.pen` 设计源文件。当前采用[代表页驱动协作流程](../pencil-representative-page-workflow.md)：Pencil 只维护共享视觉契约、R1 / R2 代表结构、关键状态和响应式变化，不再镜像全部路由、文案或功能点。

## 当前活动设计源

- `radish-web-family-ui-v1.pen`：Radish Web / Console 唯一活动设计源。当前保留已确认的 `R1-P01 / 社区发现 / PC 1440`、`Mobile 390`、`8` 个必要组件母版和主题变量；后续命中的 R1 / R2 代表设计继续在同一文件维护。
- `radish-flutter-native-ui-v1.pen`：Flutter Native 独立活动设计源。承载原生视觉基础、compact / medium / expanded 壳层、Discover / Forum Detail 代表结构及必要 R2 状态；可复制正式 Web 可编辑代表页作为信息架构母版，但必须在本文件内完成 Flutter 安全区、壳层、token、输入和响应式归一化，不复制四套主题页面族。
- 当前 family-ui 基线：RadishX `v26.7.3`。
- Web / Console 当前进度：`R1-F01`、六个 R1、四个 R2、六组 R3 继承与 Web 四主题退出门禁均已关闭；后续只在新的真实代表差异触发升级条件时修改 Web 活动源。
- Flutter 当前进度：P1–P5 已完成首轮主题、页面族与成组静态门禁，P6 双 AVD 运行态与项目所有者 `Android UI AVD RC Go` 裁决已关闭；P7-B 已生成 iOS 工程、实现跨平台持久化和系统 OIDC bridge，并通过 Android / iOS 原生 build 门禁，当前进入 P7-C readiness。这些批次没有暴露需要回写 Pencil 的共享视觉结构偏差；只有后续 Simulator 或真机出现共享结构偏差时才进入新的代表设计裁决。
- `R1-P01` 评审结论：Public 内容流采用现代自然紧凑语法、非对称主次、连续扫描行和灰玉 / 墨蓝 / 国风暖白家族色；后续页面继承视觉语法，不照搬社区信息架构。
- `R1-P01` 颜色裁决：`guofeng` 品牌使用低饱和灰玉 `#5d6c57`，悬停 `#6e736d`，常规操作继续使用墨蓝 `#435c74`；Web 全局品牌 token 已由 T01 成组更新并通过 T03 四主题运行态验收，Flutter 在 P4-B1 通过独立 Dart 语义映射对齐。

文件名中的 `v1` 表示对应产品线设计源的主版本，而不是每次提交的快照号：

- 日常新增或修订代表画板、组件、关键状态、主题样例和文案时，继续更新 `v1`，由 Git 保存变更历史；
- 只有共享视觉契约、画板组织或协作治理发生不兼容的结构性变化，且无法在现有文件内清晰演进时，才新建 `v2`；
- 新主版本建立后，旧主版本转为只读留档，不同时维护多个活动版本。

## 历史留档

以下既有大型设计源保留为历史设计资产，不删除、不改名、不继续同步或新增画板：

- `web-ui-foundation.pen`
- `public-web-unified-experience.pen`
- `private-web-workflows.pen`
- `console-governance-workbench.pen`

需要追溯旧批次事实时可以只读查看；当前 Web / Console 设计进入 `radish-web-family-ui-v1.pen`，Flutter Native 设计进入 `radish-flutter-native-ui-v1.pen`。旧文件中的路由级画板不再构成“路由与 Pencil 一一对应”的现行要求。

模板文件：

- `empty-design-source-template.pen`：空白 Pencil 文件模板，不承载活动业务设计稿。

## 操作规则

- `.pen` 参与 Git 版本控制，但内容只能通过 Pencil 工具创建、读取和修改，禁止使用普通文本工具打开或编辑。
- Pencil 写入以当前活动窗口为准；切换或创建文件前确认产品线与活动文件，写入后在 Pencil 内保存。
- 不假设 MCP `filePath` 会把写入可靠落到非活动窗口文件；切换前未保存可能导致内容丢失或写入错误文件。
- 修改后对目标节点执行布局检查与截图目检，再提交 Git。
- 功能、按钮、文案、权限、接口、路由和状态机以专题文档与当前代码为准；Pencil 不自行发明功能。
- 设计前先完成代码事实审计、自动升级判断和六维评分，明确 R1 / R2 / R3 与继承关系。
- R3 派生页直接继承代表画板进入实现，通过真实 PC / mobile 截图复核，不追加重复画板。
- 四主题通过变量矩阵和少量代表面验证，不复制四套页面族。
- 阶段级横向审阅使用对应产品线活动文件内的轻量 review frame 或导出截图，不再创建独立评审 `.pen`。

## 配套说明

- [Pencil 代表页协作流程](../pencil-representative-page-workflow.md)
- [F4-R C-1 代表页代码事实审计](../f4-r-representative-page-audit.md)
- [Web UI 共享基座设计说明](../web-ui-foundation-design.md)
- [公开 Web 统一体验设计说明](../public-web-unified-experience-design.md)
- [私域与作者态 Web 工作流设计说明](../private-web-workflows-design.md)
- [Console 治理工作台设计端点](../console-governance-workbench-design.md)

## 同步规则

- Web 的 header、按钮、pill、卡片、状态槽和移动 tab 先在 Web 活动源的 `R1-F01` 确认；Flutter 的 App Bar、Navigation Bar / Rail、Button、Card、Field、Chip 和状态面先在 Flutter 活动源的原生基础矩阵确认。
- Public、Private / Author、Console 代表画板在同一文件中组合合法变体；业务密度和导航职责可以不同，但不得分叉共享样式。
- Flutter 代表画板只把 Flutter 活动源组件视为可复用 owner；为保留已确认信息架构，可以从 Web 活动源复制代表页母版，但复制结果必须转为 Flutter 组件、壳层和交互语义，不能把跨文件副本伪装成共享组件或直接实现依据。
- 共享契约变更只更新受影响的 R1 / R2；R3 从代码共享层继承，不复制结构。
- 移动底栏统一使用浮动胶囊样式、图标上文字下、5 项以内顶级入口和柔和品牌色激活态；`radish.client` public / private 共用 `发现 / 论坛 / 聊天 / 更多 / 我的`，Console 保留后台专用导航职责。
- `/workbench` 是正式 Web “更多”功能地图，PC header 和移动底栏只展示高频入口，其余能力由“更多”或页面内入口承接。
- 读取和审阅优先定位当前任务节点，避免无目的加载历史大型设计源。
