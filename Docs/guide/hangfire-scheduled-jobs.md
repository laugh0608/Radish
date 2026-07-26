# Hangfire 定时任务指南

> 状态：当前运行说明
>
> 最后更新：2026-07-20

Hangfire 是 `Radish.Api` 的后台任务调度与执行设施。任务定义与注册以 `Radish.Api/Program.cs` 为准，Hangfire 自有存储只保存调度和执行状态，不进入业务库 `RadishSchemaVersion` ledger。

## 1. 入口与权限

- API 直连：`http://localhost:5100/hangfire`
- Gateway：`https://localhost:5000/hangfire`
- Console：Gateway `/console/hangfire` 页面承载受保护的 Dashboard 外壳

本地回环请求允许进入 Dashboard。远程请求必须完成认证，并满足以下任一条件：

- 当前用户为 `System / Admin`；
- 当前角色具备 `console.hangfire.view`。

Dashboard 只用于查看、诊断和按权限执行 Hangfire 自带操作，不代表项目已经实现独立任务编排平台。生产环境应继续通过 Gateway、可信代理和访问控制限制入口。

## 2. 当前 recurring jobs

| Job ID | 默认计划 | 时区 | 职责 |
| --- | --- | --- | --- |
| `reliable-outbox-dispatch` | `*/1 * * * *` | UTC | 分派可靠 Outbox 事件 |
| `cleanup-chat-reaction-operations` | `15 4 * * *` | UTC | 清理 Chat Reaction 幂等操作事实 |
| `cleanup-notification-inbox` | `30 3 * * *` | UTC | 按保留与容量规则清理已读 / 已删除通知数据，不删除未读事实 |
| `cleanup-wiki-terminal-draft-payloads` | `45 3 * * *` | UTC | 清空超过保留期的终态 Wiki 草稿正文载荷 |
| `cleanup-deleted-files` | `0 3 * * *` | 应用默认时区 | 清理超过保留期的软删除附件 |
| `cleanup-temp-files` | `0 * * * *` | 应用默认时区 | 清理临时文件 |
| `cleanup-recycle-bin` | `0 4 * * *` | 应用默认时区 | 清理回收站文件 |
| `cleanup-orphan-attachments` | `0 5 * * *` | 应用默认时区 | 清理孤立附件 |
| `cleanup-expired-upload-sessions` | `*/15 * * * *` | 应用默认时区 | 回收过期分片上传会话与容量预留 |
| `cleanup-expired-access-tokens` | `0 7 * * *` | 应用默认时区 | 清理过期文件访问令牌 |
| `comment-highlight-stat` | `0 1 * * *` | 应用默认时区 | 统计神评 / 沙发 |
| `retention-reward` | `0 2 * * 0` | 应用默认时区 | 发放神评 / 沙发保留奖励 |
| `forum-post-lottery-auto-draw` | `*/1 * * * *` | 应用默认时区 | 为到期论坛抽奖自动开奖 |
| `shop-cancel-timeout-orders` | `*/10 * * * *` | 应用默认时区 | 取消超时未支付订单 |
| `shop-mark-expired-benefits` | `0 0 * * *` | 应用默认时区 | 标记过期权益 |
| `shop-daily-stats` | `0 1 * * *` | 应用默认时区 | 生成商城日统计 |

除前两项固定注册外，多数任务受对应 `Enable` 配置控制。实际计划应在启动日志和 Dashboard Recurring Jobs 页复核，不能只依赖本文默认值。

## 3. 配置边界

Hangfire 基础配置位于 `Radish.Api/appsettings.json` 的 `Hangfire` 节点：

```json
{
  "Hangfire": {
    "DbType": 2,
    "ConnectionString": "Data Source=DataBases/Radish.Hangfire.db",
    "Server": {
      "WorkerCount": 1
    },
    "Dashboard": {
      "Enable": true,
      "RoutePrefix": "/hangfire",
      "AllowLocalOnly": true
    },
    "NotificationInboxCleanup": {
      "Enable": true,
      "Schedule": "30 3 * * *",
      "BatchSize": 200,
      "SoftRelationLimitPerUser": 5000
    },
    "WikiDraftPayloadCleanup": {
      "Enable": true,
      "Schedule": "45 3 * * *",
      "BatchSize": 200
    }
  }
}
```

- `DbType=2` 使用 SQLite，`DbType=4` 使用 PostgreSQL；生产连接串通过本地覆盖或环境变量提供。
- SQLite 本地默认单 worker，避免后台任务并发写入冲突。
- recurring 配置键统一使用 `Schedule`，不是旧文档中的 `Cron`。
- `Time.DefaultTimeZoneId` 决定“应用默认时区”任务的 Cron 解释；Outbox、通知、Wiki 与 Chat Reaction 清理显式使用 UTC。
- `Dashboard.AllowLocalOnly` 是部署意图配置；当前实际授权仍以 `HangfireAuthorizationFilter` 的本地 / 身份 / 权限判断为准。

完整配置加载与环境变量规则见 [配置管理](/guide/configuration)，时间语义见 [时间语义与业务自然日](/guide/time-semantics)。

## 4. Wiki 终态草稿正文清理

`cleanup-wiki-terminal-draft-payloads` 由 `WikiDraftPayloadCleanupJob` 执行：

- 保留天数读取 `Document.Authoring.TerminalDraftRetentionDays`，默认 `90`；
- 批次大小读取 `Hangfire.WikiDraftPayloadCleanup.BatchSize`，默认 `200`；
- 只选择 `Applied / Rejected / Withdrawn`、尚未清理且超过 cutoff 的草稿；
- 只把 `MarkdownContent` 清空并写入 `PayloadPurgedAt`，不删除草稿记录、审核事件或正式 Revision；
- 活跃 `Editing / Submitted / ChangesRequested` 草稿不进入清理；
- Repository 按批次和 `PayloadPurgedAt == null` 条件更新，任务可安全重跑；
- 任务失败最多自动重试两次，延迟为 `60 / 300` 秒。

这项任务落实的是用户内容保留边界，不是数据库物理级联清理。修改保留天数、终态定义或清理字段时，必须同步 [文档作者协作设计](/features/wiki-author-contribution-collaboration-design) 与回归测试。

## 5. 任务设计规则

- Job 必须可重入；业务正确性由数据库条件更新、唯一键、幂等键或事务保证，不能依赖“计划不会重复触发”。
- Cron 只负责触发。超时、过期、锁定和清理 cutoff 使用注入的 `TimeProvider` UTC 时刻；业务自然日使用 `BusinessCalendar`。
- 大数据量任务按稳定排序和有限批次处理，不一次性加载整表。
- 失败必须抛回 Hangfire 形成 Failed / Retry 状态，不能吞异常后伪装成功。
- 日志记录 Job 名称、处理数量和安全参数，不记录正文、凭据、连接串或用户敏感信息。
- 新增 recurring job 时同步注册、默认配置、权限 / 运行影响、定向测试和本文任务表。

## 6. 排障顺序

任务未出现或未执行时依次检查：

1. `Radish.Api` 是否成功启动，启动日志是否打印对应“已注册”消息。
2. 对应 `Enable / Schedule / BatchSize / Retention*` 是否来自预期配置层。
3. Dashboard 的 Recurring Jobs、Retries 和 Failed Jobs 是否记录异常。
4. Hangfire 存储连接是否可用；SQLite 是否误启多个 Server / worker，PostgreSQL 连接与 schema 是否正确。
5. 任务时区是 UTC 还是 `Time.DefaultTimeZoneId`，避免把计划时间误读为本地时间。
6. 业务库结构是否已经由 `Radish.DbMigrate apply` 前滚；Job 不负责补业务 schema。

涉及 Wiki 清理时，额外确认终态时间、`PayloadPurgedAt`、保留天数与批次大小。正文为空但审核事件和 Revision 仍存在是预期结果。

## 相关文档

- [配置管理](/guide/configuration)
- [数据库总览](/guide/database-overview)
- [时间语义与业务自然日](/guide/time-semantics)
- [文档作者协作与审核使用说明](/guide/docs-author-collaboration)
- [通知中心使用说明](/guide/notification-center)
