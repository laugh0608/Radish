# 日志 L2：SelfLog 独立自诊断治理

> 日期：2026-10-15（Asia/Shanghai）。本批在自有 Auth 日志提交 `9655b716` 后开展；项目所有者明确授权 SelfLog 实施、定向验证与文档同步，DataProtection 和 Auth 启动日志不进入实现。

## 问题与实现

原旧链在配置 logger 时启用每日 `SerilogDebug/Serilog*.txt` 原文追加文件，没有本组件的容量或 writer 释放管理；候选链把所有 SelfLog 统一转为 `pipeline.output_failed`，且在 CreateLogger 后才注册、Logger.Dispose 前已禁用，初始化和关闭诊断存在覆盖缺口。

现在 `RuntimeLoggingSession` 在读取自身日志配置前注册 `SerilogSelfLogSession`，旧链与候选链共享这一个注册入口。三个 Web 宿主和 DbMigrate 已有 Session 作用域直接继承；旧链宿主拥有 logger，候选 Session 拥有 logger，释放顺序保持不变。旧文件停止创建和追加，不删除或迁移历史证据。

| 项目 | 本批行为 |
| --- | --- |
| 事件 | 新增 `pipeline.self_diagnostic`，固定 Warning，非 diagnostic，SourceCategory=pipeline |
| 内容 | 只含 `count`；原消息、异常正文、换行载荷、scope 与业务身份不进入输出 |
| 来源 | deploymentId=bootstrap、调用方的宿主 service、`instanceId=process-<pid>`、release=unversioned；不依赖配置来源字段或机器名格式 |
| 模式 | 宿主 Development 对应 Development；其余宿主环境对应 Production；独立于主链 Mode / MinimumLevel / Diagnostics / Enabled |
| 介质 | 默认 stderr；不进入应用文件、数据库或 Serilog 管道；可注入 writer 做定向验证 |
| 已确认输出失败 | `RuntimeLogOutput` / `RuntimeSerilogSink` 继续使用 `pipeline.output_failed` Error，限频与计数独立 |

SelfLog 没有可信结构化等级或产生它的具体 logger 身份，不能从一条自诊断推导出业务失败、丢失日志、原始异常责任或原始严重程度。`count` 是本会话累计收到的回调次数，不是业务请求数或丢失事件数。Serilog 可能在进入回调前已经格式化原文，本批不阻止上游求值，也不保留原文供后续恢复。

## 限频与故障语义

- 首次立即报告；随后每个会话每 60 秒至多尝试一次。单调时钟控制窗口，计数上限为 9007199254740991；下一次回调触发窗口检查，没有计时器或后台任务。
- writer 与 Flush 成功后扣除本次报告计数。失败不调用 Serilog，不抛出替代业务异常，保留计数供后续窗口重试。部分写入或 Flush 失败可能造成计数重复，不能宣称恰好一次。
- 写入期间发生同线程 SelfLog 重入时只累计，不再次写入；下一次窗口保留这部分计数。并发回调串行计数和输出。writer 是同步调用，限频不提供 IO 超时或业务线程永不阻塞的保证。
- 退出不强制冲刷，未报告计数只在内存中；应急介质持续不可写或没有后续回调时，不承诺最终一定输出。既有统一事件大小上限仍生效。
- 普通 WriteTo 继续消费 sink 异常并触发 SelfLog；AuditTo 仍可抛出 AggregateException。配置构建失败和 sink 释放失败仍向调用方传播；自诊断不改变原操作的成功或失败结果。

## 生命周期与所有权

1. Session 先注册自诊断，再读取日志配置和创建候选 logger；旧链配置与 logger 创建发生在同一个存活 Session 内。
2. Session 构造失败时释放自己的注册，原异常原样传播；正常退出先释放宿主 / logger，再注销自诊断，finally 保障失败路径的注销。
3. SelfLog 是进程全局通道，最新存活会话接收回调，退出后恢复最近仍存活的会话。旧会话提前 Dispose 不禁用新会话，也不恢复已经释放的接收方；最后一个会话释放后禁用。
4. 该所有权仅处理本组件注册；外部自行调用 `SelfLog.Enable/Disable` 会绕过它，不通过反射恢复未知外部回调。事件无法自动归属多个重叠 logger 的真实发起方。业务 `Log.Logger` 仍遵循原有单宿主作用域约定，本批没有增加多宿主全局 logger 调度能力。

接管期从 RuntimeLoggingSession 构造开始；宿主最初加载配置之前、Session 最终释放之后以及不使用该入口的外部 logger 不在覆盖范围。候选链固有 sink 不产生可注入的释放诊断；其释放接线通过代码复核及真实 Session / Host Dispose 回归核对，释放诊断与释放失败的真实生成证据来自旧链可配置 sink，不能表述为候选 sink 已被强制触发同类故障。

## 验证

| 检查 | 结果 |
| --- | --- |
| 全解决方案 `dotnet build Radish.slnx -c Debug --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false` | 通过，0 warning / 0 error |
| 新增 `SerilogSelfLogTests` | 30 个用例通过 |
| 新增用例 + RuntimeLog 系列 + Health / OpenIddict LoggerFactory 回归 | 77 个通过，0 failed / skipped |
| `npm run check:logging-contract` | 33 个通过，含新增跨 .NET / Node 共享样本 |
| `npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check` | 通过 |

定向回归覆盖旧链 / 候选链 × Development / Production、主等级 Error / Fatal 与 diagnostics=false、会话配置读取前的回调、构造失败恢复接收方、真实 HostBuilder Build / Dispose、真实旧链配置 sink 创建 / 失败 / 释放 / 释放失败、WriteTo / AuditTo 异常语义、实际输出失败与自诊断并存、200 次并发回调、59 / 60 秒边界、写入 / Flush 失败、写入重入和交错释放。测试对旧 SelfLog 目录文件大小与修改时间做前后核对，确认未新增或改写原文文件。

执行入口为测试输出目录中的 xUnit 进程内 runner，外层 subprocess 设置 90 秒超时：

```bash
dotnet Radish.Api.Tests.dll -noLogo -noColor -reporter verbose -longRunning 15 \
  -class 'Radish.Api.Tests.SerilogSelfLogTests' \
  -class 'Radish.Api.Tests.RuntimeLog*' \
  -class 'Radish.Api.Tests.ErrorHandling.HealthLoggerFactoryTests' \
  -class 'Radish.Api.Tests.ErrorHandling.OpenIddictLoggerFactoryTests'
```

本批无依赖安装或还原、服务 / 容器启动、端口绑定、真实数据库或系统证书库访问。证据不替代真实磁盘耗尽、跨进程采集、生产部署、完整认证或应用退出故障验收。

## 前置只读核对与后续边界

- OpenIddict 7.2.0 的 Client / Validation / DataProtection 包被传递引入；当前 Auth 为 Core + Server + ASP.NET Core，API 为 Core + JwtBearer，没有注册这些包对应的 Client / Validation / DataProtection 能力。
- ASP.NET DataProtection 实际用于 Cookie 等功能。安装版本 10.0.8 的程序集元数据确认 XmlKeyManager 等来源存在 XML 元素、路径与异常事件，既有精确日志适配器未覆盖；本批没有实施。
- 已安装 IdentityModel 8.14.0 的独立进程核对为 NullIdentityModelLogger、ShowPII=false、LogCompleteSecurityArtifact=false、EventSource 未启用；仓库未配置 IIdentityLogger 桥接或诊断订阅器。该结果不代表部署进程、外部 EventPipe 或诊断工具不能启用采集，也不代表 DiagnosticListener / Activity / metrics 已治理。
- Auth ApplicationStarted 回调的 16 条 banner / 配置摘要，以及 RuntimeExtension 程序集加载失败的原始终端输出仍单独待治理。DbMigrate doctor / verify / help 的 stdout 是命令结果，本批不改写。
- L2 尚未整体关闭，`RadishLogging.Enabled` 默认 false；L1 剩余门禁与 L3–L6 顺位不变。
