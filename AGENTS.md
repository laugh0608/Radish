# Repository Guidelines

## 项目结构与模块组织
Radish.slnx 汇总所有 .NET 10 项目，遵循分层架构：
- `Radish.Api/`：ASP.NET Core Web API 主机（Program.cs、Controllers、Dockerfile）。
- `Radish.Core`、`Radish.Service`、`Radish.Repository`：封装领域逻辑、应用服务与数据访问；`Radish.IService`、`Radish.IRepository` 提供接口契约；`Radish.Common`、`Radish.Shared`、`Radish.Extension` 复用通用模型与扩展。
- `Radish.Model/`：集中实体与 DTO；保持与数据库/前端同步。
- `radish.client/`：React + TypeScript + Vite 前端；`src/` 为页面与组件，`public/` 存放静态资源。
- `docs/` 收录开发计划、框架选型与日志；更新流程或规范时同步此目录。

## 构建、测试与开发命令
- `dotnet restore && dotnet build Radish.slnx -c Debug`：还原并在 Debug 配置下构建所有后端项目。
- `dotnet run --project Radish.Api/Radish.Api.csproj`：启动本地 API；需要 PostgreSQL 与 appsettings 中的连接信息。
- `dotnet watch --project Radish.Api/Radish.Api.csproj`：热重载 API，便于调试。
- `npm install --prefix radish.client`：安装前端依赖（Node 24+）。
- `npm run dev --prefix radish.client`：本地启动 Vite 开发服务器；生产版本使用 `npm run build --prefix radish.client` 输出至 `dist/`。

## 编码风格与命名约定
- C# 使用 4 空格缩进、文件范围命名空间，启用 nullable；公共类型/成员采用 PascalCase，本地变量、字段使用 camelCase，接口以 `I` 前缀。
- DTO 与数据库实体分离，保持 `Radish.Model` 中的记录不可变。
- React 组件、hooks 均为 TypeScript：组件文件 PascalCase.tsx，hooks 以 `use` 前缀。运行 `npm run lint --prefix radish.client` 保持 ESLint 通过。

## 测试指南
- 当前尚未创建独立测试项目；新增功能前优先补充单元测试，可使用 `dotnet new xunit -o Radish.Tests` 并在解决方案中引用相应层。命名遵循 `MethodUnderTest_State_Result`，文件置于 `Radish.Tests/*Tests.cs`。
- 前端建议采用 Vitest + React Testing Library，测试文件命名为 `*.test.tsx`，并在 package.json 中添加 `test` 脚本后运行 `npm run test --prefix radish.client`。提交前至少确保关键服务与 hooks 有覆盖。

## 提交与 Pull Request 指南
- Git 历史遵循 Conventional Commits（如 `feat: 完成项目分层创建`、`fix:`、`chore:`）；保持小步提交，并在需要时附中文说明。
- 提交信息需清晰描述核心变动，避免使用 `save local changes` 等笼统内容；若改动较大，可拆分为多个语义明确的提交。
- PR 描述包含变更摘要、测试结果、关联 Issue；前端 UI 变更附截图/GIF，后端接口调整附示例请求（可复用 `Radish.Api.http`）。更新文档或脚本时在同一 PR 中同步。

## 配置与安全提示
- `global.json` 固定 .NET SDK 10.0.0；若本地多版本，请使用 `dotnet --list-sdks` 确认。环境敏感值放入用户密钥或 `appsettings.{Environment}.json` 中的占位符，并通过环境变量或 Secret Manager 注入。
- Docker 化部署需进一步补充 compose/镜像说明；提交前不要将真实连接串、证书或 .user 文件加入版本库。
