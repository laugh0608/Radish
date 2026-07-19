# F4-F-B 聊天轻量阅读回执服务端权威契约完成记录

> 日期：2026-07-19
>
> 结论：F4-F-A / B 已完成，下一顺位进入 F4-F-C Pencil 与正式 Web / WebOS 共用页面。

## 本批边界

本批在 [F4-F 权威专题设计](/features/chat-message-read-receipt-design) 已确认的边界内，完成 Chat 已读游标、隐私聚合、REST、Hub 失效提示和 `@radish/http` 契约。没有修改 Pencil、Store 或消息页面，没有启动 Radish 服务，也没有执行 Gateway 或浏览器 smoke。

## 完成内容

### 数据、迁移与单调游标

- `ChannelMember.LastReadMessageId` 继续作为唯一持久化阅读事实；未新增逐消息回执表、精确已读时间或前端重复计数。
- 新增专属 `ChatReadReceiptRepository`：条件更新只接受更大目标；相同或更旧目标返回当前权威游标，Public / Announcement 缺少跟踪行时允许原子创建，Private / Direct 不恢复缺失或软删除成员。
- 过渡期消息发送和旧 Hub 写入也复用同一 Repository，不再直接覆盖成员实体；`JoinChannel` 已退回纯访问校验，不能恢复成员、清除归档或推进已读。
- Chat ledger migration `20260719_006_chat_read_receipt` 显式建立并验证 `(ChannelId, UserId)` 唯一索引、`UserId` 索引和 `(ChannelId, IsDeleted, LastReadMessageId, UserId)` 聚合索引；历史重复成员或负数游标不会被静默修复。
- PostgreSQL 实跑发现 `ChannelMember` 的历史 `[SugarIndex]` 没有被 Code First 物化，新 migration 因此把既有唯一关系一并纳入显式契约；索引存在性按 PostgreSQL 表 / schema 和 SQLite provider 分别使用真实可靠入口。

### 权限、查询与分页

- `PUT ChannelReadState/Advance` 校验当前 `CanView`、消息归属和精确 `readThroughMessageId`，允许撤回占位作为有序游标目标，返回游标、未读、mention 和 `changed`。
- `POST ChannelReadReceipt/GetSummaries` 对去重后最多 20 条当前账号自己所发、未撤回消息批量读取；Public / Announcement 和受抑制 Direct 统一返回 `mode=none`，不泄露成员规模或会话状态。
- 普通 Private 以数据库 join / group 聚合当前有效成员的 `readCount`，同时校验 `JoinedAt <= message.CreateTime` 与 `LastReadMessageId >= message.Id`；不把全量成员加载到应用内存。
- Accepted、未阻断且对端可用的 Direct 只返回 `peerHasRead` 边界；Pending / Declined / Blocked / 对端不可用不读取或返回对端游标。
- `GET ChannelReadReceipt/GetReaders` 仅允许普通 Private 消息发送者调用，按 50 人上限和 `UserId` keyset 分页；opaque cursor 绑定版本、租户、账号、频道和消息，Main 库用户与头像按页批量装配，无跨库 N+1。

### API、Hub 与共享客户端契约

- 新增 `ChannelReadStateController`、`ChannelReadReceiptController`、DTO / Vo、稳定 `Code / MessageKey` 和中英文服务端错误资源。
- 游标实际前进后向当前账号组广播 `ChannelUnreadChanged`；允许回执的频道另发 `ReadReceiptsChanged { channelId }`，事件不包含读者、头像、人数或游标。
- `@radish/http` 新增全部 LongId 字符串类型、`none / private_group / direct` mode、读者 cursor 和 Hub 失效载荷契约；页面消费留给 F4-F-C。
- HTTP 联调资产已补精确游标、批量摘要和普通 Private 读者分页示例。
- 未读、通知、最后消息、搜索、Reaction、置顶和频道排序的既有数据与协议没有新增耦合。

## 验证结果

- F4-F / Chat 定向：SQLite migration、创建 / 单调推进 / 旧请求、归档不变、软删除成员拒绝、人数聚合、加入时间、keyset、Public / Private / Direct 隐私、cursor 绑定及 Hub 无个人数据载荷均通过。
- PostgreSQL 17 migration 重入、三项显式索引和双连接并发首次写入另行实跑 `2/2` 通过；一次性容器已停止并自动删除。
- 后端全量：`938` 项通过，`24` 项 PostgreSQL 环境用例按配置跳过；解决方案 Debug 构建 `0 warning / 0 error`。
- `@radish/http` type-check / lint 与 `16` 项测试通过；LongId 字符串安全扫描通过。
- Baseline Quick 通过，其中 `@radish/ui 23`、client `442`、Console `56` 项测试通过；敏感字面量、时间语义、权限、Repo Quality 与身份门禁通过。
- 未启动 Radish 服务，未执行 Gateway、PC / mobile 浏览器或 WebOS smoke；这些运行态路径按停止线留给 F4-F-D，且执行前仍需当轮启动授权。

## 下一顺位

进入 F4-F-C：先更新 PC / mobile Pencil，再在正式 `/messages` 与 WebOS 共用 `ChatApp` 中实现活跃阅读面、精确 REST 游标提交、断线内存重试、Store 权威摘要和 Hub 失效去抖重读；随后实现 Direct 单一已读边界、普通 Private 人数入口与 PC Popover / mobile Bottom Sheet、中英文复数、键盘、无障碍、四主题和账号 reset。消费者迁移并验证后删除 `ChatHub.MarkChannelAsRead`。
