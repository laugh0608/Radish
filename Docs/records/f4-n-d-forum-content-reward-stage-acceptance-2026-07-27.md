# F4-N-D 论坛内容赞赏成组验收记录

> 日期：2026-07-27（Asia/Shanghai）
>
> 结论：通过；F4-N A-D 批形成完整闭环，专题关闭。

## 验收范围

本批通过 Gateway 对 `Post / Comment` 内容赞赏执行代表性 PC / mobile 运行态验收，并结合服务端、HTTP 契约和前端自动化回归覆盖完整资格、幂等与失败恢复矩阵。

运行态重点覆盖：

- 匿名点击赞赏后分别以发送者和接收者身份登录，精确回到原帖子或评论定位；
- 帖子与评论的确认、预设理由、成功、已赞赏、自赞赏失败和公开记录；
- 发送者 `-1`、接收者 `+1`、Main 总量守恒及 Log 双分录一致；
- `ContentRewardAuditProjectionRequested` 与 `NotificationRequested` 两类 Outbox 成功消费；
- 接收者收到 `ContentRewardReceived / Reaction` 通知，并可返回 canonical 帖子目标；
- `1920 × 1080` PC 与 `390 × 844` mobile CSS 视口无水平溢出；
- `zh-CN / en-US`、`default / guofeng` 代表运行态；四主题及其余状态由静态契约回归覆盖。

双向屏蔽、账号不可用、治理目标、余额不足、重复请求、同键异参、并发唯一、Outbox 重投与通知抑制继续由专属服务端测试覆盖，未为了运行态造数重复改写这些权威状态。

## 验收中修正

### 登录回流意图

公开详情原有 `authReturnPath` 只允许既有意图，匿名赞赏登录后会退回 `/discover`。本批把 `reward` 纳入公开论坛返回意图，并同时支持帖子和 `commentId` 定位；回流地址仍经过既有受控路径校验，不开放任意外部返回地址。

### Reliable Outbox JSON 契约

`ContentRewardRepository` 使用 Web 默认 JSON 命名写入 camelCase payload，而 `ReliableTaskProcessor` 曾使用大小写敏感的默认反序列化，导致审计任务上下文为空并进入 DeadLetter、通知任务触发空引用。

本批把可靠任务反序列化统一到 `JsonSerializerDefaults.Web`，并为缺失通知负载返回稳定永久失败；新增 camelCase 回归用例后，审计投影和通知任务均在新事务中一次成功。修正针对生产者与消费者契约根因，没有增加忽略错误的 fallback。

## 验证结果

- `dotnet test Radish.Api.Tests --no-restore`：`1071` 通过，`32` 个需要外部环境的条件用例跳过，`0` 失败。
- Reliable Task 内容赞赏 / 通知定向回归：`7` 通过。
- `radish.client`：`470` 项测试通过，type-check、lint 和 production build 通过。
- `@radish/http`：内容赞赏契约测试、type-check 与 lint 通过。
- `npm run check:host-runtime`：Gateway、API、Auth 均返回 `200`。
- `npm run check:repo-hygiene:changed` 与 `git diff --check` 通过。
- Main `20260727_014_content_reward`、Log `20260727_014_content_reward_audit_projection` 已应用；六库严格 migration verify 通过。

当前机器未配置 PostgreSQL 集成测试环境，因此后端全量中的相关条件用例保持显式跳过；本批没有把 SQLite 运行态结果表述为 PostgreSQL 实跑结果。浏览器工具只能固定 CSS viewport，无法独立设置设备 DPR；mobile 结论限定为 `390 × 844` CSS 视口。

## 清理与关闭

验收结束后已恢复默认关闭的 `Forum.ContentReward.Enabled`，退出测试身份、重置浏览器视口并关闭浏览器会话，停止 Gateway、API、Auth、Frontend 与 Console。

六个 SQLite 数据库已从迁移完成后的验收前基线精确恢复并复核哈希：

- `ContentReward`、内容赞赏 `CoinTransaction` 和相关 Outbox：`0`；
- Log 内容赞赏余额投影：`0`；
- 内容赞赏通知和收件箱分组：`0`；
- Admin / TestUser 余额均恢复为 `51`；
- 严格 migration verify 通过。

F4-N 至此关闭。下一步回到 F4 功能完成线，先做下一专题的只读候选审计与长期边界设计；在候选正式裁决前不直接进入代码。
