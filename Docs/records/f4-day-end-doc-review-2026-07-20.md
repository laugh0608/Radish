# F4 2026-07-20 日终提交回顾与文档审阅

> 日期：2026-07-20（Asia/Shanghai）
>
> 范围：今日 6 个业务与集成提交；本次日终文档提交自身不计入回顾范围。

## 今日结论

- PR `#64` 已完成 `dev -> master` 集成和 `master -> dev` fast-forward 回灌，稳定主线与协作分支当时统一到 `8f8fc6e2`；本批没有创建 tag、镜像或部署。
- F4-G 按 A-D 四批完成 Docs / Wiki 普通作者贡献与协作：权威设计、服务端草稿 / 协作 / 审核契约、Author / Console 正式页面、Gateway 成组验收全部关闭。
- Wiki 已从“管理员直接修改正文”收敛为“已批准正文 + 独立工作草稿”：普通所有者和已接受协作者编辑草稿，具备 `console.docs.review` 的审核者批准应用，发布继续是独立治理动作。
- 旧 SQLite 主库升级路径暴露的启动前映射和迁移顺序问题已修复；`20260720_007_wiki_author_collaboration` 可在旧库上前滚并通过严格 verify。
- 成组验收修复 Repository 生成 ID 回传、待接受邀请只读访问、终态草稿后开启下一稿和 `DocsAuthorDraft` 通知目标校验四个共同根因。
- 临时账号、草稿、审核、通知、授权和验收备份均已清理；精确残留为 `0`，六库完整性和 DbMigrate verify 通过。

## 今日全部提交

| 提交 | 主题 | 日终结论 |
| --- | --- | --- |
| `8f8fc6e2` | `feat: 集成发布后首批功能完成成果 (#64)` | required checks 通过，完成稳定主线集成与回灌，不触发发布。 |
| `7b908a31` | `docs(wiki): 明确普通作者贡献与协作边界` | 固定 Owner / Collaborator / Draft / Review Event、双版本 CAS、可靠通知、保留策略和 A-D 停止线。 |
| `60e79420` | `feat(wiki): 建立普通作者协作服务端契约` | migration、Repository、Service、Author / Console API、审核权限、通知和终态载荷清理落地。 |
| `52c2f557` | `feat(wiki): 完成作者协作与审核页面` | Author / Console PC / mobile 页面、冲突恢复、审核证据和 WebOS 同 API 兼容完成，旧作者直写 HTTP 入口删除。 |
| `571d922f` | `fix(db): 修复 Wiki 旧库迁移启动与升级` | 修复 DbMigrate 启动映射依赖和旧库 migration 顺序，补启动与 SQLite 升级回归。 |
| `b9e56679` | `fix(wiki): 收口作者协作成组验收` | Gateway 多角色矩阵通过，四个共同根因收口并关闭 F4-G。 |

## 代码与文档交叉审阅

### Wiki 权威边界

- `WikiDocument` 继续只承载已批准正文和发布生命周期；`WikiDocumentDraft` 承载唯一活跃工作草稿，`WikiDocumentCollaborator` 承载显式邀请，`WikiDocumentReviewEvent` 承载追加式审核证据。
- 作者保存使用 `ExpectedDraftVersion`，审核 Apply 同时校验正式版本、草稿版本和活跃草稿指针；未审核、批准但未发布及已驳回正文都不会进入公开读取面。
- Author 入口只按 Owner / Accepted Editor / Administrator 和服务端 `VoCan*` 裁决；Pending Invitee 只能读取邀请上下文并响应，不能编辑。
- Console 复用 `/documents` 并新增 `console.docs.review`，RequestChanges / Reject / Apply 与 Publish、访问策略、归档、删除和回滚保持分离。

### 数据库、后台任务与通知

- Main ledger migration 已登记 Owner、ActiveDraft、Draft、Collaborator、Review Event 及索引 / 约束，并覆盖旧库安全归属回填。
- DbMigrate 在读取 Wiki 新实体映射前先完成所需 schema adoption / migration，避免旧库因缺列提前启动失败；相关启动与迁移测试已补齐。
- 终态草稿正文按 `Document.Authoring.TerminalDraftRetentionDays` 小批次清空，审核事件和正式 Revision 保留。
- Wiki 邀请和审核通知使用结构化 `DocsAuthorDraft` 目标；通知服务已补目标合法性校验，失权后目标解析不可继续打开草稿。

### 本次修正的文档漂移

- `document-system.md` 原先仍写“普通作者、协作者和审核未实现”，并保留 `SystemOrAdmin` 作者入口口径；已按 F4-G 完成事实更新。
- 发布后功能完成线仍停在 F4-G-B；已更新为 F4-G A-D 关闭并进入 F4-H-A。
- Console 权限覆盖矩阵漏记 `console.docs.review` 和三项审核 API；已补齐。
- Chat 总览虽然功能本身无误，但下一工程顺位仍停在 F4-G-A；已校准为 F4-H-A。
- 7 月月志和年度入口仍停在发布候选早期；已补正式发布、F1-F4 功能完成和 F4-G 关闭的当前摘要。
- `api-index.md` 已正确列出 Author / Review 接口和权限边界，F4-G 专题设计及 B / C / D 批次记录与当前代码一致，本次无需重复改写。

## 今日验证回顾

- F4-G-B：后端全量 `954` 项通过，`25` 项 PostgreSQL 环境用例按配置跳过；相关项目构建 `0 warning / 0 error`。
- F4-G-C：`@radish/http 18`、`@radish/ui 24`、client `449`、Console `57` 项通过；Wiki 后端定向 `47` 项通过，Client / Console production build、解决方案构建和 Baseline Quick 通过。
- F4-G-D：Wiki Authoring / Repository / Notification 定向 `22/22` 通过，client production build、Baseline Quick 和 DbMigrate verify 通过。
- Gateway 成组验收覆盖所有者、协作者、无权用户、审核者、`zh / en × PC / mobile`、CAS 冲突、失权、审核、独立发布和公开隔离。
- 本次日终文档批次只执行文档链接 / 篇幅检查、仓库卫生和 `git diff --cached --check`，不重复启动服务或运行浏览器 smoke。

## 明日事项

1. 进入 `F4-H-A 功能完成线候选复核与专题裁决`，优先向用户确认是否采用“电子宠物公开名片与隐私闭环”作为唯一候选。
2. 推荐依据：`/pet` 已持久化 `PetProfile.IsPublic` 和 `pet_` PublicId，页面允许用户开启“公开展示宠物名片”，但当前没有匿名公开读取契约，公开个人主页也没有宠物消费面，形成明确的产品承诺断点。
3. 若获批准，先更新宠物权威专题设计，固定公开卡片字段、关闭开关后的即时隐藏、PublicId / 租户 / 软删除边界、公开个人主页归属、A-D 批次和验收矩阵，再进入服务端实现。
4. 公开卡片只允许名称、形态、成长阶段、心情和安全装扮摘要；不得暴露饱食度、清洁度、精力、成长值、最后照顾时间、流水、动作资格或内部 LongId。
5. 不扩展宠物经济、商城物品、社区任务、通知提醒、独立宠物 SEO 页面、WebOS 新功能、Flutter 或 Tauri；未获批准前不修改接口、模型、Pencil 或业务代码。

## 当前状态

- 当前工作区在本次日终提交后应保持干净。
- F4-G 已关闭，没有遗留待运行的服务或临时验收数据。
- 下一轮从 F4-H-A 设计裁决开始，不沿用今天的服务启动状态或种子登录会话。
