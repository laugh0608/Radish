# P3-12-D63 Console 治理工作台首批实现记录

> 日期：2026-07-05（Asia/Shanghai）
> 范围：`radish.console` 内容治理与经验治理工作台
> 设计源：`Docs/frontend/design-sources/console-governance-workbench.pen`

## 背景

`P3-12-D61 Public Web` 与 `P3-12-D62 Private / Author` 当前发布前页面族已完成首批实现。按 `Docs/planning/current.md`，下一步进入 `P3-12-D63 Console`，继续对照 Console 设计源处理后台页面族的逐页 UI 与现有接口内功能缺口。

本批通过 Pencil MCP 只读确认 `console-governance-workbench.pen` 当前活动文件，并抽取 `P02 - Console Content Moderation - Review Desk`、`P03 - Console Experience Governance - Ledger Desk`、`P07 - Mobile Content Moderation - Review Flow` 与 `P08 - Mobile Experience Governance - Ledger Flow`。设计源强调：内容审核和经验治理不是普通表格页，应按队列、证据、动作、留痕组织工作台，并在窄屏保持治理任务流顺序。

## 实现范围

### 内容治理工作台

- `/console/moderation` 补内容治理任务流摘要，按举报队列、目标证据、处理动作、最近留痕展示当前工作台进度。
- 在手动治理动作前新增目标证据卷宗 rail，复用当前页首条举报的目标快照、举报人、原因、回看状态和真实打开目标动作。
- 证据卷宗可一键带入关联治理留痕筛选，继续复用现有 `applyActionLogPreset`、`resolveOpenTarget` 与权限判断。
- 治理动作日志补最近动作上下文，展示动作单、动作类型、生效状态和操作者，帮助管理员从证据回到留痕。

### 经验治理工作台

- `/console/experience` 补经验台账任务流摘要，按用户定位、趋势证据、流水核对、复核留痕组织页面。
- 在调经验 / 冻结表单前新增台账证据与动作 rail，展示当前用户、等级 / 总经验、系统建议、首要异常规则或当前观察日、冻结状态和最近治理留痕。
- rail 内的“查看流水 / 定位流水 / 带入复核”继续调用现有异常规则、每日统计与复核草稿处理函数，不新增接口或提交字段。
- 共享 Console 样式新增 `governance-task-flow`、`admin-feature-rail` 与 `admin-feature-inline-context`，供治理工作台复用。

## 保持不变

- 不新增 API、权限键、数据库结构、路由语义或保存 / 提交载荷。
- 不改变举报审核、手动治理动作、治理日志、目标打开、经验查询、经验调整、冻结 / 解冻、复核结论、等级重算等既有行为。
- 不引入独立移动 Console 应用；`P07 / P08` 仅作为响应式后台任务流参考。
- 不实现 `P04 / P13 / P18` 内部调度、内部 Jobs、失败重试、运行审计或完整任务平台；这些继续作为 D63 内后置产品 / API 缺口。

## 验证

- `npm run build --workspace=radish.console`：通过。
- `npm run lint:changed`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

本批尚未执行 Gateway PC / mobile 真实页面 smoke。按协作规则，真实 smoke 需要在执行前由用户在本轮明确确认前后端已启动；当前先完成代码侧与静态验证。

## 后续

- 继续 `P3-12-D63 Console`，后续建议进入表格 CRUD / 权限矩阵页面族，对照 `P05 / P12` 补选中对象摘要、审计 / 权限上下文、详情抽屉、权限预览和保存反馈。
- D63 完成前不进入 `P3-12-E`，不创建发布 tag，不恢复发布流程。
