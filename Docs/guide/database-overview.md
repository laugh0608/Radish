# 数据库总览

本文提供 Radish 当前数据库拓扑、逻辑库职责与协作边界，重点回答“现在有哪些库、分别干什么、配置从哪来”。

字段级细节、迁移流程与配置覆盖规则请分别查看：

- [数据库连接管理](/guide/database-connection)
- [数据库结构变更协作口径](/guide/database-schema-change-governance)
- [配置管理](/guide/configuration)

## 1. 当前逻辑库划分

共享配置 [`appsettings.Shared.json`](</D:/Code/Radish/appsettings.Shared.json>) 当前定义了四个逻辑库连接：

| ConnId | 默认本地类型 | 默认连接串名 | 主要职责 |
| --- | --- | --- | --- |
| `Main` | SQLite | `Radish.db` | 核心业务数据 |
| `Log` | SQLite | `Radish.Log.db` | 应用与 SQL 日志落库 |
| `Message` | SQLite | `Radish.Message.db` | 通知 / 消息相关数据隔离 |
| `Chat` | SQLite | `Radish.Chat.db` | 聊天室消息数据隔离 |

除此之外，`Radish.Auth` 还维护独立的 `OpenIddict` 连接，用于 OIDC / 授权服务器相关持久化。

## 2. 宿主与数据库关系

- `Radish.Api`
  - 消费共享 `Databases` 配置
  - 负责绝大多数业务写入与读取
- `Radish.Auth`
  - 复用共享数据库配置中需要的业务 / 日志能力
  - 另外维护独立 `OpenIddict` 数据库
- `Radish.Gateway`
  - 不承载业务数据库
  - 主要负责路由转发、门户与统一入口

## 3. 本地默认形态

当前默认本地开发走 SQLite，特点是：

- 无需先安装 PostgreSQL
- 连接配置直接由共享 `Databases` 节提供
- 切换到 PostgreSQL 时，优先保持 `ConnId` 不变，只覆盖 `DbType` 与连接串

注意：

- `Radish.db`、`Radish.Log.db`、`Radish.Message.db`、`Radish.Chat.db` 是当前默认连接串名
- 实际落盘位置以最终运行时的连接串和宿主解析结果为准
- 如果本地或部署环境通过 `appsettings.Local.json` / 环境变量覆盖了连接串，应以覆盖后的配置为准

## 4. 当前数据库相关协作约定

### 主库标识

- `MainDb` 当前默认为 `Main`
- 新增连接时，不要擅自重定义 `MainDb` 语义

### 多库边界

- `Log` 作为共享日志库，不要把它当普通业务库使用
- `Message` 与 `Chat` 的存在，意味着高频消息类数据已经开始按职责隔离
- 需要新增独立逻辑库前，应先确认是否真有性能、数据生命周期或边界隔离需求

### 配置来源

数据库连接的推荐覆盖顺序仍然是：

1. `appsettings.Shared.json`
2. 宿主 `appsettings.json`
3. 宿主 `appsettings.Local.json`
4. 环境变量

## 5. 建模与查询相关规则

### 软删除

- 业务数据优先使用软删除
- 实体需实现 `IDeleteFilter`
- 删除时应记录 `DeletedAt`、`DeletedBy`

### 多租户

- 字段级：`ITenantEntity`
- 表级：`[MultiTenant(Tables)]`
- 库级：`[MultiTenant(DataBases)]`

### Service / Repository 分层

- Repository 返回实体
- Service 对外返回 DTO / Vo
- Service 层禁止直接绕过仓储去访问 `_repository.Db.Queryable`

## 6. 当前文档缺口说明

仓库现在已经有数据库配置与变更治理文档，但还没有独立的 ERD / 关系图手册。  
因此当你需要进一步确认领域模型时，建议顺序是：

1. 先看本页确认逻辑库边界
2. 再看 [`Radish.Model`](</D:/Code/Radish/Radish.Model>) 中的实体与 ViewModel
3. 最后回到对应领域专题文档核对业务语义

## 相关文档

- [数据库连接管理](/guide/database-connection)
- [数据库结构变更协作口径](/guide/database-schema-change-governance)
- [开发规范](/architecture/specifications)
