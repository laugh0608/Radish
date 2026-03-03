# 2026-03 第一周 (03-03 至 03-08)

## 2026-03-03 (周二)

### 聊天室 P0 编译收口与回归

- **后端编译错误修复**：修复 `ChatService` 对 `User.AvatarUrl` 的错误访问，在线成员头像字段先返回空值，确保聊天室主链路可编译。
- **后端构建验证**：`Radish.Api` 在 `--no-restore` 条件下完成编译通过，聊天室相关控制器、Hub、服务链路可用。
- **前端连接稳定性修复**：`chatHub` 在“连接成功/重连成功”后自动加入当前激活频道，降低首开或重连后漏订阅导致的实时消息丢失风险。
- **前端验证**：`radish.client` 类型检查通过，聊天室新增代码无 TS 类型回归。

### 聊天室联调资产补齐

- **新增联调脚本**：新增 `Radish.Api.Tests/HttpTest/Radish.Api.Chat.http`，覆盖频道列表、历史分页、发送、撤回、在线成员查询等 REST 主链路。
- **联调口径说明**：明确该脚本聚焦 REST，`MessageReceived/MessageRecalled/ChannelUnreadChanged` 等实时事件需配合 SignalR 客户端联调。

### 聊天室文档对齐更新

- **文档状态更新**：`chat-app-index.md` 从“设计阶段”更新为“P0 主链路已落地”。
- **架构文档对齐**：`chat-app-architecture.md` 同步当前代码目录与 store/action 实现现状，标注 P1 才进行 hooks/components 拆分。
- **实时文档收口**：`chat-app-realtime.md` 同步“连接后自动入组/重连自动回组”现状，并标记历史补拉为 P1 优化项。
- **系统文档补充**：`chat-system.md` 增加“当前实现快照”，注明 `ChannelCategory` 暂未单独落地、在线成员头像来源待补齐。
