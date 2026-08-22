# AGENTS 指南

本文件为 AI 协作者提供 Radish 项目的启动级长期约束。

## 定位与优先级

- `Docs/` 是项目文档唯一真相源；本文件只保留任何任务开始时都必须知道的长期约束，不承载当前阶段、临时门禁、历史记录或专题实现细节。
- 当前阶段、优先级、停止线和“当前不做”以 `Docs/planning/current.md` 为准；架构、接口、流程、验证和视觉细则以对应专题文档为准。
- 项目所有者在当前任务中的明确要求优先于仓库默认流程；若要求会改变架构、规则、接口、依赖或运行时边界，仍应先说明影响并确认范围。
- 只读取当前任务需要的文档；不要默认展开 `Docs/changelog/`、`Docs/records/`、`Docs/planning/archive.md` 或历史 RC 材料。

## 称呼与语言

- 对话开始或结束总结时，请称呼项目所有者为 `萝卜SAMA`。
- 默认使用中文说明、讨论和编写文档。
- 代码、技术标识、配置键、命令、路径和引用名保留原文。

## 协作原则

- 涉及架构、规则、接口、依赖、运行时行为或范围不清的改动前，必须先说明方案并等待批准。
- 小规模、低风险、需求明确的文档、配置或清理类变更可直接实施。
- 需求不明确且不同解释会明显改变结果、风险或范围时，必须先澄清。
- 优先从根因、长期维护性、系统一致性和可验证性出发；不要把最小修复、临时兼容或层层兜底当作默认方案。
- 在满足需求和质量的前提下控制修改范围；不做无关重构，不为“架构感”增加无真实收益的抽象。
- 长期功能、重要页面、跨模块能力和阶段性目标默认先确认专题文档与边界，再进入实现。
- 更完整的任务推进、个人开发者阶段和文档分层规则见 `Docs/guide/agent-collaboration.md`。

## 必须单独授权的操作

- 包安装或依赖更新：`dotnet add package`、`npm install`、`npm update`、`npm ci`。
- 项目启动：`dotnet run`、`npm run dev` 及组合启动脚本。
- 授权只覆盖当前任务中已说明的命令、版本、端口、运行影响和清理方式；不得沿用历史会话或上一轮任务的授权。
- 若命令、依赖范围或运行影响发生实质变化，必须重新说明并获得授权。
- 代码读写、构建、测试、类型检查、Lint、静态分析和常规 Git 操作可直接执行。
- 默认先在沙盒中验证；若受权限、网络、证书或 PATH 限制而无法取得真实结果，可为构建、测试或已获授权的操作申请最小范围提权。

## 任务文档路由

| 任务 | 优先读取 |
| --- | --- |
| 当前阶段、优先级、下一步 | [当前进行中](Docs/planning/current.md)；不足时再读[开发路线图总览](Docs/development-plan.md) |
| Agent 协作、开发节奏、生产证据边界 | [Agent 协作与执行规则](Docs/guide/agent-collaboration.md) |
| 架构、分层、编码与接口规范 | [架构总览](Docs/architecture/overview.md)、[开发规范](Docs/architecture/specifications.md)、[开发框架说明](Docs/architecture/framework.md) |
| 本地验证、CI 对齐、回归入口 | [验证基线说明](Docs/guide/validation-baseline.md) |
| 页面联调与浏览器 Smoke | [浏览器 Smoke 规则](Docs/guide/browser-smoke.md) |
| 环境、启动、端口与排障 | [快速开始](Docs/guide/getting-started.md)、[运行与排障](Docs/guide/operations-runbook.md) |
| 配置与数据库 | [配置管理](Docs/guide/configuration.md)、[数据库总览](Docs/guide/database-overview.md) |
| Web / Flutter / WebOS / Tauri 边界 | [前端设计](Docs/frontend/design.md)、[前端多壳层策略](Docs/frontend/shell-strategy.md)、相关功能专题 |
| UI、主题与 Pencil | [UI 差异附录](Docs/frontend/ui-addendum.md)、[视觉主题](Docs/frontend/visual-theme-spec.md)、[颜色参考](Docs/frontend/visual-color-reference.md)、[Pencil 流程](Docs/frontend/pencil-representative-page-workflow.md) |
| 分支、PR 与回灌 | [分支与 PR 治理 ADR](Docs/adr/0001-branch-and-pr-governance.md) |
| 文档拆分、索引和篇幅 | [文档篇幅治理](Docs/guide/document-governance.md) |

## 项目长期边界

- 技术栈：ASP.NET Core 10 + SQLSugar ORM + PostgreSQL（本地默认 SQLite）/ React 19 + Vite（Rolldown）+ TypeScript。
- 正式产品线只保留 Web 与 Flutter Native；Web 优先覆盖 PC / mobile 浏览器，Flutter 次级覆盖原生 PC / mobile 安装包。
- `radish.client` 的 `/desktop` 只保留 WebOS 历史兼容；`Clients/radish-tauri` 是正式弃用资产，不进入当前开发、UI、CI、构建、发布或验收门禁。
- 前端使用 npm workspaces：`radish.http`、`radish.client`、`radish.console`、`radish.ui`。
- `@radish/ui` 为源码直连共享组件库；`@radish/http` 是统一 API 客户端和类型边界。
- 后端宿主为 `Radish.Api`、`Radish.Gateway`、`Radish.Auth`，初始化与只读自检入口为 `Radish.DbMigrate`。
- Flutter Native 位于 `Clients/radish.flutter`；Rust 扩展位于 `Lib/radish.lib`。

## 实现红线

### 后端

- 依赖流、项目职责和详细分层以架构文档为准；`Common` 不依赖业务层。
- Repository 只返回实体，Service 映射 DTO / Vo，Controller 只注入 Service 且不得返回匿名对象或直接暴露实体。
- 先定义 `IService` / `IRepository` 再实现；简单 CRUD 优先复用 `BaseService` / `BaseRepository`。
- Service 禁止直接使用 `_repository.Db.Queryable` 或 `_repository.DbBase.Queryable`；通用查询能力优先沉淀到 Repository。
- 对外 ViewModel 使用 `Vo` 后缀和 `Vo` 字段前缀；业务数据默认采用可审计的软删除。
- 配置文件只允许共享 `appsettings.Shared.json`、宿主 `appsettings.json` 和本地不提交的 `appsettings.Local.json`。

### 前端

- API 调用统一使用 `@radish/http`；除上传进度等有明确理由的场景外，不新增 fetch / axios 封装。
- Client 与 Console 禁止直接使用 `console.log/info/warn/error`，必须使用各自统一 `log` 工具。
- 环境变量必须以 `VITE_` 开头并通过 `env.ts` 访问；敏感信息只放 `.env.local`。
- 颜色先抽象为语义 token；不得持续增加硬编码颜色，也不得破坏 Dock、窗口、滚动区等基础交互边界。
- `radish.client` 必须支持 `default / guofeng` 主题；更完整视觉与多端规则按 UI 专题执行。

### 通用

- C# 使用 4 空格、文件范围命名空间和 nullable；React 组件默认使用 `const`，变量优先 `const`，需要重赋值时使用 `let`。
- 单个代码文件建议控制在 `1000` 行左右，硬上限 `1500` 行；超过上限时按真实职责拆分，不做机械切片。
- 命名必须表达业务意图和技术边界；禁止空泛工具类、晦涩缩写、无意义转发方法和影响可读性的函数嵌套。
- 仓库自有文本默认使用 UTF-8 无 BOM、规定换行和文件末尾换行；怀疑漂移时执行仓库卫生检查。
- 异常、兼容和兜底必须对应明确边界风险，不得吞掉真实错误或掩盖契约问题。

## 验证与文档

- 按风险选择定向测试、构建、类型检查、Lint、静态分析和 `git diff --check`；不要求每次改动都跑全量测试。
- 验证和留痕区分开发中、准备合并到 `master`、发布部署三种粒度，具体命令以验证基线为准。
- 真实 Smoke 只在专题、阶段性任务或成组功能准备验收时，或用户明确要求时执行；执行前必须确认本任务已授权并启动所需服务，默认同时覆盖 PC 与 mobile。
- 修改架构、接口、流程、视觉或项目规则时同步更新对应 `Docs/` 真相源；历史流水和证据进入 records、changelog 或 archive，不堆入入口文档。

## Git 约束

- 日常协作分支为 `dev`；`master` 是稳定主线，只通过 Pull Request 合并。
- `master` 允许 merge commit 与 rebase merge，禁用 squash merge；合并后必须先按 ADR 将最新 `origin/master` 回灌 `dev`，再开始下一轮开发。
- 回灌禁止使用 rebase、reset 或 force push 伪造同步，也不会自动触发 tag、发布或部署。
- 提交信息必须符合 Conventional Commits；复杂提交建议补充 `2-5` 条简洁说明。
- 必须使用当前用户 Git 身份，严禁加入任何 AI 协作者署名。

## 入口文件维护

- `AGENTS.md` 与 `CLAUDE.md` 只在跨任务、跨阶段且必须启动即生效的长期约束发生变化时修改。
- 阶段状态、临时门禁、“当前不做”和批次事实应更新其 Docs 真相源，不得复制回根入口。
- 两个文件除标题和首段入口名称外必须逐字同步；修改任一文件时同步另一份，并执行 `npm run check:docs`。
- 详细维护判定与内容归位规则见 `Docs/guide/agent-collaboration.md` 和 `Docs/guide/document-governance.md`。
