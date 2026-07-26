# F4-M-B 论坛内容版本服务端权威契约

## 结论

F4-M-B 已完成服务端权威契约，下一顺位进入 F4-M-C Pencil 与正式页面。

本批为帖子和评论建立不可变完整 Revision、当前版本 CAS、历史基线和受权安全恢复；现有正式 Web 编辑入口同步透传 `ExpectedContentRevision`，但没有提前开发版本页面、修改 Pencil、启动服务或执行 Gateway 浏览器验收。

## 已完成范围

- Main migration `20260726_013_forum_content_revision` 新增 `PostContentRevision`、`PostContentRevisionTag`、`CommentContentRevision`、`ForumContentRevisionAttachment`，并为 `Post / Comment` 增加正整数 `ContentRevision`。
- migration 为既有帖子和评论生成当前状态基线，完整保存帖子标题、正文、内容类型、分类、标签、封面与正文附件引用，以及评论正文与附件引用；无法证明分类、标签或附件完整性的历史状态显式标记 `LegacyIncomplete`，不猜测、不补造关系。
- 普通编辑改为以 `ExpectedContentRevision` 执行原子 CAS；成功后递增 `EditCount / ContentRevision` 并追加完整 Revision。旧 `PostEditHistory / CommentEditHistory` 保留受权只读兼容，新写入不再双写或裁剪旧历史。
- 发布帖子和创建评论在同一 Main 事务追加基线版本；编辑、分类计数、标签同步、附件绑定、Revision 和提交意图完成保持同一事务边界。
- 恢复只允许当前作者或既有 `System / Admin`，要求目标 Revision 完整且当前分类、标签、附件仍有效；恢复沿用现有内容规则、编辑次数和评论时间窗，并以新 Revision 记录 `RestoredFromRevisionId`，不修改旧版本或倒退当前指针。
- 恢复和普通编辑均接入 `ContentSubmissionRecord`；成功结果保存不可变 Revision Id，网络重放不会漂移到后续当前版本。
- 新增帖子 / 评论公开摘要、受权版本列表、详情和恢复 API；旧全文历史接口收口为作者 / 管理员受权访问。
- 附件孤立清理把 `ForumContentRevisionAttachment` 纳入权威引用集合，历史版本仍引用的资产不会被当作孤立附件删除。
- 新增 `Forum.Revision*` 稳定错误码与中英文资源；`@radish/http` 提供统一版本摘要、详情、恢复请求和写入结果契约，`radish.client` 现有编辑请求已透传版本号。

## 验证结果

- `dotnet build Radish.slnx -c Debug --no-restore`：通过，0 warning / 0 error。
- `dotnet test Radish.Api.Tests --no-build --no-restore`：1057 项通过，31 个 PostgreSQL 等环境驱动用例按配置跳过。
- F4-M 定向测试覆盖 SQLite migration 首次应用、重入、完整 / 不完整基线、附件关系、版本快照写入、公开摘要裁剪、过期版本冲突和既有论坛写入回归。
- `npm test --workspace=@radish/http`：23 项通过，包含论坛版本稳定错误码契约。
- `npm run type-check --workspace=@radish/http`：通过。
- `npm run build --workspace=radish.client`：通过；仅保留仓库既有大 chunk 提示。
- `npm run validate:baseline:quick`、`npm run check:docs`、`npm run check:repo-hygiene:changed` 与 `git diff --check`：通过。
- 本批未启动 API、Auth、Gateway 或前端服务，未执行 PC / mobile 浏览器 smoke；运行态矩阵保留到 F4-M-D。

## 下一批边界

F4-M-C 先更新 PC / mobile Pencil 的帖子与评论版本工作流，再在正式 Web 接入公开编辑摘要、作者版本列表、详情对比、恢复确认、冲突内容保留和“使用此版本编辑”，并覆盖双语、四主题、键盘、长正文、加载失败和失权清理。

本批不新增 Console、Flutter、WebOS 或 Tauri 实现；F4-M-C 不启动服务、不执行 Gateway smoke，真实多身份 PC / mobile 矩阵、PostgreSQL 实跑、临时数据清理和专题关闭保留到 F4-M-D。
