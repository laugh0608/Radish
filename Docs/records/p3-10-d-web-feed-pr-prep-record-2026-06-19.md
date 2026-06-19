# P3-10-D Web 信息流 / UI 结构整理 PR 准备记录（2026-06-19）

> 本记录承接 [P3-10-D 合并前验证记录](/records/p3-10-d-pre-merge-validation-record-2026-06-19)，用于整理 `dev -> master` PR 前的批次范围、验证结论和剩余限制。

## 批次结论

`P3-10-D Web 信息流 / UI 结构整理` 已完成公开页结构整理、入口 / 链接 / 来源语义四批治理、合并前自动化验证和 Gateway PC / 移动真实页面复核。当前未发现需要阻断 `P3-10-D` 收束的问题，本批具备进入 `dev -> master` PR 的工程条件。

后续不再默认追加第五批链接语义扫尾；若 PR 检查、真实 smoke 或人工复核命中新阻断，再按明确影响面定向回修。

## 本批提交

| 提交 | 范围 |
| --- | --- |
| `3bc40941 fix(public): 补齐公开个人页链接语义` | 公开个人页返回、tab、分页和状态卡公开链接语义 |
| `cb1bd359 docs(planning): 记录 P3-10-D Gateway 补验` | Gateway PC / 移动补验结论 |
| `60fa595b fix(public): 补齐公开壳层发现链接语义` | 共享公开头部发现动作与 `/me` 最近访问来源转交 |
| `4aa4a3e8 docs(planning): 收束 P3-10-D 阶段验证口径` | 阶段收束与合并前验证口径 |
| `fd35fe93 fix(public): 收敛公开路由 ID 字符串契约` | 公开用户路由 ID 字符串契约与合并前验证记录 |

## 变更范围

| 项目 | 结果 |
| --- | --- |
| 公开个人页 | 返回、内容 tab、分页、状态卡动作补齐真实公开 `href`，普通点击保留壳层导航 |
| 公开帖子详情 | 未找到 / 错误状态卡返回补齐来源 `href` |
| 公开壳层头部 | “发现”动作补齐真实 `/discover` 链接 |
| `/me` 最近访问 | 只对公开详情页转交“我的状态”来源，避免公开浏览页误用详情返回 |
| 公开 ID 契约 | 公开用户路由 helper 移除 `string | number` fallback，收敛为字符串契约 |
| 文档记录 | 补 P3-10-D 合并前验证与 PR 准备记录，更新当前规划和专题结论 |

## 验证结果

执行目录：仓库根目录 `/Users/luobo/Code/Radish`

```bash
npm run validate:ci -- --report
```

结果：通过。

- Repo Hygiene changed-only：通过，本次执行时工作区无未提交文件。
- Frontend changed-only Lint：通过，本次执行时无未提交前端脚本文件。
- Baseline Quick：通过。
  - 前端 TypeScript 类型检查通过：`@radish/http`、`@radish/ui`、`radish.client`、`radish.console`。
  - `radish.client` node 测试：270 passed。
  - Console 权限链路扫描通过。
  - Repo Quality contract 自校验通过。
  - 身份语义影响面判定自校验与防回归扫描通过。
- Backend Guard / Identity Guard：本次执行时无未提交变更，按本地 `Repo Quality` 口径跳过。

合并前验证记录中已完成并通过：

- `npm run validate:baseline`
- `npm run validate:identity`
- `npm run validate:baseline:quick`
- `npm run check:host-runtime -- --details`
- `npm run build --workspace=radish.client`
- `npm run check:repo-hygiene:changed`
- `git diff --check`

## 真实页面 Smoke

入口：`https://localhost:5000`

结论见 [P3-10-D 合并前验证记录](/records/p3-10-d-pre-merge-validation-record-2026-06-19)。

已覆盖：

- PC `1920x1080`
- 移动 `390x844` CSS viewport
- `/discover`、公开论坛列表 / 搜索 / 标签 / 问答流、公开帖子详情、公开文档目录 / 搜索 / 详情、公开榜单、公开商城、公开个人页、`/circle` 和 `/me`
- `/discover`、`/shop/products`、`/leaderboard` 的详情进入与来源返回

## 合并判断

当前判断：

- 本批属于 `P3-10-D` 阶段性收束，符合 `dev -> master` PR 触发条件。
- 自动化、身份语义、宿主健康和真实页面复核均已有批次级留痕。
- 未发现需要继续阻断合并的公开 / 私域边界、标题层级、来源返回、分享 URL 或入口动作问题。

建议下一动作：

- 发起 `dev -> master` PR。
- PR 检查若命中新增问题，只回修阻断或清晰一致性问题。
- PR 合并后再按整体规划选择下一条产品增量主线，不继续把 `P3-10-D` 链接语义扫尾作为默认主线。

## 限制与后置项

- in-app Browser 当前只能稳定设置 CSS viewport，不能设置 DPR；移动结论不写成 DPR 3 物理高分屏完整 smoke。
- 浏览器工具同一 tab 连续重载矩阵曾污染旧 React root / console 日志，已通过新 tab 单页复核规避；旧日志不作为页面错误结论。
- 本记录不代表发布 / 部署完成，不创建 tag，不进入 M15 测试 / 生产部署流程。
