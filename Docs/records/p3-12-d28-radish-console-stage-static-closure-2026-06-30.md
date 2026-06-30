# P3-12-D28 radish.console 阶段静态收口记录

> 日期：2026-06-30（Asia/Shanghai）
>
> 范围：`Frontend/radish.console/src/router/routerComponents.tsx`、`Frontend/radish.console/src/router/routerComponents.css`，以及 D14-D27 已迁移页面的静态扫描结果。

## 目标

承接 D27 后的 `radish.console` 阶段静态收口，复核 D14-D27 已迁移页面是否保持 Console 语义页头、指标、工具条、状态组件和外层壳层一致性，并整理剩余 inline 样式 / 硬编码色 / 旧壳层风险。

本批不扩大到业务契约、API、权限动作、表单字段、运行态 smoke 或内部运维平台设计。

## 代码变更

- 新增 `routerComponents.css`，将路由认证中、无 Console 权限和懒加载状态的旧 inline 样式迁入 CSS。
- `routerComponents.tsx` 继续保留 token 校验、idle 过期、权限判断、路由守卫和重定向逻辑不变。
- 路由加载状态补 `role="status"`，只影响语义与样式承载，不改变页面跳转或鉴权流程。

## 阶段扫描结论

- D14-D27 目标页面已覆盖 `SystemConfigList`、`OrderList`、`UserList`、`ProductList`、`DocumentGovernancePage`、`TagList`、`CategoryList`、`StickerGroupList`、`StickerList`、`RoleList`、`RolePermissionPage`、`ModerationPage`、`ExperienceAdminPage` 和 `HangfirePage`。
- 上述页面外层已接入 `ConsolePageHeader`、`ConsoleMetricGrid`、`ConsoleMetricCard`、`ConsoleStatusChip` 或 `ConsoleToolbar`，保持“页头 / 指标 / 工具条 / 主内容 / 摘要或工作台区块”的扫描顺序。
- `routerComponents.tsx` 与 `SystemTools` 当前不再命中 `style=`、硬编码十六进制色或 `rgba(...) / rgb(...)`。
- 本批不把 `/hangfire` 扩展为内部任务平台；它仍是受 Console 权限保护的外部 Hangfire Dashboard 外壳。

## 剩余风险

- 表单与上传预览：`ProductForm`、`CategoryForm`、`StickerForm`、`StickerGroupForm` 仍有输入宽度、隐藏输入、上传预览框、占位背景和弱文本的历史 inline 样式 / 硬编码色。它们涉及多种媒体上传与表单布局，适合后续按“表单 / 媒体上传控件”集中治理。
- 详情与抽屉：`OrderDetail`、`ProductDetail`、`DocumentGovernancePage` 的详情区域仍有少量危险色、图片展示、垂直布局和隐藏输入样式；它们与订单履约、商品详情和文档治理抽屉耦合，适合后续按“详情 / 抽屉静态收口”治理。
- 批量上传弹窗：`StickerBatchUploadModal.css` 仍有历史硬编码提示色；适合与贴纸上传预览、批量修复队列一起收口。
- `TagForm` 的 `#1677FF` 是颜色字段示例文案，不作为硬编码样式处理。

## 停止线

- 不在 D28 中批量重写商品、分类、贴纸和文档表单内部结构。
- 不修改订单详情、商品详情、文档治理抽屉、上传预览、批量上传或系统设置表单的业务行为。
- 不新增共享上传组件或表单抽象；后续如要治理，先按真实重复度和业务边界设计。
- 不执行真实 Gateway PC / mobile smoke；阶段页面真实验收需在用户确认前后端已启动后执行。

## 验证

- `npm run type-check --workspace=radish.console`
- `npm run build --workspace=radish.console`
- `npm run check:repo-hygiene:changed`
- `node Scripts/check-repo-hygiene.mjs Docs/records/p3-12-d28-radish-console-stage-static-closure-2026-06-30.md`
- `git diff --check`
- `rg -n "style=|#[0-9a-fA-F]{3,8}|rgba\\(|rgb\\(" Frontend/radish.console/src/router/routerComponents.tsx Frontend/radish.console/src/router/routerComponents.css Frontend/radish.console/src/pages/SystemTools`：无命中

## 下一步

若继续静态治理，优先进入 `P3-12-D29` 深层表单 / 详情静态收口，按 `ProductForm / CategoryForm / StickerForm / StickerGroupForm` 的上传预览和宽度规则先拆清重复点；若准备做阶段验收，则在用户明确前后端已启动后执行 Gateway PC / mobile 页面复核。
