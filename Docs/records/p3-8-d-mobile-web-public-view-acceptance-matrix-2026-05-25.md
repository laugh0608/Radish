# P3-8-D 移动 Web 公开视图验收矩阵（2026-05-25）

> 本记录用于承接 `P3-8-D` 首批移动 Web 公开视图验收矩阵。
>
> 当前批次先做代码静态审阅和验收矩阵收口；在用户启动前后端开发服务器后，已补充一次 `radish.client` Vite 直连移动视口实测。本记录不修改运行时代码。

## 结论

`/discover / forum / docs / u/:id / leaderboard / shop` 已有分散的专题验收清单和公开壳层代码基础，可以进入统一移动 Web 主路径验收。

首轮静态审阅与 `390 x 844` Vite 直连实测均未发现必须立即抢占主线的 `P0/P1` 阻断，但发现一个与新路线强相关的高信号问题：

- 公开页顶部 `PublicShellHeader` 的 `WebOS` 链接在本次实测时仍指向 `/`。当根路径 `/` 切到纯 Web 默认入口后，该链接会失去“进入工作台”的语义，应在 `/` 切换前改为明确指向 `/desktop`，并把文案从 `WebOS` 收敛为“工作台”或等价表述。

该问题已在后续小闭环中修正为 `/desktop` + “工作台”。因此本批后续建议顺序为：

1. 先按本矩阵补齐 `430 x 932` 或 Gateway 入口复核。
2. 根路径 `/` 已切向 `/discover` 公开分发页，继续复核切换后是否还有其他依赖 `/` 的公开页内部入口。
3. 再进入纯 Web 登录后轻量链路或 Flutter 下一批高价值功能评估。

## 验收维度

| 维度 | 通过标准 |
| --- | --- |
| 窄屏信息密度 | `390 x 844` 与 `430 x 932` 下首屏标题、说明、核心列表 / 卡片、按钮不重叠、不横向撑破，长标题、长 slug、长用户名、长商品名可控显示 |
| 公开直达 | 直接访问公开路径时进入 `PublicEntry`，不先进入 WebOS `RootEntry` 或桌面窗口系统 |
| 来源返回 | 从 `/discover`、榜单、个人页、搜索页、列表页进入详情后，返回按钮优先回到真实来源，而不是退回专题默认页 |
| 分享入口 | 详情类页面复制 canonical 链接；非详情页若无显式分享，至少保持 URL 可直达、head/canonical 可用，并记录是否需要后续补入口 |
| 公开只读边界 | 不误暴露发帖、评论提交、投票提交、购买、订单、背包、编辑、版本治理、账号治理等登录后 / 工作台动作 |
| 公开链接契约 | 地址栏、canonical、复制链接、详情跳转和来源返回使用同一公开路由语义，不混入桌面窗口参数 |
| 根路径切换影响 | 页面内“工作台 / WebOS / 桌面”入口不得再依赖 `/`；根路径切到纯 Web 后仍能明确进入 `/desktop` 保留入口 |

## 端点矩阵

| 端点 | 现有资料 | 代码入口 | 静态审阅判断 | 人工实测重点 | 风险 |
| --- | --- | --- | --- | --- | --- |
| `/discover` | [discover-public-acceptance](/records/discover-public-acceptance) | `PublicDiscoverApp.tsx`、`discoverRouteState.ts` | 分发页已覆盖 forum / docs / leaderboard / shop 摘要、区块预览和公开页直达；代码中有区块记忆与来源回流设计 | 首屏摘要卡信息密度、摘要卡主区域和“直接进入公开页”按钮是否容易混淆、从专题页回到上次区块是否成立 | 中 |
| `/forum` | [forum-public-mobile-acceptance](/records/forum-public-mobile-acceptance) | `PublicForumApp.tsx`、`PublicForumList.tsx`、`PublicForumDetail.tsx`、`forumRouteState.ts` | 公开 forum 已覆盖列表、分类、标签、类型、搜索、详情和来源返回；详情页分享已存在 | 长标题 / 多标签帖子、类型页横向筛选、详情返回 `/discover` / `/u/:id` / 列表上下文、评论阅读区窄屏节奏 | 中 |
| `/docs` | [docs-public-acceptance](/records/docs-public-acceptance) | `PublicDocsApp.tsx`、`docsRouteState.ts`、`publicDocsApi.ts` | 公开 docs 已覆盖目录、搜索、详情、旧 `__documents__` 兼容和 canonical 复制 | 搜索结果回跳、正文长链接 / 长 slug、正文内链和锚点、从 `/discover` 返回来源 | 中 |
| `/u/:id` | [profile-public-acceptance](/records/profile-public-acceptance) | `PublicProfileApp.tsx`、`profileRouteState.ts` | 公开个人页已覆盖资料、帖子 / 评论页签、榜单 / forum 来源返回；当前没有显式分享入口 | 头像 / 昵称 / 长 ID、页签分页 URL、榜单进入后返回、本人 / 他人登录态识别；评估是否需要补个人页分享入口 | 中 |
| `/leaderboard` | [leaderboard-public-acceptance](/records/leaderboard-public-acceptance) | `PublicLeaderboardApp.tsx`、`leaderboardRouteState.ts` | 公开榜单已覆盖类型切换、分页、用户榜单到公开个人页；商品榜单保持只读不进购买链路 | 榜单行在 `390px` 下是否拥挤、分页是否可点、用户榜单到 `/u/:id` 返回是否保留来源 | 中 |
| `/shop` | [shop-public-acceptance](/records/shop-public-acceptance) | `PublicShopApp.tsx`、`shopRouteState.ts` | 公开商城已覆盖首页、商品列表、商品详情、详情分享和来源返回；只读边界明确 | 商品图片、价格 / 库存 / 时效文本、列表筛选分页 URL、详情从 `/discover` 或列表返回、工作台入口语义 | 中 |

## 横向发现

| 发现 | 影响 | 建议 |
| --- | --- | --- |
| `PublicShellHeader` 的工作台入口曾是 `<a href="/">WebOS</a>` | 根路径 `/` 切到纯 Web 后，公开页顶部入口会回到当前纯 Web 页面而不是 `/desktop`，且 `WebOS` 文案不符合新路线 | 已在后续小闭环中改为 `/desktop`，默认文案改为“工作台”，图标态补充 `aria-label` / `title` |
| `/u/:id` 没有显式分享入口 | 公开个人页是增长入口之一，但当前只依赖浏览器地址栏可复制；是否需要显式分享未重新评估 | 人工实测后若个人页承担跨端回流，列入 P2 小修；不要抢在工作台入口修正前 |
| `/leaderboard` 和 `/discover` 没有显式分享动作 | 列表 / 分发页天然可直达，但分享入口标准弱于详情页 | 暂不作为首批修复；仅在实测确认分享是高频入口时回拉 |
| 公开页多处文案仍使用“桌面工作台 / WebOS”作为边界说明 | 新路线后这些文案仍可表达“重功能在工作台”，但需要避免暗示 WebOS 是主线 | 跟随 `/desktop` 入口修正一起抽查，必要时小范围改为“工作台保留入口” |
| 根路径 `/` 实测时仍落到 `RootEntry`，Tauri 环境才替换到 `/desktop` | 与新规划不冲突，因为代码切换被拆成后续批次；但矩阵需要提前识别依赖 `/` 的入口 | 已在后续小闭环中将普通浏览器 `/` 切向 `/discover`，Tauri 当前仍保留 `/desktop` |

## 开发服务器实测记录

- 验收日期：2026-05-25
- 环境：`radish.client` Vite 直连 `http://localhost:3000`
- 视口：`390 x 844`
- 覆盖入口：`/discover`、`/forum`、`/docs`、`/leaderboard`、`/shop`、`/u/1`、`/desktop`
- 结果：未发现 `P0/P1` 阻断
- 直接访问：
  - `/discover`、`/forum`、`/docs`、`/leaderboard`、`/shop` 均进入公开壳层
  - `/u/1` 在当前样本下显示“用户主页不存在”，属于样本数据不足，不是路由阻断
  - `/desktop` 可作为 WebOS 保留入口访问
- 窄屏布局：
  - 已测页面 `documentElement.scrollWidth` 与 `body.scrollWidth` 均为 `390`，未发现页面级横向撑破
  - `/leaderboard` 榜单类型按钮超出视口，但超出元素均位于 `_tabRail` 横向滚动容器内，属于局部横向 tab 滚动，不是页面级溢出
- 公开边界：
  - 页面文案保持只读边界，未在首屏误暴露发帖、评论提交、购买、订单、背包或治理动作
  - 公开页顶部工作台入口实测时仍是 `href="/"`，与根路径转纯 Web 后的路线冲突；该问题已在后续小闭环中修正

补充 Gateway 根路径复核：

- 验收日期：2026-05-25
- 环境：Gateway `https://localhost:5000`
- 结果：
  - `https://localhost:5000/` 已进入 `/discover` 公开分发页
  - `https://localhost:5000/desktop` 仍进入 WebOS 工作台保留入口
  - `http://localhost:3000/?demo` 仍保留在早期登录 / 通知测试页，没有被根路径切换覆盖

## 后续实测前置

需要用户手动启动服务，AI 协作者不直接执行启动命令。

推荐本地联调入口：

```bash
pwsh ./start.ps1
npm run dev --workspace=radish.client
```

或按需要分别启动：

```bash
dotnet run --project Radish.Api
dotnet run --project Radish.Auth
dotnet run --project Radish.Gateway
npm run dev --workspace=radish.client
```

默认访问：

- Gateway：`https://localhost:5000`
- Vite 直连：`http://localhost:3000`

人工视口：

- `390 x 844`
- `430 x 932`

最小样本数据：

- 至少 1 条公开帖子，最好包含长标题、多标签、轻回应和评论
- 至少 1 篇公开文档，最好包含长 slug、正文内链或锚点
- 至少 1 个公开用户主页，含公开帖子和公开评论
- 至少 1 个用户榜单和 1 个商品榜单
- 至少 1 个公开商品详情

## 实测记录模板

```md
- 验收日期：2026-05-25
- 验收人：<name>
- 环境：Gateway / Vite 直连
- 视口：390 x 844 / 430 x 932
- 覆盖入口：/discover、/forum、/docs、/u/:id、/leaderboard、/shop
- 结果：通过 / 阻塞 / 有 P2 问题
- 高信号问题：
  - <端点>：<问题、复现步骤、影响判断>
- 建议小闭环：
  - <若无更高阻断，默认选择 PublicShellHeader 工作台入口修正>
```
