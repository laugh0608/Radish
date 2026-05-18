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
