# F4-I-D 内容治理案件成组验收记录

## 一、结论

- 验收日期：`2026-07-25`
- 验收范围：`F4-I-D`
- 最终结论：通过，F4-I 内容治理案件、证据与动作一致性专题关闭。
- 正式入口：Gateway 下的 Console `/console/moderation` 与用户私域 `/me/reports`。
- 数据结论：临时案件、举报、动作、通知、账号、授权、作业和审计数据已清理；六个本地 SQLite 文件完整性检查均为 `ok`。

本批使用举报者、被举报者、只读治理员、审核员和动作执行员，覆盖
`Post / Comment / PostQuickReply / ChatMessage / Product` 五类目标。案件聚合、追加式证据、
决定与动作分离、用户治理唯一当前状态、跨 Chat 库动作失败恢复和举报者精简结果均按权威设计工作。

## 二、运行态覆盖

### 2.1 身份与权限

| 身份 | 已验证边界 |
| --- | --- |
| 举报者 | 可提交举报、查看本人举报结果；同目标重复提交被拒绝，第二举报者聚合到同一案件 |
| 被举报者 | 不具备 Console 治理读取、证据、审核或动作权限 |
| 只读治理员 | 可读取队列与案件详情；证据、决定和动作写入均被权限边界阻止 |
| 审核员 | 可采集证据和登记决定；不能执行用户治理动作 |
| 动作执行员 | 可执行禁言、封禁、解除和失败动作重试；不能代替审核员登记决定 |

跨租户目标和案件统一按不可见处理，不向调用方泄露目标、案件或权限细节。

### 2.2 目标、并发与恢复

- `Post`：目标在证据采集后被编辑时返回版本冲突；重新采集证据后可完成限制处置。
- `Comment`：目标删除后证据状态为不可用，案件仍保留举报时快照与事件留痕。
- `PostQuickReply`：目标删除后可按证据不足结案，不把不可用目标误判为动作成功。
- `Product`：两个审核请求并发提交时只有一个成功，另一请求返回案件版本冲突；成功业务键重放保持同一结果。
- `ChatMessage`：正常撤回通过 Main outbox 跨库执行；受控 SQLite 写锁下，同一任务先进入
  `ActionFailed`，锁释放后由同一 outbox 重试并进入 `ActionSucceeded`，案件随后变为已解决。
- 用户治理状态覆盖临时禁言、自然到期、再次禁言、封禁、解除封禁和解除禁言；内容发布权限随权威状态即时变化。

决定与公开结果码使用严格对应关系：

| 决定 | 公开结果码 |
| --- | --- |
| `NoViolation` | `NoViolation` |
| `Violation` | `MeasuresTaken` |
| `InsufficientEvidence` | `InsufficientEvidence` |

不匹配的决定与公开结果码返回 `Moderation.ValidationFailed`，不会修改案件或创建 outbox。

### 2.3 页面矩阵

- Gateway 正式路径覆盖 `zh / en`。
- PC 使用 `1920 × 1080`，mobile 使用 `390 × 844` CSS viewport；两种视图均无横向溢出。
- 覆盖默认、国风、暗夜和樱花四套主题代表状态。
- `/me/reports` 可看到最终案件结果；目标失效时保留提交摘要。
- Console 可回看 `ActionFailed → ActionSucceeded` 时间线，筛选器具备可访问名称。
- Back / Forward、长文本、键盘路径和移动单列治理流程均完成复核。
- 当前浏览器工具实际设备像素比固定为 `1`，因此本批结论只覆盖 CSS viewport，不将其写成 DPR 3 设备证据。

## 三、验收发现与契约修正

### 3.1 Repository 联表表达式与 SQL 投影边界

真实 SQLite 队列查询暴露出 `BaseRepository.QueryMuch` 的租户过滤表达式仍绑定原始实体参数，
而联表查询使用新的别名参数；部分 `select / where` 表达式也存在同类参数来源不一致。

本批在 Repository 边界统一重绑定联表表达式参数，并增加真实 SQLite 多租户联表测试。
案件导航查询不再把 `BuildTextSnapshot` 等自定义方法放进 SQL 投影，而是先读取原始字段，
再在内存中执行确定性文本规范化。

### 3.2 决定与公开结果码契约

服务端此前允许部分语义不一致的决定与公开结果码组合。现已改为逐项精确匹配，
并增加参数化测试和真实 HTTP 无副作用验证，防止 Console 或未来消费者写入互相矛盾的案件结论。

### 3.3 Outbox 异常传播与动作失败归属

受控 Chat SQLite 写锁复核发现，非泛型 `Task` 经 `ServiceAop` 执行时会记录异常但不再抛出，
导致跨库动作已经写入 `ActionFailed`，外层 outbox 却被错误标记为成功。

修正后：

1. `ServiceAop` 记录异常后继续抛出，保持异步方法原始失败语义。
2. `ReliableTaskProcessor` 只负责执行动作和成功完成，不在内部吞掉异常。
3. `ReliableOutboxJobs` 作为任务生命周期边界，同时标记 outbox 失败并追加案件 `ActionFailed`。
4. Hangfire 重试继续使用同一 outbox；成功后追加 `ActionSucceeded`，不会重复决定或重复举报结果通知。

该行为由 AOP、processor、outbox job、案件仓储和真实锁冲突矩阵共同覆盖。

### 3.4 Console 筛选器无障碍名称

状态、目标类型和关键词筛选器已增加明确的可访问名称，并由静态契约测试固定。

## 四、自动验证

- 后端全量：`986` 通过，`26` 个 PostgreSQL 环境用例按配置跳过，共 `1012`。
- Console：`59` 项测试通过。
- 定向契约：
  - 内容治理契约 `19 / 19`；
  - AOP / outbox `9 / 9`；
  - `QueryMuch` 真实 SQLite `3 / 3`。
- 解决方案构建：`0 warning / 0 error`。
- `npm run validate:ci`：通过。
- `npm run check:host-runtime`：服务运行期间通过。
- `Radish.DbMigrate doctor / verify`：通过，`main.20260721_008_content_moderation_case` 已应用，
  OpenIddict pending migration 为 `0`。

本批没有另行启动 PostgreSQL 运行态验收。SQLite / PostgreSQL migration、仓储并发和历史映射的
环境用例沿用 F4-I-B 已完成的专题证据；本批新增代码由全量测试和真实 SQLite 运行态覆盖。

## 五、数据清理

- Main 库按专用账号、目标和案件范围删除举报、证据、事件、用户治理状态、动作、通知 outbox、
  商品主题权益、注册余额、角色权限和临时业务内容。
- Chat、Message、OpenIddict、Hangfire 和 Log 恢复到验收前备份。
- F4-I-D 专用账号、目标、案件、动作和明确授权主体残留均为 `0`。
- 既有非临时案件和正式 schema ledger 保留。
- `Radish.db / Radish.Chat.db / Radish.Message.db / Radish.OpenIddict.db /
  Radish.Hangfire.db / Radish.Log.db` 的 `PRAGMA integrity_check` 均为 `ok`。

## 六、专题关闭

F4-I 完成标准已经满足：

- 同目标开放案件唯一；
- 举报、证据、决定、动作、用户当前状态和事件各自拥有唯一职责；
- 五类目标均有明确处置或不可用结果；
- 角色、租户和私聊边界不泄露；
- Console 与本人举报页在双语、PC / mobile、冲突、失效和失败恢复下可用；
- 定向测试、全量回归、运行态矩阵、数据清理和数据库自检全部完成。

新的举报类型、申诉 / 工单、机器审核或关系隐私政策不继续扩入 F4-I，需要重新完成候选复核和权威设计。
