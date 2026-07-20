# F4-C 聊天历史搜索与消息定位

> **状态**：F4-C-A 至 F4-C-D 已完成；专题已关闭
>
> **复核日期**：2026-07-19
>
> **适用主线**：正式 Web `/messages`；WebOS `/desktop` 复用同一 Chat App，Flutter 仅处理既有兼容阻断
>
> **关联文档**：[聊天室系统设计](./chat-system.md) · [聊天室 App 文档总览](./chat-app-index.md) · [正式 Web 一对一私聊与会话管理设计](./chat-direct-conversation-design.md)

## 摘要

- F4-C 在现有 `Channel / ChannelMember / ChannelMessage / DirectConversation` 上增加当前会话与全部可见会话的历史搜索，不建立平行消息库。
- 搜索权限与历史读取、消息窗口、附件和 Hub 加组复用同一服务端访问策略；管理员角色不能穿透私聊成员边界。
- 搜索正文使用由原消息内容派生的 `SearchText`，统一 mention 可见文本和大小写语义；原 `Content` 仍是唯一消息真值。
- 分页采用查询指纹、首批快照上界与 `(CreateTime, Id)` keyset cursor，不返回昂贵且容易漂移的总数。
- 搜索结果只返回安全摘要和结构化消息目标；点击后复用现有 `GetMessageWindow` 与 `/messages?channelId=&messageId=` 定位，不复制第二套历史加载逻辑。
- PC 使用可关闭的搜索侧栏，mobile 使用单列搜索结果视图；关键词不进入 URL、持久化 Store、跨标签广播或日志。
- F4-C 不包含 Reaction、置顶、阅读回执、附件 OCR、Console 私聊搜索、外部搜索平台或移动系统通知。

## 一、专题定位

一对一私聊完成后，Radish 已能稳定创建、恢复和定位会话，但用户只能顺序翻阅历史。随着公开频道和私聊积累，重新寻找一段讨论、链接、约定或历史回复会越来越困难。

F4-C 解决三个长期问题：

1. 用户可以在当前会话内按正文和时间找到历史消息。
2. 用户可以在自己当前有权读取的全部会话中寻找消息，并知道结果属于哪个会话。
3. 用户点击结果后能进入原消息上下文；消息已撤回、频道失效或权限变化时，不泄露旧正文，也不伪造可打开状态。

本专题不是搜索平台建设，也不把聊天内容用于推荐、分析或治理扫描。搜索只服务于当前用户主动发起的历史回看。

## 二、F4-C-A 现状审计

### 2.1 可复用基础

| 边界 | 当前事实 | F4-C 复用方式 |
| --- | --- | --- |
| 消息存储 | `ChannelMessage` 保存文本、图片说明、回复、撤回和 UTC 创建时间 | 原表继续作为唯一消息真值 |
| 历史分页 | `GetHistory` 支持 `beforeMessageId / afterMessageId` | 搜索结果定位后继续由历史分页承接上下文 |
| 消息定位 | `GetMessageWindow(channelId, messageId)` 已返回锚点前后窗口和双向分页标记 | 搜索不新增第二个窗口协议 |
| 访问策略 | `ChatChannelAccessService` 已覆盖公开、公告、私有群组和一对一会话 | 增加批量可见频道快照，规则仍由同一服务维护 |
| 私聊状态 | 待处理、接受、拒绝、阻断和账号不可用均有服务端状态 | 能查看历史的状态可以搜索；不能查看的状态不返回结果 |
| 正式 Web | `/messages` 已支持 `channelId / messageId` LongId 字符串深链 | 结果点击沿用同一 URL 与消息高亮 |
| 移动布局 | Chat App 已有列表 / 详情切换 | 搜索结果作为独立单列状态，不压缩 PC 多栏布局 |
| 实时恢复 | ChatHub 负责消息、撤回、未读和会话状态刷新 | 搜索页保持快照；目标打开时重新走权威读取 |

### 2.2 当前缺口

- 没有消息搜索 DTO、Vo、Service、Repository 或 HTTP API。
- `IChatChannelAccessService` 只有单频道访问判定；全部会话搜索若逐结果调用会形成授权 N+1。
- `ChannelMessage.Content` 包含 mention 存储标记，直接查询会把内部用户 ID 当作可搜索文本。
- SQLite 与 PostgreSQL 对大小写、`LIKE` 通配符和 Unicode 的默认行为不同，不能把 `Contains` 翻译差异当成稳定契约。
- 现有索引服务于频道历史和发送者时间线，没有全租户搜索的 `(TenantId, CreateTime, Id)` 顺序入口。
- 搜索翻页若只传最后 ID，新消息、时间相同消息和条件变化会造成重复或遗漏。
- `GetMessageWindow` 已能显示撤回占位，但搜索尚未定义结果产生后目标撤回、禁用或权限变化的处理。
- `ChatApp.tsx` 为 `1494` 行、`ChatApp.module.css` 为 `1461` 行、`ChatService.cs` 为 `1303` 行；继续在原文件堆搜索会越过仓库上限并扩大职责。
- 旧 Chat 文档仍存在“本地频道过滤”“已启用虚拟滚动”和私聊尚未实现等过期描述，需要同步校准。

## 三、用户路径与产品范围

### 3.1 当前会话搜索

1. 用户在已打开会话的标题区进入搜索。
2. 默认范围为“当前会话”，显示当前频道或私聊对象名称。
3. 用户输入关键词，可选择全部时间、最近 7 天、最近 30 天或自定义 UTC 时间边界对应的本地日期。
4. 提交后展示发送者、时间和正文摘要；不在前端已加载的 50 条消息中自行过滤。
5. 点击结果后加载目标消息窗口并高亮锚点；PC 保留搜索侧栏以便连续查看结果，mobile 隐藏搜索页并由 Back 恢复原结果和滚动位置；用户可继续向前或向后加载历史。

### 3.2 全部可见会话搜索

1. 用户把范围切换为“所有会话”。
2. 服务端在当前请求中重新计算可见频道，不依赖前端频道列表、归档视图或本地角色。
3. 结果额外展示会话名称、头像或频道图标；已归档会话仍可出现，因为归档不改变读取权。
4. 点击结果先选择或定向加载对应频道，再执行消息窗口定位。

全部会话包含：

- 当前租户启用的公开与公告频道；
- 当前用户是有效成员的私有群组；
- 当前用户按一对一会话状态有权保留历史的互关、陌生、已拒绝或被阻断会话；
- 接收人尚不可见且没有首条请求消息的空待处理会话没有消息，因此不会产生结果。

### 3.3 结果失效与恢复

- 消息在搜索后被撤回：结果快照不继续展示正文；打开时由 `GetMessageWindow` 返回撤回占位。
- 频道被禁用、删除或成员关系失效：打开目标返回统一不可用状态，保留搜索条件以便用户返回或重试。
- 私聊被阻断：阻断不删除双方已有历史，仍可搜索和举报，但不能发送。
- 网络失败：保留已返回结果和搜索表单，提供显式重试，不清空当前会话历史。
- 搜索期间产生新消息：当前结果保持首批快照；用户显式重新搜索后才进入新快照，不在分页中插入新项。

## 四、搜索语义

### 4.1 可搜索消息

搜索只覆盖：

- 未撤回、未软删除的 `Text` 消息；
- 具有用户可见文字说明的 `Image` 消息；
- 当前用户在请求时仍有权读取的频道。

不搜索：

- `System` 消息；
- 撤回消息的原正文；
- 图片二进制、文件名、EXIF、OCR 或附件元数据；
- 引用消息自动展开的旧正文；
- 本地 `sending / failed` 乐观消息；
- `sticker://` 等仅承担资源定位的内部协议文本。

### 4.2 SearchText 派生字段

`ChannelMessage` 增加可空 `SearchText`，只作为可重建派生数据：

1. 从当前消息 `Content` 生成用户可见纯文本。
2. mention `@[name](userId)` 只保留 `@name`，不把内部用户 ID 写入搜索语义。
3. 统一换行和连续空白，去除控制字符与纯资源协议。
4. 使用 .NET `ToLowerInvariant()` 生成稳定大小写归一文本。
5. 截断到与消息正文一致的安全长度。

搜索关键词执行同一规范化。`Content` 仍是展示、引用和审计真值；`SearchText` 不返回给客户端，也不允许业务修改接口直接写入。

新增消息在 Service 写入前同步生成 `SearchText`。历史数据通过 Chat schema ledger migration 分批回填；migration apply 完成后由 verify 检查缺失或不一致的派生值，不修改原正文。ledger 已记账后的再次 apply 只验证并跳过；未来若规范化规则变化，必须新增 migration 重建历史值。

### 4.3 匹配规则

- 关键词去除首尾空白后长度为 `2-100` 个字符；中文双字和英文短词均可使用。
- 使用规范化后的单个字面子串匹配，不做分词、同义词、拼音、模糊纠错或 OR 关键词扩展。
- `%`、`_`、反斜杠和引号都按普通字符搜索，不具备 SQL 通配符含义。
- Repository 使用参数化的字面包含函数：SQLite 与 PostgreSQL 分别采用等价的 `instr / strpos` 语义，不拼接 SQL 字符串。
- 时间范围采用 `[fromUtc, toUtc)`；只传一端时按单边界过滤，`fromUtc >= toUtc` 返回结构化校验错误。
- 结果按 `CreateTime DESC, Id DESC` 排序；同一创建时间使用消息 ID 稳定排序。

本批不承诺任意 Unicode 语言学分词。F4-C 的跨库一致性基线是中文、英文、数字、emoji、mention 可见名和 SQL 特殊字符的字面搜索。

## 五、权限与隐私

### 5.1 批量访问快照

`IChatChannelAccessService` 增加批量可见频道能力，返回当前用户在本次请求可读取的频道 ID 与必要展示元数据。批量实现必须与 `GetAccessAsync` 共用规则函数和测试矩阵，不能复制一份宽松的搜索权限。

约束：

- 当前会话搜索先校验单频道 `CanView`；无权访问统一返回 `404`，不泄露频道是否存在。
- 全部会话搜索只接受服务端可见 ID 集合；客户端传来的频道列表、会话分类或归档状态不参与授权。
- 普通 `Admin / System` 角色不能搜索自己不是成员的私聊或私有群组。
- 每一页都重新计算访问快照；全部会话 cursor 同时绑定首次可见频道集合哈希。集合发生增减时返回 cursor 失效并要求重新搜索，不能静默扩张、缩小或沿用旧权限结果。
- 租户 ID 始终来自当前身份，不接受客户端输入。

### 5.2 搜索词保护

- API 使用认证 `POST /api/v1/ChannelMessage/Search`，关键词不进入 URL、浏览器历史或代理查询日志。
- 前端不把关键词写入 `localStorage / sessionStorage`、Zustand 持久化、跨标签事件或诊断复制文本。
- 后端业务日志只记录 scope、页大小、耗时、结果数量和 TraceId，不记录关键词、摘要或消息正文。
- 搜索接口复用现有认证限流边界；校验失败和权限失败继续使用统一错误契约。
- Console 不新增搜索入口；用户举报快照仍是治理侧查看私聊内容的唯一正式来源。

## 六、数据、索引与迁移

### 6.1 实体调整

`ChannelMessage` 新增：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `SearchText` | `string?`，最大 4000 | 从可见正文派生的规范化搜索文本；不对客户端暴露 |

索引新增：

- `idx_channel_message_channel_search_order`：`(ChannelId, CreateTime DESC, Id DESC)`；
- `idx_channel_message_tenant_search_order`：`(TenantId, CreateTime DESC, Id DESC)`。

`SearchText` 使用前导任意位置子串匹配，普通 B-tree 不承担正文索引职责。两个顺序索引用于先按权限范围、时间和 keyset 缩小候选，再执行字面匹配。

### 6.2 Migration

F4-C-B 增加 Chat ledger migration：

```text
20260718_003_chat_message_search
```

职责：

- SQLite / PostgreSQL 增加 `SearchText` 和两个搜索顺序索引；
- 按消息 ID 小批次读取并回填派生文本，避免一次把全部历史加载到内存；
- 校验非撤回文本 / 图片说明消息的派生值，允许不可搜索消息保持 `null`；
- 支持 apply、重入、checksum、doctor、verify 和备份恢复；
- 不修改 `Content`、消息类型、撤回状态、已读游标或会话关系。

本专题不要求 SQLite FTS5、PostgreSQL `pg_trgm`、额外扩展权限或外部索引服务。若真实容量证明字面扫描不足，后续可以在 Repository 内替换为提供者专属索引，但必须保持本文 API、ACL、排序和匹配语义。

## 七、API 与 ViewModel

### 7.1 请求 DTO

`SearchChannelMessagesDto`：

| 字段 | 说明 |
| --- | --- |
| `Scope` | `CurrentChannel / AllVisibleChannels` |
| `ChannelId` | 当前会话范围必填；全部会话范围必须为空 |
| `Keyword` | 原始关键词，服务端规范化后长度 `2-100` |
| `FromUtc / ToUtc` | 可选 UTC 时间边界，`[from, to)` |
| `Cursor` | 可选不透明 cursor |
| `PageSize` | 默认 `20`，范围 `1-50` |

作用域与字段组合必须严格验证，不使用“有 channelId 就猜当前会话”之类的兼容推断。

### 7.2 返回契约

`ChannelMessageSearchPageVo`：

- `VoItems`
- `VoNextCursor`
- `VoHasMore`

`ChannelMessageSearchItemVo`：

- `VoChannelId / VoMessageId`
- `VoChannelDisplayName / VoChannelIcon / VoConversationKind`
- `VoPeerUserId / VoPeerPublicId / VoPeerAvatarUrl`（非一对一会话为空）
- `VoSenderUserId / VoSenderDisplayName / VoSenderAvatarUrl`
- `VoSnippet`
- `VoCreateTime`
- `VoMessageType`

所有 `long / long?` 继续由现有 JSON 契约传为字符串。`VoSnippet` 是服务端从当前原文生成的纯文本窗口，不包含 HTML；前端用文本节点和 `<mark>` 分段渲染，不使用 `dangerouslySetInnerHTML`。

不返回：

- 总结果数；
- 完整附件地址或附件元数据；
- 私聊参与者内部关系字段；
- `SearchText`；
- 客户端可据此自行推导权限的内部状态。

### 7.3 Cursor

首次查询生成版本化 cursor 上下文：

- `Version`
- `Scope / ChannelId`
- 规范化关键词与时间边界的 SHA-256 查询指纹
- 请求开始时的 `SnapshotMaxMessageId`
- 全部会话范围首次可见频道 ID 排序后生成的 `VisibleChannelSetHash`
- 当前页最后一项的 `LastCreateTimeUtc / LastMessageId`

后续页必须同时满足：

- 当前请求指纹与 cursor 一致；
- 消息 ID 不超过首次快照上界；
- 排序位置严格位于上一页最后项之后；
- 当前会话仍允许读取；或全部会话重新计算后的可见频道集合哈希与首次一致。

cursor 畸形、版本未知、跨条件复用或可见频道集合变化返回 `409 Chat.SearchCursorInvalid`，前端保留条件并提示重新搜索。搜索不使用总 revision；新消息通过重新提交获得新快照。

### 7.4 稳定错误

至少覆盖：

- `Chat.SearchKeywordInvalid`
- `Chat.SearchScopeInvalid`
- `Chat.SearchTimeRangeInvalid`
- `Chat.SearchCursorInvalid`
- `Chat.ChannelUnavailable`
- 通用网络、认证和限流错误

错误使用真实 HTTP 状态、稳定 `Code / MessageKey`、中英文资源和 TraceId，不把异常原文或 SQL 信息返回页面。

## 八、服务与代码边界

### 8.1 后端

新增独立边界：

```text
IChannelMessageSearchRepository / ChannelMessageSearchRepository
IChatMessageSearchService / ChatMessageSearchService
Radish.Model/ChatMessageSearchTextNormalizer
```

- Repository 只返回按查询顺序排列的 `ChannelMessage` 实体列表；通过请求 `pageSize + 1` 由 Service 判断 `hasMore`，不返回匿名对象或 ViewModel。
- Service 负责 DTO 校验、访问快照、cursor、用户 / 频道批量装配、摘要和 Vo 映射。
- `ChatService` 继续负责历史、定位、发送、撤回和未读，不继续扩入搜索实现。
- `ChannelMessageController` 只增加薄 `Search` action 并注入搜索 Service。
- 搜索文本规范化放在 `Radish.Model` 的纯函数中，由写入、migration 和测试共用，不维护三份正则，也不依赖 Service 或 DbMigrate 运行时。

### 8.2 前端

新增独立模块：

```text
apps/chat/ChatMessageSearchPanel.tsx
apps/chat/ChatMessageSearchPanel.module.css
apps/chat/useChatMessageSearch.ts
apps/chat/useChatMessageNavigation.ts
```

- 先把 `ChatApp.tsx` 中消息窗口加载、目标聚焦和高亮生命周期迁入 `useChatMessageNavigation`，再接入搜索结果。
- 搜索结果、表单、分页和错误由 `useChatMessageSearch` 管理，不写入全局 `chatStore.messageMap`。
- `ChatApp.module.css` 不继续增加搜索样式；新模块只消费现有 `--theme-*` 语义 token。
- `MessagesApp` 改为响应 `pushState / popstate` 的路由状态；搜索点击不整页重载，浏览器前进 / 后退可恢复频道和消息目标。
- WebOS 由同一 Chat App 内部导航到结果，不新增 WebOS API、Store 或搜索协议。

## 九、Pencil 与页面交互

F4-C-C 进入代码前更新 `Docs/frontend/design-sources/private-web-workflows.pen`：

- `P13C - Messages Search / Desktop`：当前 / 全部范围、时间筛选、结果、继续加载；
- `P13D - Messages Search States / Desktop`：空态、加载、错误、cursor 失效、结果目标失效和长文本；
- `P27C - Mobile Messages Search`：单列搜索表单与结果；
- `P27D - Mobile Messages Search States`：移动加载、空态、错误、离线和目标失效。

PC：

- 搜索按钮位于会话标题区；没有当前会话时仍可进入全部会话搜索。
- 搜索侧栏与成员面板共用右侧辅助区域，一次只展示一个，避免挤压消息正文。
- 结果项展示会话、发送者、时间和最多约三行摘要；继续加载位于列表底部。

Mobile：

- 搜索进入独立单列状态，顶部提供返回、关键词和范围入口。
- 点击结果进入消息详情并隐藏搜索页；返回可回到原搜索结果和滚动位置。
- 不把 PC 三栏、筛选侧栏或悬浮成员面板压缩到窄屏。

状态：

- 初始态解释搜索范围，不展示教学式大卡片；
- 加载保留输入和已返回页；
- 空态明确当前条件无结果并允许调整范围；
- 错误态说明原因和重试动作；
- cursor 失效要求重新搜索，不层层 fallback 到第一页并伪装连续分页；
- 离线时保留当前消息历史与已有搜索结果，禁用新搜索并提供恢复提示。

## 十、中英文、键盘与可访问性

- 固定文案进入 `locales/zh/chat.ts` 与 `locales/en/chat.ts`，消息、频道名和搜索词保持用户原文。
- 日期、时间和结果数量使用当前 locale formatter；不把 LongId 作为可见文本。
- 搜索按钮、范围切换、时间筛选、清空、关闭、结果和继续加载均可通过 Tab 到达。
- 表单提交使用 Enter；Esc 关闭搜索侧栏或移动搜索视图，不劫持浏览器原生 `Ctrl/Cmd+F`。
- 搜索状态使用 `aria-live` 的礼貌播报；结果列表有稳定列表语义，当前定位结果具有非颜色提示。
- `<mark>` 必须在四主题中保持对比度，焦点使用现有语义 token 和清晰 outline。
- 长英文、连续数字、emoji、mention 和 4000 字符正文摘要必须可换行，不扩大页面根宽度。

## 十一、实时、一致性与恢复

- 搜索是显式 HTTP 快照，不新增 `SearchResultChanged` Hub 事件。
- 新消息、撤回和会话状态事件继续更新正常 Chat 状态；已有搜索页不在后台重排分页。
- 点击任何结果都重新调用 `GetMessageWindow`，因此撤回占位、权限和频道状态以打开时为准。
- 搜索结果定位不会修改未读计数；只有更新页加载完毕、消息窗口真实到达尾部且活跃阅读面成立时，才通过 REST 提交实际可见的最高持久消息游标。
- REST 搜索失败不停止 ChatHub，不清空当前频道或草稿。
- 多标签页不广播关键词或结果；各标签分别按当前身份查询，避免账号切换后继承旧私聊搜索。
- 登出和账号切换立即清除内存中的搜索条件、结果、cursor 和摘要。

## 十二、实施批次

### F4-C-A：现状审计与专题设计（已完成）

- 交叉审计 Chat 数据、访问策略、历史 / 窗口 API、索引、正式 Web、WebOS、LongId、实时和测试。
- 固定当前 / 全部会话范围、派生搜索文本、ACL、cursor、页面、停止线和验证口径。
- 建立本文档并校准 Chat 文档入口与阶段状态。

本批只修改文档，不修改代码、数据库或 Pencil，不启动服务或执行浏览器 smoke。

### F4-C-B：服务端权威检索（已完成）

- 增加 `SearchText`、搜索顺序索引和 `20260718_003_chat_message_search` migration。
- 实现批量可见频道快照、专属 Repository / Service、POST API、cursor 和稳定错误。
- 补 SQLite / PostgreSQL migration、查询语义、ACL、分页快照、特殊字符和 LongId 测试。

本批不改正式 Web 页面结构，只允许增加 `@radish/http` / client 类型与静态契约测试。

2026-07-19 已完成实现与回归：派生文本、Chat ledger migration、批量 ACL、SQLite / PostgreSQL 字面检索、查询指纹与权限集合 cursor、POST API、稳定双语错误和 `@radish/http` 契约均已落地。详细证据见 [F4-C-B 服务端权威检索完成记录](../records/f4-c-b-chat-message-search-server-contract-2026-07-19.md)。

### F4-C-C：Pencil 与正式 Web（已完成）

- 先更新 P13C / P13D / P27C / P27D，再进入页面实现。
- 抽出消息导航 Hook，接入搜索面板、移动搜索、路由前进 / 后退和结果定位。
- 完成中英文、键盘、焦点、加载、空态、错误、离线、长文本和目标失效。
- WebOS 只做同组件阻断级兼容。

2026-07-19 已完成实现与静态回归：Pencil `P13C / P13D / P27C / P27D`、正式 Web 搜索工作区、内存搜索状态、消息导航 Hook、权威窗口定位、PC / mobile 布局、Back / Forward 恢复、中英文和完整页面状态均已落地。详细证据见 [F4-C-C 正式 Web 工作区完成记录](../records/f4-c-c-chat-message-search-web-workspace-2026-07-19.md)。

### F4-C-D：回归与成组验收（已完成）

- 执行 Service、Repository、Controller、migration、客户端与仓库卫生定向回归。
- 使用至少两个普通账号覆盖公开频道、互关私聊、陌生请求、拒绝 / 阻断和无权私有频道。
- 完成 `zh / en × 1920x1080 PC / 390x844 @ DPR 3 mobile` 搜索、分页、定位、撤回、权限变化和恢复矩阵。
- 记录并清理临时账号、频道、消息、会话、搜索样本、浏览器凭据、容器和备份。

2026-07-19 已完成成组验收：Gateway 正式 Web 与 WebOS 复用面覆盖 `zh / en × PC / mobile`、当前 / 全部会话、日期、分页、定位、Back / Forward、撤回、公开频道、互关私聊、陌生请求、拒绝 / 阻断和无权频道。验收修复了 Enter 提交、PC Forward 侧栏状态和撤回摘要残留三个共同根因；两批临时数据已精确清理，六库完整性正常。详细证据见 [F4-C-D 成组验收记录](../records/f4-c-d-chat-message-search-stage-acceptance-2026-07-19.md)。

## 十三、验证矩阵

### 13.1 后端与数据库

- SearchText：中文、英文大小写、mention、空白、emoji、资源协议、控制字符和 4000 字符边界。
- SQLite / PostgreSQL migration：空库、历史回填、重入、checksum、索引、备份恢复和严格 verify。
- 当前会话：频道存在 / 不存在、公开、公告、私聊、私有群组成员与非成员。
- 全部会话：归档、陌生请求、拒绝、阻断、账号失效、频道禁用和跨租户隔离。
- 查询：字面 `%/_/\\/引号`、中文、英文、时间边界、同时间消息和 page size。
- cursor：稳定翻页、新消息插入、条件变化、畸形、版本未知、跨账号、可见频道集合增减与单频道 ACL 变化。
- 结果：无总数、LongId 字符串、摘要不泄露内部 mention ID，附件和撤回正文不泄露。

### 13.2 前端与页面

- 当前 / 全部范围切换和时间筛选由请求字段驱动，不扫描 `messageMap` 或标题猜范围。
- 搜索加载、继续加载、重试和重新搜索不清空正常消息历史或草稿。
- 点击结果进入正确频道和消息窗口，前后历史、浏览器 Back / Forward 与高亮一致。
- 撤回、频道失效和权限变化具有明确状态，不自动跳到频道最新消息掩盖目标失败。
- PC 搜索侧栏与成员栏互斥，mobile 搜索 / 结果 / 消息详情返回路径稳定。
- 中文、英文、键盘、焦点、aria-live、长内容和四主题 token 通过。

### 13.3 开发与验收入口

开发中按风险执行：

- Chat search Service / Repository / Controller / migration 定向测试；
- client 搜索、路由、LongId、定位和静态结构测试；
- client / `@radish/http` / `@radish/ui` type-check；
- client lint 与 production build；
- 解决方案构建、Baseline Quick、repo hygiene 和 `git diff --check`。

Gateway、PC / mobile 浏览器和真实双账号只在 F4-C-D 成组验收执行；启动服务必须取得当轮授权。

## 十四、停止线与风险

本专题不做：

- Reaction、消息置顶、逐条阅读回执、消息编辑、转发或阅后即焚；
- 附件 OCR、图片内容识别、语音转写、拼音、同义词、模糊纠错或推荐；
- Console 私聊全文搜索、管理员私聊浏览或常态关键词扫描；
- Flutter 新搜索页、移动系统通知、PWA 或 Tauri 专属实现；
- Elasticsearch、OpenSearch、外部分词器或大而全搜索分析平台；
- 搜索词历史、热门词、行为聚合、生产使用证据采集或营销能力。

剩余风险：

- 字面子串搜索在超大历史量下仍需扫描时间 / 频道范围内候选；当前通过 keyset、顺序索引、页大小和可见范围控制，只有真实容量证明不足时才评估 FTS。
- `SearchText` 是可重建派生数据，未来修改 mention 或文本规范时必须通过新 migration 重建，不能静默改变历史搜索语义。
- 多 API 实例不影响 HTTP 搜索正确性；搜索页不实时插入新结果，用户需显式重新搜索获得新快照。
- 本轮内置浏览器只支持 CSS 视口，不支持 DPR 覆盖和离线网络切换；`390 × 844` 布局已实跑，DPR 3、真实断网恢复、实体手机键盘、系统分享和原生通知仍是明确未覆盖边界。

## 十五、完成标准

只有同时满足以下条件，F4-C 才能关闭：

1. 当前与全部可见会话搜索均只返回当前用户有权读取的未撤回消息。
2. SearchText、migration、排序索引和字面匹配在 SQLite / PostgreSQL 语义一致。
3. cursor 在新消息、条件变化和重复请求下不产生重复、越权或静默跳页。
4. 搜索结果使用结构化频道 / 消息目标，点击后复用权威窗口并正确处理撤回和失效。
5. 正式 Web 的 PC / mobile、中英文、键盘、焦点、长文本、离线和错误状态完整。
6. WebOS 复用面无阻断，Flutter 未被扩成新的通知或聊天搜索主线。
7. 定向测试、构建、Baseline Quick、仓库卫生和双账号成组验收通过，临时数据完整清理。
8. 文档明确记录完成结果、剩余风险和下一顺位。

2026-07-19 的 F4-C-D 已满足以上条件，F4-C 正式关闭。下一顺位进入 F4-D-A 聊天消息 Reaction 的现状审计与专题设计；置顶、逐条已读和移动系统通知继续分别后置。
