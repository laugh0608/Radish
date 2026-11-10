# 2026-11-05 L3-A 接收与幂等持久化本地实施

## 结论与授权

项目所有者已批准按[实施方案](../features/unified-logging-l3-ingest-plan.md)完成 L3-A 本地实现与验证，服务与容器启动另行确认。准备基线为 `fee65b97`，在 `dev` 串行开发；本批不安装依赖，不启动项目服务 / 容器，不连接 PostgreSQL、生产或真实日志库。

已交付默认关闭的内网接收、分批幂等事务、Log migration 和直接相关防递归修正。这里只关闭本地实现与验证切片；PostgreSQL 16、真实 Kestrel 监听、collector 端到端、L1 磁盘 / 平台与 L3-B 部署留存门禁仍未关闭。

## 实现

- `RuntimeLogIngestSetup` 在 API 的审计 / 静态资源 / 业务管道之前按实际 LocalPort 分流；公共端口拒绝 `/internal`，专用端口只接受凭据正确的指定 POST。Gateway 同时拒绝该命名空间；Controller 不在 OpenAPI 中展示，不使用用户 JWT。
- 接收配置继承受信日志 Mode，部署与服务允许列表独立验证。请求不能通过 Host、转发头或载荷 Development 模式绕过边界；凭据不进入共享配置与输出。
- `RuntimeLogJsonLines` 限制 UTF-8、深度、重复字段、行字节、信封字节和行数，空信封拒绝。共享策略验证固定说明和属性；保留 eventId、来源与原 UTC 毫秒时间。
- Service 每 200 条提交；Repository 选择 Log 配置，使用独立参数化 SQLite / PostgreSQL 事务及数据库唯一约束。重复返回已有实体，同键异内容拒收，不覆盖原记录。后续批次失败不撤销之前提交，整体重放吸收已存在事件。
- `RuntimeLogEventRecord` 不继承旧 BaseLog / 软删除。Log migration `20261105_023_runtime_log_ingest` 增加 `runtime_log_event` 和 `runtime_log_rejection_bucket`，验证列、索引顺序与唯一性；API 不建表，旧表不修改。
- 拒收仅保存受信来源、固定原因、UTC 日桶和饱和计数，保留当前日与此前 6 日；每次事务最多清理 200 个过期桶。重放可能重复计算拒收尝试，不能将此数解释成唯一坏事件数。
- 接收作用域不进入普通日志持久化，Hosting 正常内部请求事件被排除；PRAGMA 失败使用独立限频 stderr，保留 false 返回。故障计数与最近提交时间仅为进程内状态，不是持久管道状态页。

## 本地验证

- `dotnet build Radish.slnx --no-restore -c Debug --warnaserror`：通过，0 warning / 0 error。
- L3 专项：63 项通过，包含真实 Controller → Service → 临时 SQLite 的内存管道；未调用 Start / Run 或绑定端口。
- 扩展定向回归：343 项通过，包含 L3、生成策略 / adapter、schema ledger、SQLite native、外层 HTTP sink、Hangfire 输出；全部明确排除 `Database=PostgreSQL`。
- `npm run check:logging-contract`：52 项通过。
- 身份 Claim 与 Console 权限检查通过；Console 检查保留既有 `console.hangfire.replay` 未被前端入口使用的提示。
- 文档 / 本地链接、changed-only 文本卫生与 `git diff --check` 通过。

扩展回归命令：

```bash
DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --filter '(FullyQualifiedName~RuntimeLogIngest|FullyQualifiedName~RuntimeLogPolicyTests|FullyQualifiedName~RuntimeLoggingAdapterTests|FullyQualifiedName~SchemaMigrationLedgerTest|FullyQualifiedName~SqliteNativeContractTests|FullyQualifiedName~OuterHttpFrameworkSinkTests|FullyQualifiedName~HangfireOutputLoggingTests)&Database!=PostgreSQL'
```

验证覆盖：普通 / 内网端口双向隔离、Host / 转发头伪造、凭据错误、模式伪造、非法文本和 UTF-8、重复 JSON 字段、8 KiB / 16 MiB / 32768 行预算、超时 / 取消与并发槽释放、同键重放 / 冲突、实际 SQLite 并发与锁恢复、事务内故障回滚、分批提交后故障恢复、拒收聚合 / 清理 / 饱和计数、旧 baseline 升级 / 重入 / 漂移拒绝、旧 / 候选与 AuditTo 的防递归输出。

新增 PostgreSQL 集成测试覆盖迁移、并发重放、冲突与 UTC，但本批未执行，不能用编译成功替代 PostgreSQL 证据。

## 验证中处理与保留项

- 原默认 WebApplication builder 在本地测试配置加载阶段停滞；已终止本任务测试，专项改用空宿主及显式测试服务，仅 Build。扩展回归关闭测试默认配置文件监视，不改变产品的配置加载方式。
- 旧分表 baseline 接管夹具原本只创建 AuditLog 分表。新增普通表后，夹具补齐当前普通表，继续断言不存在分表逻辑模板；没有放宽产品 baseline 接管规则。
- 全仓 `npm run check:time-semantics` **未通过**：`Radish.Service/WikiDocumentService.Governance.cs:730` 的 `DateTime.Now` 超过原 baseline。已确认 HEAD 与工作区相同，本批未修改该文件，也未刷新 baseline 掩盖问题；本批新增时间均使用 TimeProvider / 显式 UTC。
- 临时 SQLite 与测试目录由夹具释放清理。未执行真实迁移命令、生产开关切换、push、PR、发布或部署。

后续仍按方案串行推进剩余自有来源；实际 PostgreSQL / 监听 / collector 验证需要准备具体命令、端口、隔离资源和清理清单后另行确认。
