# 公开 forum 应用结构

> Radish 公开内容壳层中的 forum 阅读入口说明。
>
> **最后更新**: 2026.06.21

## 定位

公开 forum 应用承载未登录或外链回流用户的论坛阅读路径，入口位于 `Frontend/radish.client/src/public/forum/`。它属于公开内容壳层，不等同于登录后的 WebOS 论坛工作台。

当前公开 forum 以阅读为主，同时开放两类登录后轻参与：帖子详情内发布轻回应和根评论。`2026-06-21` 起，正式 Web 首批作者态也进入公开 forum 路由体系：`/forum/compose` 承接发帖，详情页受控 `intent=answer|edit|history` 承接问答回答 / 采纳、作者帖子编辑和帖子编辑历史查看。

公开 forum 仍不复刻 WebOS 三栏工作台、Dock、窗口参数或 `openApp` 语义；评论回复直达 URL、点赞、投票提交、删除、治理、通知中心和完整创作器仍不进入首批正式 Web 作者态。

## 路由范围

- `/forum`：公开帖子列表。
- `/forum/type/:type`：公开类型流，当前用于 `recommend`、`hot`、`newest`。
- `/forum/search`：公开搜索结果，支持关键词、时间范围、排序与分页。
- `/forum/tag/:tagId`：公开标签聚合阅读。
- `/forum/compose`：登录态发帖入口；未登录时保存该路径登录回流，可携带合法 `category` query。
- `/forum/post/:postId`：公开帖子详情；当前参数可以是 `Post.PublicId` 或旧 long 字符串，公开分享、canonical、OpenGraph、JSON-LD 和普通内容入口优先生成 `PublicId` 路径，旧 long 仅保留兼容读取。
- `/forum/post/:postId?intent=quickReply|comment|answer|edit|history`：公开详情登录回流参与 / 作者意图，只用于未登录用户触发轻回应、根评论、回答、作者编辑或历史查看后恢复现场，不进入 canonical、分享链接、OpenGraph、JSON-LD 或 sitemap。

## 前端结构

```text
Frontend/radish.client/src/public/forum/
├── PublicForumApp.tsx       # 公开 forum 应用容器与路由状态编排
├── PublicForumList.tsx      # 公开帖子列表页
├── PublicForumSearch.tsx    # 公开搜索结果页
├── PublicForumTag.tsx       # 公开标签聚合页
├── PublicForumTypeFeed.tsx  # recommend / hot / newest 类型流
├── PublicForumDetail.tsx    # 公开帖子详情页与受控作者态 intent
├── PublicForumCompose.tsx   # 正式 Web 发帖入口
├── PublicStatusCard.tsx     # 公开页通用加载 / 空态 / 错误状态
├── publicForumUtils.ts      # 公开 forum URL、统计与展示辅助逻辑
├── publicForumViewState.ts  # 公开 forum 视图状态解析
└── PublicForumApp.module.css
```

## 维护约束

- `PublicForumApp.tsx` 只负责公开 forum 的应用级状态、路由分派、详情上下文和共享数据协调。
- 列表、搜索、标签、类型流和详情页面组件应优先保持页面职责清晰，不把局部展示逻辑回填到容器。
- 跨页面复用的 URL、统计、展示文案和视图状态解析逻辑应继续收敛到 `publicForumUtils.ts` 或 `publicForumViewState.ts`。
- 公开页共享的加载、空态和错误态优先复用 `PublicStatusCard.tsx`。
- 公开 forum 与桌面 forum 可以复用 API、局部组件和展示语义；公开详情允许轻回应、根评论、问答回答 / 采纳、作者帖子编辑和帖子编辑历史查看。其余写入型交互不应从桌面工作台直接搬入公开页。
- 公开 forum 的用户可见文案不应把旧 long 帖子 ID、评论 ID、作者 ID 或分类 ID 当作标题 / 摘要 fallback；这些标识只能继续作为兼容路由、内部点击或定位参数使用。
- 旧 long 详情路径加载成功后，如果详情接口返回 `VoPublicId`，运行时 head 必须把 canonical、OpenGraph URL 与 JSON-LD 统一刷新到 `/forum/post/:publicId`，不能继续把旧 long 路径作为分享预览主口径。
- 公开详情首屏优先展示帖子正文和关键统计；阅读边界说明与登录后轻参与入口应放在正文之后，避免说明型内容抢占真实阅读入口。
- 公开详情的轻回应输入复用帖子轻回应独立模型；根评论输入复用评论发布接口但只提交 `parentId = null`。作者态发帖、回答和编辑继续复用论坛写入可靠性治理中的 `clientSubmissionId`，不能新增临时 fetch / axios 调用或绕开 `@radish/http`。
- 公开详情登录回流只接受 `commentId` 与 `intent` 两类查询参数，且 `intent` 必须是 `comment`、`quickReply`、`answer`、`edit` 或 `history`；`commentId` 只允许配合 `comment` / `quickReply`。普通公开来源、专题返回和分享复制继续使用 `history.state` 或 canonical 路径承载。
- 公开 forum 的列表、搜索、标签、类型流、详情返回和状态卡动作都必须输出真实公开 `href`。普通点击可以保持 SPA 导航和来源返回；新开标签、复制链接、canonical、OpenGraph、JSON-LD 与 sitemap 不读取当前标签页来源状态。

## 验证要点

- 匿名打开 `/forum/post/:publicId` 可阅读正文、轻回应墙和评论树。
- 匿名触发轻回应或根评论会进入 OIDC 登录，并在回调后回到同一公开详情的对应输入区。
- 登录后可在公开详情直接发布轻回应和根评论；问题帖可通过受控 `answer` intent 进入回答现场，作者可通过 `edit` / `history` intent 编辑自己的帖子或查看帖子编辑历史。
- 评论回复直达 URL、点赞、投票、删除、举报治理和通知中心入口不出现在公开详情主流程。
- 公开详情复制链接、canonical、OpenGraph、JSON-LD 和 sitemap 不携带 `intent`、`commentId`、来源状态或桌面窗口参数。
- `/forum/compose` 发帖成功后回到正式 Web 帖子详情；未登录登录回流后仍留在发帖现场。

## 相关文档

- [论坛应用功能说明](./forum-features.md)
- [论坛帖子分类与标签](./forum-category-tag.md)
- [前端设计](../frontend/design.md)
