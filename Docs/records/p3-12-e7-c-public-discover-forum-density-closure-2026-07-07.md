# P3-12-E7-C Public 发现与论坛详情信息密度第二批收口记录

> 日期：2026-07-07（Asia/Shanghai）
>
> 状态：代码实现与静态验证已完成
>
> 上游记录：[P3-12-E7 Console / Public UI 与文案成熟度首批差距审计](/records/p3-12-e7-console-public-ui-copy-gap-audit-2026-07-07)、[P3-12-E7-C Public 信息密度首批收口记录](/records/p3-12-e7-c-public-information-density-first-closure-2026-07-07)

## 目标

本批承接 `E7-C` 首批 Docs / Shop 收口之后的 Public 第二批问题，聚焦 Discover 和 Forum detail：

- Discover 首屏应先展示正在发生的社区内容和可继续浏览的内容流，不应以入口说明为主。
- Forum detail 应把正文、轻回应、评论和参与入口放在主要阅读路径，阅读说明只作为辅助信息。
- 公开页仍保留来源返回、登录后动作边界和语义说明，但不让说明块压过内容、讨论和动作。

## 设计留痕

已通过 Pencil MCP 在 `Docs/frontend/design-sources/public-web-unified-experience.pen` 新增 `E7-C - Public Information Density - Discover and Forum Detail` 画板，节点 id：`x68Nii`。

本次设计留痕覆盖：

- Discover：首屏以社区讨论和内容流为主，指标、相关文档 / 商品 / 榜单和参与边界进入辅助 rail。
- Forum detail：正文、回应汇总、轻回应、评论和参与 CTA 位于主路径；来源返回、讨论语义、作者模式和阅读提示进入侧栏辅助层。
- 移动端：保持单列高频任务优先，搜索、筛选、轻回应和讨论输入不被说明块前置。

限制说明：

- 当前 `.pen` 变更未出现在 Git diff 中，设计留痕以 Pencil MCP 当前编辑状态为准。
- `x68Nii` 的 `snapshot_layout` 未发现布局问题。

## 实施范围

已覆盖：

- `radish.client` Discover：
  - 首屏社区帖子从 `3` 条扩为 `5` 条，提高真实内容密度。
  - 收紧首屏指标、社区讨论区、相关内容 rail 和说明卡片间距。
  - 将“入口导航”口径改为“继续浏览”，公开入口类文案改为社区动态、内容流和公开页面表达。
- `radish.client` Forum detail：
  - 右侧 rail 调整为来源返回后直接给参与讨论入口，阅读提示降到侧栏底部。
  - 收紧参与 CTA、回应汇总、轻回应区和评论区间距，减少说明块视觉重量。
  - 清理 Forum 公共文案中的 `workspace interactions`、`desktop interactions` 和 `公开入口` 残留表达。

## 停止线

本批未做：

- 未修改 Discover 信息源接口、排序算法、推荐逻辑或数据合并规则。
- 未修改 Forum detail API、评论 / 轻回应提交逻辑、登录回流参数、权限或作者动作行为。
- 未改后端、数据库、审计、运行时配置或 Gateway 路由契约。
- 未处理 Auth 授权页信息层级；该项进入 `E7-D`。

## 本地验证

已完成：

- `npm run build --workspace=radish.client`：通过。
- `npm run check:repo-hygiene:changed`：通过，已检查 `8` 个变更文件，未发现文本卫生问题。
- `git diff --check`：通过。
- 目标术语扫描：`Public entry / Entry navigation / Open entry / 公开入口 / 入口导航 / 打开入口 / desktop workspace interactions / desktop interactions / workspace interactions / 桌面工作台` 在 `i18n.ts`、公开 Discover 和公开 Forum 目标目录中无命中。

复核说明：

- 本轮未改后端、接口、权限、登录回流或运行时配置，不需要重启后端。
- 本轮未执行 Gateway 真实页面 smoke；按当前协作规则，真实 smoke 需要用户在当轮明确说明前后端已启动，不能沿用历史会话启动状态。
