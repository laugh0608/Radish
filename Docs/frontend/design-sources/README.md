# 设计源文件目录

本目录保存历史设计源及按产品面和版本组织的新 Penpot 评审包。2026-11-14 起停用 Pencil，现行 R1 / R2 / R3 流程见[代表页设计协作流程](../pencil-representative-page-workflow.md)。

## Web / Console 与 Flutter 基准留档

- `radish-web-family-ui-v1.pen`：Web / Console 历史基准，保留已确认的 `R1-P01 / 社区发现 / PC 1440`、`Mobile 390`、8 个组件母版和主题变量。
- `radish-flutter-native-ui-v1.pen`：Flutter Native 历史基准，保留原生视觉基础、compact / medium / expanded 壳层、Discover / Forum Detail 代表结构和必要 R2 状态。
- 当前 family-ui 基线：RadishX `v26.7.3`。
- Web / Console 已关闭 `R1-F01`、六个 R1、四个 R2、六组 R3 继承与四主题退出门禁。
- Flutter 当前进度：P1–P5 已完成首轮主题、页面族与成组静态门禁，P6 双 AVD 运行态与项目所有者 `Android UI AVD RC Go` 裁决已关闭；P7-B 已生成 iOS 工程、实现跨平台持久化和系统 OIDC bridge，并通过原生 build 门禁，P7-C 又以同一候选在 iPhone compact 与 iPad medium / expanded 完成系统 OIDC、Keychain、旋转、真实键盘和主题代表面，结论为 `Simulator Go`。后续真机或新结构若暴露共享差异，按现行流程裁决代表设计。
- `R1-P01` 评审结论：Public 内容流采用现代自然紧凑语法、非对称主次、连续扫描行和灰玉 / 墨蓝 / 国风暖白家族色；后续页面继承视觉语法，不照搬社区信息架构。
- `R1-P01` 颜色裁决：`guofeng` 品牌使用低饱和灰玉 `#5d6c57`，悬停 `#6e736d`，常规操作继续使用墨蓝 `#435c74`；Web 全局品牌 token 已由 T01 成组更新并通过 T03 四主题运行态验收，Flutter 在 P4-B1 通过独立 Dart 语义映射对齐。

## 历史留档

以下既有大型设计源保留为历史设计资产，不删除、不改名、不继续同步或新增画板：

- `web-ui-foundation.pen`
- `public-web-unified-experience.pen`
- `private-web-workflows.pen`
- `console-governance-workbench.pen`

模板文件：

- `empty-design-source-template.pen`：空白 Pencil 文件模板，不承载活动业务设计稿。

## 新设计证据

新任务按代表页分级记录可编辑的 Penpot 稿或代码原型、评审图与代码锚点。采用 Penpot 时在本目录的 `reviews/` 按产品面、代表页或交互及版本保存源稿、预览和恢复记录。R3 沿用已确认的结构和组件，在真实 PC / mobile 页面上复核。已有 `.pen` 及其预览保持归档状态。[Penpot 评审留档与恢复](../pencil-representative-page-workflow.md#56-penpot-评审留档与恢复)

## 配套说明

- [代表页设计协作流程](../pencil-representative-page-workflow.md)
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
