# 2026-06-10 收工回顾与明日事项

## 今日提交回顾

`git log --since="2026-06-10 00:00 +0800"` 在本记录提交前回顾到今日 5 个已提交变更；本次收工提交将补齐 B6 会话治理、视图基线和文档留痕。

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `d63a8367 fix(notification): 修复全部已读未读数残留` | 通知 / 状态一致性 | 修复通知全部已读后未读数和缓存状态残留，避免已读动作后继续显示旧未读提示。 |
| `4eb1b642 docs(guide): 增加页面联调视图规则` | 页面真实联调规则 | 新增页面真实联调与浏览器 smoke 规则，明确真实页面验证默认走 Gateway 并覆盖 PC 与移动端视图。 |
| `d67f3cb3 feat(circle): 落地登录态个人圈子入口` | P3-10-B2 / Web | `/circle` 登录态个人圈子入口落地，首批承接关注动态、我的关注和我的粉丝，并对齐 `/discover`、`/forum`、`/circle` 职责边界。 |
| `a59f14af fix(forum): 修复评论实时高亮一致性` | P3-10-B4 / B5 | 修复评论实时高亮变化后的前端一致性问题，继续保持并列神评 / 沙发与稳定窗口口径。 |
| `b381191c docs: 更新称呼` | 协作规则 | 更新 AGENTS 协作称呼规则，后续对话开始或结束总结称呼用户为 `萝卜SAMA`。 |
| 本次收工提交 | P3-10-B6 / 文档 | 补 Auth refresh idle 校验、前端活跃记录、退出登录 UX、Hub 恢复、Console refresh 前置等待、B6 文档和 1080p+ 页面联调视图基线。 |

## 文档同步复核

- 已同步规划入口：[当前进行中](/planning/current) 已记录 B4/B5 人工联调通过、B2 完成、B6 首批代码完成，以及明日 B6 真实联调和后续 `P3-10-B7` 入口。
- 已同步专题规划：[P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture) 已记录 B6 运行时口径、明日联调顺序和 B6 通过后的 Web UI 改造方向。
- 已新增运行时说明：[Token 不活跃过期治理](/guide/auth-idle-session) 记录 `IdleSession` 配置、refresh 参数、前端行为和验证入口。
- 已同步浏览器规则：[页面真实联调与浏览器 Smoke 规则](/guide/browser-smoke) 已把 PC 基线调整为 `1920x1080`，移动端调整为高 DPR 设备口径。
- 已同步开发日志：[2026 年 6 月开发日志](/changelog/2026-06) 与 [2026 年 6 月第 2 周开发日志](/changelog/2026-06/week2) 已补 2026-06-10 日结。
- 已同步文档入口：[文档目录](/README) 与 [文档首页](/index) 已加入 Token 不活跃过期治理入口。
- 已复核无需跟随更新范围：今天没有新增视觉 token、Pencil 设计源、数据库结构迁移、Console 权限模型、公开 head 策略或正式发布部署流程；相关视觉规范、设计源文件、数据库版本化 SQL、Console 权限说明、公开 SEO 说明和发布记录无需跟随更新。

## 今日验证

- 后端：
  - `dotnet test Radish.Api.Tests`
- 前端：
  - `npm run test --workspace=radish.client`
  - `npm run type-check --workspace=@radish/http`
  - `npm run type-check --workspace=radish.console`
  - `npm run build --workspace=radish.client`
  - `npm run build --workspace=radish.console`
- 仓库级：
  - `npm run validate:identity`
  - `npm run check:repo-hygiene:changed`
  - `git diff --check`

说明：B6 页面真实联调放到 2026-06-11。本机 `https://localhost:5000` 与 `http://localhost:3000` 未运行，且 AI 协作规则不直接启动 `dotnet run` 或 `npm run dev`。

## 明日事项

1. 先读取 [当前进行中](/planning/current)、[P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture)、[Token 不活跃过期治理](/guide/auth-idle-session)、[页面真实联调与浏览器 Smoke 规则](/guide/browser-smoke) 和本记录。
2. 第一顺位做 `P3-10-B6` 页面真实联调：用户启动 Gateway / 前端服务后，使用 Gateway 覆盖 PC `1920x1080` 与移动高 DPR 视图，验证登录成功活动记录、refresh 请求参数、idle 过期退出、页面前台恢复和 Console 登录回跳。
3. 同步覆盖 Hub 场景：通知 / 聊天授权 Hub 在 token idle 过期后停止，评论详情页断开后能以匿名订阅恢复，重新登录后能恢复登录态互动。
4. 联调暴露问题时，按 Auth refresh 判定、前端活动记录、退出登录 UX、Hub token factory / 重连和页面恢复入口成组修复，并执行对应精准验证。
5. B6 联调通过且没有新的阻断后，进入 `P3-10-B7 Web UI 改造与去 WebOS 化迁移图`：先审 `/discover`、`/forum` 详情、`/u/usr_...`、`/circle` 和 Console 高频入口的页面层级、设计边界与验证矩阵，再选择同一组高价值入口推进代码。
6. 保留 `P3-10-B1 / B2 / B3 / B4 / B5` 回归入口；不把 P3-8-D 购买 / 订单 / 背包、Console 低频页面筛查、Flutter 完整能力套件、完整推荐系统、完整关注流、完整联邦社交或 WebOS `/desktop` 新功能承载作为默认主线。
