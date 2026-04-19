# 公开壳层首轮收口与真实联调复核变更回归记录

- 记录日期：2026-04-16
- 记录人：Codex
- 变更范围：`Phase 2-2` 公开内容壳层下的公开榜单规范路由、`discover` 来源返回口径，以及公开榜单 / 公开个人页 / 公开商城相关人工验收清单收口

### 变更摘要

- 对齐公开榜单默认经验榜单的规范路径口径，明确 `/leaderboard` 为 canonical 路径，兼容直链 `/leaderboard/experience` 会自动收口回规范地址
- 将 `/discover -> /leaderboard -> /u/:id -> 返回` 的真实联调结果补入公开榜单、公开个人页与总基线验收口径
- 将 `/discover -> /shop/product/:productId -> 返回` 的来源返回行为补入公开社区分发页专题清单与高层规划口径
- 同步更新 `planning/current`、`phase-two-community-multiplatform` 与 `frontend/shell-strategy`，避免“已联调通过但主线文档仍未显式覆盖”的口径漂移

### 影响专题

- 公开榜单首批
- 个人公开页首批
- 公开社区分发页首批
- 公开商城浏览首批
- 公开内容壳层总基线与来源返回规则

### 身份语义影响面（按需）

- 命中情况：未命中
- 命中原因：无

### 自动化执行

- `npm run validate:baseline:quick`：未执行
- `npm run validate:baseline`：未执行
- `npm run validate:baseline:host`：未执行
- `npm run validate:ci`：未执行

说明：

- 本轮改动仅涉及公开壳层规划文档、验收清单与回归口径同步，未触达业务代码、接口契约或宿主配置，因此未追加自动化验证

### 专题回归

- [公开榜单首批人工验收清单](/guide/leaderboard-public-acceptance)
- [个人公开页首批人工验收清单](/guide/profile-public-acceptance)
- [公开社区分发页首批人工验收清单](/guide/discover-public-acceptance)
- [验证基线说明](/guide/validation-baseline)

### 人工验收

- 执行情况：已执行
- 摘要：
  - 复核 `/leaderboard` 与 `/leaderboard/experience` 的规范路由行为，确认默认经验榜单页为 `/leaderboard`，兼容直链会自动收口
  - 复核 `/leaderboard`、`/leaderboard/experience`、`/leaderboard/hot-product` 与 `/u/:id` 的公开壳层行为，确认除规范路径口径外其余检查项通过
  - 复核 `/discover -> /leaderboard -> /u/:id -> 返回`，确认返回动作会优先回到原榜单与公开来源上下文
  - 复核 `/discover -> /shop/product/:productId -> 返回` 现有代码与验收口径，确认公开商城详情返回动作按来源优先收口

### 部署复核（按需）

- 执行情况：未执行
- 记录入口：无
- 摘要：
  - 本轮未涉及部署、宿主运行态或外部访问环境复核

### 故障归类 / 环境边界（按需）

- 归类：无
- 说明：本轮未出现自动化失败、环境受限或身份语义相关阻塞

### 结论

- 当前这一批公开壳层的规范路由、来源返回与专题验收口径已完成一轮集中收口
- `/leaderboard` canonical、榜单到公开个人页的回跳，以及 `discover` 下商城详情返回当前都已进入正式文档口径，不再依赖会话口头结论
- 本轮可作为 `Phase 2-2` 公开壳层“真实联调复核与口径统一”的一次批次级记录，当前结论为：可继续推进下一轮公开壳层任务

### 风险 / 后置项

- 当前留痕仍以人工验收与文档同步为主，尚未补批次级自动化报告
- 若后续继续推进公开壳层，优先考虑把下一轮真实联调结果继续沉淀到同类记录，而不是重新回到分散口头结论
