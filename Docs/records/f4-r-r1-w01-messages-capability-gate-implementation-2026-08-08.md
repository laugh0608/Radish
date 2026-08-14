# F4-R R1-W01 消息工作区能力门禁修复

> 日期：2026-08-08（Asia/Shanghai）
>
> 状态：代码与静态验收完成；readiness 发现的举报 ACL / LongId、发送重试幂等和 History / MessageWindow 错误契约阻断已闭合
>
> 范围：正式 Web `/messages` 与 WebOS 共用 Chat 核心；未修改 `.pen`、撤回权限、路由、数据库 schema 或全局主题 token，未启动服务或执行浏览器 smoke

## 1. 结论

- ChatMessage 举报只允许当前请求租户中对目标频道具有 `CanView` 的用户提交；普通 Admin / System 不因角色穿透 Private / Direct 成员边界。
- 前端举报全链保留 Snowflake LongId 字符串，不再经 JavaScript `number` 丢失精度。
- 失败消息重试复用原 `clientRequestId` 和完整发送指纹；Pending Direct 首条消息即使权威 `VoCanSend=false`，仍可把原键交给服务端完成同键回放，不把模糊结果变成第二条消息。
- History / MessageWindow 对无权、目标不存在或 channel / tenant 绑定错误统一返回稳定 `404 / Chat.ChannelUnavailable / error.chat.channel_unavailable`；有权真实空历史仍返回 `200 + []`。
- 页面只在权威 ChannelUnavailable 合同时清理服务端历史缓存；普通网络错误保留缓存。消息定位从请求开始即隐藏旧窗口、Pin、成员和引用上下文，失败后不回显旧正文。
- 上述修复恢复既有专题已确认的权限、幂等和错误边界，没有新增聊天能力或扩大写权限；`R1-W01` 能力门禁据此解除，但设计顺位仍在 `R1-A01` 之后。

## 2. 举报授权与 LongId

后端新增仅供两个用户提交入口共用的 reporter target resolver：

1. ChatMessage 先按 `messageId + request tenant` 读取，并校验消息、频道和租户绑定；继续兼容既有 `tenant=0` Chat 资源约定。
2. 以 reporter 调用 `IChatChannelAccessService.GetAccessAsync(..., canManageChannel:false)`，只有 `Exists && CanView` 才构建正文快照。
3. 无权、非成员、跨租户或目标失效统一在查重和任何 Report / Case 写入前返回 `Moderation.TargetUnavailable` 404。
4. Accepted、Declined 和带 UserBlock interaction barrier 的合法历史可读参与者仍可举报；授权不误用 `CanSend / CanReact`。
5. Evidence / Appeal 使用的通用目标快照回源保持治理权限边界，没有混入普通 reporter ACL。

前端 `ChatMessageList → ChatApp → ContentReportModal → contentModeration API` 统一传递字符串 ID；共享 Modal 对其他既有 number / string 调用保持兼容。

## 3. 发送重试幂等

- 乐观消息继续保存首次发送生成的 `voClientRequestId`。
- `buildFailedMessageRetryRequest` 只接受 failed 本地消息、临时 ID、原请求键以及完整的 type / content / reply / attachment 指纹；任一关键证据缺失即拒绝重试。
- `handleRetryMessage` 不再生成新键，也不以当前 `VoCanSend` 阻断既有失败项；仍要求消息属于当前可见频道，由服务端先执行同键回放、未命中时再按当前会话状态拒绝。
- 用户显式撤销失败项并重新撰写时，才由正常发送路径生成新键。

回归覆盖普通文字、引用、图片附件和 Pending Direct 首条纯文本。

## 4. History / Window 与前端 fail-closed

- `GetHistoryAsync` 在 `CanView=false` 时抛稳定 404；合法空频道仍返回空数组。
- `GetMessageWindowAsync` 改为非 nullable，失权、anchor 不存在或 message / channel / tenant 错绑均抛同一 404；Controller 删除旧 null→400 分支，并补 OpenAPI 404 声明。
- history、window、前后分页与 has-more 查询使用同一 tenant 绑定口径。
- Client 识别稳定 Code / MessageKey，仅权威不可用错误清理缓存；网络异常仍保留已读内容。
- Store 清理只移除服务端消息，保留用户本地 sending / failed 项；页面不可用态会隐藏这些本地项以及 Pin、成员、typing、旧引用和发送入口，直到权威读取恢复。
- 消息窗口定位在 effect 发请求前的首个渲染即进入加载态，不短暂回显旧窗口；失败进入目标不可用态，不自动跳到最新消息。

## 5. 验证

- 后端定向权限与契约矩阵：`66 passed`。
- 完整 `Radish.Api.Tests --no-restore`：`1193 passed / 39 skipped / 0 failed`；跳过项为需要 PostgreSQL 环境的既有条件用例。
- Client 定向测试：`26 passed`。
- Client 全量测试：`501 passed / 0 failed`。
- `radish.client` type-check、changed-file ESLint 与 production build 通过。
- `check:long-id-safety`、文档检查、changed-file repo hygiene 与 `git diff --check` 通过。

本批没有 schema / migration，也未执行真实运行态或浏览器 smoke；后续视觉专题成组验收时再按 Gateway PC / mobile 规则复核。

## 6. 保留边界与下一步

- 撤回仍缺少服务端 `VoCanRecall` 证据，频道 Moderator / Owner 与当前 System / Admin 权限口径尚未裁决；本批不改变该权限。
- `720px / 680px` 响应式断点、账号切换 reset、搜索焦点、mobile Pin Sheet 和成员紧凑入口继续作为 R1-W01 设计 / 实现输入，不夹带进本次维护修复。
- `ChatApp.tsx` 保持在 `1500` 行硬上限内；后续视觉实现必须先按真实职责拆分，不能继续向主文件堆叠。
- Pencil 可用后仍先完成 `R1-A01 /docs/edit/:id` PC 代表画板；其确认后再进入 Mobile，随后才轮到 `R1-W01`。

关联记录：[R1-W01 设计前代码事实与能力覆盖门禁](/records/f4-r-r1-w01-messages-readiness-audit-2026-08-08)。
