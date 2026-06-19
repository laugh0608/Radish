# 个人圈子

> Radish 登录态个人圈子 `/circle` 的产品边界、职责分工与维护约束。
>
> **最后更新**: 2026.06.18

## 定位

`/circle` 是登录用户的关系链复访入口，回答“我关注的人最近有什么动态”。

它不是公开发现页，也不是另一个论坛列表。首批只复用既有 `UserFollow` 与 `Post` 数据，承接关注动态、我的关注和我的粉丝。

## 职责分工

| 入口 | 职责 | 不承担 |
| --- | --- | --- |
| `/discover` | 公开发现、外链回流、SEO、分享、未登录阅读 | 我的关注流、粉丝列表、私域动态 |
| `/forum` | 帖子、评论、标签、分类、搜索、发布、回复、点赞、神评 / 沙发 | 按个人关系链组织长期复访 |
| `/circle` | 登录态关系链复访、关注动态、关注 / 粉丝列表、打开帖子和公开个人页 | 新内容对象、推荐算法、转发 / 引用、私信、联邦协议 |

同一篇帖子可以同时出现在 `发现`、`论坛` 和 `圈子`，但对象权威归属仍在 `论坛`。详情、评论、点赞、神评、搜索和分类规则不在 `圈子` 重新实现。

## URL 与登录边界

- 固定入口：`/circle`
- 支持查询参数：`tab=feed|following|followers`、`page=正整数`
- 默认路由：`/circle` 等价于 `tab=feed&page=1`
- 匿名访问：进入 OIDC 登录并保存 `/circle` 回流路径
- 公开 SEO：不进入 sitemap，不输出 public canonical / OpenGraph / JSON-LD
- 工作台边界：不使用 `/desktop?app=...` 表达圈子状态

`/circle` 属于纯 Web 登录态页面，但不属于 public content shell。入口识别应和 `/discover`、`/forum`、`/u/:identifier` 分开维护。

从 `/circle` 普通点击进入 `/forum/post/:postPublicId` 或 `/u/:userPublicId` 时，前端可写入一次性来源转交，让公开详情页或公开个人页显示“返回我的圈子”。该状态只服务当前标签页的返回语义，不进入公开 URL、canonical、分享链接、sitemap 或 JSON-LD。

## 页面信息结构

`/circle` 当前按“关系摘要 -> 当前 tab -> 内容列表”的顺序组织，不做营销页或公开发现页：

1. 公共头部：保留品牌、社区发现和工作台入口；圈子自身不再重复展示“我的圈子”主动作。
2. 关系摘要：展示我的关注数、粉丝数和当前入口说明，辅助用户判断这是登录态私域关系流。
3. 快捷动作：保留去论坛和我的状态两个相邻入口，不把发帖、推荐或私信放进首屏主动作。
4. 三个 tab：`feed` 展示关注动态，`following` 展示我的关注，`followers` 展示我的粉丝。
5. 内容列表：关注动态卡片展示标题、摘要、作者、时间、评论数和点赞数；关注 / 粉丝用户项展示头像、展示名、公开索引、互相关注标记和关注时间。

所有帖子和用户项都输出真实公开 `href`。普通点击会附带当前 `/circle` tab / page 的一次性来源返回；新开标签、复制链接、canonical 和分享仍只使用公开 URL。

## 对象与接口

首批复用现有接口：

- `GET /api/v1/UserFollow/GetMyFollowingFeed`：关注动态
- `GET /api/v1/UserFollow/GetMyFollowing`：我的关注
- `GET /api/v1/UserFollow/GetMyFollowers`：我的粉丝
- `GET /api/v1/UserFollow/GetMySummary`：关系链汇总

对象标识规则：

- 帖子跳转优先使用 `Post.PublicId`，进入 `/forum/post/:postPublicId`
- 用户跳转优先使用 `User.PublicId`，进入 `/u/:userPublicId`
- `UserFollowUserVo` 必须输出 `VoPublicId`
- 旧 LongId 只作为兼容 fallback，不作为首选外部链接标识

## 当前实现

前端：

```text
Frontend/radish.client/src/circle/
├── CircleEntry.tsx             # 圈子入口，包裹 BootstrapGate
├── CircleApp.tsx               # 登录态、路由、数据加载与渲染
├── CircleApp.module.css        # 圈子页面样式
└── circleRouteState.ts         # /circle tab/page 路由解析
```

后端：

```text
Radish.Model/ViewModels/UserFollowVo.cs
Radish.Service/UserFollowService.cs
Radish.Api/Controllers/UserFollowController.cs
```

`UserFollowService` 会在关注 / 粉丝列表读取时补齐旧用户的 `PublicId`，保证圈子用户列表能稳定进入公开个人页。

## 维护约束

- 不新增 `CirclePost`、短动态、转发、引用或私信模型。
- 不在圈子页内重写帖子详情、评论、点赞、神评或分类搜索。
- 不把推荐 / 热门 / 最新公共分发流并入圈子主职责；公共分发仍归 `/discover` 或 forum 分发接口。
- 不直接实现 ActivityPub / WebFinger。若后续进入联邦方向，先单独评审对象标识、隐私边界和协议映射。
- Flutter 后续可承接同一关系链契约，但不要求复刻 Web 页面结构。

## 验证要点

- 匿名访问 `/circle` 后登录回流仍回到圈子页。
- `/circle?tab=following&page=2` 只保留合法 tab/page 参数。
- `/circle` 不被 `isPublicContentPathname` 识别为公开内容路由。
- 关注动态卡片进入 `/forum/post/:postPublicId`。
- 关注 / 粉丝用户进入 `/u/usr_...`。
- 从圈子进入公开帖子详情或公开个人页后，应能返回原来的圈子 tab/page；新开标签和复制链接仍只保留公开 URL。
- 公开主页 `/u/usr_...` 能被入口白名单识别并进入公开壳层。
- `radish.client` 构建、路由 / 回流测试、`UserFollow` 后端测试通过。
