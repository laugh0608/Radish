# 第三开发阶段：真实使用增长与长期契约治理

> 状态：`P3-12` Web 完全化与 WebOS 收束；`P3-11` 已暂缓 PR 后收束
>
> 启动日期：2026-05-13（Asia/Shanghai）
>
> 本页承载第三开发阶段的目标定义、边界和阶段记录。后续多端功能与 UI 设计治理方向见 [P3-8 多端功能补全与 UI 设计治理](/planning/p3-8-multiplatform-feature-ui-governance)。快速入口仍以 [当前进行中](/planning/current) 为准；第二阶段事实以 [第二阶段收口评审](/planning/phase-two-closure-review) 与 [已完成摘要](/planning/archive) 为准。

## 阶段判断

第二开发阶段已经完成公开内容壳层、Flutter Android MVP、Tauri + WebOS 桌面壳验证、多端分工和产品治理收口。`2026-05-25` 路线复盘后，第三阶段后续投入进一步收敛为纯 Web + Flutter 主线：根路径 `/` 与默认浏览器入口转向纯 Web，`/desktop` 仅保留为 WebOS 历史入口，PC/Tauri 放到最后再评估且不再绑定 WebOS。当前不应继续把第二阶段低收益尾项、WebOS 扩张或 PC 分发材料当成默认主线，也不应在缺少边界定义时直接开大功能。

第三开发阶段的核心问题不再是“某个入口能不能做出来”，而是：

1. 真实用户能否通过纯 Web 公开内容、移动 Web 访问和 Flutter 移动复访形成持续使用。
2. 多端之间的路由、对象标识、登录回流和公开链接契约是否足够稳定。
3. 现有代码体量、验证入口和文档入口是否还能支撑继续扩展。

因此第三阶段主题暂定为：**真实使用增长与长期契约治理**。

## 当前推进状态

`P3-0` 已完成第三阶段定义、公开内容增长基础审计和第一批任务排序；`P3-1` 已完成公开内容 SEO 与分享基线。`P3-2` 已完成 `P3-2-A` 外部 ID 契约审计和 `P3-2-B` `Post.PublicId` 首批实现，试点对象保持收敛为 `Post`。`P3-3` 已完成 `PublicForumApp.tsx` 公开论坛热区首轮拆分和收工复核。`P3-4` 已完成 forum / docs / shop 留存回流矩阵首轮主动验收和补洞。

`P3-5 公开内容增长后续专题` 已于 `2026-05-17` 阶段收尾：`P3-5-A` 评估、`P3-5-B` 运行时结构化数据基线、`P3-5-C` 动态 sitemap 首批实现、`P3-5-D` 详情首包 HTML 可见性方案评审、`P3-5-D1` 公开详情 HTML head 快照注入首批实现和 `P3-5-D2` 部署前 smoke 入口均已完成。最新公开 head smoke 已通过 robots、sitemap、forum / docs / shop 三类详情。动态 sitemap 已采用 API + Gateway 路由，构建期静态生成器仅保留为离线 / 夜间导出备选；详情首包 HTML 可见性首批只覆盖 forum / docs / shop 详情 head 注入，不启动完整 SSR / SSG 或正文预渲染。

`P3-6 真实使用运营观察与反馈分流` 已阶段收口：本地 Gateway 与生产公开域名 `https://radishx.com` 均已通过 public head smoke；后续只从真实部署、真实内容、爬虫抓取、分享预览、用户回流和运行日志中挑选高信号问题，公开内容增长能力转入部署 / 运营维护线。

`P3-7-A / P3-7-B` 已完成 WebOS / PC 工作台复访小闭环和高信号候选筛查，当前未发现新的 `P0/P1`。`P3-7-C` 已完成近期开发任务重评估与首批热区治理：`WikiApp`、`ChatApp`、`ContentModerationService` 与 `ExperienceService` 均已完成首批行为等价拆分。继续硬拆 `ExperienceService` 经验发放主流程的收益下降、风险上升，因此 `P3-7-C3` 作为当前主线阶段收口。

`P3-8-A/B/C` 已完成审计、Flutter 榜单首批、Console 设计端点、治理工作台结构基座和高频页面类型试点；`P3-8-D` 已完成移动 Web 公开视图矩阵阶段收口、根路径 `/` 默认入口切换、纯 Web 参与 / 购买回流补强，以及 Flutter 多条真实移动主路径补齐。`P3-9` 已完成访客公开访问、Flutter 登录移动用户和 Console 管理员三类真实使用主路径的自动化总回归、人工复核与 `dev -> master` 合并。本轮明确跳过发布，不创建 tag、不进入 M15 测试 / 生产部署流程；项目仍处于功能建设期，不进入生产稳定运营。`P3-10` 已完成 Web-first 信息架构、公开入口治理、用户身份语义、系统设置治理、写操作可靠性、论坛内容写入可靠性和 Flutter 首批受控承接，并进入阶段收束准备；当前暂缓 `dev -> master` PR，不继续默认追加功能入口或第五批链接语义扫尾。`P3-11` 已完成发布候选验收矩阵、轻量复访缺口只读审计和暂缓 PR 后的阶段收束，未触发定向回修。当前阶段约束调整为进入 [P3-12 Web 完全化与 WebOS 收束](/planning/p3-12-web-completion-webos-retirement)：目标是在 PC / mobile 浏览器中完成项目正式版主路径，让 `/desktop` 退为历史兼容入口；功能迁移只迁移产品能力，不迁移 WebOS 形态能力，UI 设计与美化专题必须先使用 Pencil 设计稿，再更新设计 / 说明文档，最后进入实现。

## `P3-0` 定义与工程整备

`P3-0` 是第三阶段定义与工程整备窗口，不直接铺大功能。建议周期控制在 `2-4` 个工作日。

### 目标

- 明确第三阶段第一批正式主任务。
- 做一次窄范围主路径与代码热区审计。
- 把候选任务按产品价值、契约风险、维护成本和验证成本排序。
- 给出第一批可开工任务的完成条件与验证口径。

### 不做

- 不继续追加第二阶段 Flutter 低收益微体验。
- 不继续无边界扫 WebOS / Console / 公开壳层零碎按钮、提示或样式。
- 不启动完整 `PublicId` 全量迁移。
- 不启动完整联邦、完整 PWA、完整 E2E、完整可观测性平台或开放平台。
- 不把 Tauri 签名、自动更新、SmartScreen、托盘 / 菜单和公开分发材料提前拉回主线。

## `P3-0` 审计范围

本轮只审计会影响第三阶段主线判断的范围，不做全仓 TODO 清扫。

| 范围 | 审计重点 | 产出 |
| --- | --- | --- |
| 公开内容壳层 | `/discover`、forum、docs、`/u/:id`、leaderboard、shop 的公开直达、来源返回、分享与 SEO 基础 | 判断是否进入 `P3-1 公开内容增长基础` |
| WebOS 桌面工作台 | forum、chat、shop、radish-pit、notification、profile 的已登录主路径 | 只登记成片工作流阻断，不登记低收益边角 |
| Flutter 移动端 | discover / forum / docs / profile、OIDC、handoff、复访与通知回流 | 判断是否只维护 Android MVP，或进入移动留存小闭环 |
| Console / 后端治理 | 权限、内容治理、商城、经验治理、安全授权一致性 | 只登记安全、授权或治理数据可信度问题 |
| 外部 ID 契约 | 公开路由、通知 `extData`、窗口参数、深链、分享链接和前端 `Number(...)` 边界 | 判断 `PublicId` 最小试点边界 |
| 代码热区 | 超大前端页面、超大 Service、Flutter 大页面与测试覆盖 | 排出首批拆分 / 降复杂度候选 |

## `P3-0` 初始审计快照

审计日期：2026-05-13。

本轮先按文档入口、近期提交、目录结构、测试资产和代码热区做初始审计，结论如下：

- 规划入口已经一致确认第二阶段归档 Go，当前不存在必须继续回拉第二阶段尾项的文档冲突。
- 近期提交已经从安全、商城、构建 warning、Flutter 小闭环切到第二阶段归档与下一阶段入口对齐，说明继续补洞的收益开始递减。
- 当前未发现新的资产、安全、登录、购买、转账、权限授权或主路径中断 `P0/P1` 阻断项。
- 代码中仍有 mock / TODO / 占位痕迹，例如 `radish-pit` 未调用通知 hook、通知未读按类型统计、文件访问令牌管理员权限、部分展示型 mock；这些暂归维护池，不作为第三阶段首批主线。
- 代码热区已经明显：`PublicForumApp.tsx`、`ExperienceService.cs`、`ContentModerationService.cs`、`ChatApp.tsx`、`WikiApp.tsx`、Flutter `forum_detail_page.dart` 和 `profile_page.dart` 等文件已超过或接近项目建议维护边界。
- 验证资产较完整：后端有 `Radish.Api.Tests` 与专题 `HttpTest`，前端有 `type-check / node --test`，Flutter 有 topic tests 与 `smoke_test`；第三阶段不需要先建设完整 E2E 平台才能继续推进。

初始判断：`P3-0` 的第一批开工不应从新增业务页开始，而应先把 `P3-1 / P3-2 / P3-3` 的范围、验收和风险写成可执行小批次。

## `P3-0-A` 公开内容增长基础审计

审计日期：2026-05-13。

### 现状

| 范围 | 当前事实 | 判断 |
| --- | --- | --- |
| HTML 基线 | `Frontend/radish.client/index.html` 仅有固定 `<title>radish.client</title>`、viewport 和 runtime config 脚本 | 搜索引擎和社交爬虫首包只能看到通用 SPA 壳 |
| 运行时标题 | discover、forum、docs、profile、leaderboard、shop 均有 `document.title` 更新 | 对浏览器标签页有效，但不等同于服务端可见 SEO |
| meta / Open Graph | 未发现统一 `meta description`、`og:title`、`og:description`、`og:image`、`twitter:*` 管理 | 分享卡片和搜索摘要缺统一契约 |
| canonical | 公开路由已有 `buildPublic*Path` 与 `replaceState` 归一化；docs detail、forum tag、shop products/detail 等局部有 canonical route 收口 | URL 归一化已具备基础，但缺 `<link rel="canonical">` 输出 |
| 分享入口 | docs 公开详情已有复制 canonical 公共链接；forum / profile / shop / discover 暂未形成统一分享动作 | 分享能力不一致，无法支撑公开内容增长第一批 |
| 来源返回 | `PublicEntry` 已记录 discover / docs / forum / profile / shop 的来源返回动作 | 公开壳层回流基础可复用，不需要重做路由系统 |
| sitemap / robots | `Frontend/radish.client/public` 未提供 `robots.txt` / `sitemap.xml`；Gateway 未定义专门路由；生产前端静态服务会对未知路径回退 `index.html` | 当前没有搜索引擎抓取入口约束 |
| 承载位置 | Gateway catch-all 会把未匹配路径转到 frontend；Frontend 生产静态服务可直接服务 dist 内静态文件 | 静态 `robots.txt` / sitemap seed 可先放前端，动态 sitemap 需要后续单独设计 |

### 关键结论

- `P3-1` 不应第一步改 SSR / SSG。当前风险更基础：没有统一 `head` 契约、没有 `robots.txt`、没有 sitemap seed、分享入口不一致。
- 第一批应先做“静态可见 + SPA 运行时一致”的增长基础：静态 `index.html` 品牌基线、统一 head helper、静态 robots、sitemap seed 和公开详情分享入口。
- 动态 sitemap、SSR / SSG、JSON-LD 和内容详情首包 HTML 属于第二批或专题评估；它们需要后端查询、缓存、部署和爬虫策略一起定，不应在 `P3-1` 第一批直接铺开。
- 公开内容增长基础应保持只读边界，不把购买、发帖、评论提交、个人治理、订单或背包动作带进公开壳层。

### `P3-1` 第一批建议

建议把 `P3-1` 第一批命名为：**公开内容 SEO 与分享基线**。

建议范围：

1. 建立 `publicHead` 前端小工具，统一写入：
   - `document.title`
   - `meta[name="description"]`
   - `link[rel="canonical"]`
   - `meta[property="og:title"]`
   - `meta[property="og:description"]`
   - `meta[property="og:url"]`
   - 可选 `og:image`，无业务图时使用站点默认图
2. 为公开壳层首批页面接入运行时 head：
   - `/discover`
   - `/forum` 与 forum 列表类路由
   - `/forum/post/:postId`
   - `/docs`、`/docs/search`、`/docs/:slug`
   - `/u/:id`
   - `/shop`、`/shop/products`、`/shop/product/:productId`
3. 在前端静态产物中加入第一版 `robots.txt`：
   - 允许公开内容壳层。
   - 禁止 `/console/`、OIDC 回调、登录后工作台治理类路径。
   - 指向当前公开域名下的 sitemap。
4. 加入第一版静态 sitemap seed：
   - 至少包含 `/discover`、`/forum`、`/docs`、`/docs/search`、`/leaderboard`、`/shop`、`/shop/products`。
   - 详情页动态 sitemap 先不做，后续由 API / 构建任务或独立生成器承接。
5. 统一公开详情分享入口策略：
   - docs 已有复制链接，优先抽出可复用的复制链接反馈。
   - forum detail 与 shop detail 首批补复制 canonical 链接。
   - profile 是否开放分享入口在第一批内只做判断，不强制补。

完成条件：

- `npm run type-check --workspace=radish.client` 通过。
- `npm run build --workspace=radish.client` 通过。
- 新增或更新 `node --test` 覆盖 head helper、canonical URL 构造和 robots / sitemap 静态产物关键内容。
- 人工检查至少覆盖 `/discover`、`/forum/post/:postId`、`/docs/:slug`、`/shop/product/:productId` 的 title、description、canonical 与复制链接。

明确不做：

- 不做 SSR / SSG 架构迁移。
- 不做动态详情 sitemap。
- 不做 JSON-LD。
- 不把公开壳层扩成发帖、购买、订单、背包或治理工作台。

### `P3-1-A` 公开 head 与抓取入口基线

完成日期：2026-05-13。

已完成：

- 新增 `Frontend/radish.client/src/public/publicHead.ts`，统一公开路由的 `document.title`、`meta description`、Open Graph、`link[rel="canonical"]` 与 canonical URL 构造。
- `PublicEntry` 已按公开路由接入 `publicHead`，覆盖 discover、forum、docs、profile、leaderboard、shop。
- `Frontend/radish.client/index.html` 已改为中文语言基线和 Radish 默认 meta / Open Graph 基线。
- 新增 `Frontend/radish.client/public/robots.txt` 与 `Frontend/radish.client/public/sitemap.xml`，先以 `https://radishx.com` 作为当前公开域名 seed。
- 新增 `publicHead` 与静态 SEO 文件测试，纳入 `radish.client` 现有 `node --test` 入口。

验证：

- `npm run type-check --workspace=radish.client` 通过。
- `npm run test --workspace=radish.client` 通过，`126/126`。
- `npm run build --workspace=radish.client` 通过，仍有既有 `app-shop` chunk size warning。
- `npm run check:repo-hygiene:changed` 通过，保留既有 `Docs/frontend/design.md` 与 `Docs/guide/notification-api.md` 篇幅提醒。
- `git diff --check` 通过。

### `P3-1-B` 公开详情分享入口

完成日期：2026-05-13。

已完成：

- forum detail 已新增复制 canonical 链接按钮，支持 busy / success / error 反馈。
- shop detail 已新增复制 canonical 链接按钮，支持 busy / success / error 反馈。
- docs detail 既有复制链接能力保持不变，三类公开详情的分享入口口径已经对齐。
- 中英文 i18n 已补齐 forum / shop 公开分享文案。

验证：

- `npm run type-check --workspace=radish.client` 通过。
- `npm run test --workspace=radish.client` 通过，`126/126`。
- `npm run build --workspace=radish.client` 通过，仍有既有 `app-shop` chunk size warning。
- `npm run check:repo-hygiene:changed` 通过。

后置评估：

- 动态 sitemap、详情页结构化数据、SSR / SSG 与详情首包 HTML 可见性进入后续专题，不并入 `P3-1` 首批。
- profile 分享入口暂不补；公开个人页仍以来源返回和公开阅读为主。

## `P3-2-A` 外部 ID 契约审计与最小试点方案

审计日期：2026-05-13。

### 暴露面审计

| 暴露面 | 当前事实 | 试点判断 |
| --- | --- | --- |
| 公开路由 | forum 使用 `/forum/post/:postId`，shop 使用 `/shop/product/:productId`，profile 使用 `/u/:userId`；docs 已使用 `slug` 作为公开详情主路由 | forum post 是公开传播、通知回流和浏览历史的交汇点，最适合作为第一试点 |
| 通知 `extData` | forum / chat 导航 `extData` 已显式把 ID 写成字符串；通知主体仍返回 `businessId / triggerId` 等 `long` 语义字段 | 试点只新增 forum post 的 `postPublicId`，继续保留 `postId / businessId` 兼容 |
| 窗口参数 | forum / chat `appParams` 已按字符串安全解析；profile / shop 仍以目标 ID 打开 | 首批只扩展 forum 窗口参数，不牵动 profile / shop |
| 分享链接 | forum detail 和 shop detail 复制 canonical 链接；forum 当前仍复制 long 版帖子路径 | 试点可让 forum 详情在拿到 `VoPublicId` 后输出 PublicId canonical，long 路径继续可读 |
| API 返回 | `PostVo / ProductVo / UserPublicProfileVo / WikiDocumentVo / UserBrowseHistoryVo / NotificationVo` 仍暴露内部 `long` 或 `LongId`；docs 同时有 `slug` | 首批只为 `PostVo` 增加 `VoPublicId`，不删除 `VoId` |
| 浏览历史 / 回跳 | `UserBrowseHistory.RoutePath` 已记录公开路径，`TargetId` 仍是内部 ID；Post / Product / Wiki 分别由后端构建 routePath | Post 试点后只调整 Post routePath 为 PublicId canonical，`TargetId` 继续保留内部关联 |

### 最小试点对象

首批只选 `Post`。

原因：

- 公开传播价值最高：`/forum/post/:postId` 是当前公开分享、SEO、通知回流和个人公开页回跳共同依赖的核心内容对象。
- 兼容面可控：现有前端已经把 `postId / commentId` 当字符串处理，后端也已对 `long` JSON 输出做字符串化，试点可在不改主键的前提下平滑并行。
- 风险低于 `User / Product`：`User` 牵涉身份、关注、头像、权限和隐私；`Product` 牵涉交易、订单、背包和 Console 管理，不适合作为第一批外部契约试验场。
- `WikiDocument` 已有 `slug` 公开路由，当前收益低于 forum post；可在后续把 `PublicId` 作为内部稳定补充，而不是 P3-2 首批主对象。

`Comment` 不作为独立试点对象。首批仅把 `commentId` 作为帖子详情定位参数保留，继续接受 long 字符串；等 `Post` 试点稳定后，再评估 `Comment / Reaction / Notification` 是否进入第二批。

### 兼容策略

- 数据库不改主键，只在 `Post` 增加可空 `PublicId` 字符串列和唯一索引；历史数据可后置批量补齐或按读取 / 分享生成前的维护任务补齐。
- `PublicId` 形态遵循长期路线：`pst_` 前缀 + 固定长度 UUIDv7 编码体；生成时机优先在创建帖子时完成。
- `PostVo` 新增 `VoPublicId`，`VoId` 保留；列表、详情、个人公开页、浏览历史和通知仍可使用 `VoId` 兼容旧客户端。
- 公开路由接受双格式：`/forum/post/{postKey}` 中 `{postKey}` 可为旧 long 字符串或 `pst_` PublicId；服务端查询优先按 PublicId 解析，失败再按 long 兼容。
- canonical 输出逐步切换：当详情 API 返回 `VoPublicId` 时，公开 forum 详情复制链接和运行时 canonical 使用 `/forum/post/{VoPublicId}`；旧 long 链接继续 301/前端 replace 或直接兼容读取。
- 通知 `extData` 新增 `postPublicId`，继续保留 `postId`；前端打开 forum 时优先使用 `postPublicId`，缺失时回退 `postId`。
- 窗口参数新增 `postPublicId`，继续保留 `postId`；桌面 forum 窗口和公开 forum 路由解析都保持字符串安全，不引入 `Number(...)`。
- 浏览历史的 `RoutePath` 在 Post 试点后写入 PublicId canonical；`TargetId` 保持内部 ID，用于当前用户私有历史去重与内部关联。

### 验证入口

实施批次建议验证：

```bash
dotnet test Radish.Api.Tests
npm run type-check --workspace=radish.client
npm run test --workspace=radish.client
npm run build --workspace=radish.client
npm run check:repo-hygiene:changed
```

定向用例至少覆盖：

- 后端 `Post` 创建时生成 `PublicId`，唯一且格式固定。
- `GetById` 或新增兼容查询入口能同时读取 long 与 `pst_`。
- `PostVo` 同时返回 `VoId / VoPublicId`，旧字段不缺失。
- forum 公开路由能解析 long 与 `pst_`，并在详情加载后产出 PublicId canonical。
- 通知 `extData` 同时存在 `postPublicId / postId` 时优先走 `postPublicId`；仅有旧 `postId` 时仍可回流。
- 浏览历史 Post `RoutePath` 写入 PublicId canonical 后仍能从“继续使用”打开桌面 forum。

### 回滚边界

- 回滚不删除数据库列和历史值；只停止新写 `PublicId` canonical，并让前端分享、通知和窗口参数继续使用旧 `postId`。
- 因旧 long 路由、`postId` 通知字段、`VoId` 和 `TargetId` 全部保留，前端可单独回退，不需要数据回滚。
- 若 `PublicId` 生成或唯一索引出现异常，发布 / 编辑主路径应优先失败并暴露错误，不允许静默生成空值后把公开链接切到不可解析状态。
- 试点稳定前，不扩展到 `User / Product / WikiDocument / Comment`，也不启动全量 DTO 字段替换、数据库主键迁移或 ActivityPub / WebFinger 实现。

## `P3-2-B` `Post.PublicId` 首批实现

完成日期：2026-05-14。

已完成：

- `Post` 增加可空唯一 `PublicId`，新发帖生成 `pst_` + UUIDv7 编码体；历史空值继续通过旧 long 链路兼容。
- `PostVo` 并行暴露 `VoPublicId / VoId`，列表与详情旧字段保持不变。
- `PostController.GetById` 支持 long 与 PublicId 双读，浏览次数和浏览历史仍以内部 `VoId` 作为关联键。
- forum 公开详情、复制 canonical 链接、运行时 head、浏览历史 routePath、通知 `extData` 和 WebOS forum 窗口参数均支持 `postPublicId`，并保留旧 `postId` 回退。
- 评论定位、轻回应墙和详情加载在拿到 PublicId 打开的帖子后，会回到真实 `VoId` 调用内部评论 / 轻回应接口。

验证：

- `npm run type-check --workspace=radish.client` 通过。
- `npm run test --workspace=radish.client -- --test-name-pattern="Forum|forum|Public|public|workspace"` 通过。
- `dotnet test Radish.Api.Tests --filter "PostControllerTest"` 通过。
- `npm run check:repo-hygiene:changed` 通过。
- `git diff --check` 通过。

后置边界：

- 不回填历史 `Post.PublicId`；如需批量补齐，应单独做数据维护方案。
- 不扩到 `User / Product / WikiDocument / Comment`。
- 不启动完整 `PublicId` 全量迁移、数据库主键迁移或 ActivityPub / WebFinger 实现。

## `P3-3-A / P3-3-B` `PublicForumApp` 首批低风险拆分

启动日期：2026-05-14。

已完成：

- 抽出 `publicForumUtils.ts`，承载公开论坛 route key、分页、阅读 guide、PublicId route identifier、评论树 children merge 等纯 helper。
- 抽出 `PublicStatusCard.tsx`，收口公开论坛多处加载 / 空态 / 错误状态展示。
- 抽出 `PublicForumTypeFeed.tsx`，独立承载问答 / 投票 / 抽奖类型流页面。
- 抽出 `PublicForumSearch.tsx`、`PublicForumTag.tsx`、`PublicForumList.tsx` 和 `PublicForumDetail.tsx`，分别承载公开搜索、标签页、默认列表页和帖子详情页。
- `PublicForumApp.tsx` 从约 `2911` 行降到约 `208` 行，当前只保留公开论坛外层路由容器、滚动恢复和子组件接入。
- 拆分过程中不改公开论坛路由、只读边界、PublicId / long 双读、评论定位、轻回应墙、分享 canonical 或列表 / 标签 / 搜索参数同步行为。

验证：

- `npm run type-check --workspace=radish.client` 通过。
- `node --test --test-isolation=none ./tests/forumNavigation.test.ts ./tests/workspaceNavigation.test.ts ./tests/publicRouteNavigation.test.ts ./tests/publicRouteState.test.ts ./tests/publicRouteSync.test.ts ./tests/publicHead.test.ts ./tests/publicSeoStatic.test.ts` 通过。
- `npm run check:repo-hygiene:changed` 通过。
- `git diff --check` 通过。
- 新增组件均已显式纳入 `node Scripts/check-repo-hygiene.mjs --stdin-z` 与路径级 `git --no-pager diff --check`。

下一步：

- 本轮 `PublicForumApp.tsx` 首批治理先收口。
- 不继续无边界深拆 `PublicForumDetail` 内部 hook / 子结构；如未来暴露真实维护痛点，再单独评估。
- `2026-05-16` 收工复核已通过：`npm run type-check --workspace=radish.client`、公开 forum / public route / public head 定向 `node --test`、`npm run check:repo-hygiene:changed` 与 `git diff --check` 均通过。
- 下一主线选择为 `P3-4 用户留存轻闭环`，公开内容增长后续专题后置。

## `P3-4-A` 用户留存轻闭环审计

启动日期：2026-05-16。

### 目标

- 从已有通知、最近阅读、我的轻回应、公开分享和 Flutter 复访入口中主动验收真实回流断点。
- 产品正式上线进入稳定运营前，默认采用“主动批量验收 + 成组修复 + 一次性交付结论”，不把每个小修复都交给开发者手动复测后再继续推进。
- 首批围绕同一高价值链路选择一组成片问题，让用户能从“看到内容 / 收到提醒 / 回看记录”稳定回到上下文。
- 保持桌面工作台、公开内容壳层和 Flutter 移动端的边界一致，不把任一端扩成完整运营平台。

### 审计范围

| 范围 | 审计重点 | 首批判断 |
| --- | --- | --- |
| 公开分享 | forum / docs / shop 复制 canonical 后，未登录和已登录用户是否都能进入正确公开壳层 | 只处理回流断点，不追加分享统计或海报 |
| 通知回流 | forum 相关通知是否优先带 `postPublicId`，点击后能回到帖子和评论上下文 | 不做完整移动通知中心或系统推送 |
| 最近阅读 | WebOS “继续使用”与浏览历史 routePath 是否稳定打开 forum / docs / shop 上下文 | 不扩完整收藏、关注或阅读管理 |
| 我的轻回应 | Flutter 与桌面个人复访入口是否能看懂目标并回到原帖 | 不开放完整评论提交、点赞或投票 |
| 多端一致性 | 桌面工作台和 Flutter 对复访入口的展示和回流边界是否一致 | 不把 WebOS 移植成移动版 |

### 首批完成条件

- 形成 `P3-4-A` 审计结论，明确首批要做的成组回流闭环和不做项。
- 若涉及代码改动，按影响面至少执行：
  - `npm run type-check --workspace=radish.client`
  - `npm run test --workspace=radish.client -- --test-name-pattern="Forum|forum|Public|public|workspace|notification|browse"`
  - Flutter 相关改动追加对应 `flutter test test/<topic>_test.dart`
  - `npm run check:repo-hygiene:changed`

### 明确不做

- 不做完整运营平台、完整通知中心移动版、系统通知栏推送或通知设置。
- 不做完整评论提交、点赞、投票、编辑治理、聊天移动版或商城工作台移动化。
- 不继续无边界扫按钮、提示、空态和视觉微调。
- 不借留存主题扩大 `PublicId` 全量迁移或动态 sitemap / SSR / SSG 专题。

### `2026-05-16` 初步审计结论

- 首批最高收益小闭环建议先选 **Flutter forum notification 回流优先使用 `postPublicId`**：
  - 服务端 `NotificationNavigationHelper.BuildForumNavigationExtData` 已在 forum 通知 `extData` 中并行写入 `postId / postPublicId / commentId`。
  - WebOS 通知中心已经通过 `parseForumNotificationNavigation` 优先解析 forum navigation，并由 `buildForumAppParams` 打开 forum 窗口。
  - Flutter `Clients/radish.flutter/lib/features/notifications/data/notification_repository.dart` 当前只读取 `extData.postId`，未优先使用 `postPublicId`；这会让移动通知回流继续依赖旧 long 路由，和 `P3-2` forum canonical / PublicId 方向不完全一致。
  - 建议实现时只改 Flutter 通知解析和对应测试：`postPublicId` 存在时作为 `ForumDetailHandoffTarget.postId`，缺失时继续回退旧 `postId`，不改服务端接口。
- 第二候选为 **我的轻回应回流并行携带 `VoPostPublicId`**：
  - WebOS `DesktopResumePanel` 和 Flutter Profile 的“我的轻回应”当前都能回到原帖，但数据契约 `UserPostQuickReplyVo` 只暴露 `VoPostId`。
  - 若要让轻回应复访也逐步切到 PublicId，需要为 `UserPostQuickReplyVo` 增加 `VoPostPublicId` 并在 WebOS / Flutter 回流入口优先使用该字段。
  - 该项涉及后端 ViewModel/API 契约扩展，建议在 Flutter 通知小闭环完成后单独批准实施。

### `P3-4-A1` Flutter forum notification PublicId 回流

完成日期：2026-05-16。

已完成：

- Flutter `ForumNotificationPayload.forumTarget` 解析 forum 通知 `extData` 时，已优先使用 `postPublicId` 作为详情回流目标。
- 旧通知 payload 仍保留兼容：`postPublicId` 缺失时继续回退 `postId`，`commentId` 继续用于评论上下文定位。
- 新增单测覆盖 `postPublicId + postId` 并存、仅 `postPublicId`、旧 `postId`、非 forum 通知跳过和畸形 payload 忽略。

验证：

- `flutter test test/notification_repository_test.dart` 提权环境通过，`5/5`。

后置：

- “我的轻回应”回流并行 `VoPostPublicId` 仍后置；该项涉及后端 ViewModel/API 契约扩展，需单独批准。

### `P3-4-A2` 我的轻回应 PublicId 回流

完成日期：2026-05-16。

已完成：

- `UserPostQuickReplyVo` 新增 `VoPostPublicId`，并保留旧 `VoPostId`。
- `PostQuickReplyService.GetMinePageAsync` 查询“我的轻回应”时并行填充帖子 `PublicId`，历史未补齐 `PublicId` 的帖子继续返回空值并走旧 `VoPostId`。
- WebOS “继续使用”和个人页“我的轻回应”回流均优先携带 `postPublicId`，缺失时回退 `postId`。
- Flutter Profile “我的轻回应”回流优先使用 `voPostPublicId`，旧 payload 继续使用 `voPostId`。

验证：

- `dotnet test Radish.Api.Tests --filter "PostQuickReplyServiceTest" -v minimal` 提权环境通过，`3/3`。
- `flutter test test/profile_page_test.dart` 提权环境通过，`29/29`。
- `npm run type-check --workspace=radish.client` 通过。
- `node --test --test-isolation=none ./tests/workspaceNavigation.test.ts ./tests/forumNavigation.test.ts` 通过，`36/36`。

后置：

- `P3-4-A` 首批 forum 回流已覆盖通知和我的轻回应两条高价值路径；下一步不继续扩全量 `PublicId`，优先做主动链路验收，或再评估最近阅读 / 浏览历史中的历史数据补齐策略。

### `P3-4-A3` 最近阅读 / 浏览历史历史数据补齐策略评估

完成日期：2026-05-16。

审计结论：

- 后端 `PostController.GetById` 已在记录 Post 浏览历史时使用 `PublicRoutePathBuilder.BuildForumPostPath(post.VoPublicId, post.VoId)`，新访问会写入 `/forum/post/{VoPublicId}`，缺失 `PublicId` 时才回退旧 long 路由。
- `UserBrowseHistoryService.RecordAsync` 以 `UserId + TargetType + TargetId` 命中已有记录；用户再次打开同一帖子时，会用新的 PublicId canonical `RoutePath` 更新旧 long 记录，并递增浏览次数。
- WebOS 个人页浏览历史和桌面“继续使用”统一走 `resolveBrowseHistoryWorkspaceTarget`；该入口已同时支持 long route、PublicId route，以及无 `routePath` 时按 `TargetType + TargetId` 回退打开 forum。
- Flutter 近期 forum 复访仍以 `ForumDetailHandoffTarget.postId` 承载目标字符串；通知和我的轻回应已可把 `postPublicId` 作为该目标传入，旧 long 目标继续兼容。

策略：

- 当前不做一次性历史数据批量补齐，也不新增维护任务或扩展浏览历史 API 契约。
- 历史 long `RoutePath` 不阻断回流：前端可直接解析并打开，后续用户再次访问同一 Post 时会自然刷新为 PublicId route。
- 开发期先由主动验收矩阵发现是否存在成片旧历史影响；若后续真实使用仍暴露大量旧历史影响分享 / SEO / 跨端回流，再单独评估只针对 Post 浏览历史的维护脚本；不扩到 `User / Product / WikiDocument / Comment` 或全量 `PublicId` 迁移。

验证：

- `dotnet test Radish.Api.Tests --filter "PostControllerTest|UserBrowseHistoryServiceTest" -v minimal` 提权环境通过，`20/20`。
- `npm run type-check --workspace=radish.client` 通过。
- `npm run test --workspace=radish.client -- --test-name-pattern="Forum|forum|Public|public|workspace|notification|browse"` 通过，`135/135`。
- `flutter test test/notification_repository_test.dart test/profile_page_test.dart` 提权环境通过，`34/34`。

### `P3-4-A4` forum 回流窄范围真实链路复核

完成日期：2026-05-16。

已完成：

- 复核公开分享、通知、最近阅读、我的轻回应回到 WebOS / Flutter forum 详情的一致性。
- 后端评论回复、评论点赞通知补齐 `postPublicId`，仍保留旧 `postId` fallback。
- Flutter Profile 最近阅读和我的轻回应卡片不再在可见文案中展示帖子 long id 或轻回应 id，回流目标仍保持 `postPublicId ?? postId`。
- 补充 Flutter 个人页断言，确认旧 long id 文案不再出现。

验证：

- `flutter test test/profile_page_test.dart --plain-name "renders my quick replies and opens their forum handoff"` 通过。
- `flutter test test/profile_page_test.dart --plain-name "keeps long profile text constrained on narrow screens"` 通过。
- `flutter test test/notification_repository_test.dart` 通过。
- `dotnet test Radish.Api.Tests --filter "FullyQualifiedName~Comment"` 通过。
- `dotnet test Radish.Api.Tests --filter "FullyQualifiedName~PostControllerTest|FullyQualifiedName~UserBrowseHistoryServiceTest|FullyQualifiedName~PostQuickReplyServiceTest"` 通过。
- `git diff --check` 通过。

节奏调整：

- `P3-4-A` 的“观察”不再理解为等待开发者逐个手动复测和反馈。
- 产品正式上线进入稳定运营前，`P3-4` 默认由 AI 主动按链路矩阵复核、成组修复、补充定向测试，并一次性交付结论。
- 产品正式上线并进入稳定运营后，再切换为更小心谨慎的小步修复、风险隔离和发布留痕策略。
- 下一批建议命名为 `P3-4-B 留存回流主动验收批次`，范围仍限定在公开分享、通知、最近阅读、我的轻回应到 WebOS / Flutter 详情的一致性，不扩完整 `PublicId` 迁移。

### `P3-4-B` 至 `P3-4-F` forum 留存回流主动验收

完成日期：2026-05-16。

已完成：

- 公开分享、通知、最近阅读、我的轻回应、公开个人页最近公开帖子 / 评论到 WebOS 或 Flutter forum 详情的回流链路已成组复核。
- Flutter forum detail 会先解析真实帖子详情，再用真实 `VoId` 访问评论、轻回应和评论定位接口；PublicId 只作为外部入口标识，内部 long 接口不误传 PublicId。
- WebOS 与 Flutter 公开个人页最近公开帖子 / 评论回流优先使用帖子 PublicId，评论只附带必要 `commentId` 定位参数。
- forum 详情作者 / 评论作者到公开个人页、再从公开个人页回 forum 详情的来源返回和 PublicId 路由状态已补测试。
- WebOS 通知解析在 payload 同时存在 `postPublicId` 与旧 long `routePath` 时优先使用 PublicId；payload 缺少 PublicId 但 routePath 已是 PublicId 时也优先使用 routePath PublicId。
- WebOS / Flutter forum 普通可见文案不再用帖子 long id、评论 id、作者 id 或分类 id 作为 fallback；内部点击和旧数据回流能力保留。
- Flutter 最近阅读 / 最近个人页复访存储已补测试，确认重复 PublicId handoff 去重、来源归一为 `browseHistory`，最近个人页 userId 做 trim / 空值清理。

结论：

- forum 留存回流第一轮矩阵已覆盖，不需要继续拆新 forum 批次。
- 后续只处理真实使用中暴露的新公开分享 / 复访断点，不借此扩 `User / Product / WikiDocument / Comment` 全量 PublicId 迁移。

### `P3-4-G` docs 留存回流可见性复核

完成日期：2026-05-16。

已完成：

- Flutter docs handoff 详情已隐藏 long numeric slug 的普通可见上下文，保留 slug / documentId fallback 打开能力。
- WebOS 最近阅读已隐藏 `/wiki/doc/{long}` 与 `/docs/{long}` 旧文档路径可见文本，仍可通过 `documentId` fallback 打开文档。
- docs 公开路由、workspace navigation、public head 与 Flutter docs/profile 定向测试已覆盖。

结论：

- docs 当前以 slug 为公开详情主路由，不启动 `WikiDocument.PublicId` 或数据库迁移。

### `P3-4-H` shop 留存回流可见性复核

完成日期：2026-05-16。

已完成：

- WebOS 最近阅读已隐藏 `/shop/product/{long}` 商品旧路径可见文本，仍可通过 `productId` fallback 打开商品详情。
- 公开 shop 商品详情 head 不再把 `productId` 写入 title / description，canonical path 保持 `/shop/product/{productId}`，公开路由兼容不变。
- shop workspace navigation、public route、public head 与来源返回测试已覆盖。

结论：

- shop 当前仍以 long `productId` 作为兼容路由；本轮只冻结普通文案外露，不启动 `Product.PublicId` 或全量外部标识改造。

### `P3-4-I` 公开 head 标识可见性补洞与首轮收尾判断

完成日期：2026-05-17。

已完成：

- 主动收尾复核发现 `publicHead` 初始 head 仍会把 forum 旧 long 路由、forum 分类 ID、docs 数字兼容路径和公开个人页 `userId` 写入 title / description。
- 已将这些初始 head 文案收口为通用公开阅读文案，避免普通用户在浏览器标题、meta description 或分享预览兜底中看到长数字标识。
- canonical path 继续保留原始公开路径，旧 long 路由、数字 docs 兼容路径和 `/u/:id` 仍可打开；本轮不启动 `User / Product / WikiDocument / Comment` 外部标识改造。
- 详情数据加载后的业务标题仍由页面自身接管，例如 forum detail 使用真实帖子标题、docs detail 使用真实文档标题、profile 使用公开资料展示名。

验证：

- `npm run type-check --workspace=radish.client` 通过。
- `node --test --test-isolation=none ./tests/publicHead.test.ts ./tests/forumNavigation.test.ts ./tests/workspaceNavigation.test.ts ./tests/publicRouteNavigation.test.ts ./tests/publicRouteState.test.ts ./tests/publicProfileNavigation.test.ts` 通过，`80/80`。
- `npm run test --workspace=radish.client -- --test-name-pattern="Forum|forum|Public|public|workspace|notification|browse"` 通过，`142/142`。
- `flutter test test/notification_repository_test.dart test/profile_page_test.dart test/docs_page_test.dart` 提权环境通过，`49/49`。
- `dotnet test Radish.Api.Tests --filter "FullyQualifiedName~PostControllerTest|FullyQualifiedName~UserBrowseHistoryServiceTest|FullyQualifiedName~PostQuickReplyServiceTest|FullyQualifiedName~Comment" -v minimal` 提权环境通过，`34/34`。
- `npm run check:repo-hygiene:changed` 通过。
- `git diff --check` 通过。

收尾判断：

- `P3-4` 首轮 forum / docs / shop 留存回流矩阵已完成主动验收和补洞，当前未发现新的 `P0/P1` 阻断项。
- 首轮可以收尾；后续只处理真实使用中新暴露的回流断点，不继续扩全量 `PublicId`、数据库主键迁移或低收益微体验。
- 下一步进入公开内容增长后续专题评估，优先判断动态 sitemap、结构化数据或详情首包可见性是否值得作为下一批主线；未评估前不直接启动 SSR / SSG。

## `P3-5-A` 公开内容增长后续专题评估

完成日期：2026-05-17。

### 审计结论

| 方向 | 当前事实 | 收益判断 | 风险判断 |
| --- | --- | --- | --- |
| 动态 sitemap | 当前 `Frontend/radish.client/public/sitemap.xml` 只包含公开入口 seed；Gateway catch-all 默认转到 frontend，未给 `/sitemap.xml` 单独接 API / 生成器 | 可补 forum / docs / shop 详情抓取入口，长期收益明确 | 需要决定由 API + Gateway 路由、构建生成器或独立任务承载；涉及部署与缓存，不应直接改 |
| 结构化数据 | 当前只有运行时 `publicHead` 写 title / description / canonical / Open Graph；未发现 `application/ld+json` 或 `schema.org` 输出 | 可先为 forum detail、docs detail、shop detail 和公开个人页补 `BlogPosting / Article / Product / ProfilePage` 等基线 | 前端运行时 JSON-LD 不能解决无 JS 首包问题，但改动边界清晰、验证成本低 |
| 详情首包可见性 | `index.html` 仍是通用 SPA shell，详情标题、摘要和正文都依赖前端运行时加载 | 对搜索和社交爬虫收益最高 | 真正解决需要 SSR / SSG / prerender 或 Gateway HTML rewrite，牵涉构建、缓存、鉴权边界和部署策略 |

### 排序结论

下一批建议先做 `P3-5-B 运行时结构化数据基线`：

- 只在 `radish.client` 公开详情页注入和清理 JSON-LD，不改变 API、Gateway、部署和公开路由。
- 数据来源优先复用详情页已经加载到的业务数据，不新增额外详情请求。
- 覆盖 forum detail、docs detail、shop detail 和公开个人页；列表页和搜索页暂不补结构化数据。
- 为 JSON-LD helper、去重 / 清理行为和关键字段增加 `node --test` 覆盖。
- 明确该批次只增强可执行 JS 的 crawler / 分享工具可读性，不宣称解决首包 HTML 可见性。

`P3-5-C 动态 sitemap` 需要单独方案批准后再实施：

- 优先比较 `API 生成 XML + Gateway /sitemap.xml 路由` 与 `构建期静态生成` 两条路线。
- 需明确公开域名、分页上限、更新时间来源、缓存 TTL、详情 canonical 格式和异常降级。
- 在方案批准前，不直接把数据库查询、API 调用或部署脚本塞进前端构建。

`P3-5-D 详情首包可见性` 继续后置：

- 只有当公开内容增长数据、爬虫抓取反馈或分享预览质量证明收益足够时，才进入 SSR / SSG / prerender / Gateway HTML rewrite 方案评审。
- 未评审前不改 Vite 构建形态、不引入完整 SSR 运行时、不把 Gateway 变成临时 HTML 拼接层。

### 完成条件

- `P3-5-A` 只产出评估结论和下一批排序，不改运行时代码。
- 规划入口同步到 `P3-5` 口径。
- 文档卫生与 diff 检查通过。

## `P3-5-B` 运行时结构化数据基线

完成日期：2026-05-17。

已完成：

- 新增 `publicStructuredData` helper，统一构建、注入、复用和清理 `<script type="application/ld+json">`。
- forum detail 在帖子详情加载成功后输出 `BlogPosting`，复用 PublicId canonical、帖子标题、摘要 / 正文、作者、发布时间、封面、标签和互动计数。
- docs detail 在文档详情加载成功后输出 `Article`，复用文档 slug canonical、标题、摘要 / markdown 正文、发布时间和更新时间。
- shop detail 在商品详情加载成功后输出 `Product`，复用商品 canonical、名称、描述、图片、分类和 Radish 组织信息；不把积分价格伪装成法币 offer。
- 公开个人页在资料加载成功后输出 `ProfilePage` / `Person`，使用展示名 / 用户名和头像，不把长数字用户 ID 当作可见名称。
- 路由切换、详情数据缺失或组件卸载时会清理旧 JSON-LD，避免从一个公开详情跳到另一个页面后残留错误结构化数据。

验证：

- `npm run type-check --workspace=radish.client` 通过。
- `node --test --test-isolation=none ./tests/publicStructuredData.test.ts ./tests/publicHead.test.ts` 通过，`12/12`。

后置：

- 本批只增强可执行 JS 的 crawler / 分享工具可读性，不解决无 JS 首包 HTML 可见性。
- 动态 sitemap 仍需单独确认 API + Gateway 路由或构建生成器方案。
- SSR / SSG / prerender / Gateway HTML rewrite 继续后置，不因本批完成而直接启动。

## `P3-5-C` 动态 sitemap 方案评审与首批实现

评审完成日期：2026-05-17。

首批实现完成日期：2026-05-17。

### 现状判断

- 当前 `Frontend/radish.client/public/sitemap.xml` 是静态 seed，只覆盖 `/discover`、forum / docs / shop 公开入口和少量列表页。
- Gateway 当前通过 YARP 将 `/api/{**catch-all}`、`/uploads/{**catch-all}`、`/_assets/attachments/{**catch-all}` 等路由转到 API，并用最低优先级 `/{**catch-all}` 转前端；动态 sitemap 若对外暴露为顶层 `/sitemap.xml`，必须在 Gateway 中显式声明高于前端 catch-all 的路由。
- forum 已具备 `Post.PublicId` canonical，docs 已以 `slug` 作为公开详情主路由，shop 仍以 `productId` 作为兼容公开详情路由。三类详情都能由后端根据公开状态筛出可索引 URL。

### 路线评估

| 路线 | 做法 | 优点 | 风险 | 结论 |
| --- | --- | --- | --- | --- |
| API + Gateway 路由 | API 提供 sitemap index 和分片 XML；Gateway 把顶层 `/sitemap.xml`、`/sitemaps/*.xml` 或等价固定路径代理到 API | 与运行时数据同步；能复用后端状态过滤、PublicId / slug canonical 和缓存；发布后内容变化不依赖重新构建前端 | 需要新增 API 只读查询、缓存和 Gateway 路由优先级；API 异常会影响动态 sitemap | 推荐作为首批实施路线 |
| 构建期静态生成器 | 前端构建或独立构建任务调用 API / 数据库，写入 `public/sitemap.xml` 或 dist 静态文件 | 运行时最稳，前端静态服务即可返回 XML，不增加请求时数据库压力 | 构建依赖生产数据、凭据和网络；内容发布后容易过期；会把 API / DB 依赖塞进前端构建或 CI，部署耦合较重 | 不作为首批实施路线，仅保留为后续离线 / 夜间导出备选 |

### 推荐方案

首批采用 **API 生成 XML + Gateway 顶层路由**：

- API 侧新增只读 sitemap 能力，输出 `application/xml; charset=utf-8`，不走登录态、不暴露内部管理字段。
- Gateway 侧新增高优先级 sitemap 路由，避免 `/sitemap.xml` 被前端 catch-all 或旧静态文件覆盖；`robots.txt` 继续指向公开域名下 `/sitemap.xml`。
- 输出形态采用 sitemap index + 分片文件，哪怕首批数据量不大也先固定契约，避免以后超过单文件上限时再改入口。

### sitemap 范围

首批范围限定为公开、可直达、可稳定 canonical 的 URL：

- 静态 seed：继续包含 `/discover`、`/forum`、`/forum/question`、`/forum/poll`、`/forum/lottery`、`/docs`、`/docs/search`、`/leaderboard`、`/shop`、`/shop/products`。
- forum detail：只包含 `IsPublished && !IsDeleted` 的帖子；URL 优先 `/forum/post/{VoPublicId}`，历史缺失 `PublicId` 时才回退 `/forum/post/{VoId}`。
- docs detail：只包含 `Status = Published`、`Visibility = Public`、`!IsDeleted` 且 `Slug` 非空的文档；URL 使用 `/docs/{VoSlug}`。
- shop detail：只包含公开商品列表中可见的商品，至少满足 `IsOnSale && IsEnabled && !IsDeleted`；URL 使用 `/shop/product/{VoId}`。

首批不纳入：

- `/u/:id` 公开个人页。当前仍依赖内部 long 用户 ID，且用户隐私 / 展示意愿边界未单独评审。
- 搜索结果页组合、标签组合分页、评论锚点、订单 / 背包 / Console / Auth / API 文档 / Scalar / Hangfire 等登录态或治理路径。
- 未发布、草稿、私有、认证可见、软删除、禁用、下架或审核不可见内容。

### 缓存 TTL 与分页上限

- API 内存缓存或 Redis 缓存 TTL 建议首批为 `30` 分钟；Redis 开启时用 Redis，关闭时用内存缓存，跟随现有 `AddCacheSetup()` 策略。
- 可对 sitemap index 和各分片分别缓存；缓存 key 应包含公开域名、分片类型、页码和每页数量。
- 单分片 URL 上限按搜索引擎标准保守控制在 `45,000` 条以内，低于 `50,000` 硬上限；首批默认每个内容类型每页 `5,000` 条，避免一次 XML 过大。
- 首批总分页上限建议每个内容类型最多 `20` 个分片，即单类型最多约 `100,000` 条 URL；超过上限只输出前 `20` 个分片并记录告警，不在用户请求中无限扫库。
- 查询顺序按更新时间倒序；同一时间戳下按内部 ID 倒序稳定排序，避免分页游标抖动。

### 更新时间字段

- forum detail 的 `lastmod` 使用 `VoModifyTime ?? VoPublishTime ?? VoCreateTime`。
- docs detail 的 `lastmod` 使用 `VoModifyTime ?? VoPublishedAt ?? VoCreateTime`。
- shop detail 的 `lastmod` 使用商品 `ModifyTime ?? OnSaleTime ?? CreateTime`；若当前 `ProductVo` 不暴露 `VoModifyTime / VoOnSaleTime`，实施时应在 sitemap 专用查询模型中读取实体字段，不为了 sitemap 扩普通前端 Vo。
- 静态 seed 的 `lastmod` 可使用 sitemap 生成时间或固定部署时间；首批建议使用生成时间，配合 `30` 分钟缓存 TTL。
- 所有输出统一为 UTC / ISO 8601 日期时间，避免服务器本地时区差异影响 XML。

### 异常回退

- API 生成失败时，首选返回最近一次成功缓存的 XML，并记录错误日志；缓存存在但过期也可短期兜底返回。
- 没有成功缓存时，`/sitemap.xml` 返回静态 seed sitemap，保证爬虫入口不断；分片请求无缓存时返回空 `urlset` 或 `503` 需在实施前二选一，首批建议返回空 `urlset` 并记录告警，避免爬虫把根入口判为不可用。
- 单一内容类型查询失败时，不影响其它分片；对应分片使用最近成功缓存或空 `urlset`。
- XML 生成必须做 URL 转义、非法字符清洗和长度保护；异常数据只跳过单条 URL 并记录聚合告警，不让整份 sitemap 失败。

### 部署风险与护栏

- Gateway 路由优先级是主要风险：新增 `/sitemap.xml` 与分片路径必须高于前端 `/{**catch-all}`，并避免被前端静态 `sitemap.xml` 旧文件抢占。实施时要明确生产环境到底由 Gateway 代理前端还是直接由静态服务托管前端。
- 公开域名来源必须统一使用生产 `RADISH_PUBLIC_URL` / `GatewayService:PublicUrl` 派生，不允许在动态 XML 中硬编码 `localhost` 或测试域名。
- API sitemap 查询要走专用只读模型或仓储方法，不能在 Service 层直接 `_repository.Db.Queryable`；需要优先复用 / 扩展 repository 通用查询能力。
- 不把 sitemap 生成挂进 `npm run build --workspace=radish.client`，避免本地构建、CI 和部署因生产数据或网络凭据失败。
- 首批需要补后端单测覆盖公开过滤、PublicId / slug / product 路径、lastmod 选择、分页上限和异常回退；Gateway 配置需补最小路由验证或文档化人工验证点。

### 后续实施完成条件

- `/sitemap.xml` 返回 sitemap index 或 seed fallback，并能列出 forum / docs / shop 分片。
- `robots.txt` 指向的 sitemap 与生产 Gateway 实际返回一致。
- forum / docs / shop 分片只包含公开可索引内容，URL 与运行时 canonical 口径一致。
- API 缓存、分页上限、lastmod、异常回退和日志路径有测试或明确验证记录。
- `dotnet test Radish.Api.Tests`、`dotnet build Radish.slnx -c Debug` 和必要的文档 / 配置卫生检查通过。

### 首批实现记录

已完成：

- 新增 `IPublicSitemapService / PublicSitemapService`，由 API 侧生成 sitemap index 与 `static / forum / docs / shop` 分片 XML。
- 新增公开 `PublicSitemapController`，以顶层 `/sitemap.xml` 和 `/sitemaps/{fileName}` 输出 `application/xml; charset=utf-8`，不纳入 API 文档。
- Gateway 新增 `/sitemap.xml` 与 `/sitemaps/{**catch-all}` 高优先级路由，避免被前端 catch-all 覆盖。
- sitemap 范围按评审收敛：静态 seed、已发布且启用的 forum post、公开已发布 docs、公开可见 shop 商品；不纳入公开个人页、搜索组合页、评论锚点、Console / Auth / API 文档等路径。
- 缓存 TTL 固定为 `30` 分钟；单分片 `5,000` 条，单类型最多 `20` 个分片；缓存 key 包含公开域名、类型和页码。
- `lastmod` 按 forum `ModifyTime ?? PublishTime ?? CreateTime`、docs `ModifyTime ?? PublishedAt ?? CreateTime`、shop `ModifyTime ?? OnSaleTime ?? CreateTime` 输出 UTC ISO 8601。
- 生成失败时优先返回进程内最近成功 XML；无成功结果时 index 回退静态分片入口，内容分片返回空 `urlset` 并记录日志。

验证：

- `dotnet test Radish.Api.Tests --filter "PublicSitemapServiceTest" -v minimal` 通过，`4/4`。

后置：

- `2026-05-20` 已在生产公开域名 `https://radishx.com` 复跑 public head smoke，确认 sitemap index、`static / forum / docs / shop` 分片和三类详情首包 head 可达；后续 release 前仍按同一入口复核。
- 若后续内容规模接近单类型 `100,000` 条 URL，需要单独评估 cursor 分片、夜间导出或搜索引擎索引拆分策略。

## `P3-5-D` 详情首包 HTML 可见性方案评审

完成日期：2026-05-17。详见 [P3-5-D 详情首包 HTML 可见性方案评审](/planning/p3-5-d-html-first-paint-visibility)。

- 当前 `radish.client` 仍是纯 Vite SPA，Gateway 没有 SSR / HTML 渲染流水线；无 JS 爬虫 / 分享工具只能看到通用 shell。
- 完整 SSR 和构建期 SSG / 预渲染均不作为首批路线，避免引入 Node SSR 宿主、生产数据构建依赖和大范围 hydrate / 路由重构。
- 若继续实施，优先做 `P3-5-D1 公开详情 HTML head 快照注入`：API 提供公开详情 head snapshot，Gateway 只对 forum / docs / shop 公开详情做缓存化 head 注入，不渲染正文。
- 该批次涉及 API + Gateway 运行时行为，已在用户批准后进入首批实现。

## `P3-5-D1` 公开详情 HTML head 快照注入

完成日期：2026-05-17。

### 实现范围

- API 新增 `IPublicHeadSnapshotService / PublicHeadSnapshotService` 与 `PublicHeadSnapshotController`，通过内部公开只读接口输出 forum post、docs detail、shop product 三类详情的 head snapshot。
- Gateway 新增 `PublicHeadSnapshotMiddleware`，仅拦截 `GET / HEAD` 且接受 HTML 的 `/forum/post/{postKey}`、`/docs/{slug}`、`/shop/product/{productId}`，从 API 获取 snapshot，再基于 `FrontendService:BaseUrl` 的前端入口 HTML 注入 head。
- 注入内容限定为 `<title>`、`meta description`、canonical、Open Graph、Twitter card 和 `application/ld+json`；不渲染正文 HTML，不改 React hydrate，不改前端构建脚本。
- 公开过滤与 sitemap 口径保持一致：forum 只暴露 `IsPublished && IsEnabled && !IsDeleted`，docs 只暴露公开已发布且未删除文档，shop 只暴露已启用、已上架、未删除且公开列表可见的商品。

### 缓存与更新时间

- API snapshot 缓存 TTL 为 `20` 分钟，缓存 key 包含内容类型、内容 key 和公开域名；Redis 开启时跟随现有缓存配置，关闭时使用内存缓存。
- Gateway 注入后 HTML 缓存 TTL 为 `10` 分钟；前端入口 HTML 缓存 TTL 为 `5` 分钟。
- snapshot 输出 `VoPublishedAt / VoModifiedAt`，JSON-LD 使用 forum `ModifyTime ?? PublishTime ?? CreateTime`、docs `ModifyTime ?? PublishedAt ?? CreateTime`、shop `ModifyTime ?? OnSaleTime ?? CreateTime`。

### 异常回退与部署风险

- API 找不到公开内容时返回 `404`，Gateway 不注入，继续走原 YARP / SPA 链路。
- API snapshot、前端入口 HTML 获取、HTML 注入任一环节异常时，Gateway 记录 warning 并回落到原代理链路，避免 SEO head 注入影响页面可用性。
- release / 生产部署时必须确认 `GatewayService:PublicUrl` 或 `RADISH_PUBLIC_URL` 为生产公开域名，`DownstreamServices:ApiService:BaseUrl` 与 `FrontendService:BaseUrl` 对 Gateway 可达；`2026-05-20` 的 `https://radishx.com` smoke 已确认当前生产链路成立。
- 后续生产人工验收需直接查看三类详情的初始 HTML，确认 head 中 canonical / Open Graph / JSON-LD 与运行时 canonical、动态 sitemap URL 一致。

### 验证

- `dotnet test Radish.Api.Tests` 通过，`390/390`。

## `P3-5-D2` 公开详情 head 注入部署前 smoke 入口

完成日期：2026-05-17。详见 [公开详情 Head Smoke 验收](/guide/public-head-smoke)。

- 新增 `Scripts/check-public-head-smoke.mjs` 与 `npm run check:public-head-smoke`，部署后可用 Gateway base URL 与真实 forum / docs / shop 公开详情路径做首包 HTML head smoke。
- smoke 默认检查 `/robots.txt`、`/sitemap.xml` 和传入详情路径，防止被 SPA shell 覆盖，并断言详情 head 中存在非通用 title、description、canonical、Open Graph、Twitter card 与 `radish-public-jsonld`。
- 本批不启动服务、不抓生产数据、不扩大 Gateway 注入范围；真实环境需要由发布 / 部署人员传入当前公开详情样本路径。

验证：

- `npm run check:public-head-smoke -- --self-test` 通过。

## `P3-5` 收尾判断与维护线

收尾日期：2026-05-17。

`P3-5` 可以阶段收尾。

依据：

- 公开详情增长链路已覆盖运行时 JSON-LD、动态 sitemap、详情首包 head snapshot 注入和部署后 smoke 入口。
- 最新 smoke 已通过 robots、sitemap、forum / docs / shop 三类详情，说明 Gateway 顶层抓取入口和三类公开详情首包 head 当前可被部署验证覆盖。
- 首批方案已明确不渲染正文 HTML、不改变 React hydrate、不引入完整 SSR / SSG，也不把生产数据依赖塞入前端构建；当前继续扩大工程路线的收益不足以覆盖复杂度。

转入维护的部署 / 运营项：testing / release 公开域名配置、API / Frontend 可达性、public head smoke、sitemap 分片、head snapshot / sitemap 缓存日志、分享预览、搜索抓取反馈，以及内容规模接近单类型 `100,000` 条 URL 时的分片策略复评。

不继续扩大的范围：正文 HTML 预渲染、完整 SSR / SSG、构建期生产数据抓取、公开个人页纳入动态 sitemap、全量 `PublicId` 迁移，以及把公开壳层扩成发帖、评论提交、购买、背包、订单或治理工作台。

## `P3-6` 真实使用运营观察与反馈分流

启动日期：2026-05-17。

目标是把 `P3-1` 至 `P3-5` 已落地的公开内容增长、PublicId 试点、留存回流和 head / sitemap 能力放到真实部署与真实使用反馈中观察，并按 `P0/P1/P2/暂不处理` 分流。

首批观察入口包括 robots、动态 sitemap、forum / docs / shop 详情首包 head、运行时 JSON-LD、分享预览、搜索抓取反馈、公开回流断点、PublicId 优先链路、Gateway route 优先级、API / Frontend base URL、head snapshot 缓存和 sitemap 缓存。

完成摘要：

- `P3-6-A` 已完成公开增长 Gateway origin 观察：sitemap `<loc>` origin 已与 head snapshot 口径对齐，分片 origin 检查纳入 `Scripts/check-public-head-smoke.mjs`。
- `P3-6-B` 已完成 public head smoke 失败诊断增强：失败时输出请求 URL、状态码、content-type、body 前段、疑似 SPA shell、失败阶段和关键断言。
- `P3-6-C` 已完成公开增长部署观察记录：本地 Gateway 与生产公开域名 `https://radishx.com` 均已通过 public head smoke，覆盖 robots、sitemap index、`static / forum / docs / shop` 分片和 forum / docs / shop 三类详情。

记录入口：

- [P3-6 公开增长部署观察记录模板](/records/p3-6-public-growth-observation-record-template)
- [P3-6 公开增长部署观察记录（2026-05-18）](/records/p3-6-public-growth-observation-record-2026-05-18)
- [P3-6 公开增长部署观察记录（2026-05-20）](/records/p3-6-public-growth-observation-record-2026-05-20)

阶段收口日期：`2026-05-20`。

收口结论：`P3-6` 可以阶段收口。本轮未发现新的公开访问、核心 head / sitemap、分享入口或回流 `P0/P1` 阻断项。真实平台分享预览、Search Console / 爬虫日志、生产访问日志、用户回流断点、内容规模接近单类型 `100,000` 条 URL 时的 sitemap 分片策略复评转入维护线。

不继续扩大的范围：运营平台、完整可观测性平台、完整 Playwright / E2E、完整 SSR / SSG、正文预渲染、全量 `PublicId` 迁移，以及无真实证据支撑的低收益公开体验微调。

## `P3-7-C` 近期开发任务重评估与首批实现

完成日期：`2026-05-22`。

当时重评估结论：`P3-6 / P3-7-A / P3-7-B` 降为维护线；`P3-8` 多端功能补全与 UI / Pencil 设计治理先作为后续重点保留；最近开发入口回到第三阶段已点名的工作台代码热区治理，优先处理 `WikiApp.tsx` 与 `ChatApp.tsx`。

`P3-7-C1 WikiApp` 与 `P3-7-C2 ChatApp` 工作台首批热区拆分已完成。Wiki 侧新增 `WikiSidebar` 与 `wikiApp.helpers.ts`，`WikiApp.tsx` 从约 `1759` 行降至 `1419` 行；Chat 侧新增消息列表、频道侧栏、成员面板、输入区状态组件和 `chatApp.helpers.ts`，`ChatApp.tsx` 从约 `2004` 行降至 `1489` 行。本批均不改 API、路由、视觉设计或新增 P2 backlog 功能。

验证：Wiki / Chat 定向 node tests、`npm run type-check --workspace=radish.client`、`npm run build --workspace=radish.client`、`npm run check:repo-hygiene:changed` 与 `git diff --check` 通过；构建保留既有 `app-shop` chunk size warning。

`P3-7-C3` 后端 Service 热区治理完成于 `2026-05-23`：`ContentModerationService.cs` 已降至 `806` 行；`ExperienceService.cs` 已完成每日统计、经验治理观察规则、治理动作留痕、等级配置 / 缓存、交易记录、冻结状态、排行榜与管理员调整逻辑拆分，主文件从 `2807` 行降至 `888` 行。

验证：`dotnet test Radish.Api.Tests --filter ExperienceServiceTest` 与 `dotnet build Radish.slnx -c Debug` 通过。

后续已进入 `P3-8` 多端功能补全与 UI 设计治理。`P3-7-C3` 剩余经验发放主流程不在本批硬拆，后续如需治理应单独评审事务、重试、升级奖励和冻结语义风险。
