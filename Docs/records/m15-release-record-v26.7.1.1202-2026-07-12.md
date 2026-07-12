---
releaseTag: v26.7.1.1202-release
productVersion: 26.7.1
imageTag: v26.7.1.1202-release
---

# M15 补发记录（v26.7.1.1202-release，2026-07-12）

> 本页记录 `v26.7.1.1201-release` 首次生产部署被 DbMigrate 阻断后的补发，以及 1202 前滚暴露的后续 OpenIddict 阻断。

## 记录信息

- 记录日期：2026-07-12
- 发布类型：正式 Web 部署修复补发
- 当前状态：PR、tag 与五镜像成功；生产前滚仍被 OpenIddict 初始 EF migration 阻断

## 发布标识

- 计划 Git tag：`v26.7.1.1202-release`
- 产品版本：`26.7.1`
- 对应提交：`2717a8a2`（PR `#61` 合并提交）
- 正式发布矩阵：DbMigrate、API、Auth、Gateway、Frontend

## 补发原因

- 空 PostgreSQL Code First 创建小写物理表与列，经验自然日 migration 的手写 SQL 固定引用 PascalCase 标识符，触发 `42P01`。
- baseline 已成功登记，后续 migration 未登记且事务失败；数据库处于可继续前滚的受控状态。
- 部署编排没有把 OpenIddict PostgreSQL provider 与连接字符串传入 DbMigrate，迁移工具错误回退到 SQLite。
- `libgssapi_krb5.so.2` 缺失警告之后数据库连接、建表和 ledger 写入均成功，当前不认定为主阻断；若修复后仍出现 `139`，再独立处理运行库问题。

## 修复范围

- 从数据库 metadata 解析实际物理表名和列名，再生成引用 SQL；同时兼容历史 PascalCase 与 PostgreSQL 小写标识符。
- 时间语义审计与自然日 migration 共用同一物理标识符解析边界。
- DbMigrate 使用与 API/Auth 一致的 OpenIddict PostgreSQL provider 和连接字符串。
- 增加小写 PostgreSQL 表列的 migration apply、重入和 ledger 回归。

## 服务器恢复边界

- 不删除 PostgreSQL / Redis 数据或 volume。
- 不移动旧 tag，不使用 `latest`，不在容器内打补丁。
- 新镜像发布后固定 `RADISH_IMAGE_TAG=v26.7.1.1202-release`，先拉取镜像再重新执行 Compose。
- DbMigrate 必须成功完成 apply / verify，随后才允许 API、Auth 与 Gateway 启动。

## 候选验证

- `dotnet build Radish.slnx -c Debug --no-restore`：通过，`0 warning / 0 error`。
- `dotnet test Radish.Api.Tests --no-build`：通过，`625 passed / 8 skipped`；跳过项均为未注入 PostgreSQL 连接时的条件集成测试。
- PostgreSQL 16 临时实例迁移专项：`ExperienceNaturalDateSchemaMigrationTest` 共 `7 passed / 0 skipped`，覆盖小写物理表列、历史表形态、重复执行和并发写入。
- PostgreSQL 16 临时实例时间语义专项：`TimeSemanticsPostgresIntegrationTest` 共 `2 passed / 0 skipped`。
- `npm run validate:ci -- --report-file .tmp/v26.7.1.1202-release-validate-ci.md`：通过。
- `node Scripts/version-contract.mjs --tag v26.7.1.1202-release`：通过。
- Compose 固定 `v26.7.1.1202-release` 配置展开：通过。
- 临时 PostgreSQL 容器已删除，测试端口已释放，未保留测试数据。

## 生产部署结论

- Docker Images：`#18` 成功，五个正式镜像已完成候选门禁与推送。
- 部署情况：已使用固定 `RADISH_IMAGE_TAG=v26.7.1.1202-release` 保留 volume 前滚，但未完成上线。
- 已确认生效：PostgreSQL 小写物理标识符修复、`main.20260712_001_experience_natural_dates` 应用、OpenIddict provider 切换为 PostgreSQL。
- 当前阻断：空 OpenIddict PostgreSQL 执行 `20260712030518_20260712_InitialOpenIddict` 前，EF Core 检测运行态模型与 snapshot 不一致；API、Auth、Gateway 未启动。
- 数据边界：保留 PostgreSQL / Redis volume、已应用 schema ledger 与 `__EFMigrationsHistory`；未清库、未手工改 ledger、未重复执行生产 Compose。
- 后续处置：旧 tag 保持不可变，改由 `v26.7.1.1203-release` 修复 OpenIddict PostgreSQL 运行态模型后继续前滚。
