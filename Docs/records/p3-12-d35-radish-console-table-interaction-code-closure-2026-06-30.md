# P3-12-D35 radish.console 表格交互代码侧收口记录

> 日期：2026-06-30（Asia/Shanghai）
>
> 范围：`radish.console` 已迁移 Console 页面表格交互的代码侧目标扫描与低风险收口，聚焦中宽 PC / 移动窄屏可读性、表格操作区、弹窗内表格、分页与工具条布局。

## 背景

`P3-12-D33 / D34` 已完成操作列按钮换行、表格样式残留和运维 / 治理目标目录静态收口。D35 继续做代码侧扫描，不进入 `P3-12-E` 发布候选，也不把范围扩大为 Console 全站重构。

真实 Gateway PC / mobile 页面扫描仍按 UI 专题成组验收或进入发布候选前集中申请；本批不逐页申请真实联调。

## 收口内容

- `adminFeature.css`：新增 `.admin-table-scroll-region`，并统一 `admin-table-panel`、`admin-overview-panel`、`admin-feature-card` 内 Ant Table 的宽度约束与分页换行规则，避免表格或分页选项在移动窄屏撑破面板。
- `UserList`：用户列表操作列 `Space size="small"` 增加 `wrap`，保留查看详情路由和权限判断不变。
- `Dashboard`：最近订单表格接入表格滚动区域并补 `scroll.x`，避免移动窄屏压缩订单号、商品、状态和查看动作。
- `UserDetail`：最近萝卜币流水和购买记录两个 Tab 内表格接入滚动区域，保持查询、分页和跳转动作不变。
- `DocumentGovernancePage`：版本治理弹窗内版本表格接入滚动区域，保留查看版本详情和版本回滚动作不变。
- `SystemConfigList`：系统设置变更历史弹窗内表格接入滚动区域，保留历史读取、风险标签和空态不变。
- `StickerBatchUploadModal`：上传进度表补 `scroll.x`，上传确认表和冲突修复表接入弹窗内滚动区域，保留附件上传、字段确认、冲突重提和批量提交载荷不变。

本批不改 API、权限、路由、筛选、分页参数、保存动作、上传动作、版本回滚、订单跳转或任何业务载荷。

## 目标扫描

扫描范围：

- `Frontend/radish.console/src/pages/Applications`
- `Frontend/radish.console/src/pages/SystemConfig`
- `Frontend/radish.console/src/pages/Coins`
- `Frontend/radish.console/src/pages/Experience`
- `Frontend/radish.console/src/pages/Moderation`
- `Frontend/radish.console/src/pages/Users`
- `Frontend/radish.console/src/pages/Documents`
- `Frontend/radish.console/src/pages/Stickers`
- `Frontend/radish.console/src/pages/Dashboard`
- `Frontend/radish.console/src/pages/adminFeature.css`

扫描结果：

- `style={` / 十六进制硬编码色 / `rgb(` / `rgba(`：未命中。
- 排除纵向状态标签后，未换行的 `<Space size="small">` 操作按钮组：未命中。

## 验证

- `rg "style=\\{|#[0-9a-fA-F]{3,8}|rgba\\(|rgb\\("` 目标范围扫描：未命中。
- `rg --pcre2 "<Space(?=[^>]*size=\\\"small\\\")(?![^>]*wrap)(?![^>]*orientation=)"` 目标范围扫描：未命中。
- `npm run type-check --workspace=radish.console`：通过。
- `npm run build --workspace=radish.console`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

## 后续判断

本批已处理静态扫描能确认的表格交互问题；剩余中宽 PC / 移动 CSS 视口真实表现主要依赖运行态观察。按当前协作规则，真实 Gateway PC / mobile 复核继续并入 UI 专题成组验收或进入 `P3-12-E` 前集中申请，不逐批申请。
