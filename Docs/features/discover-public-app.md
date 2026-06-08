# 公开社区发现应用结构

> Radish 纯 Web 默认入口 `/discover` 的公开内容流说明。
>
> **最后更新**: 2026.06.08

## 定位

`/discover` 是普通浏览器根入口 `/` 的公开社区发现页，承接未登录、轻登录和外链回流用户的第一跳阅读。

当前它属于公开内容壳层，不等同于 WebOS 工作台，也不等同于完整推荐系统或个人圈子。

## 内容流范围

首批内容流复用现有公开数据，不新增推荐读模型：

- 公开帖子：来自 forum 公开列表，卡片优先使用 `Post.PublicId` 路径。
- 公开文档：来自公开文档列表，卡片使用 `/docs/:slug`。
- 公开商品：来自公开商品数据，卡片使用 `/shop/product/:productId`。
- 公开榜单入口：指向 `/leaderboard` 和公开榜单类型路由。
- 公开主页入口：从用户卡片、榜单或作者入口进入 `/u/:identifier`，其中 `identifier` 优先为 `User.PublicId`。

首批不包含：

- 完整推荐算法
- 完整个人圈子 / 关注流
- ActivityPub / WebFinger / Mastodon 客户端能力
- 登录后工作台治理动作
- 订单、背包、账号设置或 Console 排障入口

## 前端结构

```text
Frontend/radish.client/src/public/discover/
├── PublicDiscoverApp.tsx       # 公开发现页容器、head、分享、来源状态
├── PublicDiscoverFeed.tsx      # 内容流卡片、分组和列表渲染
├── PublicDiscoverApp.module.css
```

## URL 与来源返回

- `/discover` 本身是公开集合页，canonical 和分享链接不携带工作台状态。
- 从 `/discover` 进入 forum / docs / shop / leaderboard / 公开主页后，返回动作应优先回到社区发现页和原阅读上下文。
- 公开详情来源返回使用浏览器 `history.state`，不写入 URL query、canonical、OpenGraph 或 sitemap。
- 普通浏览器根路径 `/` 当前进入 `/discover`；Tauri / WebOS 工作台仍保留 `/desktop`。

## 公开 head 与结构化数据

`/discover` 使用公开壳层统一 head helper 和结构化数据 helper：

- 作为公开集合页输出 `CollectionPage` JSON-LD。
- canonical 使用公开 Gateway origin。
- 分享链接只保留公开路径，不携带来源状态、窗口参数或登录后上下文。

## 维护约束

- 内容卡片应优先使用公开标识和公开路径；旧 LongId 只作为兼容打开或内部接口参数。
- 页面可以增加登录后轻互动入口，但不能把工作台动作搬进首屏内容流。
- 新增内容来源前应先确认已有公开 API、公开 head、移动 / PC 布局和来源返回语义。
- 若后续需要跨类型排序、推荐解释或个性化，应先单独设计 feed API 或读模型，不在当前页面内堆叠 ad hoc 排序逻辑。

## 验证要点

- PC 与移动视角下 `/discover` 能展示公开内容流，不退回 WebOS Shell。
- 卡片进入 forum / docs / shop / leaderboard / 公开主页后，返回语义稳定。
- 公开帖子卡片优先进入 `/forum/post/:publicId`。
- 公开主页入口优先进入 `/u/usr_...`，旧 LongId 仅保留兼容。
- head、canonical、OpenGraph 和 JSON-LD 不携带登录后状态或来源状态。
