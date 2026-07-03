# P3-12-D30 radish.console 详情 / 抽屉静态收口记录

> 日期：2026-06-30（Asia/Shanghai）
>
> 范围：`OrderDetail`、`ProductDetail`、`DocumentGovernancePage`、`StickerBatchUploadModal.css` 与相关 Console CSS。

## 目标

承接 D29 剩余风险，集中收口订单详情、商品详情、文档治理抽屉和贴纸批量上传弹窗中的 inline 样式与硬编码提示色。

本批只治理样式承载，不改订单详情读取、商品详情读取、文档导入 / 访问策略 / 版本回滚、贴纸批量上传解析、权限判断、路由状态或任何保存动作。

## 代码变更

- `OrderDetail` 将失败原因、单价、总价和管理员备注区的危险色、字重、字号和间距迁入 `OrderList.css`。
- `ProductDetail` 将商品图标 / 封面展示的 `object-fit`、圆角和售价强调迁入 `ProductList.css`。
- `DocumentGovernancePage` 复用 `adminForm.css` 的隐藏输入类，并将详情、访问策略和版本详情抽屉的全宽布局迁入 `adminFeature.css`。
- `StickerBatchUploadModal.css` 将摘要、冲突行、警告文本和错误文本改为 Console 语义 token。

## 扫描结论

- `OrderDetail`、`ProductDetail`、`DocumentGovernancePage`、`OrderList.css`、`ProductList.css`、`adminFeature.css` 和 `StickerBatchUploadModal.css` 不再命中目标 `style=`、硬编码十六进制色或 `rgba(...) / rgb(...)`。
- 本批复用既有 Console token：`--console-danger`、`--console-warning-bg`、`--console-warning-text`、`--console-text-secondary`、`--console-text-primary` 和 `--console-radius-control`。
- `StickerBatchUploadModal.css` 的历史提示色已作为同批低风险扫尾处理；上传队列状态、文件解析和提交行为保持不变。

## 剩余风险

- 本批不执行真实 Gateway PC / mobile smoke；阶段页面真实验收仍需用户确认前后端已启动后再执行。
- 本批未继续扩大到全 Console 静态扫描；若后续阶段验收或目标扫描命中新残留，再按页面类型定向回拉。

## 停止线

- 不修改业务 API、权限键、路由、URL 查询、表单字段、附件字段、保存载荷或回滚 / 上传 / 备注动作。
- 不把订单详情、商品详情和文档治理抽屉抽为共享业务组件；当前重复点集中在静态样式，先保持页面边界清晰。
- 不执行真实 Gateway PC / mobile smoke；若进入阶段真实验收，必须先告知需要启动前后端并等待用户明确确认。

## 验证

- `npm run type-check --workspace=radish.console`
- `npm run build --workspace=radish.console`
- `npm run check:repo-hygiene:changed`
- `node Scripts/check-repo-hygiene.mjs Docs/records/p3-12-d30-radish-console-detail-drawer-static-closure-2026-06-30.md`
- `git diff --check`
- `rg -n "style=|#[0-9a-fA-F]{3,8}|rgba\\(|rgb\\(" Frontend/radish.console/src/pages/Orders/OrderDetail.tsx Frontend/radish.console/src/pages/Orders/OrderList.css Frontend/radish.console/src/pages/Products/ProductDetail.tsx Frontend/radish.console/src/pages/Products/ProductList.css Frontend/radish.console/src/pages/Documents/DocumentGovernancePage.tsx Frontend/radish.console/src/pages/adminFeature.css Frontend/radish.console/src/pages/Stickers/StickerBatchUploadModal.css`：无命中

## 下一步

若准备阶段验收，先告知用户需要启动前后端并等待确认，再执行 Gateway PC / mobile 页面复核；若继续静态治理，先做目标扫描评估收益，不默认扩大为全站重构。
