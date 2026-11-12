# 日志 L1 / L2 剩余门禁与 L3-A 接收入库方案

> 2026-11-05（Asia/Shanghai）。状态：**L3-A、第 8 节及第 9 节 Controller 已完成本地实现；其他来源及真实运行门禁接续**。准备基线为 `dev` 的 `fee65b97`；结果见[实施记录](../records/unified-logging-l3-ingest-local-2026-11-05.md)。只使用内存 HTTP 和临时 SQLite；未启动项目服务、容器或连接 PostgreSQL。
>
> 本页属于[统一日志专题](./unified-logging-governance-design.md)，承接[事件契约](./unified-logging-contract.md)。已完成来源仍以事件契约为准；本页维护剩余门禁及下一批实施边界，不另建主线。

## 1. 推荐顺位与交付结论

本批已实施 **L3-A：受控内网接收与幂等持久化**，交付默认关闭、可独立测试的接收链，并纳入 Log 连接直接相关的防递归修正。第 3 节列明的剩余七个自有来源也已完成本地切片；随后核对其他已登记业务来源的最终出口，再完成 L1 传输 / 磁盘 / 平台门禁及 L3-B 部署、留存和恢复。L4 查询、L5 告警与 L6 正式切换仍按主专题推进。

L2 未整体关闭不阻止用合成安全事件实现和验证 L3-A；但它阻止把全宿主真实日志接入直接认定为安全完成。L1 上界未关闭时，接收端只能是受控候选，不能开启 production Compose 汇聚。上述顺位保持一个日志专题串行推进，不同时开展 Native 或客户端连接实施。

准备核对确认：

- `Scripts/logging/collector-mock.mjs` 将请求累积到内存并追加文件，没有真实事务、认证或幂等存储，不能复用为产品接收器。
- 产品 API / Model / Repository / Service / DbMigrate 中尚无该内网接收与存储实现；Common 已存在生成契约 `Radish.Common.LogTool.RuntimeLogEvent`，新增持久实体需避免同名歧义。
- `SqlSugarSetup` 已排除 Log 库的普通 SQL AOP；但 SQLite PRAGMA 失败仍调用带异常的普通 `Log.Warning`，不能以 AOP 排除推定整条入库链绝不递归。
- `BaseLog` 带旧表的本地时间默认值与分表语义，新运行事件不能直接继承它。
- 浏览器本批、Hangfire 两来源、Common 两处异常诊断及既有框架来源保留已完成结论，不重复实施。

### 2026-11-10 收口接续

本页承接总纲[四个收口批次](unified-logging-governance-design.md#closeout-batches)的前两批；后两批沿原 L4/L5/L6，不因本页只写接收入库而删除。L3-A、七个来源和 14 个 Controller 保持本地完成，不恢复为待实现。

第一批按共享生成和消费链核对剩余 Service / Job / Hub。已完成范围见[第 10 节](#l2-final-output-first-slice)。通知与实时推送已按[第 10.11 节](#l2-notification-realtime-output)完成本地收口，下一组核验 Hub 自有生命周期。Flutter 只核对本地入口与规则，CLI 协议和应急例外分别登记。

第二批将下表 L1 剩余项与 L3 实际 collector、接收器及 SQLite / PostgreSQL 组合验证：先明确目标架构、镜像、隔离存储、端口、故障方式和恢复 / 清理边界；每个尚缺条件对应一次可验结果，不追加多平台矩阵。真实运行另获授权，替身、合成格式或模块通过不能关闭真实链路。传输、磁盘、持久化与留存出口未齐时不提前进入生产切换。

## 2. L1 剩余退出清单

| 门禁 | 已有证据 | 仍需交付 / 通过条件 | 所属批次 |
| --- | --- | --- | --- |
| 传输正式上界 | 8 KiB 应用输出限制；最大字段代表样本通过；16 MiB / 32768 条为实验候选 | 从实际 Forward 输入、规范化扩张、chunk 聚合与 HTTP 编码推导字节 / 条数 / 内存上界；覆盖边界前后及最坏组合；合法批次不因 413 丢失 | L1 接续，与 L3-B 联验 |
| 坏批隔离与真实确认 | 413 会丢弃 HTTP 支路 chunk；503 可重试 | 语义拒收与语法坏批分开；证明前置校验 / 有界隔离能处理坏批且不留原文，不靠假 200 或永久 503；真实提交后确认、响应丢失重放 | L3-A 实现接收语义，L1 / L3-B 验证插件链 |
| 磁盘与支路隔离 | 路径不可写时 HTTP 可交付；队列满有 dropped_records | 实际 ENOSPC、权限错误、buffer 与 file 共享磁盘争用、恢复、应急通道有界；分别报告文件缺口与入库缺口，不承诺补回已丢弃事件 | L1 接续 |
| 容量与恢复 | chunk 轮转、有限队列和 SIGKILL 后持久 chunk 恢复已有本机证据 | 计入单片超出量与索引 / 缓冲开销；验证重启、提交中断、备份恢复与丢失区间；SIGKILL 不等于真实断电持久性保证 | L1 / L3-B |
| 部署平台与原生来源 | Fluent Bit 5.1.2 的 OrbStack / Linux arm64 隔离实验；PG / Redis 合成格式 | 核定实际目标架构、digest、daemon 回环路径与七类服务标签；真实镜像来源关闭敏感 DETAIL / STATEMENT 等；未支持的平台显式保留 | L3-B 接入前 |

依据：[首轮传输](../records/unified-logging-l1-contract-and-transport-2026-09-19.md)、[采集安全与故障边界](../records/unified-logging-l1-guarded-collector-2026-09-19.md)。已有通过项不机械重做；只有配置 / 策略变化或组合故障新增时补回归。目标平台未确定前，不把本机 arm64 digest 当作多平台放行。

## 3. L2 剩余来源与退出条件

本节列出的八个入口已完成本地切片：PRAGMA 随 L3-A 交付，其余七个来源按第 8 节批准后完成。结果见[本批记录](../records/unified-logging-l2-owned-operations-2026-11-05.md)。这不是全仓运行覆盖证明，下面的其他退出工作仍保留。

| 来源 / 代码入口 | 本地实施结果 | 仍保留的边界 |
| --- | --- | --- |
| `UserController.GetUserByHttpContext` | 移除正常过程；权限 / 头像失败生成固定安全降级摘要 | 空权限集 / 忽略头像与其他失败外抛语义保持；真实登录读取未验收 |
| `ClientController` | 查询失败与四种写入结果使用固定事件；直接消费者 ApiExceptionHandler 补最终输出重建 | 业务调用 / 响应不变；真实 OpenIddict 存储与密钥轮换未验收 |
| `BootstrapService` | 首管理员奖励失败只记录固定部分完成摘要 | 不推定奖励未写入或注册回滚；真实奖励事务未验收 |
| `NotificationDedupService` | 缓存检查 / 写入 / 删除失败安全输出；正常 Debug 移除 | 当前仅发现构造注入，未检出业务方法调用；直接服务测试不证明生产可达 |
| `ChatMessageSearchService` | 移除成功 Info 与专用计时，保留注入签名 | 查询、游标与结果保持；真实搜索存储未验收 |
| `RateLimitSetup` | 一条注册摘要；逐请求观察仅显式开发诊断；内存管道覆盖 429 / 403 | 黑名单分区返回 GetNoLimiter 的既有行为不修复；未启动真实宿主 |
| `ExperienceExtensionSetup` | 移除重复注册 Info，保留注册与生命周期 | 不扩展经验业务行为 |
| `SqlSugarSetup.ExecuteSqlitePragma` | L3-A 已完成独立安全应急输出，保留 false 返回 | 真实数据库初始化故障仍待运行验收 |

其他必须保留的退出工作：

1. **已登记业务来源最终出口核验**：生成端安全不代表旧 sink 上继承的任意 scope 已受控。第 9 节的 14 个 Controller 已完成精确来源与最终输出收口，见[实施记录](../records/unified-logging-l2-controller-output-2026-11-05.md)。其余 Service / Job / Hub 按现有注册来源建立“生成点 → 消费边界 → 旧 / 候选输出 → 证据”的清单；已有完整证据直接引用，只补缺口，不重新调查任意外部订阅器。
2. **诊断信息完整性**：现行契约只有固定 `failureKind`，安全异常类型 / 栈帧及 `requestId / jobId / tenantId` 权威上下文尚未全齐。继续保持字段缺失，不在 L3 猜测、重造或放开自由文本；后续作为明确契约切片确认，不默认为已实现。
3. **裸输出例外**：DbMigrate doctor / verify / help 的 stdout 是命令协议，应急 stderr 有独立预算；`ConsoleHelper.WriteColorLine` 及其四个扩展方法本次未检出外部调用，保留为待明确处置项，不当作已证明可达漏洞。检查真实调用与例外表后才决定删除或限制。
4. **客户端与原生边界**：浏览器本地公共出口已完成；Flutter 日志规则 / 实际本地入口仍待确认。显式 print 文本扫描不能证明插件或整个设备无输出；不因此新增遥测、扩平台或恢复 Rust 已删除的裸打印。
5. **成组运行验收**：真实登录、业务读取 / 写入、后台空轮询、网络 / 存储故障须在后续授权环境按已登记链验证安静性、安全及异常所有权；模块替身与内存管道不替代它们。

不再挂回当前队列：第 57 节已关闭的早期错误结果构造问题、第 69 节已登记 DataProtection 来源、Hangfire 第 73 节、浏览器第 74 节。未注册 OpenIddict 集成、无产品调用的 Socket `.Client`、HttpSys delegation 与外部 EventPipe / Activity / metrics 保持原专题边界。

L2 整体退出要求：上述实际接线来源完成分类，正常生产不生成开发载荷，最终输出仅含受控字段，异常所有权和原业务结果通过回归；命令协议 / 应急例外明确。不能凭日志调用数量下降或候选 sink 的统一裁剪宣称退出。

## 4. L3-A 接收契约

### 4.1 接口与隔离

- 沿用主方案 `POST /internal/logs/ingest`，默认关闭；在 API 内配置独立监听端口，不增加新宿主。内网端口只开放该入口及必要探活；普通业务端口拒绝 `/internal`，内网端口拒绝业务、文档、静态文件与 Hub。
- 判断实际连接的本地端口，不信任 Host、转发头或请求路径代替监听隔离；Gateway 不转发 `/internal`。校验在读取请求体及通用审计之前完成。
- 使用专用 collector 凭据，独立于用户 JWT；凭据绑定配置中的 deployment 和允许 service 集合，载荷不得自行扩权。配置缺失、端口冲突或重复绑定应阻止启用；日志不得回显凭据。
- 配置仍归 `RadishLogging`、共享 / 宿主 appsettings 与未提交的本地覆盖；本批不写 production Compose、不公开宿主端口。跨主机 TLS / mTLS 保留后续范围。

### 4.2 有界读取与确认

| 项目 | L3-A 候选约束 |
| --- | --- |
| 输入 | UTF-8 JSONL；严格逐行解析，拒绝重复 JSON 字段、非法编码与不支持的压缩；空信封拒绝，不整包读入内存 |
| 大小 | 单条规范事件沿用 8 KiB；信封暂以 16 MiB / 32768 条为受控测试候选，**不是生产传输保证**；空行也计入字节预算 |
| 资源 | 内部最多 200 条一个事务；入口限制并发、读取时间与数据库命令时间；默认候选并发 2，超额返回 429，不进入无界等待队列 |
| 内容 | schema / 等级 / 诊断 / 来源 / 时间格式 / UUID / 允许字段按共享策略校验；拒绝未授权来源和 Production diagnostic，不重新生成合法事件 ID 或覆盖发生时间 |
| 成功 | 有效记录及本事务拒收摘要持久化后才算该批提交；读取完信封且全部内部批次提交后返回 200 与受控计数 Vo |
| 暂时失败 | 数据库不可用、事务失败返回可重试 503；已提交内部批次保留，整包重放依赖唯一键，不伪造全包回滚 |
| 取消 / 响应丢失 | 取消未提交事务，已提交记录不撤销；提交后断连不生成第二个 eventId，collector 重放去重 |
| 语义无效单条 | 不写原文，持久化有限原因摘要；其他有效记录继续；不得因为丢掉坏记录而不报告拒收数 |
| 认证 / 格式 / 超限 | 认证失败 401/403；语法损坏或不支持编码 400；超限 413。不得假成功；这些状态必须与 L1 前置隔离联验后才准接入真实 collector |

在后段发现语法错误 / 超限时，前面已提交的内部批次可能存在；返回值必须准确表达失败，不能称整包原子。接收器不会因 413 自动保存剩余原文；合法 chunk 的上界与坏批隔离仍由第 2 节关闭，否则停在候选状态。

接口由 Controller 注入 `IRuntimeLogIngestService`；Service 执行校验和批次编排，Repository 执行事务与幂等写入，不向 Controller 暴露实体。响应使用 `MessageModel<RuntimeLogIngestVo>` 和 `Vo` 字段，只含安全计数 / 固定状态，不回传行内容或第三方错误。

### 4.3 数据模型与幂等

- Log 库新增持久实体 `RuntimeLogEventRecord`，物理表显式命名为 `runtime_log_event`，与 Common 的生成事件明确区分；不继承旧 `BaseLog` 或默认业务软删除。
- 保存原事件核心字段，新增 UTC `IngestedAtUtc` 和存储主键；过滤字段独立列，白名单属性存有界 JSON。服务端规范化生成稳定核心内容摘要，排除本次入库时间。
- 唯一键 `(DeploymentId, EventId)` 是并发裁决者，不能仅“先查后插”。同键同内容为重复，同键异内容拒收并保留先写事件，不能静默覆盖；实现分别验证 SQLite 与 PostgreSQL 的冲突语义。
- 原 `OccurredAtUtc / ObservedAtUtc` 保留毫秒精度与 UTC；错误时间进入拒收，不用当前时间冒充；不得把旧 `DateTime.Now` 语义带入新表。
- 索引覆盖发生时间 + EventId、服务 + 时间、等级 + 时间、TraceId / OperationId；不在本批增加查询 API、任意全文检索或物理分区。
- 拒收摘要按受信部署 / 服务、固定原因和 UTC 时间桶有界聚合，不保存原文、任意来源字符串或坏 eventId。事务内更新并设置计数上界；重放可能重复计入无有效 ID 的拒收次数，明确这是拒收尝试数，不宣称唯一坏事件数。物理表为 `runtime_log_rejection_bucket`，保留当前 UTC 日及此前 6 日，每次成功事务最多清理 200 个过期桶；停收时不额外启动后台清理。
- 幂等保证限于事件键仍保留期间；L3-B 必须协调保留期、最大重放时窗与去重记录寿命，过期旧文件不能因键已清理被自动重导并触发新告警。本批无自动历史补录。

### 4.4 migration 与故障通道

- 按[结构变更治理](../guide/database-schema-change-governance.md)新增 Log scope 的有序 migration、checksum、前置条件和 Verify；接入 `init / apply / doctor / verify`，不得改已发布 migration 或让 API 启动建表。
- 覆盖空库、已有 baseline + pending、新结构重入、结构漂移拒绝。旧分级表和审计表保留；本批没有删除 / 回填旧原文或改变权威审计规则。
- 新写入专用 Repository 复用现有 Log 连接选择，不为 collector 配置数据库凭据，也不让 Common 依赖 Model / Service。
- 接收请求排除通用请求体审计、正常请求完成日志与逐批成功日志；保持 Log SQL AOP 排除。数据库失败、校验失败与 SQLite 初始化失败使用独立限频安全诊断，不写回同一数据库 sink。
- 管道保存 accepted / duplicate / rejected / failed 与最近成功提交时间等有限状态；数据库断开时仍可返回失败 / Unknown，不能靠“成功写入日志”证明日志链健康。本批不实现 Console 状态页面或告警计算。
- 回退关闭新监听与接收功能，保留新表与已收数据，旧链仍可运行；不执行 Down / 删除表 / 清空日志。跨旧版本的完整部署与恢复演练归 L3-B / L6。

## 5. 改动面与验证矩阵

实际改动范围：`Radish.Model/LogModels`、`Radish.Model/DtoModels`、`Radish.Model/ViewModels`、`Radish.IRepository` / `Radish.Repository`、`Radish.IService` / `Radish.Service`、API 内网路由与管道接线、DbMigrate 注册 / 结构验证、共享配置与日志策略、定向测试。`SqlSugarSetup` 只调整上述直接相关日志边界。实际文件按既有职责拆分，不建立通用新框架。

| 验证层 | 必须覆盖 | 证据限制 |
| --- | --- | --- |
| 无服务本地检查 | 构建、定向测试、日志契约、文档 / 链接 / 卫生、diff | 不代表端口隔离和真实 collector 已通过 |
| 内存 HTTP / 合成输入 | 公共端口与内网分流、凭据错误、非法来源、重复字段、边界长度、时间格式、生产诊断、隐私标记、取消与资源释放 | 真实 Kestrel 监听需要后续运行验收 |
| 临时 SQLite | 唯一键并发、跨内部批次失败、同键冲突、响应丢失重放、旧基线升级 / 重入 / verify、受控锁冲突 | 不替代 PostgreSQL 或全宿主启动 |
| 失败与递归 | DB 故障、PRAGMA 失败、应急输出失败 / 重入、计数上界；stdout / stderr / 拒收存储均无合成秘密 | 不替代 ENOSPC 与共享磁盘故障 |
| 授权后的 PostgreSQL 16 / 实际监听 | 与 SQLite 一致的事务与 UTC 契约、migration / 索引、连接失败恢复、端口双向隔离 | 未执行前只能报告本地实现完成，不能关闭 L3-A 全部验收 |
| L1 / L3-B 联验 | Fluent Bit → API → Log 库、413 / 429 / 503、提交后断连、恢复、文件一致性与坏批隔离 | 通过后才可申请部署接入，不等于 L6 切换 |

L3-A 本地实施可使用已安装依赖、内存管道和测试创建的临时 SQLite；不启动项目服务或容器。测试临时目录在结束时精确清理。无需为文档规划重跑产品全量测试。

## 6. 批次批准与停止线

项目所有者已批准第 4–5 节的 L3-A 代码、迁移、临时 SQLite / 内存管道验证与文档收口；包括直接相关 PRAGMA 日志防递归，第 3 节其余七个来源后续已按第 8 节单独获批并完成本地实施。L3-A 已提交为 `7002cd75`。服务与容器启动另行确认。

服务 / 容器验证在准备好可审阅脚本后，另列确切命令、镜像 digest、回环端口、目录、数据库与清理清单申请授权；本页不沿用 9 月启动许可，不请求笼统授权未来运行。缺少 PostgreSQL / 监听 / collector 证据时明确保留对应门禁。

出现以下情况时停止扩张并报告：必须新增依赖；现有分层无法承接接收事务；需要改变原业务结果；无法证明合法 chunk 上界却准备启用真实采集；需要改生产配置、迁移真实库或扩大平台范围。方案批准不代表 L1 / L2 / L3 整体完成，也不自动授权 push、PR、发布或部署。

## 7. 当前配置与验证入口

接收开关为 `RadishLogging:Ingest:Enabled`（默认 false），与生成端 `RadishLogging:Enabled` 分开。接收模式继承受信的 `RadishLogging:Mode`；载荷模式必须一致，Development 仅在 Development 宿主允许，不能由事件自行开启诊断。

启用候选时必须显式提供 `Kestrel:Endpoints:<name>:Url` 公共监听、独立 `Ingest:Port`（1024–65535）、`DeploymentId`、允许的 `Services` 与 `Credential`。缺失或公共端口冲突均失败；不能只设置 URLs 再让专用 Listen 覆盖原公共监听。实际绑定与网络可达性仍待运行验收。凭据只通过未提交的本地配置或 `RadishLogging__Ingest__Credential` 注入，不放入共享文件；HTTP 使用 `X-Radish-Collector-Key`，允许 `application/x-ndjson` / `application/json`，不支持压缩。

并发默认 2（允许 1–8）、请求时限默认 30 秒（1–120）、数据库命令时限默认 5 秒（1–30）；字段分别为 `MaxConcurrentRequests`、`RequestTimeoutSeconds`、`DatabaseTimeoutSeconds`。请求时限到达返回 408，调用方取消为 499；真实断连可能无法收到响应。返回计数只包含服务已确认完成的内部事务；数据库提交结果不确定时不能据此推定未写入，必须沿原事件 ID 重放。

`RuntimeLogIngestState` 仅保留本进程已接纳处理的提交计数、处理失败次数与最近确认提交时间，重启清零；它不是持久计数、跨实例汇总或对外查询接口。认证拒绝与尚未进入 Service 的请求不计入这些处理计数。独立 stderr 诊断每分钟最多尝试一次，输出失败保留计数但不保证落盘。

无需服务的专项入口：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~RuntimeLogIngest&Database!=PostgreSQL'
npm run check:logging-contract
```

新增 PostgreSQL 集成测试带 `Database=PostgreSQL` trait，只有另行授权隔离连接后才能执行；本批未执行。现有 Log baseline 升级走 `20261105_023_runtime_log_ingest`；尚无 baseline 的旧库仍须满足当前结构接管规则，不借新迁移自动放宽接管或修补已有漂移。

`20261105_023_runtime_log_ingest` 对拒收计数 `attempt_count` 按数据库验证 64 位存储。PostgreSQL 接受 `bigint` 和 SqlSugar 元数据返回的 `int8`，拒绝 32 位 `integer` / `int4`。SQLite 继续接受 `INTEGER`，迁移 ID 和账本校验和保持原值。

## 8. 已批准切片：剩余七个自有来源

2026-11-05 基于 `7002cd75` 完成只读核对，方案由 `29e8b4f4` 保存。状态：**项目所有者已批准，本地实施与回归已完成**。本批一次处理第 3 节剩余七个来源的生成、最终出口与回归，不逐文件重复申请；不扩展到所有旧业务来源，也不把本批完成等同于 L2 整体退出。

### 8.1 源码事实与处理边界

| 来源 | 实施内容 | 必须保留的语义 / 证据边界 |
| --- | --- | --- |
| 当前用户组装 | 移除四条正常过程 Info；权限 / 头像失败分别生成固定降级事件，只带 failureKind | 查询失败仍返回空权限集 / 忽略头像；用户读取本身的外抛失败不新增本地消费；不记录身份、角色或头像地址 |
| 客户端管理 | 列表 / 详情消费失败改为固定动作与 failureKind；创建 / 更新 / 软删除 / 密钥重置完成只带固定动作 | 写操作 catch 仍构造并外抛既有 BusinessException，由既有异常边界负责，不额外写本地失败事件；保留 manager 调用与响应，不把成功返回等同于整个请求最终完成 |
| 首管理员创建 | 奖励失败改为固定部分完成摘要，只带 failureKind | 管理员已创建的事实不回滚；不声称奖励必然未写入、已经发放或后续自动重试；下层既有重试观察不等于最终失败重复消费 |
| 通知去重 | 三类缓存失败分别使用固定操作枚举；移除正常 Debug 中的业务键 | 当前仅在 PostService / CommentService 构造签名发现注入，未检出生产方法调用；保留服务及接口，不新增调用。直接服务测试证明检查失败返回 false、写入 / 删除失败被消费，不宣称运行链已触达 |
| 聊天搜索 | 移除逐次成功 Info 及仅用于该输出的计时 | 保留搜索校验、可见范围、分页游标、结果与异常；不新增关键词、频道或用户日志 |
| 限流扩展 | 合并为一个固定注册摘要；白名单 / 黑名单观察及拒绝信息只允许显式开发诊断，受 EnableLogging 与宿主诊断门禁共同控制 | Production 不逐请求发告警；禁止 IP、路径、请求头、Lease 或自由方法文本。黑名单分区当前返回 GetNoLimiter，而 403 在 OnRejected 中处理，本批不修复该既有决策问题，也不把“命中黑名单”描述为“已拒绝” |
| 经验扩展 | 移除重复的注册 Info | 服务注册顺序与生命周期保持，不引入新输出来源 |

### 8.2 最终输出与异常所有权

- 正常过程直接移除；需要保留的状态、降级及诊断使用固定事件目录和封闭字段。不为这批增加任意字符串上下文、异常正文 / 栈、用户 / 客户端 ID、密钥、URL 或缓存键。
- 给保留事件建立精确来源身份；复用现有最终 sink 包装接线，在上下文合并后重建旧链代码 / 配置 WriteTo、AuditTo 与候选输出。按来源匹配事件，不用宽前缀或只有 EventCode 的全局拦截，也不依靠生成端安全推定 scope 安全。
- 同来源未知事件保留严重失败的固定 unknown 摘要；正常未知与开发诊断受明确门禁控制。只保留原时间、等级、固定分类、允许字段及强类型 trace / span，不从自由字符串恢复关联键。
- User 两种降级、Client 两种查询失败、Bootstrap 奖励失败和 Dedup 三类缓存失败各由当前消费边界记录；原本外抛的 Client 写入失败继续交给既有异常边界。OpenIddict manager 精确来源治理沿用事件契约第 66 节，只验证衔接，不重新扩大框架范围。
- 保留 WriteTo 消费 sink 异常、AuditTo 可传播异常和候选应急通道的既有区别；输出异常可能影响 catch / 结果构造时，测试和文档如实描述，不借日志治理新增吞错或回滚承诺。

### 8.3 实施与验收

1. 更新上述来源、必要的精确输出适配与共享事件策略；不改 API、数据模型、限流算法、业务事务或依赖版本。保留既有注入接口，避免为了移除一条输出扩大调用方改造。
2. 使用真实 Controller / Service / 限流选项及内存 HTTP 管道，依赖使用现有替身；覆盖业务返回、降级与外抛边界、正常安静性、模式 / 诊断开关及 EnableLogging。不绑定端口或执行项目 Start / Run。
3. 覆盖旧链与候选输出、任意 scope 污染、未知 / 错误事件身份、合成秘密、WriteTo / AuditTo 故障和候选应急输出；断言最终序列化结果而非只看生成参数。并复用 ClientController、BootstrapService、UserControllerProfile、ChatMessageSearchService 等已有回归。
4. 执行定向测试、解决方案构建、日志契约、身份相关定向检查、文档 / 链接 / 文本卫生和 diff 检查，按真实结果更新事件契约与当前计划。已有 Wiki 时间语义 baseline 问题不顺手改动或刷新基线。

本批不安装依赖、不启动服务 / 容器、不连接真实存储、不开启生产汇聚。L1 磁盘 / 上界、L3 PostgreSQL / collector 联验、Flutter 来源及其他已登记业务来源最终出口仍各自保留。本节批准范围内的实现和本地验收已连续完成；新增业务行为修复或实际运行验证仍另列范围。


### 8.4 直接消费者的实际补漏

Client 写入失败接入真实 `ApiExceptionHandler.TryHandleAsync` 的回归发现：该边界原本仅在生成端安全，旧链仍继承任意 scope。为完成本批已批准的最终出口与异常所有权核验，补充其固定 `EventId(1, ApiRequestFailed)`，并在 `ApplicationOperationLogSink` 按精确来源重建既有 `http.failed`。只保留 500–599 的 int 状态和封闭 failureKind，未知身份使用 `http.handler.unknown`；响应写入顺序、处理返回值和日志故障传播均保持。此修正覆盖该共享消费边界，不据此宣称其他旧业务来源已完成最终输出治理。

## 9. 下一批：API Controller 最终输出收口

2026-11-05，准备基线为 `12f739f9`。状态：**已获批准并完成本地实现与定向验证**。承接第 8 节，按一个批次完成下面 14 个 Controller 的已登记输出，不逐文件申请，不重做已完成的 User / Client / ApiExceptionHandler。结果见[实施记录](../records/unified-logging-l2-controller-output-2026-11-05.md)，现行规则见[契约第 77 节](./unified-logging-contract.md#77-api-controller-最终输出边界2026-11-05)。

### 9.1 已确认缺口与有限目录

源码核对发现，以下来源虽已移除异常原文，但大多数 Serilog 调用只有 EventCode，没有固定 SourceContext；当前精确 sink 不接管它们。Attachment 使用 ILogger，但尚无本来源的最终适配。不能按 `http.failed` 等通用 EventCode 全局拦截，否则会把其他服务或 scope 伪造事件当成已治理 Controller。

真实 Reaction 错误结果构造方法的临时内存探针已确认：原 503 响应保持，旧链最终事件仍含外部 scope 的 Authorization，候选链则经公共策略剔除该字段。这是待修缺口复现，不是验收通过；方法与限制见[准备记录](../records/unified-logging-l2-controller-output-readiness-2026-11-05.md)。

| 精确 Controller 来源 | 本批已有事件目录 | 除固定身份外允许的字段 |
| --- | --- | --- |
| `CoinController` | `http.failed`、`coin.balance_query_rejected / transaction_query_rejected / adjustment_rejected` | failureKind；http.failed 另允许 statusCode |
| `PaymentPasswordController` | `http.failed` | failureKind、statusCode |
| `AttachmentController` | `http.failed`、`upload.cleanup.failed` | failureKind；分别另允许 statusCode 或 cleanupOperation |
| `ShopController` | `http.failed`、`order.cancellation_rejected / fulfillment_retry_rejected / remark_rejected`、`benefit.activation_rejected / deactivation_rejected / revocation_rejected`、`product.management_rejected`、`inventory.use_failed / rename_failed` | failureKind；http.failed 另允许 statusCode |
| `ReactionController` | `http.failed` | failureKind、statusCode |
| `PostQuickReplyController` | `http.failed`、`quick_reply.query_rejected / notification_enqueue_failed` | failureKind；http.failed 另允许 statusCode |
| `PollController` | `http.failed`、`poll.request_failed` | 同上 |
| `LotteryController` | `http.failed`、`lottery.request_failed` | 同上 |
| `QuestionController` | `http.failed`、`question.request_failed` | 同上 |
| `PostController` | `post.edit_failed / top_failed` | failureKind |
| `CommentController` | `comment.create_failed / like_failed / edit_failed` | failureKind |
| `TagController` | `tag.request_failed` | failureKind |
| `CategoryController` | `category.request_failed` | failureKind |
| `StickerController` | `sticker.request_failed` | failureKind |

目录中的斜杠表示相同前缀下的独立事件；实现按完整字符串登记。Shop 的私有 helper 虽接收 eventCode 参数，调用点仍是表内有限常量，不开放任意事件码。

### 9.2 实施方式与行为边界

1. Serilog 生成点补本 Controller 的固定 SourceContext；Attachment 的两个 ILogger 入口使用固定命名 EventId。所有入口继续只传固定 failureKind、受控状态或操作枚举，不新增业务标识、消息、异常对象或详情字段。
2. 扩展既有 `ApplicationOperationLogSink` 的精确来源目录及按事件字段规则，复用三条输出接线，不新增一层通用日志框架。原 User / Client / Bootstrap / Dedup / RateLimit / ApiExceptionHandler 的规则保持；Controller 目录与字段若需拆文件，仅按真实职责拆分。
3. `statusCode` 仅保留 int 500–599，`cleanupOperation` 仅为 quota-complete / quota-release，failureKind 复用既有封闭枚举；其他字段不继承。http.failed 固定 http 分类，其余按现有语义固定为 http 或 application；旧 `business` 非法分类归为 application，不扩展共享类别集合。
4. 每个精确来源仅接受本目录事件，未知 / 缺失 / 跨来源身份生成该来源固定 unknown；Warning 以上保留安全摘要，低等级只在既有显式开发诊断门禁下输出。保留原 Timestamp / 等级和强类型 trace/span，剔除异常、任意 scope、自由字符串关联键；邻接类型与其他来源不匹配。
5. 保留原 catch 类型、响应字段、Message / 本地化求值顺序和异常传播；不挪动已由契约第 57 节治理的结果构造顺序，不增加全请求去重、吞错、重试、回滚或持久化。Service 原事件与 Controller 消费事件的不同语义继续保留，不把同一事件码视为同一所有者。
6. 保留 WriteTo 消费输出失败、AuditTo 可传播与候选应急通道语义；日志失败前已发生的业务副作用不撤销。生产候选开关、接口、权限、依赖和数据结构不变。

### 9.3 验证与完成标准

- 新增真实 Controller / 已有消费方法到正式 ConfigureLegacy / RuntimeLoggingConfiguration 的最终输出回归。覆盖旧 / 候选 Development / Production、配置 WriteTo / AuditTo 和代表性代码 console；注入 Authorization、任意嵌套对象、伪造 SourceContext / EventCode / Diagnostic、跨事件安全字段及非法数值，断言最终事件 / JSON 无污染。
- 覆盖每个登记事件、未知 / 错误身份、邻接来源、诊断门禁、强类型关联和输出失败。明确哪些消费方法用反射定向调用，哪些完整 action 已通过现有服务 / repository 替身覆盖；不把前者冒充 HTTP 端到端。
- 复用 CoinMovement、PaymentPassword、AttachmentUpload、ShopFulfillment / Management / Entitlement、Reaction / QuickReply / Poll / Lottery / Question、Post / Comment / Tag / Category / Sticker 等日志与业务回归。继续覆盖结果构造二次失败、专用拒绝安静性、消息只读取一次、原状态与 mutation 计数；Service 层已知最终输出缺口不借本批顺手扩张。
- 对共享 sink 的既有六来源、HTTP 异常消费者及 L3 本地接收追加定向防回归；构建、共享 Node / .NET 日志契约、身份 / 权限、文档 / 链接 / 卫生及 diff 检查按影响面执行。
- 完成后同步事件契约和本批记录，将这 14 个 Controller 从最终出口待核验队列移除；不以候选统一裁剪或历史测试通过代替旧链精确来源结果。

本批只使用已安装依赖、内存管道、已有测试替身及相关测试自管临时文件 / SQLite；不启动项目服务 / 容器、不连接真实存储、不执行实际业务迁移、不访问生产。真实 File / DB sink 故障和 collector 联验仍单独授权。新业务行为问题或需要扩大 Service / 框架来源时先报告，不将其隐藏在日志收口中。

### 9.4 命令输出例外与后续顺位

- DbMigrate doctor / verify / help 的 stdout 仍是命令结果协议；Program 的运行诊断写 stderr，目标描述已使用固定 configured 占位，不输出连接串。此次只核对代码，不启动命令访问真实库，不把协议输出机械替换成运行事件。
- 实际 ConsoleHelper 方法为 WriteColorLine 及 WriteErrorLine / WriteWarningLine / WriteInfoLine / WriteSuccessLine 扩展；仓库 C# 搜索只命中定义和内部转发，未发现外部调用。保留未使用工具，不宣称反射或外部调用绝不存在，也不为未证实的运行缺口增加重构。
- 本批后回到已登记 Service / 后台任务 / Hub 来源的最终出口清单，按共享生成 helper 和真实消费链组织有限批次；随后接续 L1 上界 / 磁盘及 L3 真实监听 / PostgreSQL / collector 联验。诊断上下文完整性、Flutter 来源仍保留各自边界，不重启已经关闭的 Controller 切片。

<a id="l2-final-output-first-slice"></a>

## 10. L2 最终输出核验与首组修复

2026-11-10 在 `dev` 的 `546b0968` 复现。商城订单取消探针经真实 Job 方法和日志配置到达旧链配置 WriteTo / AuditTo 和候选 JSONL。结果见[核验记录](../records/planning-closeout-2026-11-10.md#l2-job-output-audit)。

复现时，`BusinessJobSummary` 在全局 logger 上绑定事件码和计数，缺少固定的 `SourceContext`。原精确来源适配未重建这条输出，候选公共白名单仍会接受合法但不属于该 Job 事件的上下文。确认范围后完成本地修复，输出契约见[事件契约第 78 节](./unified-logging-contract.md#l2-business-job-final-output)。

### 10.1 已登记调用链

以下清单按[事件契约](./unified-logging-contract.md)的精确来源分组。旧链在 `SerilogSetup.ConfigureLegacy` 合并上下文后分流，候选链经 `RuntimeLoggingConfiguration` 和 `RuntimeSerilogSink` 生成 JSONL。已有生成端及直接消费者证据按原范围引用。共享后台 Job、抽奖 Job、文件和记录清理、服务内清理、Outbox、奖励 / 币 / 经验以及商城 / 库存 / 支付口令已完成本地最终输出收口，其余按组补证。

| 分组 | 实际入口与生成点 | 消费边界和证据 |
| --- | --- | --- |
| 首组业务 Job | `ShopJob.CancelTimeoutOrdersAsync / MarkExpiredBenefitsAsync / GenerateDailyStatsAsync`、`CommentHighlightJob.ExecuteAsync`、`RetentionRewardJob.ExecuteAsync` 均由 `ApiRecurringJobRegistration` 登记，共用 `BusinessJobSummary.Write` | 契约 §11、78 和 `BusinessJobLoggingTests`。商城取消通过真实配置验证最终出口，另外四个入口沿原方法回归 |
| 抽奖 Job | `PostLotteryJob.ExecuteAutoDrawAsync` 用自己的 `ILogger<PostLotteryJob>` scope 写摘要，扫描异常交给 Hangfire | 契约 §11、79、`BusinessJobLoggingTests` 和 `LotteryControllerLoggingTests`。[第 10.4 节](#l2-lottery-job-output)已完成本地切片 |
| 文件及记录清理 | `FileCleanupJob` 的软删除、临时文件、回收站、孤立附件入口用内部批次摘要。通知收件箱、Wiki 草稿、聊天回应三个 Job 各在 `ExecuteAsync` 用类型化 logger | 契约 §10、80、`CleanupLoggingTests` 和 `FileCleanupJobTest`。[第 10.5 节](#l2-cleanup-job-output)已完成本地切片 |
| 上传及令牌清理 | `ChunkedUploadService.CleanupExpiredSessionsAsync`、`FileAccessTokenService.CleanupExpiredTokensAsync` 用 `ServiceCleanupSummary`。前台目录 / 配额失败走同 helper 的 `RecordStandaloneFailure` | 契约 §13、21–24、81。[第 10.6 节](#l2-service-cleanup-output)已完成本地切片 |
| Outbox | `ReliableOutboxDispatcherJob.DispatchAsync` 输出分派摘要。`ReliableOutboxExecutionJob.ExecuteAsync` 消费任务，`ReliableOutboxRepository` 的条件更新确实影响行后输出重试 / 死信 | 契约 §9、82 和 `OutboxNativeLoggingTests`。main / chat 的状态写入、权威 `operationId` 和旧 / 候选最终出口已完成本地回归，见[第 10.7 节](#l2-outbox-output)。Hangfire provider / 状态观察器沿用 §73 证据 |
| 奖励、币与经验 | `CoinService` 批量及并发重试、`ExperienceService` 发放 / 幂等 / 初始化和等级配置缓存使用 `RewardRuntimeLog`。调账、转账幂等、奖励存在检查和经验治理各有登记生成点 | 契约 §12、14–15、17–18 及奖励 / 币 / 经验日志回归。2026-11-11 已核验六个生成来源，复现来源误归、scope 污染和生产过滤缺口；已按[第 10.8 节](#l2-reward-output)完成本地修复，输出契约见 §83 |
| 商城、库存与口令 | `OrderService.PurchaseAsync` 扣币及履约分支，及已登记的 `ProductService`、`UserInventoryService`、权益和 `PaymentPasswordService` 生成点 | 契约 §14、16、19–20、25 和对应业务回归。四个生成来源已按[第 10.9 节](#l2-shop-output)完成本地最终输出修复，契约见 §84 |
| 公开查询与报表 | `PublicHeadSnapshotService`、`PublicSitemapService` 的缓存及生成分支，Gateway 快照客户端 / 注入中间件，以及公开发现流、排行榜和统计查询链 | 契约 §26–29、85。前四个来源的 11 个事件已按[第 10.10 节](#l2-public-query-output)完成本地最终输出修复；发现流、排行榜和统计沿用 API 最终异常消费回归 |
| 通知与实时推送 | `UserFollowService` 通知入队，`NotificationService` 与可靠任务的收件箱变更到 `NotificationPushService.PushInboxChangedAsync`，以及 `UserInteractionRealtimeNotifier.NotifyRelationshipChangedAsync`、`CommentRealtimePushService.PushAsync` | 契约 §30–31、33、35、38、86。四个生成来源的五个事件已按[第 10.11 节](#l2-notification-realtime-output)完成本地最终输出修复。轻回应沿已有事务与 Controller 证据 |
| Hub 生命周期 | `ChatHub.OnDisconnectedAsync` 和 `NotificationHub.OnDisconnectedAsync` 在既有清理后用类型化 logger 写摘要。`CommentHub` 进组、退组和输入中路径已有安静性回归 | 契约 §32、34、36 和三组 Hub 测试。核验两条自有断开摘要的出口，真实连接另行验收 |
| 内容及表情 | `CommentService` 高亮重算、`ContentSubmissionService` 冲突恢复、`ForumContentRevisionService` 恢复失败、`ReactionService` 重试、`StickerService` 缓存与缩略图降级 | 契约 §37、40–42、48–49 和对应业务回归。分别核对 Service 所有权，已收口的 Controller 做关联防回归 |

浏览器、七个已收口的自有来源及其直接消费者、14 个 Controller、已登记的 Hangfire provider / 状态观察器，沿用事件契约 §73–77 的本地结论。DbMigrate `doctor / verify / help` 的 stdout 保留命令输出语义，应急 stderr 沿现有独立预算。Flutter 的实际本地入口与规则仍单列核对。

### 10.2 首版诊断字段建议

| 字段 | 首版取舍与生成边界 |
| --- | --- |
| 事件身份、等级、服务与安全计数 | 首版必需。Job 事件码、`jobKind`、`outcome`、计数与 `durationMs` 来自已登记的摘要生成点，实际消费的异常经 `RuntimeFailureSummary.Classify` 生成封闭 `failureKind` |
| `traceId / spanId / operationId` | 首版按已有契约有值时保留。trace / span 使用已绑定的 W3C 类型，operation ID 使用已登记的权威生成点，例如 Hangfire 状态观察器与 Outbox 状态写入。首组 Job 摘要不从普通 scope 继承关联值 |
| `requestId` | 后置。首版按既有 trace 查询。若需用用户提供的 `X-Correlation-ID` 查找无有效 trace 的请求，另从服务端 `HttpContext.TraceIdentifier` 定义格式与映射，并验证与 W3C trace 的区别 |
| `jobId` | 后置原始标识。任务失败关联已有 Hangfire / Outbox 各自派生的 `operationId`。若需关联批次摘要与调度记录，先验证排障场景及跨重试含义 |
| `tenantId` | 后置。首版已规划的查询依赖部署、服务与关联字段。租户级排障或权限另行确认，并从服务端解析且校验的租户上下文生成 |
| 安全异常类型及栈帧 | 后置。首版用 `failureKind` 和精确来源定位。若实际排障仍有缺口，另登记固定异常类别与受控帧标识，制定大小预算和允许来源 |

### 10.3 首组修复与本地验收

1. `BusinessJobSummary` 绑定固定生成身份。`BusinessJobLogSink` 在旧链代码 / 配置 WriteTo、配置 AuditTo 和候选链合并上下文后，按精确身份及三个已登记事件重建摘要。字段白名单剔除普通 scope 和无关字段，保留原时间、等级和类型化 trace / span。已登记的摘要固定为普通事件。同名事件继续按各自来源处理。
2. 本地修改限于共享 helper 与输出适配。保留五个入口的计数口径、锁释放、Job 返回、业务写入 / 事务和 Hangfire 重试，以及 WriteTo 消费输出故障、AuditTo 传播故障、候选有限应急的行为。
3. 正式回归覆盖旧 / 候选与 Development / Production、代码 console 与配置 WriteTo / AuditTo、固定身份、同名异来源、scope 污染、空批次、部分失败、计数、输出故障和锁释放。真实任务、介质和 collector 联验在获批的目标环境另行执行。见[实施记录](../records/planning-closeout-2026-11-10.md#l2-job-output-implementation)。

<a id="l2-lottery-job-output"></a>

### 10.4 抽奖 Job 最终输出与本地验收

`PostLotteryJob` 已有独立的类型化来源。修复前，`BusinessJobLogSink` 只处理 `BusinessJobSummary`，抽奖摘要会继承外层 scope。候选 JSONL 会输出合法但不属于抽奖摘要的字段，且 Production 下外层 `Diagnostic=true` 会使失败摘要整条消失。见[本地核验记录](../records/planning-closeout-2026-11-10.md#l2-lottery-job-output-audit)。

1. 复用 `BusinessJobLogSink` 和既有日志接线，为精确来源 `Radish.Service.Jobs.PostLotteryJob` 定义独立的事件和字段白名单。只登记 `job.batch.completed`（Information、`succeeded`）与 `job.batch.failed`（Error、`partial` / `failed`）。输出采用固定模板、`sourceCategory=job`、`jobKind=post-lottery`、本批次的 `processedCount` / `failedCount` / `durationMs`，失败事件另保留封闭 `failureKind`。两条均为普通事件，保留原时间及类型化 trace / span，排除 scope 的 `operationId`、普通上下文和原异常。该来源的未登记事件按安全通用事件处理，同名异来源保持各自归属。
2. 保留现有扫描条件、顺序、批量边界、帖子去重、返回值和单项失败后继续处理。开奖服务的事务、审计、Outbox 和 Hangfire 的扫描异常归属保持原行为。配置 WriteTo 消费 sink 故障，AuditTo 保留异常传播，候选输出故障沿用有限应急。
3. 定向回归覆盖两种环境的配置 WriteTo / AuditTo 与候选 JSONL、旧链代码 console、scope 污染和同名异来源。验证完成、部分失败、全部失败、空批次、扫描异常、批量边界与去重，并复跑抽奖业务回归。见[实施记录](../records/planning-closeout-2026-11-10.md#l2-lottery-job-output-implementation)。真实调度、生产日志介质和 collector 留到对应环境验收。

<a id="l2-cleanup-job-output"></a>

### 10.5 文件及记录清理 Job 最终输出与本地验收

修复前，文件摘要未绑定 `SourceContext`，外层已有业务 Job 来源时会被其他适配器重建为 `runtime.unclassified`，丢失原事件和计数。三个记录清理 Job 的类型化来源稳定，但同样会带出外层字段。生产候选链会因外层 `Diagnostic=true` 丢掉文件失败、记录完成和收件箱容量告警。见[核验记录](../records/planning-closeout-2026-11-10.md#l2-cleanup-job-output-audit)。

1. 文件摘要固定 `Radish.Service.Jobs.FileCleanupJob` 来源。`CleanupJobLogSink` 在旧链 WriteTo / AuditTo 和候选输出中按下表重建登记事件，固定 `SourceCategory=job` 和 `Diagnostic=false`，保留类型化 trace / span 及 Serilog 事件的原等级和时间戳。精确来源的未登记事件用安全通用摘要处理，同名事件继续按各自来源处理。
2. 保留文件移动 / 删除和原计数口径、分片排除、引用保护、空批次安静及已消费失败的返回行为。保留记录仓储调用、结果对象和异常外抛，由 Hangfire 按原策略重试。WriteTo、AuditTo 和候选链各自沿用现有输出故障行为。上传会话、文件访问令牌和 Hangfire 自有日志继续按原分组处理。
3. 定向回归覆盖七个入口、两种环境的配置 WriteTo / AuditTo、代码 console 和候选 JSONL。已验证来源错配、无关字段、生产过滤、收件箱两事件的字段隔离、空批次和异常归属。目录失败警告用适配器输入验证，真实目录权限故障、生产日志介质、Hangfire 调度及 collector 留到对应环境验收。见[实施记录](../records/planning-closeout-2026-11-10.md#l2-cleanup-job-output-implementation)。

| 来源 | 登记事件与字段 |
| --- | --- |
| `FileCleanupJob` | 保留完成、警告、失败。`jobKind` 限于 `file-deleted / file-temp / file-recycle / file-orphan`，保留对应 `outcome`、`durationMs` 和原七项计数 `processedCount / movedCount / missingCount / skippedCount / removedDirectoryCount / failedCount / directoryFailureCount`。失败或含真实目录清理异常的警告保留封闭 `failureKind`，仅缺失文件的警告排除该字段 |
| `NotificationInboxCleanupJob` | 固定 `jobKind=notification-inbox`。完成只保留 `relationCount / groupCount / notificationCount / durationMs`。`job.cleanup.capacity_warning` 只保留聚合 `count`，对应本批容量告警条数 |
| `WikiDraftPayloadCleanupJob` / `ChatMessageReactionOperationCleanupJob` | 各自固定 `jobKind=wiki-drafts / chat-reactions`，完成只保留 `count / durationMs` |

<a id="l2-service-cleanup-output"></a>

### 10.6 上传会话及文件访问令牌清理最终输出与本地验收

修复前，后台两条任务和前台清理失败的 `ServiceCleanupSummary` 缺少固定 `SourceContext`。外层来源为 `FileCleanupJob` 时，服务的完成事件被误按文件摘要处理，丢失更新计数。中断和前台失败可被归为 `runtime.unclassified`，其中中断在生产候选链消失。外层 `Diagnostic=true` 也会使原本正常的完成、中断和失败事件消失。附件 Controller 的同名 `upload.cleanup.failed` 已独立适配。见[核验记录](../records/planning-closeout-2026-11-10.md#l2-service-cleanup-output-audit)。

1. 批次 `Dispose` 和 `RecordStandaloneFailure` 均使用固定 `Radish.Service.Internal.ServiceCleanupSummary` 来源。`ServiceCleanupLogSink` 在旧链代码及配置 WriteTo、配置 AuditTo 和候选 JSONL 的上下文合并后，按下表重建这一精确来源。登记事件固定 `Diagnostic=false`，保留类型化 trace / span 及 Serilog 事件的原等级和时间戳。未登记的组合使用安全通用摘要。
2. 保留原计数口径、空批次安静、跨租户条件更新、分片目录宽限和配额结算去重。中断只报告此前进度，仓储异常由 Hangfire 处理。输出故障沿用既有语义，配置 AuditTo 写入失败仍可覆盖中断路径正在传播的仓储异常。附件 Controller 的同名事件、分片状态回写的两个 `upload.session.*` 事件沿各自生成和适配边界处理。
3. 回归覆盖两环境的旧链配置 WriteTo / AuditTo、代码 console 和候选 JSONL。已验证成功、已消费失败、先有进度后中断、空批次、前台三个操作、附件 Controller 同名事件、上下文覆盖和输出故障。真实权限故障、Hangfire 调度、生产日志介质和 collector 留到对应环境验收。见[实施记录](../records/planning-closeout-2026-11-10.md#l2-service-cleanup-output-implementation)。

| 生成路径 | 登记事件与字段 |
| --- | --- |
| `upload-sessions` 批次 | 完成、中断为 Info，已消费失败为 Error。`SourceCategory=job`，保留该 `jobKind`、对应 `outcome`、`durationMs` 和原六项计数 `processedCount / updatedCount / removedDirectoryCount / settlementCount / skippedCount / failedCount`。只有失败事件保留封闭 `failureKind` |
| `file-tokens` 批次 | 只登记完成和中断，使用同一计数结构和 `SourceCategory=job`，保持真实撤销、已处理及并发跳过的区别。中断为 Info，剔除外层 `failureKind` |
| 前台 `RecordStandaloneFailure` | 只登记 `upload.cleanup.failed` Error，固定 `SourceCategory=application`。仅保留封闭的 `cleanupOperation`（`directory / quota-release / quota-complete`）和 `failureKind` |

<a id="l2-outbox-output"></a>

### 10.7 Outbox 最终输出与本地验收

修复前，分派的类型化来源稳定，但会继承外层关联值、分类和无关字段。状态摘要由 Repository 绑定权威 `operationId`，缺少固定来源时可能被其他适配器重建为 `runtime.unclassified`，连同状态字段一起丢失。生产候选链在外层 `Diagnostic=true` 时会丢掉三类事件。直接调用 Repository 时也能继承非本次失败的 `failureKind`。见[核验记录](../records/planning-closeout-2026-11-10.md#l2-outbox-output-audit)。

1. 保留 `Radish.Api.Services.ReliableOutboxDispatcherJob` 类型化来源，为状态摘要固定 `Radish.Repository.ReliableOutboxRepository` 来源。在旧链代码及配置 WriteTo、配置 AuditTo 和候选 JSONL 合并上下文后，按下表重建已登记事件。固定 `SourceCategory=job` 和 `Diagnostic=false`，保留类型化 trace / span 及 Serilog 事件的原等级与时间戳。未登记的组合使用安全通用摘要。
2. `ReliableOutboxService` 使用现有分类器，经仓储接口把可选的 `failureKind` 显式交给 Repository。Repository 在事件中绑定这一值，包括缺省的空值，最终适配只输出合法分类。保留仓储直接调用路径，并保留 `operationId` 的原算法和库域。状态关联只取 Repository 绑定的规范 GUID。
3. 保留分派入队计数、条件更新影响行后才生成状态事件、零行和重复执行安静、既有审计字段、重试和死信规则。回归覆盖 main / chat SQLite、两种环境的最终出口、来源错配、生产过滤、事件字段、入队和输出故障。WriteTo、AuditTo 和候选沿用现有故障语义。租约归属、实际调度、PostgreSQL 并发和生产采集链路依各自边界处理。

| 精确来源 | 登记事件与字段 |
| --- | --- |
| `ReliableOutboxDispatcherJob` | `outbox.dispatched` Info，仅保留 `count / durationMs`。分派是入队数，和执行成功数分别统计 |
| `ReliableOutboxRepository` | `outbox.retrying` Warning 和 `outbox.dead_letter` Error，仅保留 `databaseScope / attempt / outcome / failureKind` 及由本次状态写入方生成的 `operationId` |

2026-11-11 所有者确认上述范围，已由 `OutboxLogSink` 和显式分类参数完成本地实现。旧链代码 console、两环境的配置 WriteTo / AuditTo 与候选 JSONL，以及 main / chat SQLite 的状态转换、零行更新和输出故障回归通过；结果及未执行边界见[实施记录](../records/unified-logging-l2-outbox-output-2026-11-11.md)。奖励、币与经验的核验和本地实现见第 10.8 节，不重做已完成切片。

<a id="l2-reward-output"></a>

### 10.8 奖励、币与经验最终输出与本地验收

2026-11-11 基于 `38d15bd3`，六个生成来源的实际方法经旧链 WriteTo / AuditTo、代码 console 和候选 JSONL 核验。普通上下文可污染字段；外层来源为 `ServiceCleanupSummary` 时事件被重建为该来源的 unknown；外层 `Diagnostic=true` 可使生产候选链丢掉事件。修复前证据见[核验记录](../records/unified-logging-l2-reward-output-audit-2026-11-11.md)。2026-11-11 所有者批准以下范围，已由固定生成来源和 `RewardLogSink` 完成本地实现；输出契约见[第 83 节](./unified-logging-contract.md#l2-reward-final-output)。

1. 在下表六个生成点绑定各自固定 `SourceContext`，复用现有日志接线，在合并上下文后的最终输出按精确来源、事件码、等级及业务枚举重建。登记事件固定 `SourceCategory=application`、`Diagnostic=false`，仅保留本事件所需字段与类型化 trace / span；Serilog 重建保留原时间和等级，JSONL 继续沿公共时间策略。不继承普通 scope 的 `operationId`、自由文本、无关计数或失败分类。
2. 只给实际消费异常的事件保留封闭 `failureKind`；重试、完成摘要、幂等记录缺失和排行榜缺失用户不继承外层失败。未知 / 缺失 / 跨来源身份、错误等级与未登记组合使用安全 unknown，低等级依原诊断门禁处理；邻接来源不匹配。保持原生成点的结果、计数和异常所有权，不用最终适配重新推断业务成功。
3. 保留币三次及经验六次重试、延迟与抖动、发放 / 幂等 / 调账 / 转账、审计、事务和 Outbox 语义。经验单项已消费失败仍只在单项记录，批次 completed 可表示拒绝或部分结果；资金写入后幂等完成重试成功仅表示调用返回。查询失败返回 true / 0、坏快照空列表、分页和缓存回退均沿原边界，不借日志修改业务契约。
4. 保留 WriteTo 消费输出失败、AuditTo 传播故障、候选有限应急。已观察到 AuditTo 的 sink 故障可使经验单项不再返回 false、缓存读取失败后不再执行数据库回退，以及批次完成后向调用方抛错；这些是本次核验到的既有语义，本批保持该语义。

| 固定来源 | 登记事件与允许字段 |
| --- | --- |
| `Radish.Service.RewardRuntimeLog` | `reward.retrying` Warning：`rewardDomain=coin / experience`、attempt、delayMs；`reward.failed` Error：固定 experience、`rewardOperation=grant / grant-once / initialize`、failureKind；`reward.conflict` Warning：固定 experience / grant-once、failureKind；`reward.cache_fallback` Warning：固定 experience、`cache-read / cache-write / cache-invalidate`、failureKind；`reward.batch_completed` Info / `reward.batch_failed` Error：coin / experience、processedCount / rejectedCount / failedCount、与原结果一致的 outcome，仅失败摘要保留 failureKind |
| `Radish.Service.CoinService` | `coin.transfer_completion_recovered` Warning：仅 failureKind |
| `Radish.Service.OperationIdempotencyService` | `idempotency.completion_missing` Warning：仅 `completionKind=success / failure` |
| `Radish.Service.CoinRewardService` | `reward.existence_check_failed` Error：仅 failureKind |
| `Radish.Service.ExperienceService` | `experience.rank_query_failed` Error 和 `experience.governance_snapshot_invalid` Warning：仅 failureKind；`experience.leaderboard_incomplete` Warning：仅 skippedCount |
| `Radish.Extension.ExperienceExtension.ExperienceCalculator` | `experience.calculator_cache_fallback` Warning：`rewardOperation=cache-read / cache-write / cache-invalidate`、failureKind |

本地回归覆盖六个精确来源、13 个事件码、各合法领域 / 操作组合和非法组合；两环境的配置 WriteTo / AuditTo、代码 console 与候选 JSONL，scope 污染、真实方法返回 / 写入调用、批次计数、重试耗尽及恢复、正常安静、同名异来源、未知事件、类型化关联和输出故障。复跑奖励、币流转、账户治理、奖励入口、经验查询治理及现有 Outbox / Controller 关联回归。事务 / 仓储 / 缓存替身不替代真实数据库、Redis、调度或 collector 验收；不扩入商城、库存、支付口令及其他下一分组。

实现与验证结果见[本批实施记录](../records/unified-logging-l2-reward-output-audit-2026-11-11.md#implementation)。本组本地切片完成不关闭 L2 整体或真实链路门禁。

<a id="l2-shop-output"></a>

### 10.9 商城、库存与支付口令最终输出与本地验收

2026-11-11 基于 `4200e6d9` 核验四个生成来源。商城 / 库存三个 Serilog 来源缺少固定身份，外层来源可使其误归为 unknown；支付口令已有类型化来源。修复前四个来源都存在最终字段污染和生产诊断过滤缺口，见[核验记录](../records/unified-logging-l2-shop-output-audit-2026-11-11.md)。2026-11-11 所有者确认以下范围，已由固定生成身份和 `ShopLogSink` 完成本地实现；输出契约见[第 84 节](./unified-logging-contract.md#l2-shop-final-output)。

1. `OrderService`、`ProductService`、`UserInventoryService` 在现有日志生成点绑定各自固定 SourceContext。`PaymentPasswordService` 保持类型化 logger，将两个局部事件 scope 改为命名 EventId：`1/PaymentLocksCleared`、`2/PaymentSuggestionsFailed`，最终映射下表事件；不让普通 scope 的 EventCode 决定支付口令事件身份。
2. 新增本组 `ShopLogSink`，复用旧链 WriteTo / AuditTo 与候选链已有接线，在上下文合并后按四个精确来源、事件身份、等级及合法 purchaseStage 重建。登记事件固定 `SourceCategory=application`、`Diagnostic=false`，采用固定模板，仅保留下表字段、类型化 trace / span 及 Serilog 原时间 / 等级；JSONL 时间沿公共策略。普通 operationId、字符串关联键、原异常对象、业务载荷和跨事件字段剔除。
3. 未知 / 缺失 / 跨来源身份、错误等级和非法购买阶段使用安全 `runtime.unclassified`；低等级沿既有诊断门禁，邻接来源不匹配。数值仅接收 int：attempt / delayMs 沿现有公共上界 1,000,000 / 60,000 且非负，processedCount 只保留正数；非法数值与非法 failureKind 丢弃。不按计数重新解释业务成功，不给无异常的配置拒绝、重试或清理数量继承 failureKind。
4. 保留库存五次尝试和退避、现有冲突识别、购买支付 / 履约阶段、库存回补与已售数、订单 FailReason / 幂等 / Outbox、历史权益快照、道具条件扣减与权威操作记录。保留改名 ArgumentException 安静、InvalidOperationException 包装 400、其他异常交直接消费者。口令算法、旧版本升级、五次失败锁定 30 分钟、审计与建议回退文字不变。
5. 保留 WriteTo 消费输出失败、AuditTo 传播及候选有限应急。已复现 AuditTo 可中断库存重试、在支付失败回补前抛错、阻止改名 400 包装及建议回退、在清理已经返回仓储数量后抛错；本批不借日志隔离改变这些既有语义。业务事务与响应改造需独立范围。

| 精确来源 | 登记事件、等级及允许字段 |
| --- | --- |
| `Radish.Service.ProductService` | `product.configuration_rejected` Warning，无扩展字段；`product.stock_retrying` Warning，仅 attempt / delayMs |
| `Radish.Service.OrderService` | `order.purchase_failed` Error，仅 `purchaseStage=payment / fulfillment` 与 failureKind；`order.purchase_interrupted` Error，仅 failureKind |
| `Radish.Service.UserInventoryService` | `inventory.rename_rejected` Warning，仅封闭 failureKind |
| `Radish.Service.PaymentPasswordService` | `1/PaymentLocksCleared → payment.locks_cleared` Information，仅 processedCount；`2/PaymentSuggestionsFailed → payment.suggestions_failed` Error，仅 failureKind |

本地回归覆盖四个来源、七个事件码及两种购买阶段；两环境的配置 WriteTo / AuditTo、代码 console 和候选 JSONL；scope / MEL scope 污染、固定身份、未知与错配事件、非法字段、类型化关联、正常安静、库存重试恢复 / 耗尽、支付 / 履约及独立补偿失败、改名包装与 Controller 消费、口令建议和清理结果、输出故障。复跑商城履约 / 管理 / 权益与口令日志、相关服务及 Controller / Outbox 关联回归。

`UserBenefitService` 无保留日志，不增加输出；已完成的 ShopController / PaymentPasswordController 适配只作关联防回归。真实库存 / 资金事务、PostgreSQL 并发、介质和 collector 仍需对应环境证据。接口、表结构、依赖、生产开关与下一组公开查询 / 报表不进入本批。

实现、验证结果与未执行边界见[本批实施记录](../records/unified-logging-l2-shop-output-audit-2026-11-11.md#implementation)。本地切片完成不关闭 L2 整体或真实链路门禁。

<a id="l2-public-query-output"></a>

### 10.10 公开查询与报表最终输出与本地验收

2026-11-12 基于 `edd47101` 核验四个来源、11 个事件。修复前，API 两个来源的六个事件可能被相邻来源适配器改写为 `runtime.unclassified`。四个来源均继承外层字段，`Diagnostic=true` 可使生产候选链丢掉全部登记事件。所有者确认以下范围，已完成本地实施，见[输出契约第 85 节](./unified-logging-contract.md#l2-public-query-final-output)及[本批记录](../records/unified-logging-l2-public-query-output-audit-2026-11-12.md#implementation)。

1. 为两个 API Service 的六个 Serilog 生成点绑定各自类型的 `SourceContext`，继续使用显式事件码。两个 Gateway 类型保留类型化 logger，为五个事件使用固定的命名 `EventId`，以数值和名称共同识别，去掉仅用于标记事件身份的 scope。
2. 在旧链代码 / 配置 WriteTo、AuditTo 和候选 JSONL 的既有适配链共用 `PublicMetadataLogSink`。按精确来源、事件身份和等级重建固定摘要，登记事件固定为非诊断。API 分类在两链统一为 `application`，旧链此前为 `business`。Gateway 使用 `http`。错配身份和等级沿现行策略输出受控 unknown，保留原等级及低级别诊断门禁。
3. 九个失败摘要只接收封闭 `failureKind`，两个不可用事件只接收符合其分支的整数 HTTP 状态。保留原 Serilog 事件时间和类型化 W3C trace / span（含当前 Activity）；自由 scope 中的关联字符串、`operationId`、额外字段、异常和原始载荷均不进入该摘要。

| 精确来源 | 事件与允许字段 |
| --- | --- |
| `PublicHeadSnapshotService` | `public_head.cache_read_failed`、`public_head.cache_write_failed` 均为 Warning，仅 `failureKind` |
| `PublicSitemapService` | `sitemap.cache_read_failed`、`sitemap.cache_write_failed`、`sitemap.section_count_failed` 均为 Warning，`sitemap.generation_failed` 为 Error，均仅 `failureKind` |
| `PublicHeadSnapshotClient` | `public_head.snapshot_unavailable`、`public_head.html_unavailable` 均为 Warning，仅 `statusCode`；`public_head.snapshot_request_failed`、`public_head.html_request_failed` 均为 Warning，仅 `failureKind` |
| `PublicHeadSnapshotMiddleware` | `public_head.injection_failed` Warning，仅 `failureKind` |

验证覆盖真实生成方法、正式出口、两种 scope、诊断开关、错配与非法字段、无 Activity 与类型化关联、代码 console 及输出故障语义。回归缓存和 TTL、sitemap 上次成功 / 空 XML / 部分索引、Gateway 404、JSON 异常原样传播、注入失败后的后续管道，以及 API 与 Gateway 各自的异常归属。公开发现流、排行榜、统计报表及已收口 API 异常处理器只作关联回归。缓存、HTTP 响应、接口、数据模型、依赖和生产开关沿用现有行为与配置，真实介质及采集仍归对应批次。

<a id="l2-notification-realtime-output"></a>

### 10.11 通知与实时推送最终输出与本地验收

2026-11-12 基于 `818ba299` 核验四个类型化来源、五个事件。修复前，已输出事件的身份及等级保持正确，旧链继承外层任意字段，候选 JSONL 继承无关白名单字段和关联值，外层 `Diagnostic=true` 可使生产候选丢掉所有五类事件。所有者确认以下范围，已完成本地实施。见[事件契约第 86 节](./unified-logging-contract.md#l2-notification-realtime-final-output)及[实施记录](../records/unified-logging-l2-notification-realtime-output-audit-2026-11-12.md#implementation)。

1. 沿用四个 `ILogger<T>` 类型来源。五个生成点改用固定的命名 `EventId`，去掉仅标记事件身份和分类的 scope。按精确类型、数值、名称和原等级共同识别，普通 EventCode 不补齐身份。
2. 在上下文合并后接入 `NotificationRealtimeLogSink`，覆盖旧链代码 / 配置 WriteTo、AuditTo 和候选 JSONL。固定摘要、`application` 分类和非诊断标记，旧链此前为 `business`。扩展字段仅允许封闭 `failureKind`。保留原 Serilog 时间及类型化 W3C trace / span，去掉自由 scope 中的关联值、`operationId`、额外属性、异常和原载荷。精确来源的错配身份或等级进入受控 unknown，沿原等级与低级别诊断门禁。

| 来源 | 命名 EventId 与事件 |
| --- | --- |
| `UserFollowService` | `1/FollowNotificationEnqueueFailed` → `user_follow.notification_enqueue_failed` Error |
| `NotificationPushService` | `1/NotificationPushFailed` → `notification.push_failed` Warning |
| `UserInteractionRealtimeNotifier` | `1/ChatRelationshipPushFailed` → `user_interaction.chat_push_failed` Warning，`2/NotificationRelationshipPushFailed` → `user_interaction.notification_push_failed` Warning |
| `CommentRealtimePushService` | `1/CommentPushFailed` → `comment.push_failed` Warning |

验证覆盖两环境的正式出口和代码 console、两种 scope、无 Activity / 类型化 Activity、错误身份或等级、非法字段和 sink 故障。保留关注关系与通知入队的既有提交边界、400 / 404 与上抛所有权、通知持久化及幂等、Outbox 成功和失败分工、双 Hub 和多接收者发送顺序、评论成功结果及后续高亮推送。WriteTo 消费输出故障，候选 writer 走有界应急，AuditTo 保留传播及其中断现有调用的行为。轻回应、已收口 Controller、Outbox 和 SignalR 框架路径仅做关联防回归。Hub 自有生命周期、业务事务调整及真实网络 / 介质 / collector 进入各自批次。
