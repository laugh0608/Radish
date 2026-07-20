# F4 2026-07-18 日终提交回顾与文档审阅

> 日期：2026-07-18（Asia/Shanghai）
>
> 范围：`400e4d6a^..0f726526` 的 13 个当日提交；本次日终文档提交自身不计入回顾范围。

## 今日结论

- F4-A 完成首轮证据归因和采集合同后，已按个人开发者节奏冻结为最终收尾资产；缺少主动生产证据不再阻断明确的功能、维护、设计或技术债任务。
- 一对一私聊按 A-D 四批完成数据与访问边界、会话生命周期、正式 Web 页面和真实成组验收，专题关闭。
- 通知中心深化按 A-D 四批完成稳定定义、权威分组 / 摘要、偏好、聚合、结构化目标、正式 Web 页面和三普通账号成组验收，专题关闭。
- F4-C-A 已完成聊天历史搜索与消息定位的现状审计和专题设计；下一工程批次固定为 F4-C-B 服务端权威检索契约。
- 今天没有遗留未提交代码；日终只校准文档，不启动服务、安装依赖、修改数据库或重新执行运行态验收。

## 今日全部提交

| 提交 | 主题 | 日终结论 |
| --- | --- | --- |
| `400e4d6a` | `docs(planning): 记录 F4-A 证据归因结论` | 生产证据与本地受控样本分离，唯一明确生产 UX 证据完成归因。 |
| `587ed98e` | `docs(planning): 建立 F4-A 生产证据采集合同` | 脱敏字段、六链路入口和记录模板建立，但不等于已经取得生产证据。 |
| `14907d1f` | `docs(planning): 调整个人开发者功能推进节奏` | 主动采集冻结到最终完成体，协作文件与规划同步长期约束。 |
| `42359a58` | `docs(chat): 完成一对一私聊专题设计` | 固定复用频道消息体系、`DirectConversation` 元数据、ACL、页面和四批边界。 |
| `36714e56` | `feat(chat): 建立私聊数据与访问边界` | Chat migration、成员 ACL、消息幂等基础与私有附件访问落地。 |
| `d32526a8` | `feat(chat): 完成私聊会话生命周期与可靠消息` | 请求、接受 / 拒绝、阻断、归档、权威摘要和可靠附件绑定完成。 |
| `fda796b7` | `feat(chat): 完成正式 Web 私聊页面` | Pencil、公开个人页入口、`/messages` PC / mobile 会话工作区和双语状态落地。 |
| `f737b82a` | `fix(chat): 收口私聊成组验收问题` | 修复 Hub 生命周期、活跃会话已读和查询凭据日志，完成临时数据清理。 |
| `a8386128` | `docs(notification): 完成通知中心深化设计` | 固定通知定义、目标、偏好、聚合、权威摘要、容量、迁移与四批边界。 |
| `29ec0c6c` | `feat(notification): 建立权威通知收件箱契约` | Message 库分组 / 摘要 / 偏好事务、新 API、revision Hub 和迁移落地。 |
| `9b9d33ac` | `feat(notification): 完成正式 Web 通知工作区` | Pencil、`/notifications`、共享 Store、导航 / Workbench / WebOS 复用面迁入权威契约。 |
| `f37fd750` | `fix(notification): 收口权威收件箱成组验收` | 修复 delivery 前滚、写入行数、关注恢复、触发者、目标解析和 Hub 生命周期，F4-B 关闭。 |
| `0f726526` | `docs(chat): 完成历史搜索专题设计` | 固定 F4-C 的 SearchText、ACL、跨库查询、cursor、定位、页面和 A-D 批次。 |

## 代码与文档交叉审阅

### F4-A 与协作节奏

- `AGENTS.md` 与 `CLAUDE.md` 都已加入个人开发者阶段约束；F4-A 指南、记录和模板均明确标为冻结的最终收尾资产。
- `Docs/planning/current.md`、总开发路线与发布后功能完成线都已把功能推进恢复为默认节奏，没有把生产证据作为 F4-C 前置。
- 协作文件其余入口专属表述与历史结构差异不是今天新增的规则分叉，本次没有扩成无关重写。

### 一对一私聊

- 代码已经存在 `DirectConversation`、Chat ledger migration、频道成员访问服务、持久化消息幂等、可靠附件绑定和独立生命周期 Controller / Service / Repository。
- 正式 Web 已存在公开个人页发消息入口、`/messages` 会话分区、请求动作、阻断 / 归档状态、mobile 列表 / 详情切换和结构化双语错误；WebOS 复用同一 `ChatApp`。
- Chat 文档已在 `0f726526` 校准私聊完成态、真实组件目录、字符串 LongId、历史分页与普通 DOM 现状；“私聊尚未实现”“已启用虚拟滚动”“本地频道搜索”等过时口径已退出当前说明。
- `private-web-workflows-design.md` 本次补记私聊 A-D 完成事实，并明确 F4-C 搜索画板留到 F4-C-C 更新。

### 通知中心

- 当前 Controller 已提供 `GetInbox / GetInboxSummary / MarkInboxGroupsAsRead / MarkAllAsRead / DeleteInboxGroup / GetPreferences / UpdatePreferences`；正式前端由 `notificationInboxSync`、`notificationStore` 和 `NotificationInboxChanged` 对账。
- `notificationActionQueue.ts` 已删除；页面使用服务端分类、summary、聚合分组和结构化 target，跨标签只广播账号隔离 revision，旧 `UnreadCountChanged` 只触发回源。
- 日终审阅发现 `notification-center.md` 与 API / frontend / implementation / realtime 四个 Guide 仍停留在 F4-B-A 状态，引用旧路由、旧事件、客户端关键字分类和已删除文件。本次已全部按现行代码重写或校准。
- `notification-center-deepening.md` 的摘要已从审计前问题描述更新为 F4-B 完成态；审计章节继续保留作为专题历史依据，不追溯改写。
- 旧 `GetNotificationList / GetUnreadCount / MarkAsRead / DeleteNotification` 仍在代码中保留给兼容消费者；删除前必须单独审计 Flutter 和其他调用方，不混入 F4-C。

### 设计源与下一专题

- `private-web-workflows.pen` 与设计说明已记录私聊 `P13 / P13B / P27 / P27B`、通知 `P12 / P12B / P26 / P26B / P26C` 的当前页面族。
- F4-C-A 按停止线只完成专题设计，没有提前修改 Pencil 或页面；F4-C-C 再扩展 `P13C / P13D / P27C / P27D`。
- `current.md` 已新增 2026-07-19 明日事项，按数据、规范化、ACL / Repository、Service / API、测试与停止线推进完整 F4-C-B 服务端批次。

## 今日验证回顾

以下结果来自各功能提交和专题验收中已经记录的实际执行，本次日终文档批次不重复运行代码测试：

- 私聊 C 批：client `422` 项、type-check、lint、production build 与 Baseline Quick 通过。
- 私聊 D 批：SQLite / PostgreSQL migration、Chat Service / Controller / Hub / 附件定向回归及三个普通账号 PC / mobile 运行态矩阵通过，临时数据与凭据清理完成。
- 通知 B 批：后端 `869` 项、解决方案构建、Baseline Quick、正式 Web build 与 PostgreSQL 定向 migration / Outbox 通过。
- 通知 C 批：client `425` 项、后端通知定向 `25` 项、相关 workspace type-check、lint、production build 和 Baseline Quick 通过。
- 通知 D 批：client `426` 项、后端定向 `44` 项、PostgreSQL `2/2`、解决方案构建、Baseline Quick、三普通账号 `zh / en × PC / mobile` 与 WebOS 成组验收通过。
- F4-C-A：staged repo hygiene 与 `git diff --check` 通过。

## 明日事项

1. 完成 `SearchText` 实体字段、共享规范化器、搜索顺序索引和 `20260718_003_chat_message_search` migration，覆盖历史分批回填、doctor、apply、verify、重入与备份恢复。
2. 建立批量可见频道快照和专属搜索 Repository，复用现有成员 ACL；实现 SQLite / PostgreSQL 参数化字面量包含与稳定 `(CreateTime DESC, Id DESC)` keyset。
3. 实现搜索 Service、POST API、DTO / Vo、双语稳定错误和 `@radish/http` 契约；cursor 绑定查询指纹、消息快照、可见频道集合哈希和最后排序位置。
4. 按共同根因补 normalizer、migration、ACL、跨租户、私聊状态、特殊字符、cursor、新消息、权限变化、LongId 和隐私日志测试，并完成静态与数据库验证。
5. F4-C-B 不修改 Pencil、`/messages` 页面或 Flutter；F4-C-C 再完成设计源和正式 Web，F4-C-D 再执行真实双账号成组验收。

## 当前不做

- 不重启 F4-A 主动生产证据采集，不把证据不足作为 F4-C-B 的阻断。
- 不在日终文档批次修改业务代码、迁移、依赖或数据库，不启动服务或执行浏览器 smoke。
- 不删除旧通知 API，不扩 SignalR Backplane、Flutter 新页面、Reaction、置顶或阅读回执。
- 不提前修改 F4-C Pencil 或正式 Web 页面，不把服务端批次和页面批次混成一次不可审阅的大提交。
