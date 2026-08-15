# F4-R R3 路由继承实施分批审计

> 日期：2026-08-11
> 状态：正式路由、继承来源、六组实现与 Gateway 运行态验收均已完成，Web R3 路由继承批次关闭
> 范围：正式 Client / Console Web 路由、R3 继承成立性、局部差异、关键状态、Mobile 转换与验证边界

## 1. 结论

现有 R3 继承方向总体成立，不需要因为进入派生路由实施而重新建立完整 Pencil 画板，也没有发现必须立即升级为新 R1 / R2 的壳层或响应式模型。R3 不能解释为“所有正式路由仍未完成”：Forum 详情、Author 编辑、消息工作区、Console 订单 / 治理，以及四个 R2 覆盖的详情、Private / Workbench、Author 列表 / 修订 / Compose、角色 / 权限 / System Config 已经形成设计—实现—Gateway 闭环，后续只做共享回归，不重复视觉迁移。

正式代码反查同时发现原继承表存在两个覆盖缺口：Console Dashboard 与后续新增的 Channel Discoverability 没有登记继承来源；Console Settings / Profile、公开 Docs 详情与 Legal 虽已登记，但没有被前述 R2 实现批实际关闭。上述页面已纳入新的后续批次，不以历史表述冒充完成。

`R3-P04 Public Forum 浏览族`、`R3-P05 Public Docs 与 Legal` 与 `R3-P06 Public Shop 浏览与 Leaderboard` 均已按继承结论完成正式实现和 Gateway 成组验收。`R3-C04 Console 普通资源` 随后按六批关闭；[R3-C05 成组运行态验收](/records/f4-r-r3-c05-console-dashboard-governance-stage-acceptance-2026-08-13)确认 Dashboard、Channel Discoverability、Documents 与 Experience 四批代码和运行态闭环成立。[R3-F02 专题审计](/records/f4-r-r3-f02-self-service-boundary-readiness-audit-2026-08-13)确认最后一组的视觉继承同样成立，并按协议信任、权威状态与错误边界拆为 A / B / C。

## 2. 正式路由事实

### 2.1 Client

- `entryRoute.ts` 当前把浏览器入口分为 Public、Messages、Notifications、Pet、Me、Circle、Private Shop、Docs Author、Workbench、OIDC 与 Root；WebOS `/desktop` 仍是历史兼容入口。
- Public 由单一 `PublicEntry` 承接 Discover、Forum、Docs、Shop、Leaderboard、公开 Profile 与 Legal；各路由已经具备独立 URL 解析 / 构建、来源返回和公开 head 生命周期。
- Forum 浏览面包含 `/forum`、`/forum/category/:id`、`/forum/search`、`/forum/tag/:slug`、`/forum/question`、`/forum/poll` 与 `/forum/lottery`；详情和 `/forum/compose` 已由 R1 / R2 关闭。
- Docs 包含列表、搜索、详情与保留 slug 回退；Shop 包含首页、商品列表和详情；Leaderboard 包含五类受控公开榜单。
- Private / Workbench、Docs Author 与 Messages 已由既有 R1 / R2 成组实现覆盖，不进入新的 R3 页面批。

### 2.2 Console

- `routeMeta.ts` 当前登记 `22` 个 Console 页面元数据；`hangfire` 为外部运维工作面，`theme-test` 为内部验证页，继续排除。
- 六个 R1 与 R2-C03 已关闭 Orders、Moderation、Roles / Permissions 与 System Config；普通资源页、Dashboard、Documents、Experience、Channel Discoverability、Settings / Profile 和边界页仍需按 R3 分批复核。
- `Users` 包含独立 `/users/:userId` 详情，`Stickers` 包含独立 group items；它们继续归属对应资源族，不因存在详情路由自动升级为治理工作台。
- Channel Discoverability 同时具备资源列表、显隐写操作和历史事件，继承应为 `R1-C01 + R1-C02`，不能只按普通表格处理。

## 3. 继承成立性与实现风险

### 3.1 Public

- 四个公开浏览模块已经共享 `PublicShellHeader`、语义 token、公开导航、`WebStateSlot`、URL 状态和 head / structured data 基础，R1-P01 的壳层与连续内容语法可直接继承。
- Forum、Docs、Shop 与 Leaderboard 仍保留各自字段、筛选、分页、辅助 rail 和关键状态；R3 只统一密度、顺序和响应式语法，不把不同业务压成同一个万能卡片。
- 审计时 `PublicForumApp.module.css` 为 `1646` 行，并同时服务浏览、详情和 Compose；P04 已按真实所有权隔离 browse 样式，原文件降至 `1185` 行，未做全文件机械格式化或新增空泛包装层。
- 审计时 `PublicDocsApp.tsx` 为 `1554` 行；P05 已按列表 / 搜索 / 详情真实职责拆为 `279` 行编排器与独立 owner。Shop 与 Leaderboard 只按实际职责收敛，没有为控制行数机械拆分。
- 审计时 Leaderboard 有三处奖牌渐变硬编码色；P06 已转为明确的榜单语义 surface，没有在 Forum 批跨模块顺手修改。

### 3.2 Console

- 多数资源页已经使用 `ConsolePageHeader`、`ConsoleMetricCard`、统一权限和 Ant Table；C04-A 已增加无业务状态的 `ConsoleResourceList`，先由 Categories / Tags 接入 PC 连续表格—Mobile 卡片，后续资源仍需逐批接入按需详情和 Mobile 单任务转换。
- Documents `1425` 行且包含资源表、治理动作与多组历史；Experience 由多子区块承接复杂治理，二者应进入 `R1-C01 + R1-C02` 批，不与普通 CRUD 页面混改。
- Dashboard 已具备主区—辅助区和 Mobile 单列，但原继承表漏记；它应继承 `R1-F01 + R2-W02` 的主任务优先、紧凑摘要和辅助信息规则，同时保留 Console 壳层。
- Applications、Products、Users、Categories、Tags、Stickers、Coins 的表格 / 表单 / Modal 复杂度差异明显；C04 审计已把它们拆为六批，Categories / Tags 先建立共同列表壳层，其余资源按风险逐批接入，不能一次改完全部资源。

## 4. R3 分批顺位

| 顺位 | 建议批次 | 正式范围 | 继承来源 | 进入条件 |
| --- | --- | --- | --- | --- |
| 1 | `R3-P04 Public Forum 浏览族` | Forum 列表、分类、搜索、标签、Question / Poll / Lottery 类型流 | `R1-P01 + R2-P03` | 先确认 browse 样式所有权拆分和响应式矩阵 |
| 2 | `R3-P05 Public Docs 与 Legal` | Docs 列表、搜索、详情、保留 slug 与 Legal | `R1-P01 + R1-P02 + R2-P03` | 先拆分超限 `PublicDocsApp.tsx`，保持受保护资源与锚点契约 |
| 3 | `R3-P06 Public Shop 浏览与 Leaderboard` | Shop 首页 / products、五类榜单；商品详情只做回归 | `R1-P01 + R2-P03` | 固定商品 CTA / 登录回流、榜单只读边界和颜色 token |
| 4 | `R3-C04 Console 普通资源` | Applications、Products、Users、Categories、Tags、Stickers、Coins | `R1-C01` | 先按读写风险拆子批，不复制七套 Mobile 状态机 |
| 5 | `R3-C05 Console 仪表与治理派生` | Dashboard、Documents、Experience、Channel Discoverability | `R1-F01 + R2-W02` 或 `R1-C01 + R1-C02` | 对每页明确主任务、证据 / 事件和写权限边界 |
| 6 | `R3-F02 自服务与边界页` | Console Settings / Profile，Client / Console 登录、OIDC 回流、Not Found | `R1-F01 + R2-C03` | 保持身份与错误原因，不新增认证能力 |

已关闭代表页和 R2 页面不列入上表；Hangfire、theme-test、WebOS 新功能、Tauri 与 Flutter 机械追平继续后置。

## 5. 首批 `R3-P04` 实施边界

### 5.1 必须保留

1. 分类、标签、关键词、时间范围、排序与分页继续由现有 URL route state 驱动；刷新、前进 / 后退和 canonical 不能丢失状态。
2. 列表、分类、标签元数据、相关标签、神评摘要和类型流保持独立权威读取；局部失败不能覆盖已取得的帖子结果，也不能把 unavailable 伪装为空。
3. Tag 禁用 / 删除、搜索空词、筛选空结果、首屏加载、分页失败和 stale 内容需要可辨识；Question / Poll / Lottery 保留各自字段与排序能力。
4. PC 保持连续内容主轴与从属上下文；Mobile 固定主任务先于辅助信息，筛选可折叠或横向滚动，但正文卡片与页面不得产生横向溢出。
5. Compose CTA、登录回流、帖子来源返回、公开 head、canonical、JSON-LD 与 sitemap 契约保持不变。

### 5.2 允许修改

- browse 专属样式所有权、页面密度、筛选带、卡片元信息顺序、辅助 rail、Mobile 折叠方式与必要双语系统文案。
- 为隔离已关闭的详情 / Compose 回归而进行的真实职责拆分，以及覆盖继承契约的定向静态测试。

### 5.3 停止线

- 不新增 API、数据库、migration、权限、Forum 业务字段、排序类型、推荐模型或第二套导航状态。
- 不修改帖子详情互动、Revision、Composer 草稿 / 上传 / 提交状态机，不恢复 WebOS 页面结构。
- 不修改 Pencil；若真实实现证明 R1-P01 / R2-P03 继承不成立，停止代码扩张并重新裁决 R1 / R2。
- 不为追求统一建立万能 Public 卡片、万能 rail 或只转发参数的抽象。

## 6. 首批验证矩阵

开发中先执行 Forum route / view-state / SEO / head 定向测试、Client 全量测试、type-check、Lint、production build、`git diff --check` 与变更卫生。专题代码和静态门禁成组完成后，再按当前任务授权启动 Gateway，覆盖：

- PC `1440 × 900` 与 Mobile `390 × 844`；
- `/forum`、分类、搜索、有效 / 不可用标签，以及 Question / Poll / Lottery；
- loading、空结果、局部 unavailable / stale、分页与 URL 前进 / 后退；
- `zh-CN / en-US`、`default / guofeng` 代表状态；
- 无横向溢出、关键可访问名称与干净标签页 `0 warning / 0 error`。

运行态不制造帖子、标签或类型流业务数据；种子数据无法覆盖的状态由自动化与受控失败守卫，不以改写数据库补齐视觉样本。

## 7. 下一步

`R3-P04`、`R3-P05`、`R3-P06`、`R3-C04` 六批、`R3-C05` 四批与 `R3-F02-A / B / C` 均已完成；[R3-F02 Gateway 成组运行态验收](/records/f4-r-r3-f02-grouped-runtime-acceptance-2026-08-15)关闭最后一组身份、自服务和错误边界。Web R3 路由继承批次至此关闭，既有代表继承成立且没有新增 Pencil；下一顺位进入 Web 主题基线与专题退出门禁审计。
