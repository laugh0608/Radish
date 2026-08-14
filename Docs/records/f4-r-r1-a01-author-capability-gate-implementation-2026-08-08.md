# F4-R R1-A01 Author 能力覆盖门禁修复

> 日期：2026-08-08（Asia/Shanghai）
>
> 状态：代码与静态门禁完成；后续正式代表设计、页面实现与运行态验收已于同日闭合，R1-A01 已关闭
>
> 范围：Author Revision 关系授权、终态证据、写响应证据、Apply 基准版本 CAS、受保护附件读取一致性与正式公开链接；未修改 `.pen`、页面视觉、数据库结构、Public / Console 既有权限或全局主题 token，未启动服务

## 1. 结论

- `/docs/revisions/:id` 已改用 Author 专属关系授权 API，不再依赖 Public detail 或 `console.docs.view` Revision 接口。
- `/docs/mine` 与 `/docs/edit/:id` 可在 `ActiveDraftId` 清除后回看最新 `Applied / Rejected / Withdrawn` 证据；Owner 仍由独立服务端能力字段开启下一稿。
- Author Create / Start / Save / Submit / Withdraw 及幂等分支统一返回 Owner、协作者、审核事件和 `VoCan*` 权威证据。
- Apply 只使用服务端 `WikiDocumentDraft.BaseDocumentVersion` 更新正式文档；草稿、正文、Revision、Review Event 与 `ActiveDraftId` 继续处于同一事务。
- 终态正文保留期以终态变更时间为锚；久置的 `ChangesRequested` 当日撤回后不会因旧审核时间立即清理。
- Pending Invitee 可读取其已获权草稿与 Revision 的受保护附件，但不会因此获得当前正式 DocumentContent、附件管理或公开读取权限。

## 2. Author Revision 契约

新增两个只读入口：

- `GET Wiki/AuthorGetRevisionHistory/{documentId}`
- `GET Wiki/AuthorGetRevisionDetail/{revisionId}`

二者只要求 `AuthorizationPolicies.Client`，Service 按 Owner、Pending Invitee、Accepted Editor、System / Admin 关系授权。Declined、Revoked、无关用户、已删除文档和错误 Revision—Document 绑定统一返回 `404`，不泄露资源存在性。

既有 `Wiki/GetRevisionList` 与 `Wiki/GetRevisionDetail` 仍要求 `console.docs.view`，没有通过移除 Console 权限来修复 Author 路径。Author detail 使用独立 `WikiAuthorRevisionDetailVo`，不返回内部 `VoCreateId`。

## 3. 终态与写响应证据

Author 列表和详情新增或固定以下语义：

- `VoDraftId`：当前展示的活跃草稿；没有活跃草稿时为最新终态草稿。
- `VoActiveDraftId / VoLatestDraftId / VoIsActiveDraft`：显式区分活跃指针与证据草稿。
- `VoCanStartDraft`：Owner 是否可以开启下一稿，不再由前端通过空 ID 猜测。
- `VoHasDraftPayload / VoPayloadPurgedAt`：区分保留期清理与真实空正文。
- `VoDocumentSlug`：始终是正式文档 Slug；`VoSlug` 继续表示草稿字段。

列表批量读取只在数据库侧按每篇文档最大 Snowflake Draft ID 选择最新终态，并投影不含 Markdown 正文的轻量 evidence；详情读取才加载单份完整终态草稿，避免历史数量和正文大小线性放大。

所有 Author 写响应统一走完整 evidence builder。前端可直接采用成功响应，不再把空数组误解释为协作者被移除或审核历史消失。

## 4. Apply 与保留期

- `ReviewWikiDraftDto.ExpectedDocumentVersion` 必须等于目标草稿的 `BaseDocumentVersion`。
- `WikiDraftApplyCommand` 不再携带第二份版本标量；Repository 只读取 `command.Draft.BaseDocumentVersion`。
- Apply 先对 `Submitted + ExpectedDraftVersion` 草稿执行 CAS，再对 `Document.Version + ActiveDraftId` 执行 CAS；后续 Revision evidence 写入失败时，真实 `TranAop` SQLite 用例证明全部写入回滚。
- 自动清理的 cutoff 与排序使用 `ModifyTime ?? ReviewedAt ?? CreateTime`，以终态实际变更时间为准。

## 5. 正式 Web 消费

- `@radish/http` 提供 Author history / detail、终态能力与正式 `voDocumentSlug` 契约，所有 LongId 继续使用字符串。
- Mine、Editor 与 Revisions 的具体公开阅读入口统一要求 `Published + version > 0 + formal slug + 非删除`；Apply 未 Publish、Draft、Archived、空 Slug 或只修改了草稿 Slug 时不显示错误链接。
- 已清理终态正文显示明确说明，协作者和审核时间线证据继续保留。
- 本批不处理 R1-A01 的视觉重构、mobile Bottom Sheet、MarkdownEditor 主题 / Split 或未保存离开保护；这些继续服从代表设计确认顺序。

## 6. 验证

- `Radish.Api.Tests`：`1179 passed / 39 skipped / 0 failed`。
- Wiki 定向后端矩阵：`104 passed / 4 skipped / 0 failed`。
- `@radish/http`：type-check、lint 与 `33` 项测试通过。
- `radish.client`：type-check、lint、全量 `492` 项测试与 production build 通过；最终正式 Slug / Published 收口另有 `44` 项相关测试通过。
- `git diff --check`、changed repo hygiene 与文档检查通过。

PostgreSQL latest-terminal 聚合已增加条件集成用例；当前机器未配置 `RADISH_TEST_POSTGRES_CONNECTION_STRING`，因此按仓库约定显式跳过，不把 SQLite 结果表述为 PostgreSQL 实跑。

## 7. 下一步

1. 本记录的能力门禁范围保持完成，不回写或扩大既有 API / 权限边界。
2. 后续 PC / Mobile 正式代表设计、页面实现与 Gateway 运行态验收见[成组实现记录](/records/f4-r-r1-a01-author-editor-implementation-2026-08-08)。
3. R1-A01 已关闭，下一正式代表设计顺位为 `R1-W01`。
