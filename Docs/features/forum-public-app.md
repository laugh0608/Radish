# 公开 forum 应用结构

> Radish 公开内容壳层中的 forum 阅读入口说明。
>
> **最后更新**: 2026.05.16

## 定位

公开 forum 应用承载未登录或外链回流用户的论坛阅读路径，入口位于 `Frontend/radish.client/src/public/forum/`。它属于公开内容壳层，不等同于登录后的 WebOS 论坛工作台。

当前公开 forum 保持只读边界：可以浏览列表、搜索结果、标签聚合、类型流、帖子详情、轻回应墙和评论阅读；不承载发帖、评论提交、投票提交、通知中心或桌面窗口上下文。

## 路由范围

- `/forum`：公开帖子列表。
- `/forum/type/:type`：公开类型流，当前用于 `recommend`、`hot`、`newest`。
- `/forum/search`：公开搜索结果，支持关键词、时间范围、排序与分页。
- `/forum/tag/:tagId`：公开标签聚合阅读。
- `/forum/post/:postId`：公开帖子详情；当前参数可以是 `Post.PublicId` 或旧 long 字符串，公开分享、canonical 和回流入口优先生成 `PublicId` 路径，旧 long 仅保留兼容读取。

## 前端结构

```text
Frontend/radish.client/src/public/forum/
├── PublicForumApp.tsx       # 公开 forum 应用容器与路由状态编排
├── PublicForumList.tsx      # 公开帖子列表页
├── PublicForumSearch.tsx    # 公开搜索结果页
├── PublicForumTag.tsx       # 公开标签聚合页
├── PublicForumTypeFeed.tsx  # recommend / hot / newest 类型流
├── PublicForumDetail.tsx    # 公开帖子详情页
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
- 公开 forum 与桌面 forum 可以复用 API 和展示语义，但不能复刻桌面工作台的写入型交互。
- 公开 forum 的用户可见文案不应把旧 long 帖子 ID、评论 ID、作者 ID 或分类 ID 当作标题 / 摘要 fallback；这些标识只能继续作为兼容路由、内部点击或定位参数使用。

## 相关文档

- [论坛应用功能说明](./forum-features.md)
- [论坛帖子分类与标签](./forum-category-tag.md)
- [前端设计](../frontend/design.md)
