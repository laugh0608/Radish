---
releaseTag: v26.7.1.1204-release
productVersion: 26.7.1
imageTag: v26.7.1.1204-release
---

# M15 PostgreSQL DateTime 修复补发记录（v26.7.1.1204-release，2026-07-12）

> 本页记录 `v26.7.1.1203-release` 在远程 Candidate Quality 暴露 PostgreSQL DateTime 参数初始化顺序依赖后的根因修复补发。PR、tag、镜像与生产结果只在实际完成后回写。

## 记录信息

- 记录日期：2026-07-12
- 发布类型：正式 Web 部署修复补发
- 当前状态：本地根因修复与 PostgreSQL / 后端回归通过，候选级完整验证进行中

## 发布标识

- 计划 Git tag：`v26.7.1.1204-release`
- 产品版本：`26.7.1`
- 当前修复提交：`8251526f`
- 最终对应提交：待修复 PR 合并后，以补发 tag 指向的 `master` 提交为准
- 正式发布矩阵：DbMigrate、API、Auth、Gateway、Frontend

## 补发原因与根因

- 1203 已修复 OpenIddict PostgreSQL 运行态模型与 migration snapshot 的差异，但 Docker Images `#19` 在镜像构建前的 Candidate Quality 中有三项 PostgreSQL 集成测试失败。
- 生产 SqlSugar 配置原本在 SQL 日志 AOP 内把 PostgreSQL `DateTime` 参数规范化为 UTC；直接创建 SqlSugar client / scope 的集成测试没有该 AOP，并隐式依赖 Npgsql legacy timestamp 全局开关的初始化顺序。
- 新增 OpenIddict migration 测试改变了同一测试进程内的 Npgsql 初始化时序，使 `DateTimeKind.Unspecified` 按现代 Npgsql 契约被拒绝写入 `timestamp with time zone`。问题属于共享持久化契约边界与测试装配缺口，不是仓储事务、令牌额度或 Outbox 并发逻辑回归。

## 修复范围

- 把 PostgreSQL DateTime 参数 UTC 规范化从 SQL 日志职责中拆为独立、可复用的持久化契约。
- 生产所有 SqlSugar 连接均安装该参数契约；Log 连接继续避免递归 SQL 日志，但不再绕过参数规范化。
- PostgreSQL 真实集成测试通过统一 factory 创建 client / scope，复用与生产一致的参数规范化，不再依赖 Npgsql legacy timestamp 或测试执行顺序。
- 保持已发布初始 migration、后续 migration、schema ledger 与 `__EFMigrationsHistory` 不变；不关闭或降级 `PendingModelChangesWarning`，不启用 legacy timestamp 兼容开关。

## 本地验证

- 1203 远程失败的三个测试类定向回归：`4 passed / 0 failed`。
- 环境驱动的全部 PostgreSQL 集成测试：`10 passed / 0 failed`；覆盖 OpenIddict 空库首次迁移、重复执行、EnsureCreated schema 接管、最终模型一致性、时间列契约、文件令牌并发与 ReliableOutbox 事务 / 领取语义。
- CI 同款全量后端测试：`635 passed / 0 skipped / 0 failed`。
- `git diff --check` 与本批 staged repo hygiene：通过。
- Release build、`validate:ci`、Candidate Quality 同款完整验证与最终仓库卫生结果待本批完成后回写。

## 服务器恢复边界

- 保留当前 PostgreSQL / Redis volume、已应用 Main schema ledger 与 OpenIddict migration history。
- 不清库、不删除 volume、不手工修改 migration ledger 或 `__EFMigrationsHistory`。
- 不移动或复用 1201 / 1202 / 1203 tag，不部署 `latest`。
- PR 合并、`master -> dev` 回灌、1204 tag 与五镜像门禁完成前，不在生产服务器重复执行 `up -d`。
- 镜像成功后固定 `RADISH_IMAGE_TAG=v26.7.1.1204-release` 前滚；DbMigrate 成功后才允许 API、Auth、Gateway 启动。

## 发布门禁

- [ ] 完成候选级 build、test、`validate:ci`、repo hygiene 与 `git diff --check`
- [ ] `dev -> master` PR required checks 全部通过并合并
- [ ] 最新 `origin/master` 回灌 `dev`
- [ ] 在 `master` 合并提交创建并推送 `v26.7.1.1204-release`
- [ ] Docker Images 五镜像门禁成功
- [ ] 生产固定 tag 前滚与部署后复核完成
