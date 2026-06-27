# P3-12-D2 公开 Web 统一体验设计源记录

> 日期：2026-06-24（Asia/Shanghai）
>
> 状态：公开 Web 设计源 `P01-P12` 已补齐；不进入视觉代码实现
>
> 结论：`public-web-unified-experience.pen` 已创建并写入 `P01-P05`，覆盖公开壳层基座、`/discover` 发现内容流、公开详情阅读、公开集合页和移动单列基线。2026-06-25 已完成公开 `P01-P05` 信息密度收口：降低桌面 `P01-P04` 的展示型字号、卡片 padding、section gap 和旧画板高度，补齐 `P01` 公开入口矩阵 / 运行态 rail，修正 `P04` 顶部双激活状态，并补齐 `P05` 移动继续探索 / 登录参与内容。已新增 [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design) 作为实现前口径；当前仍不进入视觉代码实现。
>
> 2026-06-27 补充：按 `web-ui-foundation.pen` 的 `F02` client 公共壳层契约，继续扩展 `public-web-unified-experience.pen` 至 `P01-P12`。新增 `P06-P09` PC 页面族矩阵，覆盖公开论坛、文档、商城 / 榜单和公开主页；新增 `P10-P12` 移动任务流，覆盖移动论坛 / 文档、商城 / 榜单和公开主页 / 来源返回。当前下一顺位转为补齐 `private-web-workflows.pen`，不进入代码实现。
>
> 2026-06-27 导航一致性回修：`P01-P04` 旧 64 高小标签式 PC header 已统一替换为 `F02` 84 高纸感横匾，保留品牌、公开导航、`/workbench` 和登录动作，并与 `P06-P09` 页面族矩阵保持同一 header 比例、nav pill 和 action rail。

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
- [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design)

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

### `P03 - Public Detail Reading`

职责：

- forum / docs 公开详情阅读基线。
- 详情标题、作者身份、元信息、正文、登录参与和状态槽顺序。
- 来源返回、复制公开链接和作者态入口边界。
- 文档公开阅读与论坛公开参与的差异点。

设计口径：

- 详情页以阅读主栏为主，不把作者工作台、治理台或桌面窗口能力塞进公开阅读页。
- `DisplayHandle` 面向用户展示，`PublicId` 只用于 URL、分享和传递。
- 新开标签和复制链接使用公开 URL，来源返回只存在于当前会话语义。
- forum 作者态和 docs 作者态分别由正式 Web 作者入口承接。

### `P04 - Public Collection Pages`

职责：

- forum 列表 / 搜索 / 分类、docs 搜索、公开个人页、榜单和公开商城浏览集合页基线。
- 集合页筛选、搜索、排序、分页、真实 URL 和状态槽。
- 移动端集合筛选折叠顺序的前置约束。

设计口径：

- 集合页 tab、筛选、搜索和分页需要可恢复到 URL 或明确 query 状态。
- 每个可导航项提供真实公开 `href`，普通点击保留壳层来源语义。
- 空结果、错误、加载、权限限制和登录参与必须有明确槽位。
- 公开商城不承载订单、背包、资产或完整私域商城工作台。

### `P05 - Mobile Public Single Column`

职责：

- 移动端公开 Web 单列阅读基线。
- 状态栏 / 公开头部、页面说明、来源返回、导航分组、主体内容、状态和登录参与顺序。
- 移动端底部导航参考和“非移动版 WebOS”边界。

设计口径：

- 移动端不依赖横向滚动承载主要筛选。
- 高级筛选向下展开，当前条件和搜索入口优先可见。
- 登录参与动作放在明确状态槽内，不打断公开阅读。
- 不搬运 Dock、窗口系统、桌面背景、窗口几何记忆或 WebOS app 外壳。

### `P06 - Public Forum Browse Matrix`

职责：

- 覆盖 `/forum`、`/forum/search`、`/forum/question`、`/forum/poll`、`/forum/lottery`、`/forum/tag/:slug`、`/forum/category/:id` 和 `/forum/post/:id?intent=*`。
- 固定公开论坛的路由 rail、列表 dominant region、筛选 / 搜索工具条和详情 intent 右侧 rail。
- 约束评论定位、轻回应、问答回答、作者编辑 / 历史查看的公开阅读边界。

设计口径：

- 列表页负责筛选、排序、分页和真实公开 `href`。
- 详情 intent 只承接公开阅读和登录参与，作者态动作跳正式 Web 作者路由。
- 空列表、登录参与和错误必须使用状态槽说明原因和恢复动作。

### `P07 - Public Docs Matrix`

职责：

- 覆盖 `/docs`、`/docs/search`、`/docs/:slug`、正文锚点和 `/__documents__/:slug` 保留 slug。
- 固定文档目录、搜索结果、详情阅读、右侧目录 / 作者入口 / 治理边界。

设计口径：

- 公开文档详情保持只读，编辑、发布、版本回看进入正式 Web 作者页。
- 权限限制、下线、未找到和加载失败必须给出明确原因。
- 正文内链继续改写为公开 URL，新开标签和复制链接不携带来源状态。

### `P08 - Public Commerce Leaderboard Matrix`

职责：

- 覆盖 `/shop`、`/shop/products`、`/shop/product/:id?intent=purchase` 和 `/leaderboard/:type`。
- 固定公开商城浏览、商品状态、登录购买回流、多类型榜单和榜单实体跳转。

设计口径：

- 公开商城只做浏览、详情和登录购买回流；订单、背包、资产流水进入正式私域路由。
- 商品售罄、下架、无购买资格和未登录必须有状态说明。
- 用户榜跳 `/u/:id`，商品榜跳商品详情，普通点击可保留来源返回。

### `P09 - Public Profile Source Matrix`

职责：

- 覆盖 `/u/:id` 公开主页、`posts / comments` tab、分页、关注登录回流和来源返回。
- 固定公开身份展示、内容列表、来源返回和分享 / canonical 边界。

设计口径：

- 普通展示优先 `DisplayHandle / DisplayName`；`PublicId` 只用于 URL、分享和传参。
- 关注动作需要登录并回到当前公开主页。
- 复制链接和新开标签不携带来源状态，普通点击可使用 history state 保留返回语义。

### `P10 - Mobile Forum Docs Flow`

职责：

- 移动端论坛 / 文档筛选摘要、列表卡片、详情阅读、高级筛选 sheet 和状态槽。

设计口径：

- 主要内容使用单列卡片，不依赖横向滚动。
- 搜索词、分类、排序和来源返回优先显示为当前条件摘要。
- 登录参与和空结果使用状态槽，不打断阅读。

### `P11 - Mobile Shop Leaderboard Flow`

职责：

- 移动端商城首页、商品详情、购买 intent、榜单切换和榜单实体跳转。

设计口径：

- 购买 intent 回流后突出主购买动作，并解释库存、资格、余额和登录状态。
- 榜单类型切换使用 route sheet，不把 PC 工具条缩进手机。
- 购买成功进入私域订单路由，公开页只提示结果和去向。

### `P12 - Mobile Profile Source Flow`

职责：

- 移动端公开主页身份头部、内容 tab、来源返回条、登录关注和分享公开链接。

设计口径：

- `DisplayHandle / DisplayName` 优先，PublicId 不作为普通展示名。
- 内容 tab、分页和分享链接都应可恢复公开 URL。
- 用户不存在、隐私限制和暂无公开内容必须有状态槽。

## 验证

Pencil 侧：

- `P01`：`snapshot_layout` 返回 `No layout problems.`
- `P01`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P01`：2026-06-25 按桌面壳层密度收口，压缩 header / context bar / 主卡片 / 右侧 rail，并补齐公开入口矩阵和运行态 rail；复查无布局问题。
- `P02`：首次生成后发现主体内容被画板高度裁切；已加高画板并复查。
- `P02`：修正 lucide 图标名 `check-circle-2` 为 `circle-check`。
- `P02`：复查 `snapshot_layout` 返回 `No layout problems.`
- `P02`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P02`：2026-06-25 按桌面信息密度收口，压缩 header / lead / toolbar / 卡片 padding / 右侧 rail 和画板高度；复查无布局问题。
- `P03`：`snapshot_layout` 返回 `No layout problems.`
- `P03`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P03`：2026-06-25 按桌面阅读页密度收口，压缩详情 lead、正文卡片 padding、段落字号和右侧 rail；复查无布局问题。
- `P04`：首次生成后发现 lucide 图标名 `check-circle-2` 不存在；已替换为 `circle-check`。
- `P04`：复查 `snapshot_layout` 返回 `No layout problems.`
- `P04`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P04`：2026-06-25 按集合页密度收口，压缩 lead / toolbar / 列表项 / 右侧 rail，修正顶部“论坛 / 文档”双激活为单一激活；复查无布局问题。
- `P05`：`snapshot_layout` 返回 `No layout problems.`
- `P05`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P05`：2026-06-25 按移动单列密度收口，补齐继续探索、公开商城和登录参与内容，收住底部留白；复查无布局问题。
- `P01-P04`：2026-06-27 按 `F02` 公共壳层契约统一 PC header 至 84 高纸感横匾；局部 `snapshot_layout` 均返回 `No layout problems.`，截图抽查 `P01 / P02 / P03` 与 `P06` header 风格一致。
- `P06`：`snapshot_layout` 返回 `No layout problems.`
- `P06`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P07`：`snapshot_layout` 返回 `No layout problems.`
- `P08`：`snapshot_layout` 返回 `No layout problems.`
- `P08`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P09`：`snapshot_layout` 返回 `No layout problems.`
- `P10`：`snapshot_layout` 返回 `No layout problems.`
- `P10`：截图目检未发现明显裁切、坍塌或横向溢出。
- `P11`：`snapshot_layout` 返回 `No layout problems.`
- `P12`：`snapshot_layout` 返回 `No layout problems.`
- `P12`：截图目检未发现明显裁切、坍塌或横向溢出。
- 全局：`public-web-unified-experience.pen` 复查 `snapshot_layout` 返回 `No layout problems.`

仓库侧：

```bash
git diff --check -- Docs/frontend/design-sources/README.md Docs/frontend/public-web-unified-experience-design.md Docs/records/p3-12-d2-public-web-unified-design-source-2026-06-24.md Docs/planning/current.md
```

结果：通过。

## 后续顺序

1. 以 [公开 Web 统一体验设计说明](/frontend/public-web-unified-experience-design) 和 `P01-P12` 作为公开 Web 视觉实现前口径。
2. 下一轮切换到 `private-web-workflows.pen` 后，补齐 `/workbench`、`/me` 子页、资产 / 订单、通知 / 消息 / 圈子 / 宠物、论坛作者态、文档作者态和移动私域任务流。
3. public / private 业务设计源和说明文档确认后，再进入 `radish.client` 视觉实现与 PC / mobile 复核。
4. Console 公共壳层与治理工作台代码实现按 [P3-12-D6 Console 视觉代码实现前盘点](/records/p3-12-d6-console-visual-code-prep-2026-06-27) 后移承接。

## 当前不做

- 不进入 `radish.client` 视觉代码实现。
- 不修改 Console 设计源。
- 不修改 `private-web-workflows.pen`；下一轮由用户切换到该文件后再补。
- 不把 `/desktop` 或 WebOS Dock / 窗口系统纳入公开 Web 视觉基线。
- 不把公开入口改成营销首页或品牌宣传页。
