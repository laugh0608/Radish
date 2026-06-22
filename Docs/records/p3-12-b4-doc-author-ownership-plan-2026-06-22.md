# P3-12-B4 文档作者态归属裁决

> 日期：2026-06-22（Asia/Shanghai）
>
> 状态：只读盘点与归属方案已完成；B4-1 正式 Web 文档作者入口首批代码已完成；B4-2 Console 文档治理首批代码已完成
>
> 结论：公开 `/docs` 继续只承载阅读、搜索、正文内链和分享；正式 Web 应新增登录态文档作者页，承接常规创建、编辑、草稿和版本回看；Console 承接发布、撤回、归档、恢复、受限可见性、角色 / 权限配置、内置文档同步观察等治理动作；WebOS `WikiApp` 在替代路径落地前继续作为 `/desktop` 历史维护入口，不再扩展新功能。

## 本轮核对

本轮只做代码与文档只读盘点，不启动服务、不做真实页面 smoke，也不改接口或权限模型。

已读取和核对：

- `Docs/planning/current.md`
- `Docs/planning/p3-12-web-completion-webos-retirement.md`
- `Docs/guide/validation-baseline.md`
- `Docs/frontend/shell-strategy.md`
- `Radish.Api/Controllers/WikiController.cs`
- `Radish.IService/IWikiDocumentService.cs`
- `Radish.Model/WikiDocument.cs`
- `Radish.Model/WikiDocumentRevision.cs`
- `Radish.Model/DtoModels/WikiDocumentDto.cs`
- `Radish.Model/ViewModels/WikiDocumentVo.cs`
- `Radish.Service/WikiDocumentService.cs`
- `Radish.Service/WikiDocumentService.BuiltIn.cs`
- `Frontend/radish.client/src/public/docs/*`
- `Frontend/radish.client/src/public/docsRouteState.ts`
- `Frontend/radish.client/src/apps/wiki/*`
- `Frontend/radish.console/src/router/*`
- `Frontend/radish.console/src/constants/permissions.ts`
- `Radish.Common/PermissionTool/ConsolePermissions.cs`
- `Radish.DbMigrate/InitialDataSeeder.ConsoleAuthorization.cs`

未执行：

- 未启动 API / Auth / Gateway / Vite。
- 未做 PC / mobile Gateway 页面复核。
- 未做页面级 UI 设计或 Pencil 设计稿。

## 现状判断

### 公开文档

`/docs`、`/docs/search`、`/docs/:slug` 已经是正式公开阅读入口：

- 公开页只调用 `GetTree`、`GetList`、`GetBySlug`。
- 有登录 token 时允许读取 `Authenticated` 或命中角色 / 权限的 `Restricted` 文档，但页面仍是阅读态。
- 验证基线已明确公开 docs 不开放编辑、发布、回收站、版本历史或桌面治理动作。

因此公开 docs 不应直接加入作者按钮，也不应承载发布、回滚、恢复等管理能力。

### WebOS `WikiApp`

WebOS `WikiApp` 当前覆盖完整 Wiki 管理能力：

- 创建、编辑、删除、恢复。
- 发布、撤回、归档。
- 导入 / 导出 Markdown。
- 版本历史、版本详情和回滚。
- 文档树、搜索、状态筛选、回收站筛选。
- 受限可见性、允许角色和允许权限配置。

但它是 WebOS 窗口应用，仍处在 `/desktop` 历史入口内；直接搬迁会把窗口工作台形态带入正式 Web。可复用的是 API helper、类型、Markdown 编辑器和部分表单语义，不是 WebOS 容器。

### 后端 Wiki 能力

当前后端接口分为两类：

| 能力 | 当前授权 | 说明 |
| --- | --- | --- |
| `GetList` / `GetTree` / `GetById` / `GetBySlug` | `AllowAnonymous` | 读取时按发布状态、删除状态、可见性、登录态、角色和权限过滤。 |
| `Create` / `Update` / `Delete` / `Restore` | `SystemOrAdmin` | 当前是管理员写入模型，不是普通作者模型。 |
| `Publish` / `Unpublish` / `Archive` | `SystemOrAdmin` | 属于高影响治理动作。 |
| `GetRevisionList` / `GetRevisionDetail` / `Rollback` | `SystemOrAdmin` | 版本回看和回滚当前只开放管理员。 |
| `ImportMarkdown` / `ExportMarkdown` | `SystemOrAdmin` | 当前按管理工具能力设计。 |

服务层已经具备：

- `Draft / Published / Archived` 生命周期。
- `Public / Authenticated / Restricted` 可见性。
- `AllowedRoles / AllowedPermissions` 受限访问控制。
- 内置文档只读保护，`BuiltIn` 来源必须回到 `Docs/` 源文件修改。
- 版本记录和回滚。

首批代码不应在不调整后端权限契约的情况下伪装成“普通作者可编辑”。如果要开放非管理员作者态，需要先单独定义角色、权限、归属、审核和发布边界。

### Console 现状

Console 当前没有文档管理页面，也没有 `console.docs.*` 权限键。

若把治理动作迁入 Console，需要同时补齐：

- `Frontend/radish.console/src/constants/permissions.ts`
- `Frontend/radish.console/src/router/routes.ts`
- `Frontend/radish.console/src/router/routeMeta.ts`
- `Radish.Common/PermissionTool/ConsolePermissions.cs`
- `Radish.DbMigrate/InitialDataSeeder.ConsoleAuthorization.cs`
- Console 权限覆盖矩阵文档
- 必要的 API 授权属性或资源映射

这说明 Console 不是简单加一个前端页面即可承接 Wiki 全量能力，必须按既有 Console 权限治理链路落地。

## 归属裁决

### 1. 公开 `/docs` 保持阅读态

范围：

- 文档目录。
- 文档搜索。
- 文档详情。
- 正文内链。
- 复制公开链接。

不纳入：

- 创建、编辑、发布、撤回、归档、恢复。
- 版本历史、回滚、导入、导出。
- 角色 / 权限配置。
- 内置文档同步治理。

### 2. 正式 Web 承接轻量作者页

推荐新增登录态路径：

| 路由 | 类型 | 说明 |
| --- | --- | --- |
| `/docs/mine` | 登录态作者入口 | 我的文档、草稿、已发布文档和最近编辑。 |
| `/docs/compose` | 登录态作者入口 | 新建普通手写文档。 |
| `/docs/edit/:id` | 登录态作者入口 | 编辑可编辑文档；内置文档只读提示。 |
| `/docs/revisions/:id` | 登录态作者反馈 | 查看版本列表和版本详情；首批不开放回滚。 |

首批作者页建议只面向 `SystemOrAdmin` 复用现有后端写入权限，不引入普通作者角色。这样可以先脱离 WebOS 工作台，同时不扩大后端授权面。

正式 Web 作者页首批只承接：

- 创建普通文档。
- 编辑非内置、未删除文档。
- 查看我的 / 管理员可见文档列表。
- 查看版本列表和版本详情。
- 从公开详情进入对应编辑页的真实 URL。
- 登录回流白名单和真实链接语义。

暂不承接：

- 发布、撤回、归档、恢复。
- 受限角色 / 权限配置。
- Markdown 导入 / 导出。
- 版本回滚。
- 普通非管理员作者权限模型。

### 3. Console 承接治理动作

推荐 Console 后续新增 `/docs` 或 `/documents` 管理页，作为文档治理入口。

Console 负责：

- 全量文档筛选、状态筛选、删除筛选。
- 发布、撤回、归档、恢复。
- 受限可见性、角色 / 权限配置。
- 导入、导出和版本回滚。
- 内置文档同步状态观察与只读提示。
- 与角色授权资源树、API 资源映射、权限覆盖矩阵保持一致。

`B4-2` 确认后的首批权限键：

- `console.docs.view`
- `console.docs.publish`
- `console.docs.archive`
- `console.docs.delete`
- `console.docs.restore`
- `console.docs.permissions`
- `console.docs.import`
- `console.docs.rollback`
- `console.docs.export`

首批不新增 `console.docs.create/edit`。创建和正文编辑继续由正式 Web 作者入口承接；Console 首批只做治理入口，避免把正文创作和权限 / 发布治理混成同一职责面。详细口径见 [P3-12-B4-2 Console 文档治理设计](/records/p3-12-b4-2-console-doc-governance-design-2026-06-22)。

### 4. WebOS 只作为过渡维护入口

`WikiApp` 保留：

- `/desktop` 历史入口。
- 已有管理员 Wiki 管理能力。
- 旧窗口参数和桌面工作台内部导航。

不再新增：

- 新作者态能力。
- 新公开 URL 契约。
- 新 Console 权限语义。
- 正式 Web 导航入口。

替代路径落地后，再按 P3-12-C 口径清理默认产品路径里的 WebOS 文档回跳和说明。

## B4-1 首批代码结果

2026-06-22 已按本方案完成正式 Web 文档作者入口首批代码：

- 新增 `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id`，并在主入口中先于公开内容壳层识别文档作者路径。
- `/docs/mine` 提供 Wiki 文档列表、创建入口、编辑入口、修订入口和公开阅读链接。
- `/docs/compose` / `/docs/edit/:id` 复用 Wiki API helper、Markdown 编辑器和 Wiki 附件上传；首批只开放标题、slug、摘要、父级、排序、封面附件、正文和变更摘要，保留既有可见性但不在作者页做治理配置。
- `/docs/revisions/:id` 支持版本列表和版本详情回看，不开放回滚。
- 公开 `/docs` 只在 `System/Admin` 登录态下提供“文档作者台”和“编辑文档”真实链接；访客和普通公开阅读体验不变。
- `authReturnPath` 已纳入受控文档作者路径，只接受无 query / hash 且 ID 合法的作者入口。

本批仍不纳入：

- 发布、撤回、归档、恢复、导入、导出、回滚。
- 普通非管理员作者权限模型。
- Console 文档治理权限种子和 API 授权资源。
- WebOS `WikiApp` 删除或重做。

本轮已完成静态验证：

- `node --test --test-isolation=none ./Frontend/radish.client/tests/publicRouteState.test.ts ./Frontend/radish.client/tests/authReturnPath.test.ts ./Frontend/radish.client/tests/entryRoute.test.ts ./Frontend/radish.client/tests/realUsagePathContracts.test.ts ./Frontend/radish.client/tests/publicSeoStatic.test.ts`
- `npm run type-check --workspace=radish.client`

后续 B4 小阶段验收需要在用户确认前后端已启动后，通过 Gateway 覆盖 PC `1920x1080` 与移动 `390x844` CSS 视口的公开 `/docs`、作者入口、创建 / 编辑表单、修订回看和登录回流。

## B4-2 设计承接

2026-06-22 已确认并完成 [P3-12-B4-2 Console 文档治理设计](/records/p3-12-b4-2-console-doc-governance-design-2026-06-22) 的首批代码实现。

确认后的 Console 职责边界：

- Console 新增文档治理入口，承接全量筛选、发布 / 下架、归档、回收站、权限策略、版本回滚、导入导出和内置文档观察。
- 正式 Web 作者入口继续负责普通手写文档创建、正文编辑、基础信息编辑和版本回看。
- 公开 `/docs` 继续只承载阅读、搜索、正文内链和分享。
- WebOS `WikiApp` 只作为 `/desktop` 历史维护入口保留，不新增正式 Web / Console 权限语义。

首批代码已补 `/console/documents` 对应内部路由 `/documents`、治理专用 API、权限键、资源种子、权限覆盖矩阵和 Console 页面；后续仍以该设计记录为准，不再把公开 `/docs`、Web 作者页和 Console 治理页混成同一职责面。

## 首批代码建议

### 推荐 B4-1：正式 Web 作者入口

原因：

- 当前正式 Web 缺口是“离开 WebOS 也能管理自己的文档草稿和编辑已有文档”。
- 公开 `/docs` 已稳定，不应混入写入按钮。
- Console 文档治理涉及权限种子和 API 授权资源，改动面更大，适合作为 B4-2 或独立治理批次。

范围：

1. 路由契约
   - 扩展 docs 路由识别，新增 `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id`。
   - `authReturnPath` 白名单只允许上述路径和受控 query，不允许外部 URL、hash、未知 query。
2. API helper
   - 复用 `Frontend/radish.client/src/apps/wiki/api/wiki.ts` 或抽出不依赖 WebOS 的 docs author API。
   - 继续统一使用 `@radish/http`。
3. 页面能力
   - 新建正式 Web 作者页容器，不复用 `WikiApp` 顶层窗口壳。
   - 支持列表、创建、编辑和版本回看。
   - 内置文档显示只读说明，不允许编辑。
4. 导航
   - `/docs` 公开阅读页可在登录态管理员场景提供“管理文档”真实链接。
   - 不把普通公开详情 canonical 改成带作者 intent 的 URL。
5. 验证
   - 路由解析、登录回流、公开 docs 只读边界、真实 `href` 语义和 `radish.client` type-check / build。

### B4-2：Console 文档治理入口

首批已完成范围：

- 新增 Console docs 页面和权限键。
- 补 Console 资源种子和 API 映射。
- 为 WikiController 写入 / 治理接口补 `RequireConsolePermission`。
- 更新 Console 权限覆盖矩阵。
- 首批不新增 Console 正文创建 / 编辑权限；若后续确需 Console 内编辑正文，再单独评审 `console.docs.edit`。

该批次已在 B4-1 后独立完成，避免同时迁移正式 Web 作者入口和 Console 治理中心导致验证面过宽。

## 首批不纳入

- 普通非管理员作者权限模型。
- 文档审核流、协作编辑、评论批注和审批发布。
- 完整知识库产品重做。
- 公开 docs 页面级 UI 重设计。
- 文档内容迁移、Docs 源文件重组或内置文档同步策略重写。
- Flutter 文档作者态。
- 删除 WebOS Wiki。

## 验证口径

B4-1 建议验证：

- `Frontend/radish.client` docs route / auth return path 定向测试。
- `Frontend/radish.client` 公开 docs 只读边界静态测试。
- `npm run type-check --workspace=radish.client`
- `npm run build --workspace=radish.client`
- `git diff --check`
- 小阶段验收时，在用户确认前后端已启动后，通过 Gateway 覆盖 PC `1920x1080` 与移动 `390x844` CSS 视口的 `/docs`、`/docs/:slug`、`/docs/mine`、`/docs/compose`、`/docs/edit/:id` 登录回流。

B4-2 已完成验证：

- `npm run check:console-permissions`
- `npm run build --workspace=radish.console`
- `dotnet build Radish.slnx -c Debug`
- `dotnet test Radish.Api.Tests --filter WikiDocument`
- `dotnet test Radish.Api.Tests`
- Console 权限覆盖矩阵已更新。

B4-2 待补验收：

- 在用户确认前后端已启动后，通过 Gateway 覆盖 `/console/documents` PC / mobile 复核。

## 风险点

- 如果在公开 `/docs/:slug` 直接混入编辑和发布按钮，会破坏公开阅读只读边界。
- 如果不补 Console 资源种子和 API 映射就新增治理页，会制造前端可见但后端权限不可控的裂缝。
- 如果直接搬 `WikiApp`，会把 WebOS 窗口状态和历史工作台形态带进正式 Web。
- 如果开放普通作者但继续复用 `SystemOrAdmin` 接口，会让产品语义和后端授权不一致。
- 如果把 B4-1 和 B4-2 同批落地，验证面会同时覆盖公开 docs、登录态作者页、Console 权限、API 授权和 DbMigrate 种子，收益与风险不匹配。
