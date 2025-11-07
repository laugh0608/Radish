# Repository Guidelines

## 项目结构与模块组织
- 后端在 `src/`：`Radish.Application*`、`Radish.Domain*`、`Radish.HttpApi*`、`Radish.HttpApi.Host`、`Radish.MongoDB`、`Radish.DbMigrator`。
- 前端：`angular/`（管理端）与 `react/`（Vite）。
- 领域模块：`modules/`（如 `comtohtri`）遵循 ABP 模块分层与命名。
- 测试在 `test/`，包含 `*.Tests` 与通用基座 `Radish.TestBase`。
- 文档与运行配置：`docs/`、`etc/abp-studio/run-profiles/`。

## 构建、测试与本地运行
- 还原与构建：`dotnet restore`，`dotnet build Radish.sln -c Debug`。
- 后端运行：`cd src/Radish.HttpApi.Host && dotnet run`。
- 迁移工具：`cd src/Radish.DbMigrator && dotnet run`（首次本地需先执行）。
- 测试：`dotnet test` 或针对项目执行 `dotnet test test/Radish.MongoDB.Tests`。
- Angular：`cd angular && yarn install && yarn start`（或 `npm install && npm run start`）。
- React：`cd react && npm install && npm run dev`。

## 代码风格与命名约定
- C#：4 空格缩进，UTF-8，启用可空，`LangVersion=latest`（见 `common.props`）。
- 命名：类型/方法/属性用 PascalCase，局部变量 camelCase；按层后缀：`*.Application`、`*.Domain.Shared`、`*.HttpApi`、`*.MongoDB` 等。
- 格式化：建议 `dotnet format`；前端遵循各目录 ESLint/Prettier 配置。

## 测试指南
- 框架：xUnit + ABP TestBase；断言 Shouldly；Mock 用 NSubstitute。
- 组织：测试项目以 `*.Tests` 结尾；用例命名表达行为（如 `Method_Should_DoX`）。
- 覆盖：为公共接口与关键路径添加单测，避免引入无测试的核心逻辑。

## 提交与合并请求
- 提交：简洁动词开头，推荐 Conventional Commits（如 `feat: …`、`fix: …`）。
- PR：说明动机与影响，关联 Issue（如 `Closes #123`），列出变更点与验证步骤；涉及 UI 请附截图/录屏。
- 通过本地 `build/test` 后再提交；保持最小变更面与一致的目录/命名。

## 安全与配置提示
- 切勿提交机密；使用 `appsettings.secrets.json` 或环境变量覆盖敏感项。
- 开发配置见 `src/Radish.HttpApi.Host/appsettings.Development.json`；证书文件仅用于本地开发。
- `NuGet.Config` 与 `common.props` 统一依赖源与语言版本，请勿私改导致漂移。

