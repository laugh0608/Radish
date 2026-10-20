# 2026-10-15 日终代码与文档回顾

> 时间口径：Asia/Shanghai。审阅当日全部六个开发提交（`9655b716` 至 `c828b1a1`，对照基线 `a99d8608`）；本次另作纯文档收尾提交。项目所有者要求今天到此结束，后续建议写入当前规划。

## 今日提交与批次索引

按提交时间从早到晚。下表引用各批当时通过的测试数量，包含重叠用例，不相加为独立总数，也不表示日终再次执行。

| 提交 | 代码变化与对照结论 | 批次证据 / 当时验证 |
| --- | --- | --- |
| `9655b716` | 自有 Auth 账户与 IdleSession 共 24 处旧调用点，固定事件与安全摘要；保留认证规则和结果准备顺序 | [Auth 账户 / 会话](./unified-logging-l2-auth-account-idle-session-2026-10-15.md) / 新增 74，.NET 定向 368，Node 32 |
| `f5d62639` | 两条链统一 SelfLog 独立安全 stderr，覆盖 Session 配置读取、logger 初始化 / 释放边界；限频计数，不再追加原文文件 | [SelfLog](./unified-logging-l2-selflog-2026-10-15.md) / 新增 30，.NET 定向 77，Node 33 |
| `870254f9` | ASP.NET Core DataProtection 二十个精确来源、98 个固定编码，增加桥接前保护与最终输出重建 | [DataProtection](./unified-logging-l2-dataprotection-2026-10-15.md) / 新增 59，.NET 定向 473，Node 37 |
| `d3dc8e7e` | 三宿主 48 处旧调用点收敛为启动摘要，复用已选择状态，不为日志重读证书、地址或配置 | [宿主启动摘要](./unified-logging-l2-host-startup-2026-10-15.md) / 新增 25，.NET 定向 510，Node 41 |
| `91ada07b` | Common 两处程序集加载 / 类型枚举诊断改为独立安全 stderr，绑定早期宿主来源，隔离诊断二次失败 | [Common 诊断](./unified-logging-l2-common-assembly-diagnostics-2026-10-15.md) / 新增 19，.NET 定向 132 + CLI 6，Node 44 |
| `c828b1a1` | API 任务注册 / 固定文档启动同步与 Gateway 停止日志共 21 处调用点收口；保持原消费与释放所有权 | [Program 业务日志](./unified-logging-l2-program-operations-2026-10-15.md) / 新增 40，.NET 定向 242 + CLI 6，Node 48 |

原批次保留实现方案、测试命令与限制。批次记录中的“未扩展”是当时范围，后续实现与当前待办以[事件契约第 67–72 节](../features/unified-logging-contract.md)和[当前规划](../planning/current.md)为准。

## 代码与文档对照

审阅当日代码差异、公共策略、测试覆盖与批次记录，再沿调用链复核日志、敏感数据、认证、Gateway、Hangfire、固定文档同步和宿主观测说明。本次是改动相关文档复核，不是全仓安全审计。

1. **共享策略**：基线至 `c828b1a1` 净新增 120 个事件编码，未删除或修改既有编码正文；其他变化为新增 `runtime` 服务来源及受控属性。Node 执行共享契约验证，不表示浏览器 logger 已修改。
2. **认证指南**：移除“保留登录分段耗时”和“Account / IdleSession 尚未治理”的过期描述；改为固定账户事件、受控原因和结果准备总耗时，不能据此推断各查询阶段耗时或 HTTP 响应完成。现有登录、注册、IdleSession 和 OpenIddict 业务规则不变。
3. **敏感数据指南**：补齐 DataProtection 二十个精确类别的桥接前保护，分别说明自有 Auth / 启动业务最终输出重建、SelfLog 与 Common 独立 stderr；保留调用前求值、BeginScope、相邻类别和其他独立诊断通道的限制。
4. **Gateway 与宿主观测说明**：删除旧 ASCII banner、完整地址与 CORS 原文启动示例；按实际字段说明环境、状态和数量。同步 M14 运行指南与维护清单，明确启动通知不替代健康探测，停止通知不承诺后续释放成功。健康端点既有行为未改。
5. **Hangfire 指南**：注册实现已提取至 `ApiRecurringJobRegistration`，启动日志只表示 `AddOrUpdate` 返回。根据实际代码修正固定注册为四项、默认注册 15 项 / 全开 16 项；实际 Cron 与参数改由配置和 Dashboard 核对，不再要求从日志读取。任务表与默认计划保持对应，未改变任务执行语义。
6. **固定文档同步指南**：写明展示关闭前仍执行两次既有表存在查询，以及六种受控跳过原因；缺表的操作建议仍为先执行 `DbMigrate apply`，但日志不再输出旧自由文本。同步完成事件不保证后续 scope 释放成功，失败继续启动也不回滚此前副作用。
7. **入口收拢**：当前规划、记录索引和治理设计改为链接本日回顾，全部六份批次仍可由上表访问。保留 9 月现状审计和既有日终记录的历史口径；AGENTS / CLAUDE、架构职责、发布流程与客户端连接规划无需因本日实现修改。

## 已完成与保留边界

- 10 月 11 日记录中的自有 Auth、SelfLog，以及后续核对发现的 ASP.NET Core DataProtection、三宿主启动摘要、Common 两处诊断和 Program 业务日志，已由今日批次推进；不能再全部列作未治理。
- ASP.NET Core DataProtection 与 OpenIddict 的 DataProtection 集成是不同范围。后者及 OpenIddict Client / Validation 当前依赖存在但未注册；IdentityModel 默认空 logger、仓库无诊断订阅器，不等于外部 EventPipe / EventSource / DiagnosticListener / Activity / metrics 已被治理。Socket `.Client` 与 HttpSys delegation 继续后置。
- SelfLog 只在 Session 接管期输出固定摘要，不阻止 Serilog 在回调前格式化原文；历史原文文件未删除。限频、同步 stderr 和内存计数不提供 IO 超时、无损、恰好一次或退出必达保证。Common 只覆盖两处既有 catch，不新增扫描、反射或部分类型恢复策略。
- 注册返回、结果准备、同步方法返回和生命周期通知分别只证明对应观察点。日志保护不改变认证、数据库、任务、健康响应或事务语义；不同阶段不做全局去重，WriteTo / AuditTo 仍保留各自异常语义。
- 独立可靠性问题继续见[10 月 2 日未关闭边界](./day-end-doc-review-2026-10-02.md#独立问题与未关闭边界)及[表情管理批次](./unified-logging-l2-sticker-controller-2026-10-07.md)，Rust `.tmp` 水印回退后置。
- `RadishLogging.Enabled` 默认 false；L1 传输上界 / 磁盘故障、L2 整体、L3 入库、L4 查询、L5 Console 告警和 L6 切换仍未关闭。今日未新增平台、发布或生产部署验收结论。

## 验证证据与限制

各批均完成 solution 无还原、警告视为错误构建，结果为 0 警告、0 错误。末批提交前 .NET 日志 / 任务 / Wiki / 宿主定向回归为 **242 / 242**，进程退出 / CLI 为 **6 / 6**，Node 公共契约为 **48 / 48**；这些是对应筛选集，不代表今日所有用例总量。Auth 批次另通过 identity runtime + protocol 扫描；其首次沙盒扩大回归停滞未计为通过，最小提权重跑通过，过程保留在原记录。

证据覆盖四模式生成器、内存认证 / 密钥环 / SQLite、受控存储 / 服务替身、临时文件与子进程；没有启动项目服务、Hangfire worker、容器或监听端口，没有执行真实登录、浏览器 Smoke、生产存储 / 网络或系统证书库操作。returnUrl 解析 catch 仍仅有固定生成方法证据；DataProtection Windows / 注册表 / DPAPI 等来源的生成器覆盖不等于平台运行验收。

日终仅更新文档，不重复运行没有代码变化的 .NET / Node 回归。`npm run check:repo-quality:changed` 与 `git diff --check` 通过：12 个变更文件、862 个文档 / 治理文件文本检查通过；822 个 Markdown 文件、870 个本地相对链接检查通过。认证指南既有 1197 行硬上限提醒保留，本次只原位替换过期说明、未增加行；Gateway 882 行、日志指南 784 行和记录索引 312 行为软提醒，后续篇幅治理单独处理。Gateway 与记录索引分别由 884、322 行收拢。

## 下一次恢复事项（2026-10-16 起）

1. 对照生产接线和事件契约，只读核对剩余自有输出与相邻透传来源，重点确认既有 Hangfire provider 的事件经过 enrichment / scope 合并后，在旧链 WriteTo / AuditTo 中的实际输出；生成端只传受控值不等于最终输出已重建。
2. 分开列出剩余框架来源、桥接前求值和非 Serilog 通道；浏览器共享 HTTP / logger / 示例输出单独排期，不借 Node 契约验证宣称其已完成。
3. 形成 L2 剩余覆盖清单，先确认下一批完整范围、验证与完成边界，再进入实现。独立可靠性问题按专题确认，Native P8-C readiness 保持后续顺位。

今天到此停止实现。后续依赖、服务启动与真实运行仍按具体范围确认；本次没有执行 push、合并、tag 或部署。
