# F4-J-B 内容治理申诉与处置纠正服务端契约

## 结论

F4-J-B 已完成服务端实现，下一顺位进入 F4-J-C Pencil 与正式页面。

本批保持原 `ContentModerationCase`、举报、证据和决定不可变，新增独立 Appeal 聚合、目标动作来源和纠正流水；没有修改 Pencil 或正式页面，也没有启动本地服务或执行浏览器 smoke。

## 已完成范围

- Main migration `20260725_009_content_moderation_appeal` 建立 `ContentModerationAppeal`、`ContentModerationAppealEvent`、`ContentModerationTargetAction`，增加 Appeal / TargetAction 关联字段、四类 Main 目标来源标记及历史限制来源回填。
- Chat migration `20260725_010_chat_moderation_relief` 为 Message、Reaction、Pin 增加治理来源标记；不猜测历史无来源撤回。
- 本人决定、一次申诉、撤回、受理、全部 / 部分采纳 / 维持原决定、CAS 与 operation key 已进入专属 Repository。
- Post、Comment、PostQuickReply、Product 与 ChatMessage 五类恢复均要求当前不可用状态仍由原限制动作持有；来源不匹配、父对象不可用或后续业务变化记录为明确终态，不强制覆盖。
- Mute / Ban 只在当前权威状态仍为 Active、未自然到期且 `SourceCaseId` 等于原案件时写入 Unmute / Unban 纠正流水。
- Chat 恢复通过 Main ReliableOutbox 进入 Chat 单事务，恢复消息搜索投影及由同一限制动作联动删除的 Reaction / Pin，并回写 Appeal 终态和实时事件。
- 新增 `console.moderation.appeal`，申诉复核与动作执行继续分别受 Appeal / Action 权限控制。
- 决定通知和申诉状态通知使用公开 Case / Appeal 标识；本人 API 裁剪内部备注和事件备注。
- `@radish/http` 增加申诉、决定、事件与目标动作契约；HTTP 示例使用进程环境 token。

## 验证结果

- `dotnet build Radish.slnx -c Debug --no-restore`：通过，0 warning / 0 error。
- `dotnet test Radish.Api.Tests --no-build`：991 通过，26 个 PostgreSQL 环境用例因未配置连接串跳过。
- 内容治理、可靠任务与通知定向测试：37 通过，1 个 PostgreSQL 环境用例跳过。
- `npm run type-check --workspace=@radish/http`：通过。
- `npm run validate:baseline:quick`：通过，覆盖前端类型检查与测试、Console 权限链路、敏感字面量、时间语义、仓库质量和身份语义扫描。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

## 下一批边界

F4-J-C 先更新 `/me/appeals` PC / mobile 与 Console `P02 / P07` Pencil 设计源，再实现本人决定 / 申诉页、通知深链、Console 申诉工作台、权限裁剪、双语和四主题适配。本批不提前执行 Gateway 成组验收。
