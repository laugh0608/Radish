# 公开 Web 统一体验设计说明

> 日期：2026-06-25（Asia/Shanghai）
>
> 状态：设计源 `P01-P12` 已补齐；当前作为实现前说明，不进入视觉代码实现

## 设计源

```text
Docs/frontend/design-sources/public-web-unified-experience.pen
```

画板：

| 画板 | 职责 |
| --- | --- |
| `P01 - Public Web Shell Foundation` | 公开 Web 壳层、品牌头部、导航动作、来源返回和弱纹样边缘 |
| `P02 - Discover Content Stream` | `/discover` 公开内容流、类型筛选、搜索排序、内容分区和状态槽 |
| `P03 - Public Detail Reading` | forum / docs 公开详情阅读、作者身份、来源返回、登录参与和作者态边界 |
| `P04 - Public Collection Pages` | forum 集合、docs 搜索、公开个人页、榜单和公开商城浏览的筛选 / 列表基线 |
| `P05 - Mobile Public Single Column` | 移动端公开 Web 单列阅读、筛选摘要、来源返回、登录参与和底部导航参考 |
| `P06 - Public Forum Browse Matrix` | forum 列表、分类、标签、搜索、问答 / 投票 / 抽奖和详情 intent 路由矩阵 |
| `P07 - Public Docs Matrix` | docs 目录、搜索、详情、锚点、保留 slug 和作者 / 治理边界 |
| `P08 - Public Commerce Leaderboard Matrix` | 公开商城浏览、商品详情、登录购买回流和多类型榜单 |
| `P09 - Public Profile Source Matrix` | 公开主页、内容 tab、身份展示、关注登录回流和来源返回 |
| `P10 - Mobile Forum Docs Flow` | 移动端论坛 / 文档筛选、列表、详情阅读和状态槽任务流 |
| `P11 - Mobile Shop Leaderboard Flow` | 移动端商城 / 榜单浏览、商品详情、购买 intent 和榜单跳转任务流 |
| `P12 - Mobile Profile Source Flow` | 移动端公开主页、来源返回、关注登录、分享和公开内容状态流 |

## 目标

- 让公开 Web 在 PC / mobile 浏览器中形成统一、可发布的阅读和浏览体验。
- 公开页优先服务内容阅读、真实 URL、分享传播和登录后轻参与，不做营销首页。
- 保持正式 Web 为默认产品路径，`/desktop` 只作为 WebOS 历史入口维护。
- 在进入视觉代码前，先固定信息架构、响应式顺序、身份展示和状态槽位。

## 统一契约

### 壳层与导航

- 公开头部保留品牌、公开导航、`/workbench` 工作台动作和登录动作。
- PC 公开头部统一使用 `web-ui-foundation.pen` / `F02` 的 84 高纸感横匾：横排图标 nav rail、激活态 pill、身份 action rail 和 32px 内收，不再保留旧 64 高小标签式导航。
- “工作台”默认进入 `/workbench`，不直接打开 `/desktop`。
- 公开浏览页的主动作是继续阅读、筛选、搜索、分享或登录参与，不承载完整私域工作台。
- 弱纹样只作为边缘收边和分区提示，不抢正文和操作层级。

### 真实链接

- 可导航内容必须提供真实公开 `href`。
- 普通点击可以通过会话状态保留来源返回。
- 新开标签、复制链接、canonical、OpenGraph、JSON-LD 和 sitemap 不携带来源状态。
- 公开集合页的 tab、搜索、排序和分页应能恢复到 URL 或明确的 query 状态。

### 身份展示

- 用户可见身份优先展示 `DisplayHandle`。
- `PublicId` 只用于 URL、分享和前后端传递，不作为普通展示名回退。
- 公开个人页只承载公开资料、公开内容和轻关注 / 来源返回，不进入资料编辑、账号设置或历史治理。

### 公开详情

- forum / docs 详情页以阅读主栏为 dominant region。
- 标题、作者 / 来源、更新时间、正文、登录参与和状态反馈保持固定顺序。
- forum 作者态入口通过正式 Web 路由承接，文档作者态入口通过正式 Web 作者页承接。
- 文档公开详情保持只读；编辑、发布、版本治理、权限策略和回滚归作者页或 Console。

### 公开集合

- forum 列表 / 搜索 / 分类、docs 搜索、公开个人页、榜单和公开商城浏览共享筛选、列表、分页和状态槽节奏。
- 空结果、错误、加载、权限限制和登录参与必须有明确说明，不出现无解释空白页。
- 公开商城只承载浏览、商品详情和登录购买回流；订单、背包和资产进入正式私域 Web 路由。

### 公开页面族矩阵

- 论坛页面族覆盖 `/forum`、`/forum/search`、`/forum/question`、`/forum/poll`、`/forum/lottery`、`/forum/tag/:slug`、`/forum/category/:id` 和 `/forum/post/:id?intent=*`。
- 文档页面族覆盖 `/docs`、`/docs/search`、`/docs/:slug`、正文锚点和 `/__documents__/:slug` 保留 slug 场景。
- 商城 / 榜单页面族覆盖 `/shop`、`/shop/products`、`/shop/product/:id?intent=purchase` 和 `/leaderboard/:type`。
- 公开主页覆盖 `/u/:id`、`posts / comments` tab、分页、关注登录回流和来源返回。
- 公开页面族可以共享壳层、筛选、卡片、分页和状态槽，但每个路由族必须有自己的 dominant region 和主动作。

### 移动单列

- 移动端按单列顺序展示：状态栏 / 公开头部、页面说明、来源返回、导航分组、主体内容、状态和登录参与。
- 筛选先展示当前条件和搜索入口，高级筛选向下展开，不依赖横向滚动。
- 移动端不搬运 Dock、窗口系统、桌面背景、窗口几何记忆或 WebOS app 外壳。
- 论坛 / 文档、商城 / 榜单和公开主页分别有移动任务流参考；不要把 PC 三栏直接缩进手机。

## 视觉约束

- 继续使用 `rx-*` 设计变量，不新增未说明的硬编码颜色。
- 纸色背景、低饱和边框、克制品牌红、玉色 / 墨蓝 / 土色辅助色保持当前视觉口径。
- 卡片只用于内容项、状态块和必要工具面板，不把页面区块层层包成卡片。
- 按钮、筛选 pill、状态面板和移动触控目标保持稳定尺寸，避免文本挤压或布局跳动。

### 桌面密度

- 公开 Web 桌面画板按 `1920x1080` 浏览器视角判断首屏密度，不按展示型海报比例放大字号和卡片。
- 页面标题控制在产品级层级，公开页 H1 约 `28-32`，分区标题约 `20-24`，内容卡片标题约 `16-18`。
- 桌面端 lead 区、工具条、筛选行和卡片内边距应服务快速浏览：主要卡片 padding 约 `14-18`，列表 gap 约 `10-14`。
- 右侧 rail 不用大块空面板撑满高度；优先使用紧凑状态、推荐项、来源返回摘要或登录参与入口。
- 公开壳层、内容流和集合页首屏应展示多条真实内容项，不能主要由大标题、宽卡片和说明块占据。

### 移动密度

- 移动端保持单列阅读和稳定触控目标，不用桌面三栏或横向滚动承载主要内容。
- 页面说明、筛选 pill、内容项、状态反馈和登录参与应连续出现，避免主体内容后出现大块空白。
- 底部导航只保留主要公开入口，内容区必须在底栏前提供明确的继续探索或参与动作。

## 实现顺序

1. 先以本说明和 `.pen` 画板确认公开 Web 视觉实现边界。
2. 继续补齐 `private-web-workflows.pen`，让 client public / private 设计源在页面族和移动任务流上对齐。
3. public / private 业务设计源确认后，再进入 `radish.client` 公开壳层和相关页面的视觉代码实现。
4. 实现时优先抽取共享 token、公开壳层结构、筛选 / 状态组件和移动单列节奏。
5. 完成后执行 `radish.client` 类型检查 / 构建；准备阶段性验收时再按用户确认的前后端启动状态执行 Gateway PC / mobile smoke。

## 当前不做

- 不绕过 Pencil 设计稿直接做跨页面视觉改造。
- 不把公开入口改成营销首页。
- 不启动 WebOS 全量迁移或移动版 WebOS。
- 不把完整钱包、账号安全设置、治理台、发布 / 回滚 / 权限策略混入公开页。
- 不启动 Flutter 完整承接、推荐算法、ActivityPub / WebFinger 或完整 PWA。
