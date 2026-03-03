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

### 聊天室数据库拆分（Chat 专库）

- **实体路由落地**：`Channel`、`ChannelMessage`、`ChannelMember` 已增加 `[Tenant(configId: "Chat")]`，数据库路由与通知系统 `Message` 模式保持一致。
- **配置完成对齐**：`Radish.Api` 与 `Radish.Auth` 的 `Databases` 新增 `ConnId=Chat`，SQLite 默认文件为 `Radish.Chat.db`，并补齐 PostgreSQL `radish_chat` 示例。
- **迁移与种子对齐**：`DbMigrate` 聊天相关建表与默认频道 seed 已显式切换到 `Chat` 连接执行，避免误落主库。

### DbMigrate 可维护性重构

- **Seed 文件拆分**：将 `InitialDataSeeder.cs` 重构为 `partial` 多文件结构，按身份/论坛/聊天/等级/商城分类拆分，入口仅保留编排逻辑。
- **可读性提升**：单文件长度从千行级降到可维护范围，后续新增 seed 可按领域直接落到对应文件，降低冲突与检索成本。

### 明日待修复清单（夜间问题记录）

- **表情包附件被误判为孤立附件**：`2026-03-03 22:40` 日志出现 `FileCleanup` 将 `Sticker/2026/03/2028451843729784832.jpg` 移入回收站，需排查 Sticker 与 Attachment 的引用关系是否被清理任务正确识别。
- **表情包前端展示异常**：已新增表情包分组并上传表情，但前端表情包页面未显示新分组/新表情；需排查管理端写入链路、查询接口、前端缓存与刷新策略。
- **附件目录策略待确定**：当前回收站路径出现在 `Radish.Api/bin/Debug/net10.0/DataBases/Recycle/orphan/20260303/...`，目录层级过深且不直观；需讨论并确定是否统一落在解决方案根目录 `DataBases/`（含调试便利性与 Docker 部署影响评估）。
