# 日志 L1 / L2 剩余门禁与 L3-A 接收入库方案

> 2026-11-05（Asia/Shanghai）。状态：**已批准实施，本地实现与定向验证已完成**。准备基线为 `dev` 的 `fee65b97`；结果见[实施记录](../records/unified-logging-l3-ingest-local-2026-11-05.md)。只使用内存 HTTP 和临时 SQLite；未启动项目服务、容器或连接 PostgreSQL。
>
> 本页属于[统一日志专题](./unified-logging-governance-design.md)，承接[事件契约](./unified-logging-contract.md)。已完成来源仍以事件契约为准；本页维护剩余门禁及下一批实施边界，不另建主线。

## 1. 推荐顺位与交付结论

本批已实施 **L3-A：受控内网接收与幂等持久化**，交付默认关闭、可独立测试的接收链，并纳入 Log 连接直接相关的防递归修正。随后串行处理第 3 节剩余自有来源，再完成 L1 传输 / 磁盘 / 平台门禁及 L3-B 部署、留存和恢复。L4 查询、L5 告警与 L6 正式切换仍按主专题推进。

L2 未整体关闭不阻止用合成安全事件实现和验证 L3-A；但它阻止把全宿主真实日志接入直接认定为安全完成。L1 上界未关闭时，接收端只能是受控候选，不能开启 production Compose 汇聚。上述顺位保持一个日志专题串行推进，不同时开展 Native 或客户端连接实施。

准备核对确认：

- `Scripts/logging/collector-mock.mjs` 将请求累积到内存并追加文件，没有真实事务、认证或幂等存储，不能复用为产品接收器。
- 产品 API / Model / Repository / Service / DbMigrate 中尚无该内网接收与存储实现；Common 已存在生成契约 `Radish.Common.LogTool.RuntimeLogEvent`，新增持久实体需避免同名歧义。
- `SqlSugarSetup` 已排除 Log 库的普通 SQL AOP；但 SQLite PRAGMA 失败仍调用带异常的普通 `Log.Warning`，不能以 AOP 排除推定整条入库链绝不递归。
- `BaseLog` 带旧表的本地时间默认值与分表语义，新运行事件不能直接继承它。
- 浏览器本批、Hangfire 两来源、Common 两处异常诊断及既有框架来源保留已完成结论，不重复实施。

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

以下是本次源码复核确认仍保留旧调用的有限处理队列，不是 2026-09-19 数量快照的复用，也不是全仓运行覆盖证明。实现前核对实际消费者和最终出口；移除无用输出不改变业务分支。

| 来源 / 代码入口 | 当前缺口 | 后续关闭要求 |
| --- | --- | --- |
| `Radish.Api/Controllers/UserController.cs` 的当前用户组装 | 正常过程多条 Info；权限 / 头像降级记录异常与用户标识 | 正常过程关闭或显式诊断；固定降级事件，保留空权限集 / 忽略头像的既有返回语义 |
| `Radish.Api/Controllers/ClientController.cs` | 管理查询异常与创建 / 更新 / 软删除 / 密钥重置日志仍为旧调用 | 固定动作结果与安全失败摘要；核对 OpenIddict manager 的异常所有权；禁止密钥、客户端自由文本进入输出 |
| `Radish.Service/BootstrapService.cs` | 首管理员奖励失败含异常与 UserId | 固定部分完成 / 奖励降级摘要，不能宣称整个注册回滚或奖励已发放 |
| `Radish.Service/NotificationDedupService.cs` | 三类缓存失败及正常 debug 包含身份 / 业务键 | 保留检查失败放行、写入 / 删除失败消费语义；去掉动态键与异常原文，核对直接消费者是否重复记录 |
| `Radish.Service/ChatMessageSearchService.cs` | 每次成功搜索生成 Info | 正常搜索安静，必要耗时摘要受控；不记录关键词或查询内容，不调整搜索结果 |
| `Radish.Extension/RateLimitExtension/RateLimitSetup.cs` | 注册明细、IP 黑白名单输出及拒绝日志仍走旧调用 | 合并注册摘要，受控拒绝 / 诊断字段；高频拒绝不能直接变成逐请求告警。限流决策和响应保持，既有黑名单逻辑问题如需修复另立范围 |
| `Radish.Extension/ExperienceExtension/ExperienceExtensionSetup.cs` | 无事件分类的扩展注册 Info | 移除重复注册过程或归入显式开发诊断 |
| `Radish.Extension/SqlSugarExtension/SqlSugarSetup.cs` 的 `ExecuteSqlitePragma` | catch 将异常和 SQL 交给普通 logger | 固定操作枚举 / failureKind，Log 连接故障不得重入数据库 sink；保留 false 返回和初始化行为。本项已随 L3-A 本地实施完成，真实数据库初始化故障仍保留运行验收 |

其他必须保留的退出工作：

1. **已登记业务来源最终出口核验**：生成端安全不代表旧 sink 上继承的任意 scope 已受控。按现有注册来源建立“生成点 → 消费边界 → 旧 / 候选输出 → 证据”的清单；已有完整证据直接引用，只补缺口，不重新调查任意外部订阅器。
2. **诊断信息完整性**：现行契约只有固定 `failureKind`，安全异常类型 / 栈帧及 `requestId / jobId / tenantId` 权威上下文尚未全齐。继续保持字段缺失，不在 L3 猜测、重造或放开自由文本；后续作为明确契约切片确认，不默认为已实现。
3. **裸输出例外**：DbMigrate doctor / verify / help 的 stdout 是命令协议，应急 stderr 有独立预算；`ConsoleHelper.WriteLine` 本次未检出外部调用，保留为待明确处置项，不当作已证明可达漏洞。检查真实调用与例外表后才决定删除或限制。
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

项目所有者已批准第 4–5 节的 L3-A 代码、迁移、临时 SQLite / 内存管道验证与文档收口；包括直接相关 PRAGMA 日志防递归，不包括第 3 节其余来源实现。服务与容器启动另行确认。

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
