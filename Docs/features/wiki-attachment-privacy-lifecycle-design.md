# F4-L Wiki 附件隐私与生命周期权威闭环

> **状态**：F4-L-A / B / C 已完成；下一顺位进入 F4-L-D 成组验收与专题关闭
>
> **复核日期**：2026-07-26（Asia/Shanghai）
>
> **适用范围**：Main 库 `Attachment` 与 Wiki 聚合、正式 `/docs`、`/docs/mine|compose|edit|revisions`、Console `/documents`、受控附件资源路由和 `@radish/http`
>
> **前置专题**：[文件上传与附件管理](/features/file-upload-design) · [Docs / Wiki 普通作者贡献与协作](/features/wiki-author-contribution-collaboration-design) · [文档系统方案](/guide/document-system)

## 一、结论摘要

F4-L 选择“Wiki 附件隐私与生命周期权威闭环”为当前唯一完整功能专题。

正式 Wiki 作者页已经允许上传 `BusinessType=Wiki` 的图片和文档，Wiki 本身也已经具备 Public / Authenticated / Restricted、Owner、Accepted Collaborator、Reviewer 和 Publisher 权限。然而，现有附件仍把除 Chat 外的上传默认写为 `IsPublic=true`，读取端只判断公开标记、上传者或 `System / Admin`，没有消费 Wiki 文档权限；草稿引用也没有进入孤立附件保护。

核心裁决如下：

1. `Attachment` 继续只承载存储、上传者、文件状态和业务类型，不承担 Wiki 文档可见性真相。
2. 新增 Main 库专属 `WikiAttachmentReference`，作为 Wiki 当前正文、封面、工作草稿和正式 Revision 与附件之间的唯一关系真相。
3. 文档 App 新运行时统一上传 `BusinessType=Wiki` 并立即私有；Wiki 附件是否可读由实时文档状态、引用范围和现有 Wiki ACL 派生，不再长期双写 `Attachment.IsPublic`。
4. Public 且 Published 的当前正文 / 封面引用允许匿名读取；草稿和历史 Revision 不因文档当前公开而自动公开。
5. 草稿保存、审核 Apply、正式版本生成和引用同步处于同一 Main 事务；内容更新成功但引用未更新，或引用更新成功但正文失败，均不允许提交。
6. 被撤销协作者、失去 Restricted 权限、文档归档 / 删除或草稿终态清理后，后续附件请求立即按最新权威状态收口。
7. 访问令牌不建立独立 Wiki 分享权限；消费前必须通过当前 Wiki ACL，未经授权的请求不得消耗访问次数。
8. 孤立清理消费权威引用，覆盖活跃草稿、当前正文、封面和 Revision；未保存上传仍只允许上传者读取，并按既有孤立保留策略回收。
9. 旧 `BusinessType=Wiki` 附件通过显式 migration 转为私有并回填引用；历史 `BusinessType=Document` 只有被 Wiki 正文、封面、草稿或 Revision 明确引用时才作为兼容数据迁入，其他通用 Document 附件保持原边界。跨租户、错误类型或无法安全归属的冲突只报告并阻断，不猜测归属。
10. 本专题不建设匿名公开聊天、论坛版本恢复、内容赞赏、通用附件 ACL 平台、跨节点上传基础设施或新的文档页面族。

## 二、F4-L-A 候选复核

| 候选 | 已有基础 | 真实缺口 | 风险与投入 | 裁决 |
| --- | --- | --- | --- | --- |
| 匿名公开聊天 | 登录态 `/messages` 已覆盖 Public / Announcement、搜索、Reaction、Pin 和回执；Pencil 有 P15 / P16 后置参考 | 没有匿名 `/chat` 产品与协议 | 与论坛讨论重叠，并新增匿名身份、实时限流、滥用治理、保留和 SEO 边界 | 后置，不作为现有聊天补漏 |
| 论坛作者版本恢复 | 帖子 / 评论已有编辑历史和查看页面 | 历史不含分类、标签、封面等完整状态，写入次数和保留条数有限 | 直接恢复会把不完整、可裁剪的审计记录误当版本真相；需要先重建完整 Revision 与 CAS | 后置为独立“论坛版本完整性与恢复”专题 |
| [内容赞赏](/features/forum-content-reward) | 已有萝卜币账本、余额、交易和完整专题草案 | 尚未实现内容级固定金额资产流转 | 产品价值成立，但属于新增资产互动，不是现有功能安全缺口 | 保留为后续候选 |
| Wiki 附件隐私与生命周期 | Wiki 已有公开 / 登录 / Restricted 阅读、作者草稿、协作者、审核和正式附件上传 | Wiki 附件默认公开、无文档 ACL、草稿引用可能被孤立清理，令牌消费早于领域授权 | 直接影响已上线作者态和受限文档的隐私承诺；Main 内可形成稳定事务与迁移边界 | **选定为 F4-L** |

### 2.1 已确认的运行时事实

- `DocsAuthorApp` 与既有 `WikiApp` 都会上传 `BusinessType=Wiki` 的图片和文档。
- 产品层只有一个“文档”App；`WikiDocument / WikiController / BusinessType=Wiki` 是其后端领域命名，附件 `BusinessType=Document` 只是通用文档格式上传分类，不代表第二个文档产品域。
- `AttachmentService` 当前仅把 Chat 新上传设为私有；Wiki 默认公开。
- 受控资源路由虽然统一经过 `AttachmentService`，但非 Chat 只判断 `IsPublic`、上传者和管理角色。
- `WikiDocument` 已有 `Status / Visibility / AllowedRoles / AllowedPermissions / OwnerUserId / ActiveDraftId`。
- `WikiDocumentDraft` 保存未审核 Markdown 与封面，`WikiDocumentRevision` 保存已批准正文快照。
- 当前引用检查会扫描 Wiki 当前正文与 Revision，但没有扫描 Draft，也没有持久化可供授权复用的引用关系。
- Markdown 阅读器把 `attachment://` 直接解析为资源 URL；私有图片需要正式 Web 通过认证二进制读取，不能依赖 `<img>` 自动携带 Bearer Token。

## 三、目标与非目标

### 3.1 目标

- Wiki 新上传从产生时即为私有，不出现“先公开、保存后再收口”的时间窗。
- 当前正文、封面、草稿和 Revision 的附件引用具有可查询、可迁移、可验证的权威关系。
- 匿名、登录读者、Restricted 读者、Owner、Accepted Collaborator、Reviewer、Publisher 与无权用户得到一致访问结果。
- 草稿保存、Apply、发布、可见性变化、协作者撤销、文档归档和删除后，附件访问即时服从最新状态。
- 图片预览、Markdown 阅读、文件下载、灯箱和访问令牌全部经过同一领域授权。
- SQLite / PostgreSQL migration 支持首次应用、重入、旧数据回填、doctor、严格 verify 和备份恢复。
- 孤立附件清理不误删仍被草稿或历史版本引用的文件，也不因历史版本存在而错误公开文件。

### 3.2 非目标

- 不建设任意业务类型可注册的通用 Attachment ACL DSL。
- 不改变 Post / Comment / Chat / Avatar / Sticker / Product 已有附件权限。
- 不实现公开聊天室、论坛版本回滚、内容赞赏或 Wiki 公开评论。
- 不实现跨请求分片会话恢复、attachment session correlation、durable quota settlement。
- 不实现共享临时存储、分布式上传锁、对象存储迁移或 CDN。
- 不进行 Attachment / WikiDocument PublicId 全量 rollout。
- 不新增 Flutter、WebOS 或 Tauri 专属实现；WebOS 只保留既有兼容面。
- 不新增页面族或修改 Pencil；F4-L-C 只补既有页面的受保护资源加载和状态。

## 四、权威对象与真相源

| 对象 | 权威职责 | 不承担 |
| --- | --- | --- |
| `Attachment` | 文件存储信息、上传者、MIME、大小、启用 / 删除状态、业务类型 | Wiki 可见性、协作权限、公开状态 |
| `WikiAttachmentReference` | Wiki 文档、草稿、Revision 与附件的持久引用关系 | 文档正文、草稿状态、用户权限 |
| `WikiDocument` | 当前已批准正文、发布状态、可见性和访问策略 | 未审核附件引用、历史版本引用 |
| `WikiDocumentDraft` | 唯一活跃工作草稿和审核状态 | 公开读取 |
| `WikiDocumentRevision` | 不可变正式版本快照 | 当前公开状态、继续编辑 |
| Wiki 现有权限服务 | 文档读取、作者、协作者和治理权限 | 文件存储与引用同步 |

`Attachment.IsPublic=false` 是 Wiki 附件及已迁入的历史 Document 引用的存储安全基线，不是“当前不可公开”的第二份业务结论。Public Wiki 附件仍由 `WikiAttachmentReference + WikiDocument` 动态授权匿名读取。

## 五、数据模型

### 5.1 `WikiAttachmentReference`

首批字段：

- `Id`
- `TenantId`
- `DocumentId`
- `AttachmentId`
- `ReferenceKind`
- `ReferenceSourceId`
- `IsDeleted / DeletedAt / DeletedBy`
- 创建与修改审计字段

`ReferenceKind` 使用稳定枚举：

- `DocumentContent`
- `DocumentCover`
- `DraftContent`
- `DraftCover`
- `RevisionContent`

`ReferenceSourceId` 的含义由类型固定：

- Document 类型等于 `DocumentId`；
- Draft 类型等于 `WikiDocumentDraft.Id`；
- Revision 类型等于 `WikiDocumentRevision.Id`。

约束与索引：

- 唯一 `(TenantId, ReferenceKind, ReferenceSourceId, AttachmentId)`；
- 查询索引 `(TenantId, AttachmentId, IsDeleted)`；
- 文档清理索引 `(TenantId, DocumentId, ReferenceKind, IsDeleted)`；
- Draft / Revision 来源必须属于同租户、同文档；
- 新写入只允许引用有效、未删除、同租户的 `BusinessType=Wiki` 附件；migration 可为已被 Wiki 内容明确引用的历史 `BusinessType=Document` 建立兼容关系，后续保存只允许保留既有兼容引用，不得把新的通用 Document 附件引入 Wiki。

引用表实现 `IDeleteFilter`。当前正文和草稿保存采用目标集合语义：新增缺少关系、恢复软删除关系、软删除不再引用的关系。Revision 引用在版本生成后不可变。

### 5.2 为什么不用 `Attachment.BusinessId`

一个附件可能同时出现在当前正文、活跃草稿和多个 Revision 中，也可能被同一所有者复用于多篇文档。单个 `BusinessId` 无法表达多对多关系、引用范围和历史可见性。

`BusinessId` 保留兼容，不作为 Wiki 授权真相，也不建立 `BusinessId + IsPublic` 双写。

## 六、上传、绑定与事务

### 6.1 上传阶段

- `BusinessType=Wiki` 新上传立即写 `IsPublic=false`；通用 `BusinessType=Document` 保持既有上传边界。
- 上传阶段不接受客户端指定 `DocumentId / DraftId / RevisionId`。
- 未绑定附件仅上传者本人可通过认证二进制接口读取。
- 上传成功后编辑器可以显示本地预览或认证 object URL，但不能因为尚未保存而临时公开。

### 6.2 草稿创建与保存

Author Service 在现有草稿 CAS 事务内：

1. 从 Markdown 和 `CoverAttachmentId` 提取目标附件集合。
2. 校验附件存在、启用、未删除、租户一致、业务类型允许。
3. 校验当前作者有权把附件引入该文档；首批只允许附件上传者、文档 Owner 或当前 Accepted Collaborator 使用自己有权读取的 Wiki 附件。
4. 条件保存 `ExpectedDraftVersion`。
5. 同步 `DraftContent / DraftCover` 引用目标集合。

正文或引用任一步失败必须整体回滚。Service 不直接访问 Db，复杂集合同步和锁进入专属 Repository。

### 6.3 审核 Apply 与正式版本

在既有 Apply 事务中：

1. 锁定 Submitted 草稿和正式版本。
2. 再次校验 Draft 引用仍有效。
3. 把草稿应用到 `WikiDocument`。
4. 同步 `DocumentContent / DocumentCover` 当前引用。
5. 创建 `WikiDocumentRevision` 后写入对应 `RevisionContent` 引用。
6. 草稿进入 Applied，保留 Draft 引用直到终态正文按既有保留规则清理。

发布、下架、可见性变化、归档和恢复不重写引用，只改变访问策略计算输入。

### 6.4 终态草稿与文档删除

- 终态草稿正文被清理时，同一任务软删除该 Draft 的引用。
- 文档软删除后，当前、草稿和 Revision 引用仍保留审计事实，但访问服务统一拒绝。
- 物理清理引用和文件必须遵循独立保留策略，不在软删除请求内直接删文件。

## 七、读取权限矩阵

| 引用范围 | 匿名 | 普通登录用户 | Owner | Accepted Collaborator | Reviewer / Publisher | Admin / System |
| --- | --- | --- | --- | --- | --- | --- |
| Published + Public 当前正文 / 封面 | 允许 | 允许 | 允许 | 允许 | 允许 | 允许 |
| Published + Authenticated 当前引用 | 拒绝 | 按现有文档读取允许 | 允许 | 允许 | 按现有权限 | 仅按现有 Wiki 权限 |
| Published + Restricted 当前引用 | 拒绝 | 按角色 / 权限 | 允许 | 允许 | 按现有权限 | 仅按现有 Wiki 权限 |
| Draft 当前引用 | 拒绝 | 拒绝 | 允许 | 允许 | 有审核查看权限时允许 | 仅按现有 Wiki 权限 |
| Revision 历史引用 | 拒绝 | 拒绝 | 允许 | 按现有版本查看权限 | 按治理权限 | 仅按现有 Wiki 权限 |
| 未绑定 Wiki | 拒绝 | 仅上传者 | 仅上传者 | 仅上传者 | 拒绝 | 不自动穿透 |

补充规则：

- Pending Invitee 只能查看邀请摘要，不能读取草稿附件。
- 协作者被 Revoked 后，下一次附件请求立即拒绝；已生成 object URL 必须由页面在关系失效或账号切换时撤销。
- 文档 Archived、Deleted、来源只读或目标失效时，按既有 Wiki 查看规则决定当前正文；附件服务不另造宽松 fallback。
- 读取失败统一返回 `404`，不披露附件存在、引用范围、文档可见性或拒绝原因。
- `System / Admin` 角色本身不构成私有 Wiki 附件读取权；必须拥有现有 Wiki 治理查看权限。

## 八、服务与 Repository 边界

先定义接口再实现：

- `IWikiAttachmentReferenceRepository`
  - 同步当前 / 草稿目标集合；
  - 追加 Revision 不可变引用；
  - 批量查询附件引用上下文；
  - 查询迁移与清理所需引用。
- `IWikiAttachmentAccessService`
  - 按租户、当前用户和附件批量判定读取；
  - 复用现有 Wiki 文档、作者和治理权限；
  - 不返回正文或多余关系信息。
- `IAttachmentService`
  - 保持 Controller 唯一依赖；
  - Chat 继续调用现有 Chat ACL；
  - Wiki，以及存在 `WikiAttachmentReference` 的历史 Document，调用 `IWikiAttachmentAccessService`；
  - 其他类型保持既有边界。

Controller 不注入 Repository，Wiki Service 不直接使用 `_repository.Db`，Attachment Service 不扫描 Markdown 或自行拼接 Wiki 联表。

## 九、访问令牌与下载

- 创建、列表和撤销 Wiki 附件令牌时，必须校验调用者拥有当前 Wiki 附件管理权；上传者身份不自动覆盖文档所有权变化。
- `DownloadByToken` 先读取候选 token 和附件，再执行当前 Wiki ACL，最后原子消费次数。
- 未授权、目标失效或文档失权时不增加 `AccessCount`。
- token 只携带附件访问约束，不成为匿名 Wiki 分享链接；匿名 token 只可读取当前本来就允许匿名访问的 Public 文档附件。
- token 消费后、文件流打开前附件被禁用或删除时仍应失败，不返回旧缓存。
- 原始 token、hash、文档权限和引用上下文不得进入日志。

若未来需要“私有文档显式分享链接”，必须单独建立文档级授权、撤销和审计，不得复用本专题 token 偷渡。

## 十、孤立清理与保留

- `AttachmentReferenceInspector` 改为优先查询 `WikiAttachmentReference`，不再为授权重复扫描全部 Wiki 正文。
- 活跃 Draft、终态保留期内 Draft、当前 Document 和全部未清理 Revision 引用都保护附件不被孤立清理。
- Draft 终态正文清理时同步软删除对应 Draft 引用；若附件仍被其他范围引用，继续保留。
- 未绑定附件沿用既有孤立保留期，上传取消、页面关闭或保存失败不立即物理删除。
- 清理任务必须分页、可重入；单条异常不能把仍有权威引用的附件误判为孤立。

## 十一、迁移与恢复

F4-L-B 新增 Main ledger migration：

`main.20260725_012_wiki_attachment_authority`

apply 顺序：

1. 创建 `WikiAttachmentReference`、唯一约束和查询索引。
2. 使用代码与运行时共用的附件引用解析器扫描 WikiDocument 当前正文 / 封面。
3. 扫描 WikiDocumentDraft 正文 / 封面。
4. 扫描 WikiDocumentRevision 正文。
5. 为安全匹配的现有附件回填引用。
6. 把所有 `BusinessType=Wiki` 附件，以及已成功回填 Wiki 权威引用的历史 `BusinessType=Document` 附件设为 `IsPublic=false`；不改写其他通用 Document 附件。
7. 执行严格 verify。

doctor / verify 至少检查：

- 表、列、索引和 checksum；
- Wiki 附件、已迁入历史 Document 引用仍公开的数量；
- 现有附件引用缺少权威关系的数量；
- 引用附件不存在、已删除、跨租户或类型冲突；
- Draft / Revision 来源与 Document 不一致；
- 重复有效关系；
- 无法解析的站内旧附件 URL 单独报告。

迁移规则：

- 不修改 Wiki 文档状态、可见性、正文、草稿状态或 Revision 内容。
- 不把外部 URL 当作站内附件。
- 不把 `BusinessType=Document` 当作第二个 Wiki 域；只有被 Wiki 权威内容明确引用的历史记录才进入兼容迁移。
- 缺失附件只保留原 Markdown 并报告，不伪造 Attachment。
- 跨租户、一个引用指向错误业务类型或来源归属冲突时阻断 apply。
- 首次 apply、第二次 apply、verify、SQLite 备份恢复和 PostgreSQL 事务回滚都必须验证。

## 十二、错误与 HTTP 契约

稳定错误至少包括：

- `WikiAttachment.InvalidReference`
- `WikiAttachment.ReferenceForbidden`
- `WikiAttachment.CrossTenant`
- `WikiAttachment.TypeMismatch`
- `WikiAttachment.ReferenceConflict`
- `WikiAttachment.SourceNotFound`
- `WikiAttachment.AccessUnavailable`

客户端可见拒绝统一使用安全 `404` 或既有结构化上传 / 保存错误。内部 migration 和写入冲突保留稳定 `Code / MessageKey / TraceId`，不得回传 SQL、路径或权限细节。

`Radish.Api.http` 与测试示例覆盖：

- 上传后未绑定读取；
- 保存 Draft 后 Owner / Collaborator 读取；
- Public 发布后匿名读取；
- Restricted 允许 / 拒绝；
- 协作者撤销；
- token 未授权不消耗次数；
- 文档删除和附件禁用。

## 十三、正式 Web

### 13.1 公开阅读

- Public + Published 当前附件继续使用稳定 `/_assets/attachments/{id}` URL，支持公开 Markdown、灯箱和文档下载。
- 服务端每次读取仍核对当前文档状态，不把历史公开结果永久缓存为授权。

### 13.2 Author 与受限阅读

- Author、Authenticated / Restricted 阅读和 Console 审核使用统一认证客户端读取二进制并创建 object URL。
- 图片、灯箱原图和普通文档下载均不得退回匿名直链。
- 页面卸载、账号切换、文档切换、关系失效和重新加载时撤销 object URL。
- 上传完成但尚未保存时仍能预览；保存失败保留编辑器草稿和附件 ID，不伪装绑定成功。
- 读取失败显示中英文稳定状态，不在 alt、toast 或日志中泄露内部路径。

`@radish/ui` 只接受宿主提供的受保护附件加载函数，不直接依赖认证状态或业务 API；公开 Markdown 保持现有直接 URL 行为。

## 十四、A-D 开发批次

### F4-L-A：候选复核与权威设计（已完成）

- 交叉复核公开聊天、论坛版本恢复、内容赞赏和 Wiki 附件治理。
- 固定本文的数据、权限、事务、迁移、页面、停止线和验证口径。
- 不修改模型、接口、业务代码或 Pencil。

### F4-L-B：服务端权威契约（已完成）

- 新增模型、接口、专属 Repository / Service 和 Main migration。
- 接入上传默认值、Draft / Apply 事务、动态读取、token、清理、doctor / apply / verify。
- 补稳定错误、HTTP、`@radish/http` 类型与 SQLite / PostgreSQL 测试。
- 不修改正式页面和 Pencil。

### F4-L-C：正式 Web 受保护资源（已完成）

- 接入 Author、Authenticated / Restricted Docs、Revision 和 Console 审核的认证资源加载。
- 覆盖图片、灯箱、普通文件、加载失败、失权、账号切换和 object URL 回收。
- 完成中英文、PC / mobile、键盘和无障碍；不新增页面族或 Pencil。
- 完成结果见 [F4-L-C 正式 Web 受保护资源记录](/records/f4-l-c-wiki-attachment-web-protected-assets-2026-07-26)。

### F4-L-D：成组验收与专题关闭

- 使用 Owner、Accepted Collaborator、Revoked Collaborator、Restricted Reader、Reviewer 和匿名用户。
- 覆盖未绑定、Draft、Submitted、Applied、Published Public / Authenticated / Restricted、Revision、Archived / Deleted。
- 覆盖图片、文件、封面、token、孤立清理、跨租户、并发保存 / Apply、迁移和失败恢复。
- 覆盖 `zh / en × PC / mobile`、多标签、离线恢复、Back / Forward、键盘和无障碍。
- 清理临时附件、引用、草稿、文档、token、账号与备份，检查六库完整性并执行严格 verify。

## 十五、验证矩阵

| 层级 | 必须覆盖 |
| --- | --- |
| Migration | SQLite / PostgreSQL、首次、重入、旧公开附件、正文 / 封面 / Draft / Revision 回填、冲突拒绝、备份恢复 |
| Repository | 目标集合同步、软删除恢复、Revision 追加、并发保存 / Apply、租户隔离、批量查询 |
| Service | 未绑定、Public、Authenticated、Restricted、Owner、Collaborator、Reviewer、失权、删除和禁用 |
| Token | 创建权限、当前 ACL、未授权不消费、次数并发、撤销、过期、用户 / IP 约束 |
| Cleanup | Draft 保护、终态清理、Revision 保留、多处引用、未绑定回收、分页重入 |
| API | 公开直链、认证二进制、图片 / 文件 / thumbnail、404 防枚举、稳定错误 |
| Client | Markdown、灯箱、下载、上传预览、object URL 回收、账号 / 文档切换、错误本地化 |
| 运行态 | 六类身份、完整文档状态、双语、PC / mobile、多标签、离线、清理与六库完整性 |

## 十六、完成标准

同时满足以下条件后才能关闭 F4-L：

1. Wiki 新上传不再默认公开，通用 Document 上传不被错误套用 Wiki ACL。
2. 当前正文、封面、Draft 和 Revision 引用都有单一权威关系。
3. Public 匿名访问和私有 Wiki ACL 都由最新文档状态决定。
4. Draft 保存与 Apply 不产生正文 / 引用分裂。
5. token、下载、thumbnail 和灯箱不能绕过领域权限。
6. 清理不会误删有效草稿 / Revision 附件，也不会把历史引用误当公开许可。
7. SQLite / PostgreSQL migration、并发、恢复和严格 verify 通过。
8. 正式 Web 与 Gateway 成组矩阵通过，临时数据和备份清理完成。

## 十七、停止线

- 不让 `Attachment.IsPublic` 与 Wiki 可见性长期双写。
- 不把 `Attachment.BusinessId` 升级为多引用真相。
- 不由 Controller 扫描 Markdown、联表或推导文档权限。
- 不由前端隐藏图片模拟服务端 ACL。
- 不让上传者、Admin/System 或访问令牌自动穿透 Wiki 权限。
- 不在 Wiki 查询失败时回退到公开、上传者或历史缓存结果。
- 不因 Revision 保留而公开历史附件。
- 不把迁移冲突静默改成 General、公开或任意 Document。
- 不顺带建设匿名公开聊天、论坛版本恢复、内容赞赏、通用分享链接或全站附件平台。
- 不扩展共享临时存储、分布式锁、durable quota、PublicId、Flutter、WebOS 或 Tauri。
