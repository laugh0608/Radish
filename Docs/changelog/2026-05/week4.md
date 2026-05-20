# 2026 年 5 月第 4 周开发日志

> 时间范围：2026-05-18 至 2026-05-24（Asia/Shanghai）

## 2026-05-18

- `P3-6-A` 本地 Gateway 公开增长观察首轮收口：公开 head smoke 已覆盖 robots、sitemap index、`static / forum / docs / shop` 分片和 forum / docs / shop 三类详情首包 head。
- `Scripts/check-public-head-smoke.mjs` 已纳入 sitemap 分片 `<loc>` origin 自动检查，减少部署后人工临时抽查。
- `P3-6-B` 公开增长 smoke 失败诊断增强已收口：失败时输出请求 URL、状态码、content-type、响应 body 前段、疑似 SPA shell 判断、失败阶段和关键断言；self-test 与本地 Gateway smoke 均已通过。
- `P3-6-C` 部署观察与分流记录入口已补齐：新增公开增长观察记录模板，用于按 local / testing / release 前生产 facts 分流公开 head smoke、动态 sitemap、head snapshot、公开域名配置、分享预览和搜索抓取反馈。
- `P3-6-C` 首份本地观察记录已补齐：本地 Gateway public head smoke 覆盖 robots、sitemap index、`static / forum / docs / shop` 分片和 forum / docs / shop 三类详情并通过；后续优先补 testing URL 观察，生产域名验证改为 release 前置项。
- WebOS / PC 工作台成片工作流阻断级缺口筛查已完成：应用注册、窗口复用、继续使用、通知回流、forum / docs / shop 主路径未发现新的 `P0/P1`，轻量基线验证通过。
- 观察中发现本地 SQLite + Hangfire 后台任务并发读可能触发 reader closed 异常；已在仓储 SQLite fallback 读路径按连接串行化处理，并完成仓储项目构建与重启后初步观察。
- 下一步继续按 `P3-6` 真实使用观察口径分流高信号问题；未出现新的 `P0/P1` 前，不启动运营平台、完整可观测性平台或 SSR / SSG。
- 已将 Console 后续 UI 一致性治理写入未来规划：后续回拉 Console 扩展时优先复用 `@radish/ui` 组件、交互反馈与主题 token，避免后台视觉继续分叉。
- Console 后续 UI 一致性评估已补记录：当前后台已部分复用 `@radish/ui`，但页面壳、局部 CSS、直接 `antd` 引入和硬编码色值仍需在后续新增 / 改动页面时小范围收敛；当前不启动整站视觉重构。
- Console 用户列表一处疑似乱码错误提示已恢复为“获取用户列表失败”，并通过 `radish.console` 类型检查与 changed-only 文本卫生检查。
- 收工前已补 [2026-05-18 收工回顾与明日事项](/records/daily-handoff-2026-05-18)：今日提交已复核到公开 head smoke 手册、`P3-6` 规划、阶段专题、记录索引和开发日志；明日优先补 testing URL 公开增长观察，若 testing URL 暂不可用则只做 Console token bridge 小方案评审。

## 2026-05-19

- 部署态 Compose 与镜像轨道继续收敛：测试 / 生产默认使用统一 `Deploy/docker-compose.yaml`、`RADISH_IMAGE_TRACK` 与 `test-latest` / `release-latest` 浮动别名，固定 tag 仅用于可复现部署。
- PostgreSQL / Redis / Auth 证书持久化默认改为宿主目录，部署说明已强调测试 / 生产环境应提前创建并维护 `DeployData/Postgres`、`DeployData/Redis` 与 `DeployData/AuthCerts`。
- 测试 / 生产首次部署安全口径已收口：`Seed:DeveloperDefaultsEnabled=false` 时不创建开发管理员账号，首次访问根入口由部署人员创建首个管理员。
- 修复首次管理员初始化在 PostgreSQL 旧库上的 `SystemBootstrapState.CompletedUserId / CompletedTime` 非空约束问题，并补 `DbMigrate` 旧库约束修复。
- 修复 PostgreSQL 写入 `timestamp with time zone` 时拒绝 `DateTimeKind.Local` 的问题：SqlSugar AOP 参数层统一把 `DateTime` / `DateTimeOffset` 规范化为 UTC。
- 复核 Redis 当前能力：业务实际主要通过 `ICaching` / `IDistributedCache` 走 Redis 或内存缓存，Redis 已在部署态生效，但尚未系统性承担原子计数、在线状态、Backplane 或分布式锁。
- 新增 [Redis 与缓存治理专题](/planning/redis-cache-governance)，将通知未读原子计数、上传限流、聊天室在线状态、SignalR Backplane、商城 / 萝卜币幂等与并发保护、排行榜 / 热点读模型缓存纳入后置规划。
- 收工前已补 [2026-05-19 收工回顾与明日事项](/records/daily-handoff-2026-05-19)：今日提交已复核到部署指南、架构框架、开发路线图、backlog、Redis 专题和开发日志；配置指南当前已超篇幅硬上限，本次不继续追加。

## 2026-05-20

- `P3-6-C` 生产公开增长观察已补齐：`https://radishx.com` public head smoke 覆盖 robots、sitemap index、`static / forum / docs / shop` 分片和 forum / docs / shop 三类详情首包 head 并通过。
- 新增 [P3-6 公开增长部署观察记录（2026-05-20）](/records/p3-6-public-growth-observation-record-2026-05-20)，并挂入 [记录与验收索引](/records/)。
- `P3-6` 阶段收口结论已同步到 [当前进行中](/planning/current)、[开发路线图](/development-plan) 与 [第三开发阶段专题](/planning/phase-three-real-usage-contract-governance)：本轮未发现新的公开访问、head / sitemap、分享入口或回流 `P0/P1`，不切修复小闭环。
- 真实平台分享预览、Search Console / 爬虫日志、生产访问日志和用户回流断点转入运营维护观察；未出现真实证据前，不启动运营平台、完整可观测性平台、完整 E2E、SSR / SSG、正文预渲染或全量 `PublicId` 迁移。
- Console token bridge 小范围落地：`radish.console` 入口样式、`AdminLayout.css` 与 `adminFeature.css` 已接入局部 `--console-*` token 口径，并删除旧 Vite 模板入口样式污染；当前不启动后台整站视觉重构。
- `P3-7-A` WebOS / PC 工作台复访链路首批小闭环已完成：订单通知携带合法业务 ID 时进入商城订单详情，订单详情可回到商品详情，购买成功后进入订单详情，背包权益可基于 `VoSourceOrderId / VoSourceProductId` 回到来源订单 / 商品。
- 今日同步 [当前进行中](/planning/current) 与 [2026-05-20 收工回顾与明日事项](/records/daily-handoff-2026-05-20)；[商城前端设计说明](/guide/shop-frontend) 当前已超文档篇幅硬上限，后续若要迁入复访链路说明需先拆分该说明书；明日优先做商城复访链路收尾复核，不扩大到运营平台、完整 E2E、SSR / SSG 或全量 `PublicId`。
