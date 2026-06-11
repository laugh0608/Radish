# 系统设置治理专题

> 状态：专题方案已确认，等待排期进入实现
>
> 最后更新：2026-06-11（Asia/Shanghai）
>
> 关联文档：
>
> - [配置管理](/guide/configuration)
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

当前项目已有 `SystemConfig`、Console 系统配置权限和站点 favicon 配置持久化能力，后续应在此基础上升级为“设置定义 + 覆盖值 + 审计”的治理体系。

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
| `ValueType` | `string`、`number`、`boolean`、`enum`、`json`、`duration` 等 |
| `DefaultValue` | 代码级默认值 |
| `ValidationRule` | 最小值、最大值、正则、枚举项或 JSON schema |
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
| `账号身份` | 登录名最小长度、登录名最大长度、展示名最小长度、展示名最大长度、公开索引普通用户起始值 |
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
| `Medium` | 注册字段长度、内容长度、通知默认开关 | 保存前确认影响范围 |
| `High` | 会话过期、登录失败锁定、奖励数值、审核阈值 | 二次确认、填写原因、写审计日志 |
| `Critical` | 会影响大面积访问、权限、安全或资产的设置 | 重新认证、输入确认短语、必要时只允许超级管理员操作 |

高危设置不能只依赖前端弹窗，后端必须校验权限、风险等级、确认参数和审计要求。

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

## 7. 读取与生效

后端业务代码不应直接读取 `SystemConfigRecord`。建议新增统一读取入口，例如：

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

- 类型转换失败应暴露为配置错误，不应静默回退掩盖问题。
- 低风险即时设置可以缓存并支持刷新。
- 需要重启或任务下次运行生效的设置必须在 Console 明确标识。
- 多实例部署时，后续需要缓存失效广播或短 TTL 策略。
- 服务层消费强类型设置，不把字符串 key 散落到业务代码。

## 8. Console 交互要求

系统设置页建议采用“分组导航 + 设置列表 + 影响范围摘要”的结构：

- 支持按分类、风险等级、修改状态、关键词搜索。
- 每个设置展示当前值、默认值、是否已覆盖、风险等级和生效方式。
- 支持恢复默认。
- 支持查看修改历史。
- 保存前展示变更摘要。
- 高危设置要求二次确认和填写原因。
- 敏感设置默认脱敏，不允许复制明文。

设置项控件应按类型选择：开关、数字输入、滑块、枚举下拉、文本输入、JSON 编辑器或只读说明。

## 9. 与现有 `SystemConfig` 的关系

当前 `SystemConfigRecord` 可作为覆盖值存储的基础，但需要逐步补齐治理能力：

- 当前 `Category / Key / Name / Value / Type / IsEnabled` 可保留。
- `DefaultValue`、`RiskLevel`、`EffectiveMode`、`ValidationRule` 不建议只存在数据库里，应由代码级定义提供。
- `SystemConfigDefaults` 可扩展为设置定义注册表，或拆成更清晰的领域设置定义。
- `Site.Branding.FaviconUrl` 可作为首个迁移示例：默认值来自代码，Console 修改写覆盖值，恢复默认移除覆盖。

长期不建议让管理员任意创建未知 key。创建自定义 key 可以保留给开发或插件体系，普通运营设置应来自已注册定义。

## 10. 首批候选设置

建议首批只做低到中风险设置：

| 设置 | 默认值建议 | 风险 |
| --- | ---: | --- |
| `UserIdentity.LoginName.MinLength` | `3` | Medium |
| `UserIdentity.LoginName.MaxLength` | `32` | Medium |
| `UserIdentity.DisplayName.MinLength` | `2` | Medium |
| `UserIdentity.DisplayName.MaxLength` | `24` | Medium |
| `Content.PostTitle.MinLength` | `3` | Medium |
| `Content.PostBody.MinLength` | `10` | Medium |
| `Comment.Body.MinLength` | `1` | Low |
| `Site.Branding.FaviconUrl` | `/uploads/DefaultIco/bailuobo.ico` | Low |

安全会话、奖励数值、审核阈值和资产相关设置应等审计与二次确认基础完成后再开放。

## 11. 实施阶段建议

### Phase A：文档与定义

- 固定本页治理口径。
- 梳理现有 `SystemConfig` 使用点。
- 定义设置注册表、风险等级和默认值模型。

### Phase B：只读设置中心

- Console 展示设置定义、默认值和当前覆盖值。
- 支持搜索、分类和查看生效方式。
- 不开放高危修改。

### Phase C：低风险可编辑

- 支持低到中风险设置编辑。
- 支持恢复默认。
- 写入基础审计。
- 后端服务通过统一 provider 消费设置。

### Phase D：高危设置治理

- 增加二次确认、原因填写、重新认证和确认短语。
- 增加完整审计历史。
- 增加缓存刷新或多实例失效策略。

## 12. 当前不做

- 不把部署密钥、数据库连接、证书或 OIDC 私钥放入 Console。
- 不让 Console 直接修改宿主监听端口、数据库类型、迁移策略等基础设施项。
- 不一次性迁移所有 `appsettings` 配置。
- 不让业务代码继续散落读取字符串配置。
- 不在身份字段契约稳定前开放过多账号身份设置。
