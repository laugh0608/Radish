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
- 当前状态：生产固定 tag 前滚成功，首个管理员已创建，服务运行正常

## 发布标识

- Git tag：`v26.7.1.1204-release`
- 产品版本：`26.7.1`
- 当前修复提交：`8251526f`
- PR：`#63`
- 最终对应提交：`53539556`
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
- `dotnet build Radish.slnx -c Release --no-restore`：通过，`0 warning / 0 error`。
- 注入 PostgreSQL 的 `npm run validate:candidate`：通过；全仓 `2422` 个文件卫生预算无新增，前端 lint 零 warning，warnings-as-errors baseline、LongId 安全与依赖安全均通过，npm / NuGet High / Critical 为 `0`，其中后端测试为 `635 passed / 0 skipped / 0 failed`。
- `npm run validate:ci`：通过；因代码与文档已分批提交，changed-only Backend / Identity Guard 按契约跳过，完整后端验证已由同批 `validate:candidate` 覆盖。
- changed / staged repo hygiene 与 `git diff --check`：通过；全仓 `110` 个历史卫生问题均在既有 baseline 内，本批未新增。
- 临时 PostgreSQL 容器已在验证完成后清理。

## 生产部署结果

- `v26.7.1.1204-release` 五个正式镜像构建成功，生产以固定 `RADISH_IMAGE_TAG=v26.7.1.1204-release` 完成前滚，未部署 `latest`。
- 既有 PostgreSQL / Redis 持久化数据、Main schema ledger 与 OpenIddict migration history 均保留；DbMigrate、API、Auth、Gateway 与 Frontend 当前运行正常。
- 首个管理员已通过正式 bootstrap 创建流程成功建立，初始化入口随后按后端管理员存在性契约关闭。
- 部署后发现公开入口与 Workbench 未统一经过 `BootstrapGate`，导致首次管理员页面在点击“聊天”等私域入口后才出现。该问题不影响当前已初始化生产环境，已转入发布后维护线统一治理，不修改 1204 tag。

## 服务器恢复边界

- 保留当前 PostgreSQL / Redis volume、已应用 Main schema ledger 与 OpenIddict migration history。
- 不清库、不删除 volume、不手工修改 migration ledger 或 `__EFMigrationsHistory`。
- 不移动或复用 1201 / 1202 / 1203 tag，不部署 `latest`。
- 后续部署继续固定明确版本 tag，并保留当前 PostgreSQL / Redis / Auth 证书持久化目录。
- 首次管理员入口修复通过新的正常维护发布交付，不移动或复用 1204 tag。

## 发布门禁

- [x] 完成候选级 build、test、`validate:ci`、repo hygiene 与 `git diff --check`
- [x] `dev -> master` PR required checks 全部通过并合并
- [x] 最新 `origin/master` 回灌 `dev`
- [x] 在 `master` 合并提交创建并推送 `v26.7.1.1204-release`
- [x] Docker Images 五镜像门禁成功
- [x] 生产固定 tag 前滚与部署后复核完成
