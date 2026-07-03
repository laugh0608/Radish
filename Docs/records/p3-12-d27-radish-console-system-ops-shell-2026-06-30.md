# P3-12-D27 radish.console 系统工具与运维外壳收口记录

> 日期：2026-06-30（Asia/Shanghai）
>
> 范围：`Frontend/radish.console/src/pages/SystemTools/HangfirePage.tsx`、`Frontend/radish.console/src/pages/SystemTools/HangfirePage.css`、`Frontend/radish.console/src/router/routerComponents.tsx`、`Frontend/radish.console/src/router/index.tsx`、`Frontend/radish.console/src/pages/SystemConfig/SystemConfigList.tsx`、`Frontend/radish.console/src/pages/SystemConfig/SystemConfigList.css`。

## 目标

承接 D26 后的系统工具 / 运维外壳检查，确认 `P13` 当前真实代码边界：`SystemConfigList` 继续作为内部系统设置代表页，`/hangfire` 继续作为受 Console 权限保护的外部 Hangfire Dashboard 入口。

本批只收口 `/hangfire` 外层页面语义和样式，不把它扩展为项目内任务调度平台。

## 代码变更

- 新增 `pages/SystemTools/HangfirePage`，将 `/hangfire` 从路由临时组件迁出为正式页面模块。
- `/hangfire` 外层接入 `ConsolePageHeader`、`ConsoleStatusChip`、`ConsoleMetricGrid` 和 `ConsoleMetricCard`，与 D14 后 Console 语义页头、指标和状态组件保持一致。
- 新增 `HangfirePage.css`，把原 iframe 容器的 inline 样式、边框、半径和高度规则迁入 CSS，并复用 Console 语义 token。
- 保留 `getApiBaseUrl() + "/hangfire"` iframe 地址、`console.hangfire.view` 路由权限、`/hangfire` 路由守卫和侧栏元数据不变。
- 增加“打开新窗口”动作，仍指向同一个 Hangfire Dashboard，便于外部面板在 iframe 受限时独立查看。

## 边界判断

- `SystemConfigList` 已在 D14 / D21 完成语义页头、指标、筛选工具条和移动样式收口，本轮不继续改系统设置 API、表单、上传、低风险编辑、历史审计或权限逻辑。
- `/hangfire` 当前没有项目内任务队列、失败重试、运行审计或调度状态 API / 类型模型；因此不新增前端任务列表、重试按钮、审计面板、模拟数据或后端占位接口。
- 订单权益重试仍属于 `OrderList` 的商业履约动作，不并入本批 `P13` 运维任务平台。

## 停止线

- 不改变 Hangfire Dashboard 的宿主地址、鉴权方式或 iframe 加载策略。
- 不新增任务调度、失败重试、运行审计、任务详情、任务搜索或告警订阅等内部运维能力。
- 不新增 API mock、兜底数据或前端本地状态来模拟任务平台。
- 不执行真实 Gateway PC / mobile smoke；本批属于受保护外壳和静态样式收口，真实页面复核留到阶段验收或用户明确要求时执行。

## 验证

- `npm run type-check --workspace=radish.console`
- `npm run build --workspace=radish.console`
- `npm run check:repo-hygiene:changed`
- `node Scripts/check-repo-hygiene.mjs Docs/records/p3-12-d27-radish-console-system-ops-shell-2026-06-30.md`
- `git diff --check`
- `rg -n "style=|#[0-9a-fA-F]{3,8}|rgba\\(|rgb\\(" Frontend/radish.console/src/pages/SystemTools`：无命中

补充说明：`routerComponents.tsx` 仍保留登录校验 / 无 Console 权限 / 路由加载状态的历史 inline 样式；本批只收口 `/hangfire` 运维外壳，不把认证状态页纳入 D27 范围。

## 下一步

优先整理 `P3-12-D14-D27` Console 视觉实现的阶段静态收口和剩余风险；如果后续要把运维任务做成项目内页面，应先补任务队列、失败重试、运行审计的数据来源、API 契约、权限动作和留痕策略设计，再进入代码实现。
