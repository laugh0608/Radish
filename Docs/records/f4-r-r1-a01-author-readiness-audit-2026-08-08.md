# F4-R R1-A01 Author 设计前代码事实与能力覆盖门禁

> 日期：2026-08-08（Asia/Shanghai）
>
> 状态：静态审计完成；审计发现的能力缺口已于同日完成代码修复和静态验收，R1-A01 等待 PC 正式代表设计
>
> 范围：`/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id`，以及对应 HTTP、Service、Console 与 WebOS 历史实现；本审计本身未修改 `.pen` 或运行时代码，后续修复见[能力门禁实现记录](/records/f4-r-r1-a01-author-capability-gate-implementation-2026-08-08)

## 1. 结论

- `R1-A01` 是 `R1-P02` 关闭后的下一完整代表页，正式锚点保持 `/docs/edit/:id`。
- 正式 Web 已接入创建文档身份、开启下一稿、共享草稿保存、提交 / 撤回审核、协作者邀请与响应、CAS 冲突恢复和审核事件展示；WebOS 没有需要迁回普通 Author 的独占能力。
- 普通 Author 不拥有审核、Apply、Publish、访问策略、归档、删除、恢复、回滚或导入导出能力；这些动作继续属于 Console。
- 深入核对发现 Author Revision 读取权限、终态证据回看、写入响应证据和 Apply 基准版本 CAS 四项真实缺口。它们不能由视觉稿或前端隐藏规避；方案获批后已按长期契约闭合。
- 能力覆盖门禁现已通过代码与静态验证；本记录继续冻结代表身份、设计输入与停止线，不提前替代 PC / Mobile 代表设计确认。

## 2. 代表身份与状态

正式代表身份固定为：

```text
登录普通 Owner
+ Custom 已发布文档
+ 已存在正式 v1
+ Editing 活跃共享草稿 v2
+ 至少一名 Accepted Editor
```

选择该身份是为了在不引入 Console 权限的前提下，同时覆盖正文编辑、正式 / 草稿版本、保存、提交审核、协作上下文和有效公开回看。默认整页不展示冲突或只读终态。

关键状态只维护必要区块，不复制完整页面：

| 状态 | 必须表达 | 不得暗示 |
| --- | --- | --- |
| `ChangesRequested` | 审核意见、仍可编辑、Owner 可重新提交 | 已形成新正式 Revision |
| `Submitted` | 正文只读、Owner 可撤回审核 | 普通作者可 Apply 或 Publish |
| `Wiki.DraftVersionConflict` | 保留本地 Markdown、服务器草稿 / 正式版本、复制、下载、重新载入 | 已自动合并 |
| Pending Invitee | 只读共享草稿、接受 / 拒绝邀请 | 已拥有保存权 |
| Accepted Editor | 可保存同一活跃草稿 | 可提交审核或管理协作者 |

`Applied / Rejected / Withdrawn` 是终态；后续修改必须由 Owner 开启下一份草稿。正式文档状态与草稿审核状态继续分离，Apply 不等于 Publish。

## 3. 已承接的正式能力

| 能力 | 正式事实 | 裁决 |
| --- | --- | --- |
| 路由与登录回流 | 四条 Author 路由由独立 `docs-author` 入口承接，未登录保留目标路径 | 已承接 |
| 创建与下一稿 | 任意登录用户可创建自己的 Custom 文档；Owner 可从正式版本开启下一稿 | 已承接 |
| 共享草稿 | Owner 与 Accepted Editor 读取并保存同一 `ActiveDraftId` | 已承接 |
| 能力判定 | 核心动作使用服务端 `VoCanEdit / VoCanSubmit / VoCanManageCollaborators` | 已承接 |
| CAS 保存 | 保存携带 `ExpectedDraftVersion`；冲突保留本地 Markdown | 已承接 |
| 提交与撤回 | Owner 提交审核；当前 UI 在 `Submitted` 状态提供撤回 | 主路径已承接 |
| 协作者 | Owner 按同租户用户 `PublicId` 邀请 / 移除；Invitee 接受或拒绝 | 已承接 |
| 审核事件 | 活跃草稿详情返回 Owner、协作者与 Review Events | 审计时写后证据不稳定；现已修复 |
| 正式 Revision | 页面只读展示正式版本，不提供普通作者回滚 | 产品边界正确；审计时读取权限未闭合，现已修复 |
| WebOS / Console | WebOS 写入兼容层复用 Author Service；高风险治理动作归 Console | 无需迁移 |

## 4. 审计时能力覆盖阻断

> 下列内容保留审计当时的代码事实；修复结果见第 8 节。

### 4.1 普通 Author Revision 路由未真正打通

`/docs/revisions/:id` 调用 `Wiki/GetRevisionList` 和 `Wiki/GetRevisionDetail`，但两个接口要求 `console.docs.view`。页面同时使用公开 `GetById(includeDeleted=true)` 读取文档；普通用户不会获得 `includeDeleted`，未发布文档也可能不可访问。

因此 Admin smoke 通过不能证明普通 Owner / Editor 的正式 Revision 路由可用。修复不得简单移除 Console 权限；需要保持文档关系授权和租户隔离的 Author 只读入口或等价 Service 边界。

### 4.2 写入响应会丢失协作与审核证据

`AuthorGetById` 返回带 Owner、Collaborators 和 Review Events 的详情，但 Save / Submit / Withdraw 返回基础详情。前端直接用写响应替换当前状态后，右侧协作与审核 rail 会临时清空，直到再次读取详情；Withdraw 后 `ActiveDraftId` 已清除，甚至无法沿原路径刷新。

写入响应或前端状态合并必须保留权威证据，不能把空集合解释为协作者被移除或审核历史消失。

### 4.3 终态审核结果缺少 Author 回看路径

`Applied / Rejected / Withdrawn` 会清除 `ActiveDraftId`。Author 列表只读取活跃草稿，Author 详情在没有活跃草稿时返回不存在；普通作者刷新后无法从正式 Author 路由回看终态 Review Event。

`Applied` 可转入正式 Revision 语境，但 Revision 权限目前未闭合；`Rejected / Withdrawn` 仍需要最小、受权的结果证据，不能依赖一次性内存状态。

### 4.4 Apply 未绑定草稿的 BaseDocumentVersion

专题契约要求 Apply 校验 `WikiDocument.Version == WikiDocumentDraft.BaseDocumentVersion`。当前实现使用审核客户端传入的 `ExpectedDocumentVersion` 执行 CAS，Console 又传入审核页加载时的当前文档版本。

这能阻止“审核页加载后”的正式版本变化，却不能阻止“草稿创建后、审核页加载前”的正式正文漂移。Apply 必须与草稿创建时的权威基准版本绑定，不能用较新的客户端读值替代。

## 5. 当前页面结构债

- `DocsAuthorApp.tsx` 已达 `1433` 行，混合认证、集合加载、写入控制器、Mine 与 Revisions 视图，接近 `1500` 行硬上限；后续实现不得继续堆入主文件。
- PC 当前是主编辑区加 `300–340px` sticky rail；页面级 Hero、导航、面板标题和 rail 动作存在重复，编辑任务首屏被解释与摘要占用。
- `1080px` 以下只是把完整 rail 移到编辑器之后；mobile 中协作者和审核时间线落在至少 `420px` 的 Markdown 编辑器之后，不符合已确认的 Bottom Sheet / 抽屉单任务模型。
- 保存 / 提交动作位于长编辑器之后，移动端触达成本过高；离开保护只覆盖上传中，不覆盖未保存草稿。
- `MarkdownEditor` 未接入 Author 页主题，默认深色；mobile 仍可手动进入水平 Split，需要在共享组件边界统一治理。
- “公开阅读”只检查版本号或 Slug，没有同时检查 Published 状态，可能把 Apply 误表达为已经公开。
- `Owner / Editor / Invitee`、只读原因和 `Author Workspace / Document Draft / Revision History` 等稳定词元仍有直接英文回显，不符合双语 presentation 边界。
- `/docs/mine` 固定读取前 `100` 条且没有 Owner / 协作 / 审核状态筛选；该项归 `R2-A02`，不阻断编辑器代表页，但后续不得宣称列表已完整收口。

## 6. 后续设计输入

### PC

- 保持 `WebShellHeader` 与正式 Author 路由，不新增独立作者工作台。
- 正文编辑器是主轴；标题、保存状态、正式 / 草稿版本和高价值动作进入紧凑任务上下文，不保留重复大 Hero。
- 目录语义、元信息、正文工具、协作 / 审核 rail 分区明确；rail 只承载状态理解、协作者和 Review Event，不重复顶部返回与公开阅读动作。
- 保存与提交审核必须在长正文中保持可发现；不改变服务端 `VoCan*` 和 `ExpectedDraftVersion` 边界。

### Mobile

- 使用同一正式页面和共享壳层，收敛为单任务流，不压缩 PC 多栏。
- 先展示文档身份、审核状态、版本和当前可执行动作，再进入标题 / 正文编辑。
- 协作者与审核时间线进入共享 Bottom Sheet 或抽屉；不让整条 desktop rail 固定占位或落成长尾。
- mobile 不提供水平 Split；上传期间继续锁定离开，未保存草稿保护在实现批单独闭合。

### R2-A02

- `/docs/mine` 只补列表筛选、分页、Owner / 协作关系和只读原因。
- `/docs/revisions/:id` 只补正式版本时间线、只读快照与必要对比，不把现有 Revision 表述成 Slug、摘要、封面、目录或权限的完整快照。
- Forum Compose 只补论坛字段和发布器差异，不复制 Docs 编辑器整页。

## 7. 停止线与下一步

审计与能力修复均未修改活动或历史 `.pen`、数据库结构或全局主题 token，也未启动服务。

后续顺序：

1. Revision Author 读取、终态证据、写响应证据和 Apply `BaseDocumentVersion` CAS 已按获批方案修复并补测试。
2. Pencil 可用后，在唯一活动源中制作 `R1-A01` PC 正式代表画板；确认后再做 Mobile 和关键状态区。
3. 设计确认前不进入 R1-A01 页面视觉实现；`R1-W01` 不抢占该顺位。

## 8. 能力门禁闭环

同日修复批新增 Author Revision history / detail 关系授权入口、终态 evidence 字段和数据库侧最新终态轻量查询；所有 Author 写响应返回完整协作与审核证据，Apply 只使用服务端草稿 `BaseDocumentVersion` 并通过真实 AOP 事务回滚测试。Pending Invitee 的 Draft / Revision 附件读取与既有只读关系一致，正式公开链接同时绑定 Published 状态和正式 Slug。详见[能力门禁修复记录](/records/f4-r-r1-a01-author-capability-gate-implementation-2026-08-08)。

## 9. 主要证据

- `Docs/features/wiki-author-contribution-collaboration-design.md`
- `Docs/frontend/private-web-workflows-design.md`
- `Docs/guide/docs-author-collaboration.md`
- `Docs/frontend/f4-r-representative-page-audit.md`
- `Frontend/radish.client/src/docs/DocsAuthorApp.tsx`
- `Frontend/radish.client/src/docs/DocsAuthorEditorPage.tsx`
- `Frontend/radish.client/src/docs/DocsAuthorApp.module.css`
- `Frontend/radish.client/src/apps/wiki/api/wiki.ts`
- `Frontend/radish.http/src/wiki-authoring-contract.ts`
- `Radish.Api/Controllers/WikiController.cs`
- `Radish.Service/WikiDocumentService.Authoring.cs`
- `Radish.Repository/WikiDocumentRepository.cs`
- `Frontend/radish.console/src/pages/Documents/DocumentGovernancePage.tsx`
