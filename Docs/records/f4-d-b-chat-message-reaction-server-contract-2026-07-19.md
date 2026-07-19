# F4-D-B 聊天消息 Reaction 服务端权威契约完成记录

> 日期：2026-07-19
>
> 结论：F4-D-A / B 已完成，下一顺位进入 F4-D-C Pencil 与正式 Web。

## 本批边界

本批先完成 [F4-D 专题设计](/features/chat-message-reaction-design)，再实现 Chat 消息 Reaction 的数据、权限、幂等、API、实时和前端 HTTP 契约。没有修改 Pencil 或消息页面，没有启动 Radish 服务，也没有执行 Gateway 或浏览器 smoke。

## 完成内容

### 数据、迁移与生命周期

- 新增 Chat 库 `ChatMessageReaction` 与 `ChatMessageReactionOperation`，不把私聊消息接入 Main 库匿名通用 Reaction。
- `ChannelMessage.ReactionRevision` 默认从 `0` 开始，只有实际状态变化才在同一事务内原子递增。
- Chat ledger migration `20260719_004_chat_message_reaction` 建立两张表、唯一 / 查询 / 过期索引和消息 revision，支持历史消息默认值、doctor、重入与 verify。
- 消息撤回改为仓储原子操作：同一 Chat 事务内撤回消息、清空 `SearchText` 并软删除全部活跃回应。
- operation ledger 保留 30 天；Hangfire 每日通过固定批次清理过期事实，不在普通请求中执行无界扫描。

### 权限、写入与并发

- `ChatChannelAccessResult` 增加独立 `CanReact`：公开 / 公告可读即可回应，普通 Private 仅成员，一对一 Direct 仅已接受、未阻断且对端可用时允许写入。
- 写入使用 `IsActive` 目标状态和 `ClientOperationId`，同指纹重复请求回放、异指纹复用返回稳定 `409`，不采用会因重试反转的 Toggle。
- 单用户在单条消息最多保留 10 种活跃回应；取消使用软删除，恢复复用原唯一事实。
- SQLite 使用进程级短写锁与事务；数据库唯一竞态无法回放时转换为 `Chat.ReactionConcurrentConflict`，不泄漏 SQL 异常。
- sticker 写入校验当前租户可用资源，并保存附件快照；Unicode 与 sticker 使用统一聚合 VO。

### Service、API、Hub 与客户端契约

- 新增 `IChatMessageReactionService`、`POST ChannelMessageReaction/GetStates` 与 `POST ChannelMessageReaction/Set`；批量读取上限 100，任一目标不可读时整批拒绝。
- 返回 `messageId + revision + ReactionSummaryVo[]` 完整快照；无回应消息也返回空聚合和当前 revision。
- Controller 只在权威状态实际变化时向当前频道组广播 `MessageReactionsChanged`，幂等回放和无变化写入不制造实时噪声。
- `@radish/http` 与 client API 已建立 LongId 字符串、读取和目标状态写入契约；正式 Store 与页面交互留给 F4-D-C。
- 新增中英文稳定错误：无效参数、不可回应、目标不可用、幂等冲突、并发冲突、上限和 sticker 不可用。
- 首批不产生通知中心事件，不修改未读、最后消息、搜索文本、消息排序或正文。

## 验证结果

- SQLite migration、desired-state 新增 / 无变化 / 取消 / 恢复、同 operation 回放 / 异指纹冲突、revision、10 种上限、过期清理和撤回一致性测试通过。
- ACL、Service 聚合 / 禁写 / 并发错误、Controller 当前身份与 Hub 完整快照广播测试通过。
- PostgreSQL 17 migration 重入 / revision 默认值 / 唯一约束与并发目标状态收敛用例另行实跑 `2/2` 通过；一次性容器完成后已停止并自动删除。
- 后端全量：`906` 项通过，`20` 项环境用例按配置跳过；解决方案构建 `0 warning / 0 error`。
- `@radish/http` 与 client 的 type-check / lint 通过。
- `npm run validate:baseline:quick` 通过：`@radish/http 16`、`@radish/ui 21`、client `430`、Console `56` 项测试通过，其余版本、权限、敏感字面量、时间语义、Repo Quality 与身份门禁通过。
- changed repo hygiene 检查 `21` 个已修改文本文件无问题，`git diff --check` 通过。

## 下一顺位

进入 F4-D-C：先更新 PC / mobile Pencil，再实现正式 `/messages` 与 WebOS 共用的消息回应展示、Unicode / sticker picker、目标状态 operation ID、revision Store、Hub 乱序合并、重连追平、撤回移除、中英文、键盘与四主题。真实双账号运行态矩阵留给 F4-D-D。
