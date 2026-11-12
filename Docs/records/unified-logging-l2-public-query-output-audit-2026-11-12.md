# 公开查询与报表最终日志输出核验与实施

日期为 2026-11-12（Asia/Shanghai），基线为 `dev` 的 `edd47101`。先核验并形成[第 10.10 节修复方案](../features/unified-logging-l3-ingest-plan.md#l2-public-query-output)，随后所有者批准并完成[本地实施](#implementation)。

## 实际入口

| 来源 | 触发分支 |
| --- | --- |
| `PublicHeadSnapshotService` | 经公开 head Controller 触发缓存读、写故障 |
| `PublicSitemapService` | 经 sitemap Controller 触发缓存读、写故障，调用服务触发生成失败和单栏目计数失败 |
| `PublicHeadSnapshotClient` | 快照与前端 HTML 的非成功状态、HTTP 请求异常与取消 |
| `PublicHeadSnapshotMiddleware` | 实际注入后的响应写入失败，进入后续管道 |

临时探针使用真实服务、客户端和中间件、mock 缓存 / 仓储、内存 HttpClient handler 与响应流。旧链调用 `SerilogSetup.ConfigureLegacy`，分别经配置 WriteTo 和 AuditTo 捕获最终 `LogEvent`；候选经 `RuntimeLoggingConfiguration.Configure` 捕获最终 JSONL。两个宿主均覆盖 Development / Production。每条路线执行 clean，以及 Serilog LogContext / MEL scope 各自的字段污染、带 W3C Activity 的字段污染、`Diagnostic=true` 和相邻来源四类场景。全部注入值为合成数据。

## 复现结果

| 场景 | 正式旧链 WriteTo / AuditTo | 候选 JSONL |
| --- | --- | --- |
| 无外层 scope | 11 个事件均保持原码、等级和生成字段。API 六个事件无 `SourceContext`，Gateway 五个事件已有类型化来源 | 11 个事件均保持原码、等级和生成字段 |
| 外层字段，无 Activity | 11 个事件均继承私有载荷、`attempt` 和伪造的 trace / span / operation ID。九个失败事件继承无关 `statusCode`，两个状态事件继承无关 `failureKind` | 私有载荷被裁掉。11 个事件仍继承合法形状的关联值及 `attempt`，并混入同样的无关状态码或失败分类 |
| 外层字段，有 W3C Activity | 11 个事件都有真实类型化 trace / span，属性中仍同时含外层同名关联字符串 | 真实 trace / span 优先，外层 `operationId` 和无关字段仍在 |
| 外层 `Diagnostic=true` | 两环境均输出 11 个事件，全部误带诊断标记 | Development 显式诊断时均误带标记；Production 对两种 scope 各丢弃全部 11 个事件 |
| 外层 `SourceContext=Radish.Service.ProductService` | API 六个事件被相邻适配器接管，改为 `runtime.unclassified` 并丢失原失败字段。Gateway 五个事件保留身份 | 两环境复现同样的 API 六个事件误归 |

两种 scope 的结论一致。无相邻来源干扰时，生成点已有的状态或失败分类能保留，缺口来自未按事件隔离合并后的字段和诊断标记。API 的六个事件另有未绑定生成来源的问题。缓存回退及 TTL、上次成功 XML、部分索引、Gateway 404 安静和后续管道均通过探针断言。

## 修复前验证与边界

- `dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --warnaserror` 通过，0 警告、0 错误。
- 沙盒单跑临时探针 54 组全部通过，捕获和复核 594 条事件观测，含 72 次 API 误归及 22 次生产候选丢失。探针通过表示观测已完成。
- 初次混跑关联回归时，沙盒在记录为 `LeaderboardLoggingTests.BusinessFailures_ShouldKeep4xxQuietAnd5xxOwnedByApi` 的用例处 30 秒无进展后中止，保留 sequence，未采集转储。随后用相同程序集在宿主环境完成 128 项既有关联回归，0 失败、0 跳过。范围含公开 head / sitemap Service、Controller、Gateway 及公开元数据、公开发现流、排行榜、统计日志测试。沙盒等待原因未确定。
- 发现流的 503、排行榜的异常传播及统计报表的既有 500 包装通过 API 最终异常归属回归。三者进入关联防回归范围。
- 临时探针与报告保存在本机临时目录。核验轮使用捕获器接在正式适配出口，输出故障注入由实施轮补齐。

<a id="implementation"></a>

## 本地实施

- 两个 API 服务固定类型来源。Gateway 使用命名 EventId 表达四个客户端故障和一个注入故障。`PublicMetadataLogSink` 接入旧链代码 / 配置 WriteTo、AuditTo 和候选 JSONL，统一生成固定摘要与逐事件字段。正式输出契约见[第 85 节](../features/unified-logging-contract.md#l2-public-query-final-output)。
- 原公开元数据测试从直接 formatter 改接正式配置，结构化断言覆盖无 Activity 与 W3C Activity、Serilog / MEL 外层上下文、源和字段污染、两种环境的各出口及业务结果。额外覆盖未知 / 错配身份、非法字段、代码 console、候选诊断关闭及原 Serilog 时间。
- 输出故障实测中，WriteTo 消费 sink 故障，候选 writer 故障产生单条有界 `pipeline.output_failed`，两者保留业务返回。AuditTo 传播原 sink 故障，可在 head 缓存读失败时阻止回退生成，在 sitemap 生成失败时阻止回退 XML，在 Gateway 请求或注入故障时阻止返回 null 或调用后续管道。

### 实施验证

- 严格构建通过，0 警告、0 错误。沙盒 250 项定向回归通过，包含业务真实入口的正式出口矩阵、错配事件、诊断门禁和 24 项输出故障用例。
- 完整关联回归在沙盒内再次于既有 `LeaderboardLoggingTests.BusinessFailures_ShouldKeep4xxQuietAnd5xxOwnedByApi` 用例处 30 秒无进展后中止，保留 sequence，未采集转储。相同程序集在宿主完成 479 项回归，0 失败、0 跳过。范围含完整公开元数据 274 项、API 查询 / 统计与直接消费者、Gateway、共享输出适配及邻接的商城和奖励适配。沙盒等待原因未确定。
- 70 项日志契约检查、`npm run check:repo-quality:changed` 及 `git diff --check` 通过。事件契约与记录索引保留篇幅建议提醒。

公开元数据复跑入口：

```bash
dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --warnaserror
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~PublicMetadataLoggingTests'
npm run check:logging-contract
```

真实文件 / DB sink、外部缓存和网络、collector 及隔离部署环境仍需相应批次证据。生产开关保持关闭。
