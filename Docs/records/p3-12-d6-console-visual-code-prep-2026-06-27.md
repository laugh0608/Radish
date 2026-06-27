# P3-12-D6 Console 视觉代码实现前盘点

> 日期：2026-06-27（Asia/Shanghai）
>
> 状态：完成只读盘点；下一步等待确认后进入 `radish.console` 公共壳层首批实现
>
> 输入：`web-ui-foundation.pen`、`console-governance-workbench.pen`、[Console 治理工作台设计端点](/frontend/console-governance-workbench-design)

## 结论

`radish.console` 已经有浅色后台基座、`--console-*` token、`AdminLayout`、`adminFeature.css` 和治理 / 表格 / 设置页面结构，适合在现有边界内推进，不需要重建项目壳层。

当前差距集中在公共壳层和可复用结构：

- `AdminLayout` 仍是单文件承载侧栏、顶栏、用户菜单、全局搜索和折叠状态，侧栏宽度为 `268px`，未对齐 `P00` 的 `300px` 规格。
- 路由元数据只有 `key / path / title / permission / visible`，没有路由分组、图标键、排序分组和 badge 语义；图标映射散落在 `AdminLayout`。
- 顶栏只有折叠按钮、搜索和用户菜单；页面标题、状态 chip、主操作和二级工具区仍由各页面手写。
- `adminFeature.css` 已覆盖页面、卡片、指标、表格、调度、设置和治理工作台布局，但缺少 `ConsolePageHeader`、`ConsoleToolbar`、`ConsoleStatusChip` 这类语义结构。
- 多数页面重复手写 header、metric、toolbar 和 aside；代表页包括 `OrderList`、`SystemConfigList`、`DocumentGovernancePage`、`RolePermissionPage`、`ModerationPage`、`ExperienceAdminPage`。
- `@radish/ui` 已提供 Button、Table、Tag、Modal、BottomSheet、图表和 Ant Design re-export；Console 专用壳层不应上提到 `@radish/ui`，首批应留在 `radish.console`。

## 首批实现建议

第一批只做公共壳层和结构组件，不改变 API、权限、表单字段、路由语义或业务动作：

1. 扩展 `routeMeta`
   - 增加 `group`、`iconKey`、`sidebarOrder`、可选 `badgeTone / badgeText`。
   - `getSidebarRoutes` 输出仍按权限过滤，但保留分组信息给侧栏渲染。
   - 图标映射从 `AdminLayout` 下沉到稳定 helper，避免页面组件重复维护。

2. 拆分 `AdminLayout`
   - 保留现有导出名，内部拆出 `ConsoleSidebar`、`ConsoleTopbar`、`ConsoleUserMenu`。
   - PC 侧栏对齐浅色图标导航和真实路由分组；默认宽度调整到 `300px`。
   - 顶栏对齐 `84px`，保留全局搜索、用户菜单和折叠控制。

3. 补充 Console 语义组件
   - `ConsolePageHeader`：标题、说明、状态 chip、主操作和二级动作。
   - `ConsoleMetricGrid / ConsoleMetricCard`：统一指标密度。
   - `ConsoleToolbar`：筛选、批处理、刷新、导出。
   - `ConsoleStatusChip`：success / warning / danger / info / neutral。
   - 这些组件先放在 `Frontend/radish.console/src/components/ConsoleShell/` 或相邻 `ConsolePage` 目录，不进入 `@radish/ui`。

4. 首批页面应用范围
   - 第一批代码可以只迁移壳层和一个低风险代表页，例如 `SystemConfigList` 或 `OrderList` 的 header / toolbar / metric。
   - 治理页、权限矩阵和文档治理先不混入第一批，避免公共壳层和复杂业务动作同时变化。

## 暂不进入

- 不重做 API 客户端、权限键、数据模型、治理动作语义、经验规则或冻结语义。
- 不把 Console 专用壳层抽到 `@radish/ui`。
- 不把所有页面一次性改成同一种工作台。
- 不启动服务，不做 Gateway smoke；首批普通开发验证先用类型检查、构建和仓库卫生检查。

## 建议验证

```bash
npm run type-check --workspace=radish.console
npm run build --workspace=radish.console
npm run check:repo-hygiene:changed
git diff --check
```

阶段验收或用户明确要求时，再按 `Docs/guide/browser-smoke.md` 集中覆盖 Gateway PC / mobile 页面。
