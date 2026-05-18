# WebOS / PC 工作台 P0/P1 筛查记录（2026-05-18）

## 基本信息

- 日期：2026-05-18（Asia/Shanghai）
- 范围：WebOS / PC 工作台成片工作流阻断级缺口筛查
- 目标：只筛会阻断登录后工作台、窗口复用、通知回流、最近使用、论坛 / 文档 / 商城主路径的 `P0/P1`
- 不做：不扫低收益按钮、文案、样式、微交互或完整 E2E 平台

## 筛查范围

| 范围 | 关注点 | 结论 |
| --- | --- | --- |
| 应用注册与权限 | `AppRegistry`、`appAccess`、未登录拦截、外部 Console / Scalar 入口 | 未发现 P0/P1 |
| 窗口打开与复用 | `windowStore.openApp / openOrReuseApp`、最小化恢复、窗口参数更新、几何记忆 | 未发现 P0/P1 |
| 桌面继续使用 | `DesktopResumePanel`、`desktopRecentApps`、浏览历史与我的轻回应回流 | 未发现 P0/P1 |
| forum 回流 | 通知 `extData`、`postPublicId` 优先、旧 long fallback、评论定位参数 | 未发现 P0/P1 |
| docs / wiki 回流 | `documentId / slug` 窗口参数、旧 `/wiki/doc` 与 `/docs` 路径兼容 | 未发现 P0/P1 |
| shop 回流 | `productId` 窗口参数、商品详情打开、订单 / 背包登录态边界 | 未发现 P0/P1 |
| 通知中心 | 已读 / 删除 ID 解析、forum / chat / profile / shop 跳转分流 | 未发现 P0/P1 |

## 代码观察

- `Frontend/radish.client/src/desktop/AppRegistry.tsx`：工作台应用注册仍按公开 / 登录态 / 管理入口分层；`chat / profile / radish-pit / notification / experience-detail` 继续要求 `User`，`console / scalar` 保持外部入口。
- `Frontend/radish.client/src/stores/windowStore.ts`：`openApp` 会先做权限检查，外部入口新标签打开；`openOrReuseApp` 会复用同类窗口并追加 `__navigationKey`，可触发 forum/chat 等窗口内导航。
- `Frontend/radish.client/src/utils/workspaceNavigation.ts`：浏览历史优先解析 forum routePath，shop 使用 `productId`，docs/wiki 支持 `documentId` 与 `slug`；旧 long 可作为兼容打开，不作为普通可读文案。
- `Frontend/radish.client/src/apps/notification/NotificationApp.tsx`：通知点击优先解析 chat / forum 结构化 `extData`，forum 继续走 `postPublicId` 优先，缺失时回退业务 ID；用户、订单与普通 reply/mention/like 有明确降级路径。
- `Frontend/radish.client/src/apps/forum/ForumApp.tsx`：窗口参数通过 `parseForumWindowParams` 解析，支持 `postPublicId / postId / commentId / __navigationKey`，打开详情后再按真实 `voId` 做评论定位。
- `Frontend/radish.client/src/apps/shop/ShopApp.tsx`：窗口参数仅接受安全正整数 `productId`，可直接进入商品详情；订单、背包、购买仍跟随登录态。
- `Frontend/radish.client/src/apps/wiki/WikiApp.tsx`：窗口参数支持 `documentId / slug`，首屏和后续参数变更都有加载路径。

## 验证

```bash
npm run validate:baseline:quick
```

- 结果：通过。
- 覆盖：前端 TypeScript、`radish.client` 最小 node 测试、Console 权限扫描、Repo Quality contract、Identity impact self-test、Identity claims 扫描。
- 其中与本轮筛查直接相关的测试已覆盖 `forumNavigation`、`workspaceNavigation`、`desktopRecentApps`、`windowGeometry`、公开路由状态和 public head / structured data。

## 结论

- 本轮未发现新的 WebOS / PC 工作台 `P0/P1` 阻断项。
- 不需要切出当前批次修复小闭环。
- 后续只在真实使用中出现工作台打开失败、通知无法回流、窗口参数丢失、购买 / 订单 / 背包主路径中断或权限边界错误时再单独处理。
- 不启动完整 E2E、运营平台、SSR / SSG 或全量 `PublicId` 迁移。
