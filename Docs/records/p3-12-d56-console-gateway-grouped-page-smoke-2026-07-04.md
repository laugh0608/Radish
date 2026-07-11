# P3-12-D56 Console Gateway 成组真实页面复核与证据收口

## 基本信息

- 日期：`2026-07-04`
- 主线：`P3-12 Web 完全化与 WebOS 收束`
- 阶段：`P3-12-D` UI 专题
- 性质：Console 代表页 Gateway 真实页面复核、移动端表格固定列问题修复与证据收口
- 前置条件：执行真实 smoke 前，用户已在 `2026-07-04` 明确确认当前前后端已启动；复核通过 Gateway `https://localhost:5000/console/` 和种子管理员账户完成登录回流。
- 设计依据：[Console 治理工作台设计端点](/frontend/console-governance-workbench-design)、[P3-12-D55 Console 响应式后台 UI 差距回拉](/records/p3-12-d55-console-responsive-ui-gap-pullback-2026-07-04)

## 覆盖范围

本批使用 Browser 插件优先通过 Gateway 访问真实 Console 页面，覆盖 PC `1920x1080` 与 mobile `390x844` CSS 视图：

- `/console/roles`
- `/console/roles/10001/permissions`
- `/console/system-config`
- `/console/moderation`
- `/console/experience`
- `/console/orders`
- `/console/products`
- `/console/documents`

重点复核项：

- 全局横向溢出：`documentOverflowX` / `bodyOverflowX`。
- 页头、指标区、工具条和筛选控件的移动端收缩。
- 治理工作台、经验治理工作台和权限矩阵的窄屏布局。
- 表格是否保持局部横向滚动，而不是撑宽页面。
- 角色创建弹窗、订单详情弹窗、商品详情弹窗和文档详情弹窗在移动端是否可打开、可关闭、无全局横向溢出。

## 复核结论

- `roles`、`role permissions`、`system-config`、`moderation`、`experience`、`orders`、`products`、`documents` 在 PC 与 mobile 视图下未发现全局横向溢出。
- `system-config`、`moderation`、`orders`、`products`、`documents` 等宽表保持 `.ant-table-content { overflow-x: auto }` 局部滚动；移动端表格本身允许横向滚动，不再把页面整体撑宽。
- 角色页新增角色弹窗、订单详情弹窗和商品详情弹窗在 mobile `390x844` 下可打开，弹窗宽度收敛在视口内，未引入全局横向溢出。
- 角色权限页移动端权限矩阵已加载，权限项和 API 映射文本按 D55 的收缩边界换行，不顶出页面。
- 内容治理和经验治理工作台在 PC / mobile 视图下保持工作台区块顺序，筛选与动作区未重新撑宽页面。

## 发现与修复

文档治理页 mobile `390x844` 下命中真实 UI 问题：

- `/console/documents` 表格同时固定左侧“文档”列与右侧“操作”列。
- 在移动端局部横向滚动后，视觉上可见的“详情 / 版本 / 导出”按钮区域会被左侧固定列 sticky 叠层覆盖；命中测试返回 `td.ant-table-cell-fix-start`，真实点击无法打开文档详情。
- 该问题属于表格局部滚动与弹窗可用性问题，不涉及 API、权限、路由、数据库或提交载荷。

本批修复：

- 新增 `Frontend/radish.console/src/pages/Documents/DocumentGovernancePage.css`。
- 在 `max-width: 768px` 下，仅对 `.document-governance-page` 内的 Ant Table 固定列单元格取消 sticky 定位和固定列阴影叠层。
- `DocumentGovernancePage.tsx` 仅新增本页 CSS import，业务列定义、分页、权限判断、访问策略、版本治理、导入导出和提交 payload 均保持不变。

修复后复核：

- mobile `390x844` 下 `/console/documents` 仍保持 `documentOverflowX = 0`、`bodyOverflowX = 0`。
- 文档治理表格仍保持局部横向滚动：`tableClientWidth = 264`、`tableScrollWidth = 1500`、`overflow-x = auto`。
- 移动端固定列单元格样式已变为 `position: static`、`left/right: auto`、`z-index: auto`。
- 横向滚动到操作列后，“详情”按钮中心命中测试返回按钮自身，真实点击可打开“文档详情”弹窗。
- 文档详情弹窗在 mobile `390x844` 下宽度约 `374px`，落在视口内，未引入全局横向溢出。

## 保持不变

- 不新增或修改业务 API。
- 不新增或修改权限键、路由语义、数据库结构或数据种子。
- 不修改 Console 登录回流、授权资源保存、系统设置保存、治理动作、订单 / 商品 / 文档详情读取或文档访问策略提交载荷。
- 不把 Console 改造成独立移动应用；移动画板仍作为响应式后台验收参考。
- 不进入 `P3-12-E`，不创建 tag，不进入 M15 测试或生产部署流程。

## 验证记录

- Browser 插件：Gateway Console 真实页面 PC `1920x1080` 与 mobile `390x844` 覆盖本记录列出的 8 个代表页，未发现全局横向溢出；宽表均按局部滚动承载。
- Browser 插件：mobile `390x844` 复测 `/console/documents` 修复点，确认固定列遮挡操作按钮问题已解除，文档详情弹窗可打开且无全局横向溢出。
- `npm run build --workspace=radish.console`：通过。
- `npm run check:host-runtime -- --details`：通过，Gateway / Api / Auth 健康端点均返回 `200`。
- `git diff --check`：通过。
- `npm run check:repo-hygiene:changed`：通过，已检查 3 个已跟踪变更文本文件。
- `npm run check:repo-hygiene -- Docs/records/p3-12-d56-console-gateway-grouped-page-smoke-2026-07-04.md Frontend/radish.console/src/pages/Documents/DocumentGovernancePage.css`：通过，已检查 2 个新增文本文件。

Browser 插件在尝试把完整 8 页矩阵压成单次批量脚本重新采集时，曾因长导航等待超时重置连接；本批已按分段真实页面访问和修复点定向复核完成证据收口，不把该工具限制写成页面问题。

## 下一步

继续留在 `P3-12-D`。D56 完成 Console Gateway 代表页的一轮真实复核和一个真实移动端 UI 问题修复，但仍不能作为整个 UI 专题退出判断；后续应继续按 D36-D56 证据判断是否还需要更多真实数据态样本、页面级 Pencil 对齐或候选前集中验收。
