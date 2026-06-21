# P3-12-B3 论坛作者态 Web 化方案

> 日期：2026-06-21（Asia/Shanghai）
>
> 状态：首批代码已完成，真实 Gateway PC / mobile 复核待 B3 小阶段验收时集中执行
>
> 结论：`P3-12-B3` 已完成首批正式 Web 作者态迁移：新增 `/forum/compose`，扩展详情受控 `intent=answer|edit|history`，发帖 / 回答 / 编辑继续复用 `@radish/http` API 与 `clientSubmissionId`，登录回流进入正式 Web 路径，不搬迁 WebOS 三栏工作台、窗口参数、Dock 或 `openApp` 语义。

## 本轮核对

Git 状态：

- `git status --short --branch`：`dev...origin/dev [ahead 20]`，本轮进入 B3 梳理前仅有 B2 正式链接语义补口改动。
- `git log -1 --oneline`：`712e6dd4 feat(client): 接入个人中心 Web 子路径`。
- `git rev-list --count master..dev`：`64`。

已读范围：

- `Docs/planning/current.md`
- `Docs/planning/p3-12-web-completion-webos-retirement.md`
- `Docs/records/p3-12-b2-personal-center-web-plan-2026-06-21.md`
- `Docs/guide/forum-content-write-reliability-governance.md`
- `Frontend/radish.client/src/apps/forum/ForumApp.tsx`
- `Frontend/radish.client/src/apps/forum/hooks/useForumActions.ts`
- `Frontend/radish.client/src/apps/forum/components/PostDetail.tsx`
- `Frontend/radish.client/src/apps/forum/components/PublishPostModal.tsx`
- `Frontend/radish.client/src/apps/forum/components/EditPostModal.tsx`
- `Frontend/radish.client/src/apps/forum/components/EditHistoryModal.tsx`
- `Frontend/radish.client/src/apps/forum/views/PostDetailContentView.tsx`
- `Frontend/radish.client/src/public/forum/PublicForumApp.tsx`
- `Frontend/radish.client/src/public/forum/PublicForumDetail.tsx`
- `Frontend/radish.client/src/public/forumRouteState.ts`
- `Frontend/radish.client/src/services/authReturnPath.ts`
- `Frontend/radish.client/src/api/forum.ts`

未执行：

- 未启动 API / Auth / Gateway / Vite。
- 未做 PC / mobile Gateway 真实页面复核。
- 未进入页面级 UI 设计、视觉重塑或 Pencil 设计稿；本轮只做功能迁移方案与范围判断。

## B2 复核结论

B2 没有发现阻断 B3 的剩余缺口。本轮只补了个人中心内容与浏览历史列表的正式 Web 链接语义：

- `/me/content` 的我的帖子、评论和轻回应列表提供真实 `/forum/post/*` `href`。
- `/me/history` 的公开目标继续提供真实公开 URL，并保留来源返回。
- 旧 `ProfileApp` 和 `ExperienceDetailApp` 顶层仍作为 `/desktop` 历史入口维护，不进入正式 `/me` 页面。
- 新增静态契约测试，防止 `/me` 正式页面回退到纯 `onClick` 或 WebOS opener。

## 现状判断

### WebOS `ForumApp`

`ForumApp` 不能整页搬到正式 Web：

- 顶层使用 `useCurrentWindow()` 读取窗口参数。
- 通过 `useWindowStore().openApp()` 打开个人资料等 WebOS app。
- 发布、编辑、编辑历史、举报和确认弹层挂在桌面窗口上下文内。
- `PostDetailContentView` 的评论 / 轻回应登录回流仍使用 `buildDesktopForumPostReturnPath()`。
- 三栏布局、窗口 intent 和桌面工作台状态是 WebOS 形态能力，不是正式 Web 产品路径。

可复用的是业务动作和局部组件，而不是 WebOS 容器：

- `useForumActions` 已集中发帖、编辑、删除、回答、采纳、投票、抽奖、评论、轻回应、编辑历史、点赞和置顶等动作。
- `PostDetail` 已有 `interactive / readOnly` 模式和作者态 props，可承接正式 Web 登录态增强。
- `PublishPostModal`、`EditPostModal`、`EditHistoryModal` 可作为首批复用对象，但需要解除默认 `/desktop` 登录回流假设。

### 公开论坛正式 Web

当前正式 Web 论坛已具备公开浏览和有限写入：

- `/forum`、分类、标签、搜索、问答、投票、抽奖和 `/forum/post/:postId` 已由 `public/forumRouteState.ts` 管理。
- `PublicForumDetail` 已支持根评论、评论导航、实时评论、轻回应、登录回流和来源保持。
- 详情 intent 当前只允许 `comment` 和 `quickReply`。
- `authReturnPath` 也只白名单 `/forum/post/:id?intent=comment|quickReply`，会拒绝 `intent=edit` 等未声明作者态入口。

因此 B3 首批应先扩展受控正式 Web intent，而不是从组件里手写跳转或临时拼接未白名单 URL。

### API 与写入可靠性

现有 API 已覆盖首批 B3 需求：

- `publishPost`
- `updatePost`
- `createComment`
- `updateComment`
- `answerQuestion`
- `acceptQuestionAnswer`
- `getPostEditHistory`
- `getCommentEditHistory`
- `deletePost`
- `deleteComment`
- `createPostQuickReply`
- `deletePostQuickReply`
- `votePoll`
- `closePoll`
- `drawLottery`

后端 DTO 已支持 `clientSubmissionId` 的关键写入包括：

- `PublishPostDto`
- `UpdatePostDto`
- `CreateCommentDto`
- `UpdateCommentDto`
- `CreateAnswerDto`

`Docs/guide/forum-content-write-reliability-governance.md` 已明确论坛 Web 写入口必须生成并复用 `clientSubmissionId`。B3 不能绕开 `createClientSubmissionState`，也不能新增一套临时 fetch / axios 调用。

## 正式 Web 路由边界

推荐首批路由：

| 路由 | 类型 | 说明 |
| --- | --- | --- |
| `/forum/compose` | 登录态作者入口 | 发帖入口；未登录时进入登录并回流。 |
| `/forum/post/:postId` | 公开详情 | 保持 canonical 阅读路径，不附带作者态 intent。 |
| `/forum/post/:postId?intent=comment` | 登录态互动焦点 | 已有根评论 / 回复焦点。 |
| `/forum/post/:postId?intent=quickReply` | 登录态互动焦点 | 已有轻回应焦点。 |
| `/forum/post/:postId?intent=answer` | 登录态作者 / 参与焦点 | 问答帖回答输入焦点。 |
| `/forum/post/:postId?intent=edit` | 登录态作者入口 | 作者帖子编辑入口。 |
| `/forum/post/:postId?intent=history` | 登录态作者反馈 | 帖子编辑历史查看入口。 |

首批不建议新增 `/desktop?app=forum` 回流，也不建议新增另一套 `/forum/editor/*` 深路径。当前 `PublicForumDetail` 已以 query intent 管理评论和轻回应焦点，继续沿用 `intent` 能保持公开 canonical 路径稳定，并减少重复详情容器。

评论编辑和评论历史首批可先通过详情内作者按钮触发，不强制暴露直达 URL。若后续确需直达，应单独扩展 `intent=commentEdit|commentHistory&commentId={id}`，并补齐 `authReturnPath` 白名单和测试。

## 首批代码范围建议

1. 路由契约
   - 扩展 `PublicForumDetailIntent`：新增 `answer`、`edit`、`history`。
   - 扩展 `buildPublicForumPath()` 和解析逻辑，非法 intent 仍回到普通详情。
   - 扩展 `authReturnPath`，新增 `buildPublicForumComposeReturnPath()` 和作者态 `buildPublicForumPostReturnPath()` 白名单。

2. 发帖入口
   - 新增 `/forum/compose` 正式 Web 入口。
   - 复用 `PublishPostModal` 或抽出不依赖 WebOS 的发帖容器。
   - `PublishPostModal` 的登录回流应可注入正式 Web return path，不能继续硬编码 `buildDesktopForumReturnPath()`。

3. 详情作者态增强
   - 在 `PublicForumDetail` 中按受控 intent 启用作者态能力。
   - 已登录且有权限时，支持帖子编辑、帖子编辑历史、问答回答、采纳和必要作者反馈。
   - 未登录时使用正式 Web return path；无权限时显示明确不可操作状态，不跳回 `/desktop`。

4. 写入可靠性延续
   - 发帖继续使用 `forum-post:` submission key。
   - 评论继续使用 `forum-comment:` submission key。
   - 回答继续使用 `forum-answer:` submission key。
   - 帖子编辑继续使用 `forum-post-edit:` submission key。
   - 评论编辑继续使用 `forum-comment-edit:` submission key。

5. 导航和来源
   - 导航入口使用真实 `<a href>` 或正式 Web path builder。
   - 普通点击可保持当前壳层内导航；新标签、复制链接和 SEO 语义必须有真实 URL。
   - 作者头像 / 用户资料跳转继续优先 `/u/usr_*` 公开主页，不引入 `openApp('profile')`。

## 首批不纳入

- WebOS 三栏工作台、Dock、窗口管理、窗口几何记忆和 `/desktop` 参数兼容重做。
- 完整论坛 UI 重设计或跨页面视觉体系调整；这些后置到 `P3-12-D` 并先做 Pencil。
- 新后端 API、数据库模型、审核平台、反垃圾系统或频率限制平台。
- Flutter 发帖 / 编辑新增承接。
- 回答编辑；该能力已在论坛内容发布可靠性治理中后置单独评审。
- 评论编辑 / 评论历史直达 URL；首批先保留详情内作者按钮触发。
- 文档作者态；该项进入 `P3-12-B4` 归属裁决。

## 首批代码结果

已完成：

- `PublicForumRoute` 新增 `compose`，`PublicForumDetailIntent` 新增 `answer`、`edit`、`history`。
- `authReturnPath` 新增 `/forum/compose` 白名单和 `buildPublicForumComposeReturnPath()`，并允许详情受控作者态 intent；普通 `/forum/post/:id` 仍不能直接作为登录回流。
- 新增 `PublicForumCompose`，复用 `PublishPostModal`、`publishPost()` 与论坛发布幂等指纹，发帖成功后回到正式 Web 帖子详情。
- `PublishPostModal` 支持注入 `loginReturnPath`，未注入时保留 WebOS 桌面论坛回流。
- `PublicForumDetail` 切为受控互动详情：支持问答回答 / 采纳、作者编辑、帖子编辑历史查看；点赞、删除、置顶、评论编辑直达和治理动作仍未迁入公开详情。
- `PostDetail` 收敛空 handler 按钮，避免正式 Web 公开详情显示无处理器的点赞 / 作者按钮。
- 发帖、评论、回答、帖子编辑共用论坛提交指纹工具，继续通过 `createClientSubmissionState` 生成和复用 `clientSubmissionId`。

仍后置：

- 评论编辑 / 评论历史直达 URL。
- 投票提交、抽奖参与、点赞和删除等更重互动。
- 真实 PC / mobile Gateway smoke；按阶段约束等 B3 小阶段准备验收并由用户确认前后端已启动后执行。

## 验证口径

首批代码完成后建议覆盖：

- `Frontend/radish.client/tests/forumRouteState.test.ts` 或新增对应路由契约测试。
- `Frontend/radish.client/tests/authReturnPath.test.ts`
- `Frontend/radish.client/tests/entryRoute.test.ts`
- `Frontend/radish.client/tests/realUsagePathContracts.test.ts`
- `Frontend/radish.client/tests/publicSeoStatic.test.ts`
- `npm run type-check --workspace=radish.client`
- `npm run build --workspace=radish.client`
- `git diff --check`

本轮已执行并通过：

- `node --test --test-isolation=none ./Frontend/radish.client/tests/publicRouteState.test.ts ./Frontend/radish.client/tests/forumNavigation.test.ts ./Frontend/radish.client/tests/authReturnPath.test.ts ./Frontend/radish.client/tests/entryRoute.test.ts ./Frontend/radish.client/tests/realUsagePathContracts.test.ts ./Frontend/radish.client/tests/publicSeoStatic.test.ts`
- `npm run type-check --workspace=radish.client`
- `npm run build --workspace=radish.client`

阶段验收或准备合并前：

- 在用户明确确认 API / Auth / Gateway / 前端已启动后，再集中执行 PC / mobile Gateway 真实页面复核。
- 覆盖 `/forum`、`/forum/compose`、`/forum/post/:id?intent=answer`、`/forum/post/:id?intent=edit`、`/forum/post/:id?intent=history`、登录回流、无权限提示和公开 canonical 详情。

## 风险点

- 若直接复用 `ForumApp`，会把 `useCurrentWindow()`、`openApp()` 和 `/desktop` return path 带进正式 Web。
- 若只在按钮上做 `onClick`，会继续缺少真实 Web URL、登录回流和新标签语义。
- 若放宽 `authReturnPath` 白名单，会扩大登录后开放跳转风险。
- 若 B3 绕开 `clientSubmissionId`，会破坏已完成的论坛写入可靠性治理。
- 若把 UI 美化和功能迁移混在一批，会触发 Pencil 前置要求并扩大验收面。
