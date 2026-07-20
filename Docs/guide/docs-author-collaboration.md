# 文档作者协作与审核使用说明

> 最后更新：2026-07-20
>
> 适用入口：正式 Web `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id`，以及 Console `/documents`。

本文面向文档所有者、协作者和 Console 审核者，说明当前文档贡献流程怎么使用。底层数据、并发和权限设计见 [Docs / Wiki 普通作者贡献与协作设计](/features/wiki-author-contribution-collaboration-design)，公开阅读与固定文档边界见 [文档系统方案](/guide/document-system)。

## 1. 角色与职责

| 角色 | 可以做什么 | 不能做什么 |
| --- | --- | --- |
| Owner | 创建文档、保存共享草稿、邀请或移除协作者、提交或撤回审核、处理修改意见 | 直接发布、改变访问策略、归档、删除、恢复或执行审核 |
| Pending Invitee | 在作者库只读查看共享草稿、协作上下文并接受或拒绝 | 保存草稿、邀请他人或提交审核 |
| Accepted Editor | 与 Owner 读取和保存同一份活跃草稿 | 邀请他人、提交审核、发布或治理文档 |
| Console Reviewer | 查看待审队列与正文证据，执行 RequestChanges、Reject 或 Apply | 仅凭审核权限发布文档 |
| Console Publisher | 单独发布、下架文档 | 仅凭发布权限批准草稿 |

任意登录用户都可以进入作者入口并创建自己的文档草稿，不再依赖 `Admin/System` 角色名。页面动作以服务端返回的 `VoCanEdit / VoCanSubmit / VoCanManageCollaborators` 为准。

## 2. 创建与保存草稿

1. 登录后打开 `/docs/mine`，查看“我拥有的”和“与我协作的”文档。
2. 进入 `/docs/compose`，填写标题、可选 Slug、摘要、Markdown 正文、封面、目录建议和修改摘要。
3. 创建成功后会同时生成稳定文档身份与第一份工作草稿；新正文不会进入公开阅读。
4. 在 `/docs/edit/:id` 继续保存。每次保存都携带当前 `ExpectedDraftVersion`，成功后页面采用服务端返回的新版本。
5. `/docs/revisions/:id` 只查看已经批准的正式版本；日常草稿保存不会伪造成正式 Revision。

内置固定文档、已删除文档或当前无编辑权的文档保持只读，并显示原因。既有文档在上一份草稿进入终态后，由 Owner 开启下一份草稿。

## 3. 邀请与协作

- Owner 使用用户 `usr_...` PublicId 邀请协作者，不使用邮箱、登录名或内部 LongId 搜索。
- 邀请处于 Pending 时，受邀者可以只读查看共享草稿和协作上下文，以决定接受或拒绝，但不能保存。
- 接受后，Editor 与 Owner 共享同一份活跃草稿；不存在每人一份私有副本。
- Owner 移除协作者后，服务端立即拒绝该用户继续读取或保存；已经打开的页面不能依靠缓存继续写入。
- 首批每篇文档最多 `20` 名 Pending / Accepted 协作者，每名用户最多拥有 `20` 份活跃草稿；实际限制以服务端配置为准。

## 4. 提交、审核与发布

只有 Owner 可以提交或撤回审核：

```text
Editing / ChangesRequested -> Submitted
Submitted -> ChangesRequested / Rejected / Applied / Withdrawn
```

- `Submitted` 期间草稿只读；Owner 可撤回后继续修改。
- `RequestChanges` 保留同一份活跃草稿，Owner 或 Editor 修改后由 Owner 重新提交。
- `Reject` 和 `Withdrawn` 结束当前草稿；后续修改需要由 Owner 开启下一份草稿。
- `Apply` 将草稿原子写入权威正文并生成 Revision，但不会自动发布。
- 发布、下架、访问策略、归档、删除、恢复、回滚和 Markdown 导入导出继续由 Console 独立权限控制。

Console 审核者在 `/documents` 依次查看待审队列、正式正文与草稿证据、正式/草稿版本、协作者和审核时间线，再执行 RequestChanges、Reject 或 Apply。RequestChanges 与 Reject 必须填写审核意见。

## 5. 冲突与失败恢复

草稿保存和审核 Apply 都使用乐观并发。出现冲突时：

- 页面保留当前本地正文，不清空编辑器；
- 可以复制本地内容或下载 Markdown 备份；
- 重新载入服务端版本后再人工合并，不声称自动合并；
- `Wiki.DraftVersionConflict` 表示草稿版本已变化；`Wiki.DocumentVersionConflict` 表示审核期间正式正文已变化；
- `Wiki.DraftStateConflict` 表示草稿状态或活跃草稿指针已变化，应刷新详情后按服务端允许动作继续；
- 无权或不可见草稿统一按 `404` 处理，避免泄露文档存在性。

Markdown 正文默认不超过 `1 MiB` UTF-8 字节。页面必须保留服务端 `Code / MessageKey / TraceId`，不能通过匹配中文提示决定恢复流程。

## 6. 公开性、通知与保留

- 公开 `/docs` 只读取 `Published + Public` 的权威正文；Editing、Submitted、ChangesRequested、Rejected、Withdrawn 及 Applied 但未发布的内容都不会公开。
- 邀请和审核结果进入通知中心的 Knowledge 分类，服务端使用 `DocsAuthorDraft` 结构化目标并在返回前重新检查 Owner / Accepted Editor 权限。
- 目标失效、草稿被清理或用户失权时，通知保留安全摘要但不应提供伪造链接。
- 活跃草稿不自动过期；Applied、Rejected、Withdrawn 的正文载荷默认保留 `90` 天后小批次清空，审核事件和正式 Revision 继续保留。

## 7. 常用入口

- [文档系统方案](/guide/document-system)
- [Docs / Wiki 普通作者贡献与协作设计](/features/wiki-author-contribution-collaboration-design)
- [API 说明索引：文档与 Wiki](/guide/api-index)
- [Console 权限覆盖矩阵](/guide/console-permission-coverage-matrix)
- [通知中心使用说明](/guide/notification-center)
- [专题回归索引](/guide/regression-index)
