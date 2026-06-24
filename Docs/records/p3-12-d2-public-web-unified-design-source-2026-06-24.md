# P3-12-D2 公开 Web 统一体验设计源记录

> 日期：2026-06-24（Asia/Shanghai）
>
> 状态：公开 Web 设计源首批完成；不进入视觉代码实现
>
> 结论：`public-web-unified-experience.pen` 已创建并写入 `P01-P02`，覆盖公开壳层基座与 `/discover` 发现内容流桌面视图。后续继续在同一设计源补公开详情阅读、公开集合页和移动单列基线；完成设计稿与说明文档后，再进入实现。

## 背景

`P3-12-B1` 至 `P3-12-B6` 已让 Web 端按规划口径成为正式 Web 主路径完整 app。下一阶段进入 `P3-12-D` 统一 UI 设计与视觉收束，不继续补旧 WebOS 功能。

`P3-12-D1` 已先定义页面矩阵、设计源拆分和停止线。本轮在 Pencil 可用后，开始补公开 Web 设计源。

## 设计源

文件：

```text
Docs/frontend/design-sources/public-web-unified-experience.pen
```

已同步登记：

- [设计源文件目录](/frontend/design-sources/README)

首批变量：

- `rx-bg-app`
- `rx-bg-surface`
- `rx-bg-muted`
- `rx-text-primary`
- `rx-text-secondary`
- `rx-text-muted`
- `rx-border-soft`
- `rx-brand-primary`
- `rx-brand-soft`
- `rx-accent-jade`
- `rx-accent-ink`
- `rx-accent-earth`
- `rx-accent-purple`
- `rx-pattern-line`
- `rx-font-heading`
- `rx-font-body`
- `rx-font-mono`
- `rx-radius-card`
- `rx-radius-control`
- `rx-space-*`
- `rx-shell-max-width`

变量取值沿用 [视觉主题规范](/frontend/visual-theme-spec) 与 [视觉颜色参考](/frontend/visual-color-reference) 的淡雅新中式、纸色底、低饱和边框和克制品牌色口径。

## 已完成画板

### `P01 - Public Web Shell Foundation`

职责：

- 公开 Web 共享头部。
- 品牌锁定、公开导航和登录 / 工作台动作。
- 来源返回提示条。
- 公开内容主区与右侧参与 / 榜单 / 边界辅助区。
- 弱纹样边缘收边。

设计口径：

- “工作台”进入 `/workbench`，不直接打开 `/desktop`。
- 公开页保持内容型入口，不做营销首页。
- `PublicId` 不作为普通身份文本展示。
- 移动端后续降为单列连续阅读，不复刻 WebOS。

### `P02 - Discover Content Stream`

职责：

- `/discover` 发现内容流桌面基线。
- 内容类型筛选、搜索和排序。
- 论坛重点内容、文档更新、商城预览和榜单预览。
- 身份展示规则、数据状态槽、登录继续和链接返回契约。

设计口径：

- 公开卡片必须提供真实公开 `href`。
- 普通点击可保留来源状态；新标签、复制链接、canonical、OpenGraph、JSON-LD 和 sitemap 不携带来源状态。
- 作者和用户展示优先 `DisplayHandle / DisplayName`；`PublicId` 只用于 URL、分享和传参。
- 加载、空结果和错误必须保留明确状态槽，不出现空白页。

## 验证

Pencil 侧：

- `P01`：`snapshot_layout` 返回 `No layout problems.`
- `P01`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P02`：首次生成后发现主体内容被画板高度裁切；已加高画板并复查。
- `P02`：修正 lucide 图标名 `check-circle-2` 为 `circle-check`。
- `P02`：复查 `snapshot_layout` 返回 `No layout problems.`
- `P02`：截图目检未发现明显裁切、坍塌或横向溢出。

仓库侧：

```bash
git diff --check -- Docs/frontend/design-sources/README.md Docs/frontend/design-sources/public-web-unified-experience.pen
```

结果：通过。

## 后续顺序

1. 继续在 `public-web-unified-experience.pen` 补 `P03 - Public Detail Reading`，覆盖 forum 公开详情与 docs 公开详情的统一阅读基线。
2. 补 `P04 - Public Collection Pages`，覆盖 forum 列表 / 搜索 / 分类、docs 搜索、公开个人页、榜单和商城公开浏览集合页。
3. 补 `P05 - Mobile Public Single Column`，固定移动端公开 Web 单列阅读、筛选、来源返回和登录参与策略。
4. 公开 Web 设计源完成后，更新公开 Web 统一 UI 设计说明，再创建 `private-web-workflows.pen`。
5. 私域与作者工作流、Console 文档治理画板补齐并说明文档确认后，再进入视觉代码实现与 PC / mobile 复核。

## 当前不做

- 不进入 `radish.client` 视觉代码实现。
- 不修改 Console 设计源。
- 不创建 `private-web-workflows.pen`。
- 不把 `/desktop` 或 WebOS Dock / 窗口系统纳入公开 Web 视觉基线。
- 不把公开入口改成营销首页或品牌宣传页。
