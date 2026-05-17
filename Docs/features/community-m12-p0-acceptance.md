# M12-P0 社区主线验收清单

> 面向 **M12-P0「社区主线能力收口」** 的统一验收清单。
>
> **版本**: v26.5.0  
> **最后更新**: 2026.05.10
>
> 关联文档：
> [开发路线图](/development-plan) ·
> [聊天室系统设计](./chat-system.md) ·
> [聊天室 App 文档总览](./chat-app-index.md) ·
> [论坛应用功能说明](./forum-features.md) ·
> [通知系统实现细节](/guide/notification-implementation)

---

## 目标

本清单用于统一判断 M12-P0 四条主链是否达到“**可演示、可联调、可回归**”的收口标准：

- 聊天室
- 社区关系链
- 内容治理闭环
- 分发能力（推荐 / 热门 / 最新）

本轮**不**继续把 P1/P2 规划项纳入通过条件。

---

## 验收分层

### A. 已完成可验收项

#### 1. 聊天室

- 频道列表可获取。
- 历史消息支持分页拉取。
- 文本消息可发送。
- 消息可撤回，并通过实时事件同步。
- 基础未读数可随消息收发与已读操作变化。
- `ChatHub` 可完成连接、回组、输入中、未读变更通知。

**参考资产**：
- `Radish.Api.Tests/HttpTest/Radish.Api.Chat.http`
- `Frontend/radish.client/src/apps/chat/ChatApp.tsx`

#### 2. 社区关系链

- 支持关注 / 取关。
- 支持关注状态查询。
- 支持粉丝列表、关注列表分页查询。
- 支持“关注动态流”独立查询。
- 个人主页关系链页签已区分“关注动态”与“推荐 / 热门 / 最新”分发入口。

**参考资产**：
- `Radish.Api.Tests/HttpTest/Radish.Api.Community.http`
- `Frontend/radish.client/src/apps/profile/components/UserFollowPanel.tsx`

#### 3. 内容治理闭环

- 用户可提交举报。
- 管理端可查询审核队列。
- 管理端可审核举报，并联动禁言 / 封禁。
- 管理端可手动执行治理动作。
- 管理端可查询治理记录。
- 发帖与评论入口已接入发布权限拦截。
- 首版治理后台已集成在 `radish.console` 的 `/moderation` 页面。
- 当前治理对象范围已统一为 `Post / Comment / PostQuickReply / ChatMessage / Product`。
- 审核队列与治理动作日志已支持帖子评论、轻回应与聊天室消息的真实回看 / 回跳定位。
- `Moderation` 页面已支持从举报队列或历史动作一键带入手动禁言 / 封禁 / 解除禁言 / 解除封禁表单，并自动回跳到对应治理日志。
- 举报目标摘要已在创建时固化；审核台并列展示“创建时快照”和“当前状态”，目标失效时保留降级展示。

**参考资产**：
- `Radish.Api.Tests/HttpTest/Radish.Api.Community.http`
- `Radish.Api/Controllers/ContentModerationController.cs`
- `Frontend/radish.console/src/pages/Moderation/ModerationPage.tsx`

#### 4. 分发能力

- `recommend`、`hot`、`newest` 三路流均可访问。
- 热门流具备基础热度权重。
- 推荐流具备关注作者加权与新鲜度衰减。
- 非法 `streamType` 能返回明确错误，不静默降级。

**参考资产**：
- `Radish.Common/OptionTool/FeedDistributionOptions.cs`
- `Radish.Api.Tests/Controllers/UserFollowControllerTest.cs`

---

### B. 仍有缺口但可低风险补齐项

- 聊天室控制器 / 服务最小回归覆盖仍可继续补强。
- 内容治理控制器对管理端异常映射仍可继续补少量回归用例。
- `.http` 联调脚本可继续补绝对顺序说明、账号角色说明、预期响应样例。
- 文档可继续补“已验收证据链接”与执行人记录。

---

### C. 留待后续阶段

#### 聊天室后续阶段

- `Reaction`
- 私聊（私信）
- 消息搜索
- 语音消息
- 频道权限细分

#### 治理增强

- 审核台筛选增强与批量操作效率优化
- 敏感词
- 自动治理策略
- 批量治理能力

#### 分发增强

- 更复杂召回 / 排序策略
- 推荐解释能力
- 指标观测与压测基线

---

## 推荐验收顺序

### 1. 聊天室

1. 获取频道列表
2. 获取频道历史消息
3. 发送文本消息
4. 撤回消息
5. 联动 SignalR 客户端验证 `MessageReceived` / `MessageRecalled` / `ChannelUnreadChanged`

### 2. 关系链

1. 关注用户
2. 查询关注状态
3. 查询我的粉丝 / 我的关注
4. 查询 `GetMyFollowingFeed`
5. 查询 `GetMyDistributionFeed` 的 `recommend` / `hot` / `newest`
6. 验证非法 `streamType` 返回 `400`
7. 取消关注用户

### 3. 内容治理

1. 提交举报
2. 查询我的治理状态 / 发布权限
3. 管理端查询审核队列
4. 管理端审核举报并联动禁言 / 封禁
5. 管理端查询治理动作记录
6. 验证评论 / 轻回应 / 聊天消息目标可从审核队列或治理日志真实回看
7. 验证目标被删除、撤回或下线后，审核台仍保留创建时快照且当前状态显示为失效降级
8. 使用受限账号验证发帖 / 评论被拦截

---

## 通过标准

满足以下条件即可判定 M12-P0 社区主线进入“已收口，可转验收执行”状态：

- 四条主链均存在清晰主流程入口。
- 文档、代码、联调资产口径一致。
- 至少存在最小回归资产，能覆盖关键分支而非只靠人工记忆。
- 未把 P1/P2 规划项误判为本轮阻塞项。

---

## 本轮结论（2026-05-10）

- **结论**：M12-P0 四条主链的最小收口目标已完成，内容治理口径需以当前五类对象、真实回看与快照固化能力为准。
- **建议**：下一步从“继续补 scope”切换为“按清单执行验收 + 补最小回归 + 优化审核台操作效率”。
