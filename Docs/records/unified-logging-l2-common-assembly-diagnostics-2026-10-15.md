# Common 程序集加载与类型枚举诊断治理记录（2026-10-15）

## 范围与结论

项目所有者确认治理 Common 两处程序集诊断、早期来源绑定及诊断二次失败。本批完成实现和定向验证；契约见[第 71 节](../features/unified-logging-contract.md#71-l2-common-程序集加载与类型枚举诊断)。浏览器输出未扩展，生产候选开关保持 false，L2 未整体完成。

| 路径 | 原行为 | 本批结果 |
| --- | --- | --- |
| RuntimeExtension.GetAllAssemblies | 扫描基目录中前缀匹配 Radish.Api 的 DLL；加载失败输出文件名 / 异常正文到 stdout 后跳过 | 同一 catch 改为 `runtime.assembly_load_failed`，保留跳过行为 |
| App.GetTypes | 枚举失败输出程序集全名到彩色 stdout，返回空集合 | 同一 catch 改为 `runtime.assembly_types_failed`，不读取 FullName / LoaderExceptions，仍丢弃部分类型 |
| 早期 App 初始化 | 配置选项、缓存等路径可能在主 logger 建立前触发静态扫描；诊断输出失败可使 App 此后持续初始化失败 | 独立 reporter 在主 logger 建立前可用；其内部失败不再使 App 初始化失败 |

仅两个 catch 的生成端替换；Common 不依赖 Extension 或业务层。入口接线只在 RuntimeProcess 增加来源作用域，四个产品入口复用该接线。

## 实现

- [AssemblyDiagnosticReporter](../../Radish.Common/LogTool/AssemblyDiagnosticReporter.cs) 使用固定事件、Error、非 diagnostic、infrastructure 分类及受控 failureKind；不读取名称、路径、异常正文、部分类型、scope 或自由文本关联字段。
- 独立复用 RuntimeLogPolicy / RuntimeLogOutput 写 stderr，不经 Serilog、文件或数据库 sink。模式固定 Production，表示通道策略；不受候选开关、宿主环境、主链等级或 Diagnostics 开关影响。
- [RuntimeProcess](../../Radish.Extension.Log/RuntimeProcess.cs) 在宿主委托前绑定 api / auth / gateway / dbmigrate。未绑定、未知绑定值或已无存活祖先时使用新增 runtime 来源；bootstrap / process-进程号 / unversioned 元数据不读取配置。runtime 不代表新增可部署服务。
- 作用域使用 AsyncLocal，支持嵌套、乱序释放、并发隔离和继承已释放上下文后的祖先 / 未绑定回退，不拥有 writer。输出惰性创建；默认 Console.Error 介质变更后重建输出实例；已开始的写入不因释放取消。
- 正常事件每个 catch 一条。诊断自身的初始化、分类、序列化与输出失败被隔离；writer 的同步或继承执行上下文的重入直接返回，不递归或补发。本通道串行写完整 JSON 行，不承诺任意 writer 的 IO 超时。
- 输出失败复用 pipeline.output_failed 的 60 秒有限应急尝试；应急介质仍为 stderr。策略资源或应急介质也失效时可能没有输出；不回退原文、不持久化、不承诺恰好一次。作用域 / 介质输出实例变化会重置其应急计数窗口。
- 公共策略新增两个事件和 runtime 服务标识，无新增属性；新增两个跨 .NET / Node 样例和一个 Node runtime 来源检查。Node 只验证共享契约，没有修改浏览器输出实现。

## 保持与变化

保持原目录、前缀筛选、Default 加载上下文、即时程序集列表与延迟 EffectiveTypes；重复枚举仍会重复诊断。ReflectionTypeLoadException 的部分类型仍不回收，不新增容错加载或重试。

Directory.GetFiles、前缀筛选、公共类型过滤、直接 DefinedTypes / LoadFromAssemblyName 和独立选项扫描的既有异常边界不扩大。RuntimeProcess 保持正常退出 0、未处理失败退出 1、HostAbortedException 向工具传播。DbMigrate doctor / verify / help 的 stdout 结果保留。

有意变化是诊断二次故障不再替代既有跳过 / 空集合行为，不再访问故障元数据或控制台颜色；ConsoleHelper 其他通用能力不修改。正常日志覆盖不代表所有反射 / 程序集故障均被消费或宿主一定可用。

## 验证结果

| 检查 | 结果 |
| --- | --- |
| 全解决方案 `dotnet build Radish.slnx -c Debug --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false` | 通过，0 warning / 0 error |
| 日志与宿主定向回归 | 132 passed，0 failed / skipped，包含 19 个本批用例及两个新增共享样例 |
| 进程退出 / CLI 定向回归 | 6 passed，0 failed / skipped |
| `npm run check:logging-contract` | 44 passed |
| `npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check` | 通过；logging 指南 784 行、记录索引 320 行有既有篇幅软提醒 |
| `npm run lint:changed` | 无适用前端脚本；新增 Node 契约测试已由上方检查执行 |

本批 19 个用例覆盖四模式、配置读取失败前报告、嵌套 / 乱序释放 / 并发 / 已释放上下文、未知来源、不可读取异常、Write / Flush 故障与恢复、固定应急事件、同步 / 异步重入、最终错误与工具中止、真实 CLI help 输出、延迟类型枚举、部分类型丢弃和原有向外传播边界。

其中一个用例启动五个独立 xUnit 测试进程：正常输出、Write 失败、Flush 失败、重入及策略资源损坏。临时目录包含匹配前缀的畸形 DLL、非匹配畸形 DLL 和有效程序集副本；实际调用扫描与 App 静态初始化，确认失败后 App 属性仍可访问。策略损坏只改动临时 DLL 副本内的 JSON 资源；全部临时目录在 finally 清理，不修改仓库程序集。类型枚举使用受控 Assembly 替身，不能据此声称遇到了真实发布依赖损坏。

执行目录为 `Radish.Api.Tests/bin/Debug/net10.0`，外层 subprocess 分别设置 90 / 60 秒超时；独立测试子进程单次 20 秒上限：

```bash
dotnet Radish.Api.Tests.dll -noLogo -noColor -reporter verbose -longRunning 15 \
  -class 'Radish.Api.Tests.CommonAssemblyDiagnostic*' \
  -class 'Radish.Api.Tests.RuntimeLog*' \
  -class 'Radish.Api.Tests.SerilogSelfLogTests' \
  -class 'Radish.Api.Tests.ErrorHandling.HostStartupLoggingTests' \
  -class 'Radish.Api.Tests.SeedLoggingTests' \
  -class 'Radish.Api.Tests.DbMigrate.DbMigrateBootstrapTests'

dotnet Radish.Api.Tests.dll -noLogo -noColor -reporter verbose -longRunning 15 \
  -method 'Radish.Api.Tests.ProducerLoggingTests.ProcessBoundary_ShouldKeepFailureNonzeroAndNeverRenderException' \
  -method 'Radish.Api.Tests.ProducerLoggingTests.MigrationCommand_ShouldSeparateResultsAndPropagateFailures' \
  -method 'Radish.Api.Tests.ProducerLoggingTests.HostingFailure_ShouldHaveOneFinalError' \
  -method 'Radish.Api.Tests.ProducerLoggingTests.DoctorAndVerify_ShouldPreserveReportAndExitOutcome'
```

验证全部在沙盒中完成。宿主故障回归使用立即失败的内存 hosted service；DbMigrate bootstrap 仅 Build / Dispose。未安装依赖、启动项目服务 / 容器、绑定端口或访问真实数据库、密钥库与系统证书库。

## 后续边界

Common 两处既有 catch 已收口；不调整服务定位器、程序集发现架构或其他直接类型枚举的异常消费策略。剩余 Program 自有业务 / 关闭日志及非 Serilog 通道继续按来源只读确认下一批范围；浏览器输出独立排期，本批未实施。
