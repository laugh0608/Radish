# P3-10-D 合并前验证记录（2026-06-19）

## 结论

`P3-10-D Web 信息流 / UI 结构整理` 已完成合并前验证准备。本轮自动化、身份语义、宿主健康和 Gateway PC / 移动真实页面复核均已覆盖，未发现需要继续阻断 P3-10-D 收束的页面问题。

本轮验证中发现 `Frontend/radish.client/src/public/publicId.ts` 仍允许公开用户路由 fallback 使用 `string | number`，已收敛为 `string` 契约并补静态断言；重新执行身份语义验证后通过。

## 自动化验证

- `npm run validate:baseline` 通过。
  - `radish.client` node 测试：270 passed。
  - 后端测试：466 passed。
  - 后端构建通过，仅保留既有 XML 注释 warning。
- `npm run check:host-runtime -- --details` 通过，Gateway / Api / Auth `/health` 均返回 200。
- `npm run validate:identity` 首次命中公开用户 LongId `string | number` 类型回退；修复后复跑通过。
  - 运行时 Claim 扫描通过。
  - 协议输出回退风险扫描通过。
  - 外部 LongId 字符串安全扫描通过。
  - 身份语义后端定向测试 15 passed。
- 修复公开 ID helper 后追加验证：
  - `npm run validate:baseline:quick` 通过。
  - `npm run build --workspace=radish.client` 通过，仅保留既有 `forum-detail-comments` chunk size warning。

## 真实页面 Smoke

入口：`https://localhost:5000`

视图：

- PC：`1920x1080`
- 移动：`390x844` CSS viewport

页面覆盖：

- `/discover`
- `/forum`
- `/forum/search`
- `/forum/tag/u6d4b-u8bd5`
- `/forum/question`
- `/forum/post/pst_019ea24e37ae7f75afd6503c88d01d3a`
- `/docs`
- `/docs/search`
- `/docs/planning-p3-10-cross-platform-information-architecture`
- `/leaderboard`
- `/shop`
- `/shop/products`
- `/shop/product/100061`
- `/u/usr_019ea76872bf787981ad3e9d3c6a3417`
- `/circle`
- `/me`

页面结论：

- PC 与移动视图下公开发现、公开论坛流、公开帖子详情、公开文档、公开榜单、公开商城和公开个人页均能渲染真实内容，未发现横向溢出。
- `/circle` 与 `/me` 在未登录状态下进入 Radish 统一登录，符合登录态私域入口边界。
- 公开文档详情、公开帖子详情、公开个人页、榜单和商城页面均保持单一主标题层级。

交互覆盖：

- PC：`/discover` 普通点击进入公开帖子详情后，返回 `/discover?section=forum`。
- PC：`/shop/products` 普通点击进入商品详情后，返回 `/shop/products`。
- PC：`/leaderboard` 普通点击进入公开个人页后，返回 `/leaderboard`。
- 移动：`/discover` 滚动后普通点击进入公开帖子详情，再返回 `/discover?section=forum`。
- 移动：`/shop/products` 滚动后普通点击进入商品详情，再返回 `/shop/products`。

## 限制与说明

- 当前 in-app Browser 视口能力只支持 CSS viewport，不支持设置 DPR；因此移动结论代表 `390x844` 移动布局宽度，不写成 `390x844 @ DPR 3` 物理高分屏完整 smoke。
- 浏览器工具在同一 tab 连续重载矩阵时曾污染 React root 与 console 日志，出现旧的 `createRoot` / `removeChild` error；已改用新 tab 单页复核和坐标点击复核，页面内容渲染正常。该旧日志不作为当前页面错误结论。
- 本轮未启动服务、未安装依赖。
