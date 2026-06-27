# 设计源文件目录

本目录用于放置由 Pencil 管理的 `.pen` 设计源文件。

规则：

- `.pen` 文件作为设计源参与 Git 版本控制，便于审阅设计变更历史。
- `.pen` 文件内容只能通过 Pencil 工具创建、读取和修改。
- 不使用普通文本工具打开或编辑 `.pen` 文件。
- 设计源文件名应表达端点和职责。
- Pencil 写入以当前活动窗口为准；切换或创建 `.pen` 前，必须确认 Pencil 已打开目标文件并在写入后手动保存。
- 不要假设 MCP `filePath` 会把写入可靠落到非活动窗口文件；切换文件前若未手动保存，可能丢失或误落到上一活动文件。

当前源文件：

- `web-ui-foundation.pen`：Web UI 共享基座设计端点，当前包含 `F01-F02` 共享样板画板，覆盖 public / private header 合法变体、按钮 / pill、卡片 / rail、状态槽、移动 shell / tab、client 公共壳层组件契约和跨设计源同步规则。
- `public-web-unified-experience.pen`：公开 Web 统一体验设计端点，当前包含 `P01-P16` 编号画板，已从路由矩阵重构并二次强化为真实公开社区 App 页面族，覆盖公开首页、发现流、论坛列表 / 详情、评论树、轻回应、公开聊天室、文档列表 / 详情、商城、榜单、公开主页与移动公开任务流。
- `private-web-workflows.pen`：私域与作者态 Web 工作流设计端点，当前包含 `P01-P05` 编号画板，覆盖私域首页、资产 / 订单 / 背包、作者工作台、编辑器 / 版本回看与移动私域单列关键画板。
- `console-governance-workbench.pen`：Console 治理工作台与后台视觉基座设计端点，当前包含 `P00-P18` 编号画板，已按 `web-ui-foundation.pen` 共享基座重构并扩展，覆盖公共 Console 壳层、浅色图标侧栏、内容审核、经验台账、治理调度、表格 CRUD、设置策略、商业运营、文档治理、权限矩阵、运维任务和移动端 Console 任务流参考。

模板文件：

- `empty-design-source-template.pen`：空白 Pencil 文件模板，仅用于复制新设计源；不要直接承载业务设计稿。

配套说明：

- [Web UI 共享基座设计说明](../web-ui-foundation-design.md)
- [公开 Web 统一体验设计说明](../public-web-unified-experience-design.md)
- [私域与作者态 Web 工作流设计说明](../private-web-workflows-design.md)
- [Console 治理工作台设计端点](../console-governance-workbench-design.md)

同步规则：

- 跨 public / private / console 共享的 header、按钮、pill、卡片、rail、状态槽和移动 tab 先在 `web-ui-foundation.pen` 确认。
- 业务设计源可以有不同页面密度、导航项和端点职责，但不得自行分叉共享视觉样式。
- 后续如需阶段级横向审阅，可按需新增轻量 `web-ui-review-board.pen`，只放关键画板截图或代表性 frame，不承载完整编辑源。
- 修改任一 `.pen` 后，先在 Pencil 内手动保存，再做 `snapshot_layout`、截图目检和 Git 提交。
