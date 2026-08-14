# F4-R R2-P03 Public 只读详情变体设计前代码事实与能力覆盖审计

> 日期：2026-08-09（Asia/Shanghai）
>
> 状态：审计完成；R2 分级保持不变，进入窄前端能力门禁，尚未修改 Pencil
>
> 正式锚点：`/docs/:slug`、`/shop/product/:productId`、`/u/:identifier`、`/legal`

## 1. 结论

`R2-P03 / Public 只读详情变体` 继续保持 R2，不升级为新的完整 R1 页面类型。`R1-P02` 已提供正式 Public 详情的 PC / mobile 内容主轴、来源返回、分享、状态槽和互动动作语法，`R1-F01` 已提供四主题与共享组件基座；本专题只需要校正文档元数据与长内容、商品购买 / 举报、公开主页关系动作、Legal 锚点长文和 Mobile 信息顺序，不应复制四个完整路由画板。

现有 Docs 详情、商品详情、公开主页与 Legal 的主体能力已经在正式 Web 中存在，WebOS 没有需要整体迁回 Public 壳层的独占页面结构。但 Pencil 仍不能立即开始，审计发现两项与真实状态和用户承诺直接相关的窄前端能力缺口：

1. 正式商品详情没有商品举报入口；共享 `ContentReportModal`、`Product` 目标类型、服务端治理链路与 WebOS 商品举报均已存在，Legal 和用户承诺又明确要求用户从原目标提交举报。
2. 公开主页把基础资料与统计放在同一个 `Promise.all` 中；次要统计失败会覆盖已经可读的资料，并可能让不存在用户因统计请求先失败而丢失结构化 `404` 语义。匿名或本人查看时页面还会把未读取的粉丝数显示成 `—`，不能把它包装成真实统计值。

这两项只需关闭正式 `radish.client` 的前端消费与呈现门禁，不需要新增 API、权限、数据库、URL、LongId 形态、实时能力或移动壳层。门禁关闭并记录定向验证前，不修改唯一活动 `.pen`，不以视觉稿固化当前缺口。

## 2. 审计范围与继承裁决

| 详情变体 | 当前真实能力 | R2-P03 局部责任 | 继承来源 |
| --- | --- | --- | --- |
| Docs 详情 | slug / anchor、Markdown、元数据、目录来源、相关文档、分享、受保护附件、受权编辑回流 | 长内容层级、元数据密度、目录 / 来源关系、Mobile 正文优先顺序 | `R1-P02`、`R1-F01` |
| 商品详情 | LongId 字符串、价格 / 库存 / 限购 / 有效期、购买资格、支付确认、订单回流、分享 | 商品信息主次、购买状态、举报动作、Mobile 价格与主动作前置 | `R1-P02`、`R1-F01` |
| 公开主页 | User PublicId / LongId 兼容、资料 / 宠物、帖子 / 评论、关注、私信、屏蔽、分享 | 身份摘要、权威统计、关系 CTA、内容 tab 与 Mobile 顺序 | `R1-P02`、`R1-F01` |
| Legal | 单一 `/legal`、原文语言标记、章节锚点、公开 / 私域与举报边界 | 法务长文本、锚点导航和移动阅读节奏 | `R1-P02`、`R1-F01` |

Forum 列表、Docs 列表、Shop 列表、排行榜，以及 Author、Private、Console 页面不进入本批。它们只在后续 R3 页面继承时消费本专题结论。

## 3. 正式路由、身份与 LongId

### 3.1 Docs

- `/docs/:slug` 使用稳定 slug；hash 仅表示正文锚点，旧 `/__documents__/:slug` 继续兼容读取并规范化为正式路径。
- `search / mine / compose / edit / revisions` 是保留段，不会误解析成公开详情 slug。
- 正式读取继续使用现有 `Wiki/GetBySlug/:slug`；公开、角色可见和登录可见边界由服务端当前文档访问规则裁决，页面不自行扩大 ACL。
- 文档实体、附件与作者 ID 继续保持 LongId 字符串；slug 不替代内部 LongId。

### 3.2 商品

- `/shop/product/:productId` 只接受正整数 LongId 字符串，读取时不转换为前端 `number`。
- 唯一现有写入回流参数是 `?intent=purchase`；canonical、Open Graph 与 sitemap 不携带 intent。
- 商品举报直接作用于当前权威 `product.voId`，复用 `ContentReportModal` 的 `number | string -> string` 归一化，不新增 `intent=report` 或第二套举报 URL。

### 3.3 公开主页

- `/u/:identifier` 同时接受 `usr_` 加 32 位十六进制 PublicId 与兼容期正整数 LongId；读取成功后优先规范化到 User PublicId。
- 帖子、评论、关注、私信与关系写入继续使用服务端返回的内部 LongId / PublicId，不把公开路由 identifier 错传给内部接口。
- `tab / page / intent=follow|message` 保持现状；屏蔽动作继续使用稳定 operation key 与服务端关系版本，不新增关系类型。

### 3.4 Legal

- Legal 只有 `/legal`，没有 API、权限或写入；`lang="zh-CN"` 是当前正式原文发布边界，不把未存在的多语言法务正文设计成可用能力。

## 4. Docs 详情事实

正式 Docs 详情已经具备：

- 独立 loading、not found、unavailable 与重试 / 返回状态；
- 标题、摘要、可见性、状态、slug、来源、创建 / 修改时间；
- Markdown 正文、受控内部链接改写、hash 锚点滚动和分享；
- Public 附件直读，非 Public 附件继续走受保护附件 token；
- 只有受权作者 / 管理员可见的正式 Author 编辑回流；
- 文档目录来源、相关文档和阅读辅助区在 Mobile 下移到正文之后。

WebOS `WikiApp` 的创建、编辑、发布、撤下、归档、回收站、导入 / 导出和版本回滚已经分别归属正式 Author / Console 能力，不得重新塞回 Public 只读详情。当前正式页面没有 Markdown 标题自动生成的章节目录，R2-P03 不凭空新增该能力；“目录”只表达既有文档树、来源与相关文档关系。

Docs 详情没有代码能力阻断，可直接作为长内容局部代表输入。

## 5. 商品详情事实与能力门禁

### 5.1 已具备

- `Shop/GetProduct/:productId` 允许匿名读取；商品不存在使用现有结构化 `404`，前端区分 not found 与 unavailable。
- 详情展示商品图、类型、分类、在售 / 缺货状态、价格、原价、销量、库存、限购、有效期、描述与权益值。
- 登录后购买继续走现有 `CheckCanBuy`、`PurchaseModal`、支付口令、幂等键与订单详情回流；未登录使用受控 `intent=purchase` 回跳。
- 商品分享、来源返回、canonical 与 `Product` 结构化数据已经存在。

### 5.2 必须先关闭

正式 `PublicShopApp / PublicShopDetailView` 没有 `Product` 举报动作，也没有挂载共享 `ContentReportModal`。这与以下现行事实不一致：

- WebOS `ShopApp / ProductDetail` 已传递 `onReport` 并以 `targetType="Product"` 打开共享弹窗；
- `contentModeration.ts`、后端 `ContentModerationController / Service` 和治理案件适配器均支持 `Product`；
- `ContentReportModal` 已把 LongId 规范化为字符串并复用结构化错误消费；
- Legal、用户承诺和内容治理专题明确商品举报属于正式 Web 用户路径。

门禁只增加当前商品的“举报”次级动作、登录态守卫和共享弹窗：匿名态与正式论坛保持同一提示登录口径，不新增 URL intent；登录态提交继续使用现有举报 API、原因类型、500 字说明限制、本人举报收件和治理事务边界。购买仍是商品详情唯一主动作，举报不能与购买同级抢占首屏。

## 6. 公开主页事实与能力门禁

### 6.1 已具备

- 公开资料、头像、装扮、宠物、发帖 / 评论 / 获赞统计、公开帖子 / 评论 tab、分页与真实空态；
- 登录回流后的关注 / 取消关注与私信 intent；
- 服务端能力决定的私信、关注、屏蔽 / 解除屏蔽，以及稳定 operation key、关系版本和失败关闭；
- 本人页面不显示对自己无意义的关注、私信和屏蔽动作；
- 分享、来源返回、排行榜入口、canonical 和 `ProfilePage` 结构化数据。

WebOS 个人窗口中的本人快回复、浏览历史、附件和社交管理属于 Private `/me`、`/circle` 等登录态产品，不进入公开主页。

### 6.2 必须先关闭

当前 `PublicProfileApp` 以 `Promise.all([getPublicProfile, getPublicUserStats])` 加载主资料与次要统计。结果是：

- 统计服务暂时不可用会让已可读的身份资料、宠物和公开内容整体消失；
- 不存在用户的两个请求均可能失败，若统计请求先返回，普通 `Error` 会盖住主资料请求的结构化 `404`；
- `getPublicUserStats`、公开帖子和评论的前端消费仍以普通 `Error` 丢失现有响应状态；
- 匿名或本人查看时没有调用登录态 `GetFollowStatus`，却仍固定渲染粉丝数 `—`，无法区分“未读取”与真实零值。

门禁应把公开资料设为详情存在性与 not found 的唯一权威请求；统计和公开内容作为可局部重试的次级读取，不得覆盖主资料。相关前端 API consumer 继续使用 `createApiResponseError` 保留现有 HTTP / code / messageKey，不修改服务端路由或响应结构。未取得权威粉丝数时不伪造 `0` 或把 `—` 当统计值；R2 设计只在“登录用户查看其他用户且关系状态读取成功”的代表身份中展示真实粉丝数和关系 CTA，其他身份使用明确的不可用 / 隐藏语义。

## 7. Legal 与长内容边界

Legal 当前是静态、公开、只读的产品承诺页：章节锚点、摘要、隐私安全边界和六组长文内容已经存在。它没有业务 API、用户操作、版本选择或 Console 权限。

R2-P03 只需要确保：

- PC 锚点导航不会与正文争抢主轴；
- Mobile 先读标题与摘要，再读必要边界和章节正文，锚点可横向 / 折叠浏览但不建立新壳层；
- 原文语言提示可见，不生成不存在的翻译选择器；
- 举报、公开 / 私域、虚拟商品和证据说明继续与真实产品能力一致。

## 8. 能力门禁顺序

1. **正式商品举报**：在 `PublicShopApp / PublicShopDetailView` 接入 `Product` 举报次级动作、登录守卫和共享 `ContentReportModal`；保持 LongId 字符串、现有错误和举报事务边界。
2. **公开主页权威加载**：主资料独立决定 loading / not found / unavailable；统计和内容失败局部化，API consumer 保留结构化错误，未读取粉丝数不伪装成真实值。
3. 增加定向静态 / 单元契约，覆盖正式商品举报、匿名守卫、LongId 字符串、公开主页主资料优先、统计降级、结构化 `404` 和真实统计呈现。
4. 通过 Client 测试、Lint、类型检查、production build、repo hygiene 与 `git diff --check` 后提交能力门禁记录，再请求 Pencil 授权。

## 9. Pencil 输入与代表身份

门禁关闭后，只在 `Docs/frontend/design-sources/radish-web-family-ui-v1.pen` 增加 R2-P03 局部代表设计，不复制 Docs、Shop、Profile、Legal 四个完整路由。

建议局部代表输入：

1. PC 长内容：Docs 的标题 / 元数据 / 正文主轴与目录来源，Legal 的锚点长文差异。
2. PC 动作详情：可购买商品的价格—状态—购买主动作—举报次动作，以及登录读者查看他人公开主页时的关注 / 私信 / 屏蔽与权威统计。
3. Mobile `390px`：统一验证“返回 / 身份或商品摘要 → 核心元数据 → 主动作 → 正文 / 公开内容 → 辅助 rail”的顺序；Legal 使用“标题 / 摘要 → 锚点 → 长文”。
4. 必要关键状态：not found、局部 unavailable、商品不可购买、关系 unavailable、真实内容空态和举报弹窗；等价 loading / error 不复制整页。

代表身份固定为：

- Docs：可读取 Published 文档的普通读者；
- 商品：查看在售商品的普通登录用户，具备购买检查与举报资格；
- 公开主页：登录用户查看非本人、关系能力可用的公开用户；
- Legal：匿名读者。

## 10. 停止线

- 不新增 API、权限种类、数据库字段、商品 PublicId、文档标题目录生成或 Legal 内容管理系统。
- 不改变 `/docs/:slug`、`/shop/product/:productId`、`/u/:identifier`、`/legal` 或既有 `intent`；不把 LongId 转为前端 `number`。
- 不新增购物车、退款、转售、现金价值、批量购买、推荐关注、关注隐私策略、完整关系图谱或实时统计。
- 不把 WebOS 的文档管理、本人历史 / 附件 / 社交管理或完整桌面壳层迁入 Public 页面。
- 不为四条路由、主题、locale 或等价状态复制完整画板，不建立新的移动壳层。
- 不提前推进 R3 Forum / Docs / Shop 列表、排行榜或其他 R2 页面。

## 11. 验证口径

能力门禁至少覆盖：

- 正式商品详情显示举报次级动作；匿名不能提交，登录态以 `Product` 和字符串 LongId 打开共享弹窗；提交继续消费结构化错误。
- 商品购买 `intent=purchase`、购买幂等、订单回流、canonical 与分享不受举报接入影响。
- 主资料成功、统计失败时公开主页仍可读并显示局部不可用；主资料 `404` 稳定呈现 not found，不被次要请求竞态覆盖。
- 匿名、本人和关系读取失败时不把粉丝数伪造成零值或权威统计；登录他人关系成功时显示真实计数与能力。
- Docs slug / anchor、受保护附件、Author 回流，Profile PublicId / LongId 规范化和 Legal 原文语言边界回归。
- PC / mobile 无横向溢出，Mobile 继续复用 `PublicShellHeader` 和 Client 既有导航，不创建新 shell。

本 readiness 不启动服务或浏览器；真实 Gateway PC / mobile smoke 仍在专题成组功能准备验收并获得当前任务启动授权后执行。

## 12. 主要证据

- 正式 Public：`PublicDocsApp.tsx`、`publicDocsApi.ts`、`PublicShopApp.tsx`、`PublicShopViews.tsx`、`PublicProfileApp.tsx`、`PublicCommitmentsApp.tsx`
- 路由与 head：`docsRouteState.ts`、`shopRouteState.ts`、`profileRouteState.ts`、`legalRouteState.ts`、`PublicEntry.tsx`、`publicHead*`
- 历史 WebOS：`WikiApp.tsx`、`ShopApp.tsx`、`ProductDetail.tsx`、`ProfileApp.tsx`
- HTTP 与共享组件：`api/shop.ts`、`api/user.ts`、`api/userFollow.ts`、`api/userBlock.ts`、`api/contentModeration.ts`、`ContentReportModal.tsx`
- API / Service：`WikiController / WikiDocumentService`、`ShopController`、`UserController`、`ContentModerationController / Service`
- 现行文档：[F4-R 代表页审计](/frontend/f4-r-representative-page-audit)、[家族 UI 收敛设计](/features/family-ui-convergence-design)、[用户承诺与隐私边界](/guide/user-commitments)、[内容治理案件设计](/features/content-moderation-case-evidence-action-design)
