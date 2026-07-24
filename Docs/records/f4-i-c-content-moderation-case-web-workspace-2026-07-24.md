# F4-I-C 内容治理案件正式页面完成记录

> 日期：2026-07-24（Asia/Shanghai）
>
> 范围：Console `/moderation`、正式 Web `/me/reports`、共享治理契约、旧 HTTP 入口退役与静态门禁

## 结论

F4-I-C 已完成。Console 与举报者页面现在消费同一套案件、证据、决定、动作和精简结果权威契约；旧逐举报审核与手工治理 HTTP 入口已退出公开 API。专题尚未关闭，Gateway 多角色、五目标运行态矩阵归 F4-I-D。

## 设计与页面

- `console-governance-workbench.pen` 在既有 `P02 / P07` 上收口案件队列、追加式证据、决定 / 动作分离、权限边界和冲突恢复。
- `private-web-workflows.pen` 新增 `/me/reports` PC / mobile 画板；只展示本人收件、目标摘要、可用导航和精简结果。
- Console `/moderation` 已切换到 `GetCaseQueue / GetCase / CaptureEvidence / ReviewCase / ApplyCorrectiveAction`。
- 没有 `moderation.action` 时只能提交不含用户动作的决定；`409` 后重新加载权威案件，但保留未提交草稿并生成新的 operation key。
- `/me/reports` 归入既有 `/me` 私域与认证回流；目标失效时保留举报时摘要，不提供撤回、催办、附件或管理员对话。

## 契约与退役

- Evidence 增加内容 revision 与目标修改时间，决定前可明确绑定最新证据版本。
- 举报收件补齐目标标识、导航状态、失效说明、提交时标题 / 摘要和原因类型。
- 仓库消费者迁移确认后，API Controller 删除 `GetReviewQueue / Review / ApplyUserAction / GetActionLogs`，权限映射与初始化资源同步退役；回归测试固定旧入口不再暴露或解析权限。
- Console 用户详情只保留进入案件工作台的排障深链，不再保留第二套手工治理表单。

## 验证

- `dotnet test Radish.Api.Tests --no-restore`：`978` 通过，`26` 跳过，`0` 失败。
- `dotnet build Radish.slnx -c Debug --no-restore`：`0 warning / 0 error`。
- `radish.client`：`454` 项测试、type-check、lint、production build 通过。
- `radish.console`：`58` 项测试、strict type-check、lint、production build 通过。
- `@radish/http` 与 `@radish/ui` type-check / 测试通过。
- `npm run validate:baseline:quick` 通过。

本批没有启动 API、Auth、Gateway、client 或 Console 服务，没有执行真实浏览器 smoke、跨库动作运行态验证、临时数据清理或数据库完整性检查；这些边界按计划保留到 F4-I-D。

## 下一顺位

F4-I-D 使用举报者、被举报者、只读治理员、审核员和动作执行员，覆盖五类目标、权限、并发、失败恢复、目标失效、双语、PC / mobile 与主题代表矩阵，随后完成临时数据清理、数据库完整性和严格 migration verify，满足完成标准后关闭 F4-I。
