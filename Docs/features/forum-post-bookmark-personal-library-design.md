# F4-P 论坛帖子收藏与个人内容回访

> **状态**：F4-P-B 服务端、Main migration 与统一 HTTP 客户端已完成；等待批准进入 C 批正式 Web
>
> **复核日期**：2026-07-29（Asia/Shanghai）
>
> **适用范围**：正式 Web 论坛、个人中心、Main 数据库、API、`@radish/http` 和 `radish.client`
>
> **前置专题**：[论坛公开应用](/features/forum-public-app) · [个人圈子](/features/circle) · [用户屏蔽与关系交互隔离](/features/user-block-relationship-isolation-design) · [写操作可靠性与并发保护治理](/guide/write-operation-reliability-governance)

## 一、结论摘要

F4-P 选择“论坛帖子收藏与个人内容回访”为下一项既有功能完成专题。

当前帖子已经有 `CollectCount / VoCollectCount` 投影字段，旧论坛规划和个人中心信息架构也长期保留“我的收藏”，但仓库没有用户收藏关系、收藏写入接口、当前用户状态或个人收藏分页。用户能阅读、点赞和产生浏览历史，却不能主动保存一篇希望长期回看的帖子；浏览历史又会随访问滚动，不能替代明确收藏意图。

核心裁决如下：

1. 收藏是当前用户拥有的私有内容整理关系，不是公开社交关系，不等同点赞。
2. 新增 `UserPostBookmark` 作为 Main 唯一真相；`Post.CollectCount` 只保留为可重建投影。
3. 收藏关系新增稳定 `bmk_` PublicId。外部接口不暴露关系 LongId，帖子目标继续优先使用 `pst_` PublicId。
4. 写入使用显式目标状态 `isBookmarked`，不使用裸 toggle；同状态重试必须无副作用。
5. 收藏写入先锁定目标 Post，再在同一 Main 事务中迁移关系状态和 `CollectCount`；只有真实状态迁移才改变计数。
6. 收藏不创建通知、Reliable Outbox、经验、萝卜币、奖励、动态或作者可见用户列表。
7. 正式 Web 首批只在帖子详情提供收藏动作；列表卡片继续服务扫读，不增加每张卡片的写操作密度。
8. 个人中心在 `/me/content?tab=bookmarks` 提供分页收藏列表，按最近收藏时间排序，并返回真实公开帖子链接。
9. 目标删除、治理限制或不可读取时，个人列表显示通用“内容不可用”占位，并允许通过 Bookmark PublicId 移除；不泄露删除或治理原因。
10. 本专题不建设收藏夹、分组、备注、标签、公开收藏主页、协作清单、推荐算法或跨对象收藏。

F4-P-B 已按上述边界落地：`UserPostBookmark` 是唯一关系真相，Repository 统一承载显式状态事务与“Post -> Bookmark”锁顺序，详情查询返回当前用户收藏态，个人分页批量组装可用摘要并脱敏不可用目标。Main migration 固定为 `20260729_017_forum_post_bookmark`，在正式 Main 的既有 User / Post baseline 上创建收藏表、重建全部 `CollectCount` 并执行严格校验；ledger 隔离自测没有 User baseline 时只建立空关系结构，不通过当前实体补建未来 baseline。`@radish/http` 已提供三项正式调用契约。B 批没有修改正式页面，也没有启动服务或执行浏览器 smoke。

## 二、F4-P-A 候选审计

### 2.1 候选比较

| 候选 | 当前基础 | 主要缺口 | 长期价值与边界 | 裁决 |
| --- | --- | --- | --- | --- |
| 帖子收藏与个人回访 | `CollectCount`、正式详情、`/me/content`、公开 PublicId 已存在 | 没有用户关系、写接口、个人列表和跨设备状态 | 直接补齐“阅读 -> 保存 -> 回访”，对象和页面边界集中 | **选定为 F4-P** |
| 标签导流与 SEO 深化 | 分类、标签、slug 直达、筛选、Console 已可用 | 热门导流与相关推荐仍可增强 | 当前已有可用闭环，进一步深化偏内容分发优化 | 后置 |
| 圈子关系流深化 | feed / following / followers 与屏蔽隔离已完整 | 推荐、转发和关系解释可增强 | 会进入推荐和内容再分发，不是当前确定性缺口 | 后置 |
| 投票 / 抽奖深化 | 两类 MVP、筛选、排序、结束 / 开奖已存在 | 改票、多选、运营、防刷和奖品履约 | 容易扩张为活动运营与风控，规划已明确后置 | 不回拉 |
| 匿名公开聊天 | 登录态 Chat 功能完整 | 没有匿名实时讨论面 | 与论坛讨论重叠并增加滥用、限流和身份风险 | 不启动 |

### 2.2 代码事实

- `Radish.Model/Post.cs` 已持久化 `CollectCount`，默认值为 `0`。
- `PostVo` 已输出 `VoCollectCount`，但没有 `VoIsBookmarked` 或收藏关系标识。
- 仓库存在 `UserPostLike`、事务化点赞关系和 `VoIsLiked`，没有对应收藏实体、Repository、Service 或 Controller action。
- `PostController` 注释仍写有“点赞、收藏”，实际只提供点赞写入。
- `/me/content` 当前只有 `posts / comments / quick-replies` 三个 tab，没有 `bookmarks`。
- 论坛长期说明保留“我的收藏”，但没有可执行契约。
- 浏览历史已有独立 `UserBrowseHistory`，语义是自动记录最近访问；它不能表达用户主动保存，也不能作为收藏关系复用。

### 2.3 为什么现在优先

- V1 产品定位强调内容沉淀与复访。收藏直接连接这两条主轴，不依赖推荐或生产数据采集。
- 帖子详情、PublicId、个人中心、分页列表和来源返回已经稳定，新增能力可以沿现有边界落地。
- `CollectCount` 是长期存在但没有权威来源的投影字段；继续放置会形成模型承诺与实际能力不一致。
- 收藏是低耦合私有关系，不需要通知、跨库 Outbox、资产或治理新对象，风险明显低于活动深化和推荐。

## 三、产品定位与用户路径

### 3.1 收藏与点赞的区别

| 维度 | 收藏 | 点赞 |
| --- | --- | --- |
| 核心目的 | 以后再次找到内容 | 对内容表达轻量认可 |
| 所有权 | 当前用户私有整理关系 | 内容互动关系 |
| 作者通知 | 不发送 | 沿现有点赞规则 |
| 奖励 / 经验 | 不产生 | 沿现有点赞规则 |
| 公开身份 | 不公开收藏者 | 不改变既有点赞展示 |
| 个人入口 | 我的收藏 | 本专题不新增“我的点赞” |
| 删除目标 | 保留不可用占位与移除能力 | 沿现有点赞关系规则 |

收藏数可以作为帖子公开聚合指标，但不能反推出收藏者身份。第一批页面只在详情动作区展示收藏状态和聚合计数，不把收藏数加入热度排序。

### 3.2 匿名读者

1. 匿名用户可以看到收藏动作与当前公开收藏数。
2. 点击收藏后进入统一登录，并保存受控 `intent=bookmark` 返回路径。
3. 登录成功后回到同一 canonical 帖子详情。
4. 页面只聚焦收藏动作，不自动替用户提交；避免跨身份登录后产生未经再次确认的写入。

### 3.3 登录读者

1. 在帖子详情点击“收藏”。
2. 客户端提交明确 `isBookmarked=true`。
3. 服务端返回权威状态、关系 PublicId 和最新收藏数。
4. 再次点击“取消收藏”时提交 `isBookmarked=false`。
5. 网络响应丢失后重试同一目标状态，不得反向切换。

### 3.4 个人收藏列表

1. 用户进入 `/me/content?tab=bookmarks&page=1`。
2. 列表按 `BookmarkedAt DESC, Id DESC` 稳定分页。
3. 可用目标展示标题、摘要、作者、时间、分类、标签与基础统计，并提供真实 `/forum/post/:postPublicId` 链接。
4. 普通点击写入一次性来源返回，使详情页可回到原收藏页；新开标签与复制链接保持公开 canonical。
5. 不可用目标只展示通用占位、收藏时间和“移除收藏”，不展示旧标题、正文、作者或治理原因。

## 四、目标与非目标

### 4.1 目标

- 建立可跨设备、可分页、可移除的帖子收藏权威关系。
- 保证重复请求、并发插入、取消后重收和响应丢失重试不会造成计数漂移。
- 外部路径只使用 Bookmark / Post PublicId，不新增 LongId 暴露。
- 让帖子详情与个人收藏列表消费同一权威状态。
- 在目标删除、限制、恢复和不存在时保留安全、可理解的个人整理语义。
- SQLite / PostgreSQL migration 支持 apply、重入、严格 verify 和旧库回归。

### 4.2 非目标

- 不增加评论、回答、Docs、商品、消息或用户收藏。
- 不增加收藏夹、分组、排序拖拽、颜色、备注、个人标签或批量导入导出。
- 不增加公开收藏主页、收藏者名单、关注者动态、分享清单或协作收藏。
- 不增加推荐算法、“收藏了也喜欢”、热门收藏榜或热度权重。
- 不增加收藏通知、邮件、Push、Outbox、经验、萝卜币、徽章或任务奖励。
- 不新增 Console 收藏运营页或管理员代用户收藏。
- 不扩 Flutter、WebOS 或 Tauri 专用页面；WebOS 只保持阻断级兼容。
- 不在本专题补“我的点赞历史”。

## 五、权威对象与字段

### 5.1 `UserPostBookmark`

建议字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `Id` | `long` | 内部 Snowflake 主键，不进入新外部契约 |
| `PublicId` | `varchar(36)` | `bmk_` 前缀稳定外部标识，全局唯一 |
| `TenantId` | `long` | 关系租户 |
| `UserId` | `long` | 收藏拥有者 |
| `PostId` | `long` | 目标帖子内部 ID |
| `BookmarkedAt` | `datetime` | 最近一次进入收藏状态的 UTC 时间 |
| `IsDeleted` | `bit` | 取消收藏后的软删除状态 |
| `DeletedAt` | `datetime?` | 最近取消时间 |
| `CreateTime / CreateBy / CreateId` | 审计字段 | 首次创建 |
| `ModifyTime / ModifyBy / ModifyId` | 审计字段 | 状态变更 |

关系规则：

- `(TenantId, UserId, PostId)` 唯一；
- `PublicId` 唯一且非空；
- 首次收藏插入关系；
- 取消收藏软删除；
- 再次收藏复用原关系、更新 `BookmarkedAt` 并清除删除字段；
- 不为每次收藏 / 取消追加事件表。当前关系与审计时间足以满足私有整理语义。

建议索引：

- unique `(TenantId, UserId, PostId)`；
- unique `(PublicId)`；
- page `(TenantId, UserId, IsDeleted, BookmarkedAt DESC, Id DESC)`；
- projection `(TenantId, PostId, IsDeleted)`。

### 5.2 `Post.CollectCount`

`CollectCount` 保留为公开聚合投影：

- 真相源是同租户、同帖子、`IsDeleted=false` 的 `UserPostBookmark` 数量；
- 收藏状态真实增加时 `+1`；
- 取消状态真实发生时安全 `-1`，不得小于 `0`；
- 同状态重试、唯一冲突后的权威重读不改变计数；
- strict verify 必须检查投影与关系重建值一致；
- 不进入 hottest、推荐或榜单排序。

### 5.3 ViewModel

帖子详情 / 列表现有 VO 增加：

- `VoIsBookmarked`：当前登录用户是否收藏；匿名恒为 `false`；
- `VoCollectCount`：沿用现有字段。

收藏写回使用独立 `PostBookmarkStateVo`：

- `VoBookmarkPublicId`；
- `VoPostPublicId`；
- `VoIsBookmarked`；
- `VoCollectCount`；
- `VoBookmarkedAt`。

个人列表使用 `UserPostBookmarkVo`：

- Bookmark PublicId 与收藏时间；
- `VoTargetStatus = Available | Unavailable`；
- Available 时返回安全帖子摘要和 Post PublicId；
- Unavailable 时不返回旧标题、正文、作者、分类、标签或具体原因。

## 六、Repository 与事务

新增：

- `IUserPostBookmarkRepository`
- `UserPostBookmarkRepository`
- `IUserPostBookmarkService`
- `UserPostBookmarkService`

Controller 只注入 Service，不直接访问 Repository 或 Db。

### 6.1 设置状态

`SetStateAsync(userId, postIdentifier, isBookmarked)` 在同一 Main 事务内：

1. 解析 Post PublicId；旧数字 ID 只留 Controller 兼容边界，新客户端不生成。
2. 校验帖子属于当前租户且不是硬缺失目标。
3. 先取得目标 Post 的数据库写锁，再读取唯一用户—帖子关系；同一帖子的收藏状态迁移按 Post 串行。
4. 当前状态与目标状态相同时，直接返回权威状态，不更新计数或时间。
5. `false -> true` 时插入或恢复关系，并原子增加 `CollectCount`。
6. `true -> false` 时软删除关系，并安全减少 `CollectCount`。
7. 提交后返回同事务结果。

锁定规则：

- PostgreSQL 在事务中对目标 Post 使用行级写锁，禁止用仅限单进程的内存锁替代；
- SQLite 依赖写事务的数据库级串行化，但执行顺序仍保持“Post -> Bookmark”；
- 所有新增收藏写入口都必须复用同一 Repository 事务方法和锁顺序，避免跨入口死锁与计数漂移；
- 锁内只执行关系迁移和计数更新，不承载通知、HTTP 或其他慢 IO。

唯一约束竞态：

- 两个并发首次收藏只有一个插入成功；
- 唯一约束保留为锁外异常与历史脏数据的最后防线，冲突后在事务内重读当前权威状态；
- 不允许两个请求都增加计数；
- 相反目标状态请求按 Post 锁顺序执行，每次真实迁移只产生一次计数变化；客户端最终以最后完成请求后的权威读取为准。

### 6.2 为什么不用 toggle

toggle 无法安全处理：

- 响应丢失后的相同请求重试；
- 双击或多标签重复提交；
- Gateway / 客户端超时后的人工重试；
- 离线恢复时重复发送旧动作。

显式 `isBookmarked` 是自然幂等目标状态。当前不额外写 `ContentSubmissionRecord`，因为：

- 唯一关系约束和条件状态迁移已能保证同目标重试无副作用；
- 收藏没有通知、资产或跨库副作用；
- 若未来增加批量收藏或跨对象收藏，再单独评审 operation ledger。

### 6.3 个人分页

Repository 从 Bookmark 关系出发稳定分页，不从 Post 反向猜测：

- 先取得当前用户有效 Bookmark 页；
- 批量读取目标 Post、作者、分类和标签；
- 使用现有公开可见性与租户规则构造 Available 项；
- 不可见或缺失目标返回 Unavailable，不回填旧快照；
- 禁止逐条 N+1 查询。

## 七、权限、隐私与状态变化

### 7.1 权限

- 只有已登录用户可以设置、读取和移除自己的收藏；
- 不提供管理员读取任意用户收藏的接口；
- 不提供作者读取收藏者列表的接口；
- Service 从当前身份取得 `UserId / TenantId`，不接受客户端指定拥有者；
- Bookmark PublicId 只能在当前用户作用域内读取或删除。

### 7.2 屏蔽关系

收藏是私有内容整理，不创建关系通知或作者身份可见记录。

- `UserBlock` 不删除既有收藏；
- 公开帖子仍按现有公共读取规则可见时，收藏列表可以继续打开；
- 收藏动作不恢复关注、私信、通知或其他被隔离关系；
- 本专题不改变现有 Like / Follow / Reward 的屏蔽策略。

### 7.3 帖子删除与治理限制

- Post 软删除、禁用或治理限制时，不物理删除 Bookmark；
- 个人列表返回 Unavailable 占位；
- 用户可以通过 Bookmark PublicId 软删除自己的关系；
- Post 恢复且重新可读时，尚未移除的 Bookmark 自动恢复 Available；
- 不向普通用户区分作者删除、管理员限制、申诉中、租户变化或其他原因。

### 7.4 用户与租户

- 被禁用或删除用户不能建立新收藏；
- 用户恢复后原收藏按目标当前可见性继续存在；
- Bookmark、User、Post 必须同租户；
- migration strict verify 遇到跨租户或孤立关系时失败，不猜测归属。

## 八、HTTP 与 `@radish/http`

建议新建 `PostBookmarkController`，沿用仓库 action route 风格。

### 8.1 设置收藏状态

`POST /api/v1/PostBookmark/SetState`

请求：

```json
{
  "postIdentifier": "pst_...",
  "isBookmarked": true
}
```

响应：`PostBookmarkStateVo`。

稳定错误：

- `PostBookmark.AuthenticationRequired`
- `PostBookmark.PostNotFound`
- `PostBookmark.PostUnavailable`
- `PostBookmark.UserUnavailable`
- `PostBookmark.StateConflict`

同状态请求返回 `200` 与当前权威状态，不使用 `409` 表达幂等重放。

### 8.2 我的收藏

`GET /api/v1/PostBookmark/GetMine?pageIndex=1&pageSize=20`

- 固定登录态；
- `pageSize` 上限 `50`；
- 返回 `VoPagedResult<UserPostBookmarkVo>`；
- 不允许传入其他 `userId`。

### 8.3 从个人列表移除

`POST /api/v1/PostBookmark/Remove`

```json
{
  "bookmarkIdentifier": "bmk_..."
}
```

- 只允许当前用户自己的 Bookmark；
- 目标 Post 已不可用或硬缺失时仍可执行；
- 已移除关系重复请求返回成功；
- 不泄露其他用户是否存在同一 Bookmark PublicId。

### 8.4 统一客户端

所有前端调用进入 `@radish/http`：

- `setPostBookmarkState`
- `getMyPostBookmarks`
- `removePostBookmark`

禁止在 `radish.client` 新增自定义 fetch / axios 封装。错误使用结构化 `ApiResponseError`，控制流不识别中英文消息。

## 九、正式 Web

### 9.1 帖子详情

在正式 `/forum/post/:postPublicId` 详情动作区增加：

- 未收藏：`收藏`；
- 已收藏：`已收藏` / `取消收藏`；
- 写入中：禁用当前动作并保留明确进度；
- 成功：更新按钮状态和 `VoCollectCount`；
- 失败：保留原状态，展示结构化错误；
- 匿名：进入 `intent=bookmark` 登录回流。

帖子详情继续保持：

- 点赞是轻互动；
- 收藏是私有回访；
- 举报是治理入口；
- 三者文案、图标和状态不可混用。

首批不在 `PostCard`、Discover、Circle、Profile 帖子列表和搜索结果增加收藏按钮，避免列表写操作扩散与状态 N+1。

### 9.2 个人中心

`MeContentTab` 新增 `bookmarks`：

- URL：`/me/content?tab=bookmarks&page=1`；
- PC：沿用内容页 segmented tabs 与预览 rail；
- mobile：单列卡片、稳定分页和明确移除动作；
- Available 项输出真实帖子链接；
- Unavailable 项只提供移除，不提供详情链接；
- 来源返回继续使用一次性 source transfer，不污染公开 URL。

### 9.3 双语、主题与无障碍

- `zh / en` 独立资源；
- 数字、日期按当前 locale；
- 用户内容保持原文；
- `default / guofeng / dark / sakura` 只使用语义 token；
- 键盘可达、可见焦点、`aria-pressed`、disabled / loading 状态完整；
- `390 × 844` 无水平溢出；
- reduced-motion 不依赖动画表达状态。

## 十、Migration 与严格校验

使用不可变 Main migration ID `20260729_017_forum_post_bookmark`。正式 Main 在既有 User / Post baseline 上前滚，迁移不通过当前实体 `CodeFirst` 偷建或补写基础表的未来字段；为兼容 ledger 隔离自测，没有 User baseline 且没有收藏关系时只建立空 Bookmark 结构，即使早期 migration 已物化局部 Post 结构也不误判为正式 baseline。已存在 User 但缺少 Post，或已有关系无法校验时仍失败。

Apply：

1. 创建 `UserPostBookmark`；
2. 增加 PublicId、唯一关系、个人分页和帖子投影索引；
3. 为已有合法关系补 PublicId；首批不存在旧关系时不伪造收藏历史；
4. 以有效关系重建 `Post.CollectCount`。由于当前没有收藏关系真相源，旧的非零投影不能保留为事实；
5. 注册 schema ledger 和 checksum。

Verify：

- 表、列、索引完整；
- Bookmark PublicId 非空、格式合法、唯一；
- `(TenantId, UserId, PostId)` 唯一；
- User / Post 存在且同租户；
- `CollectCount >= 0`；
- `CollectCount` 等于有效 Bookmark 重建值；
- 个人分页索引包含稳定 Id 尾键；
- 重入不产生重复关系或计数变化。

专项回归：

- SQLite baseline 空关系库 Apply / Verify / 重入；
- ledger 隔离库不创建 User，已有 User 但缺少 Post 时明确拒绝；
- 旧库只有 `CollectCount`、没有关系表；
- 重复关系、重复 PublicId、跨租户、孤立目标和错误计数故障注入；
- PostgreSQL 条件集成测试；
- 已进 ledger 的 migration 不修改，只以前滚顺序修正。

## 十一、测试与验收

### 11.1 Repository / Service

- 首次收藏、重复收藏、取消、重复取消、取消后重收；
- 两请求并发首次收藏只有一个关系和一次计数增加；
- 相反状态并发后关系与计数一致；
- 同一帖子跨用户并发和多实例入口保持固定锁顺序；
- 唯一冲突重读；
- 租户隔离；
- 不可用帖子；
- Bookmark PublicId 权限；
- 列表稳定分页与无 N+1；
- Post 删除、限制、恢复后的 Available / Unavailable；
- 无 Notification、Reliable Outbox、经验、萝卜币副作用。

### 11.2 HTTP / 前端

- 新接口只使用统一 HTTP 客户端；
- PublicId 与 LongId 兼容边界；
- 匿名登录回流；
- 详情状态更新与错误保留；
- `/me/content` route parse / build；
- Available 公开链接与来源返回；
- Unavailable 移除；
- 双语、四主题、键盘、mobile 和 reduced-motion 静态契约。

### 11.3 Gateway D 批

代表身份：

- 匿名；
- 普通收藏者；
- 帖子作者；
- 第三方读者；
- 被禁用 / 恢复目标由受权账号或代码侧矩阵覆盖。

真实路径：

- 登录回流；
- 收藏 / 取消 / 重收；
- 响应丢失同状态重试；
- 多标签并发；
- 个人列表分页；
- 详情返回；
- 目标删除 / 限制占位与移除；
- PC / mobile、`zh / en` 与代表主题。

验收结束清理 Bookmark、临时帖子、提交记录、浏览历史和浏览器状态，执行 migration verify 与六库完整性检查。

## 十二、A-D 批次

### F4-P-A：候选审计与权威设计

- **完成状态（2026-07-28）**：候选比较、代码事实、用户路径、权威对象、接口、权限、失败恢复、migration、页面与停止线已固定。
- 完成后汇报，等待明确批准进入 F4-P-B。

### F4-P-B：服务端与 migration

- **完成状态（2026-07-29）**：已新增 `UserPostBookmark`、专属 Repository / Service、DTO / Vo 和 `PostBookmarkController`。
- 显式状态、同状态幂等、Post 锁序、软删除重收、个人稳定分页、不可用目标移除和 `CollectCount` 同事务投影已落地。
- Main migration `20260729_017_forum_post_bookmark` 已注册；strict verify 覆盖表列索引、PublicId、关系唯一性、租户与孤立目标、稳定分页尾键及计数重建一致性。
- `@radish/http` 已提供 `setPostBookmarkState`、`getMyPostBookmarks` 与 `removePostBookmark`，PublicId / LongId 兼容只保留在 Controller 边界。
- 聚焦回归、完整 `validate:baseline`、changed-only 静态检查均通过；当前机器未配置 PostgreSQL 连接串，2 个 PostgreSQL 条件用例明确跳过，未表述为实跑通过。
- 完成后汇报，等待明确批准进入 F4-P-C。

### F4-P-C：正式 Web

- 帖子详情收藏动作与登录回流；
- `/me/content?tab=bookmarks`；
- PC / mobile、双语、四主题和无障碍状态；
- 不扩列表卡片收藏按钮。

### F4-P-D：成组验收

- Gateway 代表矩阵；
- 幂等、并发、删除 / 限制 / 恢复和不可用移除；
- 清理、六库完整性和 strict migration verify；
- 形成批次记录后关闭 F4-P。

## 十三、停止线

- 不把收藏解释为点赞、关注、推荐或作者反馈。
- 不公开收藏者身份，不为收藏发送任何通知。
- 不增加跨对象收藏或通用 Favorite 平台。
- 不增加收藏夹、批量整理、备注、标签、排序同步或导入导出。
- 不让 `CollectCount` 进入热度算法或排行榜。
- 不通过 localStorage 作为登录用户真相源。
- 不在 Service 直接使用 `_repository.Db.Queryable`。
- 不暴露 Bookmark / Post LongId 给新正式 Web 契约。
- 不在 B 批顺手修改 Like、BrowseHistory、Circle 或 Tag 既有语义。
