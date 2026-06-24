# 系统设置治理专题

> 状态：低 / 中风险首轮治理已阶段收束，进入维护观察与后续独立评审池
>
> 最后更新：2026-06-23（Asia/Shanghai）
>
> 关联文档：
>
> - [配置管理](/guide/configuration)
> - [运行时配置边界与系统设置](/guide/runtime-configuration-boundaries)
> - [Console 系统说明](/guide/console-system)
> - [Console 权限治理 V1](/guide/console-permission-governance)
> - [用户身份语义与公开索引](/architecture/user-identity-semantics)
> - [P3-10 Web-first 信息架构与下一批开发任务选择](/planning/p3-10-cross-platform-information-architecture)

## 1. 结论

Radish 需要一个长期的系统设置中心，但它不应只是把 `appsettings.json` 或当前 `SystemConfig` 原样搬进 Console。目标形态应是：

- 默认值由代码级设置定义提供。
- 持久化层只保存管理员覆盖值。
- Console 展示当前值、默认值、是否已修改、风险等级和生效方式。
- 高危设置必须二次确认，并写入审计日志。
- 每个设置都具备类型、分组、校验规则、说明和恢复默认能力。

当前项目已将 `SystemConfig` 首批收敛为代码级设置定义注册表 + JSON 覆盖值存储：Console 默认只展示已注册设置，历史未注册 key-value 记录不作为运营设置暴露。`Site.Branding.FaviconUrl` 作为第一个低风险可编辑示例；第二批已补系统设置专用变更审计、修改原因 / 确认参数基础和 Console 历史查看入口；第三批已新增 `ISystemSettingProvider`，并开放内容 / 评论长度设置；第四批已将数值范围、整数约束和影响范围摘要从设置定义暴露到 Console，并让数字控件按规则约束输入；第五批继续沿内容发布边界补齐标题 / 正文 / 评论最大长度和自动摘要长度设置；第六批将轻回应长度纳入评论互动同族设置；第七批将登录名和展示名长度接入账号身份设置；第八批继续治理轻回应剩余运营参数，开放默认返回条数、最大返回条数、单帖冷却秒数和重复内容窗口秒数；第九批开放神评 / 沙发稳定窗口和替换阈值；`P3-12-B6-3` 已将展示名改名冷却、滚动窗口和窗口内最大次数纳入账号身份设置。`ForumQuickReply.Enable` 仍作为宿主功能开关保留在 `appsettings`，不进入 Console 系统设置。

第九批后，低 / 中风险系统设置首轮治理阶段收束。当前不继续默认开放第十批设置；后续设置扩面只在真实运营缺口、发布候选回归或独立专题评审确认边界后回拉。

`2026-06-24` 身份语义补充：注册页 `DisplayName` 慎重设置提示本身不属于系统设置；展示名改名冷却 / 滚动窗口 / 窗口内最大次数已完成代码级定义、服务端消费和审计记录，进入当前已注册设置。`PublicIndex` 靓号保留列表 / 靓号规则已完成代码级定义、服务端消费和配置错误暴露，进入当前已注册设置；人工指定保留号仍需后续权限动作与审计专题承接。

## 2. 目标与非目标

### 2.1 目标

- 让管理员可以在 Console 中调整账号、内容、通知、会话、安全、品牌、治理和玩法相关的可运营参数。
- 保留项目原始默认值，支持一键恢复默认。
- 对高危设置提供二次确认、原因填写、审计记录和必要的重新认证。
- 统一设置读取入口，避免业务代码散落读取 `SystemConfigRecord` 或硬编码 fallback。
- 支持后续类似 Discourse 的丰富后台设置，但按风险分批开放。

### 2.2 非目标

- 不把所有部署配置搬进数据库。
- 不在系统设置中心保存数据库连接字符串、证书、OIDC 密钥、第三方密钥等敏感部署秘密。
- 不绕过 `appsettings.Shared.json -> appsettings.json -> appsettings.Local.json -> 环境变量` 的宿主配置加载规则。
- 不让普通管理员修改会导致服务无法启动的基础设施配置。
- 不在首批实现完整 Feature Flag 平台或动态运维平台。

## 3. 设置模型

建议拆成两层：

### 3.1 设置定义

设置定义由代码维护，描述某个设置“是什么”。

| 字段 | 说明 |
| --- | --- |
| `Key` | 全局唯一键，例如 `UserIdentity.LoginName.MinLength` |
| `Category` | 分组，例如 `账号身份`、`内容发布`、`安全会话` |
| `Name` | Console 展示名称 |
| `Description` | 用途、影响范围和注意事项 |
| `ImpactSummary` | 影响范围摘要，供 Console 保存前核对 |
| `ValueType` | `string`、`number`、`boolean`、`enum`、`json`、`duration` 等 |
| `DefaultValue` | 代码级默认值 |
| `ValidationRule` | 最小值、最大值、整数约束、正则、枚举项或 JSON schema |
| `RiskLevel` | `Low`、`Medium`、`High`、`Critical` |
| `EffectiveMode` | 立即生效、缓存刷新后生效、重启后生效、任务下次运行生效 |
| `Scope` | 首批建议只做全局设置，后续再评估租户 / 用户级覆盖 |
| `IsSensitive` | 是否需要脱敏展示或禁止 Console 修改 |

设置定义是默认值和校验规则的唯一真相源，不应只存在数据库记录里。

### 3.2 设置覆盖值

覆盖值由 Console 写入，描述管理员实际改了什么。

| 字段 | 说明 |
| --- | --- |
| `Key` | 对应设置定义 |
| `Value` | 覆盖后的值 |
| `IsEnabled` | 是否启用覆盖；关闭后回到默认值 |
| `ModifyBy` | 操作者 |
| `ModifyTime` | 修改时间 |
| `Reason` | 修改原因，高危设置必填 |
| `Version` | 乐观并发控制 |

恢复默认不应删除设置定义，只应移除或禁用覆盖值。

## 4. 推荐分类

首批分类建议如下：

| 分类 | 示例设置 |
| --- | --- |
| `账号身份` | 登录名最小长度、登录名最大长度、展示名最小长度、展示名最大长度、展示名改名冷却 / 窗口次数、公开索引普通用户起始值、公开索引靓号保留列表 / 规则 |
| `内容发布` | 标题最小长度、标题最大长度、正文最小长度、正文最大长度、发帖频率 |
| `评论互动` | 评论最小长度、评论最大长度、typing 节流、神评稳定窗口、神评替换阈值 |
| `安全会话` | 不活跃过期天数、refresh 容忍窗口、登录失败锁定阈值 |
| `通知邮件` | 邮件通知开关、默认订阅策略、通知聚合窗口 |
| `上传附件` | 单文件大小、允许类型、图片处理开关、分片大小 |
| `站点品牌` | favicon、站点名称、公开描述、默认分享图 |
| `治理审核` | 举报队列阈值、自动隐藏阈值、敏感操作原因必填 |
| `经济玩法` | 注册奖励、每日经验上限、萝卜币奖励倍率、活动默认限制 |
| `实验功能` | 灰度能力开关、候选入口显示、实验说明 |

不是所有分类都需要立即实现。首批应优先选择风险低、用户价值明确、验证成本可控的设置。

## 5. 风险等级与确认规则

| 风险等级 | 示例 | Console 要求 |
| --- | --- | --- |
| `Low` | 页面展示文案、普通长度限制 | 普通保存，显示变更摘要 |
| `Medium` | 注册字段长度、内容长度、通知默认开关 | 填写原因、确认风险等级与设置键、写审计日志 |
| `High` | 会话过期、登录失败锁定、奖励数值、审核阈值 | 二次确认、填写原因、写审计日志 |
| `Critical` | 会影响大面积访问、权限、安全或资产的设置 | 重新认证、输入确认短语、必要时只允许超级管理员操作 |

高危设置不能只依赖前端弹窗，后端必须校验权限、风险等级、确认参数和审计要求。

当前实现只允许 `Low` / `Medium` 可编辑设置写入覆盖值；`Medium` 设置必须带修改原因、确认风险等级和确认设置键。`High` / `Critical` 仍由后端拒绝，不因前端提交参数而开放。保存和恢复默认还必须携带当前 `VoVersion` 作为 `ExpectedVersion`，避免多个管理员基于旧覆盖值互相覆盖。

## 6. 审计与历史

系统设置变更必须记录：

- 设置键
- 操作者用户 ID 与公开身份
- 旧值与新值
- 默认值
- 修改原因
- 风险等级
- 请求来源 IP / User-Agent
- 修改时间
- 生效方式

敏感值应脱敏入库或只记录摘要，不保存明文。审计日志不可由普通设置页删除。

当前实现先使用 `DataBases/SystemConfigs/system-config-change-logs.json` 记录系统设置专用历史，字段覆盖设置键、旧值、新值、默认值、原因、风险等级、生效方式、操作者、IP、User-Agent 和时间。该历史只按已注册设置定义查询；普通设置页不提供删除历史能力。

## 7. 读取与生效

后端业务代码不应直接读取 `SystemConfigRecord`。当前已新增统一读取入口：

```text
ISystemSettingProvider
```

读取顺序：

```text
代码级设置定义默认值
        ↓
SystemConfig 覆盖值
        ↓
类型转换与校验
        ↓
业务服务消费
```

实现约束：

- 类型转换失败或覆盖值越界应暴露为配置错误，不应静默回退掩盖问题。
- 低风险即时设置可以缓存并支持刷新。
- 需要重启或任务下次运行生效的设置必须在 Console 明确标识。
- 多实例部署时，后续需要缓存失效广播或短 TTL 策略。
- 服务层消费强类型设置，不把字符串 key 散落到业务代码。

当前消费点包括帖子发布 / 编辑、评论发布 / 编辑、论坛轻回应发布 / 列表、评论神评 / 沙发实时重算、Auth 注册登录名校验、个人资料展示名校验和个人资料展示名改名治理：标题、正文、摘要、评论、轻回应、登录名和展示名长度边界，展示名改名冷却 / 滚动窗口 / 窗口内最大次数，轻回应返回条数、单帖冷却秒数、重复内容窗口秒数，以及神评 / 沙发稳定窗口和替换阈值通过 `ISystemSettingProvider` 读取；读取顺序为代码默认值、JSON 覆盖值、类型转换与范围校验。

### 7.1 开发接入清单

新增系统设置时，开发者应按以下顺序接入：

1. 在 `SystemConfigDefaults` 注册设置定义，包含 `Key`、`Category`、`Name`、`Description`、`ImpactSummary`、`ValueType`、`DefaultValue`、风险等级、生效方式和必要的数值范围 / 整数约束。
2. 判断是否属于运营参数。部署密钥、数据库连接、证书、OIDC 密钥、会话安全策略、高危资产设置和宠物经济数值不得进入 Console 编辑。
3. 业务服务通过 `ISystemSettingProvider` 获取强类型值，不直接读取 JSON 记录，也不在业务代码里手写兜底默认值。
4. Console 只能保存已注册设置的覆盖值；恢复默认时删除覆盖值并回到代码默认值。
5. Low 设置可普通保存；Medium 设置必须填写修改原因并确认风险等级 / 设置键；High / Critical 当前不开放编辑。
6. 设置变更必须写入系统设置专用审计历史。

## 8. Console 交互要求

系统设置页建议采用“分组导航 + 设置列表 + 影响范围摘要”的结构：

- 支持按分类、风险等级、修改状态、关键词搜索。
- 每个设置展示当前值、默认值、是否已覆盖、风险等级和生效方式。
- 支持恢复默认。
- 支持查看修改历史。
- 保存前展示变更摘要。
- 数字设置必须展示范围、整数约束和影响范围摘要；前端控件按规则限制输入，后端仍作为最终校验权威。
- 高危设置要求二次确认和填写原因。
- 敏感设置默认脱敏，不允许复制明文。

设置项控件应按类型选择：开关、数字输入、滑块、枚举下拉、文本输入、JSON 编辑器或只读说明。

## 9. 与现有 `SystemConfig` 的关系

当前 `SystemConfigRecord` 可作为覆盖值存储的基础，但需要逐步补齐治理能力：

- 当前 `Category / Key / Name / Value / Type / IsEnabled` 作为覆盖值记录保留。
- `DefaultValue`、`RiskLevel`、`EffectiveMode`、`ValidationRule` 不建议只存在数据库里，应由代码级定义提供。
- `SystemConfigDefaults` 首批已承担设置定义注册表职责；后续设置数量增加后，可再按领域拆成更清晰的定义文件。
- `Site.Branding.FaviconUrl` 是首个迁移示例：默认值来自代码，Console 修改写覆盖值，恢复默认移除覆盖。

长期不建议让管理员任意创建未知 key。创建自定义 key 可以保留给开发或插件体系，普通运营设置应来自已注册定义。

首批实现的接口语义：

- `GetSystemConfigs` 返回注册定义叠加覆盖值后的设置列表。
- `GetConfigById` 使用设置定义 ID 查询，兼容旧覆盖记录 ID 回查已注册定义。
- 设置列表 / 详情返回 `MinNumberValue`、`MaxNumberValue`、`RequiresInteger` 与 `ImpactSummary`，供 Console 展示校验规则与影响范围。
- 设置列表 / 详情返回 `VoVersion`：没有覆盖值时为 `0`，已有覆盖值时为持久化记录版本号。
- `UpdateConfig` 只允许写入 `Low` / `Medium`、可编辑的注册设置覆盖值；`Medium` 必须接收修改原因、确认风险等级、确认设置键和 `ExpectedVersion`，成功变更后写入系统设置专用审计历史并递增覆盖记录版本。
- `RestoreConfigDefault` 删除覆盖值并回到代码默认值，同样接收原因 / 确认参数和 `ExpectedVersion`，版本不匹配时返回“系统设置已被其他管理员修改，请刷新后重试”。
- `GetConfigChangeLogs` 查询已注册设置的最近变更历史。
- `ISystemSettingProvider` 面向业务服务提供统一读取入口，覆盖值非法时直接暴露配置错误。
- 旧 `CreateConfig` 路由保留兼容但拒绝 Console 新增未知设置。
- 旧 `DeleteConfig` 路由保留兼容，语义收敛为恢复默认。
- 新环境不再自动种子商城、萝卜币、经验、上传等未治理示例配置；已有 JSON 记录保留但不默认暴露。

## 10. 首批候选设置

建议首批只做低到中风险设置：

| 设置 | 默认值建议 | 风险 |
| --- | ---: | --- |
| `UserIdentity.LoginName.MinLength` | `3` | Medium |
| `UserIdentity.LoginName.MaxLength` | `32` | Medium |
| `UserIdentity.DisplayName.MinLength` | `2` | Medium |
| `UserIdentity.DisplayName.MaxLength` | `24` | Medium |
| `UserIdentity.DisplayName.ChangeCooldownDays` | `30` | Medium |
| `UserIdentity.DisplayName.ChangeWindowDays` | `365` | Medium |
| `UserIdentity.DisplayName.ChangeWindowMaxCount` | `3` | Medium |
| `UserIdentity.PublicIndex.ReservedIndexes` | `[1314,5200]` | Medium |
| `UserIdentity.PublicIndex.VanityRules` | `{"repeatedDigits":true,"ascendingSequence":true,"descendingSequence":true,"palindrome":true}` | Medium |
| `Content.PostTitle.MinLength` | `3` | Medium |
| `Content.PostTitle.MaxLength` | `200` | Medium |
| `Content.PostBody.MinLength` | `10` | Medium |
| `Content.PostBody.MaxLength` | `50000` | Medium |
| `Content.PostSummary.MaxLength` | `200` | Low |
| `Comment.Body.MinLength` | `1` | Low |
| `Comment.Body.MaxLength` | `2000` | Medium |
| `Comment.QuickReply.MaxContentLength` | `10` | Low |
| `Comment.QuickReply.DefaultTake` | `30` | Low |
| `Comment.QuickReply.MaxTake` | `60` | Medium |
| `Comment.QuickReply.PerPostCooldownSeconds` | `30` | Medium |
| `Comment.QuickReply.DuplicateWindowSeconds` | `300` | Medium |
| `Comment.Highlight.StabilityWindowMinutes` | `10` | Medium |
| `Comment.Highlight.ReplacementMinLikeDelta` | `2` | Medium |
| `Site.Branding.FaviconUrl` | `/uploads/DefaultIco/bailuobo.ico` | Low |

安全会话、奖励数值、审核阈值和资产相关设置应等审计与二次确认基础完成后再开放。

当前已注册并可在 Console 展示的设置为：`Site.Branding.FaviconUrl`、`UserIdentity.LoginName.MinLength`、`UserIdentity.LoginName.MaxLength`、`UserIdentity.DisplayName.MinLength`、`UserIdentity.DisplayName.MaxLength`、`UserIdentity.DisplayName.ChangeCooldownDays`、`UserIdentity.DisplayName.ChangeWindowDays`、`UserIdentity.DisplayName.ChangeWindowMaxCount`、`UserIdentity.PublicIndex.ReservedIndexes`、`UserIdentity.PublicIndex.VanityRules`、`Content.PostTitle.MinLength`、`Content.PostTitle.MaxLength`、`Content.PostBody.MinLength`、`Content.PostBody.MaxLength`、`Content.PostSummary.MaxLength`、`Comment.Body.MinLength`、`Comment.Body.MaxLength`、`Comment.QuickReply.MaxContentLength`、`Comment.QuickReply.DefaultTake`、`Comment.QuickReply.MaxTake`、`Comment.QuickReply.PerPostCooldownSeconds`、`Comment.QuickReply.DuplicateWindowSeconds`、`Comment.Highlight.StabilityWindowMinutes`、`Comment.Highlight.ReplacementMinLikeDelta`。账号身份本批开放长度边界、展示名改名频率限制和 PublicIndex 自动分配保留号规则，不开放邮箱、登录凭证、高风险账号字段、人工指定 PublicIndex 或 Console 账号变更动作；轻回应本批只开放内容长度、返回条数、冷却和重复内容窗口，不开放 `ForumQuickReply.Enable` 功能开关；神评本批只开放稳定窗口和替换阈值，不开放任务启停、调度、扫描窗口、触发评论数量门槛或奖励数值。

首轮治理后的剩余候选不直接排成新增批次：评论 typing 节流、神评触发评论数量门槛、内容发布频率限制暂不纳入本轮；论坛编辑历史涉及普通用户编辑次数 / 时间窗、历史保留和管理员覆盖，后续如需治理应作为编辑权限与历史保留专题独立评审。

账号身份后续候选也不直接视作第十批：`PublicIndex` 靓号保留显式列表和靓号规则已经作为 B6-4 自动分配治理落地；人工指定保留号、运营活动号、官方号分配和对应权限 / 审计仍需独立评审。

## 11. 实施阶段建议

### Phase A：文档与定义

- 固定本页治理口径。
- 梳理现有 `SystemConfig` 使用点。
- 定义设置注册表、风险等级和默认值模型。
- 当前首批已落地 `SystemConfigDefaults` 注册表、`SystemConfigDefinition`、风险等级、生效方式和默认值字段。

### Phase B：只读设置中心

- Console 展示设置定义、默认值和当前覆盖值。
- 支持搜索、分类和查看生效方式。
- 不开放高危修改。
- 当前首批已按已注册定义展示系统设置，未注册历史记录不作为运营设置展示。

### Phase C：低风险可编辑

- 支持低到中风险设置编辑。
- 支持恢复默认。
- 写入基础审计。
- 后端服务通过统一 provider 消费设置。
- 当前已开放 `Site.Branding.FaviconUrl` 低风险覆盖值编辑与恢复默认，并开放帖子标题 / 正文、评论内容最小 / 最大长度、帖子自动摘要长度、论坛轻回应内容最大长度、轻回应返回条数、轻回应冷却 / 去重窗口、登录名长度、展示名长度、展示名改名冷却 / 滚动窗口 / 窗口内最大次数、PublicIndex 显式保留号 / 靓号规则、神评稳定窗口和神评替换阈值设置；第二批已补修改原因、确认参数基础、审计历史写入和 Console 历史查看入口，第三批已补统一 provider 与首批业务消费点，第四批已补校验规则元数据和数字编辑控件约束，第五批将内容发布上限与实体 / DTO 硬边界对齐，第六批继续将轻回应长度纳入评论互动同族设置，第七批将账号身份长度设置接入 Auth / API 现有校验点，第八批将轻回应剩余运营参数接入轻回应列表与发布频率治理，第九批将神评稳定窗口和替换阈值接入神评 / 沙发实时重算，`P3-12-B6-3` 将展示名改名频率接入个人资料改名服务，`P3-12-B6-4` 将 PublicIndex 保留号规则接入注册与 Bootstrap 自动分配。低 / 中风险首轮治理当前阶段收束；High / Critical 仍不开放编辑，后续需在逐项确认影响范围、二次确认策略和权限边界后再放开。

### Phase D：高危设置治理

- 增加二次确认、原因填写、重新认证和确认短语。
- 增加完整审计历史。
- 增加缓存刷新或多实例失效策略。

## 12. 当前不做

- 不把部署密钥、数据库连接、证书或 OIDC 私钥放入 Console。
- 不让 Console 直接修改宿主监听端口、数据库类型、迁移策略等基础设施项。
- 不一次性迁移所有 `appsettings` 配置。
- 不让业务代码继续散落读取字符串配置。
- 不开放邮箱、登录凭证、高风险账号字段或 Console 账号变更动作；账号身份设置先限制在已明确消费点的长度边界。
- 不开放人工指定 `PublicIndex`、运营活动号分配或官方号分配；当前只治理自动分配时跳过显式保留号与靓号规则。
- 不开放 `ForumQuickReply.Enable` 功能开关；轻回应是否启用继续由宿主配置控制。
- 不开放神评任务启停、Cron 调度、扫描窗口、缓存策略、神评 / 沙发触发评论数量门槛或奖励数值；这些参数继续逐项评审，不随稳定窗口和替换阈值一起进入 Console。
- 不把评论 typing 节流、内容发布频率限制或论坛编辑历史策略作为 B10 第十批直接开放；论坛编辑历史后续如需治理，应先确认编辑权限、普通用户限制、历史保留和管理员覆盖边界。
