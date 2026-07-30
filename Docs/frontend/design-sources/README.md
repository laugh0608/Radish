# 设计源文件目录

本目录用于放置由 Pencil 管理的 `.pen` 设计源文件。

当前采用[代表页驱动协作流程](../pencil-representative-page-workflow.md)：`.pen` 只维护共享视觉契约、主要页面和必要关键状态，不再按路由、文案或功能点创建完整页面镜像。

规则：

- `.pen` 文件作为设计源参与 Git 版本控制，便于审阅设计变更历史。
- `.pen` 文件内容只能通过 Pencil 工具创建、读取和修改。
- 不使用普通文本工具打开或编辑 `.pen` 文件。
- 功能、按钮、文案、权限、接口和路由以专题文档与当前代码为准；Pencil 不自行发明功能，也不承担逐项同步。
- 设计前按代表页流程完成代码事实审计、自动升级判断和六维评分，明确 R1 / R2 / R3 及继承关系。
- R3 派生页面直接复用代表画板进入实现，通过真实 PC / mobile 截图复核，不追加重复画板。
- 设计源文件名应表达端点和职责。
- Pencil 写入以当前活动窗口为准；切换或创建 `.pen` 前，必须确认 Pencil 已打开目标文件并在写入后手动保存。
- 不要假设 MCP `filePath` 会把写入可靠落到非活动窗口文件；切换文件前若未手动保存，可能丢失或误落到上一活动文件。

当前源文件：

- `web-ui-foundation.pen`：Web UI 共享基座设计端点，当前包含 `F01-F03` 共享样板画板和 `E7-D - Auth Consent Information Hierarchy` 授权确认具体决策卡参考画板，覆盖 public / private header 合法变体、按钮 / pill、卡片 / rail、状态槽、移动 shell / tab、client 公共壳层组件契约、Auth 授权页信息层级、动作区安全距离和跨设计源同步规则；移动底栏统一为 5 项浮动胶囊样式。
- `public-web-unified-experience.pen`：公开 Web 统一体验设计端点，当前包含核心 `P01-P16` 编号画板、F4-K 公开主页 `P09B / P09C / P14B / P14C` 状态画板、F4-N 内容赞赏 `P04B / P11B` 以及 F4-O 回答生命周期 `P26 / P27` PC / mobile 状态画板；`P04 / P11` 已同步帖子 / 评论赞赏、问答状态、已采纳回答、其他回答分页和回答 composer。设计源覆盖公开首页、发现流、论坛列表 / 详情、回答生命周期、评论树、轻回应、公开聊天室、文档列表 / 详情、商城、榜单、公开主页、屏蔽确认、通用不可互动状态、内容赞赏与移动公开任务流；E8 后正式 Web public / private 移动底栏统一为 `发现 / 论坛 / 聊天 / 更多 / 我的`，更多公开能力由 `/workbench` 功能地图承接。
- `private-web-workflows.pen`：私域与作者态 Web 工作流设计端点，当前包含 `P01-P30` 编号画板、通知状态补充画板 `P12B / P26B / P26C`、消息状态画板 `P13B / P13C / P27B / P27C`、圈子关系刷新 `P14B / P28B`、治理申诉 `P33 / P34` 及屏蔽列表 `P35 / P36` PC / mobile 画板，覆盖 `/workbench`、`/me` 系列、资产流水、订单、背包、通知、消息、圈子、宠物、论坛作者态、Docs 作者态和移动端单任务页面。
- `console-governance-workbench.pen`：Console 治理工作台与后台视觉基座设计端点，当前包含 `P00-P18` 编号画板，已按 `web-ui-foundation.pen` 共享基座重构并扩展；`P02 / P07` 已承接治理案件与申诉复核工作台，其他画板覆盖公共 Console 壳层、浅色图标侧栏、经验台账、治理调度、表格 CRUD、设置策略、商业运营、文档治理、权限矩阵、运维任务和移动端 Console 任务流参考。

上述大范围画板是既有阶段形成的历史设计资产，不再代表后续必须维持“路由与 Pencil 一一对应”。本轮不一次性重写或删除；后续只维护命中的代表画板，拆分与归档另行治理。

模板文件：

- `empty-design-source-template.pen`：空白 Pencil 文件模板，仅用于复制新设计源；不要直接承载业务设计稿。

配套说明：

- [Pencil 代表页协作流程](../pencil-representative-page-workflow.md)
- [Web UI 共享基座设计说明](../web-ui-foundation-design.md)
- [公开 Web 统一体验设计说明](../public-web-unified-experience-design.md)
- [私域与作者态 Web 工作流设计说明](../private-web-workflows-design.md)
- [Console 治理工作台设计端点](../console-governance-workbench-design.md)

同步规则：

- 跨 public / private / console 共享的 header、按钮、pill、卡片、rail、状态槽和移动 tab 先在 `web-ui-foundation.pen` 确认。
- 共享契约变更只同步受影响的代表画板；派生页面从代码复用共享组件，不在各业务源复制同一结构。
- 移动底栏统一使用浮动胶囊样式、图标上文字下、5 项以内顶级入口和柔和品牌色激活态；`radish.client` public / private 共用 `发现 / 论坛 / 聊天 / 更多 / 我的`，Console 继续使用后台专用底栏，但不得自行分叉底栏形态。
- `/workbench` 是正式 Web “更多”功能地图。PC header 和移动底栏都只展示高频入口，其余公开 / 私域功能通过“更多”或页面内功能入口回到 `/workbench` 承接。
- 业务设计源可以有不同页面密度、导航项和端点职责，但不得自行分叉共享视觉样式。
- 后续如需阶段级横向审阅，可按需新增轻量 `web-ui-review-board.pen`，只放关键画板截图或代表性 frame，不承载完整编辑源。
- 读取和审阅时优先定位当前任务相关节点，避免默认加载整个大型设计源。
- 修改任一 `.pen` 后，先在 Pencil 内手动保存，再对目标节点做 `snapshot_layout`、截图目检和 Git 提交。
