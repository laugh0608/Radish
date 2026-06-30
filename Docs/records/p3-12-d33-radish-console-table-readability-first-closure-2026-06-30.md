# P3-12-D33 radish.console 表格可读性首批收口记录

> 日期：2026-06-30（Asia/Shanghai）
>
> 范围：`radish.console` 已迁移代表表格页的中宽 PC / 移动窄屏可读性首批代码侧收口，聚焦固定右侧操作列按钮换行和贴纸排序输入样式残留。

## 背景

`P3-12-D32` 补齐了 Console 运行态数据缺口，但不代表 UI 专题完成。按当前规划，后续继续 `P3-12-D`，优先评估 Ant Table 固定列中宽交互、移动窄屏表格可读性和已迁移页面真实可用性。

本批先做代码侧目标扫描和低风险同类收口；真实 Gateway 浏览器扫描在当前宿主恢复后补验，不提前写成已完成。

## 收口内容

- `CategoryList`、`TagList`、`StickerGroupList`、`StickerList`：固定右侧操作列的 `Space` 增加 `wrap`，避免中宽或窄屏横向滚动场景下按钮组在操作列内硬挤压。
- `RoleList`：角色管理固定右侧操作列同步增加 `wrap`，保留权限配置、编辑、启停和删除动作不变。
- `DocumentGovernancePage`：版本治理弹窗内版本操作按钮组增加 `wrap`，保留查看版本详情和版本回滚动作不变。
- `StickerList`：排序 `InputNumber` 的 `style={{ width: '100%' }}` 迁入 `StickerList.css` 的 `.sticker-item-sort-input`。

本批不改 API、权限、路由、筛选、分页、删除确认、启停、排序保存、版本回滚和任何业务载荷。

## 目标扫描

- 目标文件 `style={` / 十六进制硬编码色 / `rgb(` / `rgba(` 扫描：未命中。
- 目标文件未换行的固定右侧操作列按钮组：已收口。
- 剩余未处理项：
  - `Applications`、`SystemConfig`、`Coins`、`Experience`、`Moderation` 等页面未纳入本批，避免把 D33 扩成全站表格重构。
  - 真实中宽 PC 与移动 CSS 视口浏览器扫描待宿主恢复后补验。

## 运行态阻断

尝试用 Browser 插件从 Gateway 入口开始真实联调时，`npm run check:host-runtime -- --details` 未通过：

| 宿主 | 结果 |
| --- | --- |
| Gateway `https://localhost:5000/health` | 请求超时 |
| Api `http://localhost:5100/health` | 端口未监听 |
| Auth `http://localhost:5200/health` | 端口未监听 |

端口探测显示 `5000` 被本机 `ControlCe...` 进程监听，`5100`、`5200`、`3100` 无监听。按项目规则，本批不由 AI 直接启动 `dotnet run` 或 `npm run dev`。

## 验证

- `rg "style=\\{|#[0-9a-fA-F]{3,8}|rgba\\(|rgb\\("` 目标文件扫描：未命中。
- `rg --pcre2 "<Space(?=[^>]*size=\\\"small\\\")(?![^>]*wrap)"` 目标文件扫描：未命中。
- `npm run type-check --workspace=radish.console`：通过。
- `npm run build --workspace=radish.console`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

后续在宿主恢复后补真实页面验证：

- Browser / Chrome Gateway 入口中宽 PC 视口扫描，优先 `1366x900` 和 `1280x800`。
- Browser / Chrome Gateway 入口移动 CSS 视口扫描，优先 `390x844`；若仍不能设置 DPR，应明确记录为 CSS 视口，不写成高 DPR 物理视图完整结论。

## 后续判断

下一步先恢复宿主后补 D33 浏览器视口扫描。若本批表格操作列和贴纸排序输入在真实页面验证通过，再继续按收益评估剩余 Console 表格页，不进入 `P3-12-E` 发布候选准备。
