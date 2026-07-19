# F4-D-D 聊天消息 Reaction 成组验收记录

> 日期：2026-07-19
>
> 结论：F4-D-D 已通过，F4-D 聊天消息 Reaction 专题正式关闭。

## 验收边界

本批在用户明确授权后启动本地 SQLite 后端、Gateway 与正式 Web 前端，执行迁移 apply / verify、三普通账号真实交互、PC / mobile 与 WebOS 复用面成组验收。验收只修复 Reaction 专题内暴露的共同根因，没有扩入论坛 Reaction、消息置顶、逐条已读、Flutter、PWA 或移动系统通知。

## 运行态矩阵

| 场景 | 结果 |
| --- | --- |
| `zh / en × PC / mobile` | Gateway 正式路径通过；PC 使用 `1920 × 1080`，mobile 使用 `390 × 844` CSS 视口 |
| 公开 / 公告 / 普通私有 | 可见成员按权威 `CanReact` 回应；非成员不能借消息 ID 读取或写入私有回应 |
| Direct 状态 | 已接受会话双方可回应；pending / declined / blocked 保留既有历史读取边界，但回应入口只读且服务端拒绝写入 |
| Unicode / sticker | 两类回应均可新增、取消和实时聚合；同一用户同消息最多 10 种，第 11 种被拒绝且权威数据仍为 10 种 |
| 多账号 / 多标签 | 多端实时收到完整 revision 快照，本账号选择态保持正确；相同目标状态不会因重试反转 |
| 离线重连 | 浏览器真实切换 offline 后由另一账号修改回应，恢复 online 后无需手动刷新即追平最新计数 |
| 撤回 | 消息撤回后回应展示同步移除，活跃 Reaction 软删除，不保留可写入口 |
| 长聚合 / WebOS | mobile 长聚合换行、PC 密度及 `/desktop` 共用组件均无阻断 |
| 隔离边界 | Reaction 不改变最后消息、未读、搜索文本、正文或排序，也不生成通知中心事件 |

内置浏览器本轮只能设置 CSS 视口，不能设置设备 DPR。因此 `390 × 844` CSS 布局已实跑，DPR 3 没有伪写成运行态通过；实体手机软键盘也未覆盖。真实离线 / 恢复已通过浏览器网络状态切换实跑，不再只依赖静态契约。

## 验收修复

1. 已应用的历史搜索 migration verifier 原先按完整 `ChannelMessage` 实体查询，会在 Reaction 列尚未迁移时提前选择未来字段并阻断升级。搜索回填与校验现只投影自身依赖字段，并增加“Reaction 列迁移前仍可校验搜索 migration”的防回归测试，旧 migration 不再耦合后续 schema。
2. 英文 Reaction 无障碍标签原先固定使用 `people`，单人计数会读成复数。中英文资源现使用 i18next `_one / _other` 数量键，并用真实资源实例覆盖 `1 / 2` 计数。

## 验证结果

- Chat schema migration 定向测试：8 项通过，4 项 PostgreSQL 环境用例按配置跳过；Reaction migration 的 PostgreSQL、并发和回放边界已在 F4-D-B 隔离环境实跑通过。
- `dotnet build Radish.slnx -c Debug --no-restore`：0 warning / 0 error。
- `radish.client`：437 项测试全部通过，type-check、lint 与 production build 通过；构建仅保留既有 chunk size 提示。
- 本地 SQLite 已完成 `20260719_004_chat_message_reaction` apply / verify；清理后再次执行严格 verify，Main / Log / Message / Chat ledger 均已应用，OpenIddict pending 为 0。
- Gateway、API、Auth、client 与 Console 启动及浏览器矩阵通过；验收结束后进程已全部停止，`5000 / 5100 / 5200 / 3000 / 3100` 端口均已释放。
- Baseline Quick、changed-only 仓库卫生与 `git diff --check` 均已在最终文档差异完成后再次执行并通过。

## 数据清理

- 两个临时普通账号、两个 Direct 会话、临时公告 / 私有频道及其消息、成员、Reaction、operation ledger、Outbox 和通知均按精确 ID 清理。
- 公开测试消息上的临时回应已移除并恢复原 revision；公开频道最后消息指针已恢复到最新保留消息。
- Main / Chat / Message / OpenIddict / Log 的临时对象精确残留均为 0；六个 SQLite 数据库 `integrity_check` 全部为 `ok`。
- 浏览器凭据、验收会话和两份临时数据库安全备份均已清除；原始数据库保持完整。

## 关闭结论与下一顺位

F4-D 的 Chat 专属持久化、目标状态幂等、revision 实时快照、统一 ACL、撤回一致性、正式 Web / WebOS、中英文、PC / mobile、多账号、多标签、真实重连和清理条件均已满足，专题关闭。

按路线图中已后置能力的顺序、社区治理价值和现有 Chat 扩展基础，下一顺位进入 F4-E-A 聊天消息置顶的现状审计与专题设计；逐条已读和移动系统通知继续分别后置，主动生产证据采集保持最终收尾冻结。
