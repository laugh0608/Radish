# F4-O 论坛问答回答生命周期与治理闭环

> **状态**：F4-O-A/B 已完成；等待批准进入 F4-O-C Pencil 与正式 Web
>
> **复核日期**：2026-07-27（Asia/Shanghai）
>
> **适用范围**：正式 Web 论坛问答、Main / Message 数据库、API、`@radish/http` 和 `radish.client`
>
> **前置专题**：[论坛问答 MVP](/features/forum-qa-mvp) · [论坛内容版本完整性与作者恢复](/features/forum-content-version-recovery-design) · [内容治理案件、证据与动作一致性](/features/content-moderation-case-evidence-action-design) · [通知中心深化与通知治理](/features/notification-center-deepening)

## 一、结论摘要

F4-O 选择“论坛问答回答生命周期与治理闭环”为当前唯一功能专题。

现有问答已经能发布问题、提交回答和采纳答案，也具备提交意图去重与正式 Web 入口；但 `PostAnswer` 仍是早期 MVP 内容对象：所有回答随帖子详情一次性加载，写入继续寄居 `PostService`，没有独立附件类型、公开标识、编辑 / 删除、版本恢复、举报治理、可靠通知和采纳变更审计。继续增加排序、奖励或独立问答页会放大这层权威缺口。

核心裁决如下：

1. 问答继续属于论坛帖子，不建设独立问答 App；`PostQuestion` 是问题状态聚合，`PostAnswer` 是独立可治理内容对象。
2. 新增 `IForumQuestionService / IForumQuestionRepository`，回答创建、编辑、删除、恢复和采纳不再由 `PostService` 拼接多个 BaseRepository 写入。
3. `PostAnswer` 新增稳定 `ans_` PublicId、`ContentRevision`、`EditCount` 和 `IsEnabled`；正式 Web 与新通知只使用 `postPublicId + answerPublicId` 定位。
4. 新增不可变 `PostAnswerContentRevision`；每次创建、编辑和恢复都保存完整正文快照及附件引用，普通编辑和恢复使用 `ExpectedContentRevision` CAS。
5. `PostQuestion` 新增 `AcceptanceRevision` 与 `AcceptedAnswerContentRevision`；采纳、替换、撤销和治理清除写入追加式 `PostAnswerAcceptanceEvent`，使用 CAS 防止双标签页覆盖。
6. 被采纳回答在普通作者态冻结编辑和删除。提问者先撤销或替换采纳后，回答作者才能继续修改；治理限制不受此冻结阻挡。
7. 回答作者可在规则允许时编辑、恢复或软删除自己的未采纳回答；删除、治理限制和恢复都在同一事务中维护可见回答数。
8. 回答成为第六类内容治理目标 `PostAnswer`，支持举报、证据、限制、申诉和受权恢复；被限制的已采纳回答会原子清除采纳，恢复后不自动重新采纳。
9. 回答创建、采纳 / 替换和撤销通知通过 Main Reliable Outbox 逐笔请求；业务键防重，双向屏蔽或目标失效时抑制通知但不改写权威事实。
10. 回答附件使用独立 `PostAnswer` 业务类型；migration 纠正历史上借用 `Comment` 的附件归属，并把回答 Revision 纳入附件存续引用。
11. 回答列表改为服务端分页。帖子详情只返回问题摘要与首屏回答页，不再把全部历史回答塞入 `PostVo`。
12. 本专题不增加回答投票、复杂排序、悬赏、萝卜币、排行榜、回答评论树、独立问答首页或 Console 问答运营台。

## 二、F4-O-A 候选审计

| 候选 | 已有基础 | 真实缺口 | 风险与投入 | 裁决 |
| --- | --- | --- | --- | --- |
| 论坛问答回答生命周期与治理 | 问题、回答、采纳、提交幂等、正式 Web 已存在 | 回答不是正式可治理内容对象，缺少分页、版本、通知、附件权威和采纳审计 | 触达论坛、治理、通知和 migration，但边界集中且直接服务 V1 内容沉淀 | **选定为 F4-O** |
| 论坛抽奖深化 | 发帖、评论参与、手动 / 自动开奖和通知已有 MVP | 奖励发放、防刷、公示与后台运营后置 | 会扩张为活动风控和奖品履约，不是社区核心内容缺口 | 继续后置 |
| 论坛投票深化 | 单选、截止、筛选、排序和作者关闭已闭环 | 多选、匿名、撤票和活动页属于增强 | 当前主链无阻断，改票与匿名会重开审计边界 | 继续后置 |
| 个人圈子深化 | 关注动态、关注 / 粉丝、屏蔽隔离和回流已完整 | 推荐、转发和联邦不是当前职责 | 容易与发现页、论坛和未来联邦重复 | 保持现有边界 |
| 匿名公开聊天 | 登录态聊天搜索、Reaction、Pin 和回执已闭环 | 没有匿名实时讨论面 | 与论坛讨论重叠，并引入匿名身份、限流、滥用和 SEO 风险 | 不回拉 |

### 2.1 已确认的代码事实

- `QuestionController.Answer` 通过 `ForumContentWriteService` 提供创建意图去重，但采纳仍直接调用 `IPostService.AcceptAnswerAsync`。
- `PostService.Interaction` 分别写 `PostAnswer`、`PostQuestion.AnswerCount` 和采纳字段；没有专属 Repository 保护问题聚合。
- `PostAnswer` 只有正文、作者、`IsAccepted` 和软删除字段，没有 PublicId、内容版本、编辑次数或治理可见状态。
- `PostQuestion.IsSolved / AcceptedAnswerId / AnswerCount` 是持久摘要，但缺少采纳版本与事件，无法解释何时、由谁、从哪个答案切换。
- `BuildPostQuestionVoAsync` 一次查询并返回帖子全部回答，只在内存中做默认 / 最新排序。
- 回答附件绑定使用 `BusinessType.Comment + answerId`；附件检查虽能扫描回答正文，但业务归属与评论混用。
- 内容治理只支持 `Post / Comment / ChatMessage / Product / PostQuickReply`，`PostAnswer` 不能举报、限制、申诉或恢复。
- 通知定义没有“收到回答 / 回答被采纳 / 采纳被撤销”，也没有 answer-level canonical 定位。
- 正式 `/forum/post/:postPublicId?intent=answer` 已能完成登录回流，PC / mobile 页面已有回答区，因此不需要另建入口。
- F4-M 明确没有把问答回答与采纳纳入 Post / Comment 版本恢复；F4-O 必须保持独立版本和状态边界。

### 2.2 为什么现在优先

- V1 产品定位明确包含“帖子、评论和问答沉淀内容”；回答不是附属活动数据，而是社区核心内容。
- 当前用户已经能发布带附件的长回答，系统却不能正确表达附件归属、内容变更、删除、举报和历史恢复。
- 内容治理已经形成案件、证据、动作和申诉闭环；把回答留在治理外会形成一个可公开发布但无法处置的明显缺口。
- 通知中心已经支持结构化论坛目标；补 `answerPublicId` 比继续暴露 LongId 或建立独立回答 URL 更符合长期契约。
- 投票、抽奖和圈子当前均有可用闭环，继续增强的边际价值低于补齐回答权威生命周期。

## 三、目标与非目标

### 3.1 目标

- 回答成为可独立识别、分页、编辑、恢复、软删除、举报、限制和申诉的正式内容对象。
- 回答写入、问题摘要、版本、附件、提交意图与通知 Outbox 保持 Main 原子一致。
- 提问者可以采纳、替换或撤销答案，所有变化有 CAS 和追加式事件。
- 已采纳答案不会被普通作者静默改写或删除；治理动作可以安全清除无效采纳。
- 历史回答获得稳定 PublicId 与基线 Revision，不伪造不存在的旧版本。
- 回答、采纳和撤销通知可可靠投递并精确回到帖子内目标。
- 正式 Web 在 PC / mobile 上提供一致的回答浏览、作者动作、问题所有者动作和治理入口。
- SQLite / PostgreSQL migration 支持 apply、重入、严格 verify 和旧库回归。

### 3.2 非目标

- 不建设 Stack Overflow 式回答投票、声望、徽章、复杂排序或独立问答频道。
- 不把内容赞赏扩展到 `PostAnswer`；等回答治理和定位闭环完成后再单独评审。
- 不增加悬赏、系统奖励、萝卜币发放或回答排行榜。
- 不增加回答下的独立评论树；帖子讨论区继续承接补充讨论。
- 不增加协同草稿、多人编辑、自动保存、公开全文 diff 或无限恢复。
- 不重做问题帖子本身的版本系统；问题正文继续使用 F4-M Post Revision。
- 不执行 User / Comment / Attachment 等对象的全量 PublicId rollout。
- 不增加 Flutter 新功能、WebOS 专用实现、Tauri 能力或移动系统通知。
- 不建设独立 Console 问答运营页；回答治理复用现有 Moderation 工作台。

## 四、用户路径与产品语义

### 4.1 回答者

1. 从问答帖子详情进入回答区。
2. 匿名点击回答时，经受控 `intent=answer` 登录后回到同一帖子回答区。
3. 登录用户提交正文；服务端返回新回答 PublicId 和首屏列表更新依据。
4. 作者可对未采纳、未限制的自己的回答执行编辑、历史回看、恢复或删除。
5. 已采纳回答显示稳定冻结提示；作者必须等待提问者撤销 / 替换采纳后才能改动。
6. 回答被治理限制时，作者可从现有申诉入口查看决定并提交一次申诉。

### 4.2 提问者

1. 查看分页回答并选择一个有效回答采纳。
2. 已有采纳时，可以明确“替换最佳答案”或“撤销采纳”，不能用再次点击制造隐式切换。
3. 操作请求携带当前 `AcceptanceRevision`；状态已被另一标签页改变时提示刷新。
4. 替换在同一事务内取消旧答案、采纳新答案并追加一个事件，不出现两个已采纳答案。
5. 撤销后问题回到待解决；历史事件仍保留。

### 4.3 读者与举报者

- 读者通过帖子详情浏览回答，不进入独立回答页面。
- 回答 URL 固定为 `/forum/post/:postPublicId?answer=:answerPublicId`；定位参数不进入 canonical。
- 读者可举报可见回答；举报快照包含回答 PublicId、所属帖子、作者、当前 Revision 和净化后的正文摘要。
- 目标删除或被限制后，公开定位降级为帖子详情的“回答不可用”状态，不泄露治理原因。

### 4.4 管理员与申诉处理者

- Moderation 继续使用同一 Case / Evidence / Decision / TargetAction 工作台。
- 限制回答只影响回答内容，不自动限制问题帖子或作者账号。
- 限制已采纳回答时，同一 Main 事务内清除当前采纳、递增 `AcceptanceRevision` 并追加治理事件。
- 申诉恢复只恢复回答可见性，不自动恢复旧采纳；提问者需重新做出当前选择。

## 五、权威对象与状态

| 对象 | 权威职责 | 不承担 |
| --- | --- | --- |
| `Post` | 问题正文、公开路由、分类标签和帖子可见性 | 回答正文、采纳历史 |
| `PostQuestion` | 当前采纳、采纳版本、可见回答数和解决状态摘要 | 回答正文、历史事件 |
| `PostAnswer` | 当前回答正文、作者、PublicId、内容版本和可见状态 | 历史正文、采纳历史 |
| `PostAnswerContentRevision` | 某次成功提交后的不可变回答快照 | 当前可见性、当前采纳 |
| `PostAnswerAcceptanceEvent` | 采纳 / 替换 / 撤销 / 治理清除的追加式证据 | 当前状态查询 |
| `ForumContentRevisionAttachment` | Answer Revision 与附件的持久引用 | 附件存储和公开权限 |
| `ContentSubmissionRecord` | 创建、编辑、恢复和采纳请求的幂等终态 | 内容与采纳事实 |
| `ContentModerationCase / TargetAction` | 回答治理决定、动作来源与申诉恢复 | 问题所有者的产品采纳决定 |

状态规则：

- `PostQuestion.IsSolved` 必须等价于存在有效 `AcceptedAnswerId`。
- `AcceptedAnswerId` 指向同租户、同帖子、可见且未删除的回答。
- `AcceptedAnswerContentRevision` 固化采纳时的回答版本；普通作者冻结保证该版本在采纳期间不漂移。
- `AnswerCount` 表达当前公开可见回答数，不统计软删除或治理限制回答。
- `PostAnswer.IsAccepted` 保留为查询投影，必须与 `PostQuestion.AcceptedAnswerId` 原子一致。
- 屏蔽关系变化不删除已有回答或采纳，但阻断新的回答与采纳互动，并抑制尚未投递的通知。

## 六、数据模型

### 6.1 `PostAnswer` 增量

新增字段：

- `PublicId`：非空 `ans_` + 32 位十六进制；同租户和全局查询均有唯一保护。
- `ContentRevision`：当前正文版本，migration 基线为 `1`。
- `EditCount`：普通作者成功编辑 / 恢复次数，不包含治理动作。
- `IsEnabled`：治理可见开关；默认 `true`。

继续保留 `IsDeleted / DeletedAt / DeletedBy` 表达作者软删除或受权删除。`IsEnabled=false` 与 `IsDeleted=true` 都不进入公开回答列表，但只有前者可由治理申诉恢复。

必要索引：

- 唯一 `PublicId`；
- `(TenantId, PostId, IsDeleted, IsEnabled, IsAccepted, CreateTime, Id)`；
- `(TenantId, AuthorId, IsDeleted, CreateTime)`。

### 6.2 `PostQuestion` 增量

新增字段：

- `AcceptanceRevision`：采纳状态 CAS 版本；无历史操作的基线为 `0`。
- `AcceptedAnswerContentRevision`：当前采纳答案的内容版本；未采纳时为 `null`。

`IsSolved / AcceptedAnswerId / AcceptedAnswerContentRevision` 必须同时为空态或同时有效。`AnswerCount` 在回答创建、删除、治理限制与恢复事务中原子维护，并由 migration verify 检查可重建一致性。

### 6.3 `PostAnswerContentRevision`

字段：

- `Id`
- `TenantId`
- `AnswerId`
- `PostId`
- `RevisionNumber`
- `SourceType`：`Baseline / Edit / Restore`
- `RestoredFromRevisionId`
- `IntegrityStatus`：`Complete / LegacyIncomplete / Redacted`
- `Content`
- `EditorId / EditorName`
- `CreateTime` 与标准创建审计字段

约束：

- 唯一 `(TenantId, AnswerId, RevisionNumber)`；
- 列表索引 `(TenantId, AnswerId, RevisionNumber DESC)`；
- 恢复来源必须属于同回答、同租户；
- 新运行时只写 `Complete`；
- 历史附件无法证明完整时基线标记 `LegacyIncomplete`，不得精确恢复。

### 6.4 `PostAnswerAcceptanceEvent`

字段：

- `Id`
- `TenantId`
- `PostId / PostQuestionId`
- `AcceptanceRevision`
- `EventType`：`Accepted / Replaced / Revoked / ClearedByModeration`
- `PreviousAnswerId / PreviousAnswerContentRevision`
- `CurrentAnswerId / CurrentAnswerContentRevision`
- `OperatorId / OperatorName`
- `ReasonCode`
- `CreateTime` 与标准创建审计字段

唯一 `(TenantId, PostQuestionId, AcceptanceRevision)`。事件只追加，不修改、不软删除；公开页面不返回操作者内部 ID 和治理原因。

### 6.5 附件

- `AttachmentBusinessTypes` 新增 `PostAnswer`。
- 新回答附件绑定为 `BusinessType=PostAnswer, BusinessId=AnswerId`。
- `ForumContentRevisionAttachment.TargetType` 新增 `PostAnswer`。
- 当前回答可见时，附件读取继承所属帖子可见性；作者历史和治理证据走受权读取。
- 回答删除、编辑或治理限制不物理删除仍被 Revision / Evidence 引用的附件。

## 七、服务与事务边界

### 7.1 分层

```text
QuestionController
  -> IForumQuestionService
    -> IForumQuestionRepository
    -> IContentSubmissionService
    -> IUserInteractionPolicyService
    -> IContentModerationService（权限读取）
```

- Controller 只解析认证、DTO 和统一错误契约。
- Service 组织身份、业务规则、幂等和错误映射，不直接使用 Db。
- Repository 使用同一 Main 连接完成问题、回答、Revision、附件引用、提交终态和 Outbox 的原子写。
- `PostService` 继续负责 Post 列表与详情主体，不再承担回答写入和采纳状态迁移。

### 7.2 创建回答

同一事务：

1. 锁定并复核提交意图；
2. 复核帖子 / 问题、租户、可见性、双方账号与双向屏蔽；
3. 创建 `PostAnswer` 与基线 Revision；
4. 绑定当前附件并写 Revision 引用；
5. `PostQuestion.AnswerCount + 1`；
6. 完成提交意图；
7. 写回答通知 Outbox；
8. 一次提交。

任何步骤失败都不得留下孤立回答、计数漂移、缺失基线、错误附件归属或成功幂等终态。

### 7.3 编辑与恢复

- 请求携带 `ExpectedContentRevision` 与 `clientSubmissionId`。
- Repository 以当前版本 CAS 更新正文、版本号和 EditCount，并写新 Revision。
- 无变化返回稳定 `NoChange`，不消耗编辑次数、不写 Revision。
- 被采纳、已删除、被限制、帖子不可用、超出编辑规则或引用附件失效时拒绝。
- 恢复创建新版本，不移动历史指针；`LegacyIncomplete / Redacted` 不允许恢复。

### 7.4 删除

- 回答作者只可删除自己的未采纳回答；管理员删除继续由明确治理 / 管理入口决定。
- 删除请求需要 `ExpectedContentRevision` 和幂等键。
- 同一事务软删除回答、递减可见回答数、完成提交意图；已有历史和附件引用保留。
- 已删除回答的旧 URL 返回不可用定位，不返回旧正文。

### 7.5 采纳、替换与撤销

- 请求携带 `ExpectedAcceptanceRevision` 和幂等键。
- 锁定 `PostQuestion` 后复核操作者仍为问题作者、目标回答有效、账号可互动。
- 采纳 / 替换原子更新旧 / 新 `IsAccepted`、问题当前字段、`AcceptanceRevision + 1`、事件和通知 Outbox。
- 撤销原子清除当前回答、写 `Revoked` 事件并通知原回答作者。
- 同版本并发只能一个成功；其余返回结构化 `Forum.QuestionAcceptanceConflict`。

## 八、权限、屏蔽与治理

### 8.1 回答写入

- 发送者必须登录、同租户、账号可用并具有当前发布权限。
- 问题帖子必须公开可读、已发布、未删除且未被治理限制。
- 问题作者账号必须可用；任一方向存在有效屏蔽时禁止新增回答。
- 问题作者可以补充自己的回答，但沿用现有规则，不得采纳自己的回答。
- 查询策略异常时失败关闭，不允许“查不到就放行”。

### 8.2 回答举报

`ContentReportTargetTypeEnum` 新增稳定值 `PostAnswer = 6`，不得改动既有枚举编号。

治理适配必须覆盖：

- 目标解析、租户与公开可见校验；
- 举报时快照和当前证据；
- Case 聚合与重复待处理举报；
- `Restrict / Restore` 目标动作；
- 通知导航与失效降级；
- 申诉来源保护和权限矩阵；
- Console PC 完整处理、mobile 只读复核。

### 8.3 已采纳回答治理

- 限制当前采纳回答时，在同一 Main 事务内清除采纳并写 `ClearedByModeration`。
- 该动作不会自动采纳其他回答，也不会删除问题帖子。
- 申诉恢复后回答重新进入列表，但旧采纳不自动恢复。
- 治理事件公开只表达“当前最佳答案已不可用”，不泄露案件、举报者或内部原因。

## 九、通知与定位

新增通知定义：

- `QuestionAnswered`：发送给问题作者；自己的回答不通知自己。
- `AnswerAccepted`：发送给新采纳回答作者。
- `AnswerAcceptanceRevoked`：发送给被撤销 / 被替换的回答作者；治理清除使用净化文案。

统一目标：

- `TargetKind = ForumPost`
- `TargetData.PostPublicId`
- `TargetData.AnswerPublicId`
- 旧 `PostId / AnswerId` 只作为兼容数据，不由新前端生成。

可靠性：

- 每个事实使用稳定 BusinessKey；重复消费不生成重复通知。
- `QuestionAnswered` 可按问题帖子形成收件箱分组，但权威通知事实仍逐笔保存。
- 发送前复核接收者账号、目标状态和双向屏蔽；抑制不回滚回答或采纳。
- 从通知进入后，客户端分页加载直到定位目标；目标失效时停在帖子并显示通用不可用状态。

## 十、API 与 HTTP 契约

正式新契约使用字符串标识：

| 方法 | 路径 | 说明 |
| --- | --- | --- |
| `GET` | `/Question/GetSummary?postIdentifier=...` | 问题与采纳摘要 |
| `GET` | `/Question/GetAnswers?postIdentifier=...&pageIndex=1&pageSize=20&sort=default` | 回答分页 |
| `POST` | `/Question/Answer` | 创建回答 |
| `PUT` | `/Question/Answer/{answerPublicId}` | CAS 编辑回答 |
| `DELETE` | `/Question/Answer/{answerPublicId}` | 幂等软删除 |
| `GET` | `/Question/Answer/{answerPublicId}/Revisions` | 受权版本列表 |
| `GET` | `/Question/Answer/{answerPublicId}/Revisions/{revisionNumber}` | 受权版本详情 |
| `POST` | `/Question/Answer/{answerPublicId}/Restore` | CAS 恢复为新版本 |
| `PUT` | `/Question/Acceptance` | 采纳或替换 |
| `DELETE` | `/Question/Acceptance` | 撤销采纳 |

DTO 规则：

- 正式 Web 提交 `postIdentifier / answerPublicId`，不新增 LongId 暴露。
- 创建 / 编辑 / 恢复 / 采纳均携带 `clientSubmissionId`。
- 编辑 / 删除 / 恢复携带 `expectedContentRevision`。
- 采纳 / 替换 / 撤销携带 `expectedAcceptanceRevision`。
- 所有响应使用 `Vo`、字符串 PublicId、结构化 `Code / MessageKey / MessageArguments / TraceId`。
- 旧 `PostId / AnswerId` 接口在过渡期只供旧客户端兼容，标记废弃；F4-O 不立即物理删除。

`@radish/http` 提供统一 answer summary、page、revision、write-result 和 acceptance 类型；`radish.client` 不新增本地 fetch 封装。

## 十一、正式 Web 与 Pencil

### 11.1 设计源

先更新 `public-web-unified-experience.pen`：

- `P04 / P11`：回答分页、已采纳摘要、回答锚点和动作层级；
- 新增 PC / mobile 回答编辑、历史、删除确认、采纳替换 / 撤销与目标不可用状态画板；
- 治理工作台复用既有 Console Case 画板，只补 `PostAnswer` 目标摘要和定位。

### 11.2 页面

- 回答区顺序固定为：问题状态 -> 已采纳回答 -> 其他回答分页 -> 回答 composer -> 帖子讨论区。
- 已采纳回答只展示一次，不在普通分页中重复。
- 回答卡提供作者资料、创建 / 编辑时间、版本摘要和按权限裁剪的编辑、历史、删除、举报、采纳动作。
- PC 使用行内次要动作与受控 Dialog；mobile 使用 Bottom Sheet，不压缩成密集按钮行。
- 通知定位加载目标所在页并一次性高亮；Back / Forward 保留页码和 answer 定位状态。
- 长回答、附件、空态、失效态、CAS 冲突、提交中、响应丢失重试和 reduced-motion 均需明确状态。
- 四主题只使用语义 token；`zh / en` 使用独立资源，不按中文消息控制业务。

## 十二、migration 与兼容

### 12.1 Main migration

计划 migration：`20260727_015_forum_answer_lifecycle`。

Apply：

1. 为 `PostAnswer` 增加 PublicId、内容版本、编辑次数与治理可见字段；
2. 为 `PostQuestion` 增加采纳版本与采纳内容版本；
3. 创建 `PostAnswerContentRevision / PostAnswerAcceptanceEvent` 及索引；
4. 为所有历史回答生成唯一 `ans_` PublicId；
5. 根据当前回答正文生成基线 Revision；
6. 为当前有效采纳生成基线采纳状态；矛盾数据在 preflight 明确失败，不猜测修复；
7. 把可证明属于回答且仍被正文引用的旧 `Comment` 附件重分类为 `PostAnswer`；
8. 无法证明附件完整性的回答基线标记 `LegacyIncomplete`；
9. 保留原 LongId 与旧客户端读取兼容。

Verify：

- 列、表、唯一约束、分页索引和 schema version 齐全；
- 所有回答 PublicId 格式有效且唯一；
- 每个回答存在当前 Revision，版本号与实体一致；
- 当前采纳指向有效同帖回答，`IsSolved / IsAccepted / AcceptedAnswerContentRevision` 一致；
- `AnswerCount` 等于当前可见回答数；
- 回答附件不再以新写入形式占用 `Comment` 业务类型；
- SQLite / PostgreSQL 重复 apply 无额外副作用。

### 12.2 运行时兼容

- 先 apply / verify，再部署读取新字段的应用。
- 旧 `VoAnswers` 在过渡期可返回首屏兼容数据，但新页面只消费分页契约。
- 旧 LongId 请求只在明确兼容入口解析，不写入新通知和外部 URL。
- 回滚应用版本时保留新列、Revision、事件和 PublicId，不删除历史事实。

## 十三、F4-O A-D 批次

### F4-O-A：候选审计与权威设计

- 对比问答、投票、抽奖、圈子和匿名公开聊天候选；
- 核对回答模型、写入、附件、治理、通知、正式 Web 和 migration 事实；
- 固定本文的数据、接口、事务、页面、验证与停止线；
- 完成后汇报，等待明确批准进入 F4-O-B。

### F4-O-B：服务端与 migration

- **完成状态（2026-07-27）**：模型、事务、migration、治理申诉、可靠通知、HTTP 契约与专项回归已落地；SQLite 与本地开发基线通过，PostgreSQL 条件回归留待具备数据库环境的候选门禁执行。
- 新增模型字段、Revision / Event、专属 Service / Repository 和 migration；
- 完成分页、创建、CAS 编辑 / 删除 / 恢复、采纳 / 替换 / 撤销；
- 扩展回答附件、治理、申诉、通知 Outbox、HTTP 与 `@radish/http`；
- 覆盖 SQLite / PostgreSQL、租户、并发、幂等、迁移和失败回滚；
- B 批不提前实现正式页面。

预计修改范围：

- `Radish.Model`：`PostAnswer / PostQuestion`、新 Revision / Event、DTO / Vo、通知目标与定义；
- `Radish.Shared`：治理枚举、附件业务类型、提交操作 / 结果和稳定错误码；
- `Radish.IService / Radish.IRepository`：问答专属接口；
- `Radish.Service / Radish.Repository`：问答写入 / 查询、治理第六目标、通知 Outbox 与定位；
- `Radish.Api`：`QuestionController` 新契约和旧入口兼容；
- `Radish.DbMigrate`：`20260727_015_forum_answer_lifecycle`、注册、apply / verify；
- `Frontend/radish.http`：正式 answer / revision / acceptance 类型与客户端方法；
- `Radish.Api.Tests / radish.http tests / HttpTest`：事务、迁移、治理、通知与契约回归。

B 批不安装或更新依赖，不修改 Pencil 和 `radish.client` 页面；若实现中发现必须扩大到这些范围，应先停止并重新说明。

### F4-O-C：Pencil 与正式 Web

- 先更新 PC / mobile 权威 Pencil；
- 实现回答分页、作者生命周期、采纳变更、历史恢复、举报和通知定位；
- 覆盖四主题、双语、键盘、焦点、窄屏、长正文、附件与冲突态；
- 正式 Web 与 WebOS 只复用共享组件，不建设第二套业务。

### F4-O-D：Gateway 成组验收

- 重新取得当前任务服务启动授权；
- 覆盖匿名、提问者、回答者、第三方、管理员、申诉处理者和被屏蔽双方；
- 验收创建、分页、编辑、恢复、删除、采纳 / 替换 / 撤销、治理限制 / 恢复和通知定位；
- 清理帖子、回答、Revision、事件、举报、通知、Outbox、附件、浏览器和备份；
- 检查六库完整性并执行严格 migration verify，形成批次记录后关闭 F4-O。

## 十四、验证矩阵

### 14.1 服务端

- 同键同摘要重放返回同一回答；同键异摘要冲突。
- 不同键并发创建不丢回答、不漂移计数；频率限制保持稳定。
- 跨租户、帖子不可见、账号不可用、禁言和双向屏蔽均不产生写入。
- 编辑 / 恢复 CAS 冲突不覆盖新内容；无变化不新增 Revision。
- 已采纳回答不能被普通作者编辑或删除。
- 并发采纳 / 替换 / 撤销只有一个版本成功，不出现双采纳。
- 治理限制已采纳回答时原子清除采纳；恢复不自动重采纳。
- 回答、Revision、附件、摘要、提交终态和 Outbox 任一步失败整体回滚。
- 通知重复消费、防屏蔽投递和目标失效均保持业务事实不变。
- migration 基线、重入、严格 verify、SQLite / PostgreSQL 均通过。

### 14.2 正式 Web

- 匿名登录回流准确回到回答 composer。
- 分页、默认排序、最新排序和已采纳置顶无重复 / 漏项。
- 作者编辑、历史、恢复、删除与被采纳冻结状态清晰。
- 提问者采纳、替换、撤销及 CAS 冲突反馈清晰。
- 举报、治理目标失效和申诉恢复后状态符合权限。
- 通知能定位具体 answer PublicId；目标失效时安全降级。
- `zh / en × PC / mobile`、四主题代表矩阵、长名字、长正文和附件不溢出。
- 键盘、焦点返回、Dialog / Bottom Sheet、reduced-motion 可用。

## 十五、停止线

- 不新增回答投票、复杂排序、悬赏、萝卜币、奖励、排行榜或勋章。
- 不把 `PostAnswer` 伪装成 `Comment` 继续复用附件、版本或治理类型。
- 不把回答并入 Post / Comment Revision，也不建设通用万能 Revision 表。
- 不允许客户端决定作者、租户、采纳版本或治理状态。
- 不允许普通作者改写 / 删除当前已采纳答案。
- 不因治理恢复自动恢复旧采纳或覆盖提问者的新选择。
- 不建立独立回答公开页面、独立问答 App 或 Console 运营中心。
- 不全量迁移其他对象 PublicId，不物理删除旧 LongId 兼容字段。
- 不扩展 Flutter、Tauri 或 WebOS 专用功能。
- 不在 F4-O-A 后直接编码；必须先汇报预计范围并等待批准进入 B 批。

## 十六、关联文档

- [当前进行中](/planning/current)
- [开发路线图](/development-plan)
- [发布后维护与功能完成线](/planning/post-release-maintenance-feature-completion)
- [论坛问答 MVP](/features/forum-qa-mvp)
- [论坛内容发布可靠性与编辑历史治理](/guide/forum-content-write-reliability-governance)
- [论坛内容版本完整性与作者恢复](/features/forum-content-version-recovery-design)
- [内容治理案件、证据与动作一致性](/features/content-moderation-case-evidence-action-design)
- [内容治理申诉与处置纠正](/features/content-moderation-appeal-relief-design)
- [通知中心深化与通知治理](/features/notification-center-deepening)
- [用户屏蔽与关系交互隔离](/features/user-block-relationship-isolation-design)
- [文件上传设计](/features/file-upload-design)
- [验证基线说明](/guide/validation-baseline)
