# F4-J-D 内容治理申诉成组验收记录

## 一、结论

- 验收日期：`2026-07-25`
- 验收范围：`F4-J-D`
- 最终结论：通过，F4-J 内容治理申诉与处置纠正专题关闭。
- 正式入口：Gateway 下的用户私域 `/me/appeals` 与 Console `/console/moderation?view=appeals`。
- 数据结论：临时申诉、决定、动作、通知、账号、权限、令牌和来源标记已通过迁移后数据库基线恢复清理；六个本地 SQLite 文件完整性检查均为 `ok`。

本批使用申诉人、举报者、原审核员、申诉审核员、动作执行员和只读治理员，
覆盖 `Post / Comment / PostQuickReply / ChatMessage / Product` 五类目标。
原案件与原动作保持不可变，申诉复核、纠正动作、跨库恢复和对外结果分别留痕。

## 二、运行态覆盖

### 2.1 身份与权限

| 身份 | 已验证边界 |
| --- | --- |
| 申诉人 | 只读取影响本人的决定与申诉；可提交、撤回和回看纠正结果；举报来源、内部备注和审核员身份不暴露 |
| 举报者 | 可提交五类举报并查看本人精简结果，不获得被处置者申诉陈述或复核详情 |
| 原审核员 | 可登记原案件决定，不具备申诉复核或纠正执行权限 |
| 申诉审核员 | 可读取完整申诉、追加证据并登记复核结果，不能执行纠正动作 |
| 动作执行员 | 可执行受批准的纠正范围，不能代替审核员改变复核结果 |
| 只读治理员 | 可读取脱敏申诉队列和公共结论；完整详情、受理、复核和原案件写动作均返回 `403` |

Console mobile 固定为只读复核视图，即使账号具备更高权限也不在移动端提供受理、
复核或纠正按钮。只读治理员在 `390 × 844` 下只能看到脱敏队列、批准范围数量和对外结果说明。

### 2.2 申诉状态与纠正结果

| 目标 | 申诉结果 | 纠正结果 |
| --- | --- | --- |
| `Post` | 全部支持 | 只恢复原治理动作删除的帖子，原动作与恢复动作分别展示 |
| `Comment` | 部分支持 | 恢复评论；原禁言不因部分支持被单独解除，后续封禁仍保持生效 |
| `PostQuickReply` | 维持原决定 | 原限制继续生效，不生成恢复动作 |
| `Product` | 申诉人撤回 | 商品下架与原封禁保持不变 |
| `ChatMessage` | 全部支持 | 恢复消息、搜索投影、Reaction 与 Pin，并清除对应治理来源标记 |

额外构造一条已超过申诉窗口的历史决定，用户页保留决定回看并显示
`Appeal window expired`，不会开放提交入口。同一案件再次使用不同 operation key
提交申诉时，服务端返回 `409 Moderation.AppealAlreadyExists`，一次申诉约束不依赖前端状态。

### 2.3 Chat 跨库失败恢复

Chat 纠正任务在 Main 事务完成后通过 `ContentModerationChatRestore` outbox 跨库执行。
受控 SQLite 写锁下，首次执行失败并保留待重试状态；锁释放后同一任务在第 `2` 次尝试成功，
最终只恢复一次消息、Reaction、Pin 和搜索文本。

运行态暴露并修复两项共同根因：

1. Chat 完成事件复用了纠正启动事件的 operation key，违反申诉事件唯一键，导致成功副作用后无法完成聚合。
2. 失败动作状态被当成终态提前返回，使 outbox 重试无法把既有动作推进到成功。

修正后，动作状态允许从 `Failed` 在同一可靠任务下恢复为 `Succeeded`；
完成事件使用聚合事件序列和动作状态保证幂等，不再与请求 operation key 冲突。

### 2.4 页面矩阵

- Gateway 正式路径覆盖 `zh / en`。
- PC 使用 `1920 × 1080`，mobile 使用 `390 × 844` CSS viewport；两端均保持既有导航与单列响应式边界。
- 覆盖默认与国风主题的申诉代表路径；四主题 token 与布局由前端静态回归继续覆盖。
- 用户端可区分原始 `Restrict / Succeeded` 的“限制已执行”和恢复动作的“已纠正”，不会把原处置误写为申诉纠正。
- Chat 时间线的 `ReliefRequested / ReliefApplied / ReliefNoEffect` 使用正式中英文资源，不显示内部事件标识。
- 通知深链、决定 / 申诉切换、撤回、超期、纠正摘要、Back / Forward 和长文本布局均完成代表复核。
- 当前浏览器工具实际设备像素比固定为 `1`，本批只记录 CSS viewport 证据，不写成 DPR 3 真机结论。

## 三、验收发现与契约修正

### 3.1 SQLite 申诉复核死锁

`StartAppealReview` 与 `CaptureAppealEvidence` 已处于 Repository 的 SQLite 串行写边界，
内部查询事件时再次进入同一非重入 semaphore，真实复核请求因此停滞。

本批将内部事件读取改为只在调用者已经持有的事务和锁内执行核心查询，
不重复获取 Repository 写锁。新增真实 SQLite 测试覆盖复核准备、证据追加与 operation replay。

### 3.2 Chat 纠正重试状态机

Chat 首次失败后，动作记录、outbox 与 Appeal 聚合必须分别保留可恢复状态。
修正后的执行规则为：

1. `Pending` 和 `Failed` 动作均可由同一 outbox 重试。
2. 已成功动作继续幂等返回，不重复修改 Chat 数据。
3. 失败不会结案；跨库成功并完成聚合后才进入 `Resolved`。
4. 重试沿用原来源动作，后续治理来源不会被误清除。

新增 Repository 回归覆盖失败动作恢复、成功状态推进和重复执行无副作用。

### 3.3 用户端动作与事件语义

原页面只按动作状态映射文案，导致原始限制动作和纠正恢复动作都显示“已纠正”。
现按 `ActionType + Status` 显式区分，并补齐三个跨库纠正事件的中英文资源与静态契约测试。

## 四、数据与迁移

- 验收前执行 `20260725_009_content_moderation_appeal` 与
  `20260725_010_chat_moderation_relief`，Main / Chat ledger 均已记账。
- 临时数据创建前保存迁移后的六库基线；服务与浏览器全部停止后逐库恢复该基线。
- 恢复后六库 `PRAGMA integrity_check` 均为 `ok`，临时申诉账号范围精确残留为 `0`。
- `Radish.DbMigrate verify` 确认 Main、Log、Message、Chat 与 OpenIddict 无 pending migration。
- PostgreSQL migration 重入、来源回填、并发和失败恢复不在本轮浏览器矩阵重复运行，继续由 F4-J-B 的 PostgreSQL 定向测试与 migration 门禁承担。

## 五、验证入口

- 后端全量结果：`997` 项通过，`26` 项 PostgreSQL 环境用例按配置跳过。
- 前端结果：client `455` 项、Console `59` 项通过；两端 production build 与四个 workspace type-check 通过。
- `validate:baseline:quick`、文档检查、仓库卫生与迁移严格校验均通过。
- `dotnet test Radish.Api.Tests --no-restore`
- `npm run test --workspace=radish.client`
- `npm run type-check --workspace=radish.client`
- `npm run build --workspace=radish.client`
- `npm run build --workspace=radish.console`
- `dotnet run --no-build --project Radish.DbMigrate/Radish.DbMigrate.csproj -- verify`
- `npm run check:repo-hygiene:changed`
- `git diff --check`

## 六、专题关闭与下一顺位

F4-J 的原案件不可变、一次申诉、独立复核、部分支持、五类来源保护恢复、
用户状态纠正、跨库失败恢复、正式页面和权限边界均已形成长期闭环，专题关闭。

下一顺位进入 `F4-K-A`：交叉复核圈子全局屏蔽、公开聊天和论坛作者回滚等剩余候选，
优先从关系政策、隐私边界、通知抑制、历史内容和 Direct 会话影响建立权威设计，
只选定一个完整专题后再进入服务端实现。
