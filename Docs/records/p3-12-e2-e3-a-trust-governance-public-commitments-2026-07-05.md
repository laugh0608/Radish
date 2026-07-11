# P3-12-E2/E3-A 信任治理与公开承诺首批实现记录

> 日期：2026-07-05（Asia/Shanghai）
>
> 关联专题：[P3-12-E 正式产品成熟度与质量硬化](/planning/p3-12-product-maturity-quality-hardening)
>
> 上游审计：[P3-12-E1 正式产品成熟度矩阵与差距审计](/records/p3-12-e1-product-maturity-gap-audit-2026-07-05)

## 目标

本批承接 E1 中进入正式发布前优先处理的三类缺口：

- Console 用户可见假动作和错误恢复诊断缺口。
- Console 内容治理移动视图缺少明确任务顺序。
- 正式用户承诺、社区规则、隐私边界和虚拟资产说明缺少公开入口。

本批不新增后端 API、权限、数据库结构、注册流程、独立帮助中心或真实法务协议平台。

## 实现范围

### Console 错误恢复与治理任务边界

- `Frontend/radish.console/src/components/NotFound/NotFound.tsx`
  - 404 页“搜索”动作改为打开既有 `GlobalSearch`，移除仅展示不执行的占位逻辑。
  - 404 文案补充权限入口调整和页面移除场景，避免用户只得到“页面不存在”。
- `Frontend/radish.console/src/components/ErrorBoundary/ErrorBoundary.tsx`
  - 页面异常生成 `CONSOLE-*` 诊断编号。
  - 日志写入当前路由、错误对象和组件堆栈。
  - UI 提供复制诊断信息、重试、刷新和返回 `/console/` 首页动作。
- `Frontend/radish.console/src/pages/Moderation/ModerationPage.tsx`
  - 内容治理页新增“移动视图处理顺序”，将手机视图定位为低风险核对、单项审核、日志复核。
  - 高风险批量策略和复杂权限调整继续桌面优先，不在本批假装完整移动后台。
- `Frontend/radish.console/src/pages/Moderation/moderationPageHelpers.ts`
  - `Unsupported` 回看状态调整为“未接入回看”，并给出基于快照、举报原因和用户治理记录复核的说明。

### 公开用户承诺入口

- `Frontend/radish.client/src/public/legal/`
  - 新增 `/legal` 公开用户承诺页，覆盖社区内容规则、公开 / 私域 / Console 可见边界、账号安全、虚拟商品和权益边界、未成年人 / 敏感内容、反馈诊断信息。
- `Frontend/radish.client/src/public/legalRouteState.ts`
  - 新增 `/legal` 公共路由状态。
- `Frontend/radish.client/src/public/PublicEntry.tsx`
  - 将 `/legal` 接入公共壳层。
- `Frontend/radish.client/src/public/publicHead.ts`
  - 为 `/legal` 提供正式 title、description、canonical 和 share URL。
- `Frontend/radish.client/src/public/components/PublicShellHeader.tsx`
- `Frontend/radish.client/src/components/web-shell/WebShellHeader.tsx`
  - 公共导航与移动公共导航新增“规则”入口。
- `Docs/guide/user-commitments.md`
  - 新增用户承诺与公开边界说明，作为 `/legal` 页面长期维护口径。

### 测试覆盖

- `Frontend/radish.client/tests/entryRoute.test.ts`
  - 覆盖 `/legal` 与 `/legal/` 属于公共内容路由。
- `Frontend/radish.client/tests/realUsagePathContracts.test.ts`
  - 覆盖 `/legal` public head、canonical 和 share URL。
- `Frontend/radish.client/tests/publicSeoStatic.test.ts`
  - 覆盖 `/legal` 公共入口、公共导航和 Web 壳层导航契约。

## 发布前判断

| 项目 | 本批结论 | 不阻断理由 | 后续批次 |
| --- | --- | --- | --- |
| Console 404 假动作 | 已处理 | 搜索按钮已打开真实全局搜索；不再保留用户可点击但无效的动作。 | E5 继续补错误页运行态复核。 |
| Console ErrorBoundary 诊断 | 已处理首批 | 用户可复制诊断编号和路径；日志可关联路由、时间和组件堆栈。尚未接入远端监控，不阻断本地正式候选。 | E5 / E6 评估远端可观测性与阶段 smoke。 |
| Console 内容治理移动视图 | 发布前建议已推进 | 手机视图已有明确低风险处理顺序；完整移动后台、批量治理和复杂权限调整保持桌面优先，有清楚边界。 | E2 继续按真实验收补移动表格、抽屉和队列体验。 |
| 用户承诺与公开边界 | 已满足首批 | `/legal` 已成为公开可访问入口，并纳入导航、head 和文档维护口径。完整法务协议、隐私政策和工单平台仍需后续专题，不阻断当前候选前继续开发。 | E4 / E6 对齐正式法务材料和发布验收。 |

## 验证

已执行：

- `npm run test --workspace=radish.client`
- `npm run build --workspace=radish.console`
- `npm run build --workspace=radish.client`
- `npm run check:repo-hygiene:changed`
- `git diff --check`

未执行：

- Gateway PC / mobile 真实页面 smoke。本轮用户未明确说明前后端已经启动，按当前协作规则不沿用历史启动状态。

## 后续顺位

1. `P3-12-E3-B` Workbench 社区活动中心补强：把继续处理、被回复、被提及、关注动态、创作草稿、订单 / 权益变化、宠物 / 经验反馈拆成真实行动队列。
2. `P3-12-E3-C` 通知行动队列补强：按帖子、评论、回答、聊天、关注、治理结果、订单 / 权益变化分组，并验证可跳回具体对象。
3. `P3-12-E4` 隐私、反骚扰、数据生命周期和写入可靠性复核。
4. `P3-12-E5/E6` 错误恢复、可观测性、移动运行态和正式发布候选进入判断。
