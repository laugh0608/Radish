# 运行时配置边界与系统设置

本页说明 `.env`、`appsettings.*.json`、环境变量和 Console 系统设置的职责边界，避免同一设置在多个来源中重复定义造成冲突。

## 1. 结论

Radish 运行时配置分为三类：

| 来源 | 负责内容 | 是否给运营人员在 Console 修改 | 持久化方式 |
| --- | --- | --- | --- |
| 前端 `.env*` | `VITE_` 开头的前端构建 / 运行入口，例如 API Base URL | 否 | 前端构建或 Vite dev server 环境 |
| `appsettings.Shared.json` / 宿主 `appsettings.json` / `appsettings.Local.json` / 环境变量 | 宿主启动、数据库、Redis、证书、OIDC、Snowflake、Gateway 下游地址等基础设施配置 | 否 | 配置文件、环境变量、Docker Compose `.env` / secrets |
| Console 系统设置 | 已注册的运营参数，例如 favicon、帖子标题 / 正文最小长度、评论内容最小长度 | 仅限 Low / Medium 且受后端确认规则限制 | `DataBases/SystemConfigs/system-configs.json` 覆盖值 + 代码默认值 |

核心原则：

- 不让 Console 系统设置覆盖 `appsettings` 或环境变量。
- 不把部署密钥、数据库连接、证书、OIDC 密钥、会话安全策略、高危资产设置或宠物经济数值放进 Console。
- 业务运营参数若进入系统设置中心，默认值必须先注册到代码级设置定义，再由 Console 写覆盖值。
- 同一个业务参数只能有一个读取入口；迁入系统设置后，业务代码应通过 `ISystemSettingProvider` 读取，不再直接读 `SystemConfigRecord` 或 `appsettings`。

## 2. 读取顺序

### 2.1 宿主配置

API / Auth / Gateway 启动时按固定顺序加载：

```text
appsettings.Shared.json
  -> appsettings.json
  -> appsettings.Local.json
  -> 环境变量
```

后加载来源覆盖前加载来源。该链路只服务宿主启动和基础设施配置，不会读取 Console 系统设置。

### 2.2 系统设置

系统设置读取顺序固定为：

```text
SystemConfigDefaults 代码级默认值
  -> DataBases/SystemConfigs/system-configs.json 覆盖值
  -> 类型转换与校验
  -> 业务服务消费
```

当前业务消费点包括：

- `Content.PostTitle.MinLength`
- `Content.PostBody.MinLength`
- `Comment.Body.MinLength`

覆盖值非法或越界时，应暴露为配置错误，不静默回退默认值。

## 3. Docker Compose 停启后的冲突处理

Docker Compose 停止和启动后不应产生配置冲突，前提是遵守以下规则：

1. `.env` / compose `environment` 只注入宿主配置和部署差异，例如连接串、公开域名、证书路径、密钥。
2. `DataBases/` 必须使用持久化 volume 或宿主目录挂载；否则 `DataBases/SystemConfigs/system-configs.json` 会随容器销毁而丢失，系统设置会回到代码默认值。
3. Compose 停启不会把 Console 系统设置写回 `.env` 或 `appsettings`，也不会让 `.env` 覆盖系统设置覆盖值。
4. 如果删除 volume、重建数据目录或迁移到新环境，系统设置覆盖值需要通过备份恢复或重新在 Console 配置。
5. 不要在服务运行中手工编辑 `system-configs.json`；需要修改运营设置时走 Console，让后端完成类型校验、确认参数和审计记录。

## 4. 迁移规则

当一个参数准备从 `appsettings` 迁入 Console 系统设置时，必须同时满足：

- 参数不是部署密钥、基础设施配置或启动前必须读取的配置。
- 已在 `SystemConfigDefaults` 注册默认值、类型、风险等级、生效方式和校验规则。
- 后端已通过 `ISystemSettingProvider` 消费强类型值。
- Console 已展示默认值、当前值、覆盖状态、风险等级、生效方式、恢复默认和历史记录。
- Medium 设置必须填写修改原因并确认风险等级 / 设置键；High / Critical 暂不开放编辑。

不满足这些条件时，该参数继续留在 `appsettings` / 环境变量体系。

## 5. 当前已注册设置

| 设置键 | 默认值 | 风险 | 当前用途 |
| --- | ---: | --- | --- |
| `Site.Branding.FaviconUrl` | `/uploads/DefaultIco/bailuobo.ico` | Low | 公开站点 favicon |
| `Content.PostTitle.MinLength` | `3` | Medium | 发帖 / 编辑帖子标题最小长度 |
| `Content.PostBody.MinLength` | `10` | Medium | 发帖 / 编辑帖子正文最小长度 |
| `Comment.Body.MinLength` | `1` | Low | 评论发布 / 编辑内容最小长度 |

账号身份长度、安全会话、奖励数值、审核阈值和资产相关设置仍需逐项评审，不直接开放编辑。
