# P3-10 阶段真实联调 Smoke 记录（2026-06-17）

## 结论

`P3-10` B1-B10 阶段收束后的真实联调 smoke 已完成，未发现阻断转入 `P3-10-D Web 信息流 / UI 结构整理` 的页面问题。

## 覆盖范围

- 宿主健康：Gateway / Api / Auth `/health`
- 公开 head：`robots.txt`、`sitemap.xml`、sitemap shards、论坛详情、文档详情
- PC 视图：`1920x1080`
- 移动视图：`390x844` CSS viewport

## 页面路径

- `/discover`
- `/forum/post/pst_019ea24e37ae7f75afd6503c88d01d3a`
- `/docs/changelog-2026-05-week1`
- `/leaderboard`
- `/shop`
- `/circle`
- `/console/`

## 验证结论

- `npm run check:host-runtime -- --details` 通过，Gateway / Api / Auth 均返回 200。
- `npm run check:public-head-smoke -- --base-url https://localhost:5000 --path /forum/post/pst_019ea24e37ae7f75afd6503c88d01d3a --path /docs/changelog-2026-05-week1` 通过。
- PC 视图下公开发现、论坛详情、文档详情、榜单、商城、圈子和 Console 授权确认页均可渲染，无横向溢出，浏览器 error 日志未发现页面级错误。
- 移动 `390x844` 视图下同一组页面均可渲染，无横向溢出，浏览器 error 日志未发现页面级错误。
- Console 入口本轮只验证到 OAuth 授权确认页可达；未代替用户点击“同意”授权，因此未进入 Console 深层设置页。

## 限制与说明

- 当前浏览器工具只能稳定设置 CSS viewport，不能设置 DPR；移动结论代表 `390x844` 移动布局宽度，不代表 `390x844 @ DPR 3` 的物理高分屏完整 smoke。
- `/discover`、`/leaderboard`、`/shop` 在当前开发运行态首包返回 Vite SPA shell；公开详情 head smoke 已用论坛详情和文档详情覆盖。
- 本轮未启动服务、未安装依赖。
