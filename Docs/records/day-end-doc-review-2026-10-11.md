# 2026-10-11 日终代码与文档回顾

> 时间口径：Asia/Shanghai。审阅当日全部 11 个开发提交（`5919fc49` 至 `06ba904e`，对照基线 `1d8b66fd`）；本次另作纯文档收尾提交。项目所有者要求今天到此结束，后续建议写入 10 月 12 日事项。

## 今日提交与批次索引

按提交时间从早到晚。数量引用各批当时通过的 .NET 回归，包含重叠用例，不相加为独立总数，也不表示日终再次执行。

| 提交 | 代码变化与对照结论 | 批次证据 / 当时通过数 |
| --- | --- | --- |
| `5919fc49` | CertificatePathWatcher 与 Core.KestrelServer 两个精确类别，增加 28 个编码，保留配置加载与证书行为 | [证书监视 / 配置](./unified-logging-l2-certificate-configuration-2026-10-11.md) / 新增 65，扩大 531 |
| `b265d8b5` | 10 个 Controller catch 与评论失败元组先构造后记录；通知来源通过内部异常交接，不提前读取原 Message | [早期消费点](./unified-logging-l2-early-consumer-ownership-2026-10-11.md) / 定向 100，扩大 329 |
| `5fd4d050` | 服务端 Socket / QUIC、Http.Connections 六来源及协议 resolver，增加 99 个编码 | [传输 / 协议](./unified-logging-l2-transport-protocol-2026-10-11.md) / 新增 142，扩大 661 |
| `3b75b248` | YARP 转发六来源、四个命名 HttpClient 的八个 handler 与 DefaultHttpClientFactory，增加 75 个编码 | [出站 HTTP](./unified-logging-l2-outbound-http-2026-10-11.md) / 新增 112，扩大 773 |
| `1c64ceda` | YARP 配置加载 / 管理 / 校验三个精确来源，增加 18 个编码 | [YARP 配置](./unified-logging-l2-yarp-configuration-2026-10-11.md) / 新增 71，扩大 844 |
| `0298f386` | 条件性亲和 / 健康检查七个精确来源，增加 33 个编码，保留策略和失败语义 | [YARP 亲和 / 健康](./unified-logging-l2-yarp-affinity-health-2026-10-11.md) / 新增 86，扩大 930 |
| `ada43019` | JWT / Cookie / 授权四个框架来源及 API 两处 JWT 回调，增加 24 个编码，保留 Hub token 提取与 401 / 403 响应 | [认证 / 授权](./unified-logging-l2-authentication-authorization-2026-10-11.md) / 新增 88，扩大 1052 |
| `95d812df` | endpoint routing / matcher / link generator 四个精确来源，增加 32 个编码 | [路由](./unified-logging-l2-routing-2026-10-11.md) / 新增 69，扩大 1121 |
| `497e2934` | 宿主健康服务一个精确来源、7 个编码及 ILogger 桥接前保护，保持健康结果、响应和取消语义 | [宿主健康服务](./unified-logging-l2-host-health-2026-10-11.md) / 新增 63，扩大 1192 |
| `40028aa9` | OpenIddict Server 两个精确来源、185 个编码与桥接前保护，保持认证和响应语义 | [OpenIddict Server](./unified-logging-l2-openiddict-server-2026-10-11.md) / 新增 73，扩大 1294 |
| `06ba904e` | OpenIddict Core 三个 manager 精确来源、21 个编码与保护扩展，保持存储 / 缓存及 Server 消费语义 | [OpenIddict Core](./unified-logging-l2-openiddict-core-2026-10-11.md) / 新增 46，扩大 1340 |

原批次保留实现方案、来源依据、测试命令和限制。历史“下一步”只表示当时顺序，当前范围以[事件契约第 56–66 节](../features/unified-logging-contract.md)及[当前规划](../planning/current.md)为准。

## 代码与文档对照

审阅当日生产代码差异、共享事件策略、测试覆盖与批次记录，并按调用链核对日志、敏感数据、认证、Gateway、轻回应与规划文档。这是本日改动的文档复核，不是全仓安全审计。

1. **公共策略与实现对应**：基线至 `06ba904e` 净新增 522 个事件编码，未删除或修改既有事件及其他策略字段；早期消费点批次复用既有编码。事件契约第 56–66 节已记录准确身份和边界，无需再次复制事件表。
2. **输出保护分清层次**：框架适配器在 enrichment / LogContext 合并后安全重建，覆盖旧代码 / 配置 WriteTo / AuditTo 与候选输出；宿主健康服务及 OpenIddict 五个精确来源另有桥接前保护，不读取原 state、formatter 或 exception。更新日志与敏感数据指南，移除“Http.Connections / 证书 / Socket / QUIC 均待治理”的过期结论，保留相邻来源透传、调用前属性读取、BeginScope 及独立通道的限制。
3. **结果完成不混同响应完成**：早期消费点先构造错误结果，二次失败不提前记账；轻回应通知通过内部异常保留来源并交由 Create 消费。更新轻回应专题，明确数据库回滚不撤销先前缓存，本层构造完成也不保证 HTTP 写入完成；没有引入补偿、补投或成功重放。
4. **认证排障按实际日志校准**：修正 Auth 文件路径为 `Logs/Radish.Auth/`，慢数据库操作使用 `database.slow`，失败仍由最终消费边界记录；Account / IdleSession 自有日志尚未治理，不能把 OpenIddict 框架安全输出等同完整登录链路安全。认证指南只替换现有错误说明，未追加章节或扩大实现范围。
5. **Gateway 指南回到当前接线**：修正日志目录；实际未注册 `UseSerilogRequestLogging`，已治理 HTTP / YARP 来源输出受控事件，不能继续承诺完整异常与上下文。保留旧链启动示例并标明适用范围；临时级别覆盖遵循共享配置，生产候选 Debug / Trace 仍受策略过滤。移除另配 ELK / Loki / Seq 的分歧建议，链接已确认的统一日志汇聚方案。
6. **收拢入口并写明后续顺位**：当前规划、记录索引与治理设计链接本日回顾，全部 11 份原批次仍可由上表访问。根 AGENTS / CLAUDE、架构职责、前端错误处理和发布流程无需因本日实现改变，未修改这些入口。

## 已完成与保留边界

- 10 月 7 日记录中的早期消费点、证书配置、服务端传输与协议解析待办，已由今日对应批次推进；不能继续按旧记录视为全部未实现。
- 日志保护不改变 HTTP / SignalR / OIDC 响应、认证规则、健康探测结果、manager 存储 / 缓存、事务或证书验证；不同阶段不做全局日志去重。输出 sink 失败仍按 WriteTo / AuditTo 的原语义处理。
- OpenIddict Core 当前 Scope manager 没有直接日志调用，EF store 的 EF 框架来源不等同本批 manager；Client / Validation / DataProtection、自有 Auth 日志、IdentityModel 独立日志接口 / EventSource、SelfLog 与其他非 Serilog 通道继续单独核对。
- Socket `.Client` 当前无产品调用，HttpSys delegation 后置。日志生成器与内存行为测试不代表真实 Socket / QUIC、文件监视、健康探测、端到端登录或 OpenIddict 真实数据库 / 事务提交已验收。
- Reaction 已删除行恢复 409、SQLite 自动开奖 → Outbox 时间偏差、关注与入队事务分离、评论高亮部分写入、轻回应回滚后缓存、标签 / 分类非事务写入及 0 行成功语义，仍见[10 月 2 日未关闭边界](./day-end-doc-review-2026-10-02.md#独立问题与未关闭边界)。表情错误映射 / 文件副作用 / false-null 折叠见[10 月 7 日批次](./unified-logging-l2-sticker-controller-2026-10-07.md)，Rust `.tmp` 水印回退后置。
- 生产 `RadishLogging.Enabled=false`；L1 剩余传输 / 磁盘门禁、L2 整体及 L3–L6 未关闭。未因这些日志批次新增平台验收、发布或部署结论。

## 验证证据与限制

Core 提交前：新增测试 **46 / 46**、OpenIddict 合计 **119 / 119**、扩大日志 / 健康 / 认证回归 **1340 / 1340**，失败、跳过、未运行均为 0；Node 公共契约 **27 / 27**；solution 无还原、警告视为错误构建为 **0 警告、0 错误**。1340 项是末批命令筛选的回归，不能替代今日早期消费点的独立业务测试，也不是今日所有新增用例总和。

今日各批使用真实日志生成器、内存中间件 / handler / manager、受控存储与缓存替身；早期消费点另有 SQLite 事务回归，证书批次有隔离临时 PEM / 配置重载验证。宿主健康与 OpenIddict 接线通过 Host Build / Dispose 校验 factory 和 sink 所有权，旧链 SelfLog 文件初始化仍保留。每批证据与限制以上表原记录为准，不将替身测试升级为真实数据库、网络或部署证据。

日终只更新文档，不重复没有运行时改动的 .NET / Node 回归。`npm run check:repo-quality:changed` 与 `git diff --check` 通过：9 个变更文件、854 个文档 / 治理文件文本检查通过，814 个 Markdown 文件、814 个本地相对链接检查通过。认证指南既有 1197 行硬上限提醒保留，本次只替换过期说明、未新增行，全文拆分后续单独处理；Gateway 884 行、日志指南 780 行、记录索引 310 行为软提醒。Gateway 与记录索引分别从 896、323 行收拢，未因日终回顾扩展为全篇重组。

## 下一次恢复事项（2026-10-12）

1. 先只读审计 `IdleSessionOpenIddictHandlers` 与 `AccountController`，覆盖登录 / 注册、邮箱与身份字段、分段耗时、客户端解析 / returnUrl、异常消费和结果完成顺序；提出固定事件、受控字段与四模式验证方案，再确认实现。既有 `auth.seed.completed` 不重复治理。
2. 分开核对 OpenIddict 其他集成的实际接入与 IdentityModel / SelfLog / DiagnosticListener / 裸输出通道，不把类别相近视为同一覆盖范围，也不默认开启新的诊断输出。
3. 对照生产接线形成 L2 剩余覆盖清单，选定下一批完整范围。独立可靠性问题按专题确认，Native P8-C readiness 保持后续顺位。

今天到此停止实现。后续依赖、项目启动与真实运行仍按具体范围确认；本次没有执行 push、合并、tag 或部署。
