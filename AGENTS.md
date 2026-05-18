# AGENTS 指南

本文件为 AI 协作者提供 Radish 项目的独立协作说明。

## 语言规范

- 默认使用中文进行说明、讨论和文档编写。
- 代码、技术标识、配置键、命令、路径和引用名保留原文。

## 协作流程

- 涉及架构、规则、接口、依赖、运行时行为或范围不清的改动前，必须先说明方案并等待批准。
- 小规模、低风险、需求明确的文档、配置或清理类变更，可直接实施，无需先单独描述方案并等待批准。
- 如果需求不明确，必须先提出问题并澄清后再动手。
- 每次新增/修改功能、修复 bug 或处理其他任务时，优先从根因、长期维护性和系统一致性出发，选择更完整、更稳妥的治理方案；不要把“最小修复”当作默认优先级，也不要无节制地层层增加兜底来掩盖问题。
- 修改项目规则、架构、接口或文档口径时，优先保证与 `Docs/` 中的现状一致。
- 涉及当前阶段、优先级或范围判断时，优先查看 `Docs/planning/current.md`。
- 涉及本地验证、CI 对齐或回归入口时，优先查看 `Docs/guide/validation-baseline.md`。
- 验证与留痕默认按“开发中 / 准备合并到 `master` / 发布部署”三种粒度区分：开发中的本地连续提交只做必要验证，不要求每次都补完整回归记录；批次级回归记录默认在 `PR -> master` 前补，发布 / 部署节点再补正式留痕。
- 产品正式上线进入稳定运营前，开发节奏默认采用“主动批量验收 + 成组修复 + 一次性交付结论”：AI 协作者应主动按链路矩阵复核、补测试和收口同类问题，不把每个小修复都交给开发者手动复测后再继续推进。
- 产品正式上线并进入稳定运营后，再切换为更小心谨慎的变更策略：优先小步修复、风险隔离、回归验证和发布留痕，避免在生产稳定期批量改动高风险链路。
- `Docs/` 的关键入口文档必须尽可能简约，只描述最近阶段、当前进度、执行入口和必要约束；历史批次、命令级验证流水、实现细节和长背景应写入日志、归档或专题文档，避免新会话读取无关背景浪费上下文。
- 代码应接近对应语言和框架的良好实践，禁止新增不明意义的方法、空泛工具类、晦涩命名或为了“架构感”而增加的抽象封装；抽象必须服务于真实复用、边界隔离、复杂度下降或契约稳定。

## 规划推进读取顺序

- 当用户提出“根据项目规划和开发进度，今天要做什么以推进开发”这类问题时，默认只先读取 `Docs/planning/current.md`。
- 若 `current.md` 不足以判断阶段边界或下一顺位，再读取 `Docs/development-plan.md`。
- 只有涉及阶段边界、多端路线或壳层归属争议时，才继续读取 `Docs/planning/phase-two-community-multiplatform.md`、`Docs/frontend/shell-strategy.md` 或对应专题文档。
- 不要默认展开 `Docs/changelog/`、`Docs/planning/archive.md`、RC 验收记录或命令级验证记录；这些只在需要追溯历史事实或验证证据时读取。

## Agent 协同文件

- 仓库中面向不同 Agent 入口名的协作文件，应保持“基本复制”和长期同步。
- 这些同类协作文件不应演化出彼此冲突的规则口径。
- 若某个同类协作文件更新了通用协作规则、执行边界、验证基线或阶段约束，其余同类文件也应尽快同步。
- 同类协作文件只允许保留极少量与入口名称直接相关的表述差异，不应借此分叉实际协作规范。

## 快速认知

- 技术栈：ASP.NET Core 10 + SQLSugar ORM + PostgreSQL（本地默认 SQLite） / React 19 + Vite（Rolldown） + TypeScript
- 前端结构：npm workspaces 管理 `radish.http`、`radish.client`、`radish.console`、`radish.ui`
- 桌面形态：`radish.client` 为 WebOS 桌面化 UI；`Clients/radish-tauri` 为 Tauri 桌面安装包壳层
- 共享组件：`radish.ui` 为源码直连的共享 UI 组件库，无需单独构建
- HTTP 客户端：`radish.http` 为统一 API 客户端 workspace
- 协作分支：`dev`
- `master` 仅作为稳定主线，只通过 Pull Request 合并
- `master` 允许 `merge commit` 与 `rebase merge`，禁用 `squash merge`
- 文档源：`Docs/` 为项目文档唯一真相源

## AI 执行边界

### 必须先告知用户、不得直接执行

- 包安装：`dotnet add package`、`npm install`
- 项目启动：`dotnet run`、`npm run dev`

### 正确做法

- 需要安装依赖时，明确告诉用户应执行的命令
- 需要启动服务时，明确告诉用户应执行的命令和端口

### 可直接执行

- 代码读写
- 构建：`dotnet build`、`npm run build`
- 测试：`dotnet test`、`npm run test`
- 类型检查、Lint、静态分析
- Git 操作

### 沙盒验证与提权规则

- 默认优先在当前沙盒环境内执行构建、测试与最小验证。
- 如果因沙盒限制、PATH 缺失、权限隔离、网络或证书限制，导致无法确认代码是否真实可编译、可测试、可验证，可直接申请提权。
- 提权用途必须限制在“构建 / 测试 / 必要验证”，不得扩大到安装依赖、长期运行服务或其他高风险动作。
- 即使允许提权，仍然禁止直接执行 `dotnet add package`、`npm install`、`dotnet run`、`npm run dev`。

## 仓库结构

### 后端项目

- 宿主：`Radish.Api`、`Radish.Gateway`、`Radish.Auth`
- 初始化与只读自检：`Radish.DbMigrate`
- 业务分层：`Service` / `Repository` / `Core` / `Model`
- 基础设施：`Common`、`Extension`、`IService`、`IRepository`、`Shared`
- 测试：`Radish.Api.Tests`
- Rust 扩展：`Lib/radish.lib`

### 前端项目

- `Frontend/radish.http`：统一 HTTP 客户端与相关类型封装
- `Frontend/radish.client`：WebOS 桌面，面向用户
- `Frontend/radish.console`：管理后台
- `Frontend/radish.ui`：共享组件库，供 client 和 console 直接引用源码

### 客户端项目

- `Clients/radish.flutter`：Flutter Android / iOS 移动原生安装包路线
- `Clients/radish-tauri`：Tauri 桌面安装包壳层，复用 `Frontend/radish.client` 的 WebOS 构建产物

## 环境与命令

### 基础环境

- .NET SDK 10
- Node.js 24+
- PostgreSQL 16+（本地可用 SQLite）

### AI 可执行命令

```bash
# 后端
dotnet build Radish.slnx -c Debug
dotnet test Radish.Api.Tests

# 前端
npm run build --workspace=radish.client
npm run build --workspace=radish.console
npm run type-check --workspace=@radish/ui
npm run type-check --workspace=@radish/http

# 统一验证入口
npm run validate:baseline:quick
npm run validate:baseline
npm run validate:baseline:host
```

### 供用户手动执行的启动命令

```bash
pwsh ./start.ps1
./start.sh

dotnet run --project Radish.Api
dotnet run --project Radish.Gateway
dotnet run --project Radish.Auth

npm run dev --workspace=radish.client
npm run dev --workspace=radish.console
```

### 默认端口

- API：`http://localhost:5100`
- Auth：`http://localhost:5200`
- Gateway：`https://localhost:5000`
- Frontend：`http://localhost:3000`
- Console：`http://localhost:3100`
- 文档：固定项目文档统一收口到仓库 `Docs/`，由 WebOS“文档”应用承载，不再维护独立 Docs 站点
- Scalar：Gateway `https://localhost:5000/scalar`，API 直连 `http://localhost:5100/scalar`

## 配置与数据库

### 配置加载优先级

```text
appsettings.Shared.json → appsettings.json
→ appsettings.Local.json（仅本地使用，不提交）→ 环境变量
```

- 配置文件只允许三种：共享 `appsettings.Shared.json`、宿主默认 `appsettings.json`、本地覆盖 `appsettings.Local.json`
- 禁止新增或提交这三种之外的 `appsettings.*.json` 变体，避免配置来源和口径漂移

### 关键配置

- `Snowflake.WorkId`：宿主差异配置，每实例唯一，范围 `0-30`
- `Snowflake.DataCenterId`：共享配置
- `MainDb/Databases`：至少包含 `ConnId=Main` 和 `ConnId=Log`
- `Redis.Enable/ConnectionString`：共享缓存配置
- `Redis.InstanceName`：宿主差异配置

### 数据库共享关系

- API 和 Auth 共享 `Radish.db`、`Radish.Log.db`
- Auth 独享 `Radish.OpenIddict.db`
- 所有数据库位于 `DataBases/`

### 多租户

- 字段级：`ITenantEntity`
- 表级：`[MultiTenant(Tables)]`
- 库级：`[MultiTenant(DataBases)]`

### 配置读取

- `AppSettings.RadishApp("Section", "Key")`
- 或实现 `IConfigurableOptions`

### 缓存策略

- `Redis.Enable = true` 时使用 Redis
- `Redis.Enable = false` 时使用内存缓存
- 注册方式：`builder.Services.AddCacheSetup();`

## 分层架构

### 依赖流

```text
Common → Shared → Model → Infrastructure → IRepository/Repository
→ Core → IService/Service → Extension → Api/Gateway/Auth
```

### 核心约束

1. `Common` 仅引用外部 NuGet 包，需要访问 Model/Service/Repository 的工具应放到 `Extension`
2. `Repository` 只返回实体，`Service` 必须映射为 DTO / ViewModel 后再暴露给 Controller
3. Controller 禁止直接注入 `IBaseRepository`
4. 先定义 `IService` / `IRepository`，再写实现
5. `Service` 层严禁直接使用 `_repository.Db.Queryable` 或 `_repository.DbBase.Queryable`
6. 优先扩展 `BaseRepository` 的通用方法，如 `QueryDistinctAsync`、`QuerySumAsync`
7. 复杂联表或性能优化需求再创建实体专属仓储

### ViewModel / Vo 规范

- 所有返回前端的 ViewModel 类必须使用 `Vo` 后缀
- 所有字段必须使用 `Vo` 前缀
- `UserVo` 使用混淆字段命名是安全设计，前端必须适配
- Controller 严禁返回匿名对象
- 优先使用 AutoMapper 的前缀识别映射，仅在字段名、类型或逻辑不一致时手动映射

## 软删除规范

- 业务数据优先使用软删除，避免物理删除
- 实体需实现 `IDeleteFilter`
- 查询自动过滤 `IsDeleted = true`
- 删除时记录 `DeletedAt`、`DeletedBy`
- 支持 `RestoreByIdAsync` 恢复

## 编码与前端规范

### 通用代码风格

- C# 使用 4 空格、文件范围命名空间、nullable
- React 组件默认使用 `const`
- 避免 `var`，默认 `const`，需要重赋值时使用 `let`
- React 中优先使用 `useState`、`useMemo`、`useEffect`
- 单文件建议控制在 `1000` 行左右，硬上限 `1500` 行
- 命名必须表达业务意图和技术边界，避免 `handleData`、`processInfo`、`commonHelper`、`managerUtil` 这类空泛命名或只有作者能理解的缩写
- 方法、Hook、组件和工具函数应有明确职责；不要拆出一串只转发参数、包同名调用或隐藏简单逻辑的私有方法
- 异常、空态、兼容和兜底逻辑必须有清晰业务原因，禁止用层层 fallback 掩盖契约不清、数据模型错误或调用边界混乱

### 文本文件编码与换行

- 仓库自有文本文件默认使用 `UTF-8` 无 `BOM`，避免引入乱码；如需保留外部参考资料或上游文件的特殊编码，必须有明确来源和理由
- 换行符遵循仓库根 `.editorconfig` 与 `.gitattributes`：默认使用 `LF`，仅 `*.sln`、`*.slnx`、`*.ps1`、`*.psm1`、`*.psd1`、`*.bat`、`*.cmd` 等 Windows 文件使用 `CRLF`
- 保持文件末尾换行；非 Markdown 文本避免尾随空格
- 面向人工审阅的 JSON、配置文本和本地持久化文本优先保持 `UTF-8` 可读文本，中文默认直写，仅保留语法必需或序列化器仍要求保留的转义
- 若怀疑当前修改引入乱码、换行漂移或 `BOM` 问题，优先执行 `npm run check:repo-hygiene:changed` 或 `npm run check:repo-hygiene:staged`

### 日志规范

#### 后端

```csharp
builder.Host.AddSerilogSetup();
Log.Information("User {UserId} logged in", userId);
```

- 应用日志：`Logs/{ProjectName}/Log.txt`
- SQL 日志：`Logs/{ProjectName}/AopSql/AopSql.txt`
- 审计日志：数据库 `AuditLog_YYYYMMDD`

#### 前端

- Client：`Frontend/radish.client/src/utils/logger.ts`
- Console：`Frontend/radish.console/src/utils/logger.ts`
- 禁止直接使用 `console.log/info/warn/error`
- 必须使用统一 `log` 工具

### 前端环境配置

- `.env.development`
- `.env.production`
- `.env.local`
- `.env.local.example`

规则：
- 所有环境变量必须以 `VITE_` 开头
- 敏感信息仅放 `.env.local`
- 通过 `env.ts` 访问配置，禁止直接使用 `import.meta.env`

### UI 与视觉规范

- 视觉口径以 `Docs/frontend/visual-theme-spec.md` 和 `Docs/frontend/visual-color-reference.md` 为准
- `radish.client` 必须支持 `default / guofeng` 主题切换
- 风格方向为淡雅新中式，不做厚重国潮，不牺牲可读性
- 云纹、山纹、水纹仅作边缘、角标、分区收边和弱背景修饰
- 颜色必须先抽象为语义 token，再应用到页面
- 禁止持续新增硬编码颜色
- 不得破坏 Dock、桌面图标、窗口容器、滚动区域等基础交互布局

### API 客户端规范

- 统一使用 `@radish/http` 提供的 API 客户端
- 禁止自定义 fetch / axios 封装
- 特殊场景如上传进度，可使用 `XMLHttpRequest`，但必须通过 `getApiClientConfig()` 取得统一配置

### 前端架构补充

- `@radish/ui` 无需构建，直接源码引用
- 修改 `radish.ui` 后应保证 client / console 可同步热更新

## 新增功能流程

### 后端

1. 在 `Radish.Model/Models` 定义实体，在 `Radish.Model/ViewModels` 定义 Vo
2. 在 `Radish.Extension/AutoMapperExtension/CustomProfiles` 增加映射
3. 优先复用 `IBaseRepository<TEntity>`
4. 简单 CRUD 优先复用 `IBaseService<TEntity, TVo>`
5. 复杂逻辑再创建自定义 Service / Repository
6. Controller 只注入 Service
7. 补充 `Radish.Api.http` 示例和对应测试

### 前端

- 通用组件放 `@radish/ui`
- API 调用与统一客户端能力优先复用 `@radish/http`
- WebOS 组件放 `Frontend/radish.client/src/`

## Rust 原生扩展

- 位置：`Lib/radish.lib/`
- 构建：

```bash
cd Lib/radish.lib
cargo build --release
```
- 配置：

```json
{
  "ImageProcessor": {
    "UseRustImplementation": true
  }
}
```

## 常见陷阱

1. 业务逻辑写在 Service，不写在 Controller
2. 返回 ViewModel，不直接暴露实体
3. 简单 CRUD 优先用 BaseService
4. 先定义接口再写实现
5. `Service` 层禁止直接访问 Db 实例
6. 避免内存过滤，优先数据库聚合
7. 敏感信息使用环境变量或 `appsettings.Local.json`
8. 每个环境设置唯一的 `Snowflake.WorkId`
9. `Common` 依赖只限外部包
10. `ApiModule.LinkUrl` 中的参数路由要包含正则

## Git 提交规范

- 必须遵循 Conventional Commits
- 复杂提交建议补充 `2-5` 条简洁说明
- 必须使用当前用户 Git 身份
- 严禁加入任何 AI 协作者署名

示例：

```text
feat(ui): 优化桌面主题与组件展示

- 对齐共享组件主题 token
- 修复图表初始化告警
- 调整 Dock 灵动岛收起态偏移
```

## 文档与更新要求

- `Docs/` 为唯一真相源
- 文档应按读者目标和阅读预算拆分，不应让单文件长期承载入口、规范、历史、验证流水和实现细节等多种职责。
- 入口类文档建议不超过 `300` 行，硬上限 `500` 行；包括 `Docs/index.md`、`Docs/README.md`、`Docs/development-plan.md`、`Docs/planning/current.md` 以及各目录索引。
- 架构、规范、设计类文档建议不超过 `600` 行，硬上限 `900` 行。
- 专题深度文档建议不超过 `800` 行，硬上限 `1200` 行。
- `Docs/changelog/`、`Docs/records/` 和归档资料可放宽篇幅限制，但必须按日期、阶段或批次拆分，并提供索引入口；超过 `1200` 行时应优先拆分或归档。
- 超过软上限时，应优先拆出历史记录、验证流水、实现细节或专题说明；超过硬上限时，除日志、记录和归档类文档外，不应继续追加内容，应先拆分或归档。
- 长文档顶部应提供简短摘要和必要目录，避免新会话为定位当前结论读取整篇历史。
- 重要目录包括：
  - `Docs/architecture/specifications.md`
  - `Docs/architecture/framework.md`
  - `Docs/frontend/design.md`
  - `Docs/frontend/visual-theme-spec.md`
  - `Docs/frontend/visual-color-reference.md`
  - `Docs/guide/`
  - `Docs/guide/validation-baseline.md`
  - `Docs/guide/document-governance.md`
  - `Docs/development-plan.md`
  - `Docs/planning/current.md`
  - `Docs/changelog/`
  - `Docs/deployment/guide.md`
- 更新日志使用 Asia/Shanghai（UTC+8）
- 重大架构、接口、流程、视觉规则变更必须同步更新文档

## 开发原则

1. 不做“玩具式最小实现”

- 交付必须覆盖用户真实需求和主要使用路径。
- 可以控制修改范围，但不能用临时方案、占位逻辑或半成品糊弄完成。

2. 测试和验证按风险分层

- 不要求任何改动都跑完整测试。
- 小改动优先做精准验证；涉及核心流程、公共模块、数据一致性或用户可见行为时，再扩大测试范围。
- 说明已验证的内容，以及未验证但存在风险的部分。

3. 代码优先清晰、直观、易维护

- 避免为了炫技引入复杂设计模式、过度抽象或晦涩写法。
- 代码应让新人和实习生也能顺着业务逻辑读懂。
- 只有在能明显降低复杂度、减少重复或符合现有架构时，才新增抽象。

4. 保持架构清晰

- 修改前先理解现有模块边界和调用关系。
- 优先沿用项目已有风格、目录结构和设计习惯。
- 不做无关重构，但遇到影响当前需求的结构问题时，应做小范围、必要的架构修正。

5. 不做无意义的“安全兜底”

- 不要为了表面稳妥到处吞异常、返回默认值或隐藏错误。
- 对明确的外部输入、边界条件、IO、网络、权限、并发等风险点，应做必要校验和错误处理。
- 兜底逻辑必须有明确目的，并且不能掩盖真实问题。

6. 避免不必要的函数嵌套

- 不写函数套函数、回调套回调等影响可读性的结构。
- 优先使用命名清晰的普通函数、早返回和顺序流程。
- 只有在闭包能明显简化状态管理且不影响阅读时，才允许局部函数。

7. 优先最小化修改范围

- 在满足需求和质量保证的前提下，尽量少改文件、少引入新变量、少新增函数。
- 不为单次需求扩展无关能力。
- 每个新增结构都应有明确用途，避免“顺手优化”和范围蔓延。

8. 决策顺序

- 先保证需求完整正确。
- 再保证架构边界清晰。
- 再控制修改范围和实现复杂度。
- 最后根据风险选择合适的验证方式。
