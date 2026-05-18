# Console UI 一致性评估记录（2026-05-18）

## 结论

Console 当前未发现需要立即阻断 `P3-6` 的 `P0/P1` UI 一致性问题；后续若继续扩展后台能力，应先按“新增 / 改动页面优先收敛”治理，不建议在真实使用观察期启动整站 UI 重构。

当前 Console 已经大量通过 `@radish/ui` 复用 Ant 组件、图标、表格、弹窗、确认框、骨架屏和交互反馈，但页面壳层、业务页 CSS、直接 `antd` 引入、inline style 与硬编码 Ant 色值仍然存在，后续扩展时容易继续分叉。

## 范围

- `Frontend/radish.console/src/components/AdminLayout/AdminLayout.tsx`
- `Frontend/radish.console/src/components/AdminLayout/AdminLayout.css`
- `Frontend/radish.console/src/pages/Dashboard/Dashboard.tsx`
- `Frontend/radish.console/src/pages/Dashboard/Dashboard.css`
- `Frontend/radish.console/src/pages/UserList.tsx`
- `Frontend/radish.console/src/pages/ProductList.tsx`
- `Frontend/radish.console/src/pages/Moderation/ModerationPage.tsx`
- `Frontend/radish.console/src/pages/adminFeature.css`
- `Frontend/radish.ui/src/components/*`
- `Docs/frontend/visual-theme-spec.md`
- `Docs/frontend/visual-color-reference.md`

本次只做评估与文档留痕，不修改 Console 运行时代码。

## 观察

1. Console 并非完全自建控件。`AdminLayout`、用户、商品、审核等页面已经从 `@radish/ui` 引入 `Button`、`Table`、`Form`、`Tag`、`Space`、`message`、图标、`ConfirmDialog`、`TableSkeleton`、`AntInput`、`AntSelect` 等能力。
2. `Dashboard` 仍直接从 `antd` 引入 `Card`、`Statistic`，并使用 `#1890ff`、`#3f8600`、`#722ed1`、`#cf1322` 等 inline 颜色。后续新增统计模块时容易绕过共享 UI 与主题 token。
3. `AdminLayout.css` 与 `adminFeature.css` 已形成 Console 本地页面壳和功能页样式习惯，但仍以 `#fff`、`#f0f0f0`、`#fafafa`、`#262626`、`#ffe58f` 等硬编码色值为主，尚未与 Radish 视觉 token 明确对齐。
4. `adminFeature.css` 具备继续收敛价值：它已经覆盖功能页、标题区、操作区、banner、metrics、描述列表、提示块等常见后台形态。短期应作为 Console 内部一致性基线，长期再判断是否抽入 `@radish/ui`。
5. `@radish/ui` 当前可支撑 Console 后续扩展的基础能力包括按钮、输入、选择、弹窗、确认、Toast、表格、骨架屏、图表、上传、Markdown、通知、图标与上下文菜单。新增 Console 页面没有必要再创建新的基础控件封装。
6. `UserList.tsx` 评估中发现一处疑似中文乱码错误提示，已在同日小修中恢复为“获取用户列表失败”；它不构成当前 UI 一致性治理的阻断项。

## 后续口径

- 新增或明显改动 Console 页面时，优先使用 `@radish/ui` 已有组件和 Ant alias，不新增重复的本地基础控件。
- 业务页先复用 `adminFeature.css` 的页面结构类，避免每个后台页面重新定义 header、toolbar、card、metric、empty / loading / error 形态。
- 新增颜色优先映射到 `Docs/frontend/visual-color-reference.md` 的语义角色；确需保留 Ant 语义状态色时，应集中在 Console 样式层，不在组件 JSX 中继续扩散 inline 色值。
- 不把 `adminFeature.css` 立即抽到 `@radish/ui`。只有当 client / console 多入口都需要同一后台式功能页框架时，再做共享抽象。
- 真实使用观察期不启动 Console 整站视觉重构；只在新增后台能力、修复可见缺陷或处理明确反馈时小范围收敛。

## 建议下一步

若继续推进工程治理，优先选择一个低风险小闭环：

1. 做 Console token bridge 小方案：只评审 `AdminLayout.css` 与 `adminFeature.css` 是否能先引入 Console 局部 CSS 变量，不改页面结构。
2. 等 testing URL 具备后，回到 `P3-6` 公开增长观察记录，继续补公开 head smoke 与配置事实。
