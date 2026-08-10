# 公开 forum 应用结构

> Radish 公开内容壳层中的 forum 阅读入口说明。
>
> **最后更新**: 2026.08.10

## 定位

公开 forum 应用承载未登录或外链回流用户的论坛阅读路径，入口位于 `Frontend/radish.client/src/public/forum/`。它属于公开内容壳层，不等同于登录后的 WebOS 论坛工作台。

当前公开 forum 以阅读为主，同时承接已经完成权威专题的受控互动：轻回应、两级回帖、帖子 / 回帖点赞与 reaction、问答回答生命周期、Post / Comment 赞赏、帖子收藏、内容举报，以及帖子 / 回帖作者编辑与版本历史。`/forum/compose` 承接发帖；详情页通过受控 intent 恢复登录回流、作者现场或目标定位。

公开 forum 仍不复刻 WebOS 三栏工作台、Dock、窗口参数或 `openApp` 语义。普通登录读者可在正式详情执行帖子 / 回帖点赞与 reaction，并对根回帖或既有子回帖发起固定两级回复；作者删除、投票提交、抽奖执行、通知中心和 Console 治理动作不进入普通读者代表状态。

`2026-07-04` D61 后，公开 forum 已按 Public Web Pencil `P03 / P04 / P10 / P11` 完成当前发布前首轮 UI 对齐：列表页强调标题、摘要、分类 / 标签、作者、赞评阅、最近互动和登录发帖入口；详情页强调来源返回、帖子级轻回应、神评 / 沙发 badge、表情 reaction、评论树父子层级和登录后参与入口。

## 路由范围

- `/forum`：公开帖子列表。
- `/forum/type/:type`：公开类型流，当前用于 `recommend`、`hot`、`newest`。
- `/forum/search`：公开搜索结果，支持关键词、时间范围、排序与分页。
- `/forum/tag/:tagSlug`：公开标签聚合阅读；使用 canonical slug，提供权威公开数量、相关主题、排序与分页。
- `/forum/compose`：登录态发帖入口；未登录时保存该路径登录回流，可携带合法 `category` query。
- `/forum/post/:postId`：公开帖子详情；当前参数可以是 `Post.PublicId` 或旧 long 字符串，公开分享、canonical、OpenGraph、JSON-LD 和普通内容入口优先生成 `PublicId` 路径，旧 long 仅保留兼容读取。
- `/forum/post/:postId?intent=comment|quickReply|answer|edit|history|reward|bookmark`：公开详情登录回流、参与或作者意图，只用于恢复轻回应、根评论、回答、作者编辑 / 历史、赞赏或收藏现场，不进入 canonical、分享链接、OpenGraph、JSON-LD 或 sitemap。

## 前端结构

```text
Frontend/radish.client/src/public/forum/
├── PublicForumApp.tsx       # 公开 forum 应用容器与路由状态编排
├── PublicForumList.tsx      # 公开帖子列表页
├── PublicForumSearch.tsx    # 公开搜索结果页
├── PublicForumTag.tsx       # 公开标签聚合页
├── PublicForumTypeFeed.tsx  # recommend / hot / newest 类型流
├── PublicForumDetail.tsx    # 详情数据、写入协调与身份边界
├── PublicForumDetailView.tsx # PC / mobile 正式详情结构
├── usePublicForumAnswerPage.ts
├── usePublicForumCommentFocus.ts
├── usePublicForumCommentRealtime.ts
├── usePublicForumPostHead.ts
├── PublicForumCompose.tsx   # 正式 Web 发帖入口
├── PublicStatusCard.tsx     # 公开页通用加载 / 空态 / 错误状态
├── publicForumUtils.ts      # 公开 forum URL、统计与展示辅助逻辑
├── publicForumViewState.ts  # 公开 forum 视图状态解析
└── PublicForumApp.module.css
```

## 维护约束

- `PublicForumApp.tsx` 只负责公开 forum 的应用级状态、路由分派、详情上下文和共享数据协调。
- `/forum/compose` 必须把共享 `ForumPostComposer` 作为页面内唯一任务实例；R2-A02 已确认正式路由默认在论坛浏览上下文上以底部半屏承载，并可原位展开全屏，展开 / 收起不得重建草稿、上传或提交状态。WebOS `ForumApp` 仍只通过薄 `PublishPostModal` Bottom Sheet 外壳承载同一核心，不复制正式路由的背景、双形态承载或发布状态机。
- 发布器本地草稿统一通过版本化 helper 按当前 `userId` 分区；旧 `forum_post_draft` 无 owner 记录失败关闭，Workbench、兼容 Form 和共享 Composer 读取同一账号真相源，发布成功只删除当前账号草稿。
- 发布器系统文案与无障碍名称全部来自 `community` 双语资源；分类失败与发布失败只显示本地化安全反馈，诊断进入统一日志，父页面不得再展示原始 `error.message` 或第二次失败 toast。
- 列表、搜索、标签、类型流和详情页面组件应优先保持页面职责清晰，不把局部展示逻辑回填到容器。
- 跨页面复用的 URL、统计、展示文案和视图状态解析逻辑应继续收敛到 `publicForumUtils.ts` 或 `publicForumViewState.ts`。
- 公开页共享的加载、空态和错误态优先复用 `PublicStatusCard.tsx`。
- 公开 forum 与桌面 forum 可以复用 API、局部组件和展示语义；公开详情只开放本页已列出的轻回应、两级回帖、帖子 / 回帖点赞与 reaction、回答生命周期、收藏、Post / Comment 赞赏、举报及作者编辑 / 历史能力。其余写入型交互不应因为共享组件已支持就从桌面工作台直接搬入公开页。
- 公开 forum 的用户可见文案不应把旧 long 帖子 ID、评论 ID、作者 ID 或分类 ID 当作标题 / 摘要 fallback；这些标识只能继续作为兼容路由、内部点击或定位参数使用。
- 旧 long 详情路径加载成功后，如果详情接口返回 `VoPublicId`，运行时 head 必须把 canonical、OpenGraph URL 与 JSON-LD 统一刷新到 `/forum/post/:publicId`，不能继续把旧 long 路径作为分享预览主口径。
- 公开详情首屏优先展示帖子正文和关键统计；阅读边界说明与登录后轻参与入口应放在正文之后，避免说明型内容抢占真实阅读入口。
- 公开列表首屏应保持高密度真实帖子流：分类、标签、作者、互动数字和最近互动属于列表项核心信息；reaction 不在列表页展开，避免压低首屏内容量。
- 公开详情的轻回应输入复用帖子轻回应独立模型；写回帖继续复用评论发布接口：根回帖提交 `parentId = null`，回复目标提交既有 `parentId / replyToCommentId / replyToCommentSnapshot / replyToUserName`，展示深度固定为两级。作者态发帖、回答和编辑继续复用论坛写入可靠性治理中的 `clientSubmissionId`，不能新增临时 fetch / axios 调用或绕开 `@radish/http`。
- 公开详情受控 intent 必须是 `comment`、`quickReply`、`answer`、`edit`、`history`、`reward` 或 `bookmark`；`commentId` 用于评论定位或 Comment 赞赏目标，`answer / answerPage / answerSort` 用于回答现场。普通公开来源、专题返回和分享复制继续使用 `history.state` 或 canonical 路径承载。
- 公开 forum 的列表、搜索、标签、类型流、详情返回和状态卡动作都必须输出真实公开 `href`。普通点击可以保持 SPA 导航和来源返回；新开标签、复制链接、canonical、OpenGraph、JSON-LD 与 sitemap 不读取当前标签页来源状态。
- 公开标签页的相关主题只读取服务端公开共现聚合，loading / empty / error / retry 保持局部边界；标签 canonical 始终使用无 `sort / page` 的 `/forum/tag/:canonicalSlug`。
- 公开标签被禁用、删除或不存在时，运行时必须使用 `noindex, nofollow`，并移除 canonical、OpenGraph URL 和 JSON-LD；不可索引态不得保留先前标签的可索引元数据。
- 公开详情的“回答 / 轻回应 / 编辑 / 历史 / 评论”入口必须以真实 `href` 表达对应 intent，不再用纯按钮承担跳转语义；当前标签页普通点击可以拦截为原地展开、加载作者态或聚焦输入区，辅助点击和复制链接仍保留完整回流路径。收藏和赞赏同样使用受控登录 return path，但回流后不得自动写入。
- 移动公开详情按来源 / 分享、标题与元数据、正文、帖子操作、可用回答区、轻回应、评论输入与评论流排序；桌面辅助区不得整体堆到正文末尾。
- 回帖树必须表达父子层级：父回帖保留完整条目和神评状态，子回帖缩进并保留沙发与引用上下文；新回复仍归入两级结构，不继续增加嵌套深度。帖子 / 回帖点赞总数消费服务端写入回包，reaction 使用既有权威 toggle 契约；神评 / 沙发不能降级为详情页侧栏元字段。

## 验证要点

- 匿名打开 `/forum/post/:publicId` 可阅读正文、轻回应墙和评论树。
- 匿名触发轻回应、根评论、回答、收藏或赞赏会进入 OIDC 登录，并在回调后回到同一公开详情的对应现场；收藏和赞赏仍等待用户再次确认。
- 登录后可在公开详情发布轻回应和两级回帖，并执行帖子 / 回帖点赞与 reaction；问题帖可通过受控 `answer` intent 进入回答现场，帖子 / 回帖 / 回答作者按各自专题取得编辑、历史或恢复能力，问题作者可采纳或撤销回答。
- 工作区 intent 链接支持新标签打开和复制链接；普通点击仍应保留当前页来源返回、轻回应 / 评论区聚焦和作者态现场恢复。
- 当前 Public 详情不开放超过两级的嵌套回复、投票提交、抽奖执行、帖子 / 回帖删除和通知中心；内容举报只进入既有举报链路，不在 Public 页面执行治理。
- 公开详情复制链接、canonical、OpenGraph、JSON-LD 和 sitemap 不携带 `intent`、`commentId`、来源状态或桌面窗口参数。
- 公开标签页相关主题使用真实 `/forum/tag/:canonicalSlug` 链接；普通点击切换 SPA 标签路由，辅助点击、新标签和复制链接保持浏览器原生语义。
- `/forum/compose` 发帖成功后回到正式 Web 帖子详情；未登录登录回流后仍留在发帖现场。

## 相关文档

- [R1-P02 Public 详情与互动代码事实与设计边界审计](/records/f4-r-r1-p02-public-detail-interaction-audit-2026-08-05)
- [R1-P02 帖子详情成组实现与运行态验收](/records/f4-r-r1-p02-public-detail-implementation-2026-08-08)
- [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)
- [论坛应用功能说明](./forum-features.md)
- [论坛帖子分类与标签](./forum-category-tag.md)
- [前端设计](../frontend/design.md)
