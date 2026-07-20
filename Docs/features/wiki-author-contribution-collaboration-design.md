# Docs / Wiki 普通作者贡献与协作设计

> 状态：`F4-G-A / B / C` 已完成，下一顺位进入 `F4-G-D` 成组验收与关闭。
>
> 最后更新：2026-07-20
>
> 本文负责普通作者、协作者、工作草稿、审核应用和冲突恢复的长期边界。公开阅读、固定文档同步和 Console 既有治理仍以 [文档系统方案](/guide/document-system) 为准。

## 一、结论摘要

F4-G 将现有“管理员可以维护文档”补成“登录用户可以贡献、协作者可以共同维护、治理人员可以审核应用”的完整路径，但不进入实时多人编辑。

核心裁决如下：

1. `WikiDocument` 继续只承载当前已批准的权威正文、公开可见性和发布生命周期。
2. 普通作者的未审核修改进入独立 `WikiDocumentDraft`，不得提前覆盖已发布正文。
3. 文档所有权使用显式 `OwnerUserId`，不得把审计字段 `CreateId` 长期复用为权限来源。
4. 协作关系使用独立 `WikiDocumentCollaborator`；公开阅读的 `AllowedRoles / AllowedPermissions` 不承担编辑授权。
5. 草稿保存使用 `ExpectedDraftVersion`，审核应用同时校验 `BaseDocumentVersion`，任何版本冲突都返回 `409`，不静默覆盖。
6. 普通作者不能发布、归档、恢复、改变访问策略或处理审核；这些动作继续归 Console 权限体系。
7. 审核通过只负责把草稿原子应用到权威正文并生成版本；新文档是否发布仍由既有发布动作决定。

## 二、为什么现在推进

F4-G-A 审计时，系统已经具备公开 `/docs`、正式作者页面 `/docs/mine|compose|edit|revisions`、版本历史、Markdown 导入导出和 Console `/documents` 治理，但存在两个相互关联的真实缺口：

- `WikiController.Create / Update` 当时仍使用 `SystemOrAdmin`，前端作者入口也只识别 `Admin/System`；普通用户没有贡献路径。
- `UpdateWikiDocumentDto` 不提交期望版本，服务端读取后直接更新并递增 `Version`；两个编辑者并发保存时，后写可能覆盖先写。

仅把 Controller 策略改为登录用户会制造越权和未审核内容外泄；仅补 `ExpectedVersion` 又不能完成普通作者路径。因此本专题同时治理所有权、协作者、工作草稿、审核与乐观并发。F4-G-C 已完成消费者迁移并删除旧 `Create / Update` HTTP 写入口，正式页面只使用 Author / Review 契约。

## 三、目标与非目标

### 3.1 目标

- 登录用户可以创建自己的文档草稿，持续保存并提交审核。
- 所有者可以通过用户 `PublicId` 邀请一名或多名编辑协作者；受邀者明确接受后获得编辑权。
- 所有者和已接受的编辑者共享同一份活跃工作草稿。
- 治理人员可以查看待审核草稿、请求修改、驳回或批准应用。
- 已发布文档在审核期间继续展示最后一次已批准正文。
- 保存、提交和审核在重复请求、并发请求、撤销邀请和权限变化下保持一致。
- 正式 Web 同时覆盖 PC 与 mobile，中英文、键盘和无障碍状态完整。

### 3.2 非目标

- 不做光标、选区、在线成员或逐字实时同步。
- 不引入 OT、CRDT、自动三方合并或块级冲突合并。
- 不改为富文本或块编辑器，继续使用 Markdown。
- 不做匿名投稿、公开评论审核或开放式 Wiki 审批市场。
- 不回拉本地目录监听、WebDAV、Git 或云盘双向同步。
- 不开放普通作者直接发布、归档、恢复、设置角色可见性或权限键。
- 不为 WebOS、Flutter 或 Tauri 建立独立实现；WebOS 只复用正式 Web 应用。

## 四、权威对象与真相源

| 对象 | 权威职责 | 不承担 |
| --- | --- | --- |
| `WikiDocument` | 已批准正文、Slug、目录位置、访问策略、发布 / 归档 / 删除状态、当前正式版本 | 普通作者未审核修改、协作者邀请状态 |
| `WikiDocumentDraft` | 唯一活跃工作草稿、草稿版本、基准正式版本、审核状态与提交摘要 | 公开读取、sitemap、浏览历史、最终版本历史 |
| `WikiDocumentCollaborator` | 文档编辑邀请、接受状态、撤销与操作者审计 | 公开阅读权限、发布权限、Console 权限 |
| `WikiDocumentReviewEvent` | 提交、撤回、请求修改、驳回、批准的追加式审核留痕 | 当前正文或草稿快照的重复真相源 |
| `WikiDocumentRevision` | 每次正式正文变化后的不可变版本快照 | 可继续编辑的工作副本 |
| `AllowedRoles / AllowedPermissions` | 已发布文档的读取访问控制 | 作者、所有者或协作者判断 |

`BuiltIn / LocalMirror` 继续由源文件和同步流程负责，不能创建普通作者草稿、协作者或审核单。

## 五、数据模型

### 5.1 `WikiDocument` 补充字段

- `OwnerUserId: long?`：普通作者文档的稳定所有者。`BuiltIn / LocalMirror` 以及无法安全归属的历史文档允许为空，由 Console 管理。
- `ActiveDraftId: long?`：当前活跃草稿的稳定指针；创建和关闭草稿必须在事务中维护。

`Status` 继续只使用 `Draft / Published / Archived`，不把审核状态塞入既有发布状态。

历史数据迁移规则：

- `Custom / ImportedSnapshot` 且 `CreateId` 能解析为当前租户有效用户时，回填为 `OwnerUserId`。
- 无法解析、系统导入或来源不明确的记录保持空所有者，不猜测归属。
- `BuiltIn / LocalMirror` 保持空所有者。
- 迁移不得改变现有 `Status / Visibility / Version` 或公开读取结果。

### 5.2 `WikiDocumentDraft`

首批至少包含：

- `TenantId / DocumentId`
- `BaseDocumentVersion / DraftVersion`
- `Title / Slug / Summary / MarkdownContent / CoverAttachmentId`
- `ProposedParentId`：作者只提交目录建议，最终目录位置由审核者确认。
- `ReviewState`：`Editing / Submitted / ChangesRequested / Applied / Rejected / Withdrawn`
- `ChangeSummary / SubmittedAt / SubmittedBy`
- `ReviewedAt / ReviewedBy / ReviewComment`
- `PayloadPurgedAt`：终态草稿正文按保留策略清理后的明确标记。
- 创建、修改和软删除审计字段

一个 `WikiDocument` 同一时间只能存在一份活跃草稿。`Editing / Submitted / ChangesRequested` 视为活跃，终态草稿不再接受保存。

新建贡献时，在同一事务创建 `WikiDocument(Status=Draft, Version=0)` 和首份 `WikiDocumentDraft`。`WikiDocument` 负责稳定标识和 Slug 占位，但作者列表与编辑器必须读取草稿正文；公开读取仍因 `Draft` 状态不可见。首次审核 Apply 将正式版本从 `0` 推进到 `1` 并生成第一条 `WikiDocumentRevision`，不为未审核内容制造虚假正式版本。

### 5.3 `WikiDocumentCollaborator`

首批只提供 `Editor` 一种协作者角色，避免再造与可见性重复的 Viewer 权限。

字段至少包含：

- `TenantId / DocumentId / UserId / Role`
- `InviteState`：`Pending / Accepted / Declined / Revoked`
- `InvitedBy / InvitedAt / RespondedAt / RevokedBy / RevokedAt`
- 创建、修改和软删除审计字段

同一租户、文档和用户只能有一条有效关系。所有者不能把自己加入协作者；首批不提供所有权转移。

### 5.4 `WikiDocumentReviewEvent`

审核事件只追加，不原地重写历史，至少记录：

- `TenantId / DocumentId / DraftId`
- `Action`：`Submit / Withdraw / RequestChanges / Reject / Apply`
- `ActorUserId / ActorName / Comment`
- `DocumentVersion / DraftVersion / CreateTime`

批准应用后，正式正文快照仍写入既有 `WikiDocumentRevision`；Review Event 只解释谁在何时做了什么审核决定。

## 六、权限矩阵

| 动作 | 所有者 | Accepted Editor | Console Reviewer | Console Publisher | Admin/System 恢复权限 |
| --- | --- | --- | --- | --- | --- |
| 查看共享草稿 | 是 | 是 | 是 | 按治理查看权限 | 是，必须留痕 |
| 保存草稿正文 | 是 | 是 | 否 | 否 | 仅恢复场景 |
| 邀请 / 移除协作者 | 是 | 否 | 否 | 否 | 是，必须留痕 |
| 接受 / 拒绝邀请 | 被邀请用户 | 被邀请用户 | 否 | 否 | 否 |
| 提交 / 撤回审核 | 是 | 否 | 否 | 否 | 是 |
| 请求修改 / 驳回 / 批准应用 | 否 | 否 | `console.docs.review` | 否 | 是 |
| 发布 / 下架 | 否 | 否 | 否 | `console.docs.publish` | 是 |
| 访问策略 / 归档 / 删除 / 恢复 | 否 | 否 | 否 | 按既有权限 | 是 |

补充规则：

- 任意登录用户可以进入作者工作区并创建自己的草稿，不再通过角色名控制入口。
- 编辑者不能提交审核，避免协作者绕过所有者的最终确认。
- 邀请只能使用用户 `PublicId`，不得通过邮箱、内部 LongId 或模糊搜索暴露用户身份。
- 被内容治理禁止发布的用户仍可读取和保存已有私有草稿，但不能新建贡献或提交审核。
- 访问无权草稿统一返回不泄露存在性的 `404`；已确认存在但状态冲突的动作返回 `409`。
- 所有者创建新文档或为既有文档开启下一份草稿前，必须通过服务端容量校验；默认每名用户最多拥有 `20` 份活跃草稿，每篇文档最多 `20` 名 Pending / Accepted 协作者。
- Markdown 正文按 UTF-8 字节数限制，默认上限 `1 MiB`；上限和活跃草稿 / 协作者数量分别由 `Document.Authoring.MaxMarkdownUtf8Bytes / MaxActiveOwnedDrafts / MaxCollaboratorsPerDocument` 配置，不能只靠前端限制。

## 七、状态与事务

### 7.1 草稿状态流

```text
Editing -> Submitted -> Applied
   |           |
   |           +-> ChangesRequested -> Submitted
   |           +-> Rejected
   +-> Withdrawn

Submitted -> Withdrawn
ChangesRequested -> Withdrawn
```

- 只有 `Editing / ChangesRequested` 可以保存。
- 只有所有者可以从 `Editing / ChangesRequested` 提交。
- `Submitted` 期间正文只读；所有者可以撤回后继续编辑。
- `Applied / Rejected / Withdrawn` 为终态，再次修改必须创建新草稿。
- 转入 `Applied / Rejected / Withdrawn` 时必须在同一事务清除 `WikiDocument.ActiveDraftId`；`RequestChanges` 仍保持当前活跃草稿。

### 7.2 草稿保存

请求必须提交 `ExpectedDraftVersion`。Repository 使用条件更新：

```text
WHERE Id = draftId
  AND DraftVersion = expectedDraftVersion
  AND ReviewState IN (Editing, ChangesRequested)
```

成功时 `DraftVersion + 1`；未命中时重新读取并区分不存在、失权、状态冲突或版本冲突。Service 禁止通过普通 `UpdateAsync` 模拟并发控制。

### 7.3 审核应用

批准应用必须在同一数据库事务内完成：

1. 条件锁定仍为 `Submitted` 且版本匹配的草稿。
2. 校验 `WikiDocument.Version == BaseDocumentVersion`。
3. 将草稿的标题、Slug、摘要、Markdown 正文和封面复制到 `WikiDocument`，按审核者最终确认写入目录位置；`Status / Visibility / AllowedRoles / AllowedPermissions` 保持不变。
4. `WikiDocument.Version + 1`，写入 `WikiDocumentRevision`。
5. 草稿转为 `Applied`，清除 `ActiveDraftId`，追加 Review Event。
6. 使用可靠通知业务键生成审核结果通知。

任一步失败必须整体回滚。正式版本已变化时返回 `Wiki.DocumentVersionConflict`，保留草稿和提交状态供审核者处理，不自动合并。

### 7.4 幂等与通知

- 邀请、接受、拒绝、移除、提交、撤回和审核动作采用目标状态语义；重复提交相同目标状态返回当前结果。
- 相互冲突的并发动作只允许一个条件更新成功，其余返回稳定 `409`。
- 邀请、请求修改、驳回和批准复用现有通知中心与可靠任务，不建立 Wiki 私有通知表。
- 通知业务键至少包含文档、草稿、动作和目标用户，重放不得产生重复通知。
- 通知只携带文档标题、安全摘要和结构化站内目标，不携带草稿正文。

## 八、API 边界

### 8.1 Author API

Author API 全部要求 `AuthorizationPolicies.Client`，并在 Service 内按所有者 / 协作者关系授权：

- `GET Wiki/AuthorGetList`
- `GET Wiki/AuthorGetById/{documentId}`
- `POST Wiki/AuthorCreate`
- `POST Wiki/AuthorStartDraft/{documentId}`
- `PUT Wiki/AuthorSaveDraft/{draftId}`
- `POST Wiki/AuthorSubmitDraft/{draftId}`
- `POST Wiki/AuthorWithdrawDraft/{draftId}`
- `GET Wiki/AuthorGetCollaborators/{documentId}`
- `POST Wiki/AuthorInviteCollaborator/{documentId}`
- `POST Wiki/AuthorRespondInvitation/{collaboratorId}`
- `POST Wiki/AuthorRemoveCollaborator/{collaboratorId}`

`AuthorCreate` 创建新文档身份与首份草稿；既有文档在没有活跃草稿时，由所有者通过 `AuthorStartDraft` 从当前正式版本创建下一份草稿。现有含义模糊的 `Create / Update` 不得与新 Author API 长期形成两套正文写入口。F4-G-B 完成迁移后应删除旧作者写入口或收敛为新服务的短期兼容转发，并在同一专题结束前移除兼容层。

### 8.2 Console API

- `GET Wiki/AdminGetReviewQueue`
- `GET Wiki/AdminGetDraftById/{draftId}`
- `POST Wiki/AdminReviewDraft/{draftId}`

审核请求携带目标动作、`ExpectedDraftVersion`、`ExpectedDocumentVersion`、审核意见和最终目录建议。`Apply` 只应用已批准正文；发布仍调用既有 `Publish`。

### 8.3 返回契约与错误

详情契约必须明确返回：

- 当前用户的 `VoAuthorRole / VoCanEdit / VoCanSubmit / VoCanManageCollaborators`
- `VoDraftId / VoDraftVersion / VoBaseDocumentVersion / VoReviewState`
- 正式 `VoVersion / VoStatus / VoVisibility`
- 只读原因和允许动作，不让前端通过角色名或 `SourceType` 猜测权限

稳定错误至少包括：

- `Wiki.AuthorAccessDenied`
- `Wiki.DraftNotFound`
- `Wiki.DraftVersionConflict`
- `Wiki.DocumentVersionConflict`
- `Wiki.DraftStateConflict`
- `Wiki.CollaboratorAlreadyExists`
- `Wiki.CollaboratorNotAccepted`
- `Wiki.ActiveDraftLimitReached`
- `Wiki.CollaboratorLimitReached`
- `Wiki.MarkdownTooLarge`
- `Wiki.ReviewCommentRequired`
- `Wiki.AuthorPublishingBlocked`

所有错误提供中英文 `MessageKey`，页面控制流不得匹配中文 `MessageInfo`。

## 九、正式 Web 与 Console 页面

### 9.1 Author 页面族

- `/docs/mine`：展示“我拥有的 / 与我协作的”，按草稿和审核状态筛选。
- `/docs/compose`：创建文档身份与首份工作草稿；匿名用户走统一登录回流。
- `/docs/edit/:id`：只编辑工作草稿，顶部明确显示所有者、协作状态、正式版本和草稿版本。
- `/docs/revisions/:id`：继续展示已批准版本，不把每次草稿保存伪装成正式版本。

PC 使用编辑器主区与协作 / 审核侧栏；mobile 使用同一页面结构，将协作者和审核时间线放入共享 Bottom Sheet 或抽屉。不得建立第二套移动 Author App。

版本冲突时必须：

- 保留当前本地文本，不清空编辑器。
- 展示服务器草稿版本与当前正式版本。
- 提供复制本地内容、下载 Markdown、重新载入服务器版本三个明确动作。
- 不声称已经自动合并。

### 9.2 Console `/documents`

既有文档治理页增加待审核队列和草稿详情，不新建平行治理应用。审核详情应同时显示：

- 所有者、协作者、提交说明和审核历史。
- 当前正式版本与草稿基准版本。
- Markdown 差异或至少安全的双栏正文对照。
- 请求修改、驳回、批准应用动作及权限原因。

Console mobile 顺序固定为“队列 -> 正文证据 -> 版本与权限 -> 审核动作 -> 留痕”，与既有治理工作台语义一致。

### 9.3 i18n、键盘与无障碍

- 系统状态、动作、冲突、空态和通知使用 `docs.* / documents.*` 中英文资源。
- 标题、正文、提交说明和审核意见属于用户内容，不翻译。
- 英文协作者数量、版本数量和审核事件数量使用 plural 规则。
- 编辑器、协作者弹层和审核弹窗必须支持 Tab 顺序、Escape 关闭、焦点返回和可见焦点。
- 保存、提交和审核中的 loading / disabled / error 状态必须有文本或 `aria-live`，不能只靠颜色表达。

## 十、隐私、保留与不污染边界

- 草稿正文不进入公开 API、公开 head、sitemap、搜索索引、分享卡片或浏览历史。
- 协作者列表只对所有者、已接受协作者和有治理查看权限的人员开放。
- 被撤销协作者立即失去草稿读取与保存权；已打开页面的下一次保存必须失败，不能依赖前端隐藏。
- 活跃草稿不自动过期。`Applied / Rejected / Withdrawn` 的正文载荷默认保留 90 天，之后由清理任务删除正文载荷；审核事件元数据和已批准 Revision 保留。
- 保留天数使用 `Document.Authoring.TerminalDraftRetentionDays` 配置，默认 `90`，不得散落硬编码。
- 文档软删除不物理级联清理审核证据；正式物理清理必须另行定义保留和审计策略。
- 本专题不改变帖子、评论、圈子、聊天、宠物或 Console 其他治理对象。

## 十一、迁移、实现边界与预计文件

F4-G-B 使用显式 ledger migration，建议标识为 `20260720_007_wiki_author_collaboration`，同时覆盖 SQLite 与 PostgreSQL：

- 新增 `WikiDocument.OwnerUserId / ActiveDraftId`。
- 新建 Draft、Collaborator、ReviewEvent 表与租户、文档、用户、状态、时间索引。
- 安全回填历史 Custom 文档所有者。
- DbMigrate 首次执行、重入和严格 verify 检查表、列、索引与回填结果。

主要预计修改范围：

- 后端：`Radish.Model` Wiki 实体 / DTO / Vo、`Radish.IRepository`、`Radish.Repository`、`Radish.IService`、`Radish.Service`、`WikiController`、AutoMapper、通知类型与 DbMigrate。
- Author：`Frontend/radish.client/src/docs`、`apps/wiki`、中英文资源和相关测试。
- Console：`wikiGovernanceApi`、`DocumentGovernancePage`、权限键和中英文资源。
- 测试：Wiki Controller / Service / Repository / migration、通知幂等、前端路由 / 展示 / API 契约。

不得因既有 `WikiApp.tsx` 或 Service 文件较大，就在本专题顺手进行无关全量重构；只在承接草稿、协作和审核边界时按职责拆分必要模块。

## 十二、A-D 开发批次

### F4-G-A：专题裁决与权威设计

- 完成四候选域交叉审计。
- 固定本文的数据、权限、并发、页面、停止线和验证口径。
- 不修改业务代码、接口或 migration。

### F4-G-B：服务端权威契约

- 落地实体、migration、专属 Repository、Service、Author / Console API、权限与稳定错误。
- 落地草稿 CAS、审核应用事务、目标状态幂等和可靠通知。
- 完成 SQLite / PostgreSQL migration、并发和多租户测试。

### F4-G-C：Author / Console 正式页面

- 更新正式 Author 页面与 Console 审核工作台。
- 完成 PC / mobile、中英文、键盘、无障碍、冲突恢复和账号卸载隔离。
- WebOS 只复用同一 Wiki 应用，不增加专属状态模型。

### F4-G-D：成组验收与关闭

- 使用普通所有者、普通协作者、普通无权用户和 Console 审核者覆盖完整矩阵。
- 在 Gateway 正式路径复核 PC / mobile、中英文、邀请接受、并发保存、提交、请求修改、批准应用、发布和失权。
- 清理临时账号、邀请、草稿、审核事件和通知，复核六库与 migration verify。
- 更新规划、专题状态和批次记录，关闭 F4-G。

## 十三、验证矩阵

### 13.1 代码与数据

- SQLite / PostgreSQL 首次迁移、重入、历史所有者回填和严格 verify。
- 两个编辑者使用同一 `ExpectedDraftVersion` 保存时只允许一个成功。
- 审核期间正式版本被改变时，Apply 必须失败且不产生 Revision 或通知。
- Apply 的正文、Version、Revision、ReviewEvent、ActiveDraftId 和通知业务事件同事务一致。
- 邀请接受 / 撤销、所有者 / 编辑者 / 无权用户、跨租户和 BuiltIn 只读矩阵。
- 活跃草稿、协作者数量与 Markdown UTF-8 字节上限只能由服务端配置裁决，边界值和越界错误必须覆盖测试。
- LongId 只以字符串经过 TypeScript 边界，用户邀请只接受 PublicId。

### 13.2 页面

- `/docs/mine|compose|edit|revisions` 的登录回流、刷新和深链恢复。
- `zh / en × PC / mobile` 的列表、编辑、邀请、审核状态、冲突和失败恢复。
- 键盘、焦点、Escape、`aria-live`、禁用态和错误提示。
- Console 待审核队列、正文证据、版本冲突、请求修改、驳回、Apply 与既有 Publish 的分离。
- WebOS 打开同一文档应用时不产生独立草稿或绕过正式权限。

### 13.3 基线与运行态

- 开发中执行定向后端测试、相关前端 test / type-check / build、`validate:baseline:quick`、仓库卫生和 `git diff --check`。
- B 批结束前执行后端全量与 PostgreSQL 专项迁移 / 并发测试。
- 真实 Gateway、PC / mobile 浏览器 smoke 只在 D 批成组验收执行；启动服务前仍需用户当轮明确授权。

## 十四、完成标准

同时满足以下条件才可关闭 F4-G：

1. 普通用户可以完成“创建草稿 -> 邀请协作者 -> 保存 -> 提交 -> 收到审核结果”的完整路径。
2. 治理人员可以完成“查看证据 -> 请求修改或批准应用 -> 单独发布”的完整路径。
3. 未审核正文不会进入任何公开读取面；撤销协作者后服务端立即拒绝继续访问。
4. 草稿保存和审核应用均有数据库级并发保护，不存在读取后无条件覆盖。
5. SQLite / PostgreSQL、正式 Web / WebOS、PC / mobile、中英文、键盘与无障碍矩阵通过。
6. 没有引入第二套文档正文、通知、权限或版本真相源。
