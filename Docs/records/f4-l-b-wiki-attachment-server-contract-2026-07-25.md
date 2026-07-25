# F4-L-B Wiki 附件服务端权威契约

## 结论

F4-L-B 已完成服务端实现，下一顺位进入 F4-L-C 正式 Web 受保护资源。

本批确认产品层只有一个“文档”App：运行时 Wiki 领域统一使用 `BusinessType=Wiki`；通用 `BusinessType=Document` 仍是独立文件分类，只有已被 Wiki 当前正文、封面、草稿或 Revision 明确引用的历史记录才进入兼容迁移。本批没有修改 Pencil、开发正式页面、安装依赖或启动服务。

## 已完成范围

- Main migration `20260725_012_wiki_attachment_authority` 创建 `WikiAttachmentReference`、来源唯一约束和附件 / 文档查询索引，回填当前正文、封面、草稿和 Revision 引用。
- migration 将全部 Wiki 附件及已证明属于 Wiki 引用的历史 Document 附件设为私有；跨租户、错误业务类型、Draft / Revision 来源不一致会阻断，缺失附件和旧 `/uploads/**` 引用进入严格诊断。
- migration、Repository 与运行时共用同一 Markdown 引用解析器；代码围栏和 inline code 中的示例不会被误认成真实附件。
- `IWikiAttachmentReferenceRepository` 实现来源目标集合同步、Revision 不可变追加、软删除恢复、租户隔离和批量引用查询；SQLite 写入串行化，PostgreSQL 使用来源级 transaction advisory lock，当前正文、草稿与 Revision 不再依赖 `Attachment.BusinessId` 或 Markdown 扫描形成第二真相。
- Wiki 新上传立即写为私有；未绑定附件只允许上传者读取。当前正文 / 封面、Draft 和 Revision 读取统一由 `IWikiAttachmentAccessService` 根据实时文档状态、可见性、Owner、Accepted Collaborator 和现有治理权限判定。
- Public + Published 当前引用允许匿名读取；Authenticated / Restricted、Draft 和 Revision 使用各自权限矩阵。`System / Admin` 角色本身不穿透 Wiki 权限，删除、停用、跨租户和来源失效统一拒绝。
- Author 创建 / 保存、Console 创建 / 更新 / 导入 / 回滚、审核 Apply、正式 Revision 与引用同步进入同一 Main 事务；Draft CAS 和正式版本 CAS 保持既有并发边界。
- 终态 Draft payload 清理在同一 Repository 操作中软删除 DraftContent / DraftCover 引用；孤立附件检查改为消费权威引用，不再重复扫描 Wiki 正文与 Revision。
- Wiki 文件令牌创建、列表和撤销要求当前管理权；下载先校验 token 候选与当前 Wiki ACL，再原子消费次数，拒绝请求不增加 `AccessCount`。
- 已补稳定 `WikiAttachment.*` Code / MessageKey、中英文资源、Wiki / token HTTP 示例和 `@radish/http` 契约。

## 验证结果

- `dotnet build Radish.slnx -c Debug --no-restore`：通过，0 warning / 0 error。
- `dotnet test Radish.Api.Tests --no-build --no-restore`：1049 通过，30 个环境用例因未配置对应环境跳过。
- F4-L 定向测试：102 项通过，2 项 PostgreSQL 环境用例因未配置 `RADISH_TEST_POSTGRES_CONNECTION_STRING` 跳过。
- SQLite 覆盖 migration 首次应用、第二次应用、目标集合重入、跨租户与来源冲突、旧 URL 诊断、备份恢复；Repository 覆盖目标集合同步、软删除恢复、幂等、租户隔离和批量查询。
- PostgreSQL 环境驱动用例覆盖首次 / 二次应用、事务回滚后恢复、严格 verify、Repository 目标集、软删除恢复和并发来源写入；当前会话未启动 PostgreSQL，因此这些用例已编译但未实跑。
- Service 测试覆盖未绑定、Public、Restricted、Draft、Revision、Owner、Collaborator、Reviewer、Admin 不穿透、失权、删除 / 停用、跨租户、令牌拒绝不消费和孤立清理。
- `npm run test --workspace=@radish/http`：20 项通过；`npm run type-check --workspace=@radish/http` 通过。
- `npm run validate:baseline:quick`、`npm run check:docs`、`npm run check:repo-hygiene:changed` 与 `git diff --check` 通过。

## 下一批边界

F4-L-C 只在既有 Author、Authenticated / Restricted Docs、Revision 与 Console 审核页面接入认证二进制加载和 object URL 生命周期，覆盖图片、灯箱、文件下载、上传预览、失败、失权、账号 / 文档切换、中英文、PC / mobile、键盘和无障碍。

本批不新增页面族、不修改 Pencil，也不扩展公开聊天、论坛回滚、内容赞赏、WebOS、Flutter 或 Tauri；完整 Gateway 多身份矩阵、临时数据清理和专题关闭保留到 F4-L-D。
