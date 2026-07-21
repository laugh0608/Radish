# F4-I 内容治理案件、证据与动作一致性

> **状态**：F4-I-A-B 设计与服务端权威契约已完成；下一顺位进入 F4-I-C Pencil 与正式页面
>
> **复核日期**：2026-07-21（Asia/Shanghai）
>
> **适用范围**：正式 Web 举报入口、Main 库内容治理对象与服务、Console `/moderation`；WebOS 只复用既有举报入口，Flutter 不新增治理页面
>
> **关联文档**：[Console 治理工作台设计端点](/frontend/console-governance-workbench-design) · [用户承诺与隐私边界](/guide/user-commitments) · [写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)

## 一、结论摘要

F4-I 把当前“逐条举报审核 + 手工禁言 / 封禁”补成可追溯的“案件 → 证据 → 决定 → 动作 → 结果”治理链路，但不建设自动审核平台。

核心裁决如下：

1. `ContentReport` 继续表示用户提交的举报事实，不再同时承担治理案件和最终决定。
2. 同一租户、同一目标在同一处理周期内聚合到唯一开放 `ContentModerationCase`；多名举报者不会制造彼此冲突的独立结论。
3. 举报时快照、后续受权复核快照和动作结果写入追加式 `ContentModerationEvidence`，不得覆盖旧证据。
4. 案件决定与执行动作分离：决定说明是否违规和处置意图，动作记录实际限制内容或用户的结果；决定成功不等于动作已经成功。
5. `UserModerationAction` 继续作为动作流水；新增唯一 `UserModerationState` 作为禁言 / 封禁当前状态真相源，权限读取不再扫描并修改历史流水。
6. 帖子、评论、轻回应、聊天消息和商品的限制动作必须经过目标类型适配器调用既有领域语义，不允许治理服务直接拼接跨领域字段更新。
7. 案件决定、用户状态变更、目标动作和事件留痕使用版本、事务、业务键与数据库唯一约束保护；并发管理员不能制造双重当前状态。
8. Console 保持现有 `/moderation` 页面归属，升级为案件工作台；举报者在正式 Web 只能查看自己的精简处理结果，不接触内部证据、操作者、其他举报者或用户惩罚细节。

## 二、F4-I-A 候选复核与专题裁决

### 2.1 同口径审计结果

| 维度 | 圈子关系与复访 | 治理案件、证据与动作 |
| --- | --- | --- |
| 当前用户路径 | `/circle` 已有关注动态、关注 / 粉丝列表、公开资料回跳；公开主页可关注 / 取消关注 | client 已覆盖帖子、评论、轻回应、聊天消息和商品举报；Console 已有审核、手工动作与日志 |
| 数据基础 | `UserFollow` 具备租户、软删除、关注对唯一索引与关注时间 | `ContentReport`、`UserModerationAction` 已有租户、快照、审核字段、动作来源和状态 |
| 服务端基础 | Follow / Unfollow、状态、列表、汇总、关注流、可靠通知均已存在 | 提交、队列、条件审核、发布限制、动作日志和目标导航均已存在 |
| 正式页面 | `/circle` 与 `/u/:id` 已形成 PC / mobile 主路径 | `/moderation` 已形成队列、证据、动作、日志和移动顺序 |
| 当前断点 | 缺少关系屏蔽 / 关注隐私；关注流只覆盖帖子；更深复访需要先决定新关系政策 | 多举报无案件聚合；审核字段与动作重复表达；手工来源未校验同案同目标；活跃动作缺唯一状态；过期状态由读取时顺带写回；目标内容没有统一处置结果 |
| 风险性质 | 主要是新增产品政策、隐私和反骚扰能力 | 已有高权限主路径的数据一致性、权限、并发和审计可信度风险 |
| 维护成本 | 若启动需同时定义屏蔽、私信、公开主页、圈子与通知的关系矩阵 | 可沿现有举报类型、Console 工作台、权限与通知边界纵向收口 |
| 当前顺位 | 后置；保留 `Docs/features/circle.md` 为现状权威边界 | 选为唯一 F4-I 专题 |

### 2.2 为什么治理优先

圈子不是未完成的空壳。`UserFollowService` 已覆盖软删除恢复、关注通知、公开标识补齐、关注 / 粉丝列表和关注帖子流；`CircleApp` 已有正式路由、分页、双语和来源返回。下一步若扩展屏蔽、私密关注或多对象动态，本质上是建立新的关系政策，需要单独裁决，不应借“复访增强”顺带加入。

治理则已经处理真实高权限写入，但存在以下共同根因：

- `ContentReport` 以“同一举报者、同一目标、存在 Pending”去重，不会把同一目标的多份举报汇入同一处理对象。
- 举报单上的 `ReviewActionType / ReviewDurationHours` 和 `UserModerationAction` 同时表达处置，审核结论与实际动作可能失配。
- 手工动作允许填写 `SourceReportId`，但当前服务没有验证来源举报是否存在、是否同租户、是否指向同一目标用户。
- 替换禁言 / 封禁时先停用旧记录、再新增记录；缺少数据库唯一当前状态和统一版本，手工动作也未整体纳入事务。
- `GetUserModerationStatusAsync` 在读取权限时顺带把过期动作写成非活跃，导致查询承担状态修复职责，动作日志的 `IsActive` 过滤可能长期滞后。
- 审核通过可以只改变举报状态或创建用户限制，但没有权威字段说明目标内容最终是否保持、限制、已失效或执行失败。

这些问题直接影响治理判断能否解释、动作能否复核、限制能否稳定生效，因此优先级高于圈子的新增增强项。

## 三、目标与非目标

### 3.1 目标

- 同一目标的多份举报进入唯一开放案件，治理人员围绕案件而不是孤立举报单工作。
- 举报创建时立即保存安全快照；后续证据只追加，目标编辑、删除或撤回后仍能说明当时依据。
- 治理决定、内容动作、用户动作和通知结果能够在同一时间线回看。
- 禁言 / 封禁当前状态具备唯一真相源、版本保护、幂等业务键和跨数据库并发约束。
- 帖子、评论、轻回应、聊天消息和商品按各自领域规则执行限制，不破坏附件、实时事件、库存 / 商品版本或软删除审计。
- 举报者可以查看自己的提交是否仍在处理，以及最终是未发现违规、已采取措施或证据不足。
- Console PC 保持高密度案件工作台；mobile 保持“队列 → 证据 → 决定 → 动作 → 留痕”的单列顺序。
- 迁移保留历史举报和动作，不猜测、合并或改写既有终态事实。

### 3.2 非目标

- 不做机器审核、风险评分、自动封禁、关键词审查或推荐侧降权。
- 不做完整申诉、仲裁、法务工单、SLA 分派或跨团队审批平台。
- 不允许任意证据文件上传，不复制附件二进制，不建立敏感内容训练语料库。
- 不搜索未被举报的私聊，不允许管理员借治理身份穿透完整私聊历史。
- 不把 `Ban` 扩张为登录阻断、账号注销、资产冻结或全局身份删除；本专题仍只治理内容发布能力。
- 不新增独立移动 Console、Flutter 治理页或 WebOS 专属实现。
- 不并行修改圈子关系、通知中心信息架构、推荐、ActivityPub 或主动生产证据采集。

## 四、用户与治理路径

### 4.1 举报者

1. 登录用户从帖子、评论、轻回应、聊天消息或商品进入统一举报弹窗。
2. 服务端解析真实目标、校验自举报与租户边界、创建举报事实并加入现有开放案件；没有开放案件时原子创建。
3. 响应返回举报公开标识和精简收件状态，不向举报者暴露案件内部 ID、其他举报者或管理员信息。
4. 用户可在 `/me/reports` 查看自己的举报：目标摘要、原因分类、提交时间和精简处理状态。
5. 案件结案后通过可靠通知提示结果；只显示“未发现违规 / 已采取措施 / 证据不足”，不显示内部备注、其他证据或具体惩罚详情。

### 4.2 Console 治理人员

1. 在 `/moderation` 按案件状态、目标类型、原因、证据可用性、动作结果和关键词筛选。
2. 打开案件后先查看目标身份、举报聚合摘要、创建时证据和当前目标状态。
3. 如需刷新目标，显式执行“采集当前快照”；新快照追加到证据时间线，不能覆盖举报时证据。
4. 提交决定时选择 `NoViolation / Violation / InsufficientEvidence`，填写对外结果分类和内部备注。
5. `Violation` 可选择目标处置与用户处置；服务端先验证权限、案件版本、目标版本和受保护账号规则，再在事务边界内执行。
6. 页面明确区分“决定已保存”“动作执行中 / 已完成 / 失败”，失败不得伪装成已处置。
7. 处理完成后可从案件回看报告、证据、决定、动作、通知和纠正动作。

### 4.3 被处置用户

- 用户限制生效时收到结构化通知，包含限制类型、开始时间、截止时间或永久标记、稳定原因分类和规则入口。
- 不披露举报者身份、内部证据、管理员姓名或其他举报内容。
- 发布入口继续消费服务端权威权限；前端提示不能成为限制真相源。
- 解除或自然到期后，当前状态即时恢复；不依赖先访问某个页面触发历史行修复。

## 五、权威对象与真相源

| 对象 | 权威职责 | 不承担 |
| --- | --- | --- |
| `ContentReport` | 单个用户的举报事实、原因、举报时目标快照、所属案件 | 案件当前状态、最终治理决定、用户限制当前状态 |
| `ContentModerationCase` | 同一目标本处理周期的聚合状态、决定、版本和开放唯一键 | 证据正文、动作历史、目标领域对象本身 |
| `ContentModerationEvidence` | 追加式安全证据快照、来源、摘要、哈希、可用状态和采集人 | 可编辑备注、任意附件仓库、目标当前状态 |
| `ContentModerationCaseEvent` | 案件状态转换、决定、动作请求 / 结果和纠正的追加式时间线 | 当前状态的重复缓存 |
| `UserModerationAction` | 禁言、封禁、解除和迁移校准的不可变动作流水 | 当前是否受限的查询真相源 |
| `UserModerationState` | 用户在 `Mute / Ban` 维度的唯一当前状态、版本和最近动作指针 | 完整历史 |
| 原领域对象 | 帖子、评论、轻回应、聊天消息、商品的当前可见 / 可售状态 | 治理案件和举报详情 |

禁止从 `ContentReport.ReviewActionType`、`UserModerationAction.IsActive` 或 Console 本地状态继续推导新治理状态。旧字段在兼容迁移期只读保留，所有新写入使用案件、事件、动作流水和状态表。

## 六、数据模型

### 6.1 `ContentModerationCase`

首批至少包含：

- `TenantId`
- `TargetType / TargetContentId / TargetUserId`
- `OpenTargetKey: string?`：开放案件使用稳定目标键；结案后清空
- `Status`：`Open / Reviewing / Resolved`
- `Decision`：`None / NoViolation / Violation / InsufficientEvidence`
- `TargetDisposition`：`None / Keep / Restricted / Unavailable / ActionFailed`
- `Version: int`
- `OpenedAt / ResolvedAt / ResolvedById / ResolvedByName`
- `PublicResultCode`：举报者可见的稳定结果分类
- `InternalRemark`：仅受权 Console 可见
- 创建、修改和软删除审计字段

唯一约束：

```text
(TenantId, OpenTargetKey) UNIQUE
```

开放案件的 `OpenTargetKey` 由服务端使用稳定 `TargetType:TargetContentId` 生成；终态结案时设为 `null`。SQLite 与 PostgreSQL 都允许唯一索引中存在多行 `null`，从而保留同一目标的历史案件，同时保证任意时刻最多一个开放案件。

案件不接受客户端提供目标用户、租户、开放键、状态或版本；这些全部由服务端从目标和当前身份解析。

### 6.2 `ContentReport` 调整

新增：

- `CaseId: long`
- `PublicId: string`：返回给举报者的稳定非 LongId 标识
- `ReporterState`：`Submitted / Resolved`

保留现有举报时快照字段，用于兼容和快速列表。`Status / Review*` 历史字段在迁移后只读，不再作为新写入真相源。

同一用户对同一开放案件只允许一份有效举报。使用 `(TenantId, CaseId, ReporterUserId)` 唯一约束，不靠“先查再插”防并发重复。

### 6.3 `ContentModerationEvidence`

字段至少包含：

- `TenantId / CaseId / EvidenceSequence`
- `EvidenceType`：`ReportSnapshot / CurrentTargetSnapshot / ModeratorNote / ActionResult`
- `SourceReportId / RelatedActionId`
- `TargetState`：`Available / Deleted / Recalled / Disabled / Unavailable`
- `SnapshotTitle / SnapshotSummary / TargetUserId / TargetUserName`
- `TargetPostId / TargetCommentId / TargetChannelId / TargetMessageId`
- `ContentRevision / TargetModifiedAt`：来源支持时记录；不伪造不存在的版本
- `SnapshotHash`：对规范化安全快照计算摘要，用于发现意外改写，不作为真实性签名
- `CapturedAt / CapturedById / CapturedByName`
- 创建审计字段

证据记录只追加，不提供普通 Update / Delete API。举报快照继续限制长度并清除 Markdown 图片地址、内部资源协议和不必要标识；聊天证据只包含被举报消息，不扩展相邻私聊正文。附件只记录安全类型和数量，不复制文件、不生成长期公共链接。

### 6.4 `ContentModerationCaseEvent`

至少记录：

- `TenantId / CaseId / EventSequence`
- `EventType`：`Opened / ReportAttached / ReviewStarted / EvidenceCaptured / DecisionRecorded / ActionRequested / ActionSucceeded / ActionFailed / CorrectiveAction`
- `ExpectedCaseVersion / ResultCaseVersion`
- `RelatedReportId / RelatedActionId`
- `FromStatus / ToStatus / ResultCode / Remark`
- `ActorUserId / ActorName / CreateTime`

事件只解释过程，不复制整份证据或动作 payload。`EventSequence` 在案件内单调递增，并由专属 Repository 在事务中分配。

### 6.5 `UserModerationState`

每个租户、目标用户和策略类型只允许一行：

- `TenantId / TargetUserId / PolicyType(Mute|Ban)`
- `State`：`Inactive / Active`
- `EffectiveFrom / EffectiveUntil`；`null EffectiveUntil` 在 Active 时表示永久
- `Version`
- `CurrentActionId / SourceCaseId`
- 修改审计字段

唯一约束：

```text
(TenantId, TargetUserId, PolicyType) UNIQUE
```

权限读取按当前 UTC 与状态行实时派生是否生效，不在读取路径回写过期流水。后台清理任务可以把已过期状态物化为 `Inactive` 并追加 `Expired` 动作，但物化延迟不能改变权限结果。

### 6.6 `UserModerationAction` 调整

新增：

- `CaseId: long?`
- `OperationKey: string`
- `PreviousStateVersion / ResultStateVersion`
- `SupersedesActionId: long?`
- `ResultCode`

动作行创建后不可修改业务字段。历史 `IsActive / EndTime` 保留兼容读取，但新状态由 `UserModerationState` 决定。`OperationKey` 建立租户内唯一约束，重复请求返回原动作结果，同键异参返回 `409`。

## 七、案件状态、决定与动作

### 7.1 案件状态

```text
Open -> Reviewing -> Resolved
  \----------------------/
```

- 首份举报原子创建 `Open` 案件；后续举报加入同一开放案件。
- 治理人员首次显式接手或保存新证据后进入 `Reviewing`。
- `NoViolation / Violation / InsufficientEvidence` 均为终态决定并进入 `Resolved`。
- 首批不复用已结案件；同一目标后续再次被举报时创建新案件，保持处理周期清晰。
- 纠正动作不会改写旧决定；它追加事件和动作。完整申诉 / 重开流程后置。

### 7.2 决定与动作分离

`ReviewCase` 请求必须提交：

- `casePublicId`
- `expectedVersion`
- `decision`
- `publicResultCode`
- `internalRemark`
- 可选 `targetAction`
- 可选 `userAction`
- `operationKey`

规则：

- `NoViolation` 只能选择 `Keep`，不能附带惩罚动作。
- `InsufficientEvidence` 只能选择 `Keep / Unavailable`，不能附带用户惩罚。
- `Violation` 可以只记录违规而不限制用户，但必须显式选择目标处置结果，不能靠 `None` 猜测。
- 案件 CAS 成功只代表决定已登记；每个动作还要记录请求、执行结果和失败原因。
- 如果动作属于同一 Main 库并可在同一事务完成，决定、动作、当前状态、案件事件和 Outbox 一起提交。
- Chat 库动作使用 Main 可靠 Outbox 调用专属幂等消费者；案件先保持 `Reviewing` 与 `ActionRequested`，消费者成功后才进入 `Resolved`。失败进入可重试状态，不写成“已采取措施”。

### 7.3 用户限制状态转换

- `Mute / Ban` 使用 `expectedStateVersion + operationKey` 原子设置目标状态。
- 新 Ban 生效时同时停止 Mute 当前状态，但保留两类动作流水；解除 Ban 不自动恢复旧 Mute。
- `Unmute / Unban` 只改变当前状态并追加解除动作，不修改旧动作。
- 自然到期由读取实时判断；物化任务使用状态版本条件更新，不能覆盖管理员刚延长的限制。
- 操作者不能治理自己；非 System 操作者不能治理 System 账号。Admin 账号的高权限保护在服务端统一策略中判断，不能只靠 Console 隐藏按钮。

## 八、目标内容处置适配

治理服务只依赖 `IContentModerationTargetActionService`；各目标适配器复用领域服务与审计语义：

| 目标 | `Keep` | `Restrict` | 恢复边界 |
| --- | --- | --- | --- |
| `Post` | 不改变正文 | 治理软删除 / 下线，保留帖子与附件关系 | 仅能恢复由治理动作造成、且之后未发生冲突修改的目标 |
| `Comment` | 不改变评论 | 治理软删除，保留回复锚点和举报快照 | 同上；恢复需重新校验所属帖子可用 |
| `PostQuickReply` | 不改变轻回应 | 复用管理员删除语义 | 仅在领域服务支持安全恢复后开放；首批可标记不可逆 |
| `ChatMessage` | 不改变消息 | 使用治理撤回语义并广播既有撤回事件 | 首批不可恢复，不伪造作者撤回，不暴露原正文 |
| `Product` | 不改变商品 | 使用期望版本下架，不直接删除商品或库存 | 恢复销售回到商品管理页重新校验完整商品配置 |

适配器返回结构化结果：`Succeeded / AlreadyInTargetState / VersionConflict / TargetUnavailable / Unsupported / Failed`，并附目标动作前后版本。治理服务不得根据中文消息判断结果。

## 九、权限与隐私

### 9.1 Console 权限

| 权限 | 能力 |
| --- | --- |
| `console.moderation.view` | 查看案件队列、安全证据、精简用户状态和动作日志 |
| `console.moderation.review` | 接手案件、追加管理员备注、登记决定和目标处置 |
| `console.moderation.action` | 禁言 / 封禁 / 解除用户，以及结案后用户状态纠正 |

现有 `console.moderation.review` 在迁移时为已拥有该权限的角色补同范围 `console.moderation.action`，保持当前部署能力不静默丢失；之后管理员可显式拆分。System / Admin 默认权限列表、路由映射、Controller attribute 和按钮级能力必须使用同一稳定权限键。

### 9.2 数据可见性

- 举报者只能读取自己的举报，不读取 Case 内部 ID、其他举报者、内部备注、证据列表、动作类型或操作者。
- 被处置用户只读取自身当前限制和对外原因，不读取来源举报者或内部证据。
- Console View 可读取治理必要快照，但不能读取完整私聊上下文、原附件 token、支付 / 会话凭据或无关第三方资料。
- 跨租户统一按不存在处理；客户端提供的租户、目标用户、案件状态和权限字段全部忽略。
- 目标已删除、撤回或失效时仍保留创建时安全证据，但新导航统一返回不可用，不尝试绕过领域 ACL。
- 日志记录 Case / Report / Action ID、状态、结果码、耗时和 TraceId，不记录举报详情、证据正文、聊天内容或用户令牌。

## 十、接口边界

### 10.1 用户侧

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| `POST` | `/api/v1/ContentModeration/Report` | 保留现有入口；响应改为精简举报收件对象 |
| `GET` | `/api/v1/ContentModeration/GetMyReports` | keyset / 稳定分页读取本人举报结果 |
| `GET` | `/api/v1/ContentModeration/GetMyReport/{reportPublicId}` | 读取本人单份精简结果 |
| `GET` | `/api/v1/ContentModeration/GetMyModerationStatus` | 继续读取当前用户权威限制状态 |
| `GET` | `/api/v1/ContentModeration/GetMyPublishPermission` | 继续提供发布前预检；实际写入仍必须服务端复核 |

`Report` 保持稳定 HTTP status、`Code / MessageKey` 和 LongId 字符串安全。重复同案举报返回已有收件对象，不生成第二行。

### 10.2 Console

| 方法 | 路径 | 权限 |
| --- | --- | --- |
| `GET` | `/api/v1/ContentModeration/GetCaseQueue` | `moderation.view` |
| `GET` | `/api/v1/ContentModeration/GetCase/{casePublicId}` | `moderation.view` |
| `POST` | `/api/v1/ContentModeration/CaptureEvidence` | `moderation.review` |
| `POST` | `/api/v1/ContentModeration/ReviewCase` | `moderation.review`；附带动作时同时要求 `moderation.action` |
| `POST` | `/api/v1/ContentModeration/ApplyCorrectiveAction` | `moderation.action` |
| `GET` | `/api/v1/ContentModeration/GetCaseEvents` | `moderation.view` |

旧 `GetReviewQueue / Review / ApplyUserAction / GetActionLogs` 在 F4-I-C 消费者迁移完成前保持兼容；新 Console 全部切换后删除旧写入口，旧读入口只在确有兼容消费者时保留。不得让新旧写接口同时长期维护两套状态。

## 十一、迁移与兼容

F4-I-B 增加 Main schema ledger migration，SQLite 与 PostgreSQL 同时覆盖：

1. 创建 `ContentModerationCase / ContentModerationEvidence / ContentModerationCaseEvent / UserModerationState`。
2. 为 `ContentReport` 增加 `CaseId / PublicId / ReporterState`，为 `UserModerationAction` 增加案件、业务键和状态版本字段。
3. 已处理历史举报各自创建独立终态案件，不合并或改写既有审核事实；同租户、同目标的历史 Pending 举报按目标聚合到一个开放案件，因为它们尚无终态决定，且必须满足开放案件唯一约束。
4. 历史举报快照迁为首条 `ReportSnapshot` 证据；历史审核字段迁为决定事件并原样保留。
5. 历史动作继续保留。对当前仍有效的 Mute / Ban，按现有有效性并集生成一行 `UserModerationState`：存在永久动作则为永久，否则截止时间取最晚值；迁移生成校准动作和事件说明来源。
6. migration doctor 报告缺失目标、跨租户来源、重复 PublicId、非法枚举和无法解释的动作时间，不静默修复目标身份或举报内容。
7. migration verify 检查开放案件唯一、举报案件归属、证据 / 事件序号、用户状态唯一和业务键唯一。
8. 旧审核字段和 `IsActive` 首批不删除；代码停止新写后，后续独立迁移再评估物理移除。

迁移执行前继续按正式 DbMigrate 流程备份数据库；空库、历史库、重入、回滚恢复和 PostgreSQL 并发首次 apply 都必须验证。

## 十二、Console 与正式 Web 页面

### 12.1 Console `/moderation`

F4-I-C 在既有 `P02 / P07` 上更新 Pencil，不新建平行治理应用：

- 队列从“举报单列表”改为“案件列表”，显示举报数、目标、状态、最近证据和动作结果。
- 案件详情分为举报摘要、证据时间线、目标当前状态、决定表单、动作结果和事件时间线。
- 证据必须区分“举报时固化”和“当前重新采集”，并显示可用 / 已删除 / 已撤回 / 已禁用。
- 决定与动作分区展示；没有 `moderation.action` 时可以登记无动作决定，但不能发送处置 payload。
- 并发冲突保留表单草稿，提示刷新案件；不得清空内部备注或自动覆盖新版本。
- PC 使用队列 + 详情 + 动作 rail；mobile 按队列、证据、决定、动作、留痕单列展开。

### 12.2 `/me/reports`

- 归入既有 `/me` 私域路由，不进入公开 sitemap、canonical 或 JSON-LD。
- 列表只显示本人的举报收件与精简结果；用户内容按原文展示，系统词元双语。
- 目标仍可打开时使用既有结构化导航；失效时保留提交摘要并说明无法打开。
- 不提供撤回、催办、追加附件或与管理员对话；这些属于后续申诉 / 工单专题。

## 十三、通知与失败恢复

- 举报结案、用户限制生效、解除和自然到期复用现有通知中心和 Main Reliable Outbox。
- 业务键至少包含 Case / Report / Action 与目标状态版本；重放不能重复发送同一状态通知。
- Main 内动作失败整体回滚；跨 Chat 库动作失败保留 `ActionFailed` 事件和可重试任务，案件不提前显示已完成。
- Console 可按同一 `operationKey` 重试失败动作；同键异参返回冲突，新决策必须使用新业务键和新案件版本。
- 目标版本冲突时重新加载当前目标和证据，由治理人员确认后再执行，不自动强制覆盖作者或商品管理员的新修改。
- 通知失败不回滚已经生效的治理动作，由可靠任务重试；案件时间线显示通知待处理 / 完成状态但不泄露通知正文。

## 十四、容量与性能

- 案件队列按 `(TenantId, Status, ModifyTime DESC, Id DESC)` keyset 或稳定分页查询。
- 举报按 `(TenantId, CaseId, CreateTime, Id)` 查询；证据和事件按案件内序号查询。
- 目标导航与用户摘要继续批量装配，不按每个案件逐条查询多个领域对象。
- `navigationStatus` 等派生筛选不能继续把全量举报读入内存；F4-I-B 应把可查询状态物化到案件或专属投影。
- 单案首批最多返回 100 份举报摘要、100 条证据和 200 条事件；超过后分别分页，不截断数据库事实。
- 举报详情和证据正文不进入缓存、浏览器持久化或跨标签广播；列表只缓存精简案件摘要。

## 十五、开发批次

### F4-I-A：现状审计与权威设计（已完成）

- 交叉审计圈子关系与治理候选的数据、权限、接口、页面、测试和文档。
- 选定治理案件、证据与动作一致性为唯一专题，说明圈子后置原因。
- 固定对象、状态、权限、迁移、接口、页面、通知、失败恢复、停止线和验收口径。
- 本批不修改业务代码、接口、migration 或 Pencil。

### F4-I-B：服务端权威契约（已完成）

- 完成 Main ledger migration、历史映射、doctor / verify 与 SQLite / PostgreSQL 回归。
- 增加专属 Case / Evidence / State Repository，禁止 Service 直接访问 Db。
- 落地案件聚合、证据追加、Case CAS、用户状态原子设置、目标动作适配、可靠通知和稳定错误。
- 建立新用户侧 / Console API 与 `@radish/http` 契约；保留旧消费者所需兼容读写边界。
- 按停止线不修改 Pencil 或正式页面结构。

完成事实与验证证据见 [F4-I-B 服务端权威契约完成记录](/records/f4-i-b-content-moderation-server-contract-2026-07-21)。旧 `Review / ApplyUserAction` 在消费者迁移前已复用同一 `UserModerationState` 原子写入边界，不再形成第二套当前状态；新页面完成迁移后仍按原计划删除旧写入口。

### F4-I-C：Pencil 与正式页面

- 更新 Console `P02 / P07` 的案件工作台和移动流程。
- 将 `/moderation` 迁入新 Case API，覆盖筛选、证据、决定、动作、冲突和失败恢复。
- 新增 `/me/reports` 私域精简状态页和可靠通知目标。
- 删除旧 Console 写消费者后关闭旧写 API；WebOS 只复用举报弹窗，不新增治理工作台。

### F4-I-D：成组验收与专题关闭

- 至少使用举报者、被举报者、只读治理员、审核员和动作执行员覆盖权限矩阵。
- 覆盖五类目标、多举报聚合、重复提交、并发接案 / 结案、动作失败 / 重试、目标编辑 / 删除 / 撤回、禁言 / 封禁 / 解除 / 到期和跨租户。
- 覆盖 `zh / en × PC / mobile`、目标失效、Back / Forward、长文本、键盘、无障碍和四主题代表矩阵。
- 清理临时案件、状态、动作、通知、账号与凭据；检查 Main / Message / Chat 等相关库完整性并执行严格 migration verify。

## 十六、验证矩阵

| 层级 | 必须覆盖 |
| --- | --- |
| Model / migration | 空库、历史库、重入、备份恢复、开放案件唯一、用户状态唯一、历史映射、非法数据 doctor |
| Repository | 并发举报聚合、Case CAS、事件序号、状态版本、OperationKey 同键回放 / 异参冲突 |
| Service | 自举报、跨租户、来源同案同目标、决定 / 动作规则、受保护账号、过期实时派生、目标版本冲突 |
| 目标适配器 | Post / Comment / QuickReply / ChatMessage / Product 的 Keep、Restrict、重复动作和恢复边界 |
| API / 权限 | View / Review / Action 分离、404 / 409 / 403、LongId 字符串、稳定 Code / MessageKey、旧入口退役 |
| 通知 / Outbox | 举报结果、限制 / 解除 / 到期、重复任务、失败重试、接收者隐私 |
| Client | 举报收件、本人列表、精简结果、目标失效、双语、私域路由 |
| Console | 案件队列、证据、决定、动作、事件、冲突恢复、PC / mobile 权限态 |
| 运行态 | 多角色、五类目标、离线 / 多标签、真实跨库动作、清理与数据库完整性 |

## 十七、停止线

- 不在 F4-I-B 顺带改造 Console 全站、用户角色体系、论坛阅读模型或聊天历史权限。
- 不以案件系统名义读取未举报私聊、复制附件二进制或扩大管理员数据可见范围。
- 不用异常吞噬、默认成功或重复 fallback 掩盖动作失败；决定、动作与通知结果必须分开显示。
- 不让 `ContentReport.Review*`、`UserModerationAction.IsActive`、前端缓存或通知状态继续成为新逻辑真相源。
- 不为所有领域建立通用反射式“治理适配器框架”；只为五个真实目标实现明确、可测试的适配器。
- 不启动机器审核、申诉平台、生产行为聚合、圈子关系政策、Flutter / Tauri 或独立移动 Console。
- F4-I-D 满足完成标准后关闭专题；新举报类型或申诉能力必须重新设计，不持续扩大本专题。

## 十八、完成标准

1. 同一目标任意时刻最多一个开放案件，并发举报不会产生重复案件或重复用户举报。
2. 举报、证据、决定、动作、当前用户状态和案件事件各有唯一职责，不存在互相覆盖的状态真相源。
3. 禁言 / 封禁 / 解除在并发、重复请求和自然到期下保持唯一当前状态，发布权限即时准确。
4. 五类举报目标都能给出权威 Keep / Restrict / 不支持或失败结果，不把决定成功冒充为动作成功。
5. 举报者、被处置者和 Console 不同权限角色只看到各自必要信息，跨租户和私聊边界不泄露。
6. 历史举报与动作完成可解释迁移，SQLite / PostgreSQL migration、doctor、verify 和恢复演练通过。
7. Console `/moderation` 与 `/me/reports` 在 PC / mobile、中英文、目标失效、冲突和失败恢复下可用。
8. 定向测试、前后端静态门禁、成组运行态验收、临时数据清理和数据库完整性检查全部通过。
