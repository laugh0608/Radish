# 通知系统实时推送方案（总体规划）

> **文档版本**：v2.0
> **创建日期**：2026-01-02
> **最后更新**：2026-01-06
> **状态**：待实施

## 1. 背景与问题

### 1.1 当前现状（占位 + 轮询）

- **后端**：`GetUnreadMessageCount()` 为占位实现，固定返回 `unreadCount = 0`
  - 位置：`Radish.Api/Controllers/UserController.cs:614`
- **前端**：Dock 使用轮询获取未读数
  - 位置：`radish.client/src/desktop/Dock.tsx:172`
  - 行为：初始化拉取一次 + 每 30 秒拉取一次

### 1.2 轮询的主要问题

- **不优雅且浪费**：大量请求在"无变化"时仍持续触发
- **并发与限流风险**：在线用户上升后，轮询会显著抬升 API QPS，并更容易触发速率限制
- **体验限制**：30 秒粒度无法做到"秒级到达"

## 2. 目标与范围

### 2.1 目标

- 以实时推送替代前端轮询，实现未读数"变化即到达"
- 推送能力沉淀为统一基础设施，支持：评论/点赞/@提及通知、系统公告、积分变动提醒等

### 2.2 分阶段范围

| 阶段 | 目标 | 工作量 | 验收标准 |
|-----|------|--------|---------|
| **P0（最小闭环）** | 实现未读数实时推送，替换轮询 | 3-5 天 | SignalR 连接建立，未读数实时更新，Dock 不再轮询 |
| **P1（可用通知系统）** | 通知数据模型 + 列表 API + 已读状态 + 业务集成 | 5-7 天 | 评论/点赞触发通知，通知列表可查看，已读状态同步 |
| **P2（体验增强）** | 多端同步、通知聚合、偏好设置、容量治理 | 3-5 天 | 多端已读同步，通知聚合展示，用户可配置通知偏好 |

## 3. 技术方案：SignalR（WebSocket）

> Radish 在架构文档中已明确"实时交互统一使用 SignalR Hub 承载"，避免重复造轮花。

- 参考：[开发框架说明](/architecture/framework)（"实时交互（SignalR）"章节）
- 参考：[开发规范](/architecture/specifications)（"SignalR 实时交互规范"章节）

### 3.1 为什么选择 SignalR

**优势**：
- 默认使用 WebSocket，并提供自动降级（SSE/Long Polling）
- 内置重连、分组、用户映射等成熟能力
- .NET 生态工程化成本更低，符合"先落地再演进"的策略

**对比方案**：
- ❌ **纯轮询**：将"用户在线"转化为持续请求，不适合低频变化的场景
- ❌ **原生 WebSocket**：需要自行实现重连、分组、认证等逻辑，成本高

## 4. 总体架构

```
┌─────────────┐       SignalR       ┌─────────────┐
│   Client    │ ◄─────────────────► │   Gateway   │
│ (React SPA) │      WebSocket      │   (YARP)    │
└─────────────┘                      └──────┬──────┘
                                            │
                                            │ HTTP/WS Proxy
                                            ▼
                                     ┌──────────────┐
                                     │ Radish.Api   │
                                     │ (SignalR Hub)│
                                     └──────┬───────┘
                                            │
                        ┌───────────────────┼───────────────────┐
                        ▼                   ▼                   ▼
                  ┌─────────┐         ┌─────────┐        ┌──────────┐
                  │ Service │         │  Redis  │        │   DB     │
                  │  Layer  │         │ (Cache) │        │(Postgres)│
                  └─────────┘         └─────────┘        └──────────┘
```

### 4.1 服务端（Radish.Api）

- 新增 `NotificationHub`（路径：`/hub/notification`）
- Service 层在事件发生后，通过注入 `IHubContext<NotificationHub>` 推送
- **原则**：Controller 不直接推送，推送属于业务编排的一部分

### 4.2 网关（Radish.Gateway）

- Gateway 当前已开启 WebSocket（主要用于 Vite HMR）
- 需要为 Hub 路径增加 YARP 路由：`/hub/notification/{**catch-all}`
- 验证 WebSocket Upgrade 是否正常透传

### 4.3 客户端（radish.client）

- 新增 SignalR 连接封装（Hook 或模块）
- 统一处理：连接创建、Token 附带、自动重连、订阅/退订
- Dock 订阅 `UnreadCountChanged` 事件并更新 UI

## 5. 协议与事件设计

### 5.1 Hub 路径

```
Hub URL: /hub/notification
```

### 5.2 下行事件（Server → Client）

| 事件名 | Payload | 触发时机 | 备注 |
|-------|---------|---------|------|
| `UnreadCountChanged` | `{ unreadCount: number }` | 未读数变化时 | P0 核心事件 |
| `NotificationReceived` | `{ notification: NotificationVo }` | 新通知生成时 | P1 扩展事件 |
| `NotificationRead` | `{ notificationIds: number[] }` | 其他端标记已读时 | P2 多端同步 |

### 5.3 鉴权策略

- Hub 仅允许已认证用户连接（与现有 `Client` Policy 一致）
- 连接建立时附带 JWT Access Token（SignalR 支持 `accessTokenFactory`）
- `OnConnectedAsync` 中进行：
  - 用户身份校验
  - 租户校验（若需要）
  - 将连接加入用户组：`user:{userId}`

## 6. 数据模型概览

### 6.1 核心表

**Notification（通知表）**：
- 通知内容、类型、业务关联、触发者信息
- 支持按月分表（`Notification_YYYYMM`）

**UserNotification（用户通知关系表）**：
- 用户与通知的关联关系
- 已读状态、推送状态、推送时间

### 6.2 未读数计算策略

**写扩散方案（推荐）**：
- 通知生成时，更新接收者的未读计数（Redis + DB）
- 标记已读时，递减未读计数
- 定时任务校验缓存与 DB 的一致性

详细实现见：[通知系统实现细节](/guide/notification-implementation)

## 7. 业务事件集成

### 7.1 业务事件映射

| 业务事件 | 触发位置 | 通知类型 | 接收者 | 优先级 |
|---------|---------|---------|--------|--------|
| **评论被回复** | `CommentService.AddCommentAsync` | `CommentReplied` | 父评论作者 | P1 |
| **帖子被点赞** | `PostService.ToggleLikeAsync` | `PostLiked` | 帖子作者 | P1 |
| **评论被点赞** | `CommentService.ToggleLikeAsync` | `CommentLiked` | 评论作者 | P1 |
| **@ 提及** | `PostService/CommentService` | `Mentioned` | 被提及用户 | P2 |
| **成为神评/沙发** | `CommentHighlightJob` | `HighlightAchieved` | 评论作者 | P2 |
| **积分变动** | `CoinService.*` | `CoinBalanceChanged` | 用户 | P2 |
| **系统公告** | `AnnouncementService` | `SystemAnnouncement` | 全体/指定用户 | P2 |

### 7.2 通知生成规则

- **去重规则**：同一事件短时间内不重复通知（缓存 Key 控制）
- **自己不通知自己**：用户回复/点赞自己的内容不通知
- **通知聚合**（P2）：同一帖子短时间内多次点赞合并为一条通知

## 8. 性能与容量规划

### 8.1 容量估算

**假设条件**：
- 在线用户：10,000
- 每用户平均每小时收到通知：5 条

**估算结果**：
- 并发连接数：10,000
- 每秒推送消息数：~14 条（平均），~100 条/秒（峰值）
- 带宽需求：~20 KB/s（平均），~140 KB/s（峰值）

**扩展性评估**：
- 单实例 ASP.NET Core 可支撑 10,000-50,000 并发连接
- 多实例部署需要 Redis Backplane 支持

### 8.2 限流与保护策略

- **连接速率限制**：同一用户每分钟最多建立 5 个连接
- **消息体大小限制**：单条推送消息最大 1 KB
- **订阅范围限制**：用户只能订阅自己的通知组

### 8.3 降级与容错策略

**降级措施**：
1. 连接失败 → 降级为每 60 秒轮询一次（比原来 30 秒更低频）
2. 推送失败 → 记录到失败队列，定时重试（最多 3 次）
3. 消息积压 → 只推送未读数，不推送详细内容

**容错机制**：
- 客户端断线重连后，主动拉取最近 5 分钟的通知
- 推送失败的通知，用户下次连接时补发
- 通知生成与推送解耦，即使推送失败也已保存到数据库

## 9. 监控与运维

### 9.1 关键监控指标

| 指标 | 说明 | 告警阈值 |
|-----|------|---------|
| **在线连接数** | 当前 SignalR 连接数 | > 80% 容量 |
| **推送成功率** | 推送成功 / 推送总数 | < 95% |
| **推送延迟** | 事件发生到推送到达的时间 | P95 > 3s |
| **重连频率** | 每分钟重连次数 | > 100 次/分钟 |
| **消息积压** | 待推送消息队列长度 | > 1000 |
| **未读数一致性** | 缓存与 DB 的差异率 | > 5% |

### 9.2 日志记录

**关键日志点**：
- 连接建立/断开（包含 UserId、ConnectionId、IP）
- 推送成功/失败（包含 NotificationId、UserId、错误原因）
- 降级触发（包含触发原因、降级策略）
- 容量告警（包含当前连接数、推送 QPS）

## 10. 落地实施路径

### 10.1 P0（替换轮询）- 预计 3-5 天

**核心任务**：
1. 后端 Hub 实现（`NotificationHub.cs`）
2. 推送服务封装（`INotificationPushService`）
3. 前端 Hook 封装（`useNotificationHub.ts`）
4. Dock 集成（替换轮询）
5. Gateway 路由配置（如需要）
6. 测试验证（连接、推送、重连）

**验收标准**：
- ✅ SignalR 连接建立成功
- ✅ 未读数实时更新到 Dock badge
- ✅ 断线自动重连
- ✅ 连接失败时降级为低频轮询

### 10.2 P1（通知系统可用）- 预计 5-7 天

**核心任务**：
1. 数据模型（Notification + UserNotification）
2. Repository 层（通知 CRUD）
3. Service 层（通知生成、推送、已读）
4. API 接口（列表、标记已读、删除）
5. 业务事件集成（评论回复、点赞）
6. 前端通知中心（列表、Toast）
7. 测试验证（端到端流程）

**验收标准**：
- ✅ 评论/点赞触发通知生成
- ✅ 通知推送到前端
- ✅ 通知列表可查看
- ✅ 标记已读功能正常
- ✅ 未读数与列表一致

### 10.3 P2（体验增强）- 预计 3-5 天

**核心任务**：
1. 多端已读同步
2. 通知聚合
3. 用户偏好设置
4. 监控与告警
5. 管理后台
6. 压力测试与优化

**验收标准**：
- ✅ 多端已读状态同步
- ✅ 同类通知聚合展示
- ✅ 用户可配置通知偏好
- ✅ 监控指标完整
- ✅ 10,000 并发连接稳定运行

## 11. 相关文档索引

### 11.1 本系列文档

- **总体规划**（本文档）：整体架构与阶段规划
- **实现细节**：[通知系统实现细节](/guide/notification-implementation)
- **API 契约**：[通知系统 API 文档](/guide/notification-api)
- **前端集成**：[通知系统前端集成指南](/guide/notification-frontend)

### 11.2 相关内部文档

- [开发框架说明](/architecture/framework) - SignalR 实时交互章节
- [开发规范](/architecture/specifications) - SignalR 实时交互规范
- [萝卜币系统设计](/guide/radish-coin-system) - 实时推送示例（第 16.9 节）
- [神评/沙发功能](/features/comment-highlight) - 通知触发点参考
- [Gateway 服务网关](/guide/gateway) - WebSocket 路由配置

### 11.3 外部资源

- [SignalR 官方文档](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction)
- [SignalR JavaScript 客户端](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client)
- [SignalR 扩展与 Redis Backplane](https://learn.microsoft.com/en-us/aspnet/core/signalr/redis-backplane)
- [SignalR 性能调优](https://learn.microsoft.com/en-us/aspnet/core/signalr/performance)

## 12. 待确认问题

### 12.1 功能范围

- [ ] 通知系统是否需要"站内信/会话（私信）"能力？还是仅做"事件通知"？
- [ ] 未读计数是否要按分类拆分（例如 评论/点赞/@提及 分开计数）？
- [ ] 推送与拉取的职责边界：推送未读数 + 列表通过 API 拉取（推荐），还是推送完整通知内容？

### 12.2 多租户与隔离

- [ ] 通知是否跨租户隔离（默认应隔离）？
- [ ] 系统公告是否支持跨租户推送（例如平台级公告）？

### 12.3 性能与容量

- [ ] 预期在线用户峰值是多少？（影响容量规划）
- [ ] 是否需要支持多实例部署？（影响 Redis Backplane 配置）
- [ ] 通知保留时长是多久？（影响数据清理策略）

### 12.4 UI/UX

- [ ] 通知中心的展示位置：Dock 弹窗 / 独立窗口 / 侧边栏？
- [ ] Toast 提示的样式与交互：点击跳转 / 自动消失 / 可关闭？
- [ ] 是否需要通知声音提示？

### 12.5 运维与监控

- [ ] 是否需要管理后台的通知管理功能？
- [ ] 监控指标的告警接收方式：邮件 / 企业微信 / 钉钉？
- [ ] 日志保留时长与归档策略？

---

**文档版本**：v2.0
**状态**：待评审
**负责人**：待定
**预计开始时间**：M7 里程碑（2026-01-06 起）
