# P3-12-D62 通知 / 消息页面族首批实现记录

> 日期：2026-07-05（Asia/Shanghai）
> 范围：`radish.client` Private / Author `/notifications`、`/messages`
> 设计源：`Docs/frontend/design-sources/private-web-workflows.pen`

## 背景

`P3-12-D62` 已完成 `/workbench`、`/me` 内容历史复访组和资产 / 订单 / 背包页面族首批实现。按当前规划，下一顺位继续 Private / Author 设计源中的通知 / 消息页面族。

本批通过 Pencil MCP 只读抽查 `P12 - Notifications Center`、`P13 - Messages Workspace`、`P26 - Mobile Notifications` 和 `P27 - Mobile Messages`。设计源强调：

- `/notifications` 不是单纯通知列表，而是通知范围、目标分流、未读处理队列和正式 Web 目标跳转。
- `/messages` 不是完整聊天平台重做，而是会话列表、聊天正文、回复输入、订单 / 用户上下文和定位消息恢复。
- 移动端继续按单列任务流组织，优先展示任务摘要、处理队列和当前上下文。

## 实现范围

### `/notifications`

- 在原有通知摘要基础上补通知范围 chips，按全部、评论、订单、文档、系统展示近期通知分布。
- 使用现有 `resolveWebNotificationNavigation` 生成可跳转目标统计，不新增通知协议。
- 增加通知目标 rail：
  - 展示通知 Hub 连接状态。
  - 展示可跳转目标数与需人工判断目标数。
  - 展示未读优先的处理队列，保留真实 `href` 并复用现有目标分流。
  - 展示论坛、订单、文档、消息目标的正式 Web 路由规则。
- 移动端将处理队列放在完整通知中心前，降低小屏回访时的扫描成本。

### `/messages`

- 在原有消息摘要基础上补路由聚焦状态，区分频道锚点与消息锚点。
- 使用现有 `useChatStore` 的频道、未读、@我、当前频道、本地消息和 typing 状态生成上下文 rail。
- 增加消息上下文 rail：
  - 展示当前会话、最后消息摘要、实时连接状态。
  - 展示未读总数、@我频道数、已加载消息数、typing 用户数和路由目标。
  - 展示按 @我、未读和最新消息排序的会话队列，并保留真实 `/messages?channelId=...` 链接。
  - 增加打开通知中心与回到全部频道动作。
- 移动端将上下文 rail 放在完整聊天工作区前，避免直接压缩桌面三栏聊天结构。

## 保持不变

- 不新增 API、权限、数据库结构或后端事件。
- 不修改 `NotificationCenter` 的加载、筛选、已读、删除和分页协议。
- 不修改 `ChatApp` 的频道加载、历史消息、发送、撤回、上传、成员面板和 SignalR 协议。
- 不启动完整通知中心、完整聊天平台、私聊、Reaction、移动系统通知或设备级会话治理。

## 验证

- `npm run type-check --workspace=radish.client`：通过。

本批尚未执行 Gateway PC / mobile 真实页面 smoke。按协作规则，真实 smoke 需要在执行前由用户在本轮明确确认前后端已启动；当前先完成代码侧与静态验证。

## 后续

- 继续 `P3-12-D62 Private / Author` 圈子 / 宠物页面族。
- 随后进入论坛作者态和 Docs 作者态页面族。
- 完整聊天平台能力继续作为后置产品 / API 缺口，不在 D62 当前前端页面对齐批次内硬做。
