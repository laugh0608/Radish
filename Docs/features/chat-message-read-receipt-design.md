# F4-F 聊天轻量阅读回执设计

> 本文是 Radish 聊天阅读回执的权威专题，固定用户价值、隐私矩阵、游标语义、数据与接口边界、正式 Web / WebOS 页面、失败恢复、开发批次和完成标准。
>
> **状态**：`F4-F-A / B / C 已完成，下一批为 F4-F-D 定向回归与成组验收`
>
> **版本**：v26.7.2
>
> **最后更新**：2026.07.19
>
> **关联文档**：
> [聊天室 App 文档总览](./chat-app-index.md) ·
> [聊天室系统设计](./chat-system.md) ·
> [聊天室 App 实时与同步设计](./chat-app-realtime.md) ·
> [正式 Web 一对一私聊与会话管理设计](./chat-direct-conversation-design.md) ·
> [聊天历史搜索与消息定位设计](./chat-message-search-design.md)

---

## 一、结论摘要

F4-F 不沿用“仅在线成员、最多 3 个头像、最近 5 条消息”的历史草案，改为以下长期契约：

1. `ChannelMember.LastReadMessageId` 继续作为个人未读与阅读回执的唯一持久化游标，不新增逐消息回执表。
2. “已读”只表示用户在真实活跃、可见且位于会话尾部的阅读面中展示到某条持久化消息；不表示理解、同意、送达或任务完成。
3. Public 与 Announcement 仅维护当前用户自己的未读游标，不向其他用户展示成员身份、头像、人数或比例。
4. 普通 Private 只允许消息发送者查看自己消息的已读人数和当前有效读者；Direct 只在 Accepted、未阻断且双方可用时向发送者展示对端已读边界。
5. 客户端上报精确的 `readThroughMessageId`，服务端使用数据库原子单调推进；不再由服务端在请求处理时猜“当前最新消息”。
6. REST 是游标写入和回执读取的权威边界；Hub 只广播失效提示，不承载可覆盖本地状态的读者、头像、计数或游标快照。
7. 在线状态与阅读事实完全解耦。读者离线后回执继续存在，进程内 Presence 不参与回执查询、排序或保留。

本专题服务于登录态聊天复访和私域沟通确定性，不建设聊天行为分析、公告覆盖率或在线监控平台。

---

## 二、现状审计

### 2.1 已有能力

- `ChannelMember` 已有 `(ChannelId, UserId)` 唯一关系、`LastReadMessageId`、`JoinedAt`、`ArchivedAt`、角色和软删除状态。
- 频道列表与详情按服务端游标计算未读：只统计游标之后、未撤回、非当前用户发送的消息；mention 使用相同范围。
- `ChatHub.MarkChannelAsRead` 落库后向 `user:{userId}` 广播 `ChannelUnreadChanged`，现有多标签可以同步个人未读结果。
- 历史接口按消息 ID 前后分页，每页最多 50 条，并包含撤回占位；搜索定位复用 `GetMessageWindow`。
- 正式 `/messages` 与 WebOS `/desktop` 共用 `ChatApp`、`chatStore`、聊天 API 和 `ChatHub`，无需建立第二套页面协议。
- 频道 Presence 按连接计数，单个 API 进程内能区分同一账号多个标签页；前端每 15 秒读取一次在线成员。

### 2.2 真实缺口

| 缺口 | 当前事实 | F4-F 裁决 |
|------|----------|-----------|
| 游标目标不精确 | Hub 只传 `channelId`，服务端处理时查询最新未撤回消息 | 客户端提交实际展示到的 `readThroughMessageId` |
| 游标可回退 | 当前成员实体读后无条件覆盖；乱序请求或最新消息撤回可写入更小 ID | 专属 Repository 原子执行 `max(current, target)` |
| 后台误报 | 初始历史加载后立即标记已读，没有统一检查页面可见、焦点或 WebOS 前台窗口 | 共用“活跃阅读面”判定 Hook 后才能推进 |
| Hub 是写入口 | 与“REST 写入、Hub 广播”的现有聊天边界不一致 | 客户端迁移后删除 Hub 写命令 |
| 在线与阅读混用 | Presence 是进程内瞬时状态，旧草案却只展示在线读者 | 回执完全不依赖在线状态 |
| 没有隐私矩阵 | Public、Announcement、普通 Private 与 Direct 没有独立可见规则 | 按本专题矩阵执行服务端 ACL |
| 没有权威读取 | 历史响应、`ChannelMemberVo` 和 Store 均无回执状态 | 新增发送者受限的摘要与读者分页接口 |
| 成员写入职责混杂 | `JoinChannel` 共用方法会恢复成员并清除 `ArchivedAt` | 加组、成员、归档与已读分别治理 |
| Presence 文档漂移 | 旧文档声称使用 Redis Set，真实实现是进程内计数 | 文档按真实实现校准，不在本专题重构 Presence |
| 缺少回归资产 | 现有测试未覆盖已读单调性、撤回、乱序、多标签和隐私矩阵 | F4-F-B 建立服务端定向与 PostgreSQL 用例 |

### 2.3 废止的历史口径

以下内容不再作为实现依据：

- `GetOnlineMembers` 增加 `LastReadMessageId`；
- 通过 `MemberReadUpdated` 广播头像和成员游标；
- 只显示在线成员的阅读状态；
- 消息气泡最多显示 3 个头像，超出显示 `+N`；
- 只为最新 5 条消息渲染回执；
- 按“已读时间倒序”排列读者。

现有模型没有独立已读时间，Presence 也不是阅读真相源。继续扩展 `ChannelMemberVo` 会把在线瞬时状态、成员资料和持久阅读状态耦合成重复契约。

---

## 三、用户价值与明确语义

### 3.1 用户价值

- Direct 发送者可以确认对方是否已经读到自己最近的消息，减少重复追问。
- 普通 Private 的发送者可以查看自己消息的当前有效已读人数，并在需要时展开读者列表。
- 多标签、断线重连和 WebOS 兼容入口对同一阅读事实保持一致，不因连接状态变化回弹。
- Public 与 Announcement 不暴露个人阅读行为，也不制造无法定义受众分母的虚假统计。

### 3.2 “已读”的定义

一次合法游标推进同时满足：

1. 当前账号仍具备频道 `CanView`。
2. 目标是当前频道中已经持久化的消息或撤回占位，不是乐观临时消息。
3. 正式 Web 页面处于可见状态且浏览器具有焦点。
4. WebOS 聊天窗口未最小化、位于最前，并且浏览器页面本身可见且有焦点。
5. 消息视口已经完成布局，处于会话尾部，没有尚未加载的更新页。
6. 客户端提交的是此时消息列表中实际展示到的最高持久化消息 ID。

消息加载成功、进入频道、加入 Hub 组、搜索命中、定位历史消息或收到通知都不自动等于已读。

### 3.3 不承诺的语义

- 不承诺消息已成功推送到某个设备。
- 不承诺用户阅读了正文的每个字、打开了图片或理解了内容。
- 不记录精确阅读时间、阅读设备、停留时长或回执历史轨迹。
- 不根据在线状态、输入状态、窗口挂载或 Hub 订阅推断已读。

---

## 四、隐私与可见性矩阵

| 频道 / 会话状态 | 当前用户可推进个人游标 | 发送者可见回执 | 说明 |
|-----------------|------------------------|----------------|------|
| Public | 是 | 否 | 不展示身份、头像、人数或比例 |
| Announcement | 是 | 否 | 公告覆盖率属于未来独立聚合分析，不复用成员游标 |
| 普通 Private，有效成员 | 是 | 仅自己发送的消息 | 显示已读人数；读者详情只含当前有效且加入时间不晚于消息的成员 |
| 普通 Private，非成员 / 已退出 | 否 | 否 | 管理员身份也不自动穿透 |
| Direct Pending，发起人 | 是 | 否 | 不泄露接收方是否打开陌生请求 |
| Direct Pending，接收人已可见历史 | 是 | 否 | 可清理自己的未读，但不产生对外回执 |
| Direct Accepted、未阻断、双方可用 | 是 | 仅对端发送者可见 | 以单个“已读边界”呈现，不显示在线头像 |
| Direct Declined | 按现有历史可见规则 | 否 | 不暴露拒绝后的阅读行为 |
| Direct Blocked | 按现有历史可见规则 | 否 | 双向抑制回执；历史可见不等于回执可见 |
| Direct 对端停用 / 删除 | 按现有历史可见规则 | 否 | 不继续暴露对端状态 |

普通 Private 的读取接口必须验证目标消息由当前用户发送。不能通过请求其他成员的消息 ID 枚举群内阅读行为。

---

## 五、数据模型与游标

### 5.1 唯一持久真相

继续使用：

```text
ChannelMember.LastReadMessageId
```

其语义固定为“该账号在当前成员周期内确认展示到的最高消息顺序位置”。消息 ID 是游标版本，客户端和服务端都只接受更大的值。

首批不新增：

- `ChatMessageReadReceipt` 逐消息表；
- `LastReadAt` 或精确阅读时间；
- 频道级回执 revision 热点行；
- 在线成员中的重复读游标；
- 前端持久化的独立已读计数。

### 5.2 索引

F4-F-B 已由 Chat ledger migration `20260719_006_chat_read_receipt` 增加面向回执聚合的覆盖索引，列顺序为：

```text
ChannelId, IsDeleted, LastReadMessageId, UserId
```

PostgreSQL 实跑同时确认 `ChannelMember` 的历史 `[SugarIndex]` 没有被 Code First 物化，因此该 migration 还显式补齐并验证既有 `(ChannelId, UserId)` 唯一索引和 `UserId` 索引；发现重复成员或负数游标时拒绝静默修复。迁移覆盖 SQLite、PostgreSQL、首次应用、重入和严格 verify，SQLite 备份恢复继续复用 Chat ledger 通用门禁。

### 5.3 成员周期

- Direct 两名参与者在第一条消息之前建立成员关系，因此都可使用原始 `JoinedAt`。
- 普通 Private 新增成员时，必须把 `JoinedAt` 设为本次成员周期起点，并把个人未读基线设到加入时的当前消息尾部。
- 成员退出使用软删除，立即从回执人数和读者详情中移除。
- 重新加入必须建立新的成员周期；不能恢复旧 `JoinedAt` 后把离开期间消息视为已读。
- 归档只影响当前用户的会话列表分区，不退出成员关系，也不删除或重置游标。

普通 Private 当前没有正式成员管理页面。F4-F 只固定以上长期契约并通过测试夹具覆盖，不顺带开发成员管理系统。

### 5.4 撤回与保留

- 消息撤回不回退任何成员游标。
- 已撤回消息不展示回执摘要或读者详情，但仍可作为有序历史中的游标目标。
- 未读继续排除已撤回消息，避免撤回造成未读回弹。
- 回执没有独立保留周期：它从当前成员游标和仍保留的消息派生。消息或成员关系被依法物理清理后，不保留额外阅读数据。

---

## 六、服务端权威契约

### 6.1 推进个人已读游标

```http
PUT /api/v1/ChannelReadState/Advance
Content-Type: application/json

{
  "channelId": "...",
  "readThroughMessageId": "..."
}
```

响应使用结构化 Vo：

```text
channelId
lastReadMessageId
unreadCount
hasMention
changed
```

服务端流程：

1. 校验 LongId、频道和消息归属，消息读取包含已撤回占位。
2. 复用 `ChatChannelAccessService` 校验 `CanView`，不接受客户端成员或频道类型声明。
3. Public / Announcement 缺少个人跟踪行时允许原子创建；Private / Direct 必须已有有效成员。
4. Repository 使用条件更新或等价原子 SQL，只在 `LastReadMessageId IS NULL OR LastReadMessageId < target` 时推进。
5. 唯一键竞争、相同目标和更旧目标均收敛到当前权威游标，不作为业务失败。
6. 更新后重新计算个人未读与 mention，并向 `user:{userId}` 广播 `ChannelUnreadChanged`。
7. 仅在游标实际前进且频道允许回执时发送回执失效提示。

该写入不能修改 `ArchivedAt`、`Role`、`IsDeleted`、`JoinedAt` 或 Direct 生命周期。

### 6.2 批量回执摘要

```http
POST /api/v1/ChannelReadReceipt/GetSummaries
Content-Type: application/json

{
  "channelId": "...",
  "messageIds": ["..."]
}
```

约束：

- `messageIds` 去重后最多 20 条；
- 目标必须属于同一频道、未撤回且由当前用户发送；
- 响应固定包含 `channelId + mode + items`，其中 `mode` 只允许 `none / private_group / direct`；
- Public / Announcement 以及不允许暴露回执的 Direct 状态返回 `mode=none, items=[]`，不返回人数或成员数据；
- 普通 Private 返回每条消息的 `readCount`，只统计当前有效、`JoinedAt <= message.CreateTime` 且 `LastReadMessageId >= message.Id` 的其他成员；
- Direct 只在允许状态返回 `peerHasRead`，判定条件同样是对端当前有效且游标不小于目标消息 ID；
- 当前用户不计入自己消息的读者人数；
- 摘要读取不得因 `mode=none` 暴露频道成员规模、对端账号状态或 Direct 生命周期细节。

### 6.3 读者详情分页

```http
GET /api/v1/ChannelReadReceipt/GetReaders
    ?channelId=...
    &messageId=...
    &cursor=...
    &pageSize=50
```

- 仅普通 Private 提供；Direct 无需读者列表。
- 只允许消息发送者读取自己的消息。
- `pageSize` 范围为 `1-50`，按 `UserId` keyset 分页，不使用 offset。
- Chat 库先取得一页当前有效读者 ID，Main 库再批量装配显示名、PublicId 和头像，保持原顺序并禁止 N+1。
- cursor 绑定当前账号、频道和消息；账号或目标变化必须拒绝复用。
- 页面是实时当前视图，不伪装成历史快照。成员或权限在翻页期间变化时，关闭并重新打开即可读取最新集合。

### 6.4 错误与权限

新增契约必须使用稳定 `Code / MessageKey` 和真实 HTTP 状态，至少区分：

- 参数或消息集合无效；
- 频道 / 消息不存在或不可见；
- 目标消息不是当前用户发送；
- 读者 cursor 无效。

摘要接口对 Public / Announcement 和不允许暴露回执的 Direct 状态统一返回 `mode=none`，不把隐私抑制作为可枚举的错误。读者详情只支持普通 Private；对其他频道类型或 Direct 状态的详情请求返回稳定“不支持”错误，且不得附带成员或生命周期信息。

服务端不因管理员身份绕过普通 Private 或 Direct 成员边界。

---

## 七、Hub 与 Store 权威边界

### 7.1 Hub 方法

F4-F-C 已删除：

```text
ChatHub.MarkChannelAsRead
```

客户端不再通过 Hub 执行业务写入。`JoinChannel` 只负责授权后的实时加组和 Presence，不推进已读、不恢复成员、不取消归档。

### 7.2 服务端事件

保留：

```text
ChannelUnreadChanged
```

新增轻量失效提示：

```text
ReadReceiptsChanged
{
  channelId
}
```

事件不携带读者 ID、头像、计数、前后游标或完整回执快照。当前加入频道的客户端收到后，只对当前加载且由自己发送的消息执行去抖批量重读。

失权但仍残留在旧 SignalR 组中的连接最多收到无个人数据的失效提示；后续 HTTP 读取仍会被最新 ACL 拒绝。本专题不借此扩展全站 SignalR 成员撤销治理。

### 7.3 Store

- `chatStore` 只缓存服务端返回的当前频道回执摘要，不自行累计阅读人数。
- 权威 HTTP 结果可覆盖当前摘要；Hub 事件只增加失效世代并触发重读。
- 切换账号、失权、Direct 状态变化、撤回或频道离开时清理相关摘要和详情。
- 重连不依赖事件重放，直接重读当前加载消息的摘要。
- 同一批次只请求当前消息窗口中最近最多 20 条、由当前用户发送且未撤回的持久化消息。

---

## 八、在线、离线、多标签与重连

### 8.1 在线与离线

- 已落库回执不会因用户离开频道、关闭页面或断开 Hub 而消失。
- Presence 可以继续服务在线成员面板，但不得过滤、排序或决定阅读回执。
- 断线期间若用户阅读了已经缓存的消息，前端只在内存保留该账号的最高待提交游标。
- 网络恢复后，仍需校验账号、频道、活跃阅读面和权限，再幂等提交；不建立跨登出或跨浏览器重启的长期本地回执队列。

### 8.2 多标签

- 每个标签提交自己实际看到的最高消息 ID。
- 服务端原子单调推进保证乱序、重复和较旧请求不会回退游标。
- `ChannelUnreadChanged` 继续向当前账号全部连接同步个人未读。
- 回执查看端通过失效提示重读权威摘要，不通过本地增量加减。

### 8.3 重连

- 重连后先恢复频道订阅和历史，再读取回执摘要。
- 只有当前阅读面仍然可见、有焦点并到达尾部，才提交新的精确游标。
- 搜索定位或历史窗口存在更新页时不推进；加载完全部更新页并真实到达尾部后才允许推进。

---

## 九、正式 Web / WebOS 页面

### 9.1 Direct

- 只在“对端已读到的最后一条当前用户发送消息”下显示一次 `已读 / Read`。
- 不在所有更旧消息上重复标签，不显示对端在线头像。
- 最新消息尚未读时，较旧的边界仍可保留；不伪造“已送达”状态。
- Pending、Declined、Blocked 或对端不可用时完全隐藏。

### 9.2 普通 Private

- 自己发送且当前加载的消息在 `readCount > 0` 时显示可操作摘要，例如 `3 人已读 / Read by 3 people`。
- PC 使用紧凑 Popover 或侧浮层显示读者；mobile 使用 Bottom Sheet。
- 读者按 50 人一页继续加载，头像只作辅助，同时显示可读姓名。
- `readCount = 0` 时不显示占位，避免把缺少送达语义误写成“未读”。

### 9.3 Public 与 Announcement

- 不渲染阅读回执入口或占位。
- 在线成员面板保持现有独立能力，不与回执混排。

### 9.4 共用阅读面判定

正式 Web 和 WebOS 使用同一个 Hook：

- 正式 `/messages`：检查 `document.visibilityState === 'visible'`、页面焦点、激活频道和消息尾部。
- WebOS：在相同条件上追加当前 Chat 窗口未最小化且 `zIndex` 为最前窗口。
- 布局完成后读取消息视口，不因请求成功或预定的 `requestAnimationFrame` 自动判定已读。
- Scroll、键盘翻页、触摸滚动、发送成功和新消息到达都复用同一判定函数。

### 9.5 中英文、键盘与无障碍

- 回执摘要使用真实 `button`，支持 `Enter / Space` 打开。
- `Escape` 关闭 Popover / Bottom Sheet，并把焦点还给触发按钮。
- 详情具备可读标题、焦点约束、加载、错误、空态和分页状态。
- 不用 `aria-live` 高频播报每一次阅读变化；用户主动打开详情时再宣布人数和加载结果。
- 英文人数使用 i18n 复数规则，不拼接固定 `people`。
- 四套主题只消费现有语义 token，不新增页面级硬编码颜色。

---

## 十、性能、并发与幂等

### 10.1 大频道策略

- Public / Announcement 不提供回执，从产品边界上避免对开放受众做无界聚合。
- 普通 Private 摘要每次最多 20 条自己消息，读者详情每页最多 50 人。
- 聚合查询使用 Chat 库覆盖索引，不把全量成员游标加载到应用内存。
- 读者资料按页跨库批量装配，不逐用户查询 Main 库。
- Hub 只广播频道级失效提示，不推送全成员或全消息快照。
- 前端对连续失效提示去抖合并，不为每个游标推进立即发起多轮重复请求。

F4-F-B 已使用大规模普通 Private 测试夹具覆盖数据库聚合、返回上限与 keyset 分页，并由 migration 用例验证覆盖索引；不以易波动的墙钟耗时作为唯一 CI 断言。

### 10.2 并发与幂等

- 相同目标重复提交：成功且 `changed=false`。
- 较旧目标晚到：返回当前权威游标，不回退。
- 两个标签并发推进：最终值为两者最大目标。
- 成员行首次创建竞争：依赖唯一索引收敛并重读赢家。
- 消息在校验后撤回：仍允许把游标推进到其有序位置，响应后不展示该消息回执。
- 权限在写入前失效：写入拒绝；权限在写入后失效：后续回执查询立即按最新 ACL 抑制。

---

## 十一、不污染边界

F4-F 不改变：

- 未读排除自己消息和撤回消息的现有规则；
- 通知中心分类、偏好、聚合、角标或移动系统通知；
- `Channel.LastMessageId / LastMessageTime` 与会话排序；
- 搜索文本、搜索 cursor、结果范围和消息定位协议；
- Reaction、置顶及其 revision / operation ledger；
- 撤回现有 Reaction / 置顶事务联动；
- Public / Announcement 在线成员面板；
- Flutter、Tauri、Console 或频道成员管理页面。

搜索定位到历史中部不会修改未读。只有用户加载完更新页并在活跃阅读面真实到达尾部，才推进精确游标。

---

## 十二、A-D 开发批次

### F4-F-A：现状审计与专题设计

- 交叉审计 `ChannelMember.LastReadMessageId`、未读计算、在线成员、Hub、历史接口、Store、正式 `/messages`、WebOS 共用页面和历史草案。
- 固定本文的语义、隐私矩阵、数据与接口、性能、页面、停止线和完成标准。
- 同步 Chat 入口与规划，明确旧在线头像方案失效。

停止线：不修改模型、Repository、Service、Controller、Hub、HTTP 客户端、Store、Pencil 或页面代码。

### F4-F-B：服务端权威契约（已完成）

- 增加 Chat ledger migration 和回执聚合索引。
- 建立专属 Repository 的原子游标推进、摘要聚合和读者 keyset 分页。
- 实现 `ChannelReadState`、`ChannelReadReceipt` Service / Controller / DTO / Vo 和稳定双语错误。
- 将个人已读写入迁移到 REST，拆开 `JoinChannel`、成员恢复、归档与游标职责。
- 增加 `ReadReceiptsChanged` 失效提示和 `@radish/http` 契约；删除 Hub 写命令需等 F4-F-C 消费者迁移完成。
- 覆盖 SQLite / PostgreSQL、四类频道、Direct 全状态、撤回、成员周期、单调并发、幂等和大频道查询。

完成事实见 [F4-F-B 服务端权威契约记录](/records/f4-f-b-chat-message-read-receipt-server-contract-2026-07-19)。

停止线：不提前修改 Pencil、正式 Web 页面、通知、Presence、Flutter 或 Tauri。

### F4-F-C：正式 Web 与 WebOS 共用页面（已完成）

- 先更新 Pencil PC / mobile 权威设计源。
- 实现活跃阅读面共用 Hook、精确游标提交、断线内存重试、回执 API 和 Store 失效重读。
- 实现 Direct 已读边界、普通 Private 摘要与 PC Popover / mobile Bottom Sheet。
- 完成中英文复数、键盘、无障碍、四主题和账号 reset。
- 正式 `/messages` 与 WebOS 只复用同一 `ChatApp`，不建立壳层分叉。
- 消费者迁移并验证后删除 `ChatHub.MarkChannelAsRead`。

完成事实见 [F4-F-C 正式 Web 与 WebOS 共用页面记录](/records/f4-f-c-chat-message-read-receipt-web-workspace-2026-07-19)。

停止线：不实现公告分析、在线状态重构、移动系统通知、Flutter 或 Tauri。

### F4-F-D：定向回归与成组验收

- 三个普通账号覆盖 Public、Announcement、普通 Private、Direct Pending / Accepted / Declined / Blocked / 对端不可用。
- 覆盖 `zh / en × PC / mobile`、正式 Web、WebOS、键盘和无障碍代表路径。
- 覆盖多标签乱序、真实断线重连、后台标签、WebOS 遮挡 / 最小化、撤回、成员加入 / 退出、失权和角色变化。
- 复核未读、通知、最后消息、搜索、Reaction、置顶和频道排序没有污染。
- 修复共同根因，补自动化，清理临时账号、频道、会话、消息、通知、凭据与备份，并执行六库完整性和严格 migration verify。

真实 smoke 前必须由用户在当轮确认或授权启动前后端；不得沿用历史会话启动状态。

---

## 十三、验证矩阵

| 层级 | 核心验证 |
|------|----------|
| Repository | 原子单调推进、唯一键竞争、聚合索引、20 条摘要、50 人 keyset、无 N+1 |
| Service / ACL | Public / Announcement 禁止对外回执；Private 仅发送者；Direct 全状态；成员加入 / 退出 / 失权 |
| API / Hub | LongId 字符串、稳定错误、真实状态码、Hub 只失效不写入、不泄露个人载荷 |
| Migration | SQLite / PostgreSQL 首次、重入、doctor、apply、verify、SQLite 备份恢复 |
| Store | 权威覆盖、失效去抖、撤回清理、账号 reset、重连重读、多标签不累计 |
| 阅读面 | 页面可见 / 失焦、PC / mobile 尾部、搜索定位、WebOS 前台 / 遮挡 / 最小化 |
| UI | Direct 边界、Private 人数与分页、中英文复数、键盘、焦点恢复、四主题 |
| 不污染 | 未读、通知、最后消息、搜索、Reaction、置顶、频道排序 |

开发中先执行定向测试、type-check、lint、build、repo hygiene 与 `git diff --check`；F4-F-D 再执行获授权的真实运行态矩阵。

---

## 十四、停止线与完成标准

### 停止线

- 不做 Public / Announcement 阅读统计、成员画像或公告覆盖分析。
- 不把 Presence 重构为 Redis、SignalR Backplane 或跨实例在线平台。
- 不新增逐消息回执事件表、精确已读时间线或审计导出。
- 不新增通知中心事件、Web Push 或移动系统通知。
- 不扩 Flutter、Tauri、Console 成员管理或完整频道权限系统。
- 不把“已读”升级为送达、理解、同意或任务完成状态。

### 完成标准

1. 游标在撤回、乱序、重试和多标签下只能前进。
2. 后台标签、失焦页面和被遮挡 / 最小化的 WebOS 窗口不能误报已读。
3. Public / Announcement 不泄露阅读身份或聚合人数。
4. 普通 Private 和 Direct 全状态符合服务端可见性矩阵。
5. 回执不依赖在线状态，离线和重连后可从服务端权威恢复。
6. 摘要和读者查询有固定上限、keyset 分页、覆盖索引，且无全成员 Hub 快照和跨库 N+1。
7. 正式 Web / WebOS 的 PC、mobile、中英文、键盘、无障碍和四主题完成成组验收。
8. 未读、通知、最后消息、搜索、Reaction、置顶和频道排序无回归。
9. 临时数据清理、数据库完整性和严格 migration verify 通过，专题记录可追溯。
