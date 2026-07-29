# F4-Q 论坛标签公开发现、可见性与 SEO 闭环

> **状态**：F4-Q-A/B 已完成；等待批准进入 C 批正式 Web
>
> **复核日期**：2026-07-29（Asia/Shanghai）
>
> **一句话结论**：复用既有 `Tag / PostTag / Post`，统一公开帖子可见性，补齐标签相关主题、Gateway 首包 head 和动态 sitemap，让“发现标签 -> 阅读聚合 -> 切换相关主题 -> 打开帖子”形成稳定公开闭环。

## 一、立项结论

F4-Q 不新增新的内容对象，也不启动推荐系统。当前标签能力已经具备管理、发布、公开 slug 直达、帖子筛选和运行时 head，但仍存在四个确定性缺口：

1. 公开帖子列表和详情没有与 sitemap、Gateway head、收藏不可用态共享同一套 `IsPublished && IsEnabled && !IsDeleted` 可见性口径；
2. `/discover` 已展示热门标签，标签页本身却没有相关主题入口，阅读路径在单个标签聚合页终止；
3. `/forum/tag/:tagSlug` 只有浏览器运行时 head，没有 Gateway 首包 head snapshot；
4. 动态 sitemap 只覆盖公开帖子、文档和商品，没有收录可公开阅读的标签聚合页。

F4-Q 选择在既有标签关系上完成公开发现与索引闭环。它直接服务“内容发现、讨论沉淀、公开回流”主轴，边界比圈子推荐、投票 / 抽奖运营或匿名公开聊天更稳定。

F4-Q-A 完成审计与设计；F4-Q-B 已完成服务端公开读取、标签聚合、Gateway 首包 head 与 tags sitemap，不包含正式 Web 或服务启动。

## 二、候选审计

### 2.1 候选比较

| 候选 | 当前基础 | 确定性缺口 | 长期价值与风险 | 裁决 |
| --- | --- | --- | --- | --- |
| 标签公开发现与 SEO | Tag、PostTag、canonical slug、公开标签页、热门标签 rail、Console 均已存在 | 公开可见性不统一、无相关主题、无标签首包 head、无标签 sitemap | 复用既有内容关系，补公开发现与回流；不增加用户状态或写模型 | **选定为 F4-Q** |
| 圈子关系流深化 | `/circle` 已有 feed / following / followers、屏蔽隔离、稳定分页和来源返回 | 后续主要是推荐、转发、关系解释和再分发 | 会进入推荐规则与新社交动作，不是当前确定性缺口 | 后置 |
| 公开个人页首包 SEO / sitemap | PublicId、公开主页、运行时 head 已存在 | Gateway 首包与收录策略后置 | 需要先裁决用户展示意愿、隐私和索引退出策略 | 后置 |
| 投票 / 抽奖深化 | 发布、参与、结束 / 开奖已有 MVP | 改票、多选、防刷、奖品履约和 Console 运营 | 容易扩张为活动风控和资产履约 | 不回拉 |
| 匿名公开聊天 | 登录态 Chat 已完整 | 没有匿名身份、治理、限流与公开房间契约 | 与论坛讨论重叠，滥用和实时治理风险高 | 不启动 |

### 2.2 代码与契约事实

- `Tag` 已持久化 `Name / Slug / Description / Color / SortOrder / PostCount / IsEnabled / IsFixed / IsDeleted`。
- `PostTag` 是帖子与标签的既有关系；一个帖子发布或编辑时要求 `1~5` 个已确认标签。
- `TagSlugHelper` 已统一稳定 ASCII canonical slug；旧标签名路径可解析后回写 canonical。
- 后端已有：
  - `GET /api/v1/Tag/GetFixedTags`
  - `GET /api/v1/Tag/GetHotTags`
  - `GET /api/v1/Tag/GetBySlug/{slug}`
  - `GET /api/v1/Post/GetList?tagSlug=...`
- `/discover` 已读取最多 6 个热门标签，并输出真实 `/forum/tag/:tagSlug` 链接。
- `/forum/tag/:tagSlug` 已具备标签上下文、最新 / 最热排序、稳定分页、帖子卡片、详情来源返回、错误态和 runtime head。
- `PublicHeadSnapshotMiddleware` 只识别 forum 帖子详情，没有识别 forum 标签聚合路径。
- `PublicSitemapService` 只输出 `static / forum / docs / shop` 分片，没有标签分片。
- `PublicSitemapService` 与帖子 head snapshot 已使用 `Post.IsPublished && Post.IsEnabled && !Post.IsDeleted`；公开 `Post.GetList` 和 `Post.GetById` 当前只排除未发布或软删除内容，没有统一排除 `IsEnabled=false`。
- `Tag.PostCount` 是写入时维护的关系投影，不足以单独证明当前有多少帖子仍可公开阅读；公开数量、热门候选、相关标签和 sitemap 资格不能仅依赖该字段。

### 2.3 为什么不继续深化圈子

`/circle` 已经回答“我关注的人最近有什么动态”，并具备：

- 关注动态、我的关注、我的粉丝；
- 关系摘要与稳定分页；
- `UserBlock` 双向隔离；
- 帖子和公开个人页真实 `href`；
- 原 tab / page 来源返回；
- 双语、移动布局和正式 Web 路由。

圈子下一步若继续扩展，主要会进入推荐解释、关注推荐、转发 / 引用或新短内容对象。这些不是现有关系链的缺失动作，也会扩大数据分发和治理边界，因此不作为 F4-Q。

## 三、产品目标与用户路径

### 3.1 产品目标

F4-Q 完成三个层次：

1. **公开可见性同源**：所有匿名公开 forum 列表、详情、标签统计、相关标签、head 和 sitemap 使用同一公开帖子判定。
2. **主题发现闭环**：访客可以从发现页或帖子标签进入聚合页，再通过相关标签继续阅读相邻主题。
3. **搜索与分享闭环**：标签聚合页有稳定 canonical、首包 head、`CollectionPage` JSON-LD 和 sitemap 入口。

### 3.2 代表用户路径

#### 路径 A：热门标签进入

1. 匿名或登录用户打开 `/discover`；
2. 在热门标签 rail 点击 `#react`；
3. 进入 `/forum/tag/react`；
4. 查看标签说明、公开帖子数量和帖子列表；
5. 打开帖子详情；
6. 返回时恢复原标签、排序和页码。

#### 路径 B：相关主题继续阅读

1. 用户位于 `/forum/tag/react`；
2. 页面加载与 `react` 在公开帖子中共同出现的相关标签；
3. 用户点击 `#typescript`；
4. 进入 `/forum/tag/typescript`；
5. 浏览器普通点击保留来源，右键、新标签和复制链接只使用真实公开 URL。

#### 路径 C：搜索引擎与外链

1. crawler 请求 `GET` 或 `HEAD /forum/tag/react`；
2. Gateway 从 API 获取标签 head snapshot；
3. 首包包含标签名称、描述、canonical、Open Graph、Twitter card 与 `CollectionPage` JSON-LD；
4. `/sitemap.xml` 包含 tags 分片，tags 分片只列出至少有一篇当前公开帖子的启用标签；
5. 页面加载后 runtime head 与首包 canonical 保持一致。

#### 路径 D：标签不可用

1. 标签被禁用或软删除；
2. 公开标签详情、相关标签、head snapshot 和 sitemap 都不再把它视为可公开标签；
3. 公开页面显示通用不可用状态，不回退到全站帖子，也不泄露后台状态或删除原因；
4. 已缓存 head / sitemap 最迟在既有 TTL 内失效，不把失败开放误表述为索引成功。

## 四、权威对象与可见性

### 4.1 权威对象

- `Tag`：名称、canonical slug、说明、运营状态和排序的唯一真相。
- `PostTag`：帖子与标签关系的唯一真相。
- `Post`：帖子发布、启用、删除和内容状态的唯一真相。
- `Tag.PostCount`：可重建关系投影，不是公开可见数量的唯一真相。

F4-Q 不新增 `UserTagFollow`、`TagSubscription`、`TagRecommendation`、`TagPage` 或独立 SEO 文案表。

### 4.2 公开帖子判定

F4-Q 固定公开帖子判定为：

```text
Post.IsPublished = true
AND Post.IsEnabled = true
AND Post.IsDeleted = false
```

该判定至少统一用于：

- forum 普通列表；
- 问答、投票和抽奖公开列表；
- 公开帖子详情；
- 标签页公开数量；
- 热门标签候选；
- 相关标签共现计算；
- forum / tags sitemap；
- forum post / tag Gateway head snapshot。

作者态、治理态、版本历史和个人收藏不可用占位仍按各自受权契约读取，不因公开判定而丢失治理能力。

### 4.3 公开标签判定

标签同时满足以下条件才可进入公开发现和索引：

```text
Tag.IsEnabled = true
AND Tag.IsDeleted = false
AND canonical slug 有效
AND 至少存在一条 PostTag 指向当前公开帖子
```

`IsFixed=true` 只影响运营固定展示，不自动赋予公开收录资格。

### 4.4 canonical

- 唯一公开路径：`/forum/tag/:canonicalSlug`。
- `sort`、`page` 属于阅读状态，不改变标签主体 canonical；标签页 canonical 始终收口到无查询参数的主体路径。
- 旧名称、大小写或非规范 slug 解析成功后，页面 URL 与 head 都回写 canonical slug。
- 新标签页、复制链接、Open Graph、JSON-LD 和 sitemap 不携带来源状态、排序或页码。

## 五、服务端与接口契约

### 5.1 仓储边界

新增实体专属只读仓储边界，负责数据库侧 join、去重和排序；Service 不直接访问 `_repository.Db`，也不把全量 PostTag 拉入内存后聚合。

职责固定为：

- 按公开帖子判定查询指定标签的公开帖子数量；
- 查询至少存在一篇公开帖子的热门标签；
- 查询与源标签共现的相关标签；
- 分页查询可进入 sitemap 的标签实体；
- 保持返回 `Tag` 实体集合或标量计数，由 Service 映射 VO。

若现有索引已经覆盖 `PostTag.TagId / PostId` 与公开 Post 过滤，不新增 migration；只有 SQLite 与 PostgreSQL 查询计划都证明缺失时，才通过正式 migration 增加最小复合索引。

### 5.2 标签详情

保留：

```http
GET /api/v1/Tag/GetBySlug/{slug}
```

语义收紧：

- 只返回启用且未删除的标签；
- `VoSlug` 始终为 canonical slug；
- 面向公开调用的 `VoPostCount` 使用当前公开帖子数量，不直接回显可能滞后的关系投影；
- 不存在、禁用、软删除或 slug 无法解析时返回 `404`；
- 高频错误补稳定 `Code / MessageKey`，前端不依赖中文文本分支。

### 5.3 热门标签

保留：

```http
GET /api/v1/Tag/GetHotTags?topCount=6
```

语义收紧：

- 只返回至少有一篇当前公开帖子的公开标签；
- 按公开帖子数量降序；
- 同数量按 `SortOrder ASC, Id ASC` 稳定排序；
- `topCount` 限制在 `1~20`，非法值返回结构化 `400`，不静默扩大；
- 不使用用户画像、点击历史或个性化权重。

### 5.4 相关标签

新增：

```http
GET /api/v1/Tag/GetRelated/{slug}?topCount=8
```

返回 `List<TagVo>`，规则如下：

- 先解析源标签 canonical slug；
- 只统计同时包含源标签且当前公开的帖子；
- 候选标签必须启用、未删除、不是源标签；
- 按共同公开帖子数降序；
- 同共现数按候选标签公开帖子数降序，再按 `SortOrder ASC, Id ASC`；
- 去重后最多返回 `topCount` 条；
- 没有相关标签时返回成功空数组；
- 源标签不可用时返回结构化 `404`；
- `topCount` 限制在 `1~20`。

该结果是确定性内容共现，不叫“个性化推荐”，不记录用户行为，也不对用户作推荐解释。

### 5.5 Gateway head snapshot

新增 API：

```http
GET /api/public-head/forum/tag/{slug}
```

新增 Gateway 路径识别：

```text
/forum/tag/:slug
-> /api/public-head/forum/tag/:slug
```

快照规则：

- 只为公开标签生成；
- canonical 使用 canonical slug；
- title 使用 `{Tag.Name} - Radish 论坛`；
- description 优先使用运营维护的 `Tag.Description`，为空时使用不含内部 ID 的稳定 fallback；
- JSON-LD 类型为 `CollectionPage`；
- Open Graph type 为 `website`；
- 不输出管理员、创建者、内部 Tag Id 或不可见帖子信息；
- 继续沿用 API 20 分钟、Gateway 注入 HTML 10 分钟缓存和失败开放策略。

### 5.6 sitemap

新增 `tags` 分片：

```text
/sitemaps/tags-1.xml
```

规则：

- sitemap index 根据可公开标签数量输出 tags 分片；
- 每条 URL 使用 `/forum/tag/:canonicalSlug`；
- 只列出满足公开标签判定的标签；
- `lastmod` 使用标签元数据、当前公开 PostTag 关系与公开帖子内容中最新一次有效变化的 UTC 值，避免新增帖子后标签聚合页已经变化但时间仍停留在标签创建日；
- 分片大小、最大页数、缓存、last-known-good 与失败空集策略复用现有 `PublicSitemapService`；
- 不把排序、分页、旧标签名或搜索 URL 写入 sitemap。

## 六、正式 Web 契约

### 6.1 标签页

`PublicForumTag` 保留现有主结构，并增加“相关主题”区：

- PC 放在标签摘要之后、帖子列表之前或右侧 rail；
- mobile 放在标签摘要之后，以可换行 chip 列表呈现；
- 每个标签必须是 `/forum/tag/:canonicalSlug` 的真实 `<a href>`；
- 普通点击走既有公开论坛导航；辅助点击、新标签和复制链接不被拦截；
- 相关标签加载失败不阻断主帖子列表，显示紧凑局部错误与重试；
- 空数组时不伪造热门标签 fallback，可隐藏相关主题区或展示明确空态。

### 6.2 公开数量

- 标签页展示的帖子数量来自当前公开查询；
- 列表 `DataCount` 与标签上下文公开数量必须一致；
- 帖子在禁用、软删除、恢复或重新发布后，两者最终一致；
- 页面不得显示 `Tag.PostCount` 与真实列表总数相冲突的双真相。

### 6.3 来源返回

- 从 `/discover`、forum 列表、帖子详情或其他标签进入时，沿用当前标签页一次性来源状态；
- 从标签页进入帖子详情后，返回原标签、排序和页码；
- 从标签页进入相关标签后，新标签成为新的当前聚合页，不把旧来源写入 canonical；
- 浏览器 Back / Forward 继续由公开路由 state 处理。

### 6.4 i18n、主题与可访问性

- 相关主题标题、错误、空态和数量词元进入 `forum` 中英文资源；
- 标签名称、说明和帖子内容保持原文；
- `default / guofeng / dark-night / sakura` 只使用语义 token；
- chip、重试和分页具备键盘焦点；
- mobile 标签列表允许换行，不产生横向滚动；
- reduced-motion 下不新增依赖动画才能理解的状态。

## 七、权限、失败与缓存

### 7.1 权限和隐私

- 标签详情、热门标签、相关标签、head snapshot 和 sitemap 都允许匿名读取；
- 只返回公开标签和公开帖子聚合事实；
- 禁用 / 删除原因、后台备注、创建人、内部审核状态不对外输出；
- 本专题没有关注、订阅、通知、奖励、经验或萝卜币副作用。

### 7.2 失败语义

| 场景 | 结果 |
| --- | --- |
| slug 无法解析 | 标签详情 / 相关标签 `404`，页面通用不可用 |
| 标签禁用或删除 | 不进入热门、相关、head 和 sitemap |
| 相关标签查询失败 | 主标签页继续可读，相关区局部错误 |
| head API 失败 | Gateway 沿用现有失败开放，不能把 fallback 当成 SEO 验收通过 |
| sitemap 查询失败 | 返回 last-known-good；没有成功缓存时返回合法空分片 |
| 页面请求切换 | request id / cancelled guard 阻止旧标签结果回写新路由 |

### 7.3 缓存

- 标签详情和相关标签可使用现有统一缓存入口，建议 TTL 不超过 10 分钟；
- head snapshot 20 分钟、Gateway 注入 HTML 10 分钟、sitemap 30 分钟保持现状；
- 缓存键包含 canonical slug、公开 origin 和必要分页参数；
- 不新增 Redis 专用调用，继续依赖 `ICaching`。

## 八、数据与 migration

F4-Q 默认不新增表或字段。

B 批必须复核：

- `Tag.Slug` 唯一性与 canonical 格式；
- `PostTag(PostId, TagId)` 唯一关系；
- `PostTag.TagId / PostId` 查询索引；
- 公开帖子过滤相关索引；
- `Tag.PostCount` 与 PostTag 关系投影可重建，但不把它提升为公开数量真相。

如需要新增索引：

- 使用正式 DbMigrate migration；
- SQLite 和 PostgreSQL 均提供 apply / verify；
- verify 覆盖重复关系、孤立 Tag / Post、无效 slug 和索引存在性；
- 不在服务启动时临时建索引，不依赖 ORM 自动漂移。

## 九、测试与验收

### 9.1 服务端

- 公开帖子判定覆盖 published / disabled / deleted / draft；
- 标签详情公开数量与 `Post.GetList` 总数一致；
- 热门标签排除零公开帖、禁用和删除标签；
- 热门排序稳定；
- 相关标签按公开共现排序，排除源标签和不可用标签；
- 相关标签空集、非法 topCount、源标签不存在；
- forum 普通 / 问答 / 投票 / 抽奖列表与详情统一排除 `IsEnabled=false`；
- head snapshot 不为不可用标签生成；
- tags sitemap 只包含可公开标签与 canonical slug；
- 缓存失败保持既有安全回退。

### 9.2 前端

- 相关标签输出真实 `href`；
- 普通点击与辅助点击边界；
- 标签 canonical 不包含 `sort / page`；
- 旧 slug 规范化后保留当前页面状态；
- 相关区 loading / empty / error / retry 不影响帖子列表；
- disabled 帖子不出现在标签列表；
- `zh / en`、四主题、键盘焦点、mobile wrap 和 reduced-motion 静态契约。

### 9.3 D 批运行态

- Gateway `GET /forum/tag/:slug` 与 `HEAD` 首包 head；
- `/sitemap.xml` 与 tags 分片；
- 匿名、普通登录用户和 Console 标签管理员代表身份；
- PC `1920 × 1080` 与 mobile `390 × 844 / DPR 3`；
- `zh / en` 与代表主题；
- 热门标签进入、相关标签切换、帖子详情与精确返回；
- 标签禁用、删除、恢复后的页面、head 和 sitemap 行为；
- 临时标签、帖子、PostTag、浏览历史及计数副作用清理；
- 六库完整性与 strict migration verify。

## 十、A-D 批次

### F4-Q-A：候选审计与权威设计

- **完成状态（2026-07-29）**：已完成。
- 裁决标签公开发现、可见性与 SEO 为唯一候选。
- 固定公开判定、相关标签、head、sitemap、页面和停止线。
- 不修改代码、数据库或运行状态。

### F4-Q-B：公开读取、head 与 sitemap

- **完成状态（2026-07-29）**：已完成。
- 统一 forum 公开列表 / 详情的 `IsPublished && IsEnabled && !IsDeleted` 判定；
- 落地数据库侧标签发现查询与公开数量；
- 收紧热门标签，新增相关标签 API；
- 增加标签 Gateway head snapshot；
- 增加 tags sitemap 分片；
- 补服务、Controller、Gateway、SQLite / PostgreSQL 条件测试与必要 migration verify。
- 实现说明：
  - 新增 `ITagDiscoveryRepository / TagDiscoveryRepository`，公开计数与共现均在数据库侧完成，并按公开帖子 ID 去重；
  - `Tag.PostCount` 继续作为关系投影，公开详情、热门、相关和 sitemap 使用实时公开聚合；
  - `topCount` 固定 `1~20`，非法请求和来源标签不可用均返回稳定 `Code / MessageKey`；
  - 未新增表、字段或索引，因而本批无 migration；SQLite 已实跑聚合翻译，PostgreSQL 条件用例在配置测试连接时执行。

### F4-Q-C：正式 Web

- 标签页接入相关主题与公开数量；
- 收口 runtime canonical；
- 补双语、语义主题、键盘、mobile 和 reduced-motion 契约；
- 更新标签、SEO、发现页和论坛长期文档。

### F4-Q-D：成组验收

- Gateway PC / mobile 代表矩阵；
- 热门 -> 标签 -> 相关标签 -> 帖子 -> 返回链路；
- 禁用 / 删除 / 恢复与缓存失效；
- 首包 head、JSON-LD、sitemap 与 canonical；
- 清理、六库完整性与 strict migration verify；
- 形成记录后关闭 F4-Q。

## 十一、停止线

F4-Q 明确不做：

- 标签关注、订阅、收藏夹或通知；
- 个性化标签推荐、用户画像、点击追踪或推荐解释；
- 新建标签首页 `/forum/tags`；
- 标签合并、别名图谱、层级标签或同义词治理；
- 自动生成 SEO 文案、AI 摘要或批量改写运营说明；
- 推荐系统、跨类型 feed 读模型或 `/discover` 大改版；
- 公开个人页 sitemap、SSR / SSG 或全站 head 扩面；
- 评论、点赞、投票、抽奖或发布逻辑扩展；
- WebOS、Flutter 或 Tauri 新功能。

任何超出上述边界的能力必须另立专题，不作为 F4-Q 的“顺手增强”。
