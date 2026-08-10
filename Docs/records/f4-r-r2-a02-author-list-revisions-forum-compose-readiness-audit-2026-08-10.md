# F4-R R2-A02 Author 列表、修订与 Forum 发布差异设计前代码事实与能力覆盖审计

> 日期：2026-08-10（Asia/Shanghai）
>
> 状态：readiness 已完成；五组设计前能力门禁等待确认
>
> 范围：正式 Web `/docs/mine`、`/docs/revisions/:id` 与 `/forum/compose`；反查 `R1-A01 / R1-P02`、WebOS Wiki / Forum 历史实现、Author / Forum API、本地草稿、PC / mobile 与双语状态

## 1. 结论

`R2-A02` 继续保持 R2，评分仍为 `1/1/2/1/1/0 = 6`。三个入口已有正式路由、真实服务端能力、共享编辑器和可继承壳层，不需要升级为新的 R1 页面类型，也不需要新增数据库、权限、业务实体、移动壳层或 WebOS 功能。

本专题应继承：

- `R1-A01` 的 Author 主任务、权威动作、dirty / 上传锁、版本证据和 PC → mobile 单任务转换；
- `R1-P02` 的正式 Public Header、公开来源返回、结构化状态和正文阅读层级；
- 既有 Wiki Author Service、Forum 发布、附件、标签、问答、投票、抽奖与 `clientSubmissionId`，不建立第二套作者或发布契约；
- WebOS 只作为能力来源反查：Wiki 的发布 / 回滚 / 治理动作不迁入普通 Author，Forum 继续与正式 Web 共用同一个发布器核心。

但当前实现还不能直接进入 Pencil。审计发现五组不能由视觉稿规避的能力门禁：Author 列表缺少权威筛选与完整分页、Revision 局部失败和请求竞态、正式 Forum Compose 被旧 Bottom Sheet 遮蔽、论坛本地草稿跨账号共享，以及共享发布器固定文案 / 失败反馈未完成双语与单一反馈治理。上述门禁需要先按第 3 节方案关闭。

## 2. 正式路由与能力覆盖

| 入口 | 当前正式能力 | WebOS 反查 | readiness 裁决 |
| --- | --- | --- | --- |
| Docs Mine `/docs/mine` | Owner / Pending Invitee / Accepted Editor 的文档集合、当前草稿 / 终态证据、开始下一稿、进入编辑 / Revision / 公开阅读 | WebOS `WikiApp` 还包含治理发布、下架、回滚与导入导出；这些属于 Console / 历史治理能力，不迁入普通 Author | 保持 Author 列表主任务；补服务端筛选、稳定分页和角色本地化，不恢复治理动作 |
| Docs Revisions `/docs/revisions/:id` | 关系授权 Revision history / detail、当前版本、正式公开链接、受保护附件和只读快照 | WebOS Revision 是管理员治理版本工具，普通 Author 只保留已批准快照与证据 | 保持列表—快照只读任务；补局部 unavailable / retry 和最新选择竞态保护，不增加回滚 |
| Forum Compose `/forum/compose` | 登录回流、分类预设、标签、Markdown / 富文本、问答、投票、抽奖、图片 / 文档附件、本地草稿与发布幂等 | WebOS `ForumApp` 使用同一个 `PublishPostModal`，没有正式 Web 缺失的独占发布能力 | 抽出共享发布器核心，WebOS 保持 Bottom Sheet；正式路由在后续设计实现中使用页面内任务面，不复制发布状态机 |

现有后端足以支撑 Wiki 关系授权、Revision 只读、Forum 发布写入、附件和幂等，不需要 migration。唯一需要扩展的服务端契约是 `AuthorGetList` 的权威筛选与分页查询；其余门禁均可在现有前端和共享 HTTP 边界内关闭。

## 3. 设计前必须关闭的五组能力门禁

### 3.1 Author 列表必须使用权威筛选、稳定分页和本地化角色

专题已明确 `/docs/mine` 展示“我拥有的 / 与我协作的”，并按草稿和审核状态筛选；当前实现却固定请求 `AuthorGetList?pageIndex=1&pageSize=100`，页面没有筛选或分页。超过 `100` 份历史文档后会静默缺项，`DataCount` 只显示总数而没有继续入口；在前端对首批数据筛选会进一步产生错误总数。页面还直接显示服务端稳定角色值 `Owner / Invitee / Editor`，中文界面没有本地化。

门禁方案：

1. 为 `AuthorGetList` 增加稳定查询契约：`scope=all|owned|collaborating`、`draftStage=all|editable|submitted|terminal|none`、`pageIndex / pageSize`；未知值结构化返回 `400`。
2. 在专属 Wiki Repository 中按数据库条件完成筛选、`ModifyTime ?? CreateTime DESC, Id DESC` 稳定排序和分页，不在 Service 内先取全量再过滤。
3. `@radish/http` 与 Client 使用显式查询类型；Mine 页面消费权威 `Page / PageCount / DataCount`，提供筛选、上一页 / 下一页和重试，账号变化时重置到第一页。
4. 角色继续作为稳定 code 传输，展示统一映射为双语资源；权限判断仍只消费 `VoCan*`，不通过翻译文本猜角色。

该门禁改变 Author 列表接口与 Repository 查询边界，必须获得确认后实施；不新增表、索引、权限或另一套列表端点。

### 3.2 Revision history 与 detail 必须独立裁决并拒绝过期响应

当前 `RevisionState.error` 同时承载 history 与 detail 错误。History 已加载时，detail 请求失败会把快照清空，却只渲染“请选择一个修订版本”，真实 unavailable 被吞掉。快速选择多个版本时也只有账号 epoch，没有 history / detail 请求代际；较早响应可能覆盖较新的选择。切换不同 Revision 文档时存在同类过期 history 风险。

门禁方案：拆分 `historyError / detailError`，为 history 和 detail 分别建立请求代际；只有当前文档、当前选中 Revision 和当前账号一致的响应才能提交。Detail 初次失败显示局部 unavailable 与重试；已有同一 Revision 权威快照的刷新失败显示 stale，不覆盖历史列表，也不伪装成“未选择”。

### 3.3 Forum 发布器必须拆成共享核心与两种合法承载面

正式 `/forum/compose` 先渲染页面摘要、主区与辅助轨，随后在分类就绪时自动打开 `PublishPostModal`。Bottom Sheet 覆盖页面本体，用户无法同时使用路由已经渲染的上下文；关闭 Sheet 又立即离开 Compose 路由。当前正式入口实际仍是 WebOS 弹层，而不是可继承 `R1-A01 / R1-P02` 的页面任务面。

门禁方案：从接近文件硬上限的 `PublishPostModal.tsx` 中抽出单一 `ForumPostComposer` 状态与视图核心；`PublishPostModal` 只保留 WebOS Bottom Sheet 承载，正式路由先继续复用相同核心能力，并为后续 R2 代表设计提供页面内承载接口。能力门禁阶段不提前确定最终 PC / mobile 视觉，只消除“只能作为 Modal 使用”的结构阻断；发布、附件、问答、投票、抽奖和提交幂等保持同一实现。

### 3.4 本地 Forum 草稿必须按账号隔离并与 Workbench 使用同一真相源

共享发布器和旧 `PublishPostForm` 当前都读写全局 `forum_post_draft`；Workbench 也直接读取该键。草稿没有 owner 字段，账号切换或同浏览器轮换登录后，下一账号可能看到上一账号的标题、正文、标签和扩展配置。发布器自身在账号变化时也不会重置已加载草稿。

门禁方案：建立单一版本化 Forum 草稿存储 helper，按当前登录 `userId` 隔离并由发布器、兼容 Form 和 Workbench 共用；账号变化时先清空内存状态，再只加载新账号草稿。旧无 owner 的全局草稿无法安全归属，升级后失败关闭，不自动迁给当前账号；实现记录必须明确兼容取舍。发布成功只删除当前账号草稿，不影响其他账号。

### 3.5 共享发布器必须完成双语与单一安全反馈

`PublicForumCompose` 外壳已经使用 i18n，但实际可见的 `PublishPostModal` 仍有数十处中文固定文案，覆盖标题、分类 / 标签、普通帖 / 问答 / 投票 / 抽奖、验证、设置、按钮、提示和 `aria-label`；英语环境因此仍显示中文。Docs Mine 同样直接展示角色 code。发布失败时 Modal 已使用结构化 `resolveForumPublishErrorMessage`，父级 `PublicForumCompose` 却再次展示原始 `error.message`，形成重复 toast 和原始消息泄露；分类加载失败也直接把异常文本写入页面。

门禁方案：共享发布器所有系统文案和无障碍名称进入 `community` 双语资源，用户内容保持原文；父级只负责成功导航，发布失败统一由 Composer 以结构化 code / messageKey 和本地化 fallback 呈现一次。分类失败显示固定本地化 unavailable，诊断只进入统一日志。新增静态门禁禁止正式发布器重新引入中文固定系统文案、原始 `error.message` 或第二次失败 toast。

## 4. R2 局部代表设计输入

### 4.1 代表身份与数据

- Docs Mine：普通 Owner，至少包含 `Editing`、`Submitted`、无活跃草稿且可开始下一稿三种文档，并包含一份 Accepted Editor 协作文档。
- Docs Revisions：同一普通 Author 关系下的已发布文档，至少三份已批准 Revision，当前版本与历史版本可切换。
- Forum Compose：登录普通用户，存在合法分类预设和已隔离的本地草稿；普通帖为主代表，问答 / 投票 / 抽奖只作为关键状态差异，不复制四套完整发布器。

### 4.2 PC / mobile 关键差异

| 入口 | PC 关键区块 | Mobile 转换 |
| --- | --- | --- |
| Docs Mine | 紧凑筛选 / 计数、连续文档列表与按需当前项上下文；消除全局 Hero、页内标题、摘要网格的重复 framing | 筛选与列表先于当前项上下文；主要动作贴近文档行，完整说明后置或折叠 |
| Docs Revisions | 版本轨—快照正文—窄证据区；history 与 detail 状态分别就地呈现 | 先选版本再读单一快照，证据与流转说明后置；不把三栏机械堆成长页 |
| Forum Compose | 页面内标题 / 正文写作主轴，分类、标签和附加功能作为按需设置；路由上下文不再被 Sheet 遮蔽 | 标题—正文—发布准备为主序列，设置按需展开；发布动作保持可见但不遮挡输入，不新建移动 App |

### 4.3 必要关键状态

- Docs Mine：筛选空结果、分页边界、首次 unavailable、已有页 stale、Pending Invitee 只读与无活跃草稿。
- Docs Revisions：history unavailable、detail unavailable / stale、快速切换只接受最新结果、无 Revision 和受权失效。
- Forum Compose：本地草稿恢复、账号切换隔离、分类 unavailable、dirty / 自动保存、上传中关闭锁、发布中、结构化失败保留草稿，以及普通帖 / 问答 / 投票 / 抽奖互斥。

## 5. 停止线与后续顺位

本 readiness 只记录代码事实与方案，没有修改 Pencil、运行时代码、API、数据库或权限，也没有启动服务 / 浏览器。

五组门禁方案确认前：

- 不修改活动 `.pen`，不提前画 R2-A02 代表区块；
- 不把 WebOS 发布 / 回滚 / 治理能力迁回普通 Author；
- 不新增服务端草稿、Forum 新路由、第二套发布器或移动壳层；
- 不成组推进 R3 派生页面。

方案确认后，先成组关闭五组能力门禁并执行后端定向测试、HTTP / Client 测试、type-check、Lint 与 production build；再按 R2 流程进入唯一活动设计源中的局部代表设计。

## 6. 主要证据

- `Docs/planning/current.md`
- `Docs/frontend/f4-r-representative-page-audit.md`
- `Docs/features/wiki-author-contribution-collaboration-design.md`
- `Docs/guide/docs-author-collaboration.md`
- `Docs/features/forum-public-app.md`
- `Docs/guide/forum-content-write-reliability-governance.md`
- `Docs/records/f4-r-r1-a01-author-readiness-audit-2026-08-08.md`
- `Docs/records/f4-r-r1-a01-author-capability-gate-implementation-2026-08-08.md`
- `Frontend/radish.client/src/docs/DocsAuthorApp.tsx`
- `Frontend/radish.client/src/docs/DocsAuthorApp.module.css`
- `Frontend/radish.client/src/apps/wiki/api/wiki.ts`
- `Frontend/radish.client/src/public/forum/PublicForumCompose.tsx`
- `Frontend/radish.client/src/apps/forum/components/PublishPostModal.tsx`
- `Frontend/radish.client/src/workbench/WorkbenchApp.tsx`
- `Radish.Api/Controllers/WikiController.cs`
- `Radish.Service/WikiDocumentService.Authoring.cs`
