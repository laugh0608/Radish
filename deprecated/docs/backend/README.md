# 后端（.NET / ABP）开发指南

本项目后端基于 ABP Framework（.NET 9），采用分层架构与模块化设计。本文聚焦本地开发、构建、运行与配置，便于与前端（React/Angular）统一管理。

## 项目结构

- 解决方案：`Radish.sln`
- 后端目录：`src/`
  - `Radish.Application*` 应用层
  - `Radish.Domain*` 领域层（含共享资源 `*.Domain.Shared`）
  - `Radish.HttpApi*` 接口层
  - `Radish.HttpApi.Host` Web Host（启动项目）
  - `Radish.MongoDB` 数据访问
  - `Radish.DbMigrator` 迁移/种子数据工具
- 领域模块：`modules/`（如 `comtohtri`），按 ABP 模块分层与命名
- 测试：`test/`（`*.Tests` 与通用基座 `Radish.TestBase`）
- 文档与运行配置：`docs/`、`etc/abp-studio/run-profiles/`

## 环境要求

- .NET 9 SDK
-（如需前端联调）Node.js 18/20

## 还原与构建

```bash
dotnet restore
dotnet build Radish.sln -c Debug
```

## 初始化与运行

首次在本地运行前，请先执行数据库迁移：

```bash
cd src/Radish.DbMigrator
dotnet run
```

启动后端 Host：

```bash
cd src/Radish.HttpApi.Host
dotnet run
```

默认地址示例：`https://localhost:44342`（以本机配置为准）。

## 测试

执行全部测试：

```bash
dotnet test
```

或指定项目：

```bash
dotnet test test/Radish.MongoDB.Tests
```

## 配置与安全

本项目在开发阶段以 `.env` 为唯一可信的配置来源（关键项必须来自 `.env`），优先级高于 `appsettings*.json`。运行时会在常见目录搜索 `.env` 并注入到 `IConfiguration`，空值将被忽略。

放置位置与示例：
- `src/Radish.HttpApi.Host/.env`
  - 必填：
    - `ConnectionStrings__Default=...`
    - `ConnectionStrings__Chrelyonly=...`
- `src/Radish.DbMigrator/.env`
  - 与 Host 保持一致，便于迁移工具使用同一套连接串。
  
跨域来源（CORS）：
- 通过 `App__CorsOrigins` 指定允许的前端来源，多个用逗号分隔（建议同时包含 http/https，例：`http://localhost:5173,https://localhost:5173,http://localhost:4200,https://localhost:4200`）。
- Host 会自动为相同 host:port 补全另一种协议（例如仅配置了 `https://localhost:4200`，运行时也会放行 `http://localhost:4200`）。
- 验证预检：
  - `curl -i -X OPTIONS "https://localhost:44342/api/abp/application-configuration" -H "Origin: http://localhost:4200" -H "Access-Control-Request-Method: GET"`
  - 期望 204 响应，且包含 `Access-Control-Allow-Origin` 头。
- 启动时后端会自动解析并应用到默认 CORS 策略（允许凭据、任意头与方法，支持通配子域）。

弃用说明（关于 appsettings.json 中的 CORS 配置）：
- `App:CorsOrigins` 在 `appsettings.json` 中已被注释并弃用，原因：
  - 开发者本地端口/域名差异较大，提交到仓库会互相覆盖；
  - 使用环境变量更利于按环境切换配置；
  - 与前端本地 HTTPS 方案统一管理。
- 迁移指引：
  - 请将原先写在 `appsettings.json` 中的来源迁移到 `src/Radish.HttpApi.Host/.env` 的 `App__CorsOrigins`。
  - 修改 `.env` 后重启服务，启动日志会打印“CORS allowed origins: ...”用于确认。

你可以从示例文件开始：
- `src/Radish.HttpApi.Host/.env.example` → 复制为 `.env` 并按需修改
- `src/Radish.DbMigrator/.env.example` → 复制为 `.env` 并按需修改

注意事项：
- `.env` 支持 `KEY=VALUE` 形式，键名中的层级可写成 `ConnectionStrings__Default`（双下划线）或 `ConnectionStrings:Default`（冒号）。
- 连接字符串如未从 `.env` 明确提供，后端会在启动时抛出异常并提示。
- `.env` 仅用于本地/自托管环境，请勿提交到仓库。

机密与证书：
- 建议用 `.env` 或环境变量覆盖敏感项，避免 `appsettings` 直接存放机密。
- Host 默认以 HTTPS 运行：首次本地开发建议执行 `dotnet dev-certs https --trust` 信任 Kestrel 证书。
- OpenIddict 开发证书：仓库内的 `openiddict.pfx` 仅用于本地开发；生产环境请自行生成并安全配置。

生成示例（可自定义密码）：

```bash
dotnet dev-certs https -v -ep openiddict.pfx -p 83c828cf-930d-4c16-8cc7-e98e05fc8143
```

## 代码风格与命名

- C#：4 空格缩进，UTF-8，可空启用，`LangVersion=latest`（见 `common.props`）
- 命名：类型/方法/属性 PascalCase，局部变量 camelCase
- 分层后缀：`*.Application`、`*.Domain.Shared`、`*.HttpApi`、`*.MongoDB` 等
- 格式化：建议在根目录执行 `dotnet format`

## 约定与常用命令

- 恢复前端依赖（ABP 工具）：`abp install-libs`
- 运行迁移工具：`cd src/Radish.DbMigrator && dotnet run`
- 启动后端服务：`cd src/Radish.HttpApi.Host && dotnet run`
- 统一运行脚本（可按需在 `etc/abp-studio/run-profiles/` 中配置）

## 与前端联调

- React：见 `docs/frontend/react/README.md`
- Angular（管理端）：见 `docs/frontend/angular/README.md`

两端均基于 ABP 的服务端本地化，若新增文案，请在 `src/Radish.Domain.Shared/Localization/Radish/*.json` 维护，并在 `RadishDomainSharedModule` 中登记语言与默认资源。
