# 内容治理系统说明

> **适用范围**：帖子、评论、回答、轻回应、聊天消息、商品的用户举报，Console 案件与申诉处理，以及目标处置、禁言 / 封禁和纠正后的当前状态
>
> **最后更新**：2026-08-09（Asia/Shanghai）
>
> **权威设计**：[内容治理案件、证据与动作一致性](/features/content-moderation-case-evidence-action-design) · [内容治理申诉与处置纠正](/features/content-moderation-appeal-relief-design)

## 系统定位

内容治理使用“举报事实 → 案件 → 证据 → 决定 → 动作 → 结果 → 申诉 → 纠正”的可追溯链路。它是人工治理系统，不是机器审核、敏感词平台、自动处罚或私聊浏览工具。

核心原则：

- 同一租户、同一目标在一个处理周期内只有一个开放案件，多名举报者共享案件结论，但彼此不可见。
- 举报时快照、当前复核证据和动作结果只追加，不覆盖历史依据。
- 决定、目标处置和用户限制是三个不同事实；决定成功不等于动作已经成功。
- 禁言 / 封禁的当前状态只读取 `UserModerationState`，不扫描旧动作流水猜测。
- 申诉是独立聚合，不重开或改写原案件；采纳申诉也不等于纠正动作已经成功。
- 目标恢复只撤销仍由原治理动作造成的状态，不覆盖作者、商品管理员、其他案件或后续业务操作。
- Console 权限、目标领域 ACL 和租户边界分别校验，管理员身份不自动获得未举报私聊的浏览权。

## 角色与可见范围

| 角色 | 可以看到 | 不可看到 |
| --- | --- | --- |
| 举报者 | 自己的举报公开标识、目标摘要、提交时间、`Submitted / Resolved` 和精简结果 | 案件内部 ID、其他举报者、内部备注、证据正文、管理员身份、具体处罚 |
| 被处置用户 | 自己当前是否被禁言 / 封禁、有效截止时间和状态变化通知摘要 | 举报者身份、内部证据、其他举报内容、治理人员身份 |
| `console.moderation.view` | 案件队列、举报聚合、证据、事件和当前用户状态 | 决定或动作写入能力 |
| `console.moderation.review` | View 范围，加上追加复核证据、登记案件决定和目标处置 | 没有 Action 权限时不能提交用户动作或纠正动作 |
| `console.moderation.action` | 禁言 / 封禁 / 解除和结案后用户状态纠正 | 绕过 Review、目标 ACL、受保护账号或租户边界 |
| `console.moderation.appeal` | 查看完整申诉陈述、追加申诉证据并登记复核结果 | 仅凭 Appeal 权限执行目标恢复或用户限制解除 |

## 举报者使用说明

登录用户可以从帖子、评论、回答、轻回应、聊天消息或商品的既有举报入口提交原因和不超过 500 字的补充说明。

`POST /api/v1/ContentModeration/Report` 返回 `ContentReportReceiptVo`：

- `voReportPublicId`：举报者可保存和反馈的公开编号。
- `voTargetType / voTargetSnapshotTitle`：举报目标类型和提交时摘要。
- `voReporterState`：只使用 `Submitted / Resolved`。
- `voPublicResultCode`：结案后的稳定精简结果，由页面映射为“未发现违规 / 已采取措施 / 证据不足”。
- `voSubmittedAt / voResolvedAt`：提交与结案时间。
- `voIsDuplicate`：同一用户对同一开放案件重复提交时为 `true`；重复提交不会制造第二份举报事实。

本人结果读取使用：

- `GET /api/v1/ContentModeration/GetMyReports`
- `GET /api/v1/ContentModeration/GetMyReport/{reportPublicId}`

产品入口归属 `/me/reports` 私域，不进入 sitemap、canonical 或 JSON-LD。目标仍可访问时使用既有结构化导航；目标失效时保留提交摘要并说明不可打开。该入口不提供撤回、催办、追加附件或与管理员对话。

## 案件与证据

| 对象 | 职责 |
| --- | --- |
| `ContentReport` | 用户提交的独立举报事实、举报者可见状态和提交时摘要 |
| `ContentModerationCase` | 同一目标本处理周期的状态、决定、版本、公开结果和内部备注 |
| `ContentModerationEvidence` | 追加式举报快照、当前目标快照、治理备注和动作结果 |
| `ContentModerationCaseEvent` | 案件状态、证据、决定、动作请求 / 结果、通知和纠正动作时间线 |
| `ContentModerationTargetAction` | 六类目标 Restrict / Restore 的来源、版本、幂等键和执行结果 |
| `ContentModerationAppeal` | 被处置用户的一次申诉、版本、复核结果、纠正范围和公开说明 |
| `ContentModerationAppealEvent` | 申诉提交、接手、证据、决定、纠正、失败重试和通知时间线 |
| `UserModerationAction` | 禁言、封禁、解除和迁移校准的不可变动作流水 |
| `UserModerationState` | 用户某类限制的唯一当前状态、截止时间和版本 |

证据目标状态使用 `Available / Deleted / Recalled / Disabled / Unavailable`。目标后来被编辑、删除、撤回或下架时，历史快照不会被当前内容覆盖。

## 决定、目标处置与动作

案件决定：

- `NoViolation`：只能保留目标，不允许附带用户动作。
- `InsufficientEvidence`：只能保留目标或记录目标不可用，不允许限制目标或用户。
- `Violation`：必须明确目标处置或用户动作，不能依赖空值猜测。

目标处置结果使用 `Keep / Restricted / Unavailable / ActionPending / ActionFailed`。其中：

- Post、Comment、PostAnswer、PostQuickReply、Product 在 Main 事务内执行限制。
- Post / Comment 使用 `EditCount`，PostAnswer 使用 `ContentRevision`，Product 使用 `Version` 校验目标版本；版本变化时拒绝覆盖。
- 被限制的 PostAnswer 如果正被采纳，会在同一事务中清除采纳并追加采纳事件；后续恢复不会自动重新采纳。
- ChatMessage 通过 Main 可靠 Outbox 请求 Chat 库精确撤回；任务完成前案件保持处理中，不能提前显示“已采取措施”。

用户动作使用 `Mute / Ban / Unmute / Unban`：

- Mute / Ban 激活时可以设置 1–720 小时，空截止时间表示永久限制。
- Unmute / Unban 不允许携带持续时间。
- Ban 生效时会终止当前 Mute，但解除 Ban 不恢复旧 Mute。
- 自然到期由读取时按 UTC 实时派生，不修改历史动作流水。
- 不能对自己或受保护的 System / Admin 账号执行限制。

## 申诉与处置纠正

被处置用户从 `/me/appeals` 查看本人可申诉决定、资格截止时间、实际影响范围和申诉结果。每个案件对目标用户只接受一次申诉，申诉陈述为 20–1000 字；决定登记前允许撤回，但撤回后不能重新提交同案申诉。

- 申诉状态使用 `Submitted / Reviewing / ReliefPending / ReliefFailed / Resolved / Withdrawn`。
- 复核结果使用 `Upheld / PartiallyGranted / Granted`；部分采纳必须明确 `TargetContent / Mute / Ban` 中的纠正范围。
- Post、Comment、PostAnswer、PostQuickReply、Product 在 Main 事务内按来源与版本恢复；ChatMessage 通过可靠 Outbox 恢复消息可见性、搜索投影以及由同一治理动作移除的 Reaction / Pin。
- 用户限制只在 `UserModerationState.SourceCaseId` 仍指向原案件时解除；后续限制、自然到期或独立纠正不会被申诉反向覆盖。
- `ReliefPending / ReliefFailed` 都不是“已纠正”；只有所有选定纠正项成功、已被后续状态替代或确认无需执行后才能进入终态。
- 申诉正文、举报者、证据和操作员不向普通通知、举报者或无 `console.moderation.appeal` 权限的角色披露。

## 并发、幂等与失败恢复

- 案件写入提交 `expectedVersion`；版本冲突后必须刷新证据和目标状态，保留尚未提交的决定、备注和动作草稿。
- 申诉写入提交 `expectedAppealVersion`；冲突后保留申诉或审核草稿并刷新权威详情。
- 用户动作提交 `expectedStateVersion`，防止两个治理人员覆盖同一类当前限制。
- 决定、申诉提交 / 撤回 / 复核和纠正动作提交 8–160 字符的 `operationKey`；同键同参数回放原结果，同键异参返回冲突。
- Main 库动作失败时整个决定事务回滚；跨 Chat 动作失败保留失败事件和可重试任务。
- 通知失败不回滚已经生效的治理动作，由可靠任务按稳定业务键重试。
- 目标不存在、跨租户、来源举报与目标用户不匹配、目标版本变化和案件版本冲突均返回稳定 `Code / MessageKey`，调用方不得按展示文案分支。

## 通知与隐私

治理通知属于 `Governance` 分类：

- `ContentReportResolved`：普通优先级，发送给案件内各举报者，只包含精简 `resultCode`。
- `UserModerationChanged`：高优先级，发送给被处置用户，只包含动作类型摘要。
- `ContentModerationDecisionAvailable`：发送给被处置用户，结构化导航到本人 `/me/appeals` 决定入口。
- `ContentModerationAppealUpdated`：只发送给申诉人，提供申诉状态和精简公开结果。

举报结案和账号状态变化通知使用 `TargetKind=None`；决定与申诉通知使用 `GovernanceDecision / GovernanceAppeal` 结构化目标，但只能进入本人私域入口。任何通知都不得构造 Console 案件链接，也不得在正文泄露举报者、内部证据、内部备注、申诉陈述或管理员身份。账号限制、申诉和纠正状态必须重新读取权威接口，不能把通知正文当作状态真相源。

## API 与权限

用户侧：

- `POST Report`
- `GET GetMyReports / GetMyReport/{reportPublicId}`
- `GET GetMyModerationStatus / GetMyPublishPermission`
- `GET GetMyAppealableDecisions / GetMyAppeals / GetMyAppeal/{appealPublicId}`
- `POST SubmitAppeal / WithdrawAppeal`

Console：

- View：`GetCaseQueue / GetCase/{casePublicId} / GetCaseEvents`
- Review：`CaptureEvidence / ReviewCase`，包括案件决定和目标处置
- Action：带用户动作的 `ReviewCase`、`ApplyCorrectiveAction`；不替代 Review
- View：`GetAppealQueue`，只返回调度所需的脱敏摘要
- Appeal：`GetAppeal / GetAppealEvents / StartAppealReview / CaptureAppealEvidence / ReviewAppeal`
- Action：`ExecuteAppealRelief`，只执行已获准的纠正范围

旧 `GetReviewQueue / Review / ApplyUserAction / GetActionLogs` HTTP 入口已在正式页面迁移后移除。案件、申诉和动作契约统一位于 `Frontend/radish.http/src/content-moderation-contract.ts`，不得恢复旧举报单写模型或另建 fetch / axios 封装。

LongId 在 HTTP 和 TypeScript 边界保持字符串；普通用户只接收 `mod_... / apl_...` 等公开标识和隐私裁剪后的字段。

## 数据库与迁移

Main migration `20260721_008_content_moderation_case`：

- 创建 Case / Evidence / Event / UserModerationState，并扩展举报和动作字段。
- 将历史 Pending 举报按目标聚合，历史终态保持可解释的独立案件。
- 把历史举报快照、决定和有效用户动作映射为追加式证据、事件和唯一当前状态。
- `doctor / verify` 检查非法枚举、时间、来源错配、开放案件唯一性、序号、状态和业务键漂移。

申诉与纠正由 Main `20260725_009_content_moderation_appeal` 和 Chat `20260725_010_chat_moderation_relief` 承接：

- 创建 Appeal / AppealEvent / TargetAction，并为当时五类目标及用户动作补治理来源。
- 只对可证明由原案件造成的历史限制回填可恢复来源；无法唯一证明的历史状态保持不可自动恢复。
- Chat 迁移补消息、Reaction、Pin 的来源标记和跨库恢复校验，不补造申诉、不重开案件。

F4-O 的 Main `20260727_015_forum_answer_lifecycle` 与顺序校验 migration `20260728_016_forum_answer_lifecycle_strict` 随后把 PostAnswer 补为第六类治理目标；回答继续复用现有 Case / Evidence / TargetAction / Appeal 聚合，不另建治理表或 Console 运营台。

已有数据库必须按 `doctor → 备份 → apply → verify` 前滚；不得依赖 API 启动或 Code First 静默补结构。详细规则见[数据库结构变更协作口径](/guide/database-schema-change-governance)。

## 实现入口与验证

- Controller：`Radish.Api/Controllers/ContentModerationController.cs`
- Service：`Radish.Service/ContentModerationService.cs`、`ContentModerationService.Cases.cs`
- Repository：`Radish.Repository/ContentModerationCaseRepository*.cs`，其中申诉与纠正分别位于 `.Appeals.cs / .AppealRelief.cs`
- Migration：`Radish.DbMigrate/ContentModerationCaseSchemaMigration.cs`、`ContentModerationAppealSchemaMigration.cs`、`ChatModerationReliefSchemaMigration.cs`
- HTTP 示例：`Radish.Api.Tests/HttpTest/Radish.Api.Community.http`、`Radish.Api.ContentModeration.Appeal.http`
- Repository / migration 测试：`Radish.Api.Tests/Repositories/ContentModerationCaseRepositoryTest.cs`、`ContentModerationCaseSchemaMigrationTest.cs`

修改治理契约时，至少覆盖案件聚合、重复举报、Case / Appeal / State 版本、操作键回放与冲突、六类目标限制与恢复、Chat 任务重试、申诉资格、通知隐私和 SQLite / PostgreSQL migration。

## 不做范围

- 不读取未举报私聊，不复制附件二进制，不扩大管理员数据可见范围。
- 不建设机器审核、风险评分、自动封禁、敏感词平台或推荐降权。
- 不提供二次申诉、多级仲裁、工单、举报撤回、催办、申诉附件或管理员对话。
- 不建立反射式通用治理适配器；六类真实目标保持明确、可测试的领域边界。
