# 2026-05-22 收工回顾与明日事项

## 今日提交回顾

| 提交 | 类型 | 结论 |
| --- | --- | --- |
| `d63aee8a refactor(client): 拆分 WikiApp 文档工作台热区` | WebOS / Wiki 工作台 | 抽出 `WikiSidebar` 与 `wikiApp.helpers.ts`，`WikiApp.tsx` 从约 `1759` 行降至 `1419` 行；不改 API、公开 docs 路由、视觉设计或文档业务能力。 |
| `fce193d2 refactor(client): 拆分 ChatApp 聊天工作台热区` | WebOS / Chat 工作台 | 抽出消息列表、频道侧栏、成员面板、输入区状态和 `chatApp.helpers.ts`，`ChatApp.tsx` 从约 `2004` 行降至 `1489` 行；不启动 Chat P2 backlog 功能。 |

## 昨日补记核对

- `2026-05-21` 的提交已补正到 [2026-05-21 收工回顾与明日事项](/records/daily-handoff-2026-05-21)，覆盖壳层路由契约、商城背包来源入口、`P3-7-A` 收口、`P3-7-B` 工作台筛查和 `P3-7-C` 主线重评估。
- 昨日 shell 路由契约变更已落到前端壳层相关说明书；商城背包变更已落到阶段规划、开发日志和定向测试，未继续向已超篇幅的商城前端说明书追加内容。

## 文档同步复核

- `P3-7-C1 / P3-7-C2` 已同步到 [当前进行中](/planning/current)、[开发路线图](/development-plan)、[第三阶段专题](/planning/phase-three-real-usage-contract-governance) 与本周开发日志。
- [当前进行中](/planning/current) 与 [开发路线图](/development-plan) 已把下一主线更新为 `P3-7-C3 后端 Service 热区评估与首批治理候选`。
- `P3-8` 多端功能补全与 UI / Pencil 设计治理仍保持后续重点方向，不作为明日默认主线。
- 本次 Wiki / Chat 拆分为行为等价结构治理，不需要更新公开 docs 业务说明、Chat P2 backlog、API 契约或数据库设计文档。

## 验证

- `npm run type-check --workspace=radish.client`
- `node --test --test-isolation=none ./Frontend/radish.client/tests/wikiApp.helpers.test.ts ./Frontend/radish.client/tests/workspaceNavigation.test.ts`
- `node --test --test-isolation=none ./Frontend/radish.client/tests/chatApp.helpers.test.ts ./Frontend/radish.client/tests/chatNavigation.test.ts ./Frontend/radish.client/tests/desktopRecentApps.test.ts ./Frontend/radish.client/tests/windowGeometry.test.ts`
- `npm run build --workspace=radish.client`
- `npm run check:repo-hygiene:changed`
- `git diff --check`

构建仍保留既有 `app-shop` chunk size warning。

## 明日事项

1. 启动 `P3-7-C3`：先读取 [当前进行中](/planning/current) 与第三阶段专题，再复核 `ExperienceService.cs`、`ContentModerationService.cs` 等已点名后端 Service 热区。
2. 只选一个一天级、行为等价、可验证的拆分候选；不改 API 契约、权限语义、数据库结构或业务规则。
3. 若评估发现必须改变运行时行为，先单独写方案并等待批准，不直接实现。
4. `P3-8` 多端功能补全与 UI / Pencil 设计治理继续后置，明日不默认切入。
