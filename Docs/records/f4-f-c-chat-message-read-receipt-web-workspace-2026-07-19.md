# F4-F-C 聊天轻量阅读回执正式 Web 与 WebOS 共用页面完成记录

> 日期：2026-07-19
>
> 结论：F4-F-A / B / C 已完成，下一顺位进入 F4-F-D 定向回归与成组验收。

## 本批边界

本批在 [F4-F 权威专题设计](/features/chat-message-read-receipt-design) 已确认的隐私、游标和 REST / Hub 边界内，完成 Pencil PC / mobile、正式 `/messages` 与 WebOS 共用阅读面、发送者可见回执和旧 Hub 写入口退役。没有新增逐消息回执表、公告分析、Presence 平台、通知事件、Flutter 或 Tauri 能力。

本批没有启动 Radish 服务，也没有执行 Gateway、浏览器或 WebOS smoke；运行态矩阵按停止线留给获得当轮启动授权后的 F4-F-D。

## 完成内容

### Pencil 与共用页面

- `private-web-workflows.pen` 新增 `P13G - Read Receipts / Desktop`：普通 Private 自己消息显示已读人数按钮，PC 读者 Popover 展示当前有效成员、关闭和焦点恢复边界，同时固定 Public / Announcement 不展示回执。
- 新增 `P27G - Read Receipts / Mobile`：普通 Private 使用 Bottom Sheet 展示每页最多 50 名当前有效读者，并明确 Direct 只在最后一条已读到的自己消息下显示单一 `已读 / Read` 边界。
- 两个画板均复用既有消息工作区母版与色彩 / 字体体系，`snapshot_layout` 深度检查均无裁剪、重叠或布局问题。
- 正式 `/messages` 与 WebOS 继续复用同一个 `ChatApp`；仅由显式 `readSurfaceMode` 区分普通页面与必须存在前台窗口的 WebOS 阅读面，不复制业务页面和 Store。

### 活跃阅读面与精确游标

- 新增共用活跃阅读面 Hook：同时检查 `document.visibilityState`、页面焦点、会话尾部、历史 / 定位加载状态和实际消息视口；WebOS 额外要求 Chat 窗口已被窗口 Store 跟踪、未最小化且具有最高 `zIndex`。
- 只从真实 DOM 可见行中选择最高持久消息 ID；临时负数消息、单纯历史加载成功、搜索定位、仍有更新页和后台页面均不能推进游标。
- 使用 REST `ChannelReadState/Advance` 提交精确 `readThroughMessageId`，服务端响应直接校正当前频道未读与 mention；不在前端自行清零。
- 同频道待提交目标按 LongId 字符串单调合并。网络与 5xx 只保留当前会话内存目标，在后续焦点、可见性、滚动、重连或消息变化时重试；4xx 视为权威拒绝并丢弃，不建立本地持久队列。
- 账号切换、登出或页面卸载会废弃旧账号待提交目标和回包；旧请求不能覆盖新账号 Store，也不会与新账号同频道请求并发回退。

### 回执摘要、实时失效与隐私表现

- `chatStore` 增加按频道的 HTTP 权威摘要与失效世代；Hub `ReadReceiptsChanged` 和 `ConversationStateChanged` 只增加失效提示，不携带或累计个人、头像、人数和游标。
- 当前消息窗口只批量查询最近最多 20 条自己发送、已持久化、发送成功且未撤回的消息；连续失效提示去抖，重连和消息集合变化重读 HTTP 权威结果。
- Direct 只显示当前加载范围内对端已读到的最后一条自己消息边界；普通 Private 仅在 `readCount > 0` 时显示人数按钮，Public / Announcement 和受抑制 Direct 状态不渲染入口或占位。
- 普通 Private PC 使用紧凑 Popover，mobile 使用共享 Bottom Sheet；读者按 50 人 keyset 继续加载、按 ID 去重，头像仅作辅助并同时显示姓名，点击可进入公开个人页。
- 撤回会同步移除当前摘要项；频道、消息、账号或权限变化会关闭详情并废弃旧分页请求，不保留跨账号个人数据。

### 中英文、键盘与无障碍

- 新增中英文人数复数、Direct 边界、加载 / 错误 / 空态 / 继续加载和稳定服务端错误词元；用户姓名与账号原文不翻译。
- 摘要使用真实按钮，PC Popover 支持 `Enter / Space`、`Escape`、Tab 焦点循环与关闭后焦点恢复。
- 从根因补齐共享 `BottomSheet` 的 `role=dialog`、`aria-modal`、标题关联、初始焦点、Tab 约束、Escape 和焦点恢复；所有宿主继续提供关闭文案，不在共享 UI 写死语言。
- 新样式只使用现有语义主题 token，适配 default / guofeng / sakura / dark，不新增页面级硬编码主题体系。

### 旧入口退役与不污染边界

- 删除前端 `chatHub.markChannelAsRead`、服务端 `ChatHub.MarkChannelAsRead`、`IChatService.MarkChannelAsReadAsync` 及其实现；个人已读写入只保留 REST 权威边界。
- 未改变未读排除自己消息 / 撤回消息、通知中心、`LastMessageId / LastMessageTime`、搜索、Reaction、置顶 revision、频道排序或 Presence 在线成员面板。

## 验证结果

- Pencil：`P13G / P27G` 均通过 `snapshot_layout(..., problemsOnly=true)`，结果为 `No layout problems`，并完成截图复核。
- Client：type-check、lint、production build 与 `447` 项测试通过；新增用例覆盖活跃阅读面组合、最近 20 条摘要目标、Direct 边界、可见 LongId 单调选择、REST / Hub / Store / PC-mobile / 账号隔离静态契约。
- `@radish/ui`：type-check、lint 与 `24` 项测试通过；Bottom Sheet 模态语义和焦点边界已纳入契约测试。
- 后端全量：`938` 项通过，`24` 项 PostgreSQL 环境用例按配置跳过；解决方案 Debug 构建 `0 warning / 0 error`。
- Baseline Quick、文档卫生、changed-file 仓库卫生、`git diff --check` 与 staged diff 检查通过。
- 未启动 Radish 服务，未执行 Gateway、PC / mobile 浏览器或 WebOS smoke。

## 下一顺位

进入 F4-F-D：取得当轮启动授权后，以三个普通账号覆盖 Public、Announcement、普通 Private、Direct Pending / Accepted / Declined / Blocked / 对端不可用，以及 `zh / en × PC / mobile × 正式 Web / WebOS`；追加多标签乱序、后台标签、WebOS 遮挡 / 最小化、真实断线重连、撤回、成员加入 / 退出、失权、四主题和不污染矩阵。共同根因成组修复并补自动化，最后清理临时账号、频道、会话、消息、通知、凭据与备份，执行六库完整性和严格 migration verify。
