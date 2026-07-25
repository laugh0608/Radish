# 生产数据库迁移发布编排完成记录（2026-07-25）

## 结论

仓库已建立生产数据库迁移与应用发布的固定编排入口。生产发布不再依赖维护者手工组合 `docker compose pull / up`，而是由 `Deploy/deploy-production.sh` 统一执行不可变版本预检、停止业务写入、六库备份、显式 migration、独立严格校验、应用启动和外部健康检查。

本批只完成仓库代码、合约测试和文档治理，没有启动服务、调用真实 Docker、连接生产数据库或执行生产发布。

## 固定契约

- 仅接受与仓库发布记录一致的不可变 `v*-release` tag。
- 生产 `.env` 必须为 `600` 或更严格权限，stage 必须为 `production`，开发账号种子必须关闭，公开入口必须使用 HTTPS。
- 拉取镜像发生在维护窗口停止应用之前。
- PostgreSQL 与 Redis 健康后停止 Gateway、Api、Auth、Frontend，阻断跨库写入。
- 备份 globals 和 Main、Log、Message、Chat、OpenIddict、Hangfire 六库，并校验 dump 可解析、生成 SHA-256 与元数据。
- 每个发布批次都显式执行一次 `Radish.DbMigrate apply` 和另一份一次性容器的 `verify`。
- migration 开始前失败可恢复此前运行的应用；migration 开始后失败保持应用停止并保留备份证据，不自动执行破坏性恢复。
- 新应用与 Gateway 分阶段启动，最后检查真实外部 `${RADISH_PUBLIC_URL}/health`。

## 代码与文档

- 新增 `Deploy/deploy-production.sh`
- 新增 `Scripts/production-deploy-contract.test.mjs`
- 新增 `npm run check:production-deploy`
- 将生产发布编排合约纳入统一 baseline
- 新增[生产数据库迁移与发布编排](/guide/production-database-migration-deployment)
- 同步修正部署指南、M15 基线与数据库结构变更协作口径

## 合约验证

隔离假 Docker / curl 合约覆盖：

1. 浮动 tag 在触达 Docker 状态前被拒绝。
2. 六库备份及可解析检查在 `apply / verify / rollout / health` 之前完成。
3. 同一版本的新发布批次仍会重新执行 `apply` 与独立 `verify`。
4. migration 失败会阻断新应用发布、保留备份，且不会自动启动旧应用。
5. 备份失败不会进入 migration，并以 `start` 恢复此前保留的原版本容器。
6. 新应用已经启动但外部健康检查失败时，会再次停止整个应用批次。

执行入口：

```bash
bash -n Deploy/deploy-production.sh
npm run check:production-deploy
```

## 恢复边界

本批没有实现自动数据库恢复。原因是 migration 开始后的恢复必须同时判断新旧 schema 兼容性、失败现场、六库一致批次和目标应用版本；自动覆盖数据库会扩大数据损失风险。

若生产 migration 失败，应保持应用停止并保留失败现场，优先评估更高 migration ID 的前滚修复。确需恢复时，必须对同批 globals 与六库备份完成隔离恢复验证，再按获批记录执行整批恢复。

## 后续顺位

本项属于生产维护线的契约修正，不改变 Phase 4 产品功能顺位。维护线关闭后继续进入 `F4-K-A`，交叉复核圈子全局屏蔽、公开聊天与论坛作者回滚等候选，只选定一个完整专题进入设计与实现。
