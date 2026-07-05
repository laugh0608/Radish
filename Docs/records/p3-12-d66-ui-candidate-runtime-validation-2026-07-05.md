# P3-12-D66 UI 专题候选前运行态验证补验

> 日期：2026-07-05（Asia/Shanghai）
> 范围：`P3-12-D` UI 专题候选前运行态补验，Gateway public / private / console PC 与 mobile 成组复核
> 前置：用户当轮明确说明前后端已经启动；`P3-12-D65` 已完成启动无关候选前验证

## 背景

`P3-12-D65` 已完成启动无关验证，并修复公开个人页只读边界与公开页静态契约回归。D66 继续执行候选前剩余运行态补验，覆盖宿主健康检查和 Gateway 真实页面复核。

本轮用户要求真实联调优先使用 Browser 或 Chrome 插件。实际执行中先接入内置 Browser，`/workbench` 可正常读取页面状态，但 Browser 在 DOM snapshot 与批量导航时连续超时，无法稳定保留成组结果；随后按用户指定范围切换到 Chrome 插件完成同一 Gateway 页面矩阵。

## 执行范围

### 运行态健康检查

- `npm run check:host-runtime -- --details --report`：通过。
- Gateway `https://localhost:5000/health`：200。
- Api `http://localhost:5100/health`：200。
- Auth `http://localhost:5200/health`：200。

### Gateway PC 视图

Chrome 视口：`1920x1080 @ DPR 1`。

- Public Web：`/discover`、`/forum`、`/docs`、`/shop/products`、`/leaderboard`、`/u/usr_019ef99117377efb85389bfe6d9d55a5`。
- Private / Author：`/workbench`、`/me`、`/me/content`、`/me/history`、`/me/assets`、`/me/assets/transactions`、`/shop/orders`、`/shop/inventory`、`/notifications`、`/messages`、`/circle`、`/pet`、`/forum/compose`、`/docs/mine`、`/docs/compose`。
- Console：`/console/`、`/console/moderation`、`/console/experience`、`/console/products`、`/console/orders`、`/console/documents`、`/console/users`、`/console/roles`、`/console/roles/10001/permissions`。

### Gateway mobile 视图

Chrome 设备指标：`390x844 @ DPR 3`。

- Public Web：`/discover`、`/forum`、`/docs`、`/shop/products`、`/leaderboard`、公开个人页。
- Private / Author：`/workbench`、`/me/content`、`/me/history`、`/me/assets`、`/shop/orders`、`/shop/inventory`、`/notifications`、`/messages`、`/circle`、`/pet`、`/forum/compose`、`/docs/mine`。
- Console：`/console/`、`/console/moderation`、`/console/experience`、`/console/products`、`/console/orders`、`/console/documents`、`/console/users`、`/console/roles/10001/permissions`。

## 结果

- 本地管理员登录回流成功；Console `radish-console` OIDC 授权确认后回到 `/console/`。
- Public / Private / Console 页面在 PC 与 mobile 视图下均未出现全局横向溢出。
- 关键页面未停留在登录页、长加载态、无权限页或运行时错误页。
- Chrome 页面 console error / warning 读取结果为空。
- Console 在 PC / mobile 下存在 AntD 表格内部横向宽度，这是表格局部滚动容器内的预期表现；页面 `scrollWidth` 与 `clientWidth` 一致，不构成全局横向溢出。
- `/forum/compose` 自动关键字检查命中“分类加载失败时阻止发布”的说明文案；人工复核正文和移动截图后确认页面可用，不是真实加载失败。
- `/forum/compose` mobile 的设置面板存在离屏抽屉元素，但页面无横向滚动，主编辑区未被遮挡，属于既有抽屉布局表现。

## 保持不变

- 不新增 API、权限键、数据库结构、路由语义或保存 / 提交载荷。
- 不执行发帖、购买、治理保存、权限保存或其他写入动作。
- 不进入独立移动 Console、内部 Jobs 平台或新的治理 API 实现。
- 不创建发布 tag，不进入 M15 测试或生产部署流程。

## 后续

- D61-D66 已完成 Public Web、Private / Author、Console 当前发布前范围的首批实现、启动无关验证和运行态补验。
- 下一顺位建议进入 `P3-12-D67 UI 专题退出判断与 P3-12-E 进入评审`，先整理是否满足退出 `P3-12-D` 的证据和剩余后置缺口，再决定是否进入正式版发布候选准备。
