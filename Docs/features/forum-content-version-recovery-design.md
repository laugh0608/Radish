# F4-M 论坛内容版本完整性与作者恢复

> **状态**：F4-M-A / B / C 已完成；下一顺位进入 F4-M-D 成组验收与专题关闭
>
> **复核日期**：2026-07-26（Asia/Shanghai）
>
> **适用范围**：正式 Web 论坛帖子与评论、Main 数据库、API、`@radish/http` 和 `radish.client`；Flutter / WebOS / Tauri 不新增独立实现
>
> **前置专题**：[论坛编辑历史](/features/forum-edit-history) · [论坛内容发布可靠性与编辑历史治理](/guide/forum-content-write-reliability-governance) · [论坛分类与标签](/features/forum-category-tag) · [文件上传设计](/features/file-upload-design)

## 一、结论摘要

F4-M 选择“论坛内容版本完整性与作者恢复”为当前唯一完整功能专题。

帖子和评论已经具备编辑、编辑次数限制、可靠提交键和编辑历史查看，但现有 `PostEditHistory / CommentEditHistory` 只记录单次编辑前后的标题或正文，不是可恢复的完整版本真相。帖子分类、标签、封面、附件引用没有进入历史，历史写入次数和保留数量还允许小于实际编辑次数；直接在现有历史弹窗增加“恢复”按钮会产生不完整恢复、并发覆盖和审计断裂。

核心裁决如下：

1. 新增不可变的 `PostContentRevision / CommentContentRevision`，保存每次成功提交后的完整作者可编辑状态；`Post / Comment.ContentRevision` 是当前版本号。
2. 帖子版本覆盖标题、正文、内容类型、分类、标签、封面和正文附件引用；评论版本覆盖正文和正文附件引用。
3. 投票选项与结果、问答回答与采纳、抽奖配置与参与结果、点赞、评论数、置顶、精华、治理状态等不属于作者内容版本，不随恢复改变。
4. 普通编辑和版本恢复都必须携带 `ExpectedContentRevision`；服务端通过 CAS 拒绝覆盖已经变化的当前内容。
5. 恢复不是把数据库指针倒退，也不修改旧版本；它以目标快照为输入创建一个新的当前版本，并记录 `RestoredFromRevisionId`。
6. 版本写入、帖子分类计数、标签关系、附件引用、编辑次数和提交意图完成必须处于同一 Main 事务，任一步失败都整体回滚。
7. `PostEditHistory / CommentEditHistory` 保留为历史兼容证据，不把缺失分类、标签或附件状态的旧记录伪装成可恢复版本；新运行时只写 Revision，避免长期双写两套历史真相。
8. migration 为现有帖子和评论生成“当前状态基线版本”；无法证明附件或分类标签完整性的历史基线显式标记为不可恢复，不猜测、不静默丢字段。
9. 完整快照和恢复操作只对当前作者与具备既有管理能力的 `System / Admin` 开放；其他访问者只看到必要的“已编辑”摘要，不再匿名读取旧正文全文。
10. 分类、标签、附件、内容规则或目标状态已经失效时，精确恢复必须拒绝且不产生部分写入；作者仍可把可见快照载入编辑器，修正失效项后按普通编辑提交。
11. 普通作者继续受帖子 / 评论现有编辑次数、评论编辑时间窗和内容规则约束；管理员是否绕过仍由 `ForumEditHistory.AdminOverride` 的既有配置决定。
12. 本专题不建设通用内容版本平台、协同草稿、分支合并、自动保存、公开 diff、匿名编辑、资产补偿或跨端专用实现。

## 二、F4-M-A 候选复核

| 候选 | 已有基础 | 真实缺口 | 风险与投入 | 裁决 |
| --- | --- | --- | --- | --- |
| 论坛内容版本完整性与作者恢复 | Post / Comment 已有编辑、可靠提交、历史模型、查看页面、分类标签与附件绑定 | 现有历史不是完整版本，作者不能安全恢复误改内容，也没有 CAS | 需要独立 Revision、基线迁移、附件存续、权限和页面闭环，但范围集中在既有论坛编辑链 | **选定为 F4-M** |
| [内容赞赏](/features/forum-content-reward) | 已有萝卜币账本、余额、交易和完整专题草案 | 尚未实现内容级固定金额资产流转 | 同时触达资产事务、幂等、通知、举报治理、Console 和三类内容目标；属于新增互动而非现有闭环缺口 | 保留为后续候选 |
| 匿名公开聊天 | 登录态 `/messages` 已覆盖 Public / Announcement、搜索、Reaction、Pin 和回执 | 没有匿名 `/chat` 产品与协议 | 与论坛公开讨论重叠，并新增匿名身份、实时限流、滥用治理、保留和 SEO 暴露面 | 后置，不作为现有聊天补漏 |
| 附件会话与多节点基础设施 | 已有分片上传、孤立清理、Wiki / Chat 专项引用治理 | 缺少跨请求 correlation、durable quota、多节点共享临时存储与锁 | 属于多项可分离基础设施演进，没有当前产品主路径阻断 | 留在维护线，按真实部署需求分别立项 |

### 2.1 已确认的运行时事实

- `PUT Post/Update` 已允许作者和管理员修改标题、正文、分类与标签，并通过 `ForumContentWriteService` 使用 `ForumPostEdit` 提交意图去重。
- `PUT Comment/Update` 已允许作者和管理员修改正文，并通过 `ForumCommentEdit` 提交意图去重。
- `PostEditHistory` 只保存 `Old/New Title + Content`；`CommentEditHistory` 只保存 `Old/New Content`。
- 历史只在 `HistorySaveEditCount` 范围内写入，还会按 `MaxHistoryRecords` 裁剪；它不能代表每个已提交状态。
- `Post.EditCount / Comment.EditCount` 只表达编辑次数，不是并发版本，也不能阻止两个编辑页互相覆盖。
- 帖子无变化判断已经包含分类和标签，证明分类与标签属于作者编辑状态；现有历史却没有保存它们。
- 当前附件引用检查只扫描帖子、评论、回答的当前正文；既有附件绑定后通常保留 `BusinessId`，但没有“某个历史版本引用了哪些附件”的可验证关系。
- 现有 `GetEditHistory` 接口允许匿名读取完整前后正文。作者恢复需要完整快照，但不应继续把已被作者删除的旧正文作为匿名恢复数据面。

### 2.2 为什么本专题优先

该专题直接修复现有功能的语义断层，而不是增加新的平行业务：

- 用户已经可以多次编辑并查看历史，自然预期误改后能够找回一个确认过的版本。
- 可靠提交只解决同一次请求重试，不解决两个设备、两个标签页或旧编辑页面的并发覆盖。
- 当前历史看似“完整快照”，实际缺少分类、标签和附件关系；继续沿用会把审计记录误当版本真相。
- 新增 Revision 后，普通编辑、历史回看、作者恢复和后续内容治理可以消费同一版本号与快照边界。
- 相比内容赞赏，本专题不引入新的资产流转、经济激励和跨域治理面，能够沿既有论坛主链形成纵向闭环。

## 三、目标与非目标

### 3.1 目标

- 每次成功编辑后都有唯一、连续、不可变的完整作者内容版本。
- 作者能够从帖子或评论详情查看受权版本、比较当前状态并恢复到一个可用快照。
- 两个编辑会话并发提交时，后提交者不会静默覆盖先提交结果。
- 恢复后仍保留完整时间线，可以明确看到“由哪个版本恢复而来”。
- 当前分类、标签、附件或内容规则不再允许目标快照时，服务端给出稳定失败语义并保持当前内容不变。
- 现有旧历史得到诚实兼容：可查看既有证据，但只有可证明完整的 Revision 才能恢复。
- SQLite / PostgreSQL migration 支持 doctor、apply、重入、严格 verify、历史基线与备份恢复。
- 正式 Web 同时覆盖 PC 与 mobile，键盘、焦点、长正文、加载、冲突和失权状态可用。

### 3.2 非目标

- 不建设类似 Git 的分支、合并、cherry-pick 或逐行冲突解决。
- 不建设多人协同草稿、自动保存、审核流或定时发布；Docs 协作继续由 Wiki 专题承接。
- 不允许恢复投票选项 / 票数、问答回答 / 采纳、抽奖参与 / 结果、互动计数或治理状态。
- 不把普通编辑次数上限、评论编辑时间窗改造成无限历史编辑。
- 不开放任意用户读取完整旧正文，也不建设公开全文 diff 搜索。
- 不重新设计所有 Post 附件的公开 / 私有策略，不建立通用附件 ACL 平台。
- 不把 `PostEditHistory / CommentEditHistory` 原地扩列成长期 Revision 真相。
- 不新增 Console 页面族、Flutter 页面、WebOS 专属入口或 Tauri 能力。
- 不顺带实现内容赞赏、匿名公开聊天、推荐、PWA 或 PublicId 全量迁移。

## 四、用户路径与产品语义

### 4.1 作者帖子恢复

1. 作者在正式帖子详情打开“版本”。
2. 列表显示版本号、提交类型、操作者、时间、是否当前版本和恢复可用性。
3. 作者打开某一版本详情，对比目标版本与当前版本的标题、正文、分类、标签和封面。
4. 可精确恢复时，确认框明确展示目标版本号和“恢复会创建新版本，不会删除现有历史”。
5. 服务端成功后返回新的当前版本号，详情页刷新为权威状态。
6. 若分类、标签或附件失效，精确恢复不可点击；作者可选择“使用此版本编辑”，在编辑器中修正后按普通编辑提交。

### 4.2 作者评论恢复

- 根评论与子评论使用同一版本契约。
- 恢复入口位于评论既有历史动作附近，移动端进入 Bottom Sheet，不挤压评论主要操作。
- 普通作者仍受评论编辑时间窗和次数上限约束；超过边界只允许受权管理员按既有配置处理。
- 恢复后评论定位、楼层关系、点赞和神评状态不改变；正文变化后的派生展示按现有逻辑重新计算。

### 4.3 公开编辑痕迹

- 匿名和非作者读者可以知道内容已经编辑、最近编辑时间和编辑次数。
- 公开摘要不返回旧标题、旧正文、标签快照、附件引用、内部 Revision Id 或恢复来源。
- 作者和管理员进入受权详情后才能读取完整快照。
- 旧 `GetEditHistory` 的匿名全文读取在 F4-M 内收口；前端迁移到公开摘要和受权详情两层接口。

## 五、权威对象与术语

| 对象 | 权威职责 | 不承担 |
| --- | --- | --- |
| `Post / Comment.ContentRevision` | 当前作者内容版本号、CAS 条件 | 编辑次数、版本正文 |
| `PostContentRevision` | 帖子某次成功提交后的不可变主体快照 | 当前互动、治理和结构化玩法状态 |
| `PostContentRevisionTag` | 版本时的标签身份、名称快照与顺序 | 当前标签是否仍有效 |
| `CommentContentRevision` | 评论某次成功提交后的不可变正文快照 | 评论树关系、点赞和高亮状态 |
| `ForumContentRevisionAttachment` | Post / Comment Revision 与附件的持久引用 | 附件存储状态、公开权限 |
| `PostEditHistory / CommentEditHistory` | F4-M 前的兼容审计证据 | 可恢复版本、当前版本号 |
| `ContentSubmissionRecord` | 编辑 / 恢复提交意图的幂等与重放 | 版本正文、权限 |

术语固定如下：

- **当前版本**：`ContentRevision` 指向的已提交作者内容状态。
- **基线版本**：migration 根据 F4-M 上线时当前实体状态生成的首个 Revision。
- **精确恢复**：目标 Revision 的所有受控字段均可按原状态恢复。
- **使用此版本编辑**：只把受权快照载入本地编辑器，不产生服务端写入。
- **旧历史**：F4-M 前 `PostEditHistory / CommentEditHistory` 的前后差异记录。

## 六、数据模型

### 6.1 当前实体版本号

`Post` 与 `Comment` 新增：

- `ContentRevision`：非空正整数；migration 后当前基线为 `1`。

普通编辑或恢复从 `N` 成功提交后，当前实体与新 Revision 同时写为 `N + 1`。`EditCount` 继续承担业务编辑次数限制，不能替代 `ContentRevision`。

### 6.2 `PostContentRevision`

首批字段：

- `Id`
- `TenantId`
- `PostId`
- `RevisionNumber`
- `SourceType`：`Baseline / Edit / Restore`
- `RestoredFromRevisionId`
- `IntegrityStatus`：`Complete / LegacyIncomplete / Redacted`
- `Title`
- `Content`
- `ContentType`
- `CategoryId`
- `CategoryNameSnapshot`
- `CoverAttachmentId`
- `EditorId / EditorName`
- `CreatedAt`
- 标准创建审计字段

约束与索引：

- 唯一 `(TenantId, PostId, RevisionNumber)`；
- 列表索引 `(TenantId, PostId, RevisionNumber DESC)`；
- 恢复来源索引 `(TenantId, RestoredFromRevisionId)`；
- `Restore` 必须有同帖子、同租户的 `RestoredFromRevisionId`；
- `Baseline / Edit` 不得填写恢复来源；
- 新运行时只允许写 `Complete`；`LegacyIncomplete` 仅由 migration 生成，`Redacted` 仅供后续受权保留治理使用。

### 6.3 `PostContentRevisionTag`

首批字段：

- `Id`
- `TenantId`
- `RevisionId`
- `TagId`
- `TagNameSnapshot`
- `SortOrder`

唯一 `(TenantId, RevisionId, TagId)`。恢复使用 `TagId` 解析当前标签身份，`TagNameSnapshot` 只用于版本展示和失效提示；标签重命名不创建旧名称新标签，标签已删除或禁用时精确恢复拒绝。

### 6.4 `CommentContentRevision`

首批字段：

- `Id`
- `TenantId`
- `CommentId`
- `PostId`
- `RevisionNumber`
- `SourceType`
- `RestoredFromRevisionId`
- `IntegrityStatus`
- `Content`
- `EditorId / EditorName`
- `CreatedAt`
- 标准创建审计字段

唯一 `(TenantId, CommentId, RevisionNumber)`，列表索引 `(TenantId, CommentId, RevisionNumber DESC)`。恢复来源必须属于同评论、同租户。

### 6.5 `ForumContentRevisionAttachment`

首批字段：

- `Id`
- `TenantId`
- `TargetType`：`Post / Comment`
- `TargetId`
- `RevisionId`
- `AttachmentId`
- `ReferenceKind`：`Content / Cover`
- 标准创建审计字段

约束与索引：

- 唯一 `(TenantId, TargetType, RevisionId, AttachmentId, ReferenceKind)`；
- 附件存续索引 `(TenantId, AttachmentId)`；
- 目标版本索引 `(TenantId, TargetType, TargetId, RevisionId)`；
- `Post + Cover` 最多一条，`Comment` 不允许 `Cover`；
- Revision、目标和附件必须同租户，引用目标必须与 Revision 所属目标一致。

引用关系追加后不可由普通编辑软删除。附件清理检查器必须把完整 Revision 引用纳入存续判断，但这不自动改变 `Attachment.IsPublic` 或建立新的公开权限。

### 6.6 保留与配置

- Revision 不消费 `HistorySaveEditCount / MaxHistoryRecords`，也不按旧历史配置裁剪；否则当前版本号会再次失去完整时间线。
- `MaxEditCount / EditWindowMinutes / AdminOverride` 继续约束业务编辑与恢复。
- F4-M 兼容期结束后，历史写入和裁剪配置转为 legacy，只允许为旧表查询保留说明，不继续控制 Revision。
- 普通软删除只改变目标可用性，不物理删除 Revision；目标被恢复后仍沿用原版本号继续追加。
- 法务删除、隐私擦除或受权证据脱敏必须另行定义 `Redacted` 事件与审计，不允许普通清理任务静默修改或删除快照。

## 七、旧历史兼容与 migration

### 7.1 为什么不直接恢复旧历史

旧记录存在四个无法安全补齐的事实缺口：

- 没有编辑当时的分类；
- 没有编辑当时的标签集合；
- 没有封面与附件引用集合；
- 历史写入和保留数量可能小于实际编辑次数。

因此不能根据相邻 `Old/New` 字段猜测完整版本，也不能把“最接近的当前分类标签”写成历史事实。

### 7.2 Main schema ledger

F4-M-B 使用下一条 Main 有序 migration，预定标识为：

```text
20260726_013_forum_content_revision
```

migration 负责：

1. 创建 Revision、RevisionTag 和 RevisionAttachment 表及索引。
2. 为 `Post / Comment` 增加 `ContentRevision`。
3. 按目标逐条读取当前正文、分类、标签、封面，以及共享解析器可证明归属的 `attachment://` 和历史受控附件 URL 引用。
4. 写入 `RevisionNumber=1 / SourceType=Baseline`。
5. 能完整证明关系时标记 `Complete`；存在缺失附件、跨租户关系、无法安全映射的历史 URL 或其他无法解析关系时标记 `LegacyIncomplete` 并记录稳定诊断，不猜测引用。
6. 把实体 `ContentRevision` 更新为 `1`。
7. 不修改、删除或重排旧历史记录。

doctor 至少报告：

- 重复目标版本；
- 实体版本号与最大 Revision 不一致；
- Revision、目标、分类、标签或附件跨租户；
- RevisionTag / RevisionAttachment 孤立关系；
- `Complete` 快照引用不存在、禁用或已删除对象；
- `Restore` 来源不存在或指向其他目标。

严格 verify 必须阻断结构漂移、当前指针漂移和新运行时生成的不完整快照。历史基线的 `LegacyIncomplete` 允许存在，但数量和目标必须可追踪。

### 7.3 旧接口兼容窗口

- F4-M-B 新增 Revision API 后，旧 `GetEditHistory` 暂时保留供 F4-M-C 迁移。
- 旧接口的完整正文读取改为作者 / 管理员受权访问，不继续保留匿名全文契约。
- F4-M-C 正式页面只消费 Revision 摘要与详情；旧历史以“早期编辑记录，仅供查看”独立展示，不提供恢复动作。
- F4-M-D 验收确认无正式消费者后，决定删除旧 HTTP 入口或保留受权只读兼容；无论选择哪种，新写入都不得继续双写旧历史表。

## 八、普通编辑写入契约

帖子和评论编辑 DTO 新增必填 `ExpectedContentRevision`。客户端提交意图摘要与指纹同时包含该字段，避免同一 key 在不同当前版本上被误认为同一请求。

一次成功编辑的事务顺序固定为：

1. 读取目标、权限、当前 `ContentRevision` 和提交意图。
2. 校验 `ExpectedContentRevision == current ContentRevision`。
3. 执行现有内容、分类、标签、编辑次数和评论时间窗校验。
4. 校验正文与封面引用的附件存在、有效、同租户且调用者有权绑定。
5. 以 `WHERE ContentRevision = expected` 更新目标为新状态和 `N + 1`。
6. 同步帖子分类计数和当前标签关系。
7. 写入 `RevisionNumber=N+1 / SourceType=Edit / IntegrityStatus=Complete` 及标签、附件关系。
8. 更新 `EditCount` 与审计字段。
9. 完成 `ContentSubmissionRecord`。

无变化请求返回当前版本，不增加 `ContentRevision / EditCount`，不创建 Revision。相同 `ClientSubmissionId + 摘要` 重试返回同一新版本；同 key 异参继续稳定冲突。

## 九、恢复写入契约

### 9.1 请求

帖子：

```http
POST /api/v1/Post/RestoreRevision
```

评论：

```http
POST /api/v1/Comment/RestoreRevision
```

请求至少包含：

```json
{
  "targetId": "123456789012345678",
  "revisionId": "223456789012345678",
  "expectedContentRevision": 4,
  "clientSubmissionId": "forum-post-revision-restore:uuid"
}
```

Long ID 继续按字符串传输。帖子与评论分别使用新的稳定操作类型：

- `ForumPostRevisionRestore`
- `ForumCommentRevisionRestore`

### 9.2 恢复前校验

服务端必须重新校验：

- 目标存在、未删除、属于当前租户；
- 当前用户是作者，或为既有 `System / Admin` 管理角色；
- 目标 Revision 属于同目标、同租户，且 `IntegrityStatus=Complete`；
- `ExpectedContentRevision` 等于当前版本；
- 当前编辑次数和评论时间窗允许本次变更，或管理员覆盖配置允许绕过；
- 标题、正文和内容类型仍满足当前规则；
- 分类、标签仍存在、启用并允许当前目标使用；
- 封面和正文附件存在、启用、未删除、同租户且属于该目标的合法历史引用；
- 恢复内容与当前状态确实不同。

任何失败都不得调整分类计数、标签、附件、正文、版本号或提交意图终态。

### 9.3 恢复事务

校验通过后：

1. 使用 CAS 把当前目标更新为快照状态和 `N + 1`。
2. 同步分类计数、当前标签关系和当前附件绑定。
3. 创建 `RevisionNumber=N+1 / SourceType=Restore`。
4. 新 Revision 保存恢复后的完整快照，并设置 `RestoredFromRevisionId`。
5. `EditCount` 增加一次，审计字段记录实际操作者。
6. 完成提交意图并返回新版本号。

目标 Revision 本身保持不可变；新版本和来源版本都继续可查看。恢复不发送新的点赞、提及或评论通知，不重新触发投票、问答、抽奖和经验奖励。

### 9.4 稳定失败语义

至少提供：

- `Forum.RevisionNotFound`
- `Forum.RevisionAccessDenied`
- `Forum.RevisionIncomplete`
- `Forum.RevisionConflict`
- `Forum.RevisionCategoryUnavailable`
- `Forum.RevisionTagUnavailable`
- `Forum.RevisionAttachmentUnavailable`
- `Forum.RevisionContentRejected`
- `Forum.RevisionEditLimitReached`
- `Forum.CommentRevisionWindowExpired`
- `Forum.RevisionRestoreKeyConflict`

错误响应继续遵循统一 HTTP status、`Code / MessageKey / TraceId` 契约。目标不存在、跨租户或无读取权不得泄露 Revision 是否存在。

## 十、读取接口与权限

### 10.1 Revision 列表

- `GET Post/GetRevisionList?postId=...`
- `GET Comment/GetRevisionList?commentId=...`

匿名 / 普通读者只获得公开摘要：

- 是否编辑、编辑次数、最近编辑时间；
- 不返回 Revision Id、旧正文、旧标题、分类标签或附件。

作者 / 管理员获得分页版本摘要：

- Revision Id 与版本号；
- `SourceType`、操作者、时间；
- 是否当前版本；
- `VoCanViewSnapshot / VoCanRestore`；
- 不可恢复的稳定原因码；
- 恢复来源版本号。

### 10.2 Revision 详情

- `GET Post/GetRevisionDetail?revisionId=...`
- `GET Comment/GetRevisionDetail?revisionId=...`

只允许目标作者和受权管理员读取完整快照。响应返回 `ExpectedContentRevision` 所需的当前版本号，但客户端恢复前仍必须刷新并处理 CAS 冲突。

### 10.3 权限矩阵

| 能力 | 匿名 / 其他读者 | 作者 | Admin / System |
| --- | --- | --- | --- |
| 查看已编辑公开摘要 | 允许 | 允许 | 允许 |
| 查看完整 Revision | 拒绝 | 允许本人目标 | 允许，记录实际操作者 |
| 精确恢复 | 拒绝 | 按编辑次数 / 时间窗 | 按既有覆盖配置 |
| 查看旧完整 EditHistory | 拒绝 | 允许本人目标 | 允许 |
| 恢复 `LegacyIncomplete / Redacted` | 拒绝 | 拒绝 | 拒绝 |

管理员身份不允许绕过租户、目标归属、附件有效性、内容规则或 CAS；只允许按既有配置绕过编辑次数和评论时间窗。

## 十一、页面与交互

### 11.1 正式 Web

- 帖子与评论既有“历史”入口升级为“版本”。
- 作者视图使用时间线 + 详情对比；PC 可双栏比较，mobile 改为“目标版本 / 当前版本”分段切换，不压缩成窄双栏。
- 当前版本明确标记，不能恢复到自身。
- 恢复确认展示目标版本号、提交者、时间和受影响字段。
- 提交期间禁用重复操作，不做正文或版本号的乐观更新。
- CAS 冲突保留当前弹窗和用户选择，不自动重放恢复；刷新当前版本后由用户重新确认。
- 失权、目标删除或版本被治理为不可用时，立即关闭详情并清除内存快照。
- “使用此版本编辑”只填充现有编辑器，不保存到持久化草稿、本地存储或 URL。

### 11.2 可访问性与隐私

- 版本列表、对比区、确认框支持键盘、可见焦点、Escape 关闭和焦点返回。
- 长正文使用受控滚动区；屏幕阅读器能区分目标版本与当前版本。
- 不在日志、URL、Analytics 或错误消息中记录旧正文、附件 URL 或完整快照。
- 账号切换、登出、目标失权和窗口关闭时清除内存中的 Revision 详情。

### 11.3 壳层边界

- 正式 Web 是唯一新增产品入口。
- WebOS 若继续复用同一 Forum 组件，可以自然显示兼容结果，但不增加 WebOS 专属窗口、路由或验收承诺。
- Flutter 保持既有作者编辑能力，不新增版本页面或恢复入口；阻断、安全和认证兼容问题继续按条件维护。
- 不新增 Console 页面；管理员通过正式论坛详情的同一受权契约处理。

## 十二、失败恢复与一致性

| 场景 | 服务端行为 | 页面行为 |
| --- | --- | --- |
| 两标签页同时编辑 | 首个 CAS 成功，后一个返回 `RevisionConflict` | 保留用户输入，刷新当前版本并重新比较 |
| 恢复请求超时后重试 | 同 key 同摘要返回既有新版本 | 读取权威版本，不重复恢复 |
| 同 key 改换目标版本 | 返回 `RestoreKeyConflict` | 要求新建恢复意图 |
| 分类或标签已失效 | 不写任何状态 | 禁用精确恢复，允许载入编辑器修正 |
| 附件已删除或跨租户 | 不写任何状态，不披露额外附件信息 | 标记版本不可精确恢复 |
| 内容规则已更新 | 按当前规则拒绝 | 载入编辑器后人工修订 |
| 评论已过编辑窗口 | 普通作者拒绝，管理员按配置 | 显示稳定时间窗提示 |
| 目标被删除或用户失权 | 拒绝详情与恢复 | 清除快照并返回详情页安全状态 |
| Revision 写入失败 | 整个编辑 / 恢复事务回滚 | 当前内容保持不变，可使用同 key 重试 |

## 十三、A-D 开发批次

### F4-M-A：候选审计与权威设计（已完成）

- 交叉复核论坛版本恢复、内容赞赏、匿名公开聊天和附件基础设施。
- 固定本文的用户路径、数据模型、兼容迁移、CAS、权限、恢复事务、页面、失败恢复、停止线和验证口径。
- 不修改模型、接口、业务代码或 Pencil。

### F4-M-B：服务端权威契约（已完成）

- 新增 Revision、RevisionTag、RevisionAttachment 和当前 `ContentRevision`。
- 实现 `20260726_013_forum_content_revision` 的 SQLite / PostgreSQL doctor、apply、重入、verify 和历史基线。
- 普通编辑接入 CAS、完整 Revision 与附件引用，不再写入旧 EditHistory。
- 新增受权列表 / 详情 / 恢复 API、稳定错误和 `@radish/http` 契约。
- 补 Repository、Service、Controller、迁移、并发、幂等和附件存续测试。

### F4-M-C：Pencil 与正式页面（已完成）

- 先更新 PC / mobile Pencil 的帖子和评论版本工作流。
- 正式 Web 接入公开摘要、作者版本列表、详情对比、恢复确认、冲突保留和“使用此版本编辑”。
- 收口旧匿名全文历史，覆盖双语、四主题、键盘、长正文和失权清理。
- 不新增 Console、Flutter、WebOS 或 Tauri 专属实现。

### F4-M-D：成组验收与专题关闭

- 覆盖作者、其他登录用户、匿名、Admin / System、失权与目标删除。
- 覆盖帖子 / 根评论 / 子评论、分类标签、正文与封面附件、编辑次数和评论时间窗。
- 覆盖正常编辑、无变化、同 key 重试、同 key 异参、双标签 CAS、恢复后再编辑和多次恢复。
- 覆盖旧历史兼容、`LegacyIncomplete`、分类标签失效、附件失效和当前内容规则变化。
- 覆盖中英文、PC / mobile、键盘、长正文、Back / Forward 和账号切换。
- 完成临时数据清理、六库完整性、Main SQLite / PostgreSQL migration 严格 verify。
- 真实浏览器验收前重新说明启动命令、Gateway / API / Auth / Frontend 端口与清理方式并取得当前任务授权；验收优先使用浏览器插件。

## 十四、验证基线

### 14.1 服务端

- migration 空库、历史库、重入、checksum、doctor、严格 verify 和备份恢复；
- 当前实体版本号与最大 Revision 一致；
- 新写入 Revision 均为 `Complete`，标签与附件关系完整；
- 普通编辑和恢复均通过 CAS，竞争请求只允许一个成功；
- 提交意图重放不重复创建 Revision；
- 分类计数、标签、正文、附件引用、EditCount 和 Revision 原子一致；
- 旧历史不被改写，也不进入可恢复集合；
- 匿名、其他用户、作者和管理员权限隔离；
- 失效分类、标签、附件、内容规则和目标状态均不产生部分写；
- Long ID 字符串序列化和稳定双语错误。

### 14.2 前端静态验证

- `@radish/http` test / type-check / lint；
- `radish.client` 定向测试、type-check、lint、production build；
- 公开摘要不包含旧正文；
- 作者详情、恢复确认、CAS 冲突、失败回退和内存清理；
- PC / mobile、双语、四主题、键盘与 reduced-motion；
- `validate:baseline:quick`、changed-only 仓库卫生与 `git diff --check`。

### 14.3 运行态验收

按 [浏览器 Smoke 规则](/guide/browser-smoke) 经 Gateway 同时覆盖 PC 与移动视图。真实 smoke 只在 F4-M-D 成组验收执行，不在 A / B / C 日常连续开发中重复启动服务。

## 十五、完成标准

F4-M 只有同时满足以下条件才可关闭：

- 每次新编辑都有完整、连续、不可变 Revision。
- 作者可在正式 Web 找到、查看并安全恢复可用版本。
- 恢复始终创建新版本，旧版本和来源关系可追查。
- CAS、幂等与事务能够阻止覆盖、重复恢复和部分状态。
- 旧历史没有被伪装为完整版本，匿名旧正文暴露已收口。
- 分类、标签、附件和内容规则失效时有明确、可恢复的用户路径。
- SQLite / PostgreSQL migration、静态回归和 PC / mobile Gateway 矩阵通过。
- 临时数据、凭据、备份和运行进程完成清理，六库完整性正常。

## 十六、停止线

- 不从旧 `Old/New` 字段猜测分类、标签、封面或附件。
- 不通过覆盖旧 Revision、移动当前指针或删除中间版本实现恢复。
- 不允许前端决定作者、租户、当前版本号或恢复结果版本号。
- 不在 CAS 冲突时自动覆盖、自动合并或静默重试另一版本。
- 不在附件或分类标签失效时做部分精确恢复。
- 不让 Admin / System 穿透租户、附件、内容规则或版本完整性。
- 不长期双写 Revision 与旧 EditHistory。
- 不为历史快照重新触发互动通知、经验、资产、投票、问答或抽奖副作用。
- 不把版本附件存续关系扩张为通用附件权限平台。
- 不顺带建设内容赞赏、匿名公开聊天、协同草稿、公开 diff 或跨端追平。
- 不因单独设计或小批次频繁创建 `dev -> master` PR；待 F4-M 形成完整功能批次后统一集成。

## 十七、关联文档

- [论坛编辑历史](/features/forum-edit-history)
- [论坛内容发布可靠性与编辑历史治理](/guide/forum-content-write-reliability-governance)
- [论坛应用功能说明](/features/forum-features)
- [论坛分类与标签](/features/forum-category-tag)
- [文件上传设计](/features/file-upload-design)
- [浏览器 Smoke 规则](/guide/browser-smoke)
- [验证基线说明](/guide/validation-baseline)
- [发布后维护与功能完成线](/planning/post-release-maintenance-feature-completion)
