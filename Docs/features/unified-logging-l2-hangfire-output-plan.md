# L2 Hangfire 最终输出边界收口方案

> 2026-11-04（Asia/Shanghai），**准备与现状复现完成，运行时改动待确认**。所有者要求提交功能优先规划，再明确 L2 下一批具体改动及验收范围；规划提交为 `3d104c8b5ee0b0355fda21aa11aeeb5640c6cdc9`。本页是[统一日志专题](./unified-logging-governance-design.md)的实施切片，不另设主线，也不涉及后置的 Hangfire 包来源 / Windows Razor 专项。

## 1. 已知缺口与本批选择

| 对象 | 本轮核对结果 | 推进边界 |
| --- | --- | --- |
| API Hangfire provider / 状态观察器 | 生成端不读取消息工厂 / 原异常正文；未设置两个精确 SourceContext，现有旧 / 候选链没有对应的逐事件重建 sink | **本批收口**：两个来源及三条最终出口 |
| 既有自有业务摘要 | 事件契约第 7–72 节保留各自准确范围；生成端受控不能据此推定全部最终输出都已重建 | 本批只回归相邻已治理来源；后续沿已登记调用链确认，不重做全仓调查 |
| 浏览器共享 HTTP / Client / Console | 三份 sanitizer 均保留裸字符串、Error.message / stack；共享 HTTP 直接输出错误 message，两个 logger 的 message / tag 等未统一经过安全边界 | 后续独立前端批次，涉及三级 / diagnostic 和调用契约，先确认方案 |
| 未注册或外部非日志通道 | 原文已明确 OpenIddict 部分组件未注册、外部 EventPipe / DiagnosticListener 等不在现有控制范围 | 保留限制，不为任意外部订阅器增加本批前置任务 |
| 集中入库、Console 查询 / 告警、迁移 | 属于 L3–L6 的明确未完成能力 | 本批不改表、API、页面、Compose 或生产开关 |

当前范围选定的是实际 API 接线中的一个最终输出缺口。完成后只关闭这一切片，不宣称 L2 整体或所有进程输出已经完成；也不重跑依赖源码重建来验证日志安全。

## 2. 现状证据

源码基线为上述完整提交。`Radish.Api/Program.cs` 的 AddHangfire 回调在存储选择及 serializer 配置后调用 `HangfireRuntimeLogging.Configure`；原 `HangfireRuntimeStateFilter` 观察 AutomaticRetry 选举后的候选结果，provider 抑制 AutomaticRetry 重复日志，不求值框架消息工厂。

`SerilogSetup.ConfigureLegacy` 已在 root enrichment / LogContext 合并后，分别包裹代码 / 配置 WriteTo 和配置 AuditTo；`RuntimeLoggingConfiguration.Configure` 对候选输出也有重建链。两者已含 HostProgram 等精确来源，但没有 Hangfire provider / 状态观察器对应的识别及字段重建。`RuntimeSerilogSink` 的公共策略会剔除未知 / 不安全内容，却允许登记过的通用字段，不能代替来源与事件自己的白名单。

### 本轮真实执行

- 使用现有 SDK `10.0.300` / runtime `10.0.8`，Release `--no-restore --warnaserror` 构建 Tests 及其引用工程，**0 warning / 0 error**。
- `CleanupLoggingTests / HostProgramLoggingTests / ProgramRecurringRegistrationTests` 共 **60 passed / 0 failed / 0 skipped**。旧 Capture 中 provider 检查不带污染 scope；原状态选举与注册回归通过不能关闭新边界。
- 只读探针引用本次构建输出，调用真实 provider、真实 ConfigureLegacy / candidate 接线和配置内存 sink。Development / Production × 旧 WriteTo / AuditTo、候选 WriteTo，共 6 条模式 / 出口组合；各执行 Error / Warning / Info 共 **18 个观察场景**，消息工厂调用均为 0，应急输出为空。
- 旧链 12 个场景均在最终配置内存 sink 收到合成私密标记、环境 operationId 与无关 jobKind。探针显式将旧 MinimumLevel 设为 Verbose 以观察诊断；Warning / Error 的缺口不依赖放开该阈值。不声称已观测真实生产凭据或实际文件 / 数据库持久化。
- 候选 Development 三个场景与 Production 的 Warning / Error 共 5 个事件没有自由文本标记，但带入了 provider 自身未产生的 operationId / jobKind。候选 Production 的诊断事件仍按既有策略丢弃。说明公共全局字段白名单能去除自由内容，却不能阻止跨来源合法字段混入。
- 三份浏览器 sanitizer 的既有测试 **9 / 9** 通过；另 9 个受控样本确认敏感键被替换，但裸字符串和 Error 文本仍保留标记。这是 sanitizer 函数观察，不是浏览器真实 Smoke，也不作为本批后端修改范围。

原始日志、探针源码 / 命令 / 输出、TRX 与输入 hash 目录为 `artifacts/logging-l2-readiness/2026-11-04/`。独立探针最初裸 csc 因省略 SDK 的程序集统一设置而报 CS1701，随后改用无 PackageReference 的 SDK 工程，零包恢复、严格构建通过；未增加任何产品 NoWarn 或修改 SDK 策略。临时 SDK 工程和测试目录中的探针 DLL 已清理。

本地总索引覆盖 15 文件（不含自身），SHA256 为 `cb795b9f678c0be54d4a8340dee4ec28850e30bd4007117216788ed1adb9a24c`；17 份相关源码 / 配置均与基线 commit 逐字节核对，编译引用 DLL 的 hash 单独保存。归档已回读核验，文档 / 链接及 `git diff --check` 通过。

## 3. 下一批具体改动

### 3.1 精确来源与生成端

在 `Radish.Api/Services/HangfireRuntimeLogging.cs` 为下列两条路径显式绑定完整 SourceContext，复用现有 ForContext 与分类逻辑，不新增替代日志 provider：

- provider：`Radish.Api.Services.HangfireRuntimeLogProvider`；第三方传入的 logger 名称不进入输出。
- 状态观察器：`Radish.Api.Services.HangfireRuntimeStateFilter`；继续仅观察既有候选状态。

两个来源标识由新 `Radish.Extension.Log/HangfireLogSink.cs` 的具名常量维护，API 复用既有 Extension.Log 引用；Extension.Log 不反向依赖 API，不增加 Common 业务依赖或专门的转发层。

### 3.2 逐事件字段与输出重建

新增 `HangfireLogSink`，仅识别上述两个完整来源。没有匹配的相邻来源原样交给下一层，不能按 `Hangfire.*` 名称前缀接管其他输出。

| 来源 / 事件 | 本批允许保留 | 必须剔除 |
| --- | --- | --- |
| provider：`hangfire.runtime_warning / hangfire.runtime_failed` | 已登记 failureKind（可缺省） | operationId、jobKind、count、outcome 及所有原始 / 环境字段 |
| provider：`hangfire.diagnostic` | 已登记 failureKind（可缺省），显式 diagnostic | 任意消息工厂结果、第三方 logger 名称和环境载荷 |
| 状态观察器：`job.retrying / job.failed` | failureKind、与事件一致的 outcome、生产者生成且格式正确的 operationId | 原 Job ID / 类型 / 参数、任务异常、其他事件的字段与 scope |
| 精确来源的未知 / 缺失编码 | provider → `hangfire.runtime_unknown`；状态观察器 → `job.state_unknown` | 全部业务扩展属性和原始内容 |

重建固定模板、固定 sourceCategory=job 及精确 SourceContext，保留原时间与原 Serilog 等级；Exception 不透传。trace / span 只采用 LogEvent 强类型值或现有 Activity 回退，不能从任意字符串字段恢复。各已知事件按自己的字段集合、类型、枚举及格式校验，不能直接复用整个公共属性集合。

已知诊断与未知低于 Warning 的事件标为 diagnostic；公共候选策略继续按 Development / Production、Diagnostics 和 MinimumLevel 决定实际输出。旧链继续遵守原 MinimumLevel，不新增全局环境过滤；Fatal 继续在候选统一为 Error / isFatal，旧链保留原等级。

状态事件的 operationId 维持原 SHA-256 派生规则；provider 自身不产生 operationId，因此最终输出必须删除环境补入的同名值。源标识用于已登记生产路径的输出重建，不宣称能够认证进程内任意恶意代码的身份；生产者显式字段不能由普通 LogContext 覆盖，输出重建拒绝无关 enrichment 属性，任意外部 enricher 的提前求值不纳入保证。

### 3.3 三条出口接线及文件范围

1. `SerilogSetup.cs`：把新 sink 纳入旧链包裹范围，覆盖代码 / 配置 WriteTo 与配置 AuditTo；保留原顺序、消费异常 / 审计传播语义和释放所有权。
2. `RuntimeLoggingConfiguration.cs`：候选 RuntimeSerilogSink 前纳入同一重建 sink；不启用 RadishLogging，不读取业务配置、连接或任务参数。
3. `Radish.Common/LogTool/Contracts/runtime-log-policy.v1.json`：仅登记两个 unknown 固定编码 / 模板，不扩展自由属性或修改已有编码语义。
4. 新增 `Radish.Api.Tests/ErrorHandling/HangfireOutputLoggingTests.cs`，复用现有配置内存 sink / 存储替身能力验证实际接线；按需扩展原 CleanupLoggingTests 的回归，不机械复制生成端断言。
5. 共享 fixture 按新增 unknown 编码补齐 .NET / Node 一致性样本；实施后同步[事件契约](./unified-logging-contract.md)及本页状态。

不趁本批重构已有 sink 链结构、调整第三方版本、变更业务返回 / API、调度器、重试次数 / 延迟、Job 状态 / 事务、存储 / Dashboard、根许可或第三方源码材料。

## 4. 验收范围与结束条件

| 验收面 | 必须证明 |
| --- | --- |
| 两种模式 / 两条链 | Development / Production × 旧 / 候选；候选 diagnostics 开 / 关及 Production 拒绝诊断继续有效 |
| 实际三条出口 | 通过真实 ConfigureLegacy 分别捕获 WriteTo / AuditTo，通过 Configure 捕获候选；不能仅手工包裹新 sink 后宣称生产接线通过 |
| 生成端语义 | 消息工厂与原异常 Message / StackTrace / ToString 的抛错 getter 不被读取；AutomaticRetry 不重复；null messageFunc 的级别查询与原返回行为保留 |
| 上下文污染 | LogContext 与 root enricher 附加合成文本 / 嵌套值 / 合法跨事件字段、SourceContext / EventCode 污染；最终仅存在本来源本事件允许字段 |
| 关联与降级 | provider 无环境 operationId；状态观察器保留真实派生 ID；强类型 trace/span、字段缺失 / 错型 / 非法枚举、未知事件及相邻来源有明确结果 |
| 原状态与故障 | 沿用实际 AutomaticRetry 决定的六组边界，普通成功 / 停机重入队安静；WriteTo 消费与 AuditTo 传播、候选应急输出、logger dispose 行为保持 |
| 相邻回归 | HostProgram / HostStartup 精确重建及任务注册回归不受新接线影响，已有受控输出不重复或丢失 |

实现后执行以下命令；仅使用已有 SDK / 已恢复依赖，不安装包或启动宿主 / worker / 数据库：

```bash
dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj -c Debug --no-restore --warnaserror -p:UseSharedCompilation=false
dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj -c Release --no-restore --warnaserror -p:UseSharedCompilation=false
```

双配置均运行以下 filter，保存独立 TRX，逐项确认新场景执行，不预先承诺未形成的用例数量：

```text
FullyQualifiedName~HangfireOutputLoggingTests|FullyQualifiedName~CleanupLoggingTests|FullyQualifiedName~HostProgramLoggingTests|FullyQualifiedName~HostStartupLoggingTests|FullyQualifiedName~ProgramRecurringRegistrationTests|FullyQualifiedName~RuntimeLoggingAdapterTests|FullyQualifiedName~RuntimeLogPolicyTests
```

执行 `dotnet test ... -c <配置> --no-build --no-restore --filter '<上述 filter>' --logger 'trx;LogFileName=<配置>.trx' --results-directory <本批证据目录>`，再执行 `npm run check:logging-contract`、`npm run check:repo-quality:changed` 和 `git diff --check`。

本批退出条件为：两个来源 / 三条出口的明确字段白名单和语义保持均有测试，已复现的 scope 文本及跨事件字段不再进入受控最终事件，双配置 / 共享契约与相邻回归通过，文档和清理一致。不把本轮只读复现写作修复通过，不将内存 sink 证明扩大为真实文件 / 数据库写入或部署验收。

## 5. 授权与后续

本次已完成规划提交、源码核对、本机既有回归与只读复现、下一批方案；尚未实施上述运行时改动。按仓库约定，下一步只需确认本页精确实现与本地验收范围；不包含依赖安装、服务 / 容器启动、远端工作流、外部询证、发布或部署。

该切片通过后，回到现有 L2 清单确认剩余自有来源与前端输出的有限批次，并向 L3–L6 可用能力推进；未注册 / 外部任意通道继续按既有边界记录，不扩大为无限调查。
