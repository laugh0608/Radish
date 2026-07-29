# F4 2026-07-29 日终提交回顾与文档审阅

> 日期：2026-07-29（Asia/Shanghai）
>
> 范围：按提交日期统计的 6 个当日提交；本次日终文档提交自身不计入回顾范围。

## 今日结论

- 今天完成 F4-P 论坛帖子收藏 B-D：Main `UserPostBookmark` 私有权威关系、显式目标状态、Post 锁序、稳定个人分页、不可用目标、正式 Web、来源返回修正与代表运行态矩阵形成闭环，专题关闭。
- `20260729_017_forum_post_bookmark` 已应用并通过严格校验；收藏与取消不产生通知、Outbox、经验、资产或作者可见副作用。
- 今天完成 F4-Q 论坛标签公开发现 A-D：统一公开帖子判定、数据库侧标签公开计数 / 热门 / 相关聚合、标签首包 head、tags sitemap、正式 Web 相关主题与代表运行态矩阵形成闭环，专题关闭。
- F4-Q-D 按共同根因修正 tags sitemap 分片、首包 / runtime 单一 JSON-LD、英文数量复数、不可用标签 `noindex`、Console 显示已删除和恢复预检契约。
- 两个专题的临时数据、审计、浏览器会话与服务已经清理；六库完整性和 strict migration verify 均通过。
- 日终没有遗留未提交业务代码。明日第一顺位是取得批准后进入 F4-R-A 单专题候选只读审计，不预设候选，不直接编码。

## 今日全部提交

| 提交 | 主题 | 日终结论 |
| --- | --- | --- |
| `98b5ac2c` | `feat(forum): 实现帖子收藏服务端权威关系` | 新增 Bookmark 唯一关系、显式状态事务、稳定分页、不可用移除、`CollectCount` 投影、017 migration 和统一 HTTP 契约。 |
| `689f3e7d` | `feat(forum): 完成帖子收藏正式 Web 路径` | 帖子详情接入收藏状态与登录回流，`/me/content?tab=bookmarks` 接入分页、脱敏占位、移除和双语响应式路径。 |
| `766ace5a` | `fix(navigation): 保留个人中心来源返回上下文` | `PublicRouteDescriptor.me` 携带完整 `MeRoute`，公开详情精确返回收藏 tab 与页码；F4-P-D 验收完成并关闭专题。 |
| `65ca478d` | `docs(planning): 确立论坛标签公开发现专题` | 完成 F4-Q-A 候选审计，固定公开判定、相关主题、head、sitemap、页面和停止线。 |
| `8f319fe5` | `feat(forum): 完成标签公开发现服务端契约` | 落地数据库侧公开聚合、相关标签 API、标签 head snapshot、tags sitemap 和跨层回归。 |
| `38af6880` | `feat(forum): 完成标签公开发现闭环` | 接入正式 Web 相关主题，修正 sitemap、JSON-LD、`noindex` 与软删除恢复契约，完成 F4-Q-D 验收、清理和规划关闭。 |

## 按代码反查文档

### 已保持一致

- [F4-P 权威设计](/features/forum-post-bookmark-personal-library-design)已经记录实体、Repository / Service、Controller、显式状态、锁序、PublicId、稳定分页、017 migration、正式 Web、来源返回修正、D 批验证和停止线。
- [F4-P-D 成组验收记录](/records/f4-p-d-forum-post-bookmark-stage-acceptance-2026-07-29)准确记录匿名、收藏者、作者和第三方读者矩阵，以及同状态重试、多标签并发、不可用占位、无副作用和清理事实。
- [F4-Q 权威设计](/features/forum-tag-public-discovery-seo-design)已经记录公开可见性、实时公开聚合、相关主题、Gateway head、tags sitemap、runtime canonical、不可用态和 Console 软删除恢复契约。
- [F4-Q-D 成组验收记录](/records/f4-q-d-forum-tag-public-discovery-stage-acceptance-2026-07-29)准确记录 PC / mobile、匿名 / 普通用户 / 管理员、双语主题、首包、JSON-LD、sitemap、清理和 PostgreSQL 条件跳过事实。
- 今日代码没有新增页面族或壳层归属；收藏与相关主题都嵌入既有帖子详情、个人内容和标签聚合布局，因此本次不对 `.pen` 加密设计源做无依据改写。

### 本次修正

- 发布后功能完成线和 backlog 从“F4-Q 只完成 A 批”更新为 A-D 已关闭，并登记 F4-R-A 候选审计。
- 公开 SEO 与分享基线补齐标签 Gateway head、单一 JSON-LD、不可用态 `noindex`、tags sitemap 分片和 robots 生命周期。
- 前端设计总览不再把 F4-Q 写成后续边界；公开体验说明记录帖子收藏和标签公开发现已在既有布局中完成，不新增页面族。
- 私域复访与工作流说明补齐 `/me/content?tab=bookmarks`、稳定分页、Unavailable 占位、Bookmark PublicId 移除和完整 `MeRoute` 返回。
- 论坛功能说明和评估清单把“我的收藏 / 帖子收藏历史”更新为已完成，同时保留点赞历史、收藏夹与跨对象收藏后置。
- F4-P A-C 批次文字从历史“等待下一批”改为已按批准推进，避免与专题关闭状态冲突。
- 基础 Service / Repository 指南补齐 `includeDeleted` 仓储级显式查询契约、授权边界和恢复预检用法，避免 F4-Q-D 的通用接口只留在代码与验收记录中。
- [当前进行中](/planning/current)新增 2026-07-30 明日事项，记录索引同步加入本页。

### 已登记的文档治理债

- [论坛功能说明](/features/forum-features)在本次修改前已达 `1954` 行，超过专题深度文档 `1200` 行硬上限；今晚只修正既有清单项，不继续追加实现流水。后续应按“当前功能口径 / 历史实现记录”拆分，并作为 F4-R-A 维护候选审计输入，不自动取代产品功能候选。

## 明天事项（2026-07-30）

1. 新会话先只读 [当前进行中](/planning/current)；只有候选边界不足时再读 [开发路线图](/development-plan) 和直接相关专题，不默认展开历史记录。
2. 盘点仍然明确、可执行且未被 F4-B 至 F4-Q 既有闭环覆盖的功能或维护缺口，形成 `2~4` 个候选及代码事实依据。
3. 按用户价值、权威数据边界、正式 Web 入口、长期维护成本和停止线比较候选；不预设继续深化论坛，不把主动生产证据采集当作前置。
4. 只裁决一个边界完整专题，并先完成对应功能设计 / 说明文档；汇报边界并取得批准后再进入代码。
5. F4-R-A 不启动服务、不执行 Gateway smoke，不解冻 Flutter、WebOS 或 Tauri 新功能。

## 验证与留痕边界

- F4-Q 最终后端全量为 `1101` 通过、`36` 个 PostgreSQL 条件用例跳过、`0` 失败；`radish.client` 为 `482` 项测试、type-check、lint 与 production build 通过。
- F4-P-D 最终 `radish.client` 为 `476` 项测试；收藏事务、migration、来源返回、Gateway 矩阵与清理证据以对应专题和验收记录为准。
- Gateway、API、Auth 冷启动健康检查均返回 `200`；清理后 tags sitemap、热门标签、公开 head、`GET / HEAD` 与单一 JSON-LD 复核通过。
- 当前机器未配置 PostgreSQL 集成测试环境，文档继续保留显式条件跳过，不把 SQLite 结果描述为 PostgreSQL 实跑。
- 本次日终只修改文档，执行文档、文本卫生、链接与 Git 差异检查；不重复运行代码测试，不启动服务。

## 明日启动口径

F4-R-A 是候选与设计审计，不是默认编码批次。下一开发日先汇报候选审计范围并取得明确批准；候选裁决前不修改运行时、接口、数据模型或依赖。
