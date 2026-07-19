# F4-E-B 聊天消息置顶服务端权威契约完成记录

> 日期：2026-07-19
>
> 结论：F4-E-A / B 已完成，下一顺位进入 F4-E-C Pencil 与正式 Web。

## 本批边界

本批在 [F4-E 专题设计](/features/chat-message-pin-design) 已确认的边界内，完成 Chat 消息共享置顶的数据、权限、并发、API、实时和前端 HTTP 契约。没有修改 Pencil 或消息页面，没有启动 Radish 服务，也没有执行 Gateway 或浏览器 smoke。

## 完成内容

### 数据、迁移与一致性

- 新增 Chat 库 `ChatMessagePin`，以 `(TenantId, ChannelId, MessageId)` 唯一事实保存置顶、取消、恢复和操作审计；不向消息复制 `IsPinned`，也不使用频道单条指针。
- `Channel.PinRevision` 从 `0` 开始，只有新增、恢复、取消或撤回活跃置顶时才在同一事务内递增。
- Chat ledger migration `20260719_005_chat_message_pin` 建立表、查询 / 唯一索引和 revision，doctor / verify 覆盖历史频道、非负 revision、孤儿或已撤回引用及每租户频道 20 条上限。
- 消息撤回事务同时软删除全部活跃 Reaction 和对应活跃置顶；置顶发生变化时递增 revision，提交后依次广播撤回与最新完整置顶快照。

### 权限、写入与并发

- `ChatChannelAccessResult` 增加独立 `CanPinMessages`：Public / Announcement 允许 System / Admin 或 Moderator / Owner，普通 Private 只允许本频道 Moderator / Owner，Accepted Direct 允许未阻断且对端可用的双方。
- 普通 Private 与 Direct 均不允许 System / Admin 穿透成员和会话边界；Pending / Declined / Blocked Direct 保持只读。
- 写入使用 `IsPinned` 目标状态，重复目标状态不递增 revision；每频道最多 20 条活跃置顶，达到上限时不自动淘汰旧上下文。
- SQLite 使用共享进程级频道写入协调与事务；PostgreSQL 使用 tenant / channel 级 transaction advisory lock。置顶、取消、权威快照读取和消息撤回共享同一并发边界。
- 写入提交后返回当时或更新的权威完整快照；并发请求已经推进 revision 时不会把合法新状态误报为服务端错误。

### Service、API、Hub 与客户端契约

- 新增 `IChatMessagePinService`、`GET ChannelMessagePin/GetState` 与 `POST ChannelMessagePin/Set`；状态返回 `channelId + revision + ChatMessagePinVo[]`，每项嵌入当前可见 `ChannelMessageVo`。
- Controller 只在状态实际变化时向频道组广播 `MessagePinsChanged`；payload 与 HTTP 完整快照一致，无变化请求不制造实时噪声。
- `ChannelVo.VoCanPinMessages` 由统一 ACL 派生；`@radish/http` 建立 LongId 字符串、读取和目标状态写入契约，正式 Store 与页面交互留给 F4-E-C。
- 新增中英文稳定错误：无效参数、无管理权限、目标不可用、并发冲突和 20 条上限。
- 置顶不产生通知或系统消息，不改变未读、mention、最后消息、搜索文本、正文、排序或频道列表位置。

## 验证结果

- SQLite migration、目标状态新增 / 无变化 / 取消 / 恢复、revision、20 条上限、撤回联动、ACL、Service 和 Controller / Hub 定向测试通过。
- PostgreSQL 17 migration 与双连接并发目标状态用例另行实跑 `2/2` 通过；一次性容器完成后已停止并自动删除。
- 后端全量：`922` 项通过，`22` 项环境用例按配置跳过；解决方案构建 `0 warning / 0 error`。
- `@radish/http`、`@radish/ui`、client、Console type-check 通过；Baseline Quick 通过，其中 `@radish/http 16`、`@radish/ui 23`、client `437`、Console `56` 项测试通过。
- 仓库敏感字面量、时间语义、权限、Repo Quality 与身份门禁通过；changed repo hygiene 与 `git diff --check` 通过。

## 下一顺位

进入 F4-E-C：先更新 PC / mobile Pencil，再实现正式 `/messages` 与 WebOS 共用的紧凑置顶条、完整列表、消息动作、定位、revision Store、Hub 乱序合并、重连 HTTP 追平、撤回清理、账号 reset、中英文、键盘和四主题。真实多账号运行态矩阵留给 F4-E-D。
