# 内容治理系统说明

> **适用范围**：帖子、评论、轻回应、聊天消息、商品的用户举报，Console 案件处理，以及禁言 / 封禁当前状态
>
> **最后更新**：2026-07-21（Asia/Shanghai）
>
> **权威设计**：[内容治理案件、证据与动作一致性](/features/content-moderation-case-evidence-action-design)

## 系统定位

内容治理使用“举报事实 → 案件 → 证据 → 决定 → 动作 → 结果”的可追溯链路。它是人工治理系统，不是机器审核、敏感词平台、自动处罚或私聊浏览工具。

核心原则：

- 同一租户、同一目标在一个处理周期内只有一个开放案件，多名举报者共享案件结论，但彼此不可见。
- 举报时快照、当前复核证据和动作结果只追加，不覆盖历史依据。
- 决定、目标处置和用户限制是三个不同事实；决定成功不等于动作已经成功。
- 禁言 / 封禁的当前状态只读取 `UserModerationState`，不扫描旧动作流水猜测。
- Console 权限、目标领域 ACL 和租户边界分别校验，管理员身份不自动获得未举报私聊的浏览权。

## 角色与可见范围

| 角色 | 可以看到 | 不可看到 |
| --- | --- | --- |
| 举报者 | 自己的举报公开标识、目标摘要、提交时间、`Submitted / Resolved` 和精简结果 | 案件内部 ID、其他举报者、内部备注、证据正文、管理员身份、具体处罚 |
| 被处置用户 | 自己当前是否被禁言 / 封禁、有效截止时间和状态变化通知摘要 | 举报者身份、内部证据、其他举报内容、治理人员身份 |
| `console.moderation.view` | 案件队列、举报聚合、证据、事件和当前用户状态 | 决定或动作写入能力 |
| `console.moderation.review` | View 范围，加上追加复核证据、登记案件决定和目标处置 | 没有 Action 权限时不能提交用户动作或纠正动作 |
| `console.moderation.action` | 禁言 / 封禁 / 解除和结案后用户状态纠正 | 绕过 Review、目标 ACL、受保护账号或租户边界 |

## 举报者使用说明

登录用户可以从帖子、评论、轻回应、聊天消息或商品的既有举报入口提交原因和不超过 500 字的补充说明。

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
| `UserModerationAction` | 禁言、封禁、解除和迁移校准的不可变动作流水 |
| `UserModerationState` | 用户某类限制的唯一当前状态、截止时间和版本 |

证据目标状态使用 `Available / Deleted / Recalled / Disabled / Unavailable`。目标后来被编辑、删除、撤回或下架时，历史快照不会被当前内容覆盖。

## 决定、目标处置与动作

案件决定：

- `NoViolation`：只能保留目标，不允许附带用户动作。
- `InsufficientEvidence`：只能保留目标或记录目标不可用，不允许限制目标或用户。
- `Violation`：必须明确目标处置或用户动作，不能依赖空值猜测。

目标处置结果使用 `Keep / Restricted / Unavailable / ActionPending / ActionFailed`。其中：

- Post、Comment、PostQuickReply、Product 在 Main 事务内执行限制。
- Post / Comment 使用 `EditCount`，Product 使用 `Version` 校验目标版本；版本变化时拒绝覆盖。
- ChatMessage 通过 Main 可靠 Outbox 请求 Chat 库精确撤回；任务完成前案件保持处理中，不能提前显示“已采取措施”。

用户动作使用 `Mute / Ban / Unmute / Unban`：

- Mute / Ban 激活时可以设置 1–720 小时，空截止时间表示永久限制。
- Unmute / Unban 不允许携带持续时间。
- Ban 生效时会终止当前 Mute，但解除 Ban 不恢复旧 Mute。
- 自然到期由读取时按 UTC 实时派生，不修改历史动作流水。
- 不能对自己或受保护的 System / Admin 账号执行限制。

## 并发、幂等与失败恢复

- 案件写入提交 `expectedVersion`；版本冲突后必须刷新证据和目标状态，保留尚未提交的决定、备注和动作草稿。
- 用户动作提交 `expectedStateVersion`，防止两个治理人员覆盖同一类当前限制。
- 决定和纠正动作提交 8–160 字符的 `operationKey`；同键同参数回放原结果，同键异参返回冲突。
- Main 库动作失败时整个决定事务回滚；跨 Chat 动作失败保留失败事件和可重试任务。
- 通知失败不回滚已经生效的治理动作，由可靠任务按稳定业务键重试。
- 目标不存在、跨租户、来源举报与目标用户不匹配、目标版本变化和案件版本冲突均返回稳定 `Code / MessageKey`，调用方不得按展示文案分支。

## 通知与隐私

治理通知属于 `Governance` 分类：

- `ContentReportResolved`：普通优先级，发送给案件内各举报者，只包含精简 `resultCode`。
- `UserModerationChanged`：高优先级，发送给被处置用户，只包含动作类型摘要。

两类通知当前使用 `TargetKind=None`，不得构造 Console 案件链接，也不得在通知正文泄露案件、举报者、内部证据、内部备注或管理员身份。账号当前限制和截止时间应通过 `GetMyModerationStatus / GetMyPublishPermission` 重新读取，不能把通知正文当作状态真相源。

## API 与权限

用户侧：

- `POST Report`
- `GET GetMyReports / GetMyReport/{reportPublicId}`
- `GET GetMyModerationStatus / GetMyPublishPermission`

Console：

- View：`GetCaseQueue / GetCase/{casePublicId} / GetCaseEvents`
- Review：`CaptureEvidence / ReviewCase`，包括案件决定和目标处置
- Action：带用户动作的 `ReviewCase`、`ApplyCorrectiveAction`；不替代 Review

`GetReviewQueue / Review / ApplyUserAction / GetActionLogs` 是旧消费者兼容入口。旧写入口也复用 `UserModerationState`，不得形成第二套当前状态；新增页面和客户端只使用 Case API。

LongId 在 HTTP 和 TypeScript 边界保持字符串。前端共用契约位于 `Frontend/radish.http/src/content-moderation-contract.ts`，不得另建 fetch / axios 封装。

## 数据库与迁移

Main migration `20260721_008_content_moderation_case`：

- 创建 Case / Evidence / Event / UserModerationState，并扩展举报和动作字段。
- 将历史 Pending 举报按目标聚合，历史终态保持可解释的独立案件。
- 把历史举报快照、决定和有效用户动作映射为追加式证据、事件和唯一当前状态。
- `doctor / verify` 检查非法枚举、时间、来源错配、开放案件唯一性、序号、状态和业务键漂移。

已有数据库必须按 `doctor → 备份 → apply → verify` 前滚；不得依赖 API 启动或 Code First 静默补结构。详细规则见[数据库结构变更协作口径](/guide/database-schema-change-governance)。

## 实现入口与验证

- Controller：`Radish.Api/Controllers/ContentModerationController.cs`
- Service：`Radish.Service/ContentModerationService.cs`、`ContentModerationService.Cases.cs`
- Repository：`Radish.Repository/ContentModerationCaseRepository*.cs`
- Migration：`Radish.DbMigrate/ContentModerationCaseSchemaMigration.cs`
- HTTP 示例：`Radish.Api.Tests/HttpTest/Radish.Api.Community.http`
- Repository / migration 测试：`Radish.Api.Tests/Repositories/ContentModerationCase*Test.cs`

修改治理契约时，至少覆盖案件聚合、重复举报、Case / State 版本、操作键回放与冲突、五类目标处置、Chat 任务重试、通知隐私、旧入口一致状态和 SQLite / PostgreSQL migration。

## 不做范围

- 不读取未举报私聊，不复制附件二进制，不扩大管理员数据可见范围。
- 不建设机器审核、风险评分、自动封禁、敏感词平台或推荐降权。
- 不提供申诉、工单、举报撤回、催办或管理员对话；这些需要独立产品与权限设计。
- 不建立反射式通用治理适配器；五类真实目标保持明确、可测试的领域边界。
