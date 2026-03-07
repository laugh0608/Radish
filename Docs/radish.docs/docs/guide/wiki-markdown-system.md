# Wiki / Markdown 文档体系设计与实施方案

> 本文定义 Radish 在 M12 阶段的 Wiki / Markdown 文档能力建设方案，用于指导 **Markdown 导入/导出**、**Wiki/文档 App** 与 **`radish.docs` 迁移收口** 的整体设计与分阶段落地。
>
> **状态**：方案已确认，待进入实现
>
> **最后更新**：2026-03-07
>
> **关联文档**：
> - [开发计划](/development-plan)
> - [前端设计文档](/frontend/design)
> - [开发框架说明](/architecture/framework)
> - [开发规范](/architecture/specifications)
> - [文件上传功能设计](/features/file-upload-design)
>
> **一句话目标**：先落一个可用的 Wiki MVP，打通 Markdown 文档创建、导入、展示、导出闭环，再逐步承接 `radish.docs` 中适合迁移的运行时文档内容。

---

## 1. 背景与目标

### 1.1 背景

当前 Radish 已具备以下基础条件：

- WebOS 桌面已支持应用注册与窗口化打开；
- `radish.client` 中已有“文档中心”入口，但当前仍以 `iframe` 方式嵌入 `radish.docs`；
- `@radish/ui` 已提供可复用的 `MarkdownEditor` 与 `MarkdownRenderer`；
- 后端已有成熟的附件上传体系，可复用图片/文档上传能力；
- 路线图已将 **M12 当前功能主线** 切换为 **内容与文档体系重构**。

但目前仍存在几个明显问题：

1. **运行时文档体验与开发文档体系混在一起**：用户侧“文档中心”本质上还是开发文档站嵌入页；
2. **缺少业务态内容模型**：没有“文档”这一等价于帖子/频道/商品的正式业务对象；
3. **Markdown 只在论坛等局部场景使用**：尚未形成可管理、可导入、可导出的统一文档链路；
4. **`radish.docs` 无法直接承担运行时知识库角色**：它适合做“开发规范与架构真相源”，不适合直接承担“用户侧帮助中心 / Wiki”全部职责。

### 1.2 目标

本期目标不是“一步到位重做整个文档平台”，而是按最小闭环分阶段完成：

- 建立 **Wiki 文档业务模型**；
- 提供 **Markdown 单文档导入 / 导出**；
- 在 WebOS 内落地 **Wiki/文档 App**；
- 用统一的 Markdown 渲染与编辑能力承接文档展示与编辑；
- 为后续 `radish.docs` 内容迁移建立边界、节奏与技术路径。

### 1.3 非目标

本方案当前 **不包含** 以下内容：

- 多人协同编辑；
- 全量目录 ZIP 批量导入导出；
- 全文检索引擎；
- 文档评论、审核流、点赞等社区增强能力；
- 一次性下线 `radish.docs`；
- 将开发规范类文档全部迁入运行时 Wiki。

---

## 2. 产品定位与边界

### 2.1 三类文档边界

为避免“开发文档”和“运行时文档”再次混杂，后续文档体系分为三层：

1. **开发/架构真相源**：继续保留在 `Docs/radish.docs/docs/`
   - 面向开发者与协作者
   - 包括架构、规范、路线图、实施文档、部署说明、周报等
   - 仍然是仓库内唯一真相源

2. **运行时帮助/产品说明**：逐步迁入 Wiki App
   - 面向最终用户
   - 包括使用帮助、功能说明、FAQ、指南、活动说明等
   - 需要在 WebOS 内被浏览、维护和导出

3. **可沉淀的社区知识内容**：后续视阶段决定是否放入 Wiki App
   - 如攻略、经验总结、教程集
   - 本期只预留模型，不做复杂治理

### 2.2 当前阶段的产品结论

- `radish.docs` **继续保留**，用于开发与架构文档；
- 新增 **Wiki/文档 App**，承接运行时文档；
- 当前“文档中心”不立即替换掉 `radish.docs`，而是先并存：
  - `docs`：现有 iframe 文档站入口；
  - `wiki`：新的内置窗口应用；
- 待 Wiki MVP 稳定后，再决定是否将“文档中心”的默认入口切换到 Wiki。

---

## 3. 设计原则

### 3.1 先闭环，再迁移

优先完成“创建 → 导入 → 展示 → 导出”的单文档闭环，再考虑批量迁移与增强能力。

### 3.2 复用现有基础设施

- 编辑器复用 `@radish/ui` 的 `MarkdownEditor`
- 渲染器复用 `@radish/ui` 的 `MarkdownRenderer`
- 附件与图片上传复用现有 `Attachment` / `AttachmentController`
- WebOS 集成复用现有 `AppRegistry` 与窗口系统

### 3.3 业务文档正式建模

Wiki 文档必须成为后端正式业务对象，不能继续以“文件 + 约定目录”临时堆积。

### 3.4 开发文档与运行时文档分治

`radish.docs` 与 Wiki App 之间是“边界清晰、逐步迁移”的关系，不做粗暴合并。

### 3.5 先管理后开放

MVP 阶段优先让 `Admin/System` 能稳定管理文档；普通用户贡献、协作与治理后置。

---

## 4. 总体架构

### 4.1 当前架构

```text
radish.client
   └─ 文档中心（iframe）
        └─ /docs/ → radish.docs (VitePress)

论坛 / 个人中心 / 其他场景
   └─ 局部使用 MarkdownEditor / MarkdownRenderer

后端
   └─ 附件系统（图片 / 文档上传）
```

### 4.2 目标架构

```text
WebOS Desktop
├─ docs 应用（保留）
│   └─ iframe → radish.docs
└─ wiki 应用（新增）
    ├─ 文档列表 / 树
    ├─ 文档详情渲染
    ├─ 文档编辑器
    ├─ Markdown 导入
    └─ Markdown 导出

Radish.Api
└─ Wiki 模块
   ├─ WikiDocument Controller
   ├─ WikiDocument Service
   ├─ WikiDocument Repository
   ├─ WikiDocument / WikiDocumentRevision 实体
   └─ 附件系统复用（BusinessType = Wiki）
```

### 4.3 分层落点

遵循现有仓库分层规范：

- `Radish.Model`
  - `Models/WikiDocument.cs`
  - `Models/WikiDocumentRevision.cs`
  - `ViewModels/WikiDocumentVo.cs`
  - `ViewModels/WikiDocumentDetailVo.cs`
  - `DtoModels/WikiDocumentCreateDto.cs`
  - `DtoModels/WikiDocumentUpdateDto.cs`
  - `DtoModels/WikiMarkdownImportDto.cs`

- `Radish.IRepository` / `Radish.Repository`
  - 如仅简单 CRUD，可复用 `IBaseRepository<TEntity>`
  - 仅复杂树形查询、版本查询时再扩展专属仓储

- `Radish.IService` / `Radish.Service`
  - 建议新增 `IWikiDocumentService` / `WikiDocumentService`
  - 导入、导出、发布、版本生成等逻辑统一放 Service

- `Radish.Api`
  - 新增 `WikiController`
  - Controller 仅协调请求与响应，不直接写业务逻辑

- `Frontend/radish.client`
  - 新增 `src/apps/wiki/`
  - 作为 WebOS 内置应用接入

---

## 5. 后端模型设计

### 5.1 WikiDocument

作为当前主文档实体，承载”当前发布内容”与基本元信息。

```csharp
public class WikiDocument : RootEntityTKey<long>, IDeleteFilter
{
    public string Title { get; set; }           // 文档标题
    public string Slug { get; set; }            // URL 友好标识（唯一）
    public string Content { get; set; }         // Markdown 正文
    public string? Summary { get; set; }        // 摘要
    public long? CategoryId { get; set; }       // 分类 ID（可选）
    public int Status { get; set; }             // 0=Draft, 1=Published, 2=Archived
    public int ViewCount { get; set; }          // 浏览次数
    public DateTime? PublishedAt { get; set; }  // 发布时间
    public long CreatedBy { get; set; }         // 创建者
    public long? LastModifiedBy { get; set; }   // 最后修改者
    public long? ParentId { get; set; }         // 父节点（树结构）
    public int Sort { get; set; }               // 同级排序
    public string? SourceType { get; set; }     // Manual / Imported / DocsMigrated
    public string? SourcePath { get; set; }     // 原始路径
    public int Version { get; set; }            // 当前版本号

    // 软删除字段（继承自 IDeleteFilter）
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}
```

#### 索引策略

```sql
-- 列表查询优化
CREATE INDEX IX_WikiDocument_CategoryId_IsDeleted_Status
ON WikiDocument(CategoryId, IsDeleted, Status);

-- URL 友好访问
CREATE UNIQUE INDEX IX_WikiDocument_Slug
ON WikiDocument(Slug) WHERE IsDeleted = 0;

-- 作者文档查询
CREATE INDEX IX_WikiDocument_CreatedBy
ON WikiDocument(CreatedBy, IsDeleted);

-- 树形结构查询
CREATE INDEX IX_WikiDocument_ParentId_Sort
ON WikiDocument(ParentId, Sort, IsDeleted);
```

#### Slug 生成规则

**设计原则**：
- **简短易读**：避免过长 URL，最多 50 字符
- **纯 ASCII**：避免编码问题和乱码
- **语义化**：保持可读性，但不冗余
- **自动去重**：保证唯一性

**生成算法**：

```csharp
public static string GenerateSlug(string title)
{
    // 1. 转小写
    var slug = title.ToLowerInvariant();

    // 2. 中文转拼音或翻译（限制长度）
    //    “用户指南” → “user-guide” (翻译优先)
    //    “快速开始” → “quick-start”
    slug = TranslateOrPinyin(slug);

    // 3. 移除特殊字符，只保留字母、数字、连字符
    slug = Regex.Replace(slug, @”[^a-z0-9\s-]”, “”);

    // 4. 多个空格/连字符合并为单个连字符
    slug = Regex.Replace(slug, @”[\s-]+”, “-”);

    // 5. 去除首尾连字符
    slug = slug.Trim('-');

    // 6. 长度限制（最多 50 字符）
    if (slug.Length > 50)
    {
        slug = slug.Substring(0, 50).TrimEnd('-');
    }

    // 7. 如果为空，使用随机标识
    if (string.IsNullOrEmpty(slug))
    {
        slug = $”doc-{Guid.NewGuid().ToString(“N”).Substring(0, 8)}”;
    }

    return slug;
}
```

**冲突处理**：
- 检测到重复时自动追加序号：`user-guide` → `user-guide-2` → `user-guide-3`
- 支持手动指定 Slug（需通过唯一性校验）
- 保留关键词禁用：`admin`, `api`, `system`, `new`, `edit`, `delete` 等

**示例**：
```
“用户指南” → “user-guide”
“API 接口文档” → “api-interface-doc”
“快速开始 Quick Start” → “quick-start”
“2024年度总结” → “2024-summary”
“C# 编程规范” → “csharp-coding-standard”
“如何使用？” → “how-to-use”
“超长标题超长标题超长标题超长标题超长标题超长标题超长标题” → “long-title-long-title-long-title-long-title-lo”
“纯中文没有翻译” → “doc-a3f8b2c1” (随机)
```

### 5.2 WikiDocumentRevision

作为版本快照实体，承载编辑历史与导入来源，便于后续做版本回滚与比对。

```csharp
public class WikiDocumentRevision : RootEntityTKey<long>
{
    public long DocumentId { get; set; }        // 文档 ID
    public int Version { get; set; }            // 版本号
    public string Content { get; set; }         // 当次版本正文
    public string? ChangeSummary { get; set; }  // 修改说明
    public string? SourceType { get; set; }     // 创建来源
    public long CreatedBy { get; set; }         // 创建者
}
```

#### 索引策略

```sql
-- 版本历史查询
CREATE INDEX IX_WikiDocumentRevision_DocumentId_Version
ON WikiDocumentRevision(DocumentId, Version DESC);
```

### 5.3 为什么本期不先引入更多模型

以下能力当前先不单独建模：

- `WikiSpace`
- `WikiTag`
- `WikiPermission`
- `WikiComment`

原因是当前目标是 **最小闭环**，过早扩展模型会显著放大开发范围。

---

## 6. Markdown 导入/导出设计

### 6.1 导入设计（MVP）

本期仅支持 **单个 `.md` 文件导入**。

导入规则：

1. 文件名默认转为 `Slug`（经过 Slug 生成算法处理）
2. 文档首个一级标题（`# 标题`）优先作为 `Title`
3. 若无一级标题，则使用文件名作为 `Title`
4. 文件正文原样保存为 `Content`
5. `SourceType = Imported`
6. `SourcePath` 记录原文件名
7. 导入即生成 `WikiDocument` 与首个 `WikiDocumentRevision`
8. **文件大小限制**：单文档最大 5MB

#### 图片处理策略（MVP 阶段）

1. **本地图片**（如 `![](./images/pic.png)`）
   - 提示用户："检测到 N 张本地图片，需手动上传后替换"
   - 不自动处理，避免复杂度

2. **外链图片**（如 `![](https://example.com/pic.png)`）
   - 保持原样
   - 风险提示："外链图片可能失效"

3. **Base64 图片**（如 `![](data:image/png;base64,...)`）
   - 自动提取并上传到附件系统
   - 替换为新的附件 URL
   - 记录到 `Attachment` 表（`BusinessType = Wiki`）

#### 后续增强方向

- 提供"图片打包上传"辅助工具
- 支持拖拽批量上传并自动替换引用
- ZIP 批量导入（包含图片资源）

导入阶段暂不处理：

- 本地相对路径批量重写
- 目录级导入
- frontmatter 全量映射

### 6.2 导出设计（MVP）

本期仅支持 **单文档导出为 `.md` 文件**。

导出规则：

1. 文件名默认使用 `{slug}.md`
2. 导出内容优先保留原始 `Content`
3. 不附带图片资源包
4. 图片引用保持为附件系统 URL（外部可访问）

#### 后续增强方向

- ZIP 打包导出（包含图片资源）
- 导出为 PDF（使用 Puppeteer 或类似工具）
- 批量导出（多文档打包）

### 6.3 附件策略

- 文档内图片与文档附件继续走现有附件系统
- 业务类型建议新增 `BusinessType = Wiki`
- 引用关系仍由 Markdown 内容中的 URL 表达，不在 MVP 阶段做复杂块级引用建模

---

## 7. API 设计

建议新增 `WikiController`，优先使用 REST 风格，但保持当前项目命名习惯。

### 7.1 文档查询

- `GET /api/v1/Wiki/GetList`
  - 分页、关键词、状态、父节点过滤
- `GET /api/v1/Wiki/GetTree`
  - 返回树结构，用于左侧导航
- `GET /api/v1/Wiki/GetById/{id}`
  - 获取文档详情
- `GET /api/v1/Wiki/GetBySlug/{slug}`
  - 便于前端按稳定路径访问

### 7.2 文档写入

- `POST /api/v1/Wiki/Create`
- `PUT /api/v1/Wiki/Update`
- `POST /api/v1/Wiki/Publish/{id}`
- `POST /api/v1/Wiki/Unpublish/{id}`
- `POST /api/v1/Wiki/Archive/{id}`

### 7.3 导入导出

- `POST /api/v1/Wiki/ImportMarkdown`
- `GET /api/v1/Wiki/ExportMarkdown/{id}`

### 7.4 版本历史（MVP 已实现）

- `GET /api/v1/Wiki/GetRevisionList/{id}`
- `GET /api/v1/Wiki/GetRevisionDetail/{revisionId}`
- `POST /api/v1/Wiki/Rollback/{revisionId}`

当前阶段约定：

- 版本历史、版本详情、回滚接口仅对 `Admin/System` 开放；
- 回滚采用“恢复为新版本”模式，不直接覆写历史版本；
- 回滚仅恢复 `Title` 与 `MarkdownContent`，不恢复 `ParentId / Sort / Status / CoverAttachmentId` 等结构字段；
- 回滚成功后会为当前文档生成一个新的 `WikiDocumentRevision` 快照，修改说明固定为“回滚到 vX”。

---

## 8. 前端 Wiki / 文档 App 设计

### 8.1 应用形态

建议新增 WebOS 内置应用：

- `id: 'wiki'`
- `type: 'window'`
- 默认尺寸：`1280 x 860`
- 分类：`development` 或 `content`
- 先对所有登录用户开放浏览，对 `Admin/System` 开放编辑与导入导出

### 8.2 目录结构建议

```text
Frontend/radish.client/src/apps/wiki/
├── WikiApp.tsx
├── pages/
│   ├── WikiListPage.tsx
│   ├── WikiDetailPage.tsx
│   └── WikiEditorPage.tsx
├── components/
│   ├── WikiSidebar.tsx
│   ├── WikiToolbar.tsx
│   ├── WikiDocumentHeader.tsx
│   ├── WikiImportDialog.tsx
│   └── WikiExportButton.tsx
├── api/
│   └── wiki.ts
├── hooks/
│   └── useWikiTree.ts
└── types/
    └── wiki.ts
```

### 8.3 页面结构

#### 列表页

- 左侧：目录树 / 最近文档 / 筛选
- 右侧：文档列表
- 支持草稿 / 已发布 / 已归档的基础筛选

#### 详情页

- 使用 `MarkdownRenderer` 渲染正文
- 顶部显示标题、摘要、更新时间、导出按钮
- 编辑权限用户可直接进入编辑态

#### 编辑页

- 使用 `MarkdownEditor`
- 支持图片上传、文档上传
- 支持预览切换
- 支持导入 `.md` 文件快速创建文档

#### 版本对比展示

- 使用 `diff` 算法高亮变更（如 `diff-match-patch` 库）
- 支持”并排对比”和”行内对比”两种模式
- 提供”恢复到此版本”操作（创建新版本，内容为历史版本）

#### 当前已落地的最小前端能力

- Wiki App 在文档详情页右侧提供“版本历史”面板；
- 点击历史项可查看该版本的标题、修改说明、来源、创建时间与 Markdown 渲染结果；
- 管理员可对非当前版本执行“回滚到此版本”；
- 版本对比高亮仍为后续增强项，本期先保证“列表 / 详情 / 回滚”闭环。

### 8.4 与现有 `docs` 入口的关系

当前不删除 `docs` iframe 入口。

建议阶段性策略：

1. 新增 `wiki` 应用
2. 保留 `docs` 作为开发文档入口
3. 待 Wiki 内容成熟后，再视产品入口命名决定是否：
   - 将”文档中心”重定向到 Wiki；
   - 将 `docs` 更名为”开发文档”；
   - 将 `wiki` 更名为”帮助中心 / 知识库”。

---

## 9. 权限与租户策略

### 9.1 权限建议

MVP 阶段采用最稳妥的分层：

- 浏览已发布文档：登录用户可见
- 创建 / 编辑 / 发布 / 导入 / 导出：`Admin` / `System`
- 后续再评估普通用户“贡献文档”的能力

### 9.2 租户口径

当前项目运行口径仍以 **公共租户 `TenantId = 0`** 为主。

因此本期 Wiki 模块也遵循：

- 默认写入 `TenantId = 0`
- 查询过滤遵循当前公共租户基线
- 后续若启用实际多租户，再扩展为租户级文档空间

#### 后续权限扩展方向

- **空间级权限**：不同空间可设置不同管理员
- **文档级权限**：支持"公开/内部/私密"三档
- **协作者机制**：指定用户可编辑特定文档
- **审核流**：普通用户提交草稿，管理员审核发布

---

## 10. `radish.docs` 迁移策略

### 10.1 保留在 `radish.docs` 的内容

以下内容继续保留在仓库文档站：

- 开发规范
- 架构设计
- 里程碑与开发计划
- 部署与运维说明
- 协作者约束与技术决策
- 周报 / 月报 / Changelog

### 10.2 适合迁移到 Wiki 的内容

优先迁移这类运行时文档：

- 用户使用帮助
- 产品功能说明
- FAQ
- 活动说明
- 社区规则（视治理要求决定）
- 面向普通用户的图文教程

#### 第一批迁移清单（试点）

- `docs/guide/getting-started.md` → "快速开始"
- `docs/guide/configuration.md` → "配置说明"
- `docs/features/forum.md` → "论坛使用指南"

#### 保留在 radish.docs 的清单

- `docs/architecture/*` - 架构设计
- `docs/development-plan.md` - 开发计划
- `docs/changelog/*` - 变更日志
- `CLAUDE.md` - AI 协作指南
- `docs/guide/logging.md` - 日志系统（开发者向）
- `docs/deployment/*` - 部署指南（运维向）

### 10.3 迁移阶段建议

#### 阶段 A：先建系统，不迁旧文档

目标：让 Wiki 能创建、导入、展示、导出。

#### 阶段 B：试点迁移少量帮助文档

目标：选取 5~10 篇适合用户阅读的文档迁入，验证产品形态。

#### 阶段 C：梳理长期边界

目标：明确哪些文档长期保留在 `radish.docs`，哪些进入 Wiki。

---

## 11. 分阶段实施计划

### Phase 0：方案与文档（当前）

- [x] 明确产品边界与目标
- [x] 确定 Wiki MVP 范围
- [x] 确定后端模型与前端结构
- [x] 同步路线图与周报口径

### Phase 1：后端 MVP

- [ ] 建立 `WikiDocument` / `WikiDocumentRevision`
- [ ] 建立 `Vo` / `Dto` / `Service` / `Controller`
- [ ] 完成列表、详情、创建、更新、发布接口
- [ ] 完成 Markdown 单文件导入
- [ ] 完成单文档导出
- [ ] 补 `.http` 联调脚本

### Phase 2：前端 Wiki App MVP

- [ ] 新建 `apps/wiki` 应用目录
- [ ] 在 `AppRegistry` 注册 `wiki` 应用
- [ ] 完成列表页、详情页、编辑页
- [ ] 接入 `MarkdownRenderer` 与 `MarkdownEditor`
- [ ] 接入导入 / 导出按钮与权限控制

### Phase 3：迁移与增强

- [ ] 试点迁移一批帮助文档
- [ ] 明确 `docs` 与 `wiki` 的入口命名
- [ ] 补文档版本历史页面
- [ ] 评估全文检索、批量导入等后续能力

---

## 12. 验收标准

本期 MVP 完成需满足：

1. 能在 WebOS 中打开 Wiki App；
2. 能创建一篇 Markdown 文档并保存；
3. 能展示 Markdown 渲染结果；
4. 能导入单个 `.md` 文件生成文档；
5. 能导出单篇文档为 `.md`；
6. 不影响现有 `docs` iframe 入口；
7. 符合当前项目分层与 `Vo` / `Dto` / `Service` / `Controller` 规范。

---

## 13. 风险与应对

### 13.1 范围失控

风险：一开始就引入空间、标签、检索、协同编辑，导致交付变慢。

应对：严格按 MVP 执行，只做单文档闭环。

### 13.2 文档边界重新混乱

风险：把开发规范和运行时帮助文档再次混在一起。

应对：明确 `radish.docs` 与 Wiki 的职责分治，迁移前先分类。

### 13.3 导入后的附件路径失效

风险：Markdown 中相对路径或本地资源在导入后失效。

应对：MVP 阶段只承诺单文档 Markdown 文本导入，不承诺自动搬运全部本地资源。

### 13.4 权限过早开放

风险：普通用户也能编辑文档，带来治理压力。

应对：本期仅开放给 `Admin/System` 管理，普通用户先只读。

---

## 14. 推荐开发顺序

1. 先做后端模型与 API MVP；
2. 再做前端 Wiki App MVP；
3. 然后打通导入 / 导出；
4. 最后评估并启动 `radish.docs` 的内容迁移试点。

> **当前建议的下一步**：基于本方案先实现后端 Wiki 模块与最小 `.http` 联调资产，再接前端应用骨架。

---

## 15. 性能优化建议

### 15.1 缓存策略

- **已发布文档渲染结果缓存**：Redis 缓存，TTL 1小时
- **目录树缓存**：变更时失效
- **热门文档预加载**：根据 `ViewCount` 预加载高频文档

### 15.2 分页与懒加载

- 文档列表默认 20 条/页
- 版本历史默认显示最近 10 条，支持"加载更多"
- 大文档（>100KB）考虑分段加载或虚拟滚动

### 15.3 数据库优化

- 合理使用索引（见 5.1 节）
- 定期清理过期版本（保留最近 N 个版本）
- 考虑将 `Content` 字段存储到独立表（大字段分离）

---

## 16. 审计与监控

### 16.1 操作审计

- 记录文档创建/编辑/删除/发布操作
- 记录导入/导出操作及文件大小
- 记录权限变更操作
- 复用现有 `AuditLog` 系统

### 16.2 监控指标

- 文档总数、活跃文档数
- 导入/导出成功率
- 平均文档大小、最大文档大小
- 版本数分布（识别过度编辑）
- API 响应时间（P50/P95/P99）

### 16.3 告警规则

- 单文档超过 5MB 告警
- 单文档版本数超过 100 告警
- 导入失败率超过 10% 告警
