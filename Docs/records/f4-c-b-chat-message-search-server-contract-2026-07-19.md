# F4-C-B 聊天历史搜索服务端权威检索完成记录

> 日期：2026-07-19
>
> 结论：F4-C-B 已完成，下一顺位进入 F4-C-C Pencil 与正式 Web 搜索工作区。

## 本批边界

本批只建立聊天历史搜索的服务端权威契约和 `@radish/http` 类型边界，没有修改 Pencil、正式 `/messages` 页面、WebOS 布局或 Flutter，也没有启动 Radish 服务或执行浏览器 smoke。

## 完成内容

### 数据与迁移

- `ChannelMessage.SearchText` 作为可重建派生字段，不替代或修改原 `Content`。
- `ChatMessageSearchTextNormalizer` 由消息写入、migration 和测试共用，统一 mention 可见名、空白、控制字符、内部资源协议、Unicode Form C、invariant 大小写和 4000 字符边界。
- 新增 `idx_channel_message_channel_search_order` 与 `idx_channel_message_tenant_search_order`，用于先按权限范围和 keyset 顺序缩小候选。
- 新增 Chat ledger migration `20260718_003_chat_message_search`，覆盖 doctor、分批历史回填、apply、verify、重入和 SQLite 备份恢复；撤回消息同步清空 `SearchText`。

### 权限与查询

- `IChatChannelAccessService` 增加批量可读取频道快照，并与单频道 REST、Hub 和附件访问复用同一访问规则。
- 当前会话先校验服务端 `CanView`；全部会话只使用服务端重新计算的可见频道集合，管理员角色不能穿透私聊或私有频道成员边界。
- 专属 `IChannelMessageSearchRepository` 在 SQLite 使用参数化 `instr`、PostgreSQL 使用参数化 `strpos`，`%`、`_`、反斜杠和引号均为普通字面字符。
- 查询固定按 `CreateTime DESC, Id DESC` 排序，首次查询记录消息 ID 快照上界，不返回总结果数。

### Service、API 与隐私

- 新增 `IChatMessageSearchService`、`POST /api/v1/ChannelMessage/Search`、请求 DTO、分页 Vo 和 `@radish/http` 契约。
- cursor 绑定租户、账号、scope、频道、规范化条件、时间范围、首次消息快照、可见频道集合哈希与最后 `(CreateTime, Id)`。
- cursor 畸形、跨条件、跨账号、版本未知或权限集合变化统一返回 `409 Chat.SearchCursorInvalid`。
- 返回结果只包含结构化频道 / 消息目标、安全纯文本摘要和必要展示字段；不返回 `SearchText`、总数、附件元数据或私聊内部关系字段。
- 关键词使用 POST body，不进入 URL；业务日志只记录 scope、页大小、结果数、耗时和 TraceId，不记录关键词、摘要或正文。

## 验证结果

- 后端全量：`896` 项通过，`18` 项环境用例按配置跳过。
- F4-C-B PostgreSQL 17：migration 与 Repository 字面查询 `2` 项实跑通过，覆盖小写物理标识、`SearchText` 回填、索引和 `strpos` 特殊字符语义。
- 解决方案构建：`0 warning / 0 error`。
- `@radish/http`：type-check 与 lint 通过。
- `npm run validate:baseline:quick`：通过；其中 client `426` 项、Console `56` 项通过。
- `git diff --check`：通过。

PostgreSQL 验证使用一次性 `postgres:17-alpine` 容器；测试结束后容器已正常关闭并自动删除，Docker Desktop 已恢复为退出状态。

## 下一顺位

进入 F4-C-C：先更新 Pencil `P13C / P13D / P27C / P27D`，再实现正式 Web 搜索工作区、移动搜索结果和 `GetMessageWindow` 权威定位。真实 `zh / en × PC / mobile` 双账号矩阵留给 F4-C-D。
