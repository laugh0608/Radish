# 2026-05-25 收工回顾与明日事项

## 今日提交回顾

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `a6ebc9ef docs(planning): 收敛多端路线到纯 Web 与 Flutter` | 路线决策 | 多端投入从 WebOS / Tauri 分发主线收敛为 `纯 Web + Flutter`；`/desktop` 仅作为 WebOS 保留入口，PC/Tauri 后置且不再绑定 WebOS。 |
| `0cdb2ee0 docs(planning): 记录移动 Web 公开视图实测` | 验收矩阵 | 建立 `P3-8-D` 移动 Web 公开视图验收矩阵，并补 `390 x 844` Vite 直连实测，未发现新的 `P0/P1`。 |
| `42fbcdb7 fix(client): 修正公开页工作台入口` | 纯 Web 入口 | 公开页顶部工作台入口从 `/` + `WebOS` 收敛为 `/desktop` + “工作台”，避免根路径切换后语义漂移。 |
| `71c5867c feat(client): 将浏览器根路径切向公开发现页` | 默认入口 | 普通浏览器根路径 `/` 切向 `/discover` 公开分发页；Tauri 当前仍保留 `/desktop`。 |
| `ed0ff17e docs(planning): 补充根路径 Gateway 复核` | 验证留痕 | 补充 Gateway 根路径复核：`https://localhost:5000/` 已进入 `/discover`，`/desktop` 保留。 |
| `e974c0a3 refactor(client): 移除历史 demo 入口` | 入口治理 | 删除早期 `?demo` 认证测试页和旧 `App.tsx / App.css`，拆出独立 `/oidc/callback` 正式回调入口，并补入口解析测试。 |
| `7552ac28 feat(client): 补齐公开个人页分享入口` | 公开分享 | `/u/:id` 补显式复制链接入口；forum / docs / shop / profile 公开分享状态收敛到统一 hook，并修正剪贴板 fallback。 |

## 文档同步复核

- 已同步规划入口：[当前进行中](/planning/current) 和 [开发路线图](/development-plan) 均已记录 `P3-8-D` 当前主线、纯 Web + Flutter 路线、根路径切换、`?demo` 移除和公开个人页分享小闭环。
- 已同步路线说明：[前端多壳层策略](/frontend/shell-strategy)、[前端设计文档](/frontend/design) 与多端路线评估方案均已确认 `/desktop` 只作为 WebOS 保留入口，PC/Tauri 后置且只增强纯 Web。
- 已同步专题规划：[P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance) 已记录移动 Web 矩阵、根路径切换、`?demo` 移除、个人页分享和下一批候选方向。
- 已同步验收记录：[P3-8-D 移动 Web 公开视图验收矩阵（2026-05-25）](/records/p3-8-d-mobile-web-public-view-acceptance-matrix-2026-05-25) 已补 `390 x 844` 实测、Gateway 根路径复核、`/?demo` 行为和 `/u/:id` 分享复核。
- 已同步说明书 / 验收清单：[公开内容 SEO 与分享基线](/frontend/public-seo-sharing)、[个人公开页首批人工验收清单](/records/profile-public-acceptance) 和 [验证基线说明](/guide/validation-baseline) 已补公开个人页复制 canonical 链接口径。
- 已同步开发日志：[2026 年 5 月第 5 周开发日志](/changelog/2026-05/week5) 已记录今日路线收敛、入口治理、验收矩阵和分享入口小闭环；月度与年度日志入口已挂接。
- 本批没有修改后端 API、数据库结构、权限模型或商城 / 订单 / 背包业务契约；相关业务说明书无需跟随改运行时契约。

## 今日验证

- `npm run type-check --workspace=radish.client`
- `npm run test --workspace=radish.client -- --test-name-pattern="entryRoute|tauriBridge"`
- `npm run test --workspace=radish.client -- --test-name-pattern="copyToClipboard|publicRoute|entryRoute"`
- `npm run build --workspace=radish.client`
- `npm run check:repo-hygiene:changed`
- `git diff --check`
- 浏览器实测：`/`、`/?demo`、`/desktop`、`/oidc/callback`、`/leaderboard -> /u/20001`；控制台未见 error。

说明：`radish.client` 构建仍保留既有 `app-shop` chunk size warning；今日未处理该历史拆包项。

## 明日事项

1. 先读取 [当前进行中](/planning/current)、[P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance) 和本记录，确认继续走 `P3-8-D`，不要回到 WebOS 或 Console 微调。
2. 补一次 `430 x 932` 或 Gateway 入口移动公开视图复核，覆盖 `/discover / forum / docs / leaderboard / shop / u/:id`；重点看根路径切换后是否还有页面内入口错误依赖 `/`。
3. 若复核未发现新的 `P0/P1`，进入纯 Web 登录后轻量链路评估，优先购买 / 订单 / 背包浏览器主路径；只选一个一天级小闭环，不启动完整移动商城、完整通知中心或完整创作器。
4. 候选闭环建议先从“公开商城详情到登录后购买 / 订单 / 背包轻入口的边界设计”开始：先确认现有 WebOS 商城能力、公开商城只读边界和纯 Web 目标路径，再决定是否新增轻量页面或只做入口 / 回流契约。
5. 若明日进入实现，默认至少执行 `npm run type-check --workspace=radish.client`、命中路径定向测试、`npm run build --workspace=radish.client`、`npm run check:repo-hygiene:changed` 和 `git diff --check`；涉及后端契约再补 `Radish.Api.Tests` 定向测试。
