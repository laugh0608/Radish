# F4-S 公开排行榜参与资格、隐私边界与可信度治理

> 状态：A-C 已完成；D 批代码侧回归通过，Gateway PC / mobile 运行态复核待取得服务启动授权
>
> 正式入口：纯 Web `/leaderboard`、`/leaderboard/:type`
>
> 多端顺位：Web 优先；Flutter 只维护既有经验榜 MVP；WebOS 仅历史兼容；Tauri 不进入当前范围

## 摘要

排行榜是公开只读发现能力，不是资产、消费或订单信息的公开入口。

F4-S 固定以下长期边界：

1. 公开榜单只保留经验、发帖、评论、人气和热门商品五类。
2. 当前余额、累计消费和购买数量退出跨用户公开排名；本人数据继续由登录后的资产、交易与订单页面承载。
3. 用户榜单只纳入账号启用、未删除且目标指标有效的用户；内容榜单还必须只统计公开可见内容。
4. 列表、分页总数与“我的排名”必须复用同一资格和指标投影。
5. 排序固定为“指标降序、同值按稳定用户或商品 ID 升序”，列表序号与个人排名不得采用不同的并列规则。
6. 匿名读取不得创建、补写或修改用户公开身份；缺少 PublicId 时只保留既有数字 ID 兼容读取。
7. Web 只呈现服务端确认的公开类型；元数据请求失败时仅使用同一公开白名单的本地文案，不恢复已退役敏感类型。

## 1. 问题与目标

治理前的实现允许匿名请求八类榜单，其中包括：

- 当前余额；
- 累计获得与累计消费；
- 购买商品数量。

同时，用户榜单先按指标分页，再单独装配用户；用户是否启用、是否删除与公开资料是否可达没有成为聚合查询的共同条件。这会造成以下问题：

- 资产和消费行为越过本人可见边界；
- 禁用或删除账号可能进入榜单候选；
- 分页数据、总数和个人排名可能使用不同参与者集合；
- 同值项目没有稳定第二排序键，跨页顺序可能漂移；
- 个人排名查询把内部失败吞成 `0`，无法区分“未上榜”和“查询失败”；
- 排行榜匿名读取复制了一套 PublicId / PublicIndex 补写逻辑，与身份服务的保留号和并发分配规则分叉。

F4-S 的目标不是增加榜单玩法，而是把既有公开榜单收口为可信、可解释、不会泄露本人数据的只读产品能力。

## 2. 公开类型白名单

公开类型顺序由服务端元数据决定，固定如下：

| 顺序 | 类型 | 路由 | 排名依据 | 公开理由 |
|---:|---|---|---|---|
| 1 | `Experience` | `/leaderboard` | 有效用户累计经验 | 公开成长与长期参与信号 |
| 2 | `PostCount` | `/leaderboard/post-count` | 公开有效帖子数量 | 来源内容本身公开 |
| 3 | `CommentCount` | `/leaderboard/comment-count` | 公开有效评论数量 | 来源内容本身公开 |
| 4 | `Popularity` | `/leaderboard/popularity` | 公开帖子与评论获得的点赞总数 | 来源互动信号公开 |
| 5 | `HotProduct` | `/leaderboard/hot-product` | 在售有效商品销量 | 商品聚合热度，不指向买家 |

`LeaderboardType` 的既有数值不重排，避免旧请求被解释成另一种类型。公开白名单只决定哪些既有枚举值可通过排行榜 API 与客户端路由使用。

## 3. 退出公开排名的类型

| 类型 | 退出原因 | 正式承载位置 |
|---|---|---|
| `Balance` | 当前余额与累计获得属于本人资产 | `/me/assets`、个人钱包 |
| `TotalSpent` | 累计消费属于本人交易统计 | `/me/assets`、交易记录 |
| `PurchaseCount` | 购买频率属于本人消费行为 | `/me/orders`、订单记录 |

这些类型不再由以下接口返回或接受：

- `GET /api/v1/Leaderboard/GetTypes`
- `GET /api/v1/Leaderboard/GetLeaderboard`
- `GET /api/v1/Leaderboard/GetMyRank`

旧枚举值可以保留为反序列化兼容边界，但服务端必须返回稳定的“类型不可公开”业务错误，不得查询后再过滤响应。

旧 Web 路由 `/leaderboard/balance`、`/leaderboard/total-spent`、`/leaderboard/purchase-count` 统一安全回落到 `/leaderboard`。客户端不得为这些地址保留不可见 Tab、静态元数据或请求 fallback。

## 4. 用户参与资格

用户类榜单的共同资格为：

```text
User.Id > 0
AND User.IsEnable = true
AND User.IsDeleted = false
AND 指标来源满足对应公开可见条件
```

PublicId / PublicIndex 的职责：

- 新用户继续由身份创建链路分配公开身份；
- 榜单读取只消费已有公开身份，不负责补写；
- 已有合法 PublicId 时优先返回并生成 `/u/usr_...`；
- PublicId 缺失或非法时不公开错误值，客户端只使用既有 `/u/:userId` 兼容入口；
- 缺少 PublicIndex 时不伪造 DisplayHandle。

本专题不新增独立“退出排行榜”字段。未来若产品建立统一的公开资料可见性或排行榜退出设置，应在用户隐私契约中统一扩展，并由全部用户榜单复用，不能为单一榜单增加散点开关。

## 5. 指标资格

### 5.1 经验榜

- `UserExperience.IsDeleted = false`；
- 未冻结，或存在已经到期的冻结时间；
- 永久冻结和仍在冻结期的用户不参与列表、总数和个人排名。

### 5.2 发帖榜

- `Post.IsPublished = true`；
- `Post.IsEnabled = true`；
- `Post.IsDeleted = false`；
- 作者满足共同用户资格。

### 5.3 评论榜

- `Comment.IsEnabled = true`；
- `Comment.IsDeleted = false`；
- 作者满足共同用户资格。

当前评论模型没有独立的帖子可见性投影。F4-S 只沿用既有“有效评论”指标，不顺带改变评论领域的历史计数定义；如果以后要求评论必须随父帖子可见性退出统计，应在评论公开读取专题中统一治理。

### 5.4 人气榜

- 只聚合符合发帖榜和评论榜内容条件的 `LikeCount`；
- 总点赞数必须大于 `0`；
- 帖子与评论聚合必须先应用同一用户资格，再合并；
- 列表、总数和个人排名复用同一合并结果。

### 5.5 热门商品榜

- `Product.IsEnabled = true`；
- `Product.IsOnSale = true`；
- `Product.IsDeleted = false`；
- `Product.SoldCount > 0`。

商品榜只公开商品名称、价格、图片、销量与商品详情入口，不返回买家身份或订单数据。

## 6. 排名与分页可信度

所有公开榜单使用确定性全序：

```text
PrimaryValue DESC, StableEntityId ASC
```

- 用户榜的稳定尾键为内部 `User.Id`；
- 商品榜的稳定尾键为内部 `Product.Id`；
- `VoRank` 为该全序中的一基序号；
- 同值用户不会共享名次；这是为了保证分页、URL 回访和“我的排名”严格一致；
- `DataCount` 统计应用全部资格条件后的实体数量；
- `PageCount` 由 `DataCount` 和规范化后的 `PageSize` 计算；
- `GetMyRank` 返回同一全序中的位置，`0` 只表示用户不具备资格或没有有效指标；
- 查询异常必须向上抛出并由统一错误处理记录，不能伪装成未上榜。

当用户状态在一次请求执行期间发生并发变化时，最终用户装配仍需再次应用共同资格；允许出现短暂名次空洞，但不得把已经确认失效的用户重新补入响应。

## 7. API 契约

### `GET /api/v1/Leaderboard/GetTypes`

- 匿名可读；
- 只返回五类公开元数据；
- 顺序为经验、发帖、评论、人气、热门商品。

### `GET /api/v1/Leaderboard/GetLeaderboard`

- 匿名可读；
- `pageIndex` 小于 `1` 时规范化为 `1`；
- `pageSize` 小于 `1` 时规范化为 `50`，大于 `100` 时限制为 `100`；
- 非公开或未知类型返回 `Leaderboard.TypeUnavailable`；
- 不执行敏感类型查询。

### `GET /api/v1/Leaderboard/GetMyRank`

- 需要登录；
- 只支持四类公开用户榜；
- `HotProduct` 返回 `Leaderboard.UserRankUnavailable`；
- 退出公开的敏感类型和未知类型返回 `Leaderboard.TypeUnavailable`。

## 8. Web 与多端边界

### 正式 Web

- `publicLeaderboardTypeRouteDefinitions` 只登记五类公开路由；
- 服务端返回的类型先与公开路由白名单求交集；
- 服务端成功返回较小类型集时，以服务端为准，不通过本地 fallback 重新扩张；
- 类型元数据请求失败时，本地 fallback 也只能包含五类公开类型；
- 旧敏感 slug 与未知 slug 回落到经验榜，分页同时规范化；
- 页面沿用 F4-R 之前的既有视觉结构，本专题不提前实施 family-ui 视觉重构。

### Flutter

- 继续只请求 `Experience = 1`；
- 继续保持首屏只读经验榜、公开主页跳转和 Android Back 回流；
- 不新增其他榜单 Tab，也不机械追平 Web。

### WebOS 与 Tauri

- WebOS `/desktop` 榜单应用只消费服务端公开类型，不保留敏感类型静态入口；
- WebOS 不在本专题增加新功能或新布局；
- Tauri 暂时弃用，不进入开发、CI、发布或验收门禁。

## 9. 实现边界

后端应把公开排名聚合放在 `ILeaderboardRepository` / `LeaderboardRepository`：

- 仓储负责用户资格、指标资格、确定性排序、分页总数与个人排名；
- Service 负责类型门禁、分页参数规范化、公开用户与等级信息装配；
- Controller 负责稳定业务错误和匿名 / 登录入口；
- Controller 不直接访问仓储；
- Service 不直接使用 `_repository.Db.Queryable`。

本专题不新增数据库字段或正式 migration。用户状态、PublicId、PublicIndex、内容状态与商品状态均复用既有模型。

## 10. 测试与验收

### 后端定向测试

- 五类公开类型顺序和三类敏感类型拒绝；
- Controller 的 `TypeUnavailable` / `UserRankUnavailable` 契约；
- 禁用、删除、冻结用户不进入列表、总数或个人排名；
- 同值指标按稳定 ID 排序，分页 `VoRank` 与个人排名一致；
- 公开帖子 / 评论条件与人气合并；
- 热门商品状态条件；
- 榜单读取不补写缺失 PublicId / PublicIndex；
- SQLite 聚合翻译；
- 配置 PostgreSQL 测试连接时复核相同查询翻译。

### 前端定向测试

- 公开路由只包含五类；
- 三个旧敏感 slug 和未知 slug 回落经验榜；
- 本地 fallback 不包含敏感类型；
- 服务端类型结果与公开白名单求交集；
- 路由、canonical、分享链接和分页链接不再生成敏感路径。

### 验证粒度

- 开发中执行后端定向测试、前端定向测试、type-check、build、changed-only lint、仓库卫生与 `git diff --check`；
- 本专题代码侧收口不启动服务；
- Gateway PC / mobile 真实 smoke 留到成组验收，启动前必须取得当前任务授权；
- F4-R 后续只负责视觉重构，不重新解释 F4-S 的功能、文案、权限和类型边界。

2026-07-30 的代码侧结果见 [F4-S 公开排行榜治理代码侧验收记录](/records/f4-s-leaderboard-public-governance-code-acceptance-2026-07-30)。当前机器未配置 PostgreSQL 条件测试连接，SQLite 结果不表述为 PostgreSQL 实跑。

## 11. 非目标

- 不新增赛季榜、周榜、好友榜、奖励或排行榜运营后台；
- 不增加 Redis Sorted Set、SignalR 实时排行或新的缓存基础设施；
- 不恢复公开资产、消费或购买行为比较；
- 不新增独立隐私设置、全量 PublicId 迁移或排行榜参与 migration；
- 不扩展 WebOS、Tauri 或 Flutter 多榜单能力；
- 不在 Pencil 空闲前读取、修改 `.pen` 或提前实施页面视觉重构。
