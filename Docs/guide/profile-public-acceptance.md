# 个人公开页首批人工验收清单

> 适用范围：`Phase 2-2` 下个人公开页首批与公开内容壳层中的 `/u/:id` 直达入口。
>
> 关联文档：
>
> - [当前进行中](/planning/current)
> - [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [前端设计文档](/frontend/design)
> - [论坛应用功能说明](/features/forum-features)
> - [人工验收模板](/guide/manual-acceptance-template)

## 1. 适用改动范围

- `Frontend/radish.client/src/public/` 下公开个人页路由、状态同步或内容展示逻辑改动后
- `/u/:id` 的移动阅读布局、分页、只读边界或回跳链路改动后
- 公开 forum 中作者点击、公开个人页到 forum 帖子回跳、登录态识别或关注按钮显示逻辑改动后

## 2. 前置条件

- 已完成前端最小自动化验证：
  - `npm run type-check --workspace=radish.client`
  - `npm run build --workspace=radish.client`
- 已启动本地前端与相关宿主，能在浏览器直接访问：
  - `/u/:id`
  - `/forum`
  - `/forum/post/:postId`
  - `/leaderboard`
- 准备至少两类账号与数据：
  - 一个普通登录用户
  - 一个非当前用户的公开主页样例
- 准备至少一名同时满足以下条件的样例用户：
  - 有公开帖子
  - 有公开评论
  - 有头像或显示名中的至少一项
- 浏览器优先覆盖 `390 x 844` 与 `430 x 932` 两档窄屏视口

## 3. 联调资产

- 公开壳层入口：`Frontend/radish.client/src/public/PublicEntry.tsx`
- 公开个人页路由：`Frontend/radish.client/src/public/profileRouteState.ts`
- 公开个人页页面：`Frontend/radish.client/src/public/profile/PublicProfileApp.tsx`
- forum 公开入口：`Frontend/radish.client/src/public/forum/PublicForumApp.tsx`
- 公开资料与统计接口：`Frontend/radish.client/src/api/user.ts`
- 关注状态接口：`Frontend/radish.client/src/api/userFollow.ts`

说明：

- 本清单以公开壳层 UI / 路由 / 登录态人工验收为主，不要求额外新增 `HttpTest`
- 若本轮同时修改关系链接口或个人主页桌面 App，再并行补：
  - `Radish.Api.Community.http`
  - `Radish.Api.User.Profile.http`

## 4. 最小人工验收顺序

1. 直接打开 `/u/:id`，确认页面进入公开内容壳层，而不是先回到桌面 `Shell`。
2. 在窄屏下检查首屏，确认头像、昵称、显示名、加入时间、只读提示和统计块没有重叠、截断或横向撑破。
3. 切换“帖子 / 评论”页签并翻到第 `2` 页，确认 URL 会同步回写 `tab / page`，刷新页面后状态仍能恢复。
4. 直接打开一个越界页码，例如 `/u/:id?tab=posts&page=999` 或 `/u/:id?tab=comments&page=999`，确认页面会 replace 收口到最后一页，而不是停留在空页 URL。
5. 从帖子页签点击一条帖子，确认能跳到 `/forum/post/:postId`；再点击帖子详情页返回按钮，确认会优先回到原 `/u/:id` 页签与分页上下文，而不是退回 forum 默认列表。
6. 从评论页签点击“打开帖子”，确认能稳定跳到对应帖子详情，且不会因为大整数对象 ID 被当作前端 `number` 而打开失败。
7. 打开 `/forum` 或 `/forum/post/:postId`，点击作者头像 / 名称，确认能进入 `/u/:id`，而不是只能打开桌面 `profile` 窗口。
8. 在未登录状态打开 `/u/:id`，确认页面只展示公开资料、公开帖子和公开评论，没有误暴露编辑资料、浏览记录、附件或其他桌面治理能力。
9. 在已登录状态打开“他人”的 `/u/:id`，确认关注按钮可见且状态正确；再打开“自己”的 `/u/:id`，确认不会误显示关注按钮，也不会把自己识别成陌生用户。
10. 从 `/leaderboard` 点击任一用户榜单项进入 `/u/:id`，再执行返回，确认会优先回到原榜单与来源上下文，而不是异常退回桌面或 forum 默认列表。
11. 从 `/u/:id` 顶部点击“社区发现”，确认能稳定回到 `/discover`，且不会异常跳桌面或丢失公开壳层样式。

## 5. 预期结果

- `/u/:id` 以公开内容壳层直达，不绕回桌面入口。
- 公开个人页在手机上保持“资料优先、内容次之、回跳清晰”的阅读节奏。
- 页签和分页状态可被 URL 保存，刷新后不会丢失上下文；越界页码会自动收口回 canonical URL。
- 公开 forum 作者入口与公开个人页之间的跳转稳定，不依赖桌面窗口系统。
- 从公开个人页进入帖子详情后，返回动作会优先回到原 `/u/:id` 页签与分页上下文，而不是退回 forum 默认列表。
- 从公开榜单进入 `/u/:id` 时，返回动作会优先回到原榜单与来源上下文，而不是丢失到桌面入口或其他专题默认页。
- 公开个人页顶部存在统一的“社区发现”轻入口，可把用户重新收口回 `/discover`。
- 公开个人页继续保持只读边界，不误带入编辑资料、浏览记录、附件管理或完整关系链治理。
- 已登录时的本人识别与关注按钮状态稳定，直接打开 `/u/:id` 也不会因为认证初始化缺失而判断错误。

## 6. 可跳过项

- 若本轮只改 changelog、规划或纯文档，可跳过整份清单。
- 若本轮只改桌面 `profile` App，且未触达 `src/public/`、公开作者跳转或 `/u/:id` 路由，可跳过本清单。
- 若本轮只改公开个人页文案或纯样式，且未影响状态同步、登录态识别或跳转链路，可只抽查 1 条帖子页签和 1 条评论页签。

## 7. 结论记录格式

```md
- 验收日期：2026-04-12
- 验收人：<name>
- 验收范围：个人公开页首批
- 执行入口：/u/:id、/forum、/forum/post/:postId、/leaderboard
- 结果：通过 / 阻塞
- 备注：<如有问题，记录视口尺寸、账号状态与复现步骤>
```
