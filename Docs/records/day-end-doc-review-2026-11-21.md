# 2026-11-21 日终文档回顾

日期按 Asia/Shanghai。复核 `dev` 从 `9349e611` 到 `d89f6798` 的提交、关联源码和既有验收记录。

## 提交与证据

| 提交 | 核对结果 |
| --- | --- |
| `9349e611`、`7232ef65` | OrbStack 日志候选通过查询、满卷告警、升级、备份恢复和回滚。维持首次同步前的丢失窗口裁决，满卷未观测尾段数量保持未知。实际部署预检与慢 SQL 安全指纹单列。Windows 日常 VM 完成只读盘点并停止，CleanBase 保持停止。记录了 13.29 GiB 可用空间、工具缺项和后续授权条件。[日志出口](./unified-logging-exit-review-2026-11-21.md) · [Windows 盘点](./f4-flutter-native-p8c-windows-readiness-2026-11-21.md) |
| `f6f9f008` | 登记六项提案，选定第 2 项先推进。Flutter Native、客户端连接继续暂停，保留原有接续顺位。[提案与顺位](../planning/community-platform-next-proposals.md) |
| `1720a261`、`bc87d5d4` | 默认 5 / 25 分钟的隔离对照中，关页 7 分钟后可刷新恢复，27 分钟后 refresh token 到期。确认普通 Web 改用 7 天不活跃、30 天绝对上限，旧 Web 会话以一次交互登录迁移。实际掉线实例及入口仍待核实。[隔离复现](./auth-session-overnight-default-lifetime-2026-11-21.md) · [确认方案](../features/auth-session-overnight-recovery-plan.md#web-session-repair) |
| `8fc26007`、`6f743951` | `radish-web` 服务端会话、双数据库增量迁移、同源 Cookie、服务端凭据加密、轮换租约及撤销已交付。实际 PostgreSQL 迁移、密钥环持久化及 Auth 的受信 HTTPS 回连待隔离验收。[数据库边界](../guide/database-overview.md#结构真相源) · [配置入口](../guide/configuration.md#普通-web-会话候选) |
| `d89f6798` | 普通 Web 恢复、统一刷新、登出和 Hub 到期重连接通。GET / HEAD 可用不同 bearer 重试一次，写请求交回调用方。旧客户端和匿名评论有独立 Hub 入口。实现阶段通过 92 项前端与共享 HTTP 用例、121 项后端定向用例、类型检查、定向 Lint 和 Web 生产构建。[前端与 Hub 接入](../features/auth-session-overnight-recovery-plan.md#普通-web-前端与实时连接) |

## 文档同步与接续

- [认证指南](../guide/authentication.md)、[认证服务](../guide/authentication-service.md)与[不活跃规则](../guide/auth-idle-session.md)补齐既有客户端默认值、新 Web 模式和旧回调边界。
- [共享 HTTP](../frontend/http-client.md)、[错误处理](../frontend/error-handling.md)、[跨应用导航](../frontend/client-console-navigation-contract.md)与[Gateway](../guide/gateway.md)同步凭据隔离、写入重试和 Hub 路由。
- 下次先准备临时 Compose 覆盖、一次性 PostgreSQL、HTTPS 入口、专用密钥卷和清理方案，获准后按迁移、回连、浏览器、两道期限、真实隔夜及回退的顺序验收。普通 Web 部署开关保持关闭。[接下来的事项](../planning/current.md#接下来的事项) · [验收矩阵](../features/auth-session-overnight-recovery-plan.md#修复验收)
