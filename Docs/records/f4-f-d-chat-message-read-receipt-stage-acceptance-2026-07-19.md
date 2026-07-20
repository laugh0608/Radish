# F4-F-D 聊天轻量阅读回执成组验收记录

> 日期：2026-07-19
>
> 结论：F4-F-D 已通过，F4-F 聊天轻量阅读回执专题正式关闭。

## 验收边界

本批在用户当轮授权后启动 API、Auth、Gateway 与正式 Web 前端，使用三个临时普通账号完成 Gateway 正式 `/messages` 与 WebOS `/desktop` 共用 Chat App 的运行态矩阵。验收停留在轻量阅读回执及其不污染边界，没有扩入 Public / Announcement 阅读统计、Presence 平台化、逐消息回执表、Flutter、Tauri 或移动系统通知。

## 运行态矩阵

| 场景 | 结果 |
| --- | --- |
| `zh / en × PC / mobile` | 正式 `/messages` 通过；mobile 使用 `390 × 844` CSS 视口，实际 `devicePixelRatio=1` |
| Public / Announcement | 个人未读游标正常推进，不展示身份、人数、头像或回执入口；公告普通成员不可发消息 |
| 普通 Private | 仅发送者可见已读人数；两名有效读者通过 PC Popover 与 mobile Bottom Sheet 分页入口展示，退出成员立即移除、恢复成员后按当前有效关系重读 |
| Direct 全状态 | Pending / Declined / Blocked / 对端停用均不展示回执；Accepted 只在对端读到的最后一条当前用户消息下展示单一边界 |
| 多标签 / 后台 | 服务端游标保持单调；浏览器自动化无法制造可信的真实后台标签，改用可复现的 `visibilityState=hidden` 事件注入确认隐藏态不推进，恢复可见后才推进 |
| 真实离线 / 重连 | 浏览器网络切为 offline 后游标保持旧值，恢复 online 后 SignalR 与阅读面重新校验并推进 |
| WebOS | Chat 最小化及被 Docs 窗口遮挡时不推进；恢复前台且消息真实进入尾部可见区域后推进 |
| 撤回 / 权限变化 | 已推进游标不因撤回回退；撤回消息不展示回执；Private 成员退出、Announcement 角色降级与 Direct 阻断 / 停用均即时退出对应能力 |
| 键盘 / 无障碍 / 四主题 | Escape 关闭 Popover / Bottom Sheet 并恢复触发按钮焦点；中英文复数正确；`default / guofeng / dark-night / sakura` 均保留可读回执入口 |
| 不污染边界 | 未读清零、搜索定位正常；没有新增阅读通知、Reaction 或置顶记录；静态频道排序不变 |

自动化工具本轮只能设置 CSS 视口，因此没有把 DPR 3 写成已通过。后台标签结论也明确区分了工具限制与确定性页面生命周期注入；真实离线、WebOS 最小化和真实窗口遮挡均已实际执行。

## 验收修复

运行态复核发现：服务端游标与未读已经更新后，频道侧栏的 `voLastMessage` 仍可能停留在旧消息。根因不在阅读回执，而是 `chatStore.addMessage` 只写入 `messageMap`，没有把 Hub 收到或发送成功后的服务端持久消息合并到频道最后消息投影。

本批新增单一投影函数，按频道、消息时间与 LongId 单调合并：

- 同一消息 ID 的服务端更新可刷新投影；
- 更晚消息成为 `voLastMessage`；
- 其他频道、临时失败消息和乱序旧消息不能覆盖当前投影；
- 阅读游标、未读和频道预览继续保持各自权威边界，不通过回执代码互相兜底。

修复后已在真实双账号链路复核：新消息先同步到侧栏预览和消息列表，当前用户未到尾部时保留未读，滚动到实际尾部后服务端游标推进、未读清零，最后消息预览保持不变。

## 验证结果

- Gateway、API、Auth、client 与三个普通账号运行态矩阵通过；验收结束后服务全部停止，`5000 / 5001 / 5100 / 5200 / 3000` 端口释放。
- 验收前六库备份已精确恢复，临时用户、频道、Direct、消息、通知与主题权益残留为 `0`；公开频道 `92900` 的最后消息指针恢复为验收前值。
- 六个 SQLite 文件恢复时哈希逐一匹配备份；`chat.20260719_006_chat_read_receipt` 通过生产 `SchemaMigrationLedger.ApplyPending` 的 `chat` scope 前滚，公开 `Radish.DbMigrate verify` 严格通过，OpenIddict pending 为 `0`。全量 `apply` 会连带执行所有 seed，本轮没有把它作为清理后重复入口；用于调用同一 ledger 的临时测试文件已删除，未进入提交。
- 六库在恢复后和 Chat migration 前滚后两次执行 `integrity_check`，结果全部为 `ok`；回执覆盖索引存在。
- client `449 / 449` 单测、type-check、lint 和 production build 通过；解决方案构建为 `0 warning / 0 error`，后端测试 `938` 通过、`24` 个环境限定 PostgreSQL 用例跳过、`0` 失败。
- `npm run validate:baseline:quick` 通过；仓库卫生与 `git diff --check` 结果见提交前最终检查。

## 清理

- 四个隔离浏览器会话已关闭并删除会话数据；页面会话中的一次性密码、token 和本地 Store 未保留。
- 三个有效测试账号及一个未进入聊天的中断注册账号、临时频道、成员、Direct、消息、通知、主题权益和相关运行数据均通过验收前六库备份恢复移除。
- 恢复前保留了一份短期 post-test 安全快照；数据库完整性、严格 migration 与残留检查全部通过后，与验收前备份和 Playwright 临时产物一并删除。

## 关闭结论与下一顺位

F4-F 的唯一持久游标、原子单调推进、隐私矩阵、发送者受限读取、REST / Hub 权威边界、活跃阅读面、Direct 单一边界、Private 分页详情、多标签 / 离线恢复、正式 Web / WebOS、中英文、PC / mobile、键盘、无障碍、四主题和不污染条件均已满足，专题关闭。

规划没有预先指定 F4-F 之后的具体功能。下一顺位进入 `F4-G-A 功能完成线候选审计与专题裁决`：只读比较 Docs、聊天、通知、治理、宠物和其他既有业务域的真实能力缺口、长期用户价值、数据与权限边界、正式 Web 归属和维护成本，再选择一个边界清楚的完整专题；不先写代码，不解冻移动系统通知、Flutter、Tauri 或主动生产证据采集。
