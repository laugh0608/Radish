# 内容及表情最终日志输出核验与实施

日期为 2026-11-12（Asia/Shanghai），基线为 `dev` 的 `c15eb0a0`。先核验五个来源、13 个事件，随后所有者确认[第 10.13 节](../features/unified-logging-l3-ingest-plan.md#l2-content-sticker-output)并进入[本地实施](#implementation)。

## 核验范围

| 来源 | 实际入口和观测 |
| --- | --- |
| `CommentService` | 神评与沙发在缓存失效失败时保留已完成的写入调用和 `NoChange`，根页标识填充失败继续加载作者资料。编辑设置失败返回原失败元组。额外审计 sink 仅在内层重算输出时抛错，以触发真实外层 catch 和 `comment.highlight_recheck_failed` |
| `ContentSubmissionService` | 插入唯一冲突后回滚 savepoint、读取既有台账，再返回原 Processing |
| `ForumContentRevisionService` | 真实写入链触发更新阶段的 ArgumentException 和 InvalidOperationException，均回滚并保留原 409 及各自错误码 |
| `ReactionService` | 插入冲突选定一次重试，保留单次异常分类求值、两次写入尝试及最终响应 |
| `StickerService` | 批量附件第二项构建失败保留逐项结果和既有附件副作用。缩略图失败后流释放再失败只记录最终摘要。缓存读、写及失效失败保留既有降级 |

旧链由 `SerilogSetup.ConfigureLegacy` 接入配置 WriteTo / AuditTo，候选由 `RuntimeLoggingConfiguration.Configure` 输出 JSONL。两环境分别测试干净上下文、Serilog LogContext / MEL scope 的字段与关联污染、W3C Activity、诊断标记及相邻来源冲突。共享事件码另用真实 `CommentService` 编辑入口和外层 Controller 来源验证。

## 修复前证据

| 缺口 | 复现 |
| --- | --- |
| 静态来源误归 | 三个 Serilog 服务干净上下文无固定 SourceContext。注入相邻 `ProductService` 后，七个不同事件在三条路径被改写为 `runtime.unclassified` 并丢失失败分类，共 108 次。两个类型化来源仍保留自身身份 |
| 同名事件归属错误 | 外层 `CommentController` 使服务的 `comment.edit_failed` 进入 Controller 适配，8 次旧链观测的 SourceContext 为 Controller。候选 JSONL 保留同名事件码和失败分类 |
| 字段与关联污染 | 五个服务的正式旧链均输出合成私有载荷。候选 JSONL 裁掉该载荷，仍可继承 `statusCode`、`attempt` 及 `operationId`；无 Activity 时还接受字符串 trace / span。`post.restore_failed` 继承外层 `job` 分类 |
| 诊断误判 | 外层 `Diagnostic=true` 使旧链和 Development 候选误标。两种 scope 各使 Production 候选丢失 15 次实际触发，覆盖全部 13 个事件码 |

五个来源共跑 30 项输出故障探针。WriteTo 消费 sink 故障，候选 writer 产生有界 `pipeline.output_failed`。AuditTo 可让评论外层 catch 的再次输出也抛错并触发事务回滚调用、阻止台账返回已恢复结果或版本恢复返回预建 409、中断回应重试及阻止表情缓存回源。事务 fixture 和本地文件断言仅说明已覆盖的调用及副作用。

修复前严格构建通过，沙盒 96 项探针全部通过。54 组正式出口矩阵、12 组共享事件码探针合计 822 次事件观测。完整关联回归在沙盒于既有 SQLite 版本恢复用例无进展 30 秒后中止，相同程序集在宿主通过 213 项，0 失败、0 跳过。临时探针和合成报告保存在本机临时目录。

<a id="implementation"></a>

## 本地实施

- 三个 Serilog 服务绑定精确类型，回应和表情六个生成点使用命名 EventId。`ContentStickerLogSink` 在正式 WriteTo、AuditTo 和候选 JSONL 上按来源、身份及等级重建固定摘要，保留 `failureKind` 和类型化 W3C 关联。共享的 `comment.edit_failed` 分别由服务和已收口 Controller 的适配路径处理。见[事件契约第 88 节](../features/unified-logging-contract.md#l2-content-sticker-final-output)。
- 六组既有日志回归改接正式配置。新增覆盖实际业务入口、两种 scope、三条正式路径、代码 console、身份及字段拒绝、诊断开关和 AuditTo 故障。外层评论重算继续使用只让内层日志抛错的故障注入，以核对真实外层 catch。

### 实施验证

- 严格构建通过，0 警告、0 错误。沙盒 76 项定向回归通过，包含 24 项真实业务入口与正式出口、12 项服务与 Controller 同名事件归属、30 项输出故障和 10 项身份 / 字段 / 诊断及代码 console。
- 修复后的完整关联回归使用同一已编译程序集在宿主完成，393 项通过，0 失败、0 跳过。覆盖本批 76 项、六组既有业务与消费边界、Controller 最终输出，以及相邻的 Hub 生命周期、通知与实时推送适配。宿主运行沿用核验阶段的沙盒超时处理方式。
- 70 项日志契约检查、`npm run check:repo-quality:changed` 和 `git diff --check` 通过。事件契约和记录索引保留非阻断篇幅提醒。

定向复跑入口：

```bash
dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --warnaserror
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~ContentStickerOutputLoggingTests'
npm run check:logging-contract
```

真实数据库并发、文件介质、collector 和目标隔离部署按所属批次验收。生产候选开关保持关闭。
