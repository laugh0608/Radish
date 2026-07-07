# P3-12-E7-C Public 信息密度首批收口记录

> 日期：2026-07-07（Asia/Shanghai）
>
> 状态：代码实现与验证已完成
>
> 上游记录：[P3-12-E7 Console / Public UI 与文案成熟度首批差距审计](/records/p3-12-e7-console-public-ui-copy-gap-audit-2026-07-07)、[P3-12-E7-B Public 术语清理记录](/records/p3-12-e7-b-public-terminology-cleanup-2026-07-07)

## 目标

`E7-C` 承接 `E7-B` 术语清理之后的公开页信息密度问题。本批只处理 Public / Docs / Shop 的首屏信息结构和说明层级，不改变路由契约、API、权限、登录回流、购买提交、文档读取或后端行为。

首批判断：

- `/docs` 首屏不应让完整历史目录压过最新可读文档。
- `/shop` 首屏不应重复展示“公开商城 / 公开浏览 / 浏览提示”式说明，应优先展示商品、分类、价格、库存和购买入口。
- 公开页仍应保留边界说明，但说明应作为辅助信息，不抢占内容和动作。

## 设计留痕

已通过 Pencil MCP 在 `Docs/frontend/design-sources/public-web-unified-experience.pen` 新增 `E7-C - Public Information Density - Docs and Shop` 画板，节点 id：`TQX1h`。

本次设计留痕只作为 E7-C 首批对照画板：

- Docs：最新可读文档作为主区，目录作为紧凑定位工具，搜索和作者入口进入辅助 rail。
- Shop：推荐商品 / 商品列表作为主区，分类、购买入口和个人页面边界进入辅助 rail。
- 移动端：保持单列任务优先，不让说明块排在内容前。

限制说明：

- 当前 Pencil 活动编辑器是 `console-governance-workbench.pen`。
- 显式读取 `public-web-unified-experience.pen` 时，现有顶层节点仍以 Console 画板为主；因此本批新增 E7-C 对照画板，不直接改旧 P01-P14 Public 画板。
- `TQX1h` 的 `snapshot_layout` 未发现布局问题；本轮 `.pen` 未出现在 Git diff 中，设计留痕以 Pencil MCP 当前编辑状态为准。

## 实施范围

已覆盖：

- `radish.client` 公开 Docs 列表页：
  - 默认把“可阅读文档”放在目录前。
  - 目录默认只展示前 `36` 个节点，保留展开 / 收起完整目录能力。
  - 列表页右侧 rail 移除重复阅读提示，只保留搜索、数量和作者入口。
  - 文案从解释页面边界改为读者任务语言。
- `radish.client` 公开 Shop 浏览页：
  - 移除浏览页右侧重复阅读指南，保留分类 / 商品数量、购买入口和个人页面边界。
  - 首页 / 商品列表工具条去掉重复 kicker。
  - 商城首屏文案缩短为商品比较与购买路径，不再重复迁移说明。

## 停止线

本批未做：

- 未修改公开文档 API、文档树数据、文档 slug、Markdown 内容或文档权限。
- 未修改商品 API、购买检查、订单、背包、资产或支付逻辑。
- 未修改 Forum detail 的讨论区结构；该项可作为 E7-C 后续批次继续处理。
- 未修改 Discover 信息流排序或推荐逻辑。
- 未处理 Auth 授权页信息层级；继续进入 `E7-D`。

## 本地验证

已完成：

- `npm run build --workspace=radish.client`：通过。
- `git diff --check`：通过。
- `npm run check:repo-hygiene:changed`：通过。
- 目标术语扫描：`private Web routes / public docs shell / public shell / 公开壳层 / 正式 Web / 私域路由 / 公开 docs / 公开 forum / 留在 文档 / WebOS desktop / reaction ` 在 `i18n.ts`、公开 Docs 和公开 Shop 目标目录中无命中。
- Gateway `/docs`：Chrome 插件复核 PC `1920x1080 @ DPR 1` 与 mobile `390x844 @ DPR 3`，页面可渲染，无横向溢出；“可阅读文档”出现在“目录”前，目录默认显示 `36 / 464` 预览并保留完整展开入口；目标内部术语无可见命中。
- Gateway `/shop`：Chrome 插件复核 PC `1920x1080 @ DPR 1` 与 mobile `390x844 @ DPR 3`，页面可渲染，无横向溢出；首屏标题顺序为“公开商城 / 推荐商品 / 商品卡 / 购买入口 / 个人页面”，商品状态、库存、价格和登录购买入口可见；目标内部术语无可见命中。

复核说明：

- 本轮优先尝试 Browser 插件，导航调用持续超时，改用用户已允许的 Chrome 插件完成同一组只读复核。
- 本批只验证 Docs / Shop 首批信息密度改动；Forum detail、Discover 和 Auth 授权页继续进入后续 E7 批次。
