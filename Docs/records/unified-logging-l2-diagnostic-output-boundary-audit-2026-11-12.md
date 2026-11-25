# 首版诊断字段与本地输出边界

日期为 2026-11-12（Asia/Shanghai），核验基线为 `dev` 的 `8d02535a`。项目所有者确认按[第 10.14 节](../features/unified-logging-l3-ingest-plan.md#l2-diagnostic-and-output-boundaries)实施，结果见文末。

## 诊断字段

| 项目 | 实际输出与缺口 |
| --- | --- |
| API 用户反馈 | 真实 `ApiExceptionHandler` 的 500 在响应体 `traceId` 和 `X-Correlation-ID` 返回 `HttpContext.TraceIdentifier`。注入的请求 header 未被回显。旧链和候选日志只在有 W3C Activity 时保存另一组 trace / span，无 Activity 时缺省。六条环境 / 出口组合都没有响应 ID 字段。现有 [API 契约](../guide/api-index.md#错误与异常契约)把该响应 ID 用于日志与用户反馈 |
| 可信关联 | 已收口的 API 最终输出不继承外层 `operationId`。真实 `DbMigrateCommand` 的两个阶段事件经过通用候选 Serilog 适配时，可继承三个格式正确但由普通 scope 注入的关联字符串 |
| 任务关联 | Hangfire 状态选举使用 `radish:hangfire:{id}` 派生 operation ID，Outbox 实际状态写入使用数据库域与记录 ID 派生。任务状态测试覆盖重试与终态的既有关联及字段隔离 |
| 传输与查询 | 核验基线的事件模型、Lua 与接收白名单缺少 `requestId`。直接执行基线 Lua 时，合法 HTTP 事件保留，注入的可选字段被移除并标记裁剪。接收白名单会拒绝未知根字段。L3-A 已保存 trace 与 operation ID，并建对应索引 |

字段裁决依据及建议的权威生成点见[第 10.14 节](../features/unified-logging-l3-ingest-plan.md#l2-diagnostic-and-output-boundaries)。

## Flutter 本地入口

- 已跟踪的 Dart 业务代码及 Android、iOS、macOS 自有 runner / bridge 中，未检出显式 `print`、`debugPrint`、`dart:developer` 日志调用或平台日志入口。实际入口为 `main → RadishBootstrap.run → WidgetsFlutterBinding.ensureInitialized / platformServices.initialize → runApp`，仓库当前无自有 `FlutterError.onError` 或 `PlatformDispatcher.onError` 接线。已有认证、会话和 HTTP 代码主要将预期故障映射到状态或受控异常。
- 用 Flutter 测试绑定暂时恢复 SDK 默认 `FlutterError.presentError`，真实调用 `FlutterError.reportError`，捕获到合成异常正文和栈帧。已安装 SDK 源码表明 profile / release 对非 silent 的首次报告也调用 `debugPrintStack`，标签取 `exception.toString()`。SDK 的 root isolate 回调契约规定未处理时由具体 embedding 决定回退，可能打印到 stderr。该边界只做本机 SDK 源码核对，设备标准流和插件输出仍待目标平台验证。

## CLI 与应急例外

| 路径 | 复现与边界 |
| --- | --- |
| DbMigrate stdout | 使用真实 `DbMigrateRunner`、`DbMigrateCommand` 和 `RuntimeProcess`，注入禁用的合成数据库配置，在探测数据库之前返回。两种日志配置下，help 输出帮助并退出 0；doctor 输出报告并退出 0；verify 输出报告并退出 1；apply 预检调用 doctor，输出同类报告并退出 1。默认镜像命令为 apply |
| 报告内容 | doctor / verify / apply 预检保留目标占位，没有输出合成连接字符串。含换行的合成 `ConnId` 被直接插入报告并产生第二行；stderr JSONL 未继承该值。源码另有环境名和主库标识直接进入 stdout |
| DbMigrate stderr | 成功命令为 `dbmigrate.started / dbmigrate.completed`；失败 verify 为 started / `runtime.failed`，失败 apply 多一个 `dbmigrate.apply`。stdout 报告不含 JSONL 事件。探针不进入成功迁移、init 或 seed |
| 裸写 helper | `ConsoleHelper.WriteColorLine` 和四个字符串扩展方法仅检出定义及同文件转发，当前没有跟踪的外部调用。正式旧链的代码 console 仍是 Serilog 配置出口 |
| 有界应急 stderr | `RuntimeProcess` 的 `runtime.failed`、程序集安全摘要、Serilog SelfLog 的 `pipeline.self_diagnostic`、`RuntimeLogOutput` 的 `pipeline.output_failed` 及入库 / SQLite PRAGMA 的 `pipeline.ingest_failed` 由各自入口生成。既有用例覆盖安全字段、限频、写入失败与独立通道，`runtime.failed` 同时保留非零退出语义 |
| 当前候选采集 | 直接调用 Lua 时，合成 DbMigrate stdout 报告进入 parser 前路径，流标签被去掉，未解析内容会生成 `pipeline.rejected`。以 `bootstrap` 部署来源生成的启动失败、程序集、自诊断和入库应急均被常规部署归属校验拒收。此项是本机 Lua 证据，真实 Docker 流元数据及独立应急路线归 L1 / L3 联验 |

## 核验结果

- .NET 严格构建零警告、零错误，沙盒 21 项新探针通过。既有关联回归在沙盒的内存 API 回滚用例处超时，宿主同一程序集 276 项通过，0 失败、0 跳过。覆盖首批字段、CLI、任务关联、应急通道和本地入库。
- Flutter 沙盒因 SDK 缓存戳的写权限未运行测试，宿主以 `flutter test --no-pub` 通过 18 项，其中 1 项为本次框架报告探针。
- 6 项本地 Lua 断言及 70 项 Node 日志契约检查通过。核验期间无服务、容器或设备启动。临时探针移至本机临时目录。

<a id="implementation"></a>

## 实施与回归

实现边界见[事件契约第 89 节](../features/unified-logging-contract.md#l2-diagnostic-output-boundaries)。

- API 正常异常和业务 503 在响应写入完成后各产生一次 `http.failed`。旧链 WriteTo / AuditTo 与候选 JSONL 在 Development / Production 均验证响应体 `traceId`、响应 `X-Correlation-ID` 和日志 `requestId` 一致。验证 W3C 独立关联、无 Activity、入站 header / scope 污染、空值 / 非法字符 / 超长值、Controller 同名事件、业务 4xx 及响应写入失败。保留 Hangfire / Outbox 正常关联，通用候选输出省略普通 scope 注入的字符串关联。
- 本地 SQLite 入库保留有效 `requestId` 的 canonical JSON，接受缺省的旧事件，拒绝超长值、空 JSON 值、跨服务来源与 Fatal 事件上的非法字段。生产候选开关维持现状。独立列和精确索引归 L3，受权精确查询归 L4。
- 两种 DbMigrate 配置下，help / doctor 退出 0，verify / apply 失败预检退出 1。stdout 报告转义 CR / LF、ESC、Unicode 行分隔符和双向控制字符，长标签缩略，连接目标沿安全占位；stderr 保留阶段 JSONL 及独立 `runtime.failed` Fatal。测试配置在探测数据库前失败，成功迁移按既有验收走。
- Flutter 本地测试调用真实 `FlutterError.reportError`，验证仅输出固定摘要且保持 debug 的 silent 行为；显式引导异常只传播固定错误和空原始栈。认证、会话与 HTTP 相关回归一并运行。profile / release、引擎、插件及 isolate 的实际日志由设备批次验收。
- Lua 直调验证合法反馈 ID、既有缺省记录、错误来源 / 分类 / 等级 / 5xx 条件与非法字段裁剪。DbMigrate 合成 stdout 报告仍到达 parser，未解析时成为 `pipeline.rejected`；独立 bootstrap 应急仍被常规来源校验拒绝，具备常规部署身份的 `pipeline.output_failed` 可规范化。真实 Docker 标准流识别、介质和应急证据留存归 L1 / L3。

最终验证结果如下。

| 验证 | 结果 |
| --- | --- |
| .NET 严格构建 | `--no-restore --warnaserror`，零警告、零错误 |
| .NET 定向与扩展回归 | 沙盒定向 82 项通过。扩展回归在沙盒内存 API 异常管道超时，宿主同一程序集 1,735 项通过，0 失败、0 跳过，覆盖旧链与候选输出、应急、Hangfire、Outbox 和 SQLite 入库 |
| Flutter | SDK 缓存戳需要写入工作区外，宿主 `flutter test --no-pub` 19 项通过 |
| Node 与 Lua | `npm run check:logging-contract` 73 项通过。直接运行生成策略和 Lua 规范化函数，22 项断言通过 |
| 仓库与文档 | `git diff --check`、`npm run check:repo-quality:changed` 通过，事件契约和记录索引仍有建议篇幅提醒 |
