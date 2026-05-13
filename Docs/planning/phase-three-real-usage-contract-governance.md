# 第三开发阶段：真实使用增长与长期契约治理

> 状态：`P3-2 PublicId 最小试点方案` 进行中
>
> 启动日期：2026-05-13（Asia/Shanghai）
>
> 本页承载第三开发阶段的目标定义、边界、首批任务候选、`P3-0` 审计口径和 `P3-1 / P3-2` 首批结论。快速入口仍以 [当前进行中](/planning/current) 为准；第二阶段事实以 [第二阶段收口评审](/planning/phase-two-closure-review) 与 [已完成摘要](/planning/archive) 为准。

## 阶段判断

第二开发阶段已经完成公开内容壳层、Flutter Android MVP、Tauri + WebOS 桌面壳、多端分工和产品治理收口。当前不应继续把第二阶段低收益尾项当成默认主线，也不应在缺少边界定义时直接开大功能。

第三开发阶段的核心问题不再是“某个入口能不能做出来”，而是：

1. 真实用户能否通过公开内容、移动复访和桌面工作台形成持续使用。
2. 多端之间的路由、对象标识、登录回流和公开链接契约是否足够稳定。
3. 现有代码体量、验证入口和文档入口是否还能支撑继续扩展。

因此第三阶段主题暂定为：**真实使用增长与长期契约治理**。

## 当前推进状态

`P3-0` 已完成第三阶段定义、公开内容增长基础审计和第一批任务排序；`P3-1` 已完成公开内容 SEO 与分享基线。当前主线为 `P3-2 PublicId 最小试点方案`，已完成 `P3-2-A` 外部 ID 契约审计，首批试点对象收敛为 `Post`。

下一步若进入实现，必须仍以兼容试点为边界：只补 `Post.PublicId` 并行契约、forum 公开 canonical、通知 `extData` 与窗口参数双字段解析；不做数据库主键迁移、全量 DTO 替换或 `User / Product / WikiDocument / Comment` 扩面。

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
- `npm run check:repo-hygiene:changed` 通过。
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

## 首批候选任务

### `P3-1` 公开内容增长基础

目标：让公开内容从“可直达阅读”推进到“可传播、可索引、可回流”。

候选范围：

- sitemap / robots / canonical 口径与生成位置确认。
- forum / docs / profile / shop 的基础 `title / description / og:*` 输出策略。
- 分享卡片与公开链接复制口径复核。
- 公开内容壳层来源返回与外链打开策略复核。

边界：

- 不把公开壳层扩成工作台。
- 不开放发帖、完整评论提交、购买、订单、背包或治理动作。
- SSR / SSG 只先做方案判断，不默认立刻改构建架构。

### `P3-2` `PublicId` 最小试点方案

目标：把长期 `InternalId / PublicId / FederationId` 方向从文档原则推进到可执行试点。

候选范围：

- 选择 `Post / User / WikiDocument` 中 `1-2` 个核心对象作为试点候选。
- 定义 `PublicId` 格式、生成时机、唯一索引、DTO 暴露和兼容查询口径。
- 明确 `LongId` 字符串安全过渡期与 `PublicId` 并行期边界。

边界：

- 不迁移数据库主键。
- 不一次性修改所有 API / 前端路由。
- 不启动 ActivityPub / WebFinger 实现。

### `P3-3` 代码热区拆分与维护成本治理

目标：降低继续扩功能时的变更风险。

首批候选热区：

- `Frontend/radish.client/src/public/forum/PublicForumApp.tsx`
- `Frontend/radish.client/src/i18n.ts`
- `Radish.Service/ExperienceService.cs`
- `Radish.Service/ContentModerationService.cs`
- `Clients/radish.flutter/lib/features/forum/presentation/forum_detail_page.dart`
- `Clients/radish.flutter/lib/features/profile/presentation/profile_page.dart`

边界：

- 只做能降低真实复杂度的拆分，不做空壳抽象。
- 每次拆分必须有对应定向验证。
- 不在拆分批次里顺手改业务行为，除非是拆分暴露出的明确 bug。

### `P3-4` 用户留存轻闭环

目标：把已有通知、复访、轻互动和公开分享串成自然回流路径。

候选范围：

- 公开内容分享后进入正确壳层。
- 已登录用户从通知 / 最近阅读 / 我的轻回应回到上下文。
- 桌面工作台与 Flutter 移动端对“继续使用”的边界保持一致。

边界：

- 不做完整运营平台。
- 不做完整通知中心移动版。
- 不扩完整评论提交、点赞、投票、编辑治理或聊天移动版。

## 首批排序建议

`P3-0` 完成后，默认优先级建议为：

1. `P3-1` 公开内容增长基础。
2. `P3-2` `PublicId` 最小试点方案。
3. `P3-3` 代码热区拆分与维护成本治理。
4. `P3-4` 用户留存轻闭环。

如果 `P3-0` 审计发现资产、安全、登录、购买、转账、权限授权或主路径中断的 `P0/P1`，最多先回拉 `1-2` 个小闭环，再继续上述排序。

## `P3-0` 完成条件

- 当前阶段文档入口已经从“下一阶段主任务选择”切到 `P3-0`。
- 第三阶段目标、非目标、首批候选和排序依据已经写清楚。
- 主路径与代码热区审计形成简短结论。
- 第一批正式开工任务具备明确范围、完成条件和验证入口。
- 第二阶段维护边界仍然成立，没有被低收益尾项重新拉回。

## 验证口径

`P3-0` 以文档和审计为主，默认只需要：

```bash
npm run check:repo-hygiene:changed
```

若审计过程中改动代码，再按改动范围追加：

- 前端公开壳层：`npm run type-check --workspace=radish.client`
- Console：`npm run build --workspace=radish.console`
- 后端契约：`dotnet test Radish.Api.Tests`
- Flutter：对应 `flutter test test/<topic>_test.dart` 与 `flutter analyze`
