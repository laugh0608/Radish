# P3-12-D29 radish.console 深层表单静态收口记录

> 日期：2026-06-30（Asia/Shanghai）
>
> 范围：`ProductForm`、`CategoryForm`、`StickerForm`、`StickerGroupForm` 与共享表单样式 `adminForm.css`。

## 目标

承接 D28 归档的深层表单剩余风险，优先收口商品、分类、贴纸和贴纸分组表单中重复出现的上传预览、隐藏输入、控件宽度和弱提示文本样式。

本批只治理样式承载与重复规则，不改上传接口、附件 ID 字段、表单校验、保存载荷、权限判断或商品 / 分类 / 贴纸业务动作。

## 代码变更

- 新增 `Frontend/radish.console/src/pages/adminForm.css`，承载后台表单通用的隐藏输入、垂直字段栈、上传预览、表单控件宽度、帮助文本和弹窗 footer 动作布局。
- `ProductForm` 迁出商品图标 / 封面预览、附件隐藏输入、价格 / 库存 / 限购 / 日期 / 排序宽度、未开放商品类型提示和弹窗 footer inline 样式。
- `CategoryForm` 迁出分类图标 / 封面预览、附件隐藏输入、字段栈和排序控件宽度规则。
- `StickerForm` 迁出表情图片预览、透明图片 `contain` 展示、字段栈和排序控件宽度规则。
- `StickerGroupForm` 迁出分组封面预览、附件隐藏输入、字段栈和排序控件宽度规则。

## 扫描结论

- `ProductForm`、`CategoryForm`、`StickerForm`、`StickerGroupForm` 和 `adminForm.css` 不再命中 `style=`、硬编码十六进制色或 `rgba(...) / rgb(...)`。
- 本批复用既有 Console token：`--console-text-muted`、`--console-bg-muted`、`--console-bg-surface` 和 `--console-border-subtle`。
- 由于当前重复点集中在样式规则，暂不新增共享上传组件；后续若更多表单出现同类上传流程，再按字段契约和交互一致性评估组件抽象。

## 剩余风险

- 详情与抽屉：`OrderDetail`、`ProductDetail`、`DocumentGovernancePage` 仍有少量图片展示、垂直布局、危险色、隐藏输入或抽屉内部样式残留，适合后续按“详情 / 抽屉静态收口”集中治理。
- 批量上传弹窗：`StickerBatchUploadModal.css` 仍有历史硬编码提示色，适合与贴纸批量上传队列状态一起收口。
- `TagForm` 的 `#1677FF` 仍是颜色字段示例文案，不作为硬编码样式处理。

## 停止线

- 不修改商品、分类、贴纸和贴纸分组的上传 API、附件字段、提交 DTO 或编辑 / 新增流程。
- 不把四个表单强行合并为一个共享组件；当前只抽取样式类，避免在字段差异仍明显时制造额外耦合。
- 不执行真实 Gateway PC / mobile smoke；阶段页面真实验收仍需用户确认前后端已启动后再执行。

## 验证

- `npm run type-check --workspace=radish.console`
- `npm run build --workspace=radish.console`
- `npm run check:repo-hygiene:changed`
- `node Scripts/check-repo-hygiene.mjs Docs/records/p3-12-d29-radish-console-deep-form-static-closure-2026-06-30.md`
- `git diff --check`
- `rg -n "style=|#[0-9a-fA-F]{3,8}|rgba\\(|rgb\\(" Frontend/radish.console/src/pages/Products/ProductForm.tsx Frontend/radish.console/src/pages/Categories/CategoryForm.tsx Frontend/radish.console/src/pages/Stickers/StickerForm.tsx Frontend/radish.console/src/pages/Stickers/StickerGroupForm.tsx Frontend/radish.console/src/pages/adminForm.css`：无命中

## 下一步

若继续静态治理，优先进入 `P3-12-D30` 详情 / 抽屉静态收口，处理 `OrderDetail`、`ProductDetail` 和 `DocumentGovernancePage` 的剩余样式残留；若准备阶段验收，则在用户明确前后端已启动后执行 Gateway PC / mobile 页面复核。
