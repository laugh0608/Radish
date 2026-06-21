# P3-12-B2 完整个人中心 Web 化方案

> 日期：2026-06-21（Asia/Shanghai）
>
> 状态：方案与边界梳理完成，准备进入首批代码实现
>
> 结论：`P3-12-B2` 回到 WebOS 到正式 Web 的功能迁移主线，优先补齐 `/me` 下的我的内容、完整浏览历史、附件管理和经验详情；关注关系以既有 `/circle` 为权威入口，`/me` 只提供个人中心内的清晰联动，不重复实现关系链页面。

## 本轮核对

Git 状态：

- `git status --short --branch`：`dev...origin/dev [ahead 18]`，工作区无未提交改动。
- `git log -1 --oneline`：`c0b2560f fix(client): 收口公开账号动作私域口径`。
- `git rev-list --count master..dev`：`62`。

已读范围：

- `Docs/planning/current.md`
- `Docs/planning/p3-12-web-completion-webos-retirement.md`
- `Docs/records/p3-12-a-webos-web-function-asset-inventory-2026-06-21.md`
- `Docs/frontend/private-web-revisit.md`
- `Frontend/radish.client/src/me/*`
- `Frontend/radish.client/src/apps/profile/*`
- `Frontend/radish.client/src/apps/profile/components/*`
- `Frontend/radish.client/src/apps/experience-detail/*`
- `Frontend/radish.client/src/api/user.ts`
- `Frontend/radish.client/src/api/userFollow.ts`
- `Frontend/radish.client/src/api/experience.ts`
- `Frontend/radish.client/src/api/attachment.ts`
- `Frontend/radish.client/src/bootstrap/entryRoute.ts`
- `Frontend/radish.client/src/services/authReturnPath.ts`

未执行：

- 未启动 API / Auth / Gateway / Vite。
- 未做 PC / mobile 真实页面复核。
- 未进入 UI 设计或视觉重塑；本批是功能迁移边界和路由方案，不需要 Pencil。

## 现状判断

### `/me`

- `meRouteState` 当前只识别 `/me`、`/me/assets`、`/me/assets/transactions`。
- `/me` 是登录态状态看板，聚合公开资料、成长摘要、资产摘要、最近访问和宠物摘要。
- `MeAssetsPage` 已承接 B1 的资产入口和完整资产流水。
- `/me` 最近浏览只取 5 条，更多浏览历史仍在 WebOS `ProfileApp` 的 `browse-history` tab。

### WebOS `ProfileApp`

- `ProfileApp` 覆盖我的帖子、评论、轻回应、浏览历史、附件、关注面板、用户资料卡和外部用户关注动作。
- 顶层强依赖 `useCurrentWindow()`、`useWindowStore().openApp()`、窗口参数和 WebOS 内存导航，不能整页搬进正式 Web。
- 列表组件具备一定复用价值：
  - `UserPostList`：用户帖子列表，当前直接 `fetch /api/v1/Post/GetUserPosts`。
  - `UserCommentList`：用户评论列表，当前直接 `fetch /api/v1/Comment/GetUserComments`。
  - `UserQuickReplyList`：我的轻回应列表，已通过 `getMyQuickReplies`。
  - `UserBrowseHistoryList`：我的浏览历史列表，已通过 `getMyBrowseHistory`，但点击目标仍交给 WebOS workspace navigation。
  - `UserAttachmentList`：我的附件列表，当前直接 `fetch /api/v1/Attachment/GetMyAttachments` 并手写鉴权头。
  - `UserFollowPanel`：关注动态、粉丝和关注列表，已通过 `UserFollow` API；这部分正式 Web 已有 `/circle` 权威入口。

### WebOS `experience-detail`

- `ExperienceDetailApp` 覆盖等级、经验趋势、来源分布和分页经验流水。
- 业务 API 已集中在 `experienceApi`，可复用。
- 顶层仍是 WebOS 应用样式和窗口语义；正式 Web 应新建 `/me/experience` 路由承接，不照搬窗口壳层。

### API 与契约

现有后端能力已经覆盖 B2 首批需要：

- `GET /api/v1/Post/GetUserPosts`
- `GET /api/v1/Comment/GetUserComments`
- `GET /api/v1/Forum/GetMyQuickReplies`（前端通过 `getMyQuickReplies`）
- `GET /api/v1/User/GetMyBrowseHistory`
- `GET /api/v1/Attachment/GetMyAttachments`
- `GET /api/v1/UserFollow/GetMyFollowingFeed`
- `GET /api/v1/UserFollow/GetMyFollowers`
- `GET /api/v1/UserFollow/GetMyFollowing`
- `GET /api/v1/UserFollow/GetMySummary`
- `GET /api/v1/Experience/GetMyExperience`
- `GET /api/v1/Experience/GetTransactions`

首批不需要新增后端模型或 API。实现时应把旧组件里手写 `fetch` 的列表调用收口到 `@radish/http` 统一客户端，避免把 WebOS 时代的 API 调用方式迁进正式 Web。

## B2 范围

首批纳入：

- `/me/content`：我的内容中心，覆盖我的帖子、我的评论和我的轻回应。
- `/me/history`：完整浏览历史中心，覆盖分页、公开目标跳转和来源返回。
- `/me/attachments`：我的附件管理，覆盖筛选、搜索、下载 / 复制链接和删除确认。
- `/me/experience`：经验详情，覆盖等级状态、分页经验流水和必要统计展示。
- `/me` 看板补正式入口卡片，进入上述子页面。
- `/circle?tab=feed|following|followers` 作为关注面板正式 Web 权威入口；`/me` 提供个人中心联动，不重建第二套关注关系页。

首批不纳入：

- WebOS Dock、窗口系统、桌面 app 外壳和窗口几何记忆。
- 完整资料编辑、头像裁剪重做、账号安全设置、支付口令、转账和资产风控。
- 论坛发帖 / 编辑 / 回答作者态；这些进入 `P3-12-B3`。
- 文档作者态；这部分进入 `P3-12-B4` 归属裁决。
- 新推荐算法、短动态、私信、联邦协议、完整聊天平台或 Flutter 承接。
- 页面级 UI 重设计或跨页面视觉重塑；这些后置到 `P3-12-D` 并使用 Pencil。

## 路由边界

推荐正式 Web 路由：

| 路由 | 类型 | 说明 |
| --- | --- | --- |
| `/me` | 登录态私域 | 保持状态看板，作为个人中心总入口。 |
| `/me/assets` | 登录态私域 | B1 已完成，资产总览。 |
| `/me/assets/transactions` | 登录态私域 | B1 已完成，资产流水。 |
| `/me/content` | 登录态私域 | 我的内容，query 支持 `tab=posts|comments|quick-replies&page={n}`。 |
| `/me/history` | 登录态私域 | 完整浏览历史，query 支持 `page={n}`，筛选后续按真实需求扩展。 |
| `/me/attachments` | 登录态私域 | 我的附件，query 支持 `businessType`、`keyword`、`page`。 |
| `/me/experience` | 登录态私域 | 经验详情，query 支持 `page={n}`。 |
| `/circle` | 登录态私域 | 关注面板权威入口，保留 `tab/page` 语义。 |

路由处理原则：

- `meRouteState` 负责解析、规范化和构造 `/me/*` 路由，非法 tab / page 回到对应默认值。
- `authReturnPath` 只白名单上述精确路由和合法 query，拒绝未知参数、hash、外部 URL、反斜杠和非法页码。
- `/me/content` 进入帖子或评论目标时，优先跳正式公开详情 `/forum/post/:postPublicId`，并通过一次性来源转交提供“返回我的状态 / 我的内容”语义。
- `/me/history` 的 Post / Wiki / Product / User 目标应继续使用公开正式 URL，不再使用 WebOS workspace opener。
- `/me/attachments` 只暴露用户自己可访问的附件管理，不改变附件权限模型。
- `/me/experience` 只迁移个人经验详情，不改变经验发放、冻结、治理和排行榜规则。

## 可复用与需改造

可复用：

- `ExperienceBar`、`LineChart`、`PieChart` 等共享 UI 组件。
- `experienceApi.getMyExperience`、`experienceApi.getTransactions`。
- `getMyBrowseHistory`、`getMyQuickReplies`、`UserFollow` API helpers。
- `UserPostList`、`UserCommentList`、`UserQuickReplyList`、`UserBrowseHistoryList`、`UserAttachmentList`、`UserFollowPanel` 的业务展示思路和局部渲染结构。

需改造：

- `UserPostList`、`UserCommentList` 从手写 `fetch` 改为前端 API helper 或统一 `@radish/http` 调用。
- `UserAttachmentList` 从手写鉴权头改为 `@radish/http` helper，保留删除确认和复制链接能力。
- `UserBrowseHistoryList` 的点击目标从 `workspaceNavigation` 改为正式 Web `href` 和来源转交。
- `ProfileApp` 顶层只作为 WebOS 历史入口维护，不作为 `/me` 正式 Web 页面入口。
- `ExperienceDetailApp` 不整页复用；正式 Web 新建路由页面，复用 API 与必要展示逻辑。

## 验证口径

首批代码建议覆盖：

- `Frontend/radish.client/tests/meRouteState.test.ts`
- `Frontend/radish.client/tests/authReturnPath.test.ts`
- `Frontend/radish.client/tests/entryRoute.test.ts`
- `Frontend/radish.client/tests/realUsagePathContracts.test.ts`
- 涉及来源返回时补 `publicRouteNavigation.test.ts`
- `npm run type-check --workspace=radish.client`
- `npm run build --workspace=radish.client`（页面接入完成后）
- `git diff --check`

阶段验收或准备合并前：

- 在用户明确确认 API / Auth / Gateway / 前端已启动后，再集中执行 PC / mobile Gateway 真实页面复核。
- 覆盖 `/me`、`/me/content`、`/me/history`、`/me/attachments`、`/me/experience`、`/circle` 关注面板入口、公开详情来源返回和登录回流。

## 风险点

- 旧 `ProfileApp` 顶层依赖 WebOS 窗口状态，直接搬迁会把窗口壳层、内存导航和 `/desktop` 假设带入正式 Web。
- 旧列表组件部分仍手写 `fetch`，不符合当前统一 API client 规范；实现时应先收口 API helper。
- 浏览历史中存在旧 `routePath` 和 LongId fallback，正式 Web 跳转必须优先使用公开 URL，并保留兼容但不把旧 ID 当作新分享口径。
- 附件管理涉及删除动作和权限边界，首批只迁移用户自己的附件列表，不改变后端附件访问控制。
- 经验详情里图表和分页可以迁移，但不能借 B2 做经验系统运营规则、排行榜规则或治理台改造。
- 关注关系已有 `/circle`，若在 `/me` 再实现一套完整关系链页面，会造成入口职责重复；B2 只做个人中心联动和归属说明。
