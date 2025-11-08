# Radish

![萝卜](./docs/images/RadishAcg.png)

Radish 是一个自研分层架构的现代化内容社区：后端基于 ASP.NET Core 10 + SQLSugar + PostgreSQL，前端使用 React 19（Vite + TypeScript）。仓库提供 Server、领域层、仓储层、共享模型以及前端工程，所有模块都可在同一个解决方案中协同开发。

## 技术栈一览
- **后端**：ASP.NET Core 10、Minimal API/Controller、Serilog、FluentValidation。
- **数据访问**：SQLSugar（Code First + Migration）、Npgsql 驱动、PgAdmin/OpenTelemetry 可选。
- **数据库**：PostgreSQL 16（本地/容器均可）。
- **前端**：React 19、Vite、TypeScript、React Query、Tailwind/UnoCSS（可按需选用）。
- **测试**：xUnit + Shouldly（后端）、Vitest + Testing Library（前端）。
- **开发体验**：Docker/Docker Compose、Taskfile/PowerShell 脚本、EditorConfig、lint-staged。

## 项目结构
```
Radish.slnx                  # 解决方案入口
docs/                        # 开发规范、架构、计划、部署等文档
radish.client/               # React + Vite 前端工程
Radish.Server/               # ASP.NET Core Host，暴露 REST API
Radish.Common/               # 日志、配置等通用工具
Radish.Core/                 # 领域模型与业务规则
Radish.Service/              # 应用服务，协调 Core 与仓储
Radish.Repository/           # SQLSugar 仓储实现与配置
Radish.Model/                # DTO/实体定义，前后端共享
Radish.Extension/            # 横切关注点（健康检查、Swagger、Auth 中间件）
Radish.Shared/               # 公共常量、枚举、错误码
Radish.IService/             # 业务接口契约
Radish.IRepository/          # 仓储接口契约
```
> 解决方案视图中还包含虚拟文件夹 `others/`，用于挂载根目录的 Dockerfile、GitHub 配置、脚本等资源，但不会在物理目录中创建实际文件夹。

## 开发规范摘要

详细约束见 [docs/DevelopmentSpecifications.md](docs/DevelopmentSpecifications.md)，以下为当前版本的关键点：

### 目录职责
- `docs/`：承载开发规范、设计、计划与部署指引，是贡献者同步信息的唯一入口。
- `radish.client/`：React + TypeScript 前端，按照 feature-first 拆分页面与组件。
- `Radish.Server/`：ASP.NET Core 主机，只负责 API 宿主、DI、配置、健康检查与 Swagger/Scalar。
- `Radish.Common/`：全局可复用的日志、配置与基础工具。
- `Radish.Core/`：领域模型、聚合根、领域服务与事件。
- `Radish.Extension/`：Swagger/Scalar、HealthCheck、JWT 等横切扩展。
- `Radish.IRepository/` & `Radish.IService/`：定义仓储与业务的接口契约。
- `Radish.Model/`：数据库实体、DTO、查询对象与共享枚举。
- `Radish.Repository/`：SQLSugar 持久化实现、上下文配置、迁移与种子帮助。
- `Radish.Service/`：应用服务实现，组织用例流程、权限校验与事务。
- `Radish.Shared/`：前后端共享的常量、错误码与事件名。

### 分层依赖
- 前端（`radish.client`）只依赖 npm/pnpm 生态。
- 后端按层次引用：`Radish.Server → Radish.Service → Radish.Core/IService/IRepository → Radish.Repository → Radish.Model → Radish.Common`，保持核心域对基础设施的最小感知。
- `Radish.Core` 当前以 Domain 角色存在，可独立演进，不直接依赖具体实现。

### 依赖管理
- 前端推荐使用 `yarn`（或 pnpm）并提交 `package-lock.json` / `yarn.lock`，确保团队一致。
- 后端依赖通过 NuGet 管理，锁定 .NET 10 兼容的稳定版本。
- 第三方库需经过安全审查，记录在 PR/变更说明中，并关注社区维护状态。
- 定期升级依赖并在 `DevelopmentLog` 记录影响；大体量依赖需评估按需加载或代码拆分策略。

## 快速开始
1. 安装依赖：`.NET 10 SDK`、`Node.js 24+`、`PostgreSQL 16+`。
2. 复制并填写配置：`cp Radish.Server/appsettings.Development.json appsettings.Local.json` 或通过环境变量设置 `ConnectionStrings__Default`、`Jwt__Key` 等敏感项。
3. 还原与构建：
   ```bash
   dotnet restore
   dotnet build Radish.slnx -c Debug
   ```
4. 初始化数据库（可选 Code First）：运行 `dotnet run --project Radish.Server -- --seed` 或编写 SQLSugar 初始化脚本。
5. 启动服务：`dotnet run --project Radish.Server/Radish.Server.csproj`，默认监听 `http://localhost:5000` 与 `https://localhost:5001`。
6. 前端联调：
   ```bash
   npm install --prefix radish.client
   npm run dev --prefix radish.client
   ```
   通过 `VITE_API_BASE_URL` 指向后端地址。

## 常用命令
- `dotnet watch --project Radish.Server`：后端热重载。
- `dotnet test Radish.Server.Tests`：运行后端单元测试。
- `npm run build --prefix radish.client`：产出前端静态资源。
- `docker compose -f deploy/docker-compose.yml up --build`：一键拉起 PostgreSQL + API（详见 `docs/DeploymentGuide.md`）。

## 文档
- [docs/DevelopmentSpecifications.md](docs/DevelopmentSpecifications.md)：目录、分层与依赖管理规范。
- [docs/DevelopmentFramework.md](docs/DevelopmentFramework.md)：总体架构与技术决策。
- [docs/DevelopmentPlan.md](docs/DevelopmentPlan.md)：按周交付计划。
- [docs/DevelopmentLog.md](docs/DevelopmentLog.md)：阶段日志与关键节点。
- [docs/DeploymentGuide.md](docs/DeploymentGuide.md)：容器化与部署指南。
- [AGENTS.md](AGENTS.md)：贡献者指南。
