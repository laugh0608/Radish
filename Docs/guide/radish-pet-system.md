# Radish 电子宠物系统说明

> 状态：Phase B 登录态 Web 主链路已上线并进入稳定维护；F4-H 公开名片与隐私闭环已完成 A-D 批并关闭
>
> 最后更新：2026-07-21（Asia/Shanghai）
>
> 长期边界与后续阶段见 [Radish 电子宠物开发计划](/features/radish-pet-roadmap)。

## 1. 产品与入口

Radish 电子宠物是服务社区复访与轻陪伴的登录态玩法，不是独立经济系统，也不改变论坛、经验或商城规则。

- `/pet` 是正式 Web 主入口，未登录访问通过 OIDC 登录后回到原路径。
- `/me` 只展示宠物摘要和进入 `/pet` 的入口，不承载完整照顾、命名或流水浏览。
- `/pet` 与照顾流水默认属于本人私域，不进入公开 sitemap。
- `/desktop` 不承接新增宠物主路径；Flutter 和 Tauri 不在当前实现范围内扩张。

## 2. 当前能力

当前每个用户最多拥有一只有效宠物：

- 未领取时，`GetMy` 返回明确空态，不隐式创建宠物。
- `Claim` 创建默认宠物；重复领取返回已有宠物，不创建第二只。
- 用户可修改宠物名称和公开展示开关。
- 公开展示开关已接入 `User/GetPublicProfile` 的服务端字段白名单投影；正式 `/u/:id` 在身份摘要与公开内容之间直接消费同一次响应中的 `VoPet`，为空时整块不渲染。
- 照顾动作固定为 `feed / clean / play / rest`，分别表示喂食、清洁、互动和休息。
- 服务端计算饱食度、清洁度、精力、成长值、成长阶段、心情、每日次数、冷却和状态上下限。
- 每次有效照顾写入 `PetStatLog`，记录动作、变化前后数值、成长变化、来源、幂等键和时间。

前端只提交动作类型与可选幂等键，并展示服务端返回的 `VoCareActions`、宠物状态和流水；不能提交最终状态值，也不能自行重算服务端真值。

## 3. API

`PetController` 全部要求登录态 `Client` 授权，并使用当前用户身份定位宠物：

| 方法与路径 | 用途 |
| --- | --- |
| `GET /api/v1/Pet/GetMy` | 读取当前用户宠物；未领取时返回空态 |
| `POST /api/v1/Pet/Claim` | 领取默认宠物；重复领取返回已有记录 |
| `PUT /api/v1/Pet/UpdateProfile` | 更新名称或公开展示开关 |
| `POST /api/v1/Pet/Care` | 执行四类照顾动作，校验幂等、次数、冷却与状态边界 |
| `GET /api/v1/Pet/GetLogs` | 分页读取当前用户宠物流水；未领取时返回空页 |

`PetProfileVo` 返回 `VoPublicId`、名称、形态、成长阶段、心情、三项状态、成长值、公开开关、最后照顾时间和 `VoCareActions`。对外标识继续使用 `VoPublicId`；内部 LongId 在前端保持字符串。

匿名公开读取不新增 Pet Controller 接口，而是由 `GET /api/v1/User/GetPublicProfile?identifier=...` 聚合可空 `VoPet`。`PetPublicCardVo` 只返回 `VoPublicId / VoName / VoSpeciesKey / VoShapeKey / VoGrowthStage / VoMood / VoAdornment`；隐藏、未领取、软删除、跨租户或公开字段非法时统一为 `null`，不暴露原因。当前没有正式宠物装扮注册来源，`VoAdornment` 保持 `null`。

## 4. 多语言与内容边界

- 成长阶段、心情、动作名、状态洞察和流水动作说明按 `voGrowthStage / voMood / voActionType` 等稳定字段解析 client 词元。
- `voGrowthStageName / voMoodDisplay / voActionName / voMessage` 是兼容展示数据，不参与筛选、动作资格、状态样式或本地化控制流。
- 宠物名称是用户内容，领取、修改、摘要和流水反馈均保留原文，不自动翻译。
- 未填写领取名称时，client 按当前语言提交默认名称；该名称一经创建即按普通用户内容持久化，切换语言不会重写。
- 成长值、状态值、剩余次数和分页数量按当前 locale 格式化；日期时间结合用户时区和当前 locale 展示；英文分钟、小时、次数与流水数量使用 i18next plural 规则。
- `/me` 宠物摘要复用同一 presentation 规则，不读取服务端中文 display 字段重新推导状态。
- `/u/:id` 的公开宠物名片复用物种、形态、成长阶段和心情的稳定词元；不显示私域状态、成长值、照顾时间、动作资格或流水，也不进入 public head、JSON-LD 与 sitemap。
- Gateway 成组验收已覆盖主人 / 访客 / 匿名、显隐切换、软删除、用户失效、跨租户、无宠物、`zh / en`、PC / mobile 与四主题代表矩阵；后续新增宠物能力不得绕过同一公开投影和隐私语义。

## 5. 错误契约

Pet API helper 统一通过 `@radish/http` 解析，并在失败时抛出 `ApiResponseError`。页面保留 `httpStatus / code / messageKey / traceId`，控制流只读取稳定 status 与 `Code`：

| 场景 | HTTP | Code | MessageKey |
| --- | ---: | --- | --- |
| 请求参数无效 | `400` | `Pet.InvalidRequest` | `error.pet.invalid_request` |
| 动作类型不支持 | `400` | `Pet.InvalidAction` | `error.pet.invalid_action` |
| 尚未领取宠物 | `404` | `Pet.NotClaimed` | `error.pet.not_claimed` |
| 当日动作次数已用完 | `400` | `Pet.DailyLimitReached` | `error.pet.daily_limit_reached` |
| 照顾动作仍在冷却 | `400` | `Pet.CareCooldown` | `error.pet.care_cooldown` |

Client 优先使用本地 `MessageKey`，缺少资源时显示服务端按 `Accept-Language` 生成的安全 `MessageInfo`；不得匹配中英文错误消息判断冷却、次数耗尽或未领取状态。

## 6. 数据与业务边界

- `PetProfile` 是当前宠物主档，`PetStatLog` 是状态变化流水；Controller 只编排当前用户与 HTTP 契约，规则留在 `PetService`。
- 照顾幂等键重复时返回既有流水与当前宠物状态，不重复增加成长值或写入第二条流水。
- 每日次数、冷却、状态增减、成长阈值和心情判定由服务端单点维护；前端文案与图表不能复制这些规则。
- 当前不接入萝卜币消耗、商城物品、社区任务奖励、经验反向加成、宠物交易、排行榜或完整 Console 配置。
- 宠物经济数值不得作为普通 `SystemConfig` 在线设置开放；后续经济与治理能力必须按独立专题评审。
- 数据库结构与迁移继续由实体和 `Radish.DbMigrate` 治理，本说明不定义新的表或迁移。

## 7. 验证入口

改动宠物业务域时至少覆盖：

- Service：重复领取、只读不写入、幂等重放、次数、冷却、上下限、成长与流水。
- Controller：`400 / 404`、稳定 Code、MessageKey 和双语资源 parity。
- Client：稳定词元、locale 日期 / 数字、英文复数、ApiResponseError 与 `/me` 摘要消费者。
- 涉及正式 Web 页面成组验收时，再按浏览器 smoke 规范覆盖 PC 与移动视图；普通连续开发不默认启动服务。
