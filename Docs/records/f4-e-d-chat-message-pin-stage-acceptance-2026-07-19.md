# F4-E-D 聊天消息置顶成组验收记录

> 日期：2026-07-19
>
> 结论：F4-E-D 已通过，F4-E 聊天消息置顶专题正式关闭。

## 验收边界

本批在用户明确授权后，对本地 SQLite 执行 `chat.20260719_005_chat_message_pin` migration apply / verify，启动 API、Auth、Gateway 与正式 Web 前端，并使用三个临时普通账号和种子管理员完成真实 Gateway 成组验收。验收严格停留在消息置顶专题，没有扩入个人收藏、Forum 置顶、逐条已读、Flutter、PWA 或移动系统通知。

## 运行态矩阵

| 场景 | 结果 |
| --- | --- |
| `zh / en × PC / mobile` | Gateway 正式 `/messages` 通过；PC 使用 `1920 × 1080`，mobile 使用 `390 × 844` CSS 视口 |
| Public | System / Admin 可管理，三个普通账号只读；完整列表、定位和实时快照通过 |
| Announcement | Moderator 可发消息并管理置顶，普通成员和非成员只读；撤回后置顶实时移除 |
| 普通 Private | Owner 可管理，普通成员只读，非成员不可见；撤回与成员权限边界通过 |
| Direct 状态 | accepted 双方可管理；pending / declined / blocked 不提供管理动作，服务端在 blocked 状态稳定返回 `error.chat.pin_not_allowed` |
| 并发目标状态 | 两个相同取消请求并发返回一次 `changed=true`、一次 `changed=false`，revision 只增长一次 |
| 20 条上限 | 20 条全部可见且按最近置顶优先；第 21 条返回 `error.chat.pin_limit_exceeded`，权威快照仍保持 20 条 |
| 多标签 / 离线重连 | 同账号两个标签同时接收完整 revision 快照；真实 offline 期间错过更新，恢复 online 后无需刷新即由 HTTP 追平 |
| 撤回 / 失权 | 公告、私有和 Direct 置顶目标撤回后自动移除；Direct 阻断后历史置顶可读但管理动作与写权限即时退出 |
| mobile / 键盘 | 英文单数 `1 pinned message` 正确；完整列表以 Bottom Sheet 贴底展示，Escape 关闭并把焦点归还计数按钮 |
| WebOS | `/desktop` 双击“聊天室”后复用同一 Chat App，置顶条、列表入口和管理能力正常 |
| 不污染边界 | 置顶 mutation 不生成通知类型，不改正文、`SearchText`、未读、最后消息或频道排序；公开频道最后消息指针保持原值 |

内置浏览器本轮只能设置 CSS 视口，`window.devicePixelRatio` 为 `1`，因此 `390 × 844` 布局已实跑，DPR 3 没有伪写成运行态通过。实体手机软键盘也不在本专题范围内。真实离线 / 恢复已通过浏览器网络状态切换实跑。

## 验收修复

本批没有发现需要修改代码的共同根因。运行态行为与 F4-E-A / B / C 已固定的独立状态、ACL、目标状态、revision、撤回一致性和正式 Web / WebOS 契约一致。

## 验证结果

- 本地 SQLite 已完成 `chat.20260719_005_chat_message_pin` apply；清理后严格 `Radish.DbMigrate verify` 通过，Main / Log / Message / Chat ledger 均已应用，OpenIddict pending 为 `0`。
- 六个 SQLite 数据库 `integrity_check` 全部为 `ok`。
- Gateway、API、Auth、client 启动与浏览器矩阵通过；验收结束后四个进程均已停止，`5000 / 5001 / 5100 / 5200 / 3000` 端口全部释放。
- `dotnet build Radish.slnx -c Debug --no-restore` 通过；`dotnet test Radish.Api.Tests --no-restore` 为 `922` 通过、`22` 个环境限定集成用例跳过、`0` 失败。
- client `442 / 442` 单测、type-check、lint 和 production build 通过；`npm run validate:baseline:quick` 通过。
- `npm run check:repo-hygiene:changed` 已检查本批变更文本且通过；`git diff --check` 通过。

## 数据清理

- 三个临时普通账号、两个 Direct 会话、临时公告 / 私有频道及其成员、消息、置顶、可靠 Outbox 和私信请求通知均按精确 ID 清理。
- 公开测试消息的临时置顶已移除，公开频道 `PinRevision`、最后消息指针和业务数据恢复到验收前状态。
- Main / Chat / Message / OpenIddict / Log 的临时对象精确残留均为 `0`；六库完整性正常。
- 四个浏览器会话的本地凭据已清空并关闭；临时安全备份已删除，不保留账号密码、token 或数据库副本。

## 关闭结论与下一顺位

F4-E 的 Chat 专属持久化、目标状态幂等、20 条容量、revision 实时快照、统一 ACL、撤回一致性、不污染边界、正式 Web / WebOS、中英文、PC / mobile、多账号、多标签、真实重连和清理条件均已满足，专题关闭。

按 Chat P2 已后置能力的既定顺序，下一顺位进入 `F4-F-A 聊天轻量阅读回执：现状审计与专题设计`。先核实现有 `ChannelMember.LastReadMessageId`、在线成员、未读游标、Hub 和历史草案，再固定隐私、频道类型、性能、页面与停止线；移动系统通知继续后置，主动生产证据采集保持最终收尾冻结。
