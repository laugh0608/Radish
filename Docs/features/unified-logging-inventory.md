# 统一日志重构：现状审计与代码迁移清单

> 审计日期：2026-09-19（Asia/Shanghai）；范围：当前工作区源码、配置及日志文档。
>
> 数量表基于日志专题梳理时的源码快照；后续提交整理已将 API 权限种子从 `InitialDataSeeder.Identity.cs` 按职责移到 `InitialDataSeeder.ApiPermissions.cs`，数量不作实时门禁。
>
> 本页保留[统一日志专题](./unified-logging-governance-design.md)实施前的审计快照，不是当前实现清单或线上配置审计。当天后续 L1 / L2 入口改动以[事件契约与实现进度](./unified-logging-contract.md)为准；下表数量和旧入口描述不随实现逐项改写。

## 1. 入口、分发与存储

| 代码 / 配置 | 已核实事实 | 迁移责任 |
| --- | --- | --- |
| `appsettings.Shared.json` | Serilog 默认 Information；Console / File 的应用、SQL 输出开启；普通 Database 输出默认关闭；SQL CRUD 生成开关开启 | 统一两种模式、三级与安全采集策略 |
| `Radish.Extension.Log/SerilogSetup.cs` | 读取配置后再次设全局 MinimumLevel；注册查询参数 Enricher；无条件开启 SelfLog 文件 | 单一 provider / 策略，明确框架覆盖与有界自诊断 |
| `Radish.Extension.Log/LogConfigExtension.cs` | 应用 / SQL 各有终端和文件子 logger；数据库依赖宿主注册的 SQLSugar；文件按天和数量保留 | 生产宿主退出直接文件 / 数据库扇出；数量与天数不再混称 |
| `Radish.Extension.Log/LogBatchingSink.cs` | 按级别写 Information / Warning / Error 分表，SQL 单独存；错误使用原始 ToString；时间取 Timestamp.DateTime | 新模型 / 入库唯一所有者 / UTC / 安全异常摘要 |
| `Radish.Extension.Log/LogFilterExtensions.cs` | 关闭 SELECT 时读取 `SugarActionType` 属性，缺失时直接过滤 | 新结构化 operation 字段与契约回归 |
| `Radish.Common/LogTool/LogContextTool.cs` | SQL scope 只 push LogSource；文件模板为多行文本；自动向上找根 Logs | 显式部署路径、完整上下文、单行事件编码 |
| `Radish.Common/HelpTool/LogContextHelper.cs` | 存在重复 LogSource / 模板 / scope helper，所审 C# 范围未见外部调用 | 验证引用后移除重复实现 |
| `Radish.Model/LogModels` | 运行日志分级别，SQL / 通用审计独立模型；多处按月分表 | 新事件统一表；旧运行表保留只读；审计不混删 |
| `Deploy/docker-compose.yaml` | 七类服务；没有集中 collector 或显式 logging driver 配置 | 全服务登记、采集与容器轮转预算 |

不能把 `Logging.LogLevel` 与自定义 Serilog 选项当作已经存在的统一有效策略。实施需对 Microsoft / EF / OpenIddict / YARP / Hangfire 等框架来源逐项验证，不能只测自有 `Log.Information`。

## 2. 生成端与旁路

| 来源 | 已核实事实 | 计划处理 |
| --- | --- | --- |
| API / Auth / Gateway Program | 三者调用 AddSerilogSetup；启动输出包含多条 ASCII banner / 配置摘要 | 一条启动结果 + 必要状态事件；开发外观不写成多条生产事件 |
| Gateway | 只引用 Common / Extension.Log 等；未注册 AddSqlSugarSetup | 不让 Gateway 为日志入库建立业务数据库依赖 |
| `SqlSugarAop` | 普通 SQL 和参数按 Information 输出；慢 SQL 阈值 1000ms，与普通日志 ShouldLog 共用；catch 直接 Console.WriteLine | 独立慢查询与开发诊断规则，参数只保留类型与数量 |
| `SqlSugarSetup` | OnError 输出 Warning + 原异常；Log 连接排除 SQL AOP；参数时间规范化另行执行 | 合并异常所有者，但保留 Log 排除及 PostgreSQL 参数规范化 |
| `ServiceAop` | 同步成功与异常直接 Console.WriteLine；快照主要为类型 / 长度，不是完整参数实体 | 移除逐方法输出；不误称现状为完整请求体泄露 |
| `UnitOfWorkManage` | 事务 commit / rollback 直接打印 | 成功过程仅开发诊断，失败由事务调用边界记录 |
| Auth OpenIddict seed / schema adoption | 裸打印开始、已存在、更新等过程 | 生命周期汇总，种子变化安全摘要 |
| Auth AccountController | Debug 记录 returnUrl 等诊断 | 内容白名单；不能认为 Debug 就允许凭据 URL |
| DbMigrate | 自建 HostApplicationBuilder，未接统一 Serilog；seed / runner / doctor 多处裸打印 | 引导期统一入口，区分报告结果与诊断日志，不破坏退出码 |
| API 异常处理 | 自有 handler 对业务 4xx Warning、5xx Error；使用 TraceIdentifier 命名 TraceId | 明确 requestId / traceId，按可操作性分类 4xx，核对框架重复输出 |
| 后台任务 | 如 FileCleanup / CommentHighlight 多次 Info，含无工作量、逐文件、批次信息 | 一次运行总结；错误保留对象标识及任务关联 |
| `Frontend/scripts/serve-static.mjs` | Node 静态宿主默认 logger 为 console，有自定义安全字符串处理 | 复用统一 JSON schema / 三级策略，不按 Nginx 模板假设接入 |
| `Lib/radish.lib/src/lib.rs` | FFI 错误使用 eprintln，包括路径 / 水印 / hash 失败 | 宿主一次记录，评估错误返回契约，不擅自改变 ABI |

## 3. 隐私、审计与客户端

| 来源 | 覆盖与缺口 | 计划处理 |
| --- | --- | --- |
| `SensitiveQueryStringLogEnricher` | 处理部分字符串标量 URL 参数；不能覆盖所有嵌套 / header / 异常 | 源头最小化 + 完整事件安全边界，跨介质测试 |
| `AuditLogMiddleware` | POST / PUT / DELETE 默认审计；读取文本请求体，敏感 JSON 递归检测；非 JSON 解析失败返回原文；响应体默认不捕获 | 默认 metadata-only，显式动作白名单；非 JSON 不原样兜底 |
| 审计持久化 | 独立 AddSplitAsync；EnableLogging 再输出应用 Info 摘要；保存失败记录错误 | 领域证据与运行事件分离，摘要仅引用；不称当前通用审计已有事务级保证 |
| `AuditLogController` | 已有 SystemOrAdmin 管理 API | 统一查询页面内保留独立审计访问边界，不能直接扩成全局运行日志放行 |
| Client / Console logger | 各有 Debug / Info / Warn / Error；debug 配置影响前两级 | 统一三级 + diagnostic，保持调用点可逐批迁移 |
| 三份 `logSanitizer.ts` | 递归对象字段脱敏，但字符串、Error.message / stack 仍有缺口 | 复用公共无 UI 实现，契约样本验证，避免三份规则继续漂移 |
| Flutter `lib` | 针对常见 print / debugPrint / logger 的文本扫描未发现独立日志基建 | 明确本地诊断边界，不据扫描结论承诺整个设备没有原生日志 |
| 现有通知中心 | 面向用户社区事件，具备 summary / revision 及私域目标授权 | 运维告警使用 Console 独立权限和状态，不向普通用户收件箱复制 |

金额流水、内容治理证据、角色变更等领域记录不属于可随意去噪的普通 Log 文本；实现前需按具体调用链核对哪些是审计事实，哪些只是重复运行描述。

## 4. 静态数量参考

按 `Radish.*` 活动 C# 项目扫描，排除 Tests / bin / obj，统计常见 `Console.Write*`、Serilog 和 ILogger 文本调用匹配。**包含注释匹配，不是生产执行次数，也不是所有日志 API 的穷举。** 用于划定逐模块迁移工作量，不能当作线上噪声测量。

| 项目 | 直接 Console | Info | Warning | Error / Fatal | Debug / Trace |
| --- | ---: | ---: | ---: | ---: | ---: |
| Radish.Api | 0 | 49 | 11 | 11 | 4 |
| Radish.Auth | 11 | 24 | 4 | 2 | 10 |
| Radish.Auth.Persistence | 1 | 0 | 0 | 0 | 0 |
| Radish.Common | 2 | 0 | 0 | 1 | 0 |
| Radish.DbMigrate | 161 | 0 | 0 | 0 | 0 |
| Radish.Extension | 10 | 16 | 11 | 6 | 5 |
| Radish.Gateway | 0 | 16 | 5 | 0 | 0 |
| Radish.Infrastructure | 0 | 2 | 7 | 4 | 0 |
| Radish.Repository | 2 | 0 | 0 | 4 | 5 |
| Radish.Service | 0 | 139 | 81 | 144 | 11 |

高调用密度文件包括 `DbMigrateRunner.cs`、`InitialDataSeeder.Identity.cs`、API Program、`ExperienceService.cs`、`AttachmentService.cs`、`FileCleanupJob.cs`、`CommentHighlightJob.cs`。应逐类定义保留目的，不能按数量机械删除 Error。

## 5. 实现时的模块清单

| 模块 | 待交付 | 必须保留 / 验证 |
| --- | --- | --- |
| Common / Extension.Log | 事件规范、安全策略、模式解析、provider 注册和引导输出 | Common 不依赖业务层，配置不输出秘密 |
| Api / Auth / Gateway / DbMigrate | 标准入口、上下文及引导期失败处理 | 不因日志服务不可用阻断业务 / 迁移启动 |
| Extension / Repository / Service | AOP、SQL、事务、后台任务及业务日志分类 | 查询行为、异常传播、重试语义和审计事实保持正确 |
| Model / IRepository / Repository / IService / Service | 新运行日志、接收、查询、告警模型及接口 | UTC、幂等、long ID、并发与权限边界 |
| DbMigrate migration / doctor | 新日志表、索引、权限种子及状态验证 | 迁移可重入，旧表只读，默认 PostgreSQL 16 实测 |
| Deploy / 镜像 / 入口脚本 | collector、标准流适配、网络隔离、目录、容量 | 只需配置 .env 后 Compose 启动；已有部署有升级路径 |
| Frontend http / client / console / scripts | 公共日志规则、Node 宿主适配、管理页面与权限 | 浏览器日志不自动回传，默认 / 国风主题和 mobile |
| Rust / Flutter | 宿主错误所有权、原生本地诊断规则 | 不因日志改动扩新平台或破坏 FFI ABI |
| Scripts / Tests / Docs | 旁路扫描、fixture、存储故障与跨介质契约测试 | 例外白名单区分命令结果、测试输出与运行日志 |

## 6. 证据边界与需运行验证的事项

- 已完成静态检索和代表链路阅读；未逐条人工审阅全部业务日志调用，也未宣称完成全量运行覆盖。
- 未连接线上 Docker / 数据库，未确认服务器实际 LogLevel、日志体积、留存期或外部反代配置。
- 官方文档已核对 Forward、HTTP、文件、缓冲等能力；审计时尚未实测精确版本与配置兼容性；后续已取得的固定候选实验及未关闭门禁见[事件契约](./unified-logging-contract.md)。
- 宿主完全启动前的崩溃、容器被 OOM kill、Docker daemon 本身的错误不一定产生应用事件；需要状态 / 外部监测补充，不能靠解析标准流包办。
- 上一批论坛、Hangfire、部署目录和 Console 密度修复继续保留；其测试结果不算本日志重构验收证据。
