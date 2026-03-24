# M12-P0 社区主线验收清单

> 面向 **M12-P0「社区主线能力收口」** 的统一验收清单。
>
> **版本**: v26.3.0  
> **最后更新**: 2026.03.07
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
- 当前首版治理对象范围为 `Post / Comment`，商品与聊天室消息不作为本轮通过前提。

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

#### 聊天室 P1

- `@mention`
- 图片上传 UI
- 引用回复
- 成员头像来源
- 断线后历史补拉

#### 治理增强

- 商品 / 聊天室消息举报纳管
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
6. 使用受限账号验证发帖 / 评论被拦截

---

## 通过标准

满足以下条件即可判定 M12-P0 社区主线进入“已收口，可转验收执行”状态：

- 四条主链均存在清晰主流程入口。
- 文档、代码、联调资产口径一致。
- 至少存在最小回归资产，能覆盖关键分支而非只靠人工记忆。
- 未把 P1/P2 规划项误判为本轮阻塞项。

---

## 本轮结论（2026-03-07）

- **结论**：M12-P0 四条主链已具备进入验收执行阶段的基础。
- **建议**：下一步从“继续补功能”切换为“按清单执行验收 + 补最小回归 + 记录结果”。
