# P3-12-B4-2 Console 文档治理设计

> 日期：2026-06-22（Asia/Shanghai）
>
> 状态：设计方案已确认，待代码实现
>
> 结论：Console 新增文档治理入口，承接文档全量筛选、发布 / 下架、归档、回收站、权限策略、版本治理、导入导出和内置文档观察；正式 Web 作者入口继续负责普通创建、编辑和版本回看；公开 `/docs` 继续保持阅读、搜索、正文内链和分享；WebOS `WikiApp` 只作为 `/desktop` 历史维护入口保留。

## 本轮范围

本轮只补设计边界和后续实现口径，不进入代码实现，不启动 API / Auth / Gateway / Vite，不执行真实浏览器 smoke。

已核对：

- [当前进行中](/planning/current)
- [P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)
- [P3-12-B4 文档作者态归属裁决](/records/p3-12-b4-doc-author-ownership-plan-2026-06-22)
- [验证基线说明](/guide/validation-baseline)
- [Console 权限覆盖矩阵](/guide/console-permission-coverage-matrix)
- `Radish.Api/Controllers/WikiController.cs`
- `Radish.Common/PermissionTool/ConsolePermissions.cs`
- `Radish.DbMigrate/InitialDataSeeder.ConsoleAuthorization.cs`
- `Frontend/radish.console/src/constants/permissions.ts`
- `Frontend/radish.console/src/router/routes.ts`
- `Frontend/radish.console/src/router/routeMeta.ts`
- `Radish.Model/WikiDocument.cs`
- `Radish.Model/DtoModels/WikiDocumentDto.cs`
- `Radish.Model/ViewModels/WikiDocumentVo.cs`
- `Radish.Shared/CustomEnum/WikiDocumentStatusEnum.cs`
- `Radish.Shared/CustomEnum/WikiDocumentVisibilityEnum.cs`
- `Radish.Service/WikiDocumentService.cs`
- `Radish.Service/WikiDocumentService.BuiltIn.cs`

## 职责边界

| 入口 | 职责 | 不承接 |
| --- | --- | --- |
| 公开 `/docs` | 文档目录、搜索、详情阅读、正文内链、公开链接复制、公开分享与 head / sitemap 语义 | 创建、编辑、发布、回收站、版本治理、权限配置 |
| 正式 Web 作者入口 | `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id`，承接普通手写文档创建、正文编辑、基础信息编辑和版本回看 | 发布 / 下架、归档、恢复、权限策略、导入导出、回滚、审核 |
| Console 文档治理 | 全量筛选、状态治理、回收站、权限策略、版本回滚、导入导出、内置文档观察和治理留痕入口 | 公开阅读壳层、作者创作主流程、公开 docs UI 重设计 |
| WebOS `WikiApp` | `/desktop` 历史维护入口，保留旧窗口工作台能力 | 新作者态、新公开 URL 契约、新 Console 权限语义 |

该边界要求后续实现时不把公开 `/docs/:slug` 混入治理按钮，也不把 WebOS 窗口参数、Dock、`openApp` 或工作台状态迁入正式 Web / Console。

## Console 承接动作

### 查询与筛选

Console 需要具备治理视角的全量查询：

- 关键词、父级、状态、可见性、来源类型筛选。
- `Draft / Published / Archived` 状态筛选。
- `Public / Authenticated / Restricted` 可见性筛选。
- `Manual / Imported / BuiltIn` 来源观察。
- 包含已删除、仅看回收站。
- 查看文档详情、当前版本、发布时间、创建 / 修改时间、删除信息。

公开读取接口继续按当前公开阅读语义过滤，不承担治理全量查询。

### 状态治理

Console 承接：

- 发布：`Draft / Archived -> Published`。
- 下架：`Published -> Draft`。
- 归档：`Draft / Published -> Archived`。
- 转回草稿：`Archived -> Draft`，可复用后端下架语义，但 Console 文案必须区分。

状态治理只改变 `Status / PublishedAt / Modify*` 等生命周期字段，不改变正文、不改变可见性、不自动删除。

### 回收站治理

Console 承接：

- 软删除非内置文档。
- 查看回收站。
- 恢复已删除文档。

恢复后保留删除前状态；若父级文档不可用，仍由服务层校验并返回明确错误。回收站治理不做物理删除。

### 权限策略治理

Console 承接：

- 修改 `Visibility`。
- 当 `Visibility=Restricted` 时配置 `AllowedRoles / AllowedPermissions`。
- 展示受限策略对公开阅读、登录态阅读和 Console 治理读取的影响说明。

实现阶段建议新增独立治理接口，例如 `UpdateAccessPolicy`。不要继续把权限策略治理隐藏在通用正文 `Update` DTO 里，否则会让“正文编辑权限”和“访问策略治理权限”边界混乱。

### 版本治理

Console 承接：

- 版本列表。
- 版本详情。
- 回滚到指定版本。

回滚只恢复标题和正文内容，并生成一个新版本；不改变当前 `Status`、`Visibility`、`AllowedRoles`、`AllowedPermissions`、删除态或父级结构。

### 导入导出

Console 承接：

- Markdown 导入。
- 单文档 Markdown 导出。

导入导出是治理能力，不进入公开 `/docs` 或正式 Web 作者页首批范围。导出需要独立权限，避免只有查看治理页的角色直接导出完整内容。

### 内置文档观察

Console 只观察 `SourceType=BuiltIn` 的内置文档：

- 展示 `SourcePath`、同步来源、当前版本和发布时间。
- 提示固定文档必须从仓库 `Docs/` 源文件修改。
- 禁用编辑、删除、恢复、发布、下架、归档、权限策略、导入覆盖和回滚操作。

后端现有 `EnsureDocumentIsEditable` 仍是最终保护，不依赖前端禁用按钮兜底。

## 权限键设计

首批新增权限键建议如下：

| 权限键 | 类型 | 用途 |
| --- | --- | --- |
| `console.docs.view` | Page | 访问文档治理页，读取治理列表、详情、版本列表和版本详情 |
| `console.docs.publish` | Button | 发布、下架、归档转回草稿 |
| `console.docs.archive` | Button | 归档文档 |
| `console.docs.delete` | Button | 移入回收站 |
| `console.docs.restore` | Button | 从回收站恢复 |
| `console.docs.permissions` | Button | 修改可见性、允许角色和允许权限 |
| `console.docs.rollback` | Button | 回滚到历史版本 |
| `console.docs.import` | Button | 导入 Markdown |
| `console.docs.export` | Button | 导出 Markdown |

暂不新增：

- `console.docs.create`
- `console.docs.edit`

原因：

- 创建和正文编辑已经由正式 Web 作者入口承接。
- Console 首批目标是治理，不是变成第二个文档创作器。
- 如果后续确需 Console 内编辑正文，应单独评审是否新增 `console.docs.edit`，并明确它不得隐含权限策略修改能力。

## 菜单与路由归属

Console 建议新增：

- 路由常量：`DOCUMENTS: '/documents'`
- 菜单标题：`文档治理`
- 路由元数据：
  - `key: 'documents'`
  - `path: '/documents'`
  - `title: '文档治理'`
  - `requiredPermission: CONSOLE_PERMISSIONS.docsView`
  - `sidebarVisible: true`
  - `searchVisible: true`

前端权限常量应与后端常量保持同名同义：

- `Frontend/radish.console/src/constants/permissions.ts`
- `Radish.Common/PermissionTool/ConsolePermissions.cs`

资源种子应同步补：

- `ConsoleResourceSeeds`
- `ConsoleResourceApiSeeds`
- `ApiModule` 对应接口种子
- Admin / System 默认权限全集
- [Console 权限覆盖矩阵](/guide/console-permission-coverage-matrix)

## API 授权资源

公开读取接口继续保持公开阅读语义：

| 当前接口 | 后续定位 |
| --- | --- |
| `Wiki/GetList` | 公开 / 登录态阅读列表，继续按发布状态、删除态、可见性和当前用户过滤 |
| `Wiki/GetTree` | 公开 / 登录态阅读目录，继续按当前用户可见性过滤 |
| `Wiki/GetById` | 公开 / 登录态阅读详情，继续按当前用户可见性过滤 |
| `Wiki/GetBySlug` | 公开详情，继续用于 `/docs/:slug` |

Console 治理建议使用治理专用接口或显式治理模式接口：

| 建议接口 | 权限键 | 说明 |
| --- | --- | --- |
| `Wiki/AdminGetList` | `console.docs.view` | 治理列表，支持未发布、已归档、已删除和来源筛选 |
| `Wiki/AdminGetTree` | `console.docs.view` | 治理目录树，不替代公开目录 |
| `Wiki/AdminGetById/{id}` | `console.docs.view` | 治理详情，支持查看已删除文档 |
| `Wiki/GetRevisionList/{id}` | `console.docs.view` | 版本列表 |
| `Wiki/GetRevisionDetail/{revisionId}` | `console.docs.view` | 版本详情 |
| `Wiki/Publish/{id}` | `console.docs.publish` | 发布 |
| `Wiki/Unpublish/{id}` | `console.docs.publish` | 下架或归档转回草稿 |
| `Wiki/Archive/{id}` | `console.docs.archive` | 归档 |
| `Wiki/Delete/{id}` | `console.docs.delete` | 软删除 |
| `Wiki/Restore/{id}` | `console.docs.restore` | 恢复 |
| `Wiki/UpdateAccessPolicy/{id}` | `console.docs.permissions` | 更新可见性、允许角色和允许权限 |
| `Wiki/Rollback/{revisionId}` | `console.docs.rollback` | 回滚版本 |
| `Wiki/ImportMarkdown` | `console.docs.import` | 导入 Markdown |
| `Wiki/ExportMarkdown/{id}` | `console.docs.export` | 导出 Markdown |

后续代码实现时，已纳入 Console 治理的接口应优先使用 `RequireConsolePermission`。`System / Admin` 仍通过默认权限全集放行，但普通授权角色也应能通过角色授权配置获得真实可用的后端能力。

## 数据状态流转

| 动作 | 前置状态 | 后置状态 | 备注 |
| --- | --- | --- | --- |
| 发布 | `Draft / Archived` 且未删除 | `Published` | `PublishedAt` 首次发布时写入 |
| 下架 | `Published` 且未删除 | `Draft` | 不清空 `PublishedAt` |
| 转回草稿 | `Archived` 且未删除 | `Draft` | UI 文案与下架区分 |
| 归档 | `Draft / Published` 且未删除 | `Archived` | 不进入回收站 |
| 移入回收站 | 任意未删除非内置文档 | `IsDeleted=true` | 软删除，状态保留 |
| 恢复 | `IsDeleted=true` 非内置文档 | `IsDeleted=false` | 状态保留，父级仍需校验 |
| 更新权限策略 | 任意未删除非内置文档 | 状态不变 | 仅改可见性与允许列表 |
| 回滚版本 | 任意未删除非内置文档 | 状态不变 | 生成新版本，不改权限策略 |

内置文档不进入上述写入流转，只能观察。

## 验证口径

设计文档落地阶段：

- `git diff --check`
- `npm run check:repo-hygiene:changed`

代码实现阶段建议：

- `npm run check:console-permissions`
- `npm run build --workspace=radish.console`
- 若修改 API 授权属性或新增治理接口：`dotnet build Radish.slnx -c Debug`
- 若新增后端行为或权限测试：`dotnet test Radish.Api.Tests`
- 若触及 `ConsoleResourceSeeds / ConsoleResourceApiSeeds / ApiModule`：运行对应 DbMigrate 只读自检或最小迁移验证
- 准备小阶段验收时，在用户确认前后端已启动后，通过 Gateway 覆盖 `/console/` PC `1920x1080` 与 mobile `390x844 @ DPR 3`

真实浏览器 smoke 不作为本设计文档落地的必要步骤。

## 当前不做

- 不把公开 `/docs` 改成治理入口。
- 不在公开文档详情暴露发布、回收站、版本回滚或权限配置。
- 不新增普通非管理员文档作者权限模型。
- 不做文档审核流、审批发布、协作编辑、评论批注。
- 不把 Console 做成第二个完整文档创作器。
- 不删除或重做 WebOS `WikiApp`。
- 不迁移 WebOS Dock、窗口系统、窗口参数、桌面背景或 `openApp` 语义。
- 不重写内置文档同步策略。
- 不做公开 docs 页面级 UI 重设计。
- 不启动 Flutter 文档作者态。

