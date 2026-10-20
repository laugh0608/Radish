# API 启动业务与 Gateway 停止日志治理记录（2026-10-15）

## 范围与结论

项目所有者确认治理 API 任务注册、固定文档启动同步与 Gateway 停止日志。本批收口 21 处旧调用点；契约见[第 72 节](../features/unified-logging-contract.md#72-l2-api-启动业务与-gateway-停止日志)。浏览器输出未扩展，生产候选开关保持 false，L2 未整体完成。

| 路径 | 数量 | 结果与观察点 |
| --- | --- | --- |
| API 周期任务注册 | 16 | 每次 AddOrUpdate 正常返回后输出 job.recurring_registration_returned，仅保留固定 jobKind |
| API 固定文档启动同步 | 4 | 两处跳过、完成和失败输出改为 document.builtin_sync_skipped / completed / failed |
| Gateway 关闭 | 1 | 删除 RunAsync 后的日志；旧 / 候选链各由一个 ApplicationStopped 回调输出 runtime.stopped |

新增四个业务事件及 runtime.program_unknown；复用 runtime.stopped。公共策略 jobKind 新增 outbox-dispatch，新增 builtInSyncSkipReason 枚举和七个数量键；createdCount / updatedCount / skippedCount 复用。四个共享样例同步验证 .NET / Node 契约，没有修改浏览器输出实现。

## 实现与边界

- [HostProgramLog](../../Radish.Extension.Log/HostProgramLog.cs) 只接收受控类别、数量和用于类型分类的异常；[HostProgramLogSink](../../Radish.Extension.Log/HostProgramLogSink.cs) 按精确来源 + 固定编码识别，在旧链代码 / 配置 WriteTo、AuditTo 与候选输出上重建。它不读取异常正文、原始载荷、路径、cron、时区、任务参数或自由文本原因；丢弃跨事件字段、错误类型、scope 和字符串关联字段，保留强类型 trace / span。
- 注册返回、同步跳过 / 完成与停止通知为非诊断 Information；同步失败为非致命 Error。已知事件受 MinimumLevel 控制，不依赖 Diagnostics；未知来源内编码生成 program_unknown，保留等级，低于 Warning 标为 diagnostic。相邻来源、独立 logger 和任意外部 enricher 的提前求值不自动受控。
- [ApiRecurringJobRegistration](../../Radish.Api/Services/ApiRecurringJobRegistration.cs) 承接原注册块，Program 在原位置调用；继续使用原 Hangfire 静态 AddOrUpdate、进程全局 manager / storage 和逐项顺序。不重新解析用于日志的配置，不记录 schedule、retention 或 batchSize，不新增参数钳制、验证、重试或 catch。已有框架 provider / Job 执行日志不由本批接管。
- 任务全开为 16 项；默认 15 项（DailyStats=false）。可选项全部关闭后仍有 Outbox、Chat 回应事实、上传会话与文件令牌四项。注册成功返回只表示调用返回，不代表任务已执行、worker 正常运行或后续周期成功。注册 / 配置失败继续向外传播，由 RuntimeProcess 生成最终失败。
- [WikiBuiltInDocumentStartupSync](../../Radish.Api/Services/WikiBuiltInDocumentStartupSync.cs) 承接原 try / catch 和 scope，Program 在原位置传入既有 options 与 logger。scope 创建、实体表名解析、展示开关读取与两次 IsAnyTable 参数求值顺序保持；即使展示开关关闭仍执行原查询。服务同步仍使用默认取消参数；DI / 查询 / 同步 / 取消 / scope 释放的既有 catch 边界保持。
- [WikiBuiltInSyncSkipReason](../../Radish.Model/WikiBuiltInSyncSkipReason.cs) 统一内部摘要与启动守卫的原因；不再拼接目录文本，也不按字符串前缀推断分支。六个具体原因映射固定日志值，None / 未定义值在跳过结果中仅映射 unknown。缺表原因对应的操作仍为执行 DbMigrate apply。只有内部启动消费者使用该摘要，无新增 HTTP 接口、数据库字段或迁移。
- 完成摘要只证明 SyncBuiltInDocumentsAsync 返回，随后 scope 释放仍可能失败并产生 failed。部分同步后失败不新增补偿或回滚；本批不修复既有写入事务和同步可靠性。失败事件正常输出后沿用继续启动；若 AuditTo 抛错仍可能传播并导致进程失败，不承诺日志介质绝不影响控制流。
- [RuntimeLoggingSession.AttachGatewayLifetime](../../Radish.Extension.Log/RuntimeLoggingSession.cs) 绑定唯一的 Gateway 停止回调。候选仍保留 started，旧链不增加 started；Auth / API 旧链生命周期所有权不扩展。回调注销由 Session 负责，logger 所有权保持旧链归宿主、候选归 Session；Stop 通知不代表随后的 Dispose 成功，也不代表进程已经以 0 退出。

## 只读发现与验证方法

实施前的隔离探针确认 WebApplication.RunAsync 委托至会释放宿主的 Host RunAsync；旧 UseSerilog 回调所建 logger 的 sink 随宿主释放，后续 Log.Information 未到达 sink，候选的外部 logger 在宿主 Dispose 后仍可写入。这是统一停止通知时机的依据；探针仅 HostBuilder Build / Dispose，无项目服务启动。

本批验证覆盖：

- 四模式的固定生成方法、受控枚举与数量、未知编码、scope 污染、跨事件字段、错误类型、不可读取异常 / 载荷、typed trace / span、配置 WriteTo / AuditTo 与候选应急输出。
- 真实 Hangfire 静态入口在独立 xUnit 子进程运行，避免全局 Lazy manager 污染其他测试。使用存储 / connection / transaction 替身，核对全部任务顺序、方法、参数、cron、UTC / 应用时区、默认及关闭条件；在 Commit 前确认未提前记录本项，注入第三项提交故障、非法 cron / 数值配置与日志介质故障。保留已返回项，后续注册中止，最终进程失败只由原边界负责。
- 启动同步使用真实 SQLite 内存表查询和 DI scope，加同步服务替身验证等待返回、跳过、失败、取消、部分副作用、依赖解析、查询及释放故障。正常完成输出的 AuditTo 失败仍进入原 catch；失败输出的 AuditTo 抛 AggregateException 继续传播。真实 Wiki 服务原有生成目录 / 内链 / 恢复 / 软删除回归，以及新增缺目录 / 空目录原因回归也执行。
- Gateway 使用受控 ApplicationStarted / Stopping / Stopped 通知及 HostBuilder Build / Dispose；核对唯一停止事件、先于旧 logger 释放、注销 / 无通知不输出，以及已收到 Stopped 后宿主释放仍失败的最终退出。未模拟真实 OS 信号、网络连接排空或真实停机竞态。

## 验证结果

| 检查 | 结果 |
| --- | --- |
| 全解决方案 `dotnet build Radish.slnx -c Debug --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false` | 通过，0 warning / 0 error |
| 日志 / 任务 / Wiki / 宿主定向回归 | 242 passed，0 failed / skipped，含本批 40 个新增测试实例和 4 个新增共享样例 |
| 进程退出 / CLI 定向回归 | 6 passed，0 failed / skipped |
| `npm run check:logging-contract` | 48 passed，0 failed |
| `npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check` | 通过；logging 指南 784 行、记录索引 322 行仍有篇幅软提醒 |
| `npm run lint:changed` | 无适用前端脚本 |
| 原任务注册块静态对照 | 去除日志调用、配置变量名差异与空白后，原块与提取后的块完全一致 |

.NET 主回归使用以下类过滤，统一参数为 `-noLogo -noColor -reporter verbose -longRunning 15`，外层超时 90 秒：

```text
Radish.Api.Tests.ErrorHandling.HostProgramLoggingTests
Radish.Api.Tests.ErrorHandling.ProgramDocumentSyncLoggingTests
Radish.Api.Tests.ErrorHandling.ProgramRecurringRegistrationTests
Radish.Api.Tests.Services.WikiBuiltInSyncStartupGuardTests
Radish.Api.Tests.Services.WikiDocumentBuiltInSyncTests
Radish.Api.Tests.RuntimeLog*
Radish.Api.Tests.CommonAssemblyDiagnostic*
Radish.Api.Tests.SerilogSelfLogTests
Radish.Api.Tests.ErrorHandling.HostStartupLoggingTests
Radish.Api.Tests.CleanupLoggingTests
Radish.Api.Tests.BusinessJobLoggingTests
Radish.Api.Tests.ServiceCleanupLoggingTests
Radish.Api.Tests.SeedLoggingTests
Radish.Api.Tests.DbMigrate.DbMigrateBootstrapTests
```

进程 / CLI 回归外层超时 60 秒，按方法过滤 ProducerLoggingTests 的 ProcessBoundary_ShouldKeepFailureNonzeroAndNeverRenderException、MigrationCommand_ShouldSeparateResultsAndPropagateFailures、HostingFailure_ShouldHaveOneFinalError、DoctorAndVerify_ShouldPreserveReportAndExitOutcome。

## 执行与证据限制

定向 .NET 测试通过现有 xUnit 可执行程序集执行，工作目录为 Radish.Api.Tests/bin/Debug/net10.0，外层 subprocess 设置超时；Hangfire 子进程自身上限 30 秒。项目依赖未安装 / 还原，未启动项目服务、Hangfire worker、容器或监听端口；SQLite 仅内存数据库，真实服务文件测试只使用并清理自己的临时目录。

真实 PostgreSQL / Hangfire 存储事务、定时任务执行、生产文档同步、系统停机和部署验收不在证据范围内。旧 Hangfire provider 等相邻来源没有因本批 HostProgramLog 的精确重建而自动获得 scope 保护，后续继续对照接线核对剩余边界。浏览器输出独立排期，L2 未整体关闭。
