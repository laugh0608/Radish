# P3-12-D62 论坛 / Docs 作者页面族首批实现记录

> 日期：2026-07-05（Asia/Shanghai）
> 范围：`radish.client` Private / Author `/forum/compose`、`/forum/post/:postId?intent=answer|edit|history`、`/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id`
> 设计源：`Docs/frontend/design-sources/private-web-workflows.pen`

## 背景

`P3-12-D62` 已完成 `/workbench`、`/me` 内容历史复访组、资产 / 订单 / 背包、通知 / 消息和圈子 / 宠物页面族首批实现。按当前规划，D62 最后一组继续对齐论坛作者态和 Docs 作者态。

本批通过 Pencil MCP 只读抽查 `P16 - Forum Compose`、`P17 - Forum Edit History`、`P18 - Docs Mine`、`P19 - Docs Compose Edit`、`P20 - Docs Revisions` 和 `P30 - Mobile Author`。设计源强调：

- 论坛作者态覆盖发布、回答、编辑和历史查看，不把动作拆成新的后端能力。
- Docs 作者态应使用文档库、编辑工作台、修订证据和右侧任务上下文，不只堆单列卡片。
- 移动端继续按作者任务流组织，优先展示当前模式、边界和下一步动作。

## 实现范围

### 论坛作者态

- `/forum/compose` 在原发布器基础上补作者发布上下文 rail。
- 展示当前分类预设、发布器就绪状态、作者边界和问答 / 投票 / 抽奖相关公开路线。
- 发布、分类加载、登录回流、`PublishPostModal` 和 `clientSubmissionId` 继续复用既有逻辑。
- 公开帖子详情侧栏补作者模式 rail：
  - 展示当前 `intent` 模式、登录 / 作者身份、帖子类型和分类。
  - `answer`、`edit`、`history` 继续使用现有动作、权限判断和登录回流。
  - 参与侧栏保留轻回应和根评论，回答 / 编辑 / 历史归入作者模式侧栏，避免侧栏职责混杂。

### Docs 作者态

- `/docs/mine` 改为文档列表主列 + 作者任务 rail。
- rail 展示可编辑 / 内置只读 / 已删除计数、当前文档预览、编辑 / 修订 / 公开阅读入口和治理边界。
- `/docs/compose` 与 `/docs/edit/:id` 保留原编辑表单、Markdown 上传和保存逻辑，外层补编辑上下文 rail。
- 编辑 rail 展示新建 / 编辑模式、上级选项数、建议排序、可见性、只读状态、文档来源和版本证据。
- `/docs/revisions/:id` 保留原修订列表和快照预览，外层补版本证据 rail。
- 修订 rail 展示版本数、当前版本、选中版本、选中快照摘要和返回 / 编辑 / 公开阅读动作。

## 保持不变

- 不新增 API、权限键、数据库结构、路由语义或保存 / 提交载荷。
- 不改变论坛发布、回答、编辑、历史查看、分类加载、登录回流或提交幂等契约。
- 不改变 Docs 创建、编辑、上传、修订详情读取、公开阅读回跳或作者权限判断。
- 不把 Docs 发布、撤回、权限治理、回滚、导入导出或文档治理动作搬入作者入口。
- 不启动完整聊天平台、复杂资料 / 安全设置、完整钱包、退款 / 售后、支付口令或资产风控。

## 验证

- `npm run type-check --workspace=radish.client`：通过。
- `npm run build --workspace=radish.client`：通过。

本批尚未执行 Gateway PC / mobile 真实页面 smoke。按协作规则，真实 smoke 需要在执行前由用户在本轮明确确认前后端已启动；当前先完成代码侧与静态验证。

## 后续

- `P3-12-D62 Private / Author` 当前发布前页面族首批实现已覆盖到论坛作者态和 Docs 作者态。
- 下一顺位进入 `P3-12-D63 Console`，继续按 Console 设计源处理后台页面族 UI 差距、现有接口内功能缺口和专题内后置产品 / API 缺口。
