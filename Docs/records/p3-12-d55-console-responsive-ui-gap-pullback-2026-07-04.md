# P3-12-D55 Console 响应式后台 UI 差距回拉

## 基本信息

- 日期：`2026-07-04`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 阶段：`P3-12-D` UI 专题
- 性质：Console 响应式后台页面族的代码侧差距回拉
- 设计依据：[Console 治理工作台设计端点](/frontend/console-governance-workbench-design)、[P3-12-D37 UI 设计源差距矩阵记录](/records/p3-12-d37-ui-design-source-gap-matrix-2026-07-01)、[P3-12-D54 Private / Author Pencil 首轮页面对齐复核](/records/p3-12-d54-private-author-pencil-first-alignment-2026-07-04)

## 背景

D53 已完成 Public Web Pencil 到真实页面的首轮对齐，D54 已完成 Private / Author 页面族的 Pencil / Gateway 真实页面对齐。按 D37 矩阵，Console 侧仍保留 `P07 / P08 / P14-P17` 这组移动响应式后台验收参考：它们不是独立移动 Console 应用，也不要求新增内部调度中心或 Jobs 平台，但需要保证现有后台页面在窄屏任务流中不被页头、筛选控件、治理动作或权限矩阵撑宽。

本批按代码侧可确认的收缩边界处理，不读取或修改 `.pen`，不新增业务能力。

## 实现范围

- `ConsolePage` 共享页头、指标和工具条补 `min-width: 0` / `max-width: 100%`，长标题和说明支持换行，移动端页头动作、工具条 meta 和工具条 body 使用整行承载。
- `ConsoleMetricGrid` 改为 `minmax(min(100%, 170px), 1fr)`，避免极窄 Console 内容区中指标卡保留固定下限撑宽。
- `adminFeature` 共享功能页和卡片补收缩边界；移动端表格筛选区改为单列，输入框、选择器、日期、数字和按钮统一在筛选区内收缩到容器宽度。
- 治理工作台内 AntD `Space` 项补收缩边界，避免内容治理 / 经验治理的筛选组在移动 CSS 视口中横向顶出主内容。
- 内容治理筛选控件在移动端统一变为 `100%` 宽，人工治理状态头部和状态卡头部单列化。
- 经验治理查询、筛选、规则卡和横向动作组在移动端补收缩和单列约束。
- 角色权限矩阵面板补收缩边界，权限 API code 支持换行，移动端面板头和 API 映射单列展示。
- 系统设置筛选控件继续按 D45 口径在移动端收缩到容器宽度，避免品牌卡以外的筛选条重新撑宽。

## 保持不变

- 不新增业务 API、权限键、数据库结构、路由语义、登录回流或保存 / 提交载荷。
- 不修改内容治理、经验治理、订单 / 商品、文档治理、角色权限和系统设置的业务状态机。
- 不把 Console 改造成独立移动应用；移动画板仍作为响应式后台验收参考。
- 不回拉完整内部调度中心、内部 Jobs 平台、失败重试平台或运行审计平台。
- 不进入 `P3-12-E`，不创建 tag，不进入 M15 测试或生产部署流程。

## 验证记录

- `npm run build --workspace=radish.console`：通过。
- `npm run check:host-runtime -- --details`：通过，Gateway / Api / Auth 健康端点均返回 `200`。
- Browser 插件：复用应用内浏览器当前 tab，移动 `390x844` CSS 视口覆盖 `/console/roles` 与 `/console/system-config`；两页均确认 `documentOverflowX = 0`、`bodyOverflowX = 0`，角色页 `ConsolePage` 页头与指标区未撑宽，系统设置筛选区按单列 `grid` 收缩。
- Browser 插件：PC `1920x1080` CSS 视口覆盖 `/console/roles`，确认 `documentOverflowX = 0`、`bodyOverflowX = 0`，页头与指标区保持桌面宽屏布局。
- `git diff --check`：通过。
- `npm run check:repo-hygiene:changed`：通过，已检查 `9` 个已变更文本文件。

Browser 插件早先执行复杂多页面布局读取时曾在插件执行层超时并重置连接；按用户要求重新尝试后，改用轻量截图与页面指标读取已成功覆盖上述代表样本。本批仍不写作 Console 全量页面复核通过；若后续准备阶段验收，仍需覆盖 Console 登录、治理、经验、商业、文档、权限和系统设置代表页的 PC / mobile 视图。

## 下一步

继续留在 `P3-12-D`。后续应基于 D36-D55 证据判断是否还需要补 Console 真实 Gateway 成组复核、更多真实数据态样本或页面级 Pencil 对齐；不能把 D55 写成整个 UI 专题退出判断，也不能直接进入 `P3-12-E`。
