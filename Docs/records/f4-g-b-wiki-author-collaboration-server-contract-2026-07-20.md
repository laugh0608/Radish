# F4-G-B Docs / Wiki 普通作者贡献与协作服务端权威契约完成记录

> 日期：2026-07-20
>
> 结论：F4-G-A / B 已完成，下一顺位进入 F4-G-C Author / Console 正式页面。

## 本批边界

本批严格按 [F4-G 权威专题设计](/features/wiki-author-contribution-collaboration-design) 完成普通作者草稿、协作者邀请、审核应用、通知与保留清理的服务端契约。没有修改 Pencil、正式 Author / Console 页面，没有启动 Radish 服务，也没有执行 Gateway 或浏览器 smoke。

## 完成内容

### 数据与显式迁移

- `WikiDocument` 增加显式 `OwnerUserId / ActiveDraftId`；已批准正文继续是唯一公开真相源，普通作者未审核内容只进入 `WikiDocumentDraft`。
- 新增 Draft、Collaborator、Review Event 三类实体及租户、状态、文档、用户和时间索引；同一文档 / 用户协作者关系与活跃草稿指针由数据库唯一约束保护。
- Main ledger migration `20260720_007_wiki_author_collaboration` 同时覆盖 SQLite / PostgreSQL，安全回填当前租户有效用户可确认归属的 `Custom / ImportedSnapshot` 历史文档；BuiltIn、LocalMirror 和无法确认归属的文档保持空所有者。
- `verify` 检查表、列、八项索引和可安全回填但仍未归属的历史文档；SQLite 首次执行和重入已验证，PostgreSQL 用例接入现有环境驱动约定。

### 权限、并发与事务

- Author API 只要求登录，Service 内按 Owner / Accepted Editor / Administrator 授权；无权草稿统一按不存在处理，BuiltIn / LocalMirror 不能开启作者草稿。
- 草稿保存以 `ExpectedDraftVersion` 条件更新；审核 Apply 同时校验正式版本、活跃草稿指针和草稿版本，正式正文、Revision、Review Event、指针清除与可靠通知位于同一事务。
- 协作者邀请只接受 `PublicId`；接受 / 拒绝 / 撤销使用条件状态转换，竞争动作只能有一个成功。邀请、提交、撤回和审核重复请求按目标状态幂等返回，不重复产生事件或通知。
- 服务端权威限制覆盖 Markdown UTF-8 `1 MiB`、每名所有者 `20` 份活跃草稿和每篇文档 `20` 名 Pending / Accepted 协作者；普通作者没有发布、访问策略、归档、删除或恢复权限。

### API、通知与保留

- 新增 Author 列表、详情、创建、开启、保存、提交、撤回、协作者读取 / 邀请 / 响应 / 移除 API，以及 Console 审核队列、证据详情和审核动作 API。
- 新增 `console.docs.review`，与既有 `console.docs.publish` 分离；审核 Apply 不自动发布文档。
- 草稿详情同时返回服务端能力、所有者公开身份、协作者和追加式审核时间线，前端不再通过角色名或来源类型猜测权限。
- 通知中心新增 Wiki 邀请和审核状态类型、结构化 `DocsAuthorDraft` 目标及目标失效复核；撤销关系后不再返回可点击草稿目标。
- Hangfire 新增终态草稿正文小批次清理：默认保留 `90` 天后只清空 Markdown 载荷并标记 `PayloadPurgedAt`，审核事件与正式 Revision 保留。

## 验证结果

- Wiki / 通知定向回归覆盖迁移重入、所有者回填、草稿 / 正文 CAS、协作者竞争、终态载荷清理、Owner / Editor / 无权矩阵、目标状态重放、Controller 稳定错误与通知目标失权。
- 后端全量：`954` 项通过，`25` 项 PostgreSQL 环境用例按配置跳过；解决方案相关项目编译为 `0 warning / 0 error`。
- Wiki PostgreSQL migration 重入用例已接入 `RADISH_TEST_POSTGRES_CONNECTION_STRING`；本机没有 PostgreSQL 测试连接且 Docker daemon 未运行，因此本轮未单独实跑，由 Candidate Quality 的 PostgreSQL 服务执行。
- 未启动 Radish 服务，未执行 Gateway、PC / mobile 浏览器或 WebOS smoke；这些运行态路径按停止线留给 F4-G-D。

## 下一顺位

进入 F4-G-C：先更新 PC / mobile Pencil，再迁移正式 `/docs/mine|compose|edit|revisions` 到工作草稿契约，并在现有 Console `/documents` 中加入审核队列和证据工作台。页面必须覆盖中英文、LongId 字符串、冲突恢复、键盘、无障碍、账号 reset 和 WebOS 同组件复用；旧 `Create / Update` 作者写入口只能作为迁移期兼容层，并应在专题结束前移除。
