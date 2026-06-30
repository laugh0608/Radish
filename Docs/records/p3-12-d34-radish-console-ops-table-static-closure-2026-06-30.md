# P3-12-D34 radish.console 运维与治理表格静态收口记录

> 日期：2026-06-30（Asia/Shanghai）
>
> 范围：`radish.console` D33 未纳入的应用、系统设置、萝卜币、经验和内容治理表格页代码侧目标扫描与低风险静态收口。

## 背景

`P3-12-D33` 已收口分类、标签、贴纸、角色和文档版本治理表格的按钮组换行与贴纸排序输入样式残留。按风险分层，D33 / D34 均未改变用户可见业务流程、API、权限、路由或保存动作，真实 Gateway 视口扫描不作为每个小批次的默认前置门槛，应在 UI 专题成组验收或进入发布候选前集中补齐。

## 收口内容

- `Applications`：应用表格操作列 `Space` 增加 `wrap`，避免编辑、重置密钥、删除按钮在窄列内硬挤压；创建、编辑、重置密钥和删除动作保持不变。
- `CoinAdminPage`：萝卜币流水正负金额颜色改用 `--console-success` / `--console-danger`，移除页面级硬编码颜色。
- `CoinAdminPage`：交易表格 `marginTop` 从 `style` 迁入 `.coin-admin-transaction-table`。
- `SystemConfigForm`：数字设置 `InputNumber` 宽度从 inline style 迁入 `.system-config-form-control-full`。

本批不改调账、流水筛选、系统设置校验、设置保存载荷、应用密钥重置、删除确认和任何权限判断。

## 目标扫描

扫描范围：

- `Frontend/radish.console/src/pages/Applications`
- `Frontend/radish.console/src/pages/SystemConfig`
- `Frontend/radish.console/src/pages/Coins`
- `Frontend/radish.console/src/pages/Experience`
- `Frontend/radish.console/src/pages/Moderation`

扫描结果：

- `style={` / 十六进制硬编码色 / `rgb(` / `rgba(`：未命中。
- 未换行的 `<Space size="small">` 操作按钮组：未命中。

## 验证

- `rg "style=\\{|#[0-9a-fA-F]{3,8}|rgba\\(|rgb\\("` 目标目录扫描：未命中。
- `rg --pcre2 "<Space(?=[^>]*size=\\\"small\\\")(?![^>]*wrap)"` 目标目录扫描：未命中。
- `npm run type-check --workspace=radish.console`：通过。
- `npm run build --workspace=radish.console`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

后续在成组验收或进入 `P3-12-E` 前补：

- Gateway 下 Console 代表表格页中宽 PC 视口扫描。
- Gateway 下 Console 代表表格页移动 CSS 视口扫描。

## 后续判断

下一步继续 `P3-12-D` UI 专题目标扫描，优先评估是否还存在需要代码侧处理的 Console 表格固定列、移动窄屏可读性和已迁移页面真实可用性问题；若剩余问题主要依赖运行态观察，则集中申请一次真实联调，而不是逐批申请。
