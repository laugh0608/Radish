# F4-H-D 电子宠物公开名片成组验收记录

> 日期：2026-07-21（Asia/Shanghai）
>
> 结论：通过，F4-H A-D 批全部完成并关闭。

## 一、验收范围

- Gateway 正式路径下的普通主人、第二个普通访客账号与独立匿名会话。
- `/pet` 领取、公开开关保存，以及 `/u/:id` 后续权威读取与跨标签刷新。
- `zh / en`、PC `1920x1080`、mobile `390x844`、默认 / 国风 / 暗夜 / 樱花代表矩阵。
- 宠物软删除、宠物跨租户、用户禁用、用户软删除与无宠物用户空投影。
- 公开 API 字段白名单、页面只读边界、public head、JSON-LD 与 sitemap。

验收使用仓库种子普通用户作为主人，既有普通测试用户作为访客；没有创建临时账号。访客密码散列仅在受控窗口内替换为已知测试散列，原值预先备份并在结束后逐值恢复。本文不记录任何凭据。

## 二、运行态结论

1. 主人在 `/pet` 领取宠物并开启公开后，同一账号第二标签页、第二个普通账号和全新匿名会话的 `/u/:id` 均看到同一张只读名片。
2. 名片位于身份摘要与公开帖子 / 评论之间，只展示名称、物种、形态、成长阶段和心情；页面没有照顾、编辑、管理或 `/pet` 跳转入口。
3. 主人关闭公开后，已打开标签页刷新、访客账号刷新和匿名后续请求均立即隐藏整块名片，不显示未领取、私密或其他可探测原因。
4. 宠物软删除或租户与用户不匹配时，公开用户主页继续返回 `200`，`VoPet` 为空；用户禁用或软删除时沿用公开主页不存在语义。
5. 无宠物的普通用户保持正常公开主页，不出现宠物空态、错误或存在性提示。
6. `zh / en`、PC / mobile 和四主题代表组合均保持可读布局；国风、暗夜和樱花截图经人工复核，默认主题通过相同 DOM 与响应式矩阵检查。
7. 三个浏览器会话结束前的控制台错误数均为 `0`；服务停止后 Gateway、API、Auth、Client 和 Console 端口均已释放。

## 三、公开字段与索引边界

- 匿名 `GetPublicProfile` 响应中的 `VoPet` 只有 `VoPublicId / VoName / VoSpeciesKey / VoShapeKey / VoGrowthStage / VoMood / VoAdornment`。
- 响应未包含内部 LongId、`UserId / TenantId`、饱食度、清洁度、精力、成长值、最后照顾时间、动作资格或流水；当前装扮摘要为 `null`。
- 公开页面 JSON-LD 继续只描述 `ProfilePage / Person`，不包含宠物名称或 PublicId。
- sitemap index 不包含宠物名称、宠物 PublicId、`/pet` 或独立宠物公开路由；F4-H 没有新增 canonical、OpenGraph 或分享入口。

## 四、数据清理与完整性

- 已删除验收宠物及其流水；`PetProfile / PetStatLog` 对主人账号的残留为 `0`。
- 为四主题代表矩阵临时建立的两条主题权益及当前激活指针已删除，相关残留为 `0`。
- 访客账号密码散列已从独立基线恢复，并确认与备份逐值一致；没有新增或删除账号。
- `Radish.db`、`Radish.Log.db`、`Radish.Message.db`、`Radish.Chat.db`、`Radish.OpenIddict.db`、`Radish.Hangfire.db` 的 SQLite integrity check 均为 `ok`。
- `Radish.DbMigrate verify` 通过：Main / Log / Message / Chat ledger 已应用，OpenIddict pending migration 为 `0`。

## 五、代码侧门禁

- F4-H-C 宠物展示、公开名片和结构化数据定向测试：`19/19` 通过。
- `radish.client` 全量测试：`453/453` 通过；production build、type-check 与 changed lint 通过。
- `validate:baseline:quick`：通过；四个前端 workspace type-check、`18 + 24 + 453 + 57` 项测试及固定扫描正常。
- `check:docs`、changed repo hygiene 与 `git diff --check` 在提交前执行。

## 六、关闭与下一顺位

F4-H 完成标准全部满足：只有主人显式公开后才返回名片；关闭、删除、失效或跨租户后立即隐藏且不暴露原因；匿名响应保持字段白名单；正式 Web、双语、响应式与四主题代表矩阵通过；没有扩展独立路由、SEO、通知、经济、任务或多端状态源。

下一顺位进入 `F4-I-A 下一完整功能专题候选复核与权威设计`：只读审计社区核心与支撑域的现有断点，一次只选定一个长期价值和系统边界清楚的完整专题；不重启主动生产证据采集。
