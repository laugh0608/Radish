# 2026-05-19 收工回顾与明日事项

## 今日提交回顾

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `5b263259 chore(deploy): unify deployment compose profile` | 部署 / 文档 | 部署态 Compose 收敛到统一测试 / 生产配置，`.env` 轨道变量、PostgreSQL 初始化和部署说明已同步。 |
| `de05ac60 feat(deploy): align runtime database providers` | 部署 / 配置 / 文档 | API / Auth / Gateway 的运行时数据库 provider 口径已对齐，PostgreSQL 覆盖范围扩展到业务库、日志库、OpenIddict 与 Hangfire，并同步配置、Gateway、M15 与架构说明。 |
| `1b41e3a4 fix(frontend): avoid undeclared capacitor dependency` | 前端修复 | `radish.client` 入口避免继续依赖未声明的 Capacitor 包，保持当前 Tauri + WebOS 桌面壳和 Web 入口运行边界。 |
| `c4887506 feat(deploy): secure first admin bootstrap` | 首次部署 / 安全 / 文档 | 测试 / 生产默认不再创建开发管理员账号，新增首次管理员初始化链路，并同步部署、配置说明和后端测试。 |
| `14dc3009 fix(deploy): handle postgres shop order backfill` | 数据初始化修复 | PostgreSQL 下商城订单种子 / 回填兼容性已修复，避免部署态初始化因历史数据形态差异中断。 |
| `78b6db35 fix(deploy): use host paths for persistent data` | 部署 / 文档 | PostgreSQL、Redis 与 Auth 证书持久化从 Docker 命名卷收敛到宿主目录，便于测试 / 生产环境备份、迁移和排障。 |
| `ade353e7 chore(deploy): default to image release tracks` | 发布轨道 / 文档 | 部署态默认按 `RADISH_IMAGE_TRACK` 使用 `test-latest` / `release-latest`，固定 tag 只在需要可复现部署时启用。 |
| `f07b8b1e fix(bootstrap): 修复首次管理员初始化状态约束` | 首次管理员修复 | `SystemBootstrapState.CompletedUserId / CompletedTime` 与旧库 PostgreSQL 约束修复已落地，避免首次创建管理员时因 nullable 约束不一致报错。 |
| `de41522e fix(db): 规范 PostgreSQL 时间参数为 UTC` | 数据库兼容修复 | SqlSugar AOP 写入前规范化 PostgreSQL `DateTime` / `DateTimeOffset` 参数，避免 `timestamp with time zone` 拒绝 Local 时间。 |

## 文档同步复核

- 部署态 PostgreSQL / Redis 默认启用、统一镜像轨道、宿主目录持久化、首次管理员初始化和开发默认账号关闭，已同步到 [部署与容器指南](/deployment/guide) 与 [配置管理指南](/guide/configuration)。
- PostgreSQL `DateTime` / `DateTimeOffset` UTC 参数规范化已补到 [开发框架说明](/architecture/framework)，与配置指南中“数据库统一 UTC 存储”的口径保持一致。
- Redis 当前能力与未来治理边界已补成 [Redis 与缓存治理专题](/planning/redis-cache-governance)，并挂入 [未来规划 / Backlog](/planning/backlog)、[开发路线图](/development-plan) 与 [开发框架说明](/architecture/framework)。
- 今日未改变 `P3-6` 当前主线：Redis 专题保持后置，首次管理员和 PostgreSQL 兼容修复属于部署 / 真实使用反馈分流下的小闭环。
- 今日修复没有改变前端视觉、PublicId 试点、公开 head / sitemap 策略或多端路线，因此不更新视觉规范、ID 长期路线或公开增长专题边界。

## 明日事项

1. 若 testing URL 镜像构建完成，优先用最新 `v*-test` 轨道复测首次管理员初始化、登录链路和公开 head smoke。
2. 若 Redis / PostgreSQL 环境继续暴露问题，先修复真实报错；不启动 Redis 平台化专项。
3. 若没有新的部署阻断，继续回到 `P3-6` 真实使用观察与反馈分流，只处理高信号问题。
