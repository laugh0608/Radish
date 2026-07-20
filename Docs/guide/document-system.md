# 文档系统设计与实施方案

> 本文定义 Radish 统一文档体系的长期边界，覆盖 **固定文档挂载**、**在线文档管理**、**正式 Web Author 协作**、**Console 审核治理** 与 **Markdown 导入导出**。
>
> **状态**：公开阅读、普通作者协作、Console 审核与既有治理均已进入稳定维护
>
> **最后更新**：2026-07-20
>
> **关联文档**：
> - [前端设计文档](/frontend/design)
> - [开发框架说明](/architecture/framework)
> - [开发规范](/architecture/specifications)
> - [文件上传功能设计](/features/file-upload-design)
> - [作者协作使用说明](/guide/docs-author-collaboration)与 [权威设计](/features/wiki-author-contribution-collaboration-design)
>
> **一句话目标**：以仓库 `Docs/` 作为固定文档真相源，以公开 `/docs` 承接只读阅读，以正式 Web Author 入口承接普通作者与协作者的共享草稿，以 Console 承接审核应用、发布和治理，并保留 Markdown 导入导出作为迁移桥梁。
## 1. 背景与目标

### 1.1 背景

当前 Radish 已具备以下基础条件：

- WebOS 桌面已支持应用注册与窗口化打开；
- `radish.client` 已落地内置“文档”应用；
- `radish.client` 已落地公开 `/docs` 只读阅读与正式 Web 作者入口；
- `radish.console` 已落地 `/documents` 文档治理入口；
- `@radish/ui` 已提供可复用的 `MarkdownEditor` 与 `MarkdownRenderer`；
- 后端已有成熟的附件上传体系，可复用图片/文档上传能力；
- 公开阅读、管理员治理与 F4-G 普通作者贡献协作均已完成；当前文档系统按既定权限、草稿、审核和公开隔离边界稳定维护。

统一文档系统要解决的问题主要有：

1. **固定文档与运行时文档需要统一入口**：项目文档不能长期分散在独立站、目录文件和业务页面里；
2. **缺少正式业务态文档模型**：在线文档需要独立于帖子、评论等内容对象长期存在；
3. **Markdown 能力需要长期治理**：导入、展示、编辑、导出和版本管理需要统一承接；
4. **固定文档需要安全地暴露到运行时**：既要可访问静态资源，又不能把 `Docs/**/*.md` 原文件直接暴露给外部。

### 1.2 目标

本期目标不是“一步到位重做整个文档平台”，而是先把主链路做完整，再分阶段增强：

- 建立固定文档 + 在线文档的统一产品口径；
- 提供固定文档同步、只读展示与内链跳转；
- 提供正式 Web Author 入口下的普通用户创建、显式协作、共享草稿、提交审核、冲突恢复和版本查看；
- 提供 Console 下的审核应用、独立发布、下架、归档、回收站、权限策略和版本回滚治理；
- 提供 Markdown 单文档导入 / 导出；
- 为后续删除恢复、目录治理、搜索与迁移增强建立清晰边界。

### 1.3 非目标

当前阶段 **不包含** 以下内容：

- 实时多人协同编辑；
- ZIP 批量导入导出；
- 全文检索引擎；
- 固定文档在线编辑回写到 `Docs/`；
- 复杂空间权限、跨文档团队空间和多级审核流；
- 富文本块级建模。

## 2. 产品定位与边界

### 2.1 文档体系边界

为避免“开发真相源”和“运行时文档入口”再次混杂，当前统一文档体系分为两层：

1. **固定文档**
   - 文档源统一放在仓库 `Docs/` 目录；
   - 属于项目随仓库交付的固定内容；
   - 由 API 启动时自动扫描、同步并挂载到文档应用；
   - 默认只读，不允许在应用内直接编辑。

2. **在线文档**
   - 由登录用户在正式 Web Author 入口创建，Owner 与 Accepted Editor 共同维护独立工作草稿；
   - 由具备审核权限的 Console 用户批准应用，再由独立治理权限发布、下架、归档、恢复、回滚和调整访问策略；
   - 内容存储在数据库中；
   - 适合作为帮助文档、FAQ、专题说明、迁移稿和整理后的知识沉淀；
   - 支持 Markdown 导入导出与版本历史。

### 2.2 当前阶段产品结论

- 前端统一使用 **“文档”** 作为产品名称；
- 不再保留独立的 `radish.docs` 文档站项目作为当前主线方案；
- 不再对外区分 `docs` / `wiki` 两个应用；
- `Docs/` 是固定项目文档的唯一真相源；
- 公开 `/docs`、正式 Web 作者入口、Console 文档治理和 WebOS 历史文档应用共享同一 Wiki 数据模型，但职责边界不同。
- 公开 `/docs` 只承载 `Published + Public` 文档的目录、搜索、列表和详情。登录可看、受限、草稿、已删除或待治理内容进入正式 Web 作者入口或 Console 文档治理，不在公开阅读页混排。
- 公开 `/docs` 的列表、搜索和详情失败态只提供重试、返回目录和复制诊断；诊断用于支持排障，不暴露固定文档源文件路径、未公开正文、token 或后端堆栈。

### 2.3 P3-12 后的入口归属

| 入口 | 路由 | 面向对象 | 职责 | 明确不承接 |
| --- | --- | --- | --- | --- |
| 公开文档阅读 | `/docs`、`/docs/search`、`/docs/:slug` | 匿名用户与普通登录用户 | 公开已发布文档的只读目录、搜索、正文阅读、公开链接复制、文档内链跳转、加载失败重试与复制诊断 | 登录可看 / 受限 / 草稿 / 已删除文档展示，创建、编辑、发布、归档、回收站、版本回滚、权限治理 |
| 正式 Web Author 入口 | `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id` | 普通 Owner、Pending Invitee、Accepted Editor | 创建、Pending 邀请只读预览、共享草稿 CAS 保存、邀请响应、提交 / 撤回、冲突恢复、审核时间线和正式版本查看 | 审核、发布、下架、归档、删除、恢复、访问策略、回滚、Markdown 导入导出 |
| Console 文档治理 | Gateway `/console/` 下的 `/documents` | Console Reviewer 与其他治理人员 | 待审队列、正文证据、RequestChanges / Reject / Apply、独立发布及既有治理 | 正文日常创作、公开阅读、WebOS 桌面窗口交互 |
| WebOS 文档应用 | `/desktop` 内置文档应用 | 历史桌面入口与兼容维护 | 固定文档与在线文档阅读，以及历史已存在的管理能力 | 不再作为 P3-12 后新增治理能力的默认扩展入口 |

## 3. 设计原则

### 3.1 先打通主链路，再增强

优先保证“同步固定文档 → 浏览 → 创建在线文档 → 导入 → 导出 → 回滚”主链路可用，再逐步补目录治理、删除恢复、搜索与迁移增强。

### 3.2 复用现有基础设施

- 编辑器复用 `@radish/ui` 的 `MarkdownEditor`；
- 渲染器复用 `@radish/ui` 的 `MarkdownRenderer`；
- 附件与图片上传复用现有 `Attachment` / `AttachmentController`；
- WebOS 集成复用现有 `AppRegistry` 与窗口系统。
- 正式 Web 作者入口和公开 `/docs` 复用 `radish.client` 的公开壳层、登录回流和 route state 机制。
- Console 治理入口复用 `radish.console` 的路由权限、按钮权限和 `ConsolePermissions` 资源映射。

### 3.3 固定与在线分治

- 固定文档负责仓库真相源与只读交付；
- 在线文档负责运行时管理、导入、整理与发布；
- 两者统一展示，但数据来源与编辑权限严格分离。

### 3.4 创作、审核与发布分离

普通作者只修改独立工作草稿；Console Reviewer 只批准应用权威正文；Publisher 再独立发布。三者不能通过角色名、前端隐藏或单个动作互相替代。

### 3.5 安全优先

固定文档静态资源必须最小暴露：只开放运行时真正需要的静态资源，不直接暴露 `Docs/**/*.md` 源文件或整个目录树。

## 4. 总体架构

### 4.1 目标架构

```text
Frontend/radish.client
├─ 公开 /docs
│  ├─ 固定文档树 / 在线文档树
│  ├─ 公开搜索
│  └─ 文档详情只读渲染
├─ 正式 Web 作者入口
│  ├─ Owner / Collaborator 列表与邀请响应
│  ├─ 共享草稿 / CAS 保存 / 提交与撤回
│  └─ 冲突恢复 / 审核时间线 / 正式版本查看
└─ WebOS 文档应用（历史入口，稳定维护）

Frontend/radish.console
└─ Documents
   ├─ 待审队列 / 正式正文与草稿证据
   ├─ RequestChanges / Reject / Apply
   ├─ 文档治理列表 / 详情
   ├─ 发布 / 下架 / 归档
   ├─ 删除 / 恢复
   ├─ 访问策略治理
   ├─ Markdown 导入 / 导出
   └─ 版本历史与回滚

Radish.Api
└─ Wiki 模块（内部实现命名暂保留）
   ├─ WikiController
   ├─ WikiDocumentService / Authoring Service
   ├─ WikiDocument / Draft / Collaborator / ReviewEvent / Revision
   ├─ 固定文档启动同步
   └─ 附件系统复用（BusinessType = Wiki）

Docs/
├─ architecture/
├─ guide/
├─ frontend/
├─ features/
├─ deployment/
├─ changelog/
└─ images/
```

### 4.2 分层落点

遵循现有仓库分层规范：

- `Radish.Model`
  - `Models/WikiDocument.cs`
  - `Models/WikiDocumentRevision.cs`
  - `ViewModels/WikiDocumentVo.cs`
  - `ViewModels/WikiDocumentDetailVo.cs`
  - `DtoModels/CreateWikiDocumentDto.cs`
  - `DtoModels/UpdateWikiDocumentDto.cs`
  - `DtoModels/WikiMarkdownImportDto.cs`

- `Radish.IRepository` / `Radish.Repository`
  - 简单 CRUD 复用 `IBaseRepository<TEntity>`；
  - 树形查询、版本查询等复杂场景再扩展仓储。

- `Radish.IService` / `Radish.Service`
  - 使用 `IWikiDocumentService` / `WikiDocumentService`；
  - 同步、导入、导出、发布、版本生成、回滚等逻辑统一放 Service。

- `Radish.Api`
  - 通过 `WikiController` 暴露文档接口；
  - Controller 仅负责协调请求与响应，不直接写业务逻辑。

- `Frontend/radish.client`
  - `src/public/docs/` 承接公开 `/docs` 只读阅读；
  - `src/docs/` 承接正式 Web 作者入口；
  - `src/apps/wiki/` 保留 WebOS 文档应用历史实现；
  - 对外产品名称统一显示为“文档”。

- `Frontend/radish.console`
  - `src/pages/Documents/` 承接 Console 文档治理页；
  - `src/api/wikiGovernanceApi.ts` 统一封装治理接口调用；
  - 路由权限和按钮权限使用 `CONSOLE_PERMISSIONS.DOCS_*` 系列。

## 5. 固定文档机制

### 5.1 目录约定

固定文档直接放在 `Docs/` 下，按目录分类：

- `Docs/architecture/`
- `Docs/guide/`
- `Docs/frontend/`
- `Docs/features/`
- `Docs/deployment/`
- `Docs/changelog/`
- `Docs/images/`

### 5.2 启动同步

API 启动时执行固定文档同步：

- 扫描 `Docs/**/*.md`；
- 读取标题、摘要、正文；
- 自动写入 / 更新固定文档索引；
- 自动补目录节点；
- 将站内链接重写为文档应用可跳转链接；
- 将图片等资源重写为 `StaticAssetsRequestPath` 可访问路径；
- 固定文档视为只读内容，编辑请求应被拒绝。

当前又补了一层启动守卫：

- 若 `Document.ShowBuiltInDocs=false`，启动同步会直接跳过；
- 若 `WikiDocument / WikiDocumentRevision` 任一表尚未初始化，启动同步也会直接跳过，并输出“请先执行 `DbMigrate apply`”的提示日志；
- 也就是说，固定文档启动同步当前不再把“数据库尚未初始化”的一次性时序问题打成错误，而是明确收口为“前置条件未满足，先迁移再同步”。

同时，`DbMigrate apply/seed` 也需要保证 Wiki 表结构在本地 SQLite 环境中可自愈：

- 旧库若仅存在 `WikiDocument` 表、但缺少 `Visibility`、`AllowedRoles`、`AllowedPermissions` 等新增列，不能再被误判为“结构已齐全”；
- 当前 `DbMigrate doctor / verify` 已会显式报告 Wiki 缺列；
- 当前 `DbMigrate apply / seed` 在探测到缺列时会自动触发 `init`，并在 Seed 阶段再次对 `WikiDocument / WikiDocumentRevision` 执行 `CodeFirst.InitTables`，用于补齐缺失表或列。

### 5.3 配置项

统一配置节为 `Document`：

```json
{
  "Document": {
    "ShowBuiltInDocs": true,
    "BuiltInDocsPath": "Docs",
    "StaticAssetsRequestPath": "/docs-assets"
  }
}
```

说明：

- `ShowBuiltInDocs=false`：关闭固定文档展示，仅保留在线文档；
- `BuiltInDocsPath`：固定文档目录；
- `StaticAssetsRequestPath`：图片、附件等静态资源访问前缀，当前仅暴露 `Docs/images/` 下的固定文档静态资源，不直接暴露 `Docs/**/*.md` 源文件。

### 5.4 静态资源边界

当前固定文档静态资源策略：

- 允许访问：`Docs/images/` 下受白名单扩展名约束的静态资源；
- 拒绝访问：目录枚举、路径穿越、`*.md` 源文件和其他不在白名单目录内的文件；
- 保持 URL 口径稳定：Markdown 中仍统一改写为 `/docs-assets/...`。

## 6. 在线文档机制

在线文档继续使用现有数据库文档模型承载，但产品口径统一为“在线文档”：

- 正式 Web 作者入口支持创建、编辑和版本查看；
- Console 治理入口支持发布、下架、归档、删除、恢复、访问策略调整、Markdown 导入 / 导出和版本回滚；
- 支持 `公开可看 / 登录可看 / 指定权限可看` 三层可见性；
- 三层可见性当前落在 `WikiDocument.Visibility`、`WikiDocument.AllowedRoles`、`WikiDocument.AllowedPermissions` 字段；
- 在线新建与 Markdown 导入默认创建为“登录可看”，治理人员可后续调整为公开或受限；
- 当前对固定文档保持只读，对在线文档开放管理；
- 当前已具备回收站视图，后续继续完善目录治理与更友好的排序操作。

## 7. Markdown 导入导出定位

Markdown 导入导出不是固定文档主存储方式，而是 **迁移桥梁**：

- **导入**：把外部平台帖子 / 文档迁移进在线文档；
- **导出**：把在线文档导出为 Markdown，便于备份与迁移。

当前阶段规则：

- 仅支持单个 `.md` / `.markdown` / `.txt` 文本文件导入；
- 导入后生成在线文档与首个版本快照；
- 导出仅导出单篇 Markdown，不附带资源包；
- 附件和图片继续复用现有附件系统，`BusinessType = Wiki`。

### 7.1 本地源与在线编辑冲突后置专题

若后续支持“本地文件镜像文档”或“重新从本地文件导入覆盖已有在线文档”，应先回拉 [文档本地源与在线编辑冲突治理](/planning/document-local-source-conflict-governance)。

当前稳定口径保持不变：固定文档以本地源为权威且只读；Markdown 一次性导入生成在线文档副本，后续以数据库为权威，不回写原本地文件。

## 8. 文档应用设计

### 8.1 应用形态

当前文档系统由四类入口共同组成：

- 产品名：`文档`；
- 公开 `/docs` 面向匿名用户和普通登录用户，严格保持只读；
- 正式 Web Author 入口面向登录用户；Owner、Pending Invitee 和 Accepted Editor 的动作完全由服务端关系与 `VoCan*` 字段裁决；
- Console `/documents` 面向治理人员，按 `console.docs.*` 资源授权；
- WebOS 文档应用保留为历史桌面入口，当前以稳定维护为主。

### 8.2 当前页面结构

WebOS 文档应用当前实现以单应用工作台为主，并转入稳定维护：

- 左侧承载目录树 / 检索结果、统计与常用操作，目录节点支持加载、展开、收起和祖先自动展开；
- 中部承载详情、编辑器、历史版本弹窗和 Markdown 正文渲染；
- 窄窗口下切换为单栏布局，优先保证目录与正文完整可读；
- 当前视觉采用浅色工作台风格，深浅主题切换留待统一主题系统落地后再处理。

### 8.3 现阶段可用能力

当前文档系统已具备：

- 公开 `/docs` 下固定文档与在线文档统一只读展示；
- 公开 `/docs/search` 下关键词搜索、分页和详情回跳；
- 正式 Web 作者入口下普通用户创建、协作者邀请 / 响应、共享工作草稿、提交 / 撤回、冲突恢复和审核时间线；
- 正式 Web 作者库对内置固定文档和已删除文档展示只读原因，编辑入口只对非内置、未删除文档开放；
- Console `/documents` 下待审队列、正文证据、RequestChanges / Reject / Apply，以及发布、下架、归档、删除、恢复、访问策略调整、Markdown 导入 / 导出和版本回滚；
- WebOS 文档应用下历史工作台阅读和管理能力；
- 文档三层可见性：`公开可看 / 登录可看 / 指定权限可看`；
- 文档内链按 Slug 跳转；
- 图片与文档附件上传；
- 父级下拉选择、子孙节点禁选与同级排序建议值；
- 基于 Node 24 原生测试运行器的前端纯逻辑回归基线。
- 文档应用已完成浅色化与侧栏/主内容区重排，提升目录浏览与阅读空间。

### 8.3.1 多语言展示与错误契约

正式 Web 作者入口与 Console 文档治理使用各自宿主的 `docs.* / documents.*` 中英文资源，但共享以下稳定边界：

- 文档状态、可见性和来源类型使用稳定字段映射系统词元；筛选、按钮权限和样式不读取中文或英文展示文本。
- 已知来源类型映射为本地文案，未知来源类型显示稳定原值。文档标题、摘要、Slug、Markdown 正文、允许角色 / 权限键、修订说明、导入文件名等内容保持原文，不做自动翻译。
- 日期与数字按当前 locale 格式化；角色数、权限数、版本数等英文数量文案使用 i18next plural 规则。
- Client Wiki API 与 Console Wiki 治理 API 统一通过 `@radish/http` 解析，并在失败时抛出保留 `httpStatus / code / messageKey / traceId` 的 `ApiResponseError`。页面通过状态码、`Code` 或数据状态决定 not-found、冲突和权限分支，不匹配 `MessageInfo` 文本。
- Wiki 高频失败使用稳定 `Wiki.*` Code 与 `error.wiki.*` MessageKey：参数或导入校验为 `400`，文档 / 修订不存在或不可访问为 `404`，恢复或回滚冲突为 `409`；服务端双语 `MessageInfo` 仅作安全回退。
- Markdown 导入 / 导出等必须使用 `apiFetch` 的场景仍需解析 JSON 错误体并保留结构化字段，不能退回独立 fetch 错误约定。

### 8.4 现阶段主要缺口

当前仍需继续完善的能力：

- 更完整的目录治理能力，例如批量重排、拖拽排序或同级自动插槽；
- 更完整的前端自动化回归，例如覆盖组件交互与端到端场景；
- 正文搜索、分享与治理增强能力的后续评估；

## 9. 权限与租户策略

### 9.1 权限建议

当前采用稳妥分层：

- 浏览已发布文档按可见性分层：
  - `Public`：匿名可见；
  - `Authenticated`：仅登录用户可见；
  - `Restricted`：仅匹配角色或权限键的登录用户可见；
- 正式 Web Author 入口面向登录用户开放；创建者成为显式 Owner，Accepted Editor 共享活跃草稿，Pending Invitee 可只读预览草稿与协作上下文并响应，但不能保存；
- 草稿保存、提交、审核 Apply 和下一稿均由服务端状态、Owner / Collaborator 关系与 CAS 版本裁决，不依赖前端角色名猜测；
- Console 文档治理使用以下权限键：
  - `console.docs.view`
  - `console.docs.review`
  - `console.docs.publish`
  - `console.docs.archive`
  - `console.docs.delete`
  - `console.docs.restore`
  - `console.docs.permissions`
  - `console.docs.rollback`
  - `console.docs.import`
  - `console.docs.export`
- 固定文档默认同步为 `Public` 且只读，不允许在线修改、删除或回写仓库；
- 在线新建文档与 Markdown 导入文档默认创建为 `Authenticated`；
- `Restricted` 文档必须至少配置一个允许角色或允许权限。

### 9.2 租户口径

当前文档模块仍遵循项目现有公共租户基线：

- 默认写入 `TenantId = 0`；
- 查询过滤遵循当前公共租户口径；
- 后续若启用实际多租户，再扩展为租户级文档空间。

## 10. 迁移策略

### 10.1 长期保留在固定文档中的内容

以下内容长期保留在仓库 `Docs/` 中，作为项目真相源：

- 架构设计；
- 开发规范；
- 里程碑与开发计划；
- 部署与运维说明；
- 协作者约束与技术决策；
- 周报 / 月报 / Changelog。

### 10.2 适合进入在线文档的内容

优先迁移或整理为在线文档的内容：

- 用户使用帮助；
- 产品功能说明；
- FAQ；
- 活动说明；
- 面向普通用户的图文教程；
- 需要在运营或管理侧持续维护的说明稿。

### 10.3 迁移原则

- 固定文档优先承载“项目真相源”；
- 在线文档优先承载“运行时可治理内容”；
- 不为了统一而强行迁移所有历史文档；
- 先试点少量可用户消费的内容，再评估更大范围迁移。

## 11. 当前实施顺序与下一步

### 11.1 已完成

- 删除独立 `radish.docs` 工程，将文档源统一迁移到 `Docs/`，并通过 `Document.ShowBuiltInDocs` 控制固定文档同步。
- 打通 API 启动同步、静态资源边界、Wiki 三层可见性字段和 SQLite 本地库自愈。
- 落地 WebOS 文档应用、Markdown 导入导出、版本历史与回滚等历史主链路。
- 落地公开 `/docs`、`/docs/search`、`/docs/:slug` 只读阅读入口。
- 落地正式 Web 作者入口：`/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id`。
- 落地普通作者 Owner / Collaborator、独立工作草稿、双版本 CAS、提交 / 撤回、冲突恢复和审核时间线。
- 落地 Console `/documents` 待审队列、正文证据、RequestChanges / Reject / Apply，并与独立 `console.docs.review`、后端资源映射和种子数据对齐。
- 落地 `20260720_007_wiki_author_collaboration` 显式迁移、可靠通知和终态草稿正文保留清理。

### 11.2 当前稳定口径

当前文档系统稳定口径如下：

- 公开 `/docs` 只承接阅读、搜索、内链、分享链接复制和登录回流，不承接治理动作；
- 正式 Web Author 入口承接 Owner 创建、协作者邀请 / 响应、共享草稿编辑、提交 / 撤回、冲突恢复和审核时间线；内置固定文档保持只读保护；
- 作者库必须明确展示不可编辑原因：内置固定文档显示内置只读，已删除文档显示已删除只读，不用静默隐藏编辑入口掩盖数据态；
- Console `/documents` 承接审核与治理动作，包括 RequestChanges / Reject / Apply、发布、下架、归档、删除、恢复、访问策略、版本回滚、Markdown 导入和导出；Apply 不自动 Publish；
- 桌面 WebOS 文档应用当前以稳定维护和历史兼容为主，不再作为新增治理能力的默认扩展入口；
- 文档源码 `Docs/` 仍是项目说明书 / Wiki 的唯一真相源，不允许运行时回写仓库文件；
- 公开文档详情仍按“阅读入口 -> 文档正文 -> 阅读说明”的顺序组织，正文前只展示标题、摘要、访问属性、文档属性和时间线；
- 访问属性只展示可见性与发布状态；文档属性展示 slug 与来源类型；来源类型应使用可读文案，不把 `builtin / manual / imported` 等内部枚举直接暴露给普通用户；
- 详情路径加载完成后如需要规范化 slug，应用内替换必须保留当前标签页的来源返回状态；从 `/discover`、`/me` 或搜索结果进入详情后，返回语义不能被 canonical 替换清空；
- Flutter 原生文档详情复用公开文档路由口径；公开链接复制使用 Gateway Base URL 加 `/docs/:slug`，不复制 Flutter 内部 handoff、来源 tab 或旧 long 兼容路径。

### 11.3 阶段判断

截至当前收尾版本，文档系统已具备以下主链路：

- 固定文档自动同步与只读展示；
- 公开 `/docs` 目录、搜索、正文阅读、复制公开链接和来源返回；
- 正式 Web Author 入口下普通作者创建、显式协作、草稿 CAS、提交 / 撤回和审核结果回看；
- Console 下在线文档发布、下架、归档、删除、回收站查看与恢复；
- Console 下访问策略调整、Markdown 单文件导入 / 单篇导出和版本回滚；
- 目录树分级折叠与窄窗口单栏自适应；
- 父级下拉选择、子孙节点禁选、同级排序建议值；
- 固定文档静态资源边界收紧；
- `WikiProfile` 已注册进 AutoMapper，补齐文档详情映射链路；
- 在线文档 slug 唯一性判定已按创建 / 编辑拆分，规避 SQLite 下的 SqlSugar 复合表达式兼容问题；
- 文档应用浅色 UI 与 Markdown 正文 / 代码样式已完成第二轮收口；
- 基础前端纯逻辑回归基线。

基于以上结果，当前判断为：

- **文档应用公开阅读、普通作者贡献与协作、Console 审核治理均已完成成组验收并进入稳定维护**；
- **P3-12 的公开阅读、正式 Web 作者入口和 Console 文档治理已完成首批拆分**；
- 当前没有公开阅读、普通作者协作或既有治理阻断；未审核正文、批准但未发布正文和已驳回草稿均保持公开隔离；
- 目录治理能力先维持当前父级禁选、回收站与排序建议方案，不继续提前扩成拖拽式重排；
- 公开内容壳层文档阅读不与 Console 治理链路混为一体。

后续增强只按已批准专题推进；公开阅读、正式 Web 作者入口和 Console 治理入口的职责拆分应保持稳定。

## 12. 验收标准

当前阶段完成需满足：

1. 公开 `/docs` 可匿名或登录态浏览公开已发布文档，且不会因匿名请求持续触发 token 刷新报错；
2. 固定文档能自动同步并只读展示，不暴露 Markdown 源文件；
3. 正式 Web Author 入口可完成普通用户创建、邀请 / 响应、共享草稿 CAS 保存、提交 / 撤回、冲突恢复和审核结果回看，且内置固定文档不能被编辑；
4. Console 文档治理可完成 RequestChanges / Reject / Apply、独立发布、下架、归档、删除、恢复、访问策略调整、Markdown 导入 / 导出和版本回滚；
5. 文档三层可见性 `Public / Authenticated / Restricted` 在公开阅读、作者入口和 Console 回看中表现一致：公开阅读只展示公开已发布内容，作者入口和 Console 负责展示非公开状态与治理动作；
6. 符合当前项目分层与 `Vo` / `Dto` / `Service` / `Controller` 规范。

### 12.1 文档系统人工验收模板（样板）

> 结构遵循：[人工验收模板](/records/manual-acceptance-template)
>
> 2026-03-09 补充：已按业务侧口径完成一轮人工验收，文档主线、在线文档治理、导入导出、版本回滚、入口口径与主应用基础浏览均未发现阻断继续开发的问题。
>
> 公开内容壳层下 `/docs`、`/docs/search`、`/docs/:slug` 的目录阅读、搜索回跳、分享入口与来源返回，统一改看 [docs 公开阅读首批人工验收清单](/records/docs-public-acceptance)。

#### 12.1.1 适用改动范围

- 公开 `/docs`、`/docs/search`、`/docs/:slug` 改动后
- 正式 Web 作者入口 `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id` 改动后
- Console `/documents` 文档治理改动后
- 文档树、文档详情、固定文档挂载、Markdown 导入导出、版本历史、回滚改动后
- Wiki API、前端路由、深链、侧栏交互改动后

#### 12.1.2 前置条件

- 已启动 `Radish.Api`、`Radish.Auth`、`Radish.Gateway`
- 已准备匿名环境、普通 Owner、普通 Collaborator、普通无权用户，以及具备 `console.docs.review` 和所需治理权限的 Console 账号
- 如验证 WebOS 历史入口，需已可在 WebOS 中打开文档应用
- 已准备至少一个可导入的 Markdown 文件，必要时准备现成在线文档用于回滚 / 删除恢复验证

#### 12.1.3 联调资产

- `Radish.Api.Tests/HttpTest/Radish.Api.Wiki.http`
- `npm run test --workspace=radish.client`
- `npm run build --workspace=radish.client`
- `npm run build --workspace=radish.console`
- [专题回归索引](/guide/regression-index)

#### 12.1.4 人工验收顺序

1. 未登录打开 `/docs` 和至少一个 `/docs/:slug` 详情，确认公开目录与详情只展示公开已发布文档，且控制台不再出现匿名 401 refresh 噪音。
2. 普通 Owner 在 `/docs/compose` 创建文档和首份草稿，确认 `/docs/mine` 展示 Owner、草稿版本、审核状态和服务端允许动作。
3. Owner 使用用户 PublicId 邀请 Collaborator；受邀者在 Pending 状态可只读查看共享草稿并响应，接受后才可与 Owner 保存。
4. Owner 与 Collaborator 使用相同旧 `ExpectedDraftVersion` 分别保存，确认只允许一个成功，冲突页面保留本地文本并提供复制、下载和重载。
5. Owner 提交后确认草稿只读；Console `/documents` 查看正式正文 / 草稿证据并分别验证 RequestChanges、Reject 和 Apply，Apply 不自动 Publish。
6. 对 RequestChanges 修改并重新提交；对终态草稿由 Owner 开启下一稿，确认审核事件和 `/docs/revisions/:id` 正式版本边界清楚。
7. 使用独立 Publish 权限发布已 Apply 文档，再验证下架、归档和 `Public / Authenticated / Restricted` 访问策略；公开入口始终只列出 `Public + Published` 内容。
8. 撤销 Collaborator 后确认已打开页面的读取 / 保存失败；普通无权用户无法据响应判断草稿是否存在。
9. 在 Console `/documents` 抽查 Markdown 导入 / 导出、版本回滚、删除 / 回收站 / 恢复，确认这些治理动作不进入 Author 页面。

#### 12.1.5 预期结果

- 固定文档与在线文档边界清晰：固定文档只读，在线文档可治理。
- 文档浏览权限符合三层可见性口径：公开阅读页只展示公开已发布文档；登录文档需登录，受限文档需命中角色或权限，均不在公开阅读页混排。
- 创建、协作、CAS 保存、提交 / 撤回、审核应用、独立发布及既有治理主链路可完成。
- 正式 Web 作者入口与 Console 治理入口职责不互相混用。
- 目录树、深链与详情展示保持一致，不出现“列表状态已变更但详情未刷新”的断链现象。

#### 12.1.6 可跳过项

- 仅后端字段或 Service 细节改动、未影响前端展示时，可跳过窄窗口布局与视觉协调检查。
- 未涉及导入导出、版本历史时，可将对应步骤降级为抽查而非必跑。
- 纯固定文档内容更新时，可跳过在线治理链路，仅验证固定文档展示与内链跳转。

#### 12.1.7 结论记录格式

```md
- 验收日期：2026-03-17
- 验收人：<name>
- 验收范围：文档系统公开阅读 / 作者入口 / Console 治理
- 执行入口：Radish.Api.Wiki.http + Gateway /docs + Gateway /console/documents
- 结果：通过 / 阻塞
- 备注：<如有异常，记录文档类型、Slug、操作顺序与现象>
```

### 12.2 详细验收清单

建议至少完成以下验收动作，并记录结果：

- [ ] **固定文档展示**：启动后能在公开 `/docs` 或文档应用中看到来自 `Docs/` 的固定文档，且正文渲染正常；
- [ ] **匿名公开浏览**：未登录时可打开 `/docs` 并浏览公开已发布文档，不再出现匿名 refresh 噪音；
- [ ] **正式 Web Author 入口**：`/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id` 可按登录态、Owner / Invitee / Editor 关系和服务端 `VoCan*` 进入；
- [ ] **Console 治理入口**：Gateway `/console/` 下 `/documents` 入口受 `console.docs.view` 控制，按钮受对应 `console.docs.*` 控制；
- [ ] **目录树交互**：目录树支持分级展开 / 折叠；窄窗口下自动切为单栏布局后，目录与正文仍可正常浏览；
- [ ] **固定文档只读**：固定文档不出现编辑、删除、恢复等在线治理按钮；作者库应展示内置只读原因；
- [ ] **内链跳转**：点击 Markdown 中指向其他文档的内部链接时，能按 Slug 正确打开目标文档；
- [ ] **新建与协作**：普通 Owner 可创建文档和首份草稿，通过 PublicId 邀请协作者；Pending 可只读预览并响应，Accepted Editor 才可共享保存；
- [ ] **草稿并发**：保存携带 `ExpectedDraftVersion`，并发冲突保留本地文本；提交后只读，撤回或 RequestChanges 后按状态恢复编辑；
- [ ] **审核与发布分离**：`console.docs.review` 可 RequestChanges / Reject / Apply，Apply 校验草稿与正式版本并生成 Revision，但不会自动 Publish；
- [ ] **可见性配置**：在线文档可配置 `Public / Authenticated / Restricted`；受限文档未配置角色或权限时会被阻止保存；
- [ ] **访问控制生效**：公开阅读页只展示公开已发布内容；作者入口和 Console 中匿名用户、普通登录用户、命中权限用户看到的文档范围符合预期；
- [ ] **目录关系保护**：编辑文档时，父级下拉中不会允许选择当前文档自身及其子孙节点；
- [ ] **排序建议可用**：切换父级时，排序建议值会联动变化，且可一键采用建议值；
- [ ] **发布与归档**：已 Apply 的权威正文由独立权限发布；已发布文档可下架或归档，状态展示与列表筛选一致；
- [ ] **删除与恢复**：在线文档删除后进入回收站，可在回收站中查看并恢复；带子文档的节点会被阻止直接删除；
- [ ] **导入与导出**：可导入单个 Markdown 文件生成在线文档，也可将在线文档导出为 Markdown；
- [ ] **版本历史与回滚**：编辑后能看到版本历史，回滚后会生成新的回滚版本且正文恢复正确；
- [ ] **回收站筛选**：管理员切换“正常文档 / 回收站”视图时，列表与详情加载符合预期；
- [ ] **前端回归基线**：`npm run test --workspace=radish.client` 通过；同时确认文档应用侧栏、浅色正文与代码高亮视觉协调；
- [ ] **前端构建**：`npm run build --workspace=radish.client` 与 `npm run build --workspace=radish.console` 通过；
- [ ] **后端定向测试**：文档系统相关定向 `dotnet test` 通过。

## 13. 风险与应对

- 范围失控：空间、标签、实时协同编辑、全文检索和多级审核必须另起专题，不混入当前单草稿、单级审核维护口径。
- 固定与在线边界混乱：固定文档继续只读，在线文档继续走数据库治理，不允许运行时回写 `Docs/`。
- 静态资源暴露过宽：维持显式资源目录白名单，默认拒绝目录和 Markdown 源文件访问。
- 治理体验不足：优先按实际反馈补目录治理、删除恢复和必要前端交互，不提前扩成复杂空间模型。

## 14. 备注

当前实现阶段允许内部模块继续沿用 `Wiki` 命名的代码实体与接口，以降低改造成本；但对外产品口径、文档与前端界面统一使用“文档”。
