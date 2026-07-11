# P3-12-E8-Q 正式发布工程成熟度与安全收口

> 状态：`Q0 为进入 P3-12-F 前的硬门禁；Q1 / Q2 / Q3 调整为 F 内 Release Go 门禁；Q4 为持续维护线`
>
> 复核日期：`2026-07-11`
>
> 入口：以 [当前进行中](/planning/current) 为准。本专题承接 [P3-12-E 正式产品成熟度与质量硬化](/planning/p3-12-product-maturity-quality-hardening) 和 `P3-12-E8 产品形态差距回拉`，不替代 `E8-B` 的产品链路治理。

## 摘要

2026-07-10 全仓只读审计确认：Radish 的功能覆盖、产品规划、分层约束和测试资产已经达到第三阶段后期水平，但发布工程成熟度落后于产品完成度。当前存在高危依赖、生产调试接口、身份日志与验证配置、请求内 fire-and-forget 业务写、错误契约、时间语义、数据库升级、质量门禁和仓库历史债务等问题。

因此新增 `P3-12-E8-Q 正式发布工程成熟度与安全收口`，并按交付阶段重新分层：

- `E8-B` 负责进入 F 前的有限产品收口，只覆盖内容优先首页、社区核心旅程、公开 Docs 契约和 Console 产品边界。
- `E8-Q0` 是进入 F 前的安全与生产暴露面硬门禁；未完成前不扩大正式暴露面。
- `Q1 / Q2 / Q3` 的发布必要子集在进入 F 后实施，并作为生产 Release Go 门禁，不再要求全部完成后才允许进入候选阶段。
- `Q4` 按 touched-file / 专题持续治理；若其中发现直接影响本次发布的高风险项，应提升到对应 Release Go 清单，而不是要求 Q4 全量完成。
- 合并到 `master`、创建 tag 和生产发布是三个独立决策；集成 PR 不再被发布门禁长期阻塞。
- Q1 / Q2 涉及架构、接口、事务或运行时契约时，仍须先提交子方案并等待确认。

## 目录

- [一、阶段判断](#一阶段判断)
- [二、审计基线](#二审计基线)
- [三、目标与停止线](#三目标与停止线)
- [四、整改批次](#四整改批次)
- [五、执行顺序](#五执行顺序)
- [六、P3-12-F 与 Release Go 门禁](#六p3-12-f-与-release-go-门禁)
- [七、验证与留痕](#七验证与留痕)

## 一、阶段判断

### 1. 当前成熟度

| 维度 | 当前判断 | 主要依据 |
| --- | --- | --- |
| 开发进度 | 第三阶段后期 | Public / Private / Author / Console 主页面族和 Web-first 路线已建立 |
| 规划与产品边界 | 较成熟 | P3-12 阶段、停止线、页面矩阵和真实旅程记录较完整 |
| 后端架构 | 中上 | Service / Repository / Vo 分层总体保持，未发现 Controller 直接注入 Repository 等系统性破坏 |
| 前端架构 | 中等 | 统一客户端、共享 UI 和语义 token 已建立，但仍有直接请求、超大组件和严格度不一致 |
| 自动化测试 | 中上 | 后端、Web、Console、HTTP、Flutter 均有测试资产，但真实宿主、数据库迁移和组件交互测试偏薄 |
| 安全与发布 | 未达候选标准 | 高危依赖、生产性能测试端点、Claims 日志和审计门禁缺口仍存在 |
| 可维护性 | 需要专项治理 | 大文件、时间语义、版本漂移、文档膨胀和历史卫生债务较明显 |

### 2. 主线关系

```text
P3-12-E8 Pre-RC
├── E8-B 有限产品收口
└── E8-Q0 安全与暴露面阻断
    └── 满足进入条件后进入 P3-12-F

P3-12-F 正式版发布候选
├── Q1 关键写可靠性与安全错误契约
├── Q2 最小生产升级与版本闭环
├── Q3 候选质量门禁与高风险测试
├── 候选级 smoke、回滚材料与受控试用
└── 满足 Release Go 后才允许 tag / 生产发布

Q4 维护性与仓库治理
└── 按触达范围持续下降，不作为进入 F 的全量前置
```

产品体验与工程安全仍然不能互相替代，但“进入候选阶段”和“允许正式发布”必须区分。Q0 与 E8-B 决定是否可以进入 F；Q1 / Q2 / Q3 决定 F 是否具备 Release Go；Q4 不再制造无限 pre-F。

## 二、审计基线

### 1. 仓库与变更规模

- 2026-07-10 审计基线位于 `dev`，审计开始时工作区干净。
- 截至 2026-07-11，`master` 独有 8 个提交、`dev` 独有 82 个提交；`master` 独有项主要为阶段 PR 的 merge commit，不代表 8 组独立代码分叉，但恢复集成前仍必须刷新范围。
- `master...dev` 当前约 `255` 个文件、`25,664` 行新增、`4,492` 行删除，待集成批次已经偏大，不应继续等待 Q1-Q4 全量完成后再合并。
- 根项目、前端 workspace、.NET 和 Rust 原生库仍主要声明 `26.1.1`，但测试 tag 已到 `v26.5.5-test`，生产记录为 `v26.3.2-release`。

### 2. 已执行验证

| 验证项 | 结果 |
| --- | --- |
| 后端解决方案构建与默认 baseline | 通过，但产生约 795 条主要为 `CS1591` 的警告 |
| `Radish.Api.Tests` | `551 / 551` 通过 |
| `radish.client` 测试 | `311 / 311` 通过 |
| `radish.console` 测试 | `27 / 27` 通过 |
| `@radish/http` 测试 | `3 / 3` 通过 |
| `flutter analyze` | 通过，无分析问题 |
| `flutter test` | `204 / 204` 通过 |
| 根 `npm run lint` | 未通过，约 5 个 error、26 个 warning |
| Tauri `cargo test --locked` | 未通过，`icons/icon.png` 为 RGB，Tauri 生成上下文要求 RGBA |
| `Lib/radish.lib cargo test --locked` | 未执行成功，缺少 `Cargo.lock` |
| 全仓卫生等价扫描 | 发现约 128 条历史文件问题及 32 条文档长度告警 |

本轮没有执行 Gateway、宿主或 PC / mobile 真实页面 smoke。运行态复核仍需用户在当轮明确说明前后端已经启动。

### 3. 发布阻断证据

- `npm audit --omit=dev` 命中 3 个 High，涉及 `react-router / react-router-dom` 与 `ws`。
- `.NET` 依赖审计命中 `Microsoft.OpenApi 2.0.0` High，并通过项目引用进入多个宿主。
- `Scripts/dotnet-command.mjs` 默认追加 `NuGetAudit=false` 与 `NoWarn=NU1903`，会隐藏依赖审计，并覆盖项目原有 `NoWarn`。
- `RustTestController` 对普通 `radish-api` scope 开放，默认执行 50,000,000 次计算，部分路径会按输入分配内存或使用并行 CPU。
- API Client Policy 与 NotificationHub 以 Information 级别输出全部 Claims；JWT 关闭 Audience 校验。
- Auth 无条件调用 `DisableTransportSecurityRequirement()`，现有配置没有真正约束该行为。

### 4. 可靠性与维护性证据

- Service / Job 中存在多处 `_ = Task.Run(...)`，部分承载奖励、资产、通知持久化或高亮重算。
- 多个 Controller 直接返回 `ex.Message`，并存在较多宽泛 `catch` 与 `throw new Exception`。
- `FileAccessTokenService` 使用查询、判断、内存加一、更新的非原子流程，令牌以明文存储。
- 业务代码中大量混用 `DateTime.Now / Today / UtcNow`。
- DbMigrate 主要使用 `CodeFirst.InitTables` 与局部补丁；Auth 启动时使用 `EnsureCreated()`。
- 默认 baseline 只运行 client 测试，没有纳入现有 console / HTTP 测试。
- Console TypeScript 仍为 `strict: false`。
- 多个 C#、TSX、Dart 生产文件超过项目建议或硬性行数上限。

## 三、目标与停止线

### 1. 目标

1. 清除当前正式发布的已知安全阻断。
2. 让不可丢失业务写具备明确事务边界、幂等、重试和失败观测。
3. 建立稳定的 API 错误、时间、数据库升级和版本契约。
4. 让默认验证真正覆盖已有测试资产和依赖安全。
5. 建立历史质量债务的可见基线和持续下降机制。
6. 给 `P3-12-F` 提供可客观复核的进入条件。

### 2. 停止线

- 不把本专题扩展为完整微服务、通用消息平台、全量 E2E 平台或大而全可观测性平台。
- 不为消除告警而关闭分析器、审计或吞掉异常。
- 不用多层 fallback 掩盖事务、数据模型或接口契约问题。
- 不机械拆分大文件；拆分必须围绕真实职责、状态边界或复用边界。
- 不要求 Q4 一次清空所有历史颜色、文档和格式债务。
- 不把 Tauri、Flutter 或 Rust 原生库默认提升为本次 Web 正式版前置门槛；先明确发布矩阵。
- 包安装或项目启动仅在说明命令、影响并取得当前任务明确授权后执行；依赖调整和运行态验证继续遵循仓库协作边界。

## 四、整改批次

### Q0 安全与暴露面阻断

实施方案见 [P3-12-E8-Q0 安全与暴露面阻断实施方案](/records/p3-12-e8-q0-security-exposure-implementation-plan-2026-07-10)。Q0-A 至 Q0-D 已于 2026-07-11 完成代码与静态验证；真实宿主链路留到 Q0 成组运行态验收。

#### Q0-A 依赖安全与审计恢复

状态：`2026-07-11 已完成`。npm / NuGet High / Critical 已清零，默认 NuGet 审计已恢复，`Dependency Security` 已纳入 workflow、ruleset 模板与 contract；实施和验证证据见 Q0 方案第九节。

- 恢复 NuGet 审计，不再在默认命令层全局追加 `NuGetAudit=false`。
- 修正 `NoWarn` 传递方式，避免覆盖 `Directory.Build.props` 或各项目原有设置。
- 将 `Microsoft.AspNetCore.OpenApi` 从 RC 包统一到与 .NET 10 正式版兼容的稳定版本。
- 将 `Microsoft.OpenApi`、`react-router-dom` 和 `ws` 升级到审计无 High / Critical 的兼容版本。
- 在 Repo Quality 或独立安全 job 中加入 npm / NuGet 依赖审计。
- 依赖升级后定向复核 Scalar/OpenAPI、Console 路由和 SignalR。

退出条件：

- `npm audit --omit=dev` 无 High / Critical。
- `dotnet package list --project Radish.slnx --vulnerable --include-transitive --format json --no-restore` 无 High / Critical。
- 默认 baseline 不再主动关闭 NuGetAudit。
- OpenAPI、Console 路由和 SignalR 定向回归通过。

#### Q0-B 生产调试与高消耗端点退出

状态：`2026-07-11 已完成`。生产性能、Weather、敏感配置、事务演示、测试租户写入与手动未读推送入口已删除，正常租户查询与正式通知能力保留；专用模型和无调用方服务链已清理，精确防回归契约已建立，证据见 Q0 方案第十一节。

- 将 `RustTestController` 从生产编译或生产路由中移除；既有本地 Rust benchmark 保留，后续如需 C# / Rust 对比再单独迁入测试 / benchmark 工程。
- 将 `WeatherForecastController` 及其 DI、缓存、审计写入演示动作移出生产 API。
- 扫描 OpenAPI 中的测试、演示、性能、调试标签，确认没有普通用户可调用的资源消耗端点。
退出条件：生产 OpenAPI 不再暴露性能基准、Weather、敏感配置、事务演示或测试写入 / 手动推送接口。

#### Q0-C 身份验证与敏感日志

- JWT 启用 Audience 校验并以 `radish-api` 为明确资源契约。
- 删除 Client Policy、NotificationHub 及其他入口的完整 Claims 日志。
- 日志只允许输出诊断所需的安全字段白名单，并默认降到 Debug。
- 让 `AllowInsecureHttp` 或等价配置真正控制 OpenIddict transport security；生产只允许经过明确可信代理边界的部署方式。
- 补 Audience 错误、scope 错误、Hub 用户标识和日志脱敏回归测试。

退出条件：代码扫描不再命中“所有 Claims”日志，错误 audience token 被拒绝，生产配置不能无条件关闭 transport security。

#### Q0-D 前端危险链接协议

- `RichTextMarkdownEditor` 与 MarkdownRenderer 共用链接协议白名单。
- 默认只允许 `http`、`https`、安全相对地址和项目明确支持的自定义协议。
- `javascript:`、`data:text/html` 等危险协议必须被移除或退化为纯文本。
- 补编辑态、预览态和历史恶意内容回放测试。

退出条件：编辑态与阅读态对危险协议行为一致，定向安全测试通过。

### Q1 写可靠性与错误契约（F 内 Release Go）

#### Q1-A 事务后可靠任务

状态：`2026-07-11 已完成实施与候选级 PostgreSQL / Hangfire 验证`。

- 先形成全部 fire-and-forget 清单，按“可丢失实时推送 / 不可丢失业务写 / 可重算派生数据”分类。
- 奖励、资产、权益、通知持久化等不可丢失写采用事务后 Outbox 或 Hangfire 持久任务。
- 后台任务必须重新创建依赖作用域，不捕获请求 scope 中的 Repository / Service。
- 每类任务定义业务幂等键、重试策略、最终失败记录和人工重放入口。
- SignalR 在线推送允许 best-effort，但持久化通知和业务资产不能依赖推送成功。

进入代码前置：先提交 `事务边界 + 任务载荷 + 幂等键 + 重试 / 失败策略` 子方案并确认。

退出条件：不可丢失业务写不再使用裸 `_ = Task.Run`；进程中断与重复执行测试不造成资产重复或业务状态丢失。

#### Q1-B API 错误契约

状态：`2026-07-11 已完成全局安全边界、稳定错误码、诊断关联和关键 API 迁移`。

- 建立全局异常处理和稳定错误码，不再由 Controller 重复捕获未知异常。
- 参数、权限、未找到、冲突、限流和未知异常映射到正确 HTTP 状态。
- 未知异常只向客户端返回安全文案与 correlation ID，原始异常仅进入服务端日志。
- 将 `throw new Exception` 分批替换为领域异常或明确返回结果。
- 保持 `MessageModel` 兼容边界，避免一次性破坏全部前端；新旧契约迁移必须有明确停止点。

进入代码前置：先确认错误响应结构、错误码命名和兼容策略。

退出条件：生产 Controller 不直接返回未知 `ex.Message`；关键 API 状态码与前端错误恢复测试通过。

#### Q1-C 文件访问令牌

状态：`2026-07-11 运行时实施与 SQLite 验证已完成；待目标库 apply / verify 与 PostgreSQL 双 Worker 用例`。

- 数据库存储 token hash，只在创建时返回一次原始 token。
- 验证与计数采用带未撤销、未过期、用户 / IP 匹配、`AccessCount < MaxAccessCount` 条件的原子更新。
- 为 hash、有效状态和检索路径设计索引。
- 完成管理员权限边界，不保留权限 TODO。
- 补并发争抢、撤销竞争、过期边界和无限次数测试。

进入代码前置：先确认数据迁移、旧 token 兼容和回滚口径。

退出条件：并发访问不能突破最大次数，数据库不保存可直接使用的明文 token。

### Q2 时间、数据库与版本治理（F 内 Release Go）

#### Q2-A 时间语义

- 持久化、跨服务契约和过期比较统一使用 UTC。
- 注入 `TimeProvider`，禁止新业务逻辑直接使用 `DateTime.Now / Today`。
- 只有自然日边界、Hangfire 调度和用户展示使用配置的业务 / 用户时区。
- 盘点奖励、签到、过期、冷却、统计和幂等窗口，按风险分批迁移。
- 对既有本地时间数据给出识别、转换和兼容策略。

进入代码前置：先提交时间语义与历史数据迁移方案。

#### Q2-B 数据库演进

- 建立 schema version ledger，记录每个正式版本需要的结构、数据补丁和验证。
- DbMigrate 明确 `doctor / apply / verify` 的职责与幂等要求。
- 对已有生产版本执行“生产相似快照 → 新版本”的 PostgreSQL 升级演练。
- OpenIddict 数据库改为显式迁移，不再依赖 Auth 启动时 `EnsureCreated()` 演进结构。
- 每次正式发布保留备份、升级、验证和回滚 / 前滚说明。

进入代码前置：先确认 schema ledger 格式、OpenIddict 迁移归属和首个生产基线版本。

#### Q2-C 版本单一真值源

- 统一 .NET、npm workspace、Tauri、Flutter、Rust、tag 和镜像标签的版本归属。
- 明确哪些客户端独立版本，哪些跟随 Radish 日历版本。
- tag 创建前校验源版本、部署版本和发布记录一致。
- 不再让 `26.1.1` 源版本长期与 `v26.5.x` tag 并存而无解释。

退出条件：版本规则写入发布指南，自动校验能阻止不一致 tag / 镜像发布。

### Q3 质量门禁与测试升级（F 内 Release Go）

#### Q3-A Lint、编译警告与严格度

- 先修复根 lint 的全部 error。
- Hook dependency warning 按真实副作用语义修复，不通过禁用规则清零。
- 修复命令行 `NoWarn` 覆盖问题，建立编译警告分类和下降预算。
- Console 采用目录 / 模块分批方式开启 `strict`，不一次性用断言掩盖错误。
- `@radish/ui` 补最小测试入口，覆盖共享状态、Markdown URL 和关键组件契约。

#### Q3-B 默认 baseline 与 CI

- 默认 baseline 纳入已有 client、console 和 HTTP 测试。
- PR 保留 changed-only 快速检查，同时增加定期或候选前全量 hygiene / lint。
- Repo Quality 增加依赖安全 job；发布镜像前执行候选验证或依赖已通过的验证 workflow。
- Docker 发布补最小 SBOM、镜像漏洞扫描和构建来源信息；是否签名按发布阶段另行评估。
- 不用新增巨大 E2E 平台替代已有分层验证。

#### Q3-C 高风险集成测试

优先补以下测试：

- JWT issuer / audience / scope、refresh 和 Hub 用户标识。
- PostgreSQL schema 升级、OpenIddict 迁移和 DbMigrate 幂等。
- 奖励 / 资产任务重复执行、失败重试和进程中断恢复。
- 文件访问令牌并发上限。
- 租户隔离、软删除和恢复关键路径。
- API 错误状态、稳定错误码和 correlation ID。

退出条件：合并基线覆盖现有测试资产，候选前验证能够发现依赖漏洞、迁移失败和关键并发问题。

### Q4 维护性与仓库治理（持续维护）

#### Q4-A 代码热区

- 按职责拆分 `CommentService`、`CoinService`、`ExperienceService`、`BaseRepository`、`Radish.Api/Program.cs` 等后端热区。
- 按页面状态、数据加载、动作和展示拆分 `PublicForumDetail`、`ChatApp`、`MeApp`、`DocsAuthorApp` 等前端热区。
- `i18n.ts` 按语言和业务域拆分 namespace。
- Flutter 大页面按状态协调、区块组件和业务动作拆分，但不抢占当前 Web 主线。
- 评估 `Radish.Core`：形成明确职责并被真实引用，或在确认无依赖后移除空壳项目。

#### Q4-B 共享前端边界

- 普通业务 API 统一进入 `@radish/http`，保留 OIDC、早期 bootstrap 和上传进度等有明确原因的特殊请求。
- 合并 client / console / HTTP 重复的 `logSanitizer`，UI 组件不直接承担应用日志策略。
- 按页面族迁移硬编码颜色到语义 token，不进行一次性全仓替换。
- token service 只共享 JWT 解析、错误分类、存储抽象等稳定原语；client / console 的会话策略允许保持差异。

#### Q4-C 文档与仓库卫生

- 修复活动源码中的 `U+FFFD`，再按目录分批清理 BOM、末尾换行和尾随空格。
- 对历史卫生债务建立基线，changed-only 继续阻止新增，全量扫描用于周期收口。
- 拆分超过硬上限的架构、指南和功能专题；验证流水和历史批次迁入 records / changelog / archive。
- `current.md` 只保留最近状态、当前目标、下一顺位和维护线。
- 设计图、截图和输出证据建立保留 / 归档策略，避免仓库持续无界增长。

#### Q4-D 客户端与原生发布矩阵

- Tauri 如果属于本次发布，修复 RGBA 图标并让 `cargo test/build --locked` 进入候选验证；否则明确标记为非本次发布资产。
- `Lib/radish.lib` 若作为可发布原生依赖，补 `Cargo.lock`、FFI 空指针检查和跨平台构建；否则保持默认关闭并在文档中声明实验状态。
- Flutter 当前保持维护线，继续保留 analyze / test，不扩大为 Web 正式版阻断面。

## 五、执行顺序

### 第一批：Pre-RC 硬门禁

1. 按 `Q0-A → Q0-B → Q0-C → Q0-D` 推进安全与生产暴露面治理。
2. 用 E8-B 有限矩阵收口内容优先首页、内容参与、关系复访、聊天回流、信任治理、公开 Docs 契约和 Console 边界。
3. 两条线达到进入条件后刷新 `master...dev`，形成集成判断。

Q0 的四个子批次保持独立提交，不与 E8-B 页面调整混为同一提交。

### 第二批：进入 P3-12-F 与恢复集成

- 合并到 `master` 不等于创建 tag 或生产发布。
- Q0 与 E8-B 收口后应优先准备当前 `dev -> master` 集成，不等待 Q1-Q4 全量清理。
- P3-12-F 从进入第一天起就承接正式候选的可靠性、升级、版本和运行态验证工作。

### 第三批：F 内 Release Go 治理

- Q1-A 与 Q1-B 已完成；Q1-C 代码已收口，待数据库迁移和 PostgreSQL 并发门禁后进入 Q2。完整领域错误码继续按触达范围迁移，不回流为当前主线。
- Q2 先完成本次发布必须的 PostgreSQL / OpenIddict 升级闭环、版本单一真值和高风险时间语义，不做全仓机械替换。
- Q3 先把现有 client、console、HTTP、后端和依赖审计纳入候选基线，再补迁移、并发和身份等高风险集成测试。
- 候选运行态、回滚材料与受控试用结果共同决定 Release Go。

### 第四批：Q4 持续治理

只立即处理活动源码乱码、正式发布矩阵分类和会直接阻断当前修改的热区；其余大文件、strict、共享边界、历史颜色、文档和仓库卫生债务建立基线，按后续真实开发触达逐步下降。

## 六、P3-12-F 与 Release Go 门禁

### 1. 进入 P3-12-F

进入 `P3-12-F 正式版发布候选` 前必须同时满足：

1. npm / NuGet 无 High、Critical 已知漏洞，默认门禁不再关闭审计。
2. 生产 OpenAPI 不暴露 Rust 性能测试和 WeatherForecast 演示接口。
3. JWT audience 生效，不再输出完整 Claims，Auth transport security 由环境契约控制。
4. 前端编辑态和阅读态均拒绝危险 URL 协议。
5. E8-B 有限产品矩阵通过或形成明确的接受后置清单，不再保留无边界“产品负责人满意度”门槛。
6. 公开 Docs 的列表、搜索和详情由服务端保证 `Published + Public`。
7. 本次正式发布矩阵明确为 Gateway、API、Auth、DbMigrate、client 和 Console；Flutter 为维护线，WebOS 为兼容线，Tauri / Rust 为实验线。
8. 核心社区路径没有已知 `P0/P1`，并已刷新 `master...dev` 集成范围。

以上条件只决定是否可以进入正式候选阶段，不代表已经允许创建 tag 或生产发布。

### 2. F 内生产 Release Go

创建正式 tag 或进入生产发布前必须满足：

1. 本次发布范围内不可丢失的奖励、资产、权益和通知写不再依赖裸 fire-and-forget，并具备幂等、重试和失败观测。
2. 未知服务端异常不再以原始 `ex.Message` 返回客户端；完整领域错误码迁移可在安全边界成立后分批完成。
3. 文件访问令牌若进入本次发布，则通过并发上限测试且数据库不保存可直接使用的明文 token；否则必须退出正式暴露面。
4. 完成一次生产相似 PostgreSQL 与 OpenIddict 升级演练，并具备验证、备份和恢复 / 前滚说明。
5. 版本号、tag、镜像和发布记录规则一致且可自动校验。
6. 根 lint 无 error；client、console、HTTP、后端与依赖安全检查进入候选基线。
7. Tauri / Rust / Flutter 的发布归类已写明；只有归入本次发布的资产才要求构建通过。
8. 活动源码不存在已知 `U+FFFD`；其余历史卫生、warning、strict 和大文件债务已有基线与后续入口。
9. 用户当轮确认前后端已启动后，完成 Gateway PC / mobile 候选级 smoke，并抽查安全端点、可靠任务、错误契约、文件令牌和升级恢复。
10. 小规模受控试用已记录激活、首次参与、收到回应后的回流、核心任务失败和用户反馈，未发现未处置 `P0/P1`。

### 3. 不作为进入 F 的全量前置

以下事项只按风险或触达范围治理：

- 全仓 `DateTime.Now / Today / UtcNow` 一次性替换。
- Console 全量开启 TypeScript strict。
- 所有 C# / TSX / Dart 大文件一次性拆分。
- 历史颜色、BOM、尾随空格、长文档和截图证据全部清零。
- 完整 E2E、完整可观测性、微服务、通用消息平台或完整 SBOM / 签名平台。
- Flutter、Tauri 和 Rust 实验资产扩大为正式 Web 发布阻断面。

不得用这些历史债务继续延长 Pre-RC；也不得用“已经进入 F”跳过 Release Go 的安全、数据和升级证据。

## 七、验证与留痕

### 1. 开发中

- 文档批次：`npm run check:repo-hygiene:changed`、`git diff --check`。
- 前端批次：对应 workspace type-check / test / build、changed lint。
- 后端批次：定向测试、`dotnet build`、必要的 `dotnet test`。
- Rust / Flutter：只在对应发布矩阵或改动范围命中时执行。

### 2. 准备合并到 master

- `npm run validate:ci`
- `npm run validate:baseline`
- `npm run validate:identity`
- npm / NuGet 依赖审计
- 当前变更范围的 lint、repo hygiene 和必要专题测试
- `master...dev` 范围与 merge commit 拓扑复核

集成 PR 不要求提前完成全部生产升级演练、全仓 Q4 或正式 tag 材料。

### 3. P3-12-F 候选与 Release Go

- 执行 PostgreSQL / OpenIddict 迁移演练、版本一致性和 Q1 / Q2 / Q3 高风险测试。
- 用户确认服务已启动后执行 `validate:baseline:host`、`check:host-runtime -- --details --report`。
- Gateway PC `1920x1080` 与 mobile `390x844` 覆盖 Public / Private / Author / Console / Auth 主路径。
- 执行安全端点、错误契约、任务幂等、文件令牌并发和升级恢复抽查。
- 记录受控试用结论；发布记录只保留汇总结论与证据链接，命令级流水写入 `Docs/records/`。

### 4. 文档维护

- 本专题只维护范围、顺序、停止线、进入 F 与 Release Go 条件。
- 每个 Q 子批次的方案、实现事实和验证结果写入对应专题或 `Docs/records/`。
- `current.md` 只记录正在执行的 Q 批次、E8-B 有限矩阵和下一顺位，不复制历史流水。
