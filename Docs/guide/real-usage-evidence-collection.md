# F4 真实使用证据采集与反馈归因

> 适用范围：正式发布后的登录、首次内容参与、回应后回流、聊天、通知和 Console 管理六条链路。
>
> 当前基线：`v26.7.1.1204-release`，首次观察窗从 2026-07-12 起覆盖连续 7 至 14 天。
>
> 记录模板：[F4-A 生产证据采集模板](/records/f4-a-production-evidence-capture-template)

## 1. 目标与证据边界

本说明用于把生产运行事实、可追溯用户反馈和工程验证分开，并为维护分级与完整功能选题提供可复核输入。

可参与维护分级或功能判断的材料包括：

- 明确来自生产环境并带时间、版本、影响路径的运行事实。
- 能排除种子账号、开发者 smoke、自动化和维护动作的业务数据。
- 保留来源、原始时间、用户目标、期望与实际、复现条件的直接反馈。
- 能关联用户路径、业务目标、失败、恢复和最终结果的审计或日志。

以下材料只能证明工程能力或历史失败，不能证明真实使用价值：

- local / Development 日志与 SQLite 数据。
- 单元测试、集成测试、浏览器 smoke、页面可达和构建结果。
- 发布前设计审阅、Pencil 反馈和开发者受控写入。
- 已经关闭的部署、迁移、镜像和候选门禁失败。

一次查询命中、一条通知已读或一个页面请求，都不能单独解释为用户完成了任务。不能从现有字段证明的因果关系必须保留为观测盲区。

## 2. 采集安全边界

### 2.1 生产只读

- 数据库会话必须使用只读账号；若暂时只能使用既有运维账号，查询前显式执行 `BEGIN TRANSACTION READ ONLY`，结束时执行 `ROLLBACK`。
- 不执行迁移、DDL、临时业务表写入、状态修复、Outbox 重放或数据清理。
- 不启动或重启服务，不因采集改变日志级别、采样率或生产配置。
- 长查询先用 `EXPLAIN` 和受限时间窗确认索引路径；不得为一次观察在生产临时建索引。

### 2.2 脱敏与保留

仓库只接收汇总和必要的脱敏案例，不接收以下内容：

- 邮箱、IP、token、Cookie、client secret、证书或数据库连接串。
- 帖子、评论、聊天、通知、举报、审核备注或请求体的完整正文。
- 数据库备份、完整日志压缩包和包含生产账号的截图。
- 可跨批次反查真实用户的固定哈希。

每个观察批次使用新的随机盐生成临时匿名用户键，原始标识与盐只保留在受控分析环境。仓库记录使用 `U-001`、`A-001`、`CASE-001` 等批次内编号。原始材料的保留位置只记录受控系统名称和访问责任人，不写本地绝对路径或凭据。

### 2.3 排除集合

采集前先建立并人工确认排除集合：

- `System`、首位管理员和已知运维管理员。
- `TestUser`、`F3DTester717` 及其他种子、验收、演示账号。
- 发布部署、健康检查、探针、自动化和故障演练产生的动作。
- 开发者为复核 F1、F2、F3 或首次管理员门禁而执行的生产维护操作。

排除集合应保存账号匿名编号、排除原因和确认人，不在仓库记录邮箱或内部账号明文。无法确认身份的样本标记为“身份待确认”，不得归入真实采用统计。

## 3. 批次准备与获取入口

### 3.1 固定批次参数

开始采集前记录：

- 观察窗起止时间和统一时区，默认使用 UTC 计算、Asia/Shanghai 展示。
- 生产版本、commit 和部署完成时间。
- 租户范围、官方 OIDC client 范围和排除集合版本。
- Main、Log、Message、Chat、OpenIddict 数据源的快照时间。
- Auth、Gateway、API 日志的获取时间、保留范围和缺失区间。
- 用户反馈渠道的获取入口、原始材料责任人和脱敏责任人。

不同数据源的快照时间不一致时必须写明偏差，不能把后采集的数据倒推为较早时间点已经发生。

### 3.2 宿主日志

部署编排中的正式入口为 `deploy/docker-compose.yaml`。有生产主机只读权限的操作者可从 `auth / gateway / api` 服务获取指定时间窗日志；输出保存到仓库外的受控目录，再人工脱敏汇总。

建议入口形式：

```bash
docker compose -f deploy/docker-compose.yaml --env-file <production-env-file> \
  logs --since <ISO-8601-start> --until <ISO-8601-end> auth gateway api
```

注意事项：

- 不把 `<production-env-file>`、日志原文或命令历史复制到仓库。
- Auth 的 `Account/Login` 信息日志包含脱敏邮箱、查询结果、密码校验结果和 Cookie 登录完成；它可以统计已知登录阶段，不能证明用户最终回到原任务。
- 当前 Auth 信息日志没有稳定持久化入口类别、`client_id`、安全归一化后的 `returnUrl` 或登录后的首个任务。
- 当前 Gateway 没有可直接承担用户路径归因的结构化访问事实；外层反向代理若保留访问日志，可作为独立来源，但必须先确认其时间、采样、机器人排除和隐私口径。
- 日志缺失、轮转截断或级别过滤要作为证据质量问题记录，不能按“没有失败”处理。

### 3.3 PostgreSQL

生产编排将业务数据分为以下数据库：

| 数据库 | 默认库名 | 本批用途 |
| --- | --- | --- |
| Main | `radish` | 用户、内容参与、浏览历史、治理、商城流水、Main Outbox |
| Log | `radish_log` | `AuditLog_*` 写审计 |
| Message | `radish_message` | `Notification_*`、`UserNotification` |
| Chat | `radish_chat` | `ChannelMessage`、`ChannelMember`、Chat Outbox |
| OpenIddict | `radish_openiddict` | application、authorization、token 状态汇总 |

通过生产 PostgreSQL 的只读连接执行查询。表名和列名以生产 `information_schema` 为准：SqlSugar 的 PostgreSQL 物理标识符按小写落库，通知和审计还使用时间分表，不能把 C# 类型名直接拼成固定生产表名。

每个库先执行以下元数据检查，再按实际分表构造只读查询：

```sql
BEGIN TRANSACTION READ ONLY;

SELECT table_schema, table_name
FROM information_schema.tables
WHERE table_schema = 'public'
ORDER BY table_name;

SELECT table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public'
ORDER BY table_name, ordinal_position;

ROLLBACK;
```

元数据检查结果只用于确认表、分表和字段存在，不作为用户证据。正式导出只选择本说明列出的必要字段，并在受控环境完成匿名化和聚合。

## 4. 六条链路的采集合同

### 4.1 登录

数据源：

- Auth `Account/Login` 结构化日志。
- OpenIddict application、authorization、token 的状态与时间汇总。
- 外层反向代理或 Gateway 可用的目标访问事实。
- 带原始时间和路径的直接用户反馈。

最少输出：

- 日期、批次匿名用户、官方 client、入口类别。
- 登录阶段结果：输入拒绝、用户不存在、密码失败、Cookie 登录完成、授权失败或 token 失败。
- 是否在合理时间内恢复到原目标，恢复来源与可信度。
- 失败后的重试次数、最终结果和用户反馈编号。

判断边界：

- Cookie 登录完成只说明 Auth 会话建立，不等于 OIDC 流程完成。
- token 存在不等于页面任务完成。
- 当前日志无法稳定得出入口和原任务恢复；没有代理访问事实或直接反馈时，这两列必须标记为“不可判断”。

### 4.2 首次内容参与

Main 读取范围：

- `User.CreateTime`：候选用户创建时间。
- `Post.AuthorId / CreateTime / IsPublished / IsDeleted`。
- `Comment.AuthorId / CreateTime / IsEnabled / IsDeleted`。
- `PostAnswer.AuthorId / CreateTime / IsDeleted`。
- `PostQuickReply.AuthorId / CreateTime / Status / IsDeleted`。
- `ContentSubmissionRecord.UserId / OperationType / Status / ErrorCode / CreateTime / CompleteTime / ResultType / ResultId`。

最少输出：

- 每个合格新用户的注册时间、第一次成功参与类型和时间差。
- 参与前的提交失败码、重试次数和最终结果。
- 是否有成功业务实体与 `ContentSubmissionRecord` 相互印证。
- 来源路径是否可知；未知时不得根据业务类型反推入口。

判断边界：

- 访问帖子、保存浏览历史或打开编辑器不算内容参与。
- 草稿放弃目前没有稳定事实，不把“没有成功实体”直接解释为用户放弃。
- 种子内容、管理员验收内容和迁移生成内容必须排除。

### 4.3 回应后回流

读取范围：

- Message `Notification_*` 的 `Type / BusinessType / BusinessId / TriggerId / CreateTime`。
- Message `UserNotification` 的 `UserId / NotificationId / DeliveryStatus / DeliveredAt / IsRead / ReadAt / RetryCount`。
- Main `UserBrowseHistory` 的 `UserId / TargetType / TargetId / RoutePath / LastViewTime`。
- 目标页面访问事实和用户反馈。

最少输出：

- 回应事件、接收用户、业务目标、通知创建与投递状态。
- 已读时间、同一业务目标的后续访问时间、后续参与事实。
- 每段关联使用的字段、时间窗口和可信度。

判断边界：

- `IsRead=true` 不等于用户点击通知。
- `LastViewTime` 没有 notification/source 标识；仅凭同用户、同目标和时间接近，只能登记为“候选关联”。
- 只有来源参数、可信访问日志或直接反馈明确关联时，才记为“回应后回流”。

### 4.4 聊天

Chat 读取范围：

- `ChannelMessage.ChannelId / UserId / Type / ReplyToId / CreateTime / IsDeleted`。
- `ChannelMember.ChannelId / UserId / LastReadMessageId / JoinedAt / IsDeleted`。
- `ReliableOutboxMessage.TaskType / AggregateType / AggregateId / Status / AttemptCount / LastErrorCode / OccurredAtUtc / ProcessedAtUtc`。
- Hub 连接、重连与异常日志，以及用户复制诊断或反馈。

最少输出：

- 匿名用户、匿名频道、发送时间、消息类型和回复关系。
- 参与天数、相邻发送间隔、成员已读游标和对端参与情况。
- Outbox 失败、重试、DeadLetter、重复或最终处理状态。
- 断连、重连和重复发送是否有直接用户影响。

判断边界：

- 单条消息、同一操作者连续验收消息和种子频道不算持续会话。
- `LastReadMessageId` 只能说明已读游标状态，不能证明消息内容被理解或任务完成。
- 没有客户端重试 ID 或失败反馈时，不能从消息数量反推重复发送原因。

### 4.5 通知

读取范围：

- Message `Notification_* / UserNotification`。
- Main 与 Chat 的 `ReliableOutboxMessage`。
- 目标解析失败、Hub 推送和可用生产渠道日志。
- 通知相关直接反馈。

最少输出：

- 通知类型、业务键、接收人数和创建时间。
- `DeliveryStatus`、送达时间、重试次数和最终状态。
- Outbox 的任务类型、状态、尝试次数、错误码和处理时间。
- 已读、目标可解析、目标访问和后续任务完成分别统计。

判断边界：

- 通知持久化、Outbox 已处理、实时 Hub 调用和用户实际收到是不同阶段。
- 当前没有通用点击或任务完成事件；不能把已读率作为完整通知价值指标。
- 未配置外部推送渠道时，要明确结果只覆盖站内通知。

### 4.6 Console 管理

读取范围：

- Log `AuditLog_*` 的匿名操作人、路径、方法、状态、耗时、成功和错误摘要。
- Main `ContentReport / UserModerationAction / ShopEntitlementOperation / ReliableOutboxMessage`。
- 管理员直接反馈、截图和复制诊断。

明确禁止导出 `AuditLog.RequestBody / ResponseBody / IpAddress / UserAgent` 和业务正文快照。

最少输出：

- 匿名管理员、任务类型、匿名目标、开始和结束时间。
- 任务内动作数、失败位置、重试或重放、最终业务状态。
- 举报到审核、审核到处置、权益操作到结果等领域关联。
- 无法建立 case 的孤立请求数量和原因。

判断边界：

- 当前 `AuditLog` 没有稳定 task/case ID，按操作者、目标和时间邻近分组只能称为“候选管理任务”。
- HTTP 2xx 不等于治理或权益业务目标完成，必须用领域表最终状态确认。
- 管理员测试、部署后复核和数据清理必须排除。

## 5. 用户反馈归档

每条反馈必须保留以下脱敏字段：

| 字段 | 说明 |
| --- | --- |
| `FeedbackId` | 批次内编号 |
| 来源 | 渠道和受控原文责任人，不写私人联系方式 |
| 原始时间 | 用户首次表达时间，不用转录时间替代 |
| 匿名用户 | 批次内编号；无法确认时标记未知 |
| 影响路径 | 六链路之一和页面路径类别 |
| 用户目标 | 用户当时要完成的任务 |
| 期望 / 实际 | 保留原始语义的脱敏摘要 |
| 复现条件 | 环境、版本、角色、入口和前置状态 |
| 诊断材料 | 日志、TraceId、截图或复制诊断是否存在 |
| 责任模块 | Auth、Gateway、client、API、Message、Chat、Console 等 |
| 严重度 / 可信度 | 按第 6 节口径 |

无法追溯原文、时间、路径或用户目标的内容只记为线索。多人转述不自动变成多个独立信号；同一用户对同一事件的重复表达按一条证据处理。

## 6. 归因、分级与成组规则

### 6.1 严重度

- `P0`：生产整体不可用、数据或权限发生不可接受的破坏。
- `P1`：核心路径对合格用户稳定阻断，且没有可接受恢复方式。
- `P2`：明显影响任务完成或恢复，但存在替代路径或影响范围有限。
- `P3`：体验、表达或效率问题，不阻断任务完成。

观测盲区不自动获得产品严重度。只有证据表明盲区已经造成用户损失时，才按用户影响分级。

### 6.2 可信度

- `A`：生产事实，或带原始时间和明确上下文的直接反馈。
- `B`：来源明确但属于受控验收、设计审阅或无法完成生产关联的记录。
- `C`：环境、身份、时间或因果关系缺失的推断。

### 6.3 成组归因

- 单个已验证 `P0/P1` 直接进入维护线。
- `P2/P3` 按共同成因分组，不按页面数量或代码目录分组。
- 每组记录用户路径、共同成因、责任模块、独立信号数、已知反例和停止线。
- “产品缺陷”“运行故障”“数据不足”“观测盲区”“操作理解偏差”分别归类。
- 已关闭的发布失败不重复进入当前维护批次，除非生产出现新的同源复发证据。

## 7. 功能选题门槛

只有同时满足以下条件，才从 F4-A 进入一个完整功能专题：

1. 两个相互独立的真实使用信号指向同一用户路径和长期结果；单个已验证 `P0/P1` 仍按维护线处理。
2. 信号不是 smoke、测试、设计偏好、一次性运维失败或已经能直接修复的小范围 bug。
3. 能定义数据模型、接口责任、页面入口、权限边界和失败恢复。
4. 能写出不做范围与停止线，不借题扩成通用埋点、运营平台或全站改版。
5. 能用生产结果验证成功、失败和恢复，而不是只验证页面可达或自动化通过。

达到门槛后只选择证据最强的一项，先完成专题设计说明。没有达到门槛时，记录下一批缺少的字段、获取入口、观察周期和判定标准，不用代码量或页面缺口代替用户证据。

## 8. 批次交付与验证

每次采集应交付：

- 一份使用模板生成的脱敏批次记录。
- 六链路分别给出有效证据、无效样本、观测盲区和结论。
- 用户反馈归档、P0 至 P3 分组和可信度说明。
- 功能选题“具备条件 / 不具备条件”的唯一结论。
- 若不具备条件，给出下一次采集入口与判断标准；若具备条件，只新增一个专题设计说明。

文档进入仓库前执行 changed-only repo hygiene 与 `git diff --check`。原始生产材料、查询导出和匿名盐不进入 Git，也不因本说明默认触发服务启动、生产连接或部署操作。
