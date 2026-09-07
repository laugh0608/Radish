# 项目全面审阅记录（2026-09-06）

> 日期：2026-09-06（Asia/Shanghai）。审阅基线：本地 `dev`，`f3c5dde5`，源码版本 `26.8.2`。
>
> 本记录保存本次审阅的事实与判断；改进范围和候选状态见[工程改进候选清单](/planning/engineering-improvement-candidates)，即时顺位见[当前进行中](/planning/current)。
> 后续项目所有者要求完善文档，本批未修改运行时代码、依赖、CI 或部署配置。

## 1. 范围与总体判断

检查范围包括规划与协作规则、后端项目引用、前端与 Flutter 组织、验证脚本、CI、发布编排，以及认证、Outbox 和代表测试。采用静态抽查与定向执行，未逐行审计全仓。

总体判断：产品主轴与工程边界已形成，Web 已进入发布后维护，Native 仍按平台建设。后续维护压力主要来自功能广度、多平台系统集成、热点代码复杂度及验证结论与实际覆盖不完全一致。

值得保留的基础：

- 产品明确区分社区核心、知识 / 治理支撑和辅助激励；Web / Flutter Native 正式分工清楚。
- `Radish.Common` 没有内部业务项目引用；Service / Repository 分层与接口契约已经落地。
- 业务写入具备事务、幂等、版本条件更新、可靠任务与 SQLite / PostgreSQL 测试资产。
- 发布编排包含不可变版本、备份、显式迁移、独立验证和失败处置，生产动作与开发验收分开。
- Native 已区分共享测试、模拟设备、本地系统集成和正式分发证据。

## 2. 已确认的缺口与条件性风险

### R-01：Web 类型检查入口漏检

- [Client package.json](../../Frontend/radish.client/package.json) 的 `type-check` 为 `tsc --noEmit`；[根配置](../../Frontend/radish.client/tsconfig.json) 为 `files: []` 加 `references`。
- [Console package.json](../../Frontend/radish.console/package.json) 第一段同样为 `tsc --noEmit`；随后 strict 配置列出八个入口及其依赖，不等于全应用覆盖。
- 在两个 workspace 执行根配置的 `tsc --noEmit --diagnostics`，均得到 `Files: 0`。分别显式指定 `tsconfig.app.json` 后均通过。
- Console 的 [app 配置](../../Frontend/radish.console/tsconfig.app.json)仍为 `strict: false`，strict 扩面是独立于命令漏检的治理项。
- 两个应用的生产 build 使用 `tsc -b`，因此本次没有得出“生产构建完全没有类型保护”的结论。

影响：快速基线通过不能按原文档解释为 Client / Console 完整应用类型检查通过。修复候选应核对 app、node 与本地 / CI 覆盖，并验证错误能够触发失败。

参考：[TypeScript Project References](https://www.typescriptlang.org/docs/handbook/project-references.html)明确区分普通编译与 `--build` 对引用工程的处理。

### R-02：Outbox 缺少执行与提交的租约归属约束

- [ReliableOutboxJobs.cs](../../Radish.Api/Services/ReliableOutboxJobs.cs) 分派时使用五分钟租约，但入队参数只携带 source、message ID 和取消令牌。
- 执行阶段读取消息并检查 `Processing`，未匹配本次领取身份。
- [ReliableOutboxRepository.cs](../../Radish.Repository/ReliableOutboxRepository.cs) 领取时写入 `LockedBy / LockedAtUtc`，成功和失败更新仅按 ID 与 `Processing` 条件执行。

静态推导的场景：A 领取后排队或执行超过租约，B 接管同一消息；A 的迟到成功 / 失败结果仍可能修改 B 的状态。单实例排队延迟也可能触发租约交接，不只限于多实例。

本次确认保护缺口，没有执行迟到结果专项复现，也没有证明生产已出现重复业务扣款或数据损失。现有测试覆盖领取竞争和租约恢复，并不能替代“旧执行返回时新租约已存在”的测试。

### R-03：同源 Console 与可被 JavaScript 读取的令牌

- [Client tokenService](../../Frontend/radish.client/src/services/tokenService.ts) 与 [Console tokenService](../../Frontend/radish.console/src/services/tokenService.ts) 均把 access token / refresh token 持久化到 `localStorage`。
- 仓库默认 Gateway 拓扑使用 `/` 与 `/console` 承载两个应用。若实际同源，key 前缀仅防止数据混用，不构成浏览器隔离。
- 条件性影响：同源页面若出现可执行脚本注入，可能读取该 origin 内的管理端令牌。

本轮没有核实实际生产 origin、安全响应头或可利用 XSS。建议先评审信任边界，再比较独立管理端 origin、令牌持有方式和必要的服务端会话方案；未批准认证架构或部署变更。

参考：[OWASP HTML5 Security / Local Storage](https://cheatsheetseries.owasp.org/cheatsheets/HTML5_Security_Cheat_Sheet.html#local-storage)说明同源共享及 JavaScript 可读取存储的边界。

### R-04：Web 初始依赖负担与 Flutter 字体体积

本轮 Client 生产构建通过，产物事实如下（十进制单位）：

| 项目 | 结果 | 解释 |
| --- | --- | --- |
| `app-notification` JS | minified 约 `1,189 KB`，构建报告 gzip 约 `364 KB` | 超过配置的 `800 KB` 分块告警阈值 |
| 入口 HTML 的 JS 引用 | `20` 个 | 入口 module script 加显式 modulepreload，包含多个业务分块 |
| 这些引用的文件体积之和 | `2,789,369 bytes` | 不含 CSS、图片及后续动态请求 |
| 逐文件 gzip 估算之和 | `781,193 bytes` | 本地压缩估算，不等于生产实际传输量 |
| 两份 Flutter 中文字体源文件 | `42,898,364 bytes` | 字体源文件大小，不等于各平台最终包体增量 |

[BrowserAppRouter](../../Frontend/radish.client/src/bootstrap/BrowserAppRouter.tsx) 已有 `lazy()`；仍需核对[手工分块配置](../../Frontend/radish.client/vite.config.ts)与共享依赖在最终构建图中的关系，不能只凭源码包含动态导入判断按需加载成功。

本轮未测量浏览器请求时序、首屏时间或 LCP，也未归因具体依赖导致的全部体积。Flutter 字体优化需遵守已有中文区域子集、UGC 字符覆盖与回退契约，不按当前文案裁字。

### R-05：Flutter 验证未进入通用 CI

- [Repo Quality workflow](../../.github/workflows/repo-quality.yml)、[Candidate Quality workflow](../../.github/workflows/candidate-quality.yml)及[候选脚本](../../Scripts/validate-candidate.mjs)没有执行 Flutter analyze、test 或原生平台构建。
- Flutter 已有本地测试、平台构建及运行验收记录；本次不否定这些时点的证据。
- 影响：通用候选成功不能表述为所有正式产品线均已验证，Native 新改动仍较依赖本地手工选择检查。

建议评估共享 Dart 与各平台工程的分层触发、本地复现和成本，不要求每个提交执行全部平台或分发流程。

### R-06：维护热点与测试证据类型

对 `tests/*.test.ts` 的文件级统计：

| Workspace | 测试文件数 | 含 `readFile` 文本的文件数 |
| --- | --- | --- |
| Client | 90 | 44 |
| Console | 40 | 24 |
| UI | 11 | 7 |
| HTTP | 16 | 6 |

这是源码读取的粗粒度统计，不是纯静态测试占比。抽查存在通过源码片段和 CSS 文本判断契约的用例；它们适合静态约定，不能直接证明焦点、导航、用户切换、异步竞态和错误恢复行为。

超过 `1500` 行的代表文件：

| 文件 | 行数 |
| --- | ---: |
| [PostServiceTest.cs](../../Radish.Api.Tests/Services/PostServiceTest.cs) | 2721 |
| [InitialDataSeeder.Identity.cs](../../Radish.DbMigrate/InitialDataSeeder.Identity.cs) | 1890 |
| [ContentModerationCaseRepository.cs](../../Radish.Repository/ContentModerationCaseRepository.cs) | 1805 |
| [WikiDocumentService.cs](../../Radish.Service/WikiDocumentService.cs) | 1668 |

[PostService](../../Radish.Service/Posts/PostService.cs) 和 Wiki 服务还存在较多构造依赖及可选业务依赖。上述事实提示维护成本，不据此认定所有大文件或可选参数都是缺陷。治理应按真实职责和必要能力拆分，保留行为覆盖，不机械切片。

### R-07：入口状态漂移与集成规模

- 整理前 README 写“待 P8-B1”，路线图写“待 P8-B2”，当前规划写“进入 P8-C”；Flutter 专题也已确认 P8-B2 关闭。
- 整理前 `current.md` 为 `289` 行，含高密度完成流水和历史下一步，超过快速入口的实际阅读目的。
- `check:docs` 在整理前通过，说明文本卫生与链接检查不能识别语义过期。
- 本地 `master..dev` 为 `47` 个提交，三点差异为 `334` 个文件、`67,330` 行新增和 `33,365` 行删除。未刷新远程引用；这些是本地快照，不代表远端同步状态或建议立即发布。

后续文档整理已将历史流水迁入独立记录，路线图和 README 不再复制每个平台即时批次。完整功能批次的集成继续按既有规则，不要求等待所有 Native 平台完成。

## 3. 产品与规划建议的边界

- 保持社区内容、关系和复访主轴，明确每个新批次的用户收益与结束条件。
- 区分 Web 已发布后的维护、Native 平台基础、真实设备及分发成熟度，避免统一使用“已完成”掩盖层级差异。
- 平台 readiness / implementation / runtime acceptance 适用于风险较高的系统集成；普通低风险修正继续按既有简化流程处理。
- 不以审阅为由重启主动生产数据采集，也不阻断既有功能推进。
- 不全面迁移架构、ORM、路由或状态管理；维护热点随真实改动治理。

这些是后续候选评审依据，具体选择和实施范围以候选清单及项目所有者确认结果为准。

## 4. 本轮验证与可复现入口

| 验证 | 本轮结果 | 证据边界 |
| --- | --- | --- |
| `npm run check:docs` | 通过 | 整理前扫描 772 个文件，732 个 Markdown、455 个本地相对链接；不验证内容新鲜度 |
| `npm run validate:baseline:quick` | 通过 | HTTP `48`、UI `32`、Client `557`、Console `138`，共 `775` 项；类型入口限制见 R-01 |
| Client / Console 显式 app 配置检查 | 均通过 | 不是 Console 全量 strict，也不是全套 node 配置复核 |
| `npm run build --workspace=radish.client` | 通过，存在分块大小告警 | 包含 Client `tsc -b` 与 Vite build；体积见 R-04 |
| 后端 Outbox / Security 定向测试 | `60 passed / 0 failed / 0 skipped` | 不含 PostgreSQL 集成、后端全量或 R-02 新竞态专项 |

类型检查复现方式：在仓库现有依赖可用时，分别于两个 workspace 执行根配置 `tsc --noEmit --diagnostics`，再执行 `tsc -p tsconfig.app.json --noEmit`；或使用[验证手册](/guide/validation-baseline)的根目录补充命令。

后端命令：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~ReliableOutboxRepositoryTest|FullyQualifiedName~ReliableOutboxJobTest|FullyQualifiedName~ReliableOutboxServiceTest|FullyQualifiedName~Security' --verbosity quiet
```

体积复现方式：构建后读取 `Frontend/radish.client/dist/index.html`，收集其指向 `/assets/*.js` 的 script src 和 preload href，逐文件求字节数；本轮 gzip 估算使用 Python `gzip.compress`，版本或压缩参数变化可能造成小幅差异。

快速基线的测试监听和 MSBuild 本机进程通信首次受沙盒限制，获准在沙盒外重跑后通过；卡住的旧测试进程已精确终止。没有安装 / 更新依赖或启动项目服务。

未执行：生产访问、部署、完整依赖漏洞审计、后端全量、PostgreSQL 集成、Flutter 全量、浏览器 / 设备 Smoke 或视觉与线上性能验收。历史 Flutter 验收数字仅作已有记录引用，不计入本轮验证。

## 5. 文档整理结果

项目所有者随后要求依据审阅完善文档。本批统一入口职责，修正过期阶段口径，迁出旧流水，记录工程候选与验证限制；保留 P8-C 当前顺位、原有授权边界和生产采集冻结状态。

文档整理阶段额外执行验证手册列出的五条 `npm exec --no ... tsc` 补充命令，Client / Console 的 app、node 及 Console strict 配置均通过。文档检查、变更文本卫生与 `git diff --check` 通过；旧入口两节正文经逐字对比确认完整迁移。验证手册仍超过文档软篇幅建议，本批只订正其覆盖口径，未扩为整本手册重构。

本记录保存审阅时点的数值与结论；后续代码修复应形成新的验证记录，并更新候选状态。文档整理不关闭 R-01 至 R-06 的工程问题。
