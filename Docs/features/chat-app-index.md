# 聊天室 App 文档总览

> Radish 聊天室 App 与纯 Web 消息复访入口拆分文档导航
>
> **版本**: v26.7.2
>
> **最后更新**: 2026.07.19

---

## 概述

本组文档聚焦聊天室在 `radish.client` 中作为独立应用的前端架构与交互实现，按“架构/实时/模块/里程碑”拆分，避免单文档持续膨胀。

当前额外提供正式 Web `/messages` 入口，用于普通浏览器登录态聊天工作区。`/messages` 复用现有 `ChatApp`、聊天 API 与 `ChatHub`，承接通知里的 `channelId/messageId` 定位、成员公开主页来源返回、一对一会话生命周期、消息搜索、消息 Reaction 与移动端列表 / 详情切换。F4-C 搜索与 F4-D Reaction 均已完成 A-D 批并关闭；下一顺位进入 F4-E-A 消息置顶的现状审计与专题设计，逐条已读和移动系统通知继续后置。

后台数据模型与 API 细节请优先参考：
- [聊天室系统设计](./chat-system.md)
- [聊天室系统 - 前端架构与组件设计](./chat-frontend.md)
- [正式 Web 一对一私聊与会话管理设计](./chat-direct-conversation-design.md)
- [聊天历史搜索与消息定位设计](./chat-message-search-design.md)
- [聊天消息 Reaction 设计](./chat-message-reaction-design.md)
- [纯 Web 私域复访入口设计说明](/frontend/private-web-revisit)

---

## 文档导航

| 文档 | 作用 | 适用场景 |
|------|------|----------|
| [聊天室 App 架构设计](./chat-app-architecture.md) | 定义应用注册、目录结构、分层边界、状态模型 | 新建 `apps/chat` 或做大规模重构前 |
| [聊天室 App 实时与同步设计](./chat-app-realtime.md) | 定义 ChatHub 连接、事件流、未读同步、异常恢复 | 联调 Hub、排查实时消息问题 |
| [聊天室 App UI 模块设计](./chat-app-ui-modules.md) | 定义三栏布局、核心组件职责、交互状态 | 实现页面与组件、做体验优化 |
| [聊天室 App 实施路线图](./chat-app-roadmap.md) | 定义 P0/P1/P2 交付节奏、验收清单、风险 | 拆解任务、排期与验收 |
| [正式 Web 一对一私聊与会话管理设计](./chat-direct-conversation-design.md) | 定义私聊用户路径、模型、权限、接口、页面与停止线 | 实现一对一私聊及其安全边界 |
| [聊天历史搜索与消息定位设计](./chat-message-search-design.md) | 定义检索文本、ACL、cursor、定位、PC / mobile 页面与停止线 | 实现 F4-C 权威消息搜索 |
| [聊天消息 Reaction 设计](./chat-message-reaction-design.md) | 定义 Chat 专属持久化、`CanReact`、幂等、revision、Hub 与 PC / mobile 停止线 | 实现 F4-D 消息回应 |

---

## 阶段命名约定

本组文档并行使用两套编号，含义不同：

| 编号 | 出现位置 | 含义 |
|------|----------|------|
| Phase 1 / 2 / 3 | `chat-system.md` | 后端系统能力的全量规划（按模块整体交付） |
| P0 / P1 / P2 | `chat-app-roadmap.md` | 前端落地节奏（按阶段能力拆分） |

对应关系：P0 = Phase 1 核心子集；P1 = Phase 1 剩余能力；P2 = Phase 2；Phase 3 暂不在 roadmap 中展开。

---

## 交付边界

- 默认覆盖 Chat App 核心实现；WebOS 窗口应用与正式 Web `/messages` 共用当前 Chat App，不重复实现聊天协议。
- 不覆盖 Console 管理页细节。
- 默认复用现有 `@radish/ui` 与公共基础设施（认证、上传、日志、通知）。
- 不新增重复协议；优先复用 `chat-system.md` 已冻结的数据契约。

---

## 当前状态

- 状态：M12 聊天室 `P1` 核心交互已补齐并完成本轮短回归修复；`2026-03-28` 已补“图片先入草稿、点击发送再统一发出”交互，当前继续按收口观察态维护
- 正式 Web 聊天：`/messages` 已承接登录后频道列表、一对一会话生命周期、消息搜索与定位、消息 Reaction、通知回流和公开个人页返回“聊天”；私聊与 F4-C 已关闭，F4-D-A / B / C 已完成 Reaction 设计、服务端权威契约、Pencil 与正式 Web
- 已完成：
  - 频道列表、历史消息分页、文本发送、撤回、基础未读同步
  - `ChatHub` 事件对齐：`MessageReceived`、`MessageRecalled`、`UserTyping`、`ChannelUnreadChanged`、`ConversationStateChanged`
  - WebOS `Shell` 与正式 Web `/messages` 通过 owner 共享同一 ChatHub 生命周期，避免组件卸载互相停止连接
  - 连接成功/重连后自动加入当前激活频道（降低首次进入漏订阅风险）
  - 重连状态条 + 重连成功后自动补拉最新 50 条消息
  - `@mention`、引用回复、图片消息、按频道草稿恢复、成员面板、成员头像来源
  - 图片上传成功后会先进入当前频道的待发送附件区，不再立即发消息；用户可继续输入文字、移除待发图片，并在点击发送时与文字一起发出，也支持纯图片发送
  - 频道草稿当前包含 `content + replyTarget + pendingImage`；若图片上传完成时用户已经切换频道，待发图片会回写到原频道草稿，避免误发到新频道
  - 乐观发送 + 失败重试（含图片消息复用已上传附件重试）
  - 服务端字符串 ID / 本地临时负数 ID 的双轨约束已对齐，撤回、引用回复和历史分页不再受 Snowflake 精度影响
  - 输入区固定按钮列布局已对齐，避免输入框压住“图片 / 发送”按钮
  - 正式 Web 会话列表按 `互相关注 / 陌生人私信 / 好友群组 / 公共频道` 分区；后端未提供关系 / 群组字段时，只按明确频道类型归入公共频道或陌生人私信
  - 移动端输入占位文案不展示桌面快捷键，桌面端仍保留 `Enter / Shift+Enter / Esc` 操作提示
  - 联调资产已补齐：`Radish.Api.Tests/HttpTest/Radish.Api.Chat.http` 可覆盖 REST 主链路验收
- 当前推进原则：
  - F4-C 搜索继续以 `SearchText`、成员 ACL、跨库一致查询、快照 cursor 和 `GetMessageWindow` 作为已完成维护基线
  - F4-D-D 完成双账号 `zh / en × PC / mobile`、权限、并发、重连、WebOS 与临时数据清理成组验收
  - 文档、代码、联调资产三者口径保持一致，避免把设计项误记为已交付能力
  - 聊天室 `P1` 继续维护图片草稿发送、切频道恢复与失败重试等既有边界；新功能以正式 Web 为主，WebOS 只保持共用组件兼容
