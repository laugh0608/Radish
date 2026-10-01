# 日志 L2 seed / migration 生成端治理记录

> 日期：2026-09-23（Asia/Shanghai）。承接[上一批](./unified-logging-l2-producer-governance-2026-09-23.md)，按[统一日志专题](../features/unified-logging-governance-design.md)继续 L2。当前规则见[事件契约](../features/unified-logging-contract.md)与[日志指南](../guide/logging.md)。

## 范围与结果

- `InitialDataSeeder` 移除全局 `Console.Out` 捕获、明细计数及失败原文回放。固定阶段枚举通过异步逻辑上下文关联，正常 / 失败均记录一次阶段结果；原异常原样传播，最终 Error 仍由 `RuntimeProcess` 负责。
- seed 各 partial 的逐行裸打印移除；资源、schema、权限缺失及数据冲突保留安全 Warning，修复 / 回收 / 回填使用固定事件与数量。邮箱、名称、路径、回调、SQL 和 provider 异常消息不进入本批事件。
- schema ledger 只在事务提交后生成安全摘要，具体 Wiki authoring migration 不再打印过程。保留事务、幂等检查、顺序、校验和与权威迁移账本；逐项比较确认所有具体 migration 的 `ChecksumSource` 声明未变。
- OpenIddict schema adoption 在 history 提交后记录安全事件。Auth seed 只在整组成功后汇总 scope / client 创建、更新、移除操作和耗时；回调、权限、元数据及旧 shop 删除逻辑保持原样。
- 新事件与受控属性登记在 .NET / Node 共用策略中。新增迁移 ID 时须同步策略，注册表测试检查策略能保留每个 ID。

## 验证

定向命令：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-restore --filter 'FullyQualifiedName~SeedLoggingTests|FullyQualifiedName~IdentitySeedContractTests|FullyQualifiedName~DeveloperDefaultsSeedPolicyTests|FullyQualifiedName~SchemaMigrationLedgerTest|FullyQualifiedName~WikiAuthorCollaborationSchemaMigrationTest|FullyQualifiedName~AuthOpenIddictMigrationTest|FullyQualifiedName~RuntimeLog|FullyQualifiedName~ProducerLoggingTests'
dotnet build Radish.DbMigrate/Radish.DbMigrate.csproj --no-restore --warnaserror
dotnet build Radish.Auth/Radish.Auth.csproj --no-restore --warnaserror
npm run check:logging-contract
npm run check:repo-quality:changed
git diff --check
```

- .NET 定向：89 通过、4 跳过。新增测试覆盖旧 / 候选输出安全、原异常与最终错误所有权、并发阶段上下文隔离、SQLite 角色种子幂等、Auth seed 创建 / 更新 / 失败、迁移 ID 策略及真实 SQLite baseline 提交 / 重入 / checksum drift。
- 4 个跳过均因未设置 `RADISH_TEST_POSTGRES_CONNECTION_STRING`：Auth 空库、Auth adoption、DbMigrate Auth 空库、Wiki authoring PostgreSQL 用例。没有启动 PostgreSQL 或补造生产证据。
- 测试编译保留上一批 `ProducerLoggingTests.cs` 的 1 条 xUnit1051 提示；本批新增测试没有该提示。
- Node 共用日志契约：27 通过。DbMigrate 与 Auth 独立构建均为 0 警告、0 错误；仓库卫生、文档链接与 `git diff --check` 通过。卫生检查保留 `logging.md` 与 records 索引的篇幅软提醒，没有阻断错误。
- 初次沙盒测试因 MSBuild 命名管道权限失败，定向测试和构建改为最小范围提权；不涉及包恢复、依赖安装或业务服务启动。

## 未关闭边界与下一步

`RadishLogging.Enabled` 仍默认 false。未执行真实宿主 / 浏览器 Smoke、外部数据库验收、生产发布或部署；SQLite 与管理器 Mock 测试不替代这些证据。

下一批继续后台任务 / Rust 的工作量摘要、重试和最终异常所有权；随后治理剩余业务与框架事件、安全异常栈帧。L2 整体及 L1 传输上界、L3–L6 均未因此关闭。
