# F4-K-D 用户屏蔽成组验收记录

> 日期：2026-07-25（Asia/Shanghai）
>
> 结论：通过，F4-K 用户屏蔽与关系交互隔离专题关闭。

## 验收范围

- 三名普通用户覆盖多方向屏蔽、解除、再次屏蔽、双方关注解除和解除后不恢复关注。
- Direct 覆盖无会话、Pending、Accepted、Declined、Archived、历史消息、Reaction、Pin、阅读回执、举报入口和解除后的原状态恢复。
- 通知覆盖关系型创建抑制、历史列表 / 聚合 / 未读裁剪、实时失效，以及系统、账号安全、资产、治理和申诉例外。
- 正式 Gateway 覆盖 `zh / en`、PC `1920×1080`、mobile `390×844`、多标签、离线恢复、Back / Forward、键盘确认、内建主题和匿名公开主页。
- 数据层覆盖 SQLite / PostgreSQL migration、Repository / Service 并发与幂等、旧 Direct 数据回填、apply 重入、严格 verify 和 Outbox 失败重试。

## 关键结论

1. Main `UserBlock` 是运行时唯一真相源。稳定 operation key 重放返回当前结果，软删除关系可以恢复；两方向有效关系由统一策略合并为对称互动隔离，跨租户和自屏蔽均拒绝。
2. 屏蔽与双方 `UserFollow` 解除处于同一 Main 事务。解除屏蔽后关注保持未建立，Accepted Direct 恢复写入能力，Pending / Declined 不会被自动接受或重开。
3. 屏蔽期间 Follow、Direct 创建 / 发送、Reaction、Pin 和阅读游标推进均返回 `409 UserBlock.InteractionUnavailable` 与 `error.user_block.interaction_unavailable`。Direct 历史、既有 Reaction / Pin、附件和举报入口保留，只隐藏互动能力和阅读回执。
4. 目标用户只得到通用不可互动状态，无法从公开资料或 Direct 文案判断屏蔽方向；匿名用户仍可浏览原本公开的资料与内容，关注和发消息明确进入登录回流。
5. 关系型通知在创建、列表、聚合、未读数和实时层均被抑制；非人际通知分类继续按登记定义送达，不因用户屏蔽被裁剪。
6. 人为锁定 Message SQLite 后，关系失效 Outbox 首次处理回到 `Pending`、`AttemptCount=1` 并记录可诊断错误；解除锁后同一 Outbox 自动成功，未产生平行 Outbox 或重复投影。

## 验收中修复

Direct 取消归档在 SQLite 下曾返回 `500`。根因是 `SetArchivedAsync` 把 `archived` 参数写进同一三元条件表达式，SQLSugar 在 `false` 分支无法翻译谓词。实现已拆为归档与取消归档两个显式更新分支，各自固定 `ArchivedAt` 前置条件，并新增取消归档回归测试。修复后真实 API 返回 `200` 且 `VoIsArchived=false`。

Declined 会话同时呈现只读输入和“接受”动作属于既定契约：发起人不能继续发送，接收人以后仍可主动接受；验收未改写该状态机。

## 数据库与自动化

- 本地 SQLite 已先执行 `main.20260725_011_user_block_authority` 与 `message.20260725_011_user_block_notification_suppression` 的 apply、第二次 apply 和 verify，迁移重入通过。
- 隔离 PostgreSQL 17 中，UserBlock migration / Repository / Policy / Service、通知抑制、可靠任务和 Direct 回归共 `28` 项通过。
- 后端全量：`1022` 项通过，`28` 项环境型用例按默认配置跳过。
- 前端：radish.client `458` 项、`@radish/http` `19` 项、`@radish/ui` `24` 项、radish.console `59` 项通过；相关类型检查、client production build 与 `validate:baseline:quick` 通过。
- Gateway 宿主健康检查通过。移动端截图工具在字体完成后超时，最终以相同 `390×844` 视口的可访问性语义快照核对英文和中文状态；PC 屏蔽确认截图成功，验收不把工具超时写成产品失败。

## 清理与恢复

- 验收服务已停止，隔离 PostgreSQL 容器和浏览器会话已删除。
- 六个 SQLite 文件已恢复到迁移完成后的验收前基线；临时账号、屏蔽、operation、关注、Direct、成员、消息、通知、Outbox、token 和 authorization 精确残留均为 `0`。
- Main、Log、Message、Chat、OpenIddict、Hangfire 六库 `PRAGMA integrity_check` 均为 `ok`。
- 清理后的 `Radish.DbMigrate doctor / verify` 通过，Main / Log / Message / Chat ledger 已应用，OpenIddict pending 为 `0`；验收备份已删除。

## 下一顺位

F4-K 已完成 A-D 批并关闭。下一步进入 `F4-L-A`：只读交叉复核匿名公开聊天、论坛作者版本恢复及当前既有模块的真实缺口，选定一个边界完整、长期有价值的专题并形成权威设计；不预先指定候选胜出，也不并行启动多个专题。
