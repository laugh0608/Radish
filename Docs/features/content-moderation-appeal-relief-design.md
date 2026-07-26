# F4-J 内容治理申诉与处置纠正

> **状态**：F4-J-A / B / C / D 已完成；专题已关闭
>
> **复核日期**：2026-07-25（Asia/Shanghai）
>
> **适用范围**：正式 Web 被处置用户申诉入口、Main 内容治理案件与五类目标、Console `/moderation`；WebOS 只复用现有治理通知与正式 Web 跳转，Flutter 不新增治理页面
>
> **前置专题**：[F4-I 内容治理案件、证据与动作一致性](/features/content-moderation-case-evidence-action-design) · [内容治理系统说明](/guide/content-moderation) · [通知中心深化与通知治理](/features/notification-center-deepening)

## 一、结论摘要

F4-J 选择“内容治理申诉与处置纠正”为当前唯一完整功能专题。它在现有“举报 → 案件 → 证据 → 决定 → 动作 → 结果”之后增加独立的“决定通知 → 申诉 → 复核 → 纠正动作 → 最终结果”，但不改写原案件、原举报或历史动作。

核心裁决如下：

1. 被处置用户通过 `/me/appeals` 查看与自己有关的精简决定通知，并对仍在期限内的决定提交一次申诉；举报者不能代替被处置者申诉。
2. `ContentModerationAppeal` 是独立聚合，不把已结案件重新改成 `Reviewing`，也不覆盖 `ContentModerationCase.Decision / PublicResultCode`。
3. 申诉复核支持 `Upheld / PartiallyGranted / Granted`，部分采纳必须明确选择内容恢复、解除禁言或解除封禁中的具体纠正范围。
4. “采纳申诉”只有在所选纠正动作已经成功、已被后续状态替代或确认无需执行后才能进入终态；动作失败不能显示为已经纠正。
5. 新增 `ContentModerationTargetAction` 记录五类目标限制与恢复的来源、版本、幂等键和结果；原领域对象保留当前限制来源标记，避免申诉恢复覆盖作者、商品管理员或后续治理的新变化。
6. Post、Comment、PostQuickReply、Product 在 Main 事务内按明确领域规则恢复；ChatMessage 通过可靠 Outbox 在 Chat 库恢复正文可见性、搜索投影以及由同一治理动作移除的 Reaction / Pin。
7. 用户限制只在当前 `UserModerationState.SourceCaseId` 仍指向原案件时解除；后续独立限制、自然到期或人工纠正不会被申诉动作反向覆盖。
8. 新增独立 `console.moderation.appeal` 权限负责复核，既有 `console.moderation.action` 负责执行纠正动作；首批不强制双人审批，但所有决定和动作必须追加留痕。
9. 申诉结果只通知申诉人，不向举报者披露申诉存在、正文、复核人员或内部证据。
10. 本专题不建设举报撤回、催办、附件上传、管理员对话、多级仲裁、法务工单、SLA 或自动判定平台。

## 二、F4-J-A 候选复核与专题裁决

### 2.1 同口径审计结果

| 候选 | 已有基础 | 真实缺口 | 边界与风险 | 裁决 |
| --- | --- | --- | --- | --- |
| 治理申诉与处置纠正 | F4-I 已有案件、证据、决定、五类目标处置、用户状态、可靠通知和正式工作台 | 被处置者只有限制状态与通知，没有决定收件、申诉、独立复核和目标恢复路径；现有纠正动作只能处理用户状态 | 可沿现有 Case、TargetUser、权限、Outbox 和五类适配器纵向完成；需要补对称恢复与来源留痕 | **选定为 F4-J** |
| 圈子全局屏蔽与关系隐私 | `/circle` 已有关注流、关注 / 粉丝列表、公开主页关系动作和可靠通知；Direct 只有会话内阻断 | 缺少跨关注、公开主页、内容分发、通知与私聊的一致屏蔽政策 | 需要先定义全局屏蔽对既有关系、历史内容、互相关注、消息请求和通知的完整矩阵，影响面跨多个域 | 后置为独立关系政策专题 |
| 公开聊天入口 | 已有授权后的 Public / Announcement / Private / Direct 频道、搜索、Reaction、Pin、阅读回执和 `/messages` | 没有匿名或公开 Web `/chat` 产品入口 | 与 `/messages` 重叠，并新增匿名 ACL、限流、索引、SEO、实时连接和滥用治理；当前增量价值不足以覆盖新暴露面 | 后置，不作为现有聊天完成项 |
| 论坛作者回滚与扩展编辑 | Post / Comment 已有编辑历史、可靠提交键、历史查询和正式页面回看 | 没有版本回滚、子评论 / 回答扩展编辑和完整编辑器 | 回滚会改写当前公开内容并影响附件、回复关系和审计；其余缺口比治理申诉更窄 | 后置为作者工具专题 |

### 2.2 为什么申诉优先

F4-I 已经让治理人员可以汇总举报、保存证据、登记决定并真实限制内容或用户，但被处置用户仍只能看到当前禁言 / 封禁状态和一条不带案件导航的通知。系统能执行高权限动作，却没有对称的正式复核与纠错路径，这是信任治理主轴尚未关闭的用户路径缺口。

代码审计同时确认：

- Post、Comment、PostQuickReply 会被直接软删除。
- Product 会被直接下架并递增版本。
- ChatMessage 会被软删除、清空搜索文本，并联动软删除 Reaction 与 Pin。
- 现有 `ApplyCorrectiveAction` 只支持 `Unmute / Unban`，不能恢复目标内容。
- 现有案件事件能说明决定与动作结果，但没有独立目标动作流水，也没有可证明“当前不可用状态仍由哪次治理动作造成”的字段。

因此，F4-J 不能只增加申诉表单和审核结论。完整专题必须同时建立决定通知、申诉聚合、独立权限、目标动作来源、五类恢复、用户状态纠正、跨库失败恢复与正式页面。

圈子全局屏蔽同样有长期价值，但它不是给现有 `UserFollow` 增加一个布尔值：屏蔽后是否自动取消双向关注、是否隐藏历史内容、是否拒绝 Direct 请求、是否抑制通知、公开主页显示什么，都需要先形成新的关系政策。该候选不与本专题并行。

## 三、目标与非目标

### 3.1 目标

- 被处置用户能看到与本人有关且经过隐私裁剪的决定通知、申诉资格、截止时间和当前纠正结果。
- 每个案件对目标用户只接受一次申诉，重复请求、并发提交和重复动作不会制造两份当前事实。
- 原案件、举报结果、原决定、原动作和原证据保持不可变；申诉以独立聚合解释后续复核与纠正。
- 申诉审核人员可以维持原决定、全部采纳或按目标内容 / Mute / Ban 范围部分采纳。
- 五类目标恢复只撤销由原案件实际造成且尚未被后续变化替代的处置，不覆盖作者或其他管理人员的新操作。
- Chat 跨库恢复具备可靠任务、幂等消费、失败重试和终态回写，不能提前宣称成功。
- 用户限制纠正继续复用 `UserModerationState` 的版本与当前来源，不建立第二套发布权限真相源。
- 申诉人、举报者、只读治理员、申诉审核员和动作执行员只看到各自必要信息。
- 正式 Web、Console、通知中心、中英文、PC / mobile 和多主题形成可成组验收的路径。
- SQLite / PostgreSQL 的历史迁移、doctor、verify、重入与恢复演练能够解释现有 F4-I 案件和处置。

### 3.2 非目标

- 不允许举报者对 `NoViolation / InsufficientEvidence` 提交“反向申诉”或要求加重处罚。
- 不提供举报撤回、催办、追加附件、实名材料、管理员私聊或公开争议区。
- 不做二次申诉、多级仲裁、法务工单、SLA、跨团队审批或外部监管接口。
- 不做机器复核、风险评分、自动采纳、自动恢复或模型训练数据采集。
- 不把申诉扩展为账号注销、资产冻结、订单退款、权益补偿或声誉赔偿。
- 不恢复由作者本人、商品管理员、其他案件或后续业务操作造成的不可用状态。
- 不改造关系屏蔽、公开聊天、论坛版本回滚、推荐或生产行为采集。
- 不新增 Flutter、Tauri 或独立移动 Console 实现。

## 四、角色与用户路径

### 4.1 被处置用户

1. 用户收到治理状态变化通知，或从 `/me` 进入 `/me/appeals`。
2. 页面读取本人决定通知，只显示目标摘要、决定时间、精简原因、实际处置范围、申诉期限和是否已申诉。
3. 用户选择一条可申诉决定，填写 20–1000 字申诉陈述并确认提交；首批不上传附件。
4. 提交成功后获得 `apl_...` 公开标识，并看到 `Submitted / Reviewing / ReliefPending / ReliefFailed / Resolved / Withdrawn`。
5. 在审核决定登记前，用户可撤回申诉；撤回后不能针对同一案件重新提交。
6. 审核完成后，用户看到 `Upheld / PartiallyGranted / Granted`、公开说明、各纠正项结果与完成时间。

页面不得向用户暴露举报者、举报数量、证据正文、内部备注、审核人员、动作执行人员或其他案件。

### 4.2 申诉审核人员

1. 在 Console `/moderation` 的“申诉”视图按状态、目标类型、提交时间和纠正结果筛选。
2. 打开申诉后查看原案件的受权证据、原决定、原动作、申诉陈述和目标当前状态。
3. 如有必要，使用既有证据采集能力追加“申诉复核快照”，不得覆盖原证据。
4. 提交 `Upheld / PartiallyGranted / Granted` 和对用户可见的结果说明；部分采纳必须明确所选纠正范围。
5. 需要纠正动作时，申诉进入 `ReliefPending`，不能直接显示 `Resolved`。

`CaptureAppealEvidence` 只向原 Case 追加带 `AppealId` 的证据，并推进 Appeal 版本与事件；不得调用原案件的接案状态转换，也不得把已结 Case 改回 `Reviewing`。

### 4.3 动作执行人员

1. 读取已经采纳且等待执行的纠正项。
2. 复核当前目标来源标记、目标版本、用户状态版本和后续动作。
3. 使用稳定 `operationKey` 执行；Main 动作在事务内完成，Chat 动作进入可靠 Outbox。
4. 对 `TargetChanged / Superseded / AlreadyInactive` 等结果记录明确终态，不使用强制覆盖或默认成功。
5. 失败项可按同一参数和业务键安全重放；变更纠正范围必须回到申诉审核重新登记新版本。

### 4.4 举报者

- 举报者的 `/me/reports` 继续表示举报提交和当时结案结果，不显示目标用户是否申诉。
- 申诉、陈述、审核结果和纠正动作不向举报者通知。
- 原 `MeasuresTaken` 表示案件结案时确实采取过措施，不因后续申诉静默改写历史收件。

## 五、权威对象与不变量

| 对象 | 权威职责 | 不承担 |
| --- | --- | --- |
| `ContentModerationCase` | 原处理周期、原决定、原公开结果和原结案事实 | 申诉当前状态、申诉最终结果 |
| `ContentModerationAppeal` | 一次申诉的陈述、状态、版本、复核结果、所选纠正范围和公开说明 | 原案件证据、目标当前领域状态 |
| `ContentModerationAppealEvent` | 申诉提交、接手、决定、纠正请求 / 结果、撤回和通知的追加式时间线 | 当前状态的重复缓存 |
| `ContentModerationEvidence` | 原案件和申诉复核期间追加的安全快照 | 用户上传附件、可编辑申诉正文 |
| `ContentModerationTargetAction` | 目标 Restrict / Restore 的来源、前后版本、幂等键、执行状态与结果 | 原领域对象当前可见性 |
| `UserModerationAction` | Mute / Ban / Unmute / Unban 的不可变动作流水及 Case / Appeal 来源 | 用户当前是否受限 |
| `UserModerationState` | Mute / Ban 维度的唯一当前状态、版本与当前来源 | 申诉结果 |
| 原领域对象 | 内容当前是否可见 / 可售及当前治理来源标记 | 案件、申诉或内部证据 |

必须长期保持以下不变量：

1. 原案件结案后不因申诉重新开放，也不覆盖 `Decision / TargetDisposition / PublicResultCode / ResolvedBy*`。
2. 同一租户、同一案件、同一目标用户最多一份申诉。
3. 申诉状态只由申诉 Repository 在事务和 CAS 下推进。
4. 目标恢复必须引用一条实际改变目标状态的 `Restrict` 动作。
5. 原领域对象上的治理来源标记必须与待恢复的 Restrict 动作一致；不一致即视为后续状态已经替代。
6. 申诉采纳不等于纠正成功；所有选定纠正项终态后才能形成最终结果。
7. 通知、前端缓存和 Case Event 都不能成为申诉或目标当前状态真相源。

## 六、数据模型

### 6.1 `ContentModerationAppeal`

首批字段：

- `TenantId / CaseId / AppellantUserId`
- `PublicId`：`apl_` + UUIDv7，不向用户暴露 LongId
- `Status`：`Submitted / Reviewing / ReliefPending / ReliefFailed / Resolved / Withdrawn`
- `Outcome`：`None / Upheld / PartiallyGranted / Granted`
- `EligibleScopeSnapshot`：提交时由服务端固化的 `TargetContent / Mute / Ban` 可复核范围
- `GrantedScope`：审核采纳范围；`Upheld` 必须为空，`Granted` 必须覆盖全部 `EligibleScopeSnapshot`
- `Statement`：20–1000 字不可变申诉陈述
- `PublicResultCode / PublicResultSummary`
- `InternalRemark`
- `Version`
- `SubmissionOperationKey / DecisionOperationKey / WithdrawalOperationKey`
- `EligibleUntilUtc / SubmittedAt / ReviewedAt / ResolvedAt / WithdrawnAt`
- `ReviewedById / ReviewedByName`
- 创建、修改审计字段

唯一约束：

```text
(TenantId, CaseId, AppellantUserId) UNIQUE
(TenantId, PublicId) UNIQUE
(TenantId, SubmissionOperationKey) UNIQUE
(TenantId, DecisionOperationKey) UNIQUE
(TenantId, WithdrawalOperationKey) UNIQUE
```

申诉正文创建后不提供 Update API。用户补充说明、附件和管理员对话不通过修改 `Statement` 变相实现。

### 6.2 `ContentModerationAppealEvent`

字段至少包含：

- `TenantId / AppealId / EventSequence`
- `EventType`：`Submitted / ReviewStarted / EvidenceCaptured / DecisionRecorded / ReliefRequested / ReliefSucceeded / ReliefSuperseded / ReliefFailed / Retried / Withdrawn / NotificationQueued`
- `ExpectedAppealVersion / ResultAppealVersion`
- `RelatedEvidenceId / RelatedTargetActionId / RelatedUserActionId`
- `FromStatus / ToStatus / ResultCode / Remark`
- `ActorUserId / ActorName / CreateTime`

`EventSequence` 在申诉内单调递增。事件正文不复制申诉陈述、完整证据或通知 payload。

### 6.3 `ContentModerationTargetAction`

该对象补齐 F4-I 没有独立记录目标处置动作的缺口：

- `TenantId / CaseId / AppealId?`
- `TargetType / TargetContentId / TargetUserId`
- `ActionType`：`Restrict / Restore`
- `SourceTargetActionId?`：Restore 必须指向原 Restrict
- `OperationKey`
- `Status`：`Pending / Succeeded / Failed / Superseded / NoEffect`
- `ExpectedTargetVersion / ResultTargetVersion`
- `ChangedTargetState`
- `ResultCode`
- `RequestedAt / CompletedAt`
- `OperatorUserId / OperatorName`
- 创建审计字段

唯一约束：

```text
(TenantId, OperationKey) UNIQUE
```

同键同参数返回原结果，同键异参返回 `409`。`NoEffect` 表示原动作没有造成可恢复变化，例如目标在原决定前已由其他原因不可用；它不是失败，也不能授权恢复别人的删除。

### 6.4 既有对象调整

- `ContentModerationEvidence` 增加可空 `AppealId`，申诉复核快照仍归属原 Case，并可精确关联申诉。
- `ContentModerationCaseEvent` 增加可空 `RelatedAppealId`，只记录 `AppealSubmitted / AppealResolved` 边界事件，不复制完整申诉时间线。
- `UserModerationAction` 增加可空 `AppealId`，说明解除动作来源；既有 `CaseId` 继续指向原案件。
- Post、Comment、PostQuickReply、Product、ChannelMessage 增加可空 `ModerationTargetActionId`，表示当前不可用状态由哪条治理目标动作造成。
- ChatMessageReaction、ChatMessagePin 增加可空 `ModerationTargetActionId`，只标记由同一次消息治理动作联动移除的行。

领域侧任何非治理写操作只要改变目标可见 / 可售状态，就必须同时清除旧 `ModerationTargetActionId`。不能保留一个已经不再代表当前状态的来源标记。

## 七、资格、状态与决定

### 7.1 申诉资格

服务端只接受同时满足以下条件的请求：

- 当前用户等于 `ContentModerationCase.TargetUserId`。
- 案件已经 `Resolved`，决定为 `Violation`。
- 案件结案时至少存在目标限制或与该案件关联的 Mute / Ban。
- 当前时间不晚于 `ResolvedAt + 30 days`。
- 同案同目标用户不存在历史申诉。
- 用户仍处于原租户且身份有效。

目标已经被后续修改、用户限制已经到期或被新动作替代，不阻止提交申诉；这些事实影响纠正结果，但不剥夺对原决定的复核权。

`GetMyAppealableDecisions` 返回服务端派生的 `voEligibleUntilUtc / voEligibleScope / voCanAppeal / voIneligibleReason`。前端不得自行按本地时间或目标状态猜测资格；目标只返回从案件证据裁剪的安全标题 / 摘要，不返回目标 LongId 或由前端拼接详情路径。

### 7.2 状态转换

```text
Submitted -> Reviewing -> Resolved(Upheld)
    |            |
    |            +-> ReliefPending -> Resolved(PartiallyGranted | Granted)
    |                              \-> ReliefFailed -> ReliefPending
    +-> Withdrawn

Reviewing -> Withdrawn
```

- `Withdrawn` 只允许发生在决定登记前；与审核 CAS 竞争时只有一个请求成功。
- `ReliefFailed` 保留已成功的纠正项和失败项，不回滚已经安全完成的恢复。
- 重试只推进失败项，不能重复执行已成功、已替代或无需执行的项。
- `Resolved` 和 `Withdrawn` 为终态；首批不允许重开或再次申诉。

### 7.3 复核结果

- `Upheld`：维持原决定，`GrantedScope` 必须为空，直接进入 `Resolved`。
- `Granted`：提交时固化的全部 `EligibleScopeSnapshot` 均被采纳；存在实际纠正项时先进入 `ReliefPending`。
- `PartiallyGranted`：至少采纳一项且少于全部可纠正范围，必须逐项说明。

审核结果不提供任意“自由组合状态”。`GrantedScope` 只允许从服务端返回的 `EligibleScopes` 中选择，不能请求恢复另一个目标、解除另一个案件或修改举报结果。

## 八、处置来源与纠正矩阵

### 8.1 通用规则

目标 Restrict 动作执行前创建 `ContentModerationTargetAction`，原领域写入与动作结果在同一事务提交；Chat 先写 `Pending` 和 Outbox，消费者完成后回写结果。动作实际改变状态时，把其 ID 写入目标的 `ModerationTargetActionId`。

恢复前必须同时满足：

1. Restore 指向的 Restrict 属于同一 Case。
2. Restrict 的 `ChangedTargetState = true` 且结果为成功。
3. 当前目标 `ModerationTargetActionId` 仍等于 Restrict ID。
4. 当前目标版本和领域前置条件满足。
5. Restore `operationKey` 未被其他参数使用。

不满足第 3 或第 4 条时返回 `Superseded / TargetChanged / DomainRuleConflict`，不得强制覆盖。

### 8.2 五类目标

| 目标 | Restrict 来源 | Restore 行为 | 冲突保护 |
| --- | --- | --- | --- |
| Post | `IsDeleted: false -> true` | 恢复 `IsDeleted=false`，清空治理删除字段与来源标记 | Restrict ID、`EditCount`、删除审计与父级 / 作者状态 |
| Comment | `IsDeleted: false -> true` | 恢复 `IsDeleted=false`，清空来源标记并更新修改审计 | Restrict ID、`EditCount`、所属 Post 可用性 |
| PostQuickReply | `IsDeleted: false -> true` | 恢复 `IsDeleted=false`，清空删除字段与来源标记 | Restrict ID、所属 Post 可用性、当前状态 |
| Product | `IsOnSale: true -> false` | 经商品能力、启用状态和库存规则复核后重新上架并递增 `Version` | Restrict ID、当前 `Version`、能力矩阵与商品状态 |
| ChatMessage | 软删除消息并移除搜索、Reaction、Pin | 恢复消息，重建 `SearchText`；只恢复同一 Restrict 标记的 Reaction / Pin，递增相关 revision 并发布完整失效 / 快照事件 | Restrict ID、频道 / 成员有效性、消息未被后续动作替代、Chat 幂等任务 |

Chat 恢复不通过 Main 直接连接 Chat 库。可靠任务必须包含 `TenantId / AppealId / TargetActionId / SourceTargetActionId / MessageId / OperationKey / Operator`，消费者按 Chat 事务完成消息、Reaction、Pin、搜索投影和 revision 后，再由 Main 回写申诉状态。

### 8.3 用户限制

- Mute / Ban 纠正继续写 `UserModerationAction(Unmute / Unban)`，并使用 `expectedStateVersion + operationKey`。
- 只有当前 `UserModerationState.SourceCaseId` 等于原 Case 且策略仍有效时才解除。
- 当前状态已自然到期时记录 `AlreadyInactive`。
- 当前状态已由其他 Case 或手工动作替代时记录 `Superseded`，不能解除后续限制。
- Ban 曾使 Mute 失效时，解除 Ban 不恢复旧 Mute，延续 F4-I 当前状态语义。
- 申诉动作的 `AppealId` 必须写入动作流水和事件，不能伪装为普通人工纠正。

### 8.4 部分成功

多个纠正项独立记录终态：

- `Succeeded`：该项真实改变当前状态。
- `NoEffect`：原 Case 没有造成该项变化，或状态已经自然失效。
- `Superseded`：后续合法状态已经替代原处置。
- `Failed`：基础设施、迁移、领域规则或可靠任务尚未完成，需要处理或重试。

`Succeeded / NoEffect / Superseded` 都是可收口结果；只有 `Failed` 阻止申诉进入 `Resolved`。公开页面使用稳定、隐私安全的说明，不展示内部异常、表名、操作人或后续案件。

## 九、API 与权限

### 9.1 用户侧 API

- `GET /api/v1/ContentModeration/GetMyAppealableDecisions`
- `GET /api/v1/ContentModeration/GetMyAppeals`
- `GET /api/v1/ContentModeration/GetMyAppeal/{appealPublicId}`
- `POST /api/v1/ContentModeration/SubmitAppeal`
- `POST /api/v1/ContentModeration/WithdrawAppeal`

决定通知和申诉 VO 只返回：

- Case / Appeal 的公开标识
- 目标类型与安全摘要
- 原决定时间、精简结果和实际影响范围
- 申诉资格、截止时间和状态
- 申诉人自己的陈述
- 公开复核结果、纠正项摘要和时间

不返回 LongId、举报者、证据正文、内部备注、操作员、用户动作的内部原因或其他 Case。

Mute / Ban 及其纠正结果通过专用公开摘要返回，只包含动作类型、稳定结果码和时间；不得复用包含动作 ID、目标用户 ID、内部原因或操作员的 Console `UserModerationActionVo`。

### 9.2 Console API

View：

- `GET GetAppealQueue`

Appeal：

- `GET GetAppeal/{appealPublicId}`
- `GET GetAppealEvents`
- `POST StartAppealReview`
- `POST CaptureAppealEvidence`
- `POST ReviewAppeal`

Action：

- `POST ExecuteAppealRelief`

`GetAppealQueue` 只返回调度所需的公开标识、状态、范围、版本与时间，不返回 `Statement / InternalRemark / Events / TargetActions / UserActions`。`ExecuteAppealRelief` 只返回独立 `ContentModerationAppealActionResultVo`，不得通过写入响应向 Action-only 角色泄露申诉正文或内部复核信息；同时具备 Appeal 权限的客户端执行后重新读取完整详情。重试纠正复用该入口的版本与幂等契约，不维护第二个 Retry HTTP 入口。

所有 LongId 在 HTTP / TypeScript 边界保持字符串。前端契约继续放在 `@radish/http` 的内容治理模块，不新增 fetch / axios 封装。

### 9.3 权限

| 权限 | 能力 |
| --- | --- |
| `console.moderation.view` | 查看原案件和已脱敏申诉摘要 |
| `console.moderation.appeal` | 查看完整申诉陈述、追加申诉复核证据、登记复核结果 |
| `console.moderation.action` | 执行和重试已经获准的目标 / 用户纠正动作 |

`review` 不自动包含 `appeal`，`appeal` 也不自动包含 `action`。角色种子可以同时授予多个权限，但 API 每次独立校验。

考虑小规模独立部署可能只有一名治理人员，首批不强制“原审核人与申诉审核人必须不同”。如果同一人员处理，Console 明确提示并在事件中完整记录；后续双人复核属于独立治理政策，不在本专题暗中增加不可满足的门槛。

Console mobile 只提供队列、详情和事件查看；申诉决定与纠正动作只在 PC 工作台开放。普通用户的 `/me/appeals` 在 PC / mobile 均支持提交和撤回。

## 十、隐私、通知与导航

- 新增 `ContentModerationDecisionAvailable` 治理通知：案件以 `Violation` 结案且真实限制目标或用户后发送给目标用户，导航到 `/me/appeals?case=...`。Chat 案件必须等跨库目标动作完成并正式结案后再发送。
- 新增 `ContentModerationAppealUpdated` 治理通知，只发送给申诉人。
- 新增结构化 `GovernanceDecision / GovernanceAppeal` 目标，分别持久化 `CasePublicId / AppealPublicId`；不把 Case LongId 或 Appeal LongId 放进客户端 payload。
- 通知模板只包含 `appealStatus / publicResultCode` 等稳定参数，不包含申诉正文、案件 LongId、举报者或操作员。
- 历史 Case 由 `/me/appeals` 权威列表提供，不在 migration 中批量补发旧决定通知。
- 申诉提交不向举报者发通知，也不改变举报者通知偏好或历史结果。
- Console 队列依靠权威查询，不向所有治理角色广播包含用户陈述的通知正文。
- `/me/appeals` 是登录私域路由，不进入 sitemap、canonical、JSON-LD、浏览器持久化或跨账号缓存。
- 申诉详情和证据不进入通知摘要、客户端日志、URL 查询正文或跨标签广播；跨标签只广播“需要刷新”的稳定 revision。

## 十一、并发、幂等与失败恢复

- 申诉提交依靠数据库唯一约束，不使用“先查后插”防重复。
- Submit / Withdraw / Review / Execute / Retry 都提交 8–160 字符 `operationKey`。
- 申诉状态写入提交 `expectedAppealVersion`；冲突后保留前端陈述或审核草稿，要求刷新。
- 目标动作同时校验 `expectedAppealVersion / expectedTargetVersion / sourceTargetActionId`。
- 用户动作同时校验 `expectedStateVersion`。
- Main 内申诉、事件、目标动作、用户动作、当前状态和 Outbox 在明确事务边界提交。
- Chat 失败时申诉保持 `ReliefFailed`，失败动作与 Outbox 可追踪；重复消费者不得重复恢复 Reaction / Pin 或重复发送通知。
- 通知失败不回滚已经完成的纠正动作，由可靠通知任务按 `AppealId + final version` 重试。
- 未知异常进入统一安全错误边界；服务端返回稳定 `Code / MessageKey / TraceId`，不向用户返回原始异常。
- 目标不存在、来源标记不匹配、领域规则变化、跨租户、已撤回、超期、版本冲突和同键异参都必须有独立错误或动作结果码。

## 十二、迁移与历史兼容

F4-J-B 增加 Main 与 Chat schema ledger migration，计划使用连续迁移标识，不依赖 API 启动 Code First 补结构。

Main migration：

1. 创建 Appeal、AppealEvent、TargetAction。
2. 扩展 Evidence、CaseEvent、UserModerationAction 和四类 Main 目标来源字段。
3. 从已结 F4-I Case Event 回填 Restrict 动作：`Restricted` 只在领域审计字段与案件动作时间、目标和操作人一致时标记 `ChangedTargetState=true`。
4. `AlreadyRestricted / Unavailable / Kept` 回填为 `NoEffect`，不得把既有删除冒认为该案件造成。
5. 对无法解释却声称 `Restricted` 的历史行由 doctor 阻断，要求先查明事实，不静默猜测来源。
6. 建立 Appeal 唯一约束、动作业务键唯一约束和队列 / 事件索引。

Chat migration：

1. 为 ChannelMessage、Reaction、Pin 增加治理动作来源字段。
2. 对能以 Case、Message、删除时间和治理结果唯一证明的历史 Recall 回填来源。
3. 无法唯一证明的历史删除保持无来源，不允许申诉恢复。
4. verify 检查来源动作存在、目标一致、软删除状态一致以及子对象来源不越过消息来源。

空库、现有 SQLite、隔离 PostgreSQL 17、重入、备份恢复和跨库失败恢复都必须覆盖。迁移只建立可证明的事实，不补造申诉、不重开案件、不恢复内容。

## 十三、Pencil 与正式页面

### 13.1 `/me/appeals`

- 进入既有 `/me` 信息架构，与 `/me/reports` 分开，避免混淆举报者与被处置者角色。
- PC 使用决定列表 + 申诉详情；mobile 使用单列卡片、全屏陈述输入和底部确认动作。
- 页面显示资格、剩余时间、实际影响范围、申诉状态、公开结果和逐项纠正摘要。
- 超期、已提交、目标变化、状态已失效和通知深链都必须有明确状态，不隐藏列表项。
- 用户陈述在提交前只存组件内存；刷新前提示，提交后以服务端不可变正文为准。

### 13.2 Console `/moderation`

- 继续更新既有 `P02 / P07` 设计源，不新建平行治理应用。
- PC 增加“案件 / 申诉”视图切换；申诉详情按原决定、申诉陈述、复核证据、结果、纠正动作、留痕排列。
- 页面明确区分“复核已登记”“纠正执行中”“部分完成”“执行失败”“已完成”。
- 没有 Appeal 权限时不显示申诉正文与决定表单；没有 Action 权限时只显示待执行摘要。
- 冲突刷新保留内部备注、公开说明和所选纠正范围草稿。
- mobile 只读展示，不复制 PC 高风险决定和动作表单。

F4-J-C 必须先更新 Pencil，再实现正式页面。F4-J-A / B 不提前修改页面布局。

## 十四、容量与性能

- 本人决定 / 申诉按 `(TenantId, AppellantUserId, CreateTime DESC, Id DESC)` 稳定分页。
- Console 队列按 `(TenantId, Status, ModifyTime DESC, Id DESC)` 查询，不将全表载入内存再筛选。
- Appeal Event 按 `(TenantId, AppealId, EventSequence)` 唯一排序。
- Target Action 按 Case / Appeal 和时间建立索引，操作键租户内唯一。
- 列表批量装配目标摘要、纠正进度和用户公开信息，禁止逐行查询五类领域对象。
- 单份申诉首批最多返回 100 条关联证据、200 条事件和 20 条动作；超过后分别分页，不截断数据库事实。
- 申诉正文、内部备注和证据不进入分布式缓存；本人列表只缓存可安全失效的精简摘要。

## 十五、开发批次

### F4-J-A：候选审计与权威设计（已完成）

- 交叉复核治理申诉、圈子全局屏蔽、公开聊天和论坛作者回滚。
- 选定治理申诉与处置纠正为唯一专题，记录其余候选后置原因。
- 固定对象、状态、恢复矩阵、权限、API、迁移、页面、通知、失败恢复、验证和停止线。
- 本批不修改业务代码、migration、Pencil 或正式页面。

### F4-J-B：服务端权威契约（已完成）

- 完成 Main / Chat migration、历史来源回填、doctor / verify 和双数据库回归。
- 增加 Appeal / Event / TargetAction 专属 Repository，禁止 Service 直接访问 Db。
- 落地本人决定通知、提交 / 撤回、独立复核、部分采纳、五类目标恢复和用户状态纠正。
- 建立 Chat 恢复可靠任务、完整幂等、失败回写和通知。
- 增加权限种子、稳定错误、HTTP 示例、`@radish/http` 契约和服务端测试。
- 按停止线不修改 Pencil 或正式页面。

### F4-J-C：Pencil 与正式页面（已完成）

- 更新 `/me/appeals` PC / mobile 设计源和 Console `P02 / P07` 申诉工作台。
- 实现本人决定 / 申诉页、通知深链、提交 / 撤回、状态与纠正结果。
- 在 `/moderation` 增加申诉队列、详情、复核和 PC 动作执行。
- 完成权限裁剪、冲突草稿、中英文、键盘、无障碍和四主题适配。
- 按停止线不执行完整 Gateway 成组验收。

### F4-J-D：成组验收与专题关闭（已完成）

- 使用申诉人、举报者、原审核员、申诉审核员、动作执行员和只读治理员覆盖权限矩阵。
- 覆盖五类目标、Mute / Ban、全采纳 / 部分采纳 / 维持、撤回 / 超期、并发和幂等。
- 覆盖目标被后续修改、状态自然到期、后续案件替代、Main 回滚、Chat 失败 / 重试和通知失败。
- 覆盖 `zh / en × PC / mobile`、深链、Back / Forward、长文本、键盘、无障碍和四主题代表矩阵。
- 清理临时申诉、动作、通知、账号和来源标记，检查相关数据库完整性并执行严格 migration verify。

完成结论见 [F4-J-D 成组验收记录](/records/f4-j-d-content-moderation-appeal-stage-acceptance-2026-07-25)。
运行态代表矩阵覆盖六角色、五类目标、全采纳 / 部分采纳 / 维持 / 撤回 / 超期、
重复申诉、Chat 纠正失败恢复与双语响应式正式路径；后续状态替代、自然到期、事务回滚、
通知重放和 PostgreSQL 边界继续由仓储、服务与 migration 定向测试承担。

## 十六、验证矩阵

| 层级 | 必须覆盖 |
| --- | --- |
| Model / migration | 空库、F4-I 历史库、重入、备份恢复、唯一申诉、来源回填、不可解释历史 doctor |
| Repository | 并发提交、Appeal CAS、事件序号、TargetAction 幂等、部分动作状态聚合、Main 事务回滚 |
| Service | 本人资格、30 天期限、一次申诉、范围校验、撤回竞争、全部 / 部分采纳、原案件不可变 |
| 目标适配器 | 五类 Restrict / Restore、来源不匹配、版本变化、重复恢复、领域规则冲突、Chat 子对象恢复 |
| 用户状态 | Mute / Ban 当前来源、自然到期、后续动作替代、Unmute / Unban CAS 和动作流水 |
| API / 权限 | 本人裁剪、View / Appeal / Action 分离、LongId、403 / 404 / 409、稳定 Code / MessageKey |
| Outbox / 通知 | Chat 失败 / 重试、重复消费、最终状态回写、申诉人通知、举报者不通知 |
| Client | 决定列表、资格、提交 / 撤回、状态、纠正摘要、通知深链、目标失效 |
| Console | 队列、证据、复核、部分范围、动作、事件、冲突恢复、PC 写入与 mobile 只读 |
| 运行态 | 六角色、五目标、双语、PC / mobile、多主题、离线 / 多标签、清理与数据库完整性 |

## 十七、停止线

- 不重开、覆盖或删除原 `ContentModerationCase`、举报、证据、决定和动作。
- 不用普通 `BaseRepository.RestoreByIdAsync` 绕过来源、版本、租户和领域规则直接恢复目标。
- 不把 `ModerationTargetActionId` 当作通用领域状态；它只解释当前不可用状态的治理来源。
- 不用反射、动态字段名或通用 JSON 适配器隐藏五类目标差异。
- 不以申诉权限读取未举报私聊、其他用户申诉或无关案件。
- 不把动作失败、目标变化或后续限制包装成成功；公开结果与内部结果码分别稳定表达。
- 不顺带建设举报撤回、工单、附件、管理员聊天、多级仲裁、法务、自动审核或补偿系统。
- 不并行修改圈子关系政策、公开聊天、论坛回滚、Flutter / Tauri 或主动生产证据采集。
- F4-J-D 达到完成标准后关闭专题；新增举报类型、双人复核或二次申诉必须重新设计。

## 十八、完成标准

1. 被处置用户能看到自己的决定通知，在期限内提交一次申诉并获得稳定公开结果。
2. 原案件和原举报保持历史不可变，申诉、纠正动作和当前领域状态各有唯一职责。
3. `Upheld / PartiallyGranted / Granted` 与所选纠正范围在并发和重复请求下保持一致。
4. 五类目标只能恢复原案件真实造成且未被后续状态替代的处置，Chat 联动数据与搜索投影一致。
5. Mute / Ban 纠正不会解除后续案件或其他来源的当前限制。
6. 申诉采纳、动作执行和通知结果分开表达，跨库失败可安全重试且不产生重复副作用。
7. 申诉人、举报者和三类 Console 权限角色只看到必要信息，跨租户和私聊边界不泄露。
8. SQLite / PostgreSQL migration、历史来源回填、doctor、verify、恢复演练和相关数据库完整性通过。
9. `/me/appeals` 与 Console `/moderation` 在 PC / mobile、中英文、冲突、深链、目标变化和失败恢复下可用。
10. 定向测试、前后端静态门禁、成组运行态验收和临时数据清理全部通过。
