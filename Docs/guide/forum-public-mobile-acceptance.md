# forum 公开移动入口人工验收清单

> 适用范围：`Phase 2-2` 下 forum 公开内容壳层的移动 Web 入口。
>
> 关联文档：
>
> - [当前进行中](/planning/current)
> - [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [论坛应用功能说明](/features/forum-features)
> - [人工验收模板](/guide/manual-acceptance-template)

## 1. 适用改动范围

- `Frontend/radish.client/src/public/` 下公开内容壳层入口、路由或状态回退逻辑改动后
- `/forum`、`/forum/post/:postId` 的移动阅读布局、列表卡片密度、分页或返回链路改动后
- 公开 forum 列表 / 详情对桌面 forum 组件的只读复用方式改动后

## 2. 前置条件

- 已完成前端最小自动化验证：
  - `npm run type-check --workspace=radish.client`
  - `npm run build --workspace=radish.client`
- 已启动本地前端与相关宿主，能在浏览器直接访问：
  - `/forum`
  - `/forum/post/:postId`
- 浏览器已切到手机尺寸，优先覆盖 `390 x 844` 与 `430 x 932` 两档窄屏视口
- 准备至少 4 类帖子数据：
  - 普通帖子
  - 带轻回应的帖子
  - 长标题 / 多标签帖子
  - 问答 / 投票 / 抽奖中的任意一类结构化帖子

## 3. 联调资产

- 前端入口：`Frontend/radish.client/src/public/PublicEntry.tsx`
- 公开 forum 应用：`Frontend/radish.client/src/public/forum/PublicForumApp.tsx`
- 列表卡片：`Frontend/radish.client/src/apps/forum/components/PostCard.tsx`
- 详情复用组件：`Frontend/radish.client/src/apps/forum/components/PostDetail.tsx`
- 评论树：`Frontend/radish.client/src/apps/forum/components/CommentTree.tsx`

说明：

- 本清单以 UI / 路由人工验收为主，不要求额外新增 `HttpTest`
- 若本轮同时触达帖子详情接口、评论分页接口或轻回应接口契约，再并行补：
  - `Radish.Api.Forum.Core.http`
  - `Radish.Api.Forum.Comment.http`

## 4. 最小人工验收顺序

1. 打开 `/forum`，确认页面直接进入公开内容壳层，而不是先进入桌面 `Shell`。
2. 在窄屏下检查列表首屏，确认卡片标题、分类、标签、作者、时间、统计和最近互动没有重叠、截断异常或明显挤压。
3. 切换分类与“最新 / 最热”，确认列表刷新后 URL 会同步带上查询参数，刷新页面后状态仍能恢复。
4. 切到第 `2` 页及之后任意一页，确认分页按钮在窄屏下仍可点击，进入帖子详情后返回列表时不会丢失原来的分类 / 排序 / 分页状态。
5. 打开一条普通帖子详情，确认详情页首批仍只承载正文、轻回应墙展示与评论阅读，没有误带入桌面工作台交互。
6. 在帖子详情页检查“返回列表”按钮、浏览 / 轻回应 / 讨论摘要胶囊、评论排序按钮与“加载更多”按钮，确认窄屏下纵向节奏自然，没有横向撑破。
7. 分别检查一条长标题帖子和一条多标签帖子，确认列表卡片与详情页都没有出现文本溢出、标签顶破或关键信息被完全挤没。
8. 再检查一条问答 / 投票 / 抽奖帖子，确认结构化状态标签、正文和只读详情块在公开壳层下仍可阅读，未误暴露编辑 / 提交类操作。

## 5. 预期结果

- `/forum` 与 `/forum/post/:postId` 都以公开内容壳层直达，不绕回桌面入口。
- 公开列表在手机上保持“标题优先、作者和统计辅助、互动信息可扫读”的阅读节奏。
- 列表状态可被 URL 保存，刷新与“详情 -> 返回列表”都不会丢失上下文。
- 公开详情页只承载首批冻结范围：正文、轻回应墙展示、评论阅读。
- 公开壳层没有误暴露发帖、评论提交、投票提交或其他桌面交互动作。

## 6. 可跳过项

- 若本轮只改 changelog、规划或纯文档，可跳过整份清单。
- 若本轮只改桌面 forum 工作台壳层，且未触达 `src/public/`、`PostCard` 的 `publicCompact` 变体或只读复用路径，可跳过本清单。
- 若本轮只改后端字段但未影响公开列表 / 详情展示，可先跑对应 `HttpTest`，人工验收只抽查 1 条列表和 1 条详情。

## 7. 结论记录格式

```md
- 验收日期：2026-04-09
- 验收人：<name>
- 验收范围：forum 公开移动入口
- 执行入口：/forum、/forum/post/:postId
- 结果：通过 / 阻塞
- 备注：<如有问题，记录帖子类型、视口尺寸和复现步骤>
```
