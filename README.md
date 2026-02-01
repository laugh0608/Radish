# Radish

<p align="center">
  <img src="./radish.docs/docs/images/RadishAcg-256.png" alt="萝卜娘" width="256">
</p>

Radish 是一个自研分层架构的现代化内容社区：后端基于 ASP.NET Core 10 + SQLSugar + PostgreSQL，前端使用 React 19（Vite + TypeScript），采用桌面化 UI 设计理念。

## 技术栈

- **后端**：ASP.NET Core 10、SQLSugar、FluentValidation、Serilog
- **数据库**：PostgreSQL 16（本地开发可用 SQLite）
- **前端**：React 19、Vite (Rolldown)、TypeScript
- **测试**：xUnit + Shouldly（后端）、Vitest + Testing Library（前端）
- **容器化**：Docker / Docker Compose

## 快速开始

### 前置要求

- .NET 10 SDK
- Node.js 24+
- PostgreSQL 16+ （或使用默认的 SQLite）

### 启动项目

```bash
# 方式 1：使用一键脚本（推荐）
pwsh ./start.ps1    # Windows/PowerShell（单服务 1-8；组合：Gateway+Auth+API，或一键启动全部）
./start.sh          # Linux/macOS（单服务 1-8；组合 9-15 覆盖 Gateway+API/Frontend/Docs/Console/Auth 及 ALL）

# 方式 2：手动启动后端
dotnet restore
dotnet run --project Radish.Api/Radish.Api.csproj

# 方式 3：手动启动前端
npm install --prefix radish.client
npm run dev --prefix radish.client
```

启动后常见入口：
- **Gateway 门户**：https://localhost:5000  （统一入口，下挂各子系统；http://localhost:5001 会自动重定向到此地址）
- **前端桌面（WebOS 默认）**：https://localhost:5000/        （YARP 代理到前端 dev http://localhost:3000，支持 `/?showcase` 组件库与 `/?demo` 旧 OIDC Demo）
- **在线文档**：https://localhost:5000/docs             （VitePress 文档站，来自 radish.docs）
- **API 文档（Scalar）**：https://localhost:5000/scalar  （Gateway 转发到 Radish.Api 的 `/scalar`，`/api/docs` 作为旧路径已重定向到 `/scalar`）
- **控制台（radish.console）**：https://localhost:5000/console

如需直连后端服务（仅用于本机调试，下游服务不直接对外暴露）：
- **后端 API（内部调试）**：http://localhost:5100
- **API 文档（直连）**：http://localhost:5100/scalar
- **前端 dev**：http://localhost:3000
- **Docs dev**：http://localhost:4000/docs
- **Console dev**：http://localhost:3100

### 常用命令

```bash
# 后端开发
dotnet watch --project Radish.Api           # 热重载
dotnet test Radish.Api.Tests                # 运行测试
dotnet build Radish.slnx -c Debug           # 构建解决方案

# 前端开发（必须在项目根目录运行）
npm run dev --workspace=radish.client       # 前端开发服务器
npm run dev --workspace=radish.console      # 控制台开发服务器
npm run build --prefix radish.client        # 生产构建
npm run lint --prefix radish.client         # 代码检查

# 或使用快捷脚本
npm run dev:frontend                        # 启动 radish.client
npm run dev:console                         # 启动 radish.console
npm run dev:docs                            # 启动文档站

# UI 组件库开发
npm run type-check --workspace=@radish/ui   # 类型检查
npm run lint --workspace=@radish/ui         # 代码检查

# Windows 用户注意：
# 如果需要在子项目目录中直接运行 npm 命令，请先以管理员身份运行：
# pwsh ./setup-workspace-links.ps1
```

## 项目结构

```
Radish/
├── radish.docs/                 # 📚 VitePress 文档站（开发规范、架构设计、部署指南等）
├── radish.client/               # ⚛️ React 前端应用（WebOS 桌面环境）
├── radish.console/              # 🎛️ 管理控制台前端
├── radish.ui/                   # 🎨 UI 组件库（共享组件、Hooks、工具函数）
├── Radish.Gateway/              # 🚪 API 网关（YARP 反向代理）
├── Radish.Api/                  # 🌐 ASP.NET Core API 宿主
├── Radish.Auth/                 # 🔐 OIDC 认证服务器（OpenIddict）
├── Radish.Service/              # 💼 应用服务层（业务逻辑编排）
├── Radish.Repository/           # 💾 数据访问层（SQLSugar 实现）
├── Radish.Core/                 # 🏛️ 领域模型层
├── Radish.Model/                # 📦 实体、DTO、视图模型
├── Radish.Common/               # 🔧 通用工具（日志、配置、缓存）
├── Radish.Extension/            # 🔌 扩展功能（Swagger、AutoMapper、AOP）
├── Radish.Infrastructure/       # 🏗️ 基础设施（SqlSugar 扩展、多租户）
├── Radish.IService/             # 📋 服务接口契约
├── Radish.IRepository/          # 📋 仓储接口契约
├── Radish.Shared/               # 🌍 后端共享常量、枚举（C#）
├── Radish.Api.Tests/            # 🧪 单元测试
└── Radish.slnx                  # 📁 解决方案文件
```

## 文档导航

完整的开发文档现位于 `radish.docs/docs/` 目录（同时也是在线文档站的源文件）：

### 核心文档
- 📘 [**开发规范**](radish.docs/docs/architecture/specifications.md) - 目录职责、分层依赖、代码约定
- 📗 [**架构设计**](radish.docs/docs/architecture/framework.md) - 技术选型、分层架构、数据持久化
- 📙 [**开发计划**](radish.docs/docs/development-plan.md) - 里程碑与迭代计划
- 📕 [**开发日志**](radish.docs/docs/changelog/) - 按月份/周记录的开发历程

### 专项文档
- 🔐 [**认证与权限**](radish.docs/docs/guide/authentication.md) - OIDC 认证流程与权限体系
- 🎨 [**前端设计**](radish.docs/docs/frontend/design.md) - WebOS 桌面范式与应用集成方式
- 🚪 [**Gateway 服务网关**](radish.docs/docs/guide/gateway.md) - 统一服务入口与路由转发
- 🚀 [**部署指南**](radish.docs/docs/deployment/guide.md) - 容器化、CI/CD、生产部署
- 🧩 [**文件上传设计**](radish.docs/docs/features/file-upload-design.md) - 文件上传与图片处理方案
- 🦀 [**Rust 扩展**](radish.docs/docs/guide/rust-extensions.md) - radish-lib 使用指南

### 前端文档
- 🎨 [**UI 组件库**](radish.docs/docs/frontend/ui-library.md) - @radish/ui 入口文档
- 🧱 [**组件说明**](radish.docs/docs/frontend/components.md) - 组件与用法说明
- ⚡ [**快速参考**](radish.docs/docs/frontend/quick-reference.md) - 常用 API 速查表

### 其他资源
- 📖 [**文档首页**](radish.docs/docs/index.md) - 文档地图与推荐阅读路径
- 🤝 [**贡献指南**](AGENTS.md) - 参与项目开发的指引
- 🤖 [**AI 开发助手配置**](CLAUDE.md) - Claude Code 工作指南

## 关键特性

### 后端架构
- ✅ **分层架构**：清晰的职责分离（API → Service → Repository → Database）
- ✅ **多租户支持**：字段级、表级、库级三种隔离模式
- ✅ **认证授权**：JWT + OIDC（OpenIddict）+ 基于角色的 API 权限控制
- ✅ **日志系统**：Serilog 结构化日志 + SQL 审计日志
- ✅ **缓存策略**：Redis / 内存缓存自动切换
- ✅ **AOP 拦截**：服务层自动日志、事务、异常处理
- ✅ **API 网关**：YARP 反向代理，统一入口和路由

### 前端架构
- ✅ **桌面化 UI**：React 19 + macOS 风格交互体验（WebOS）
- ✅ **UI 组件库**：@radish/ui 共享组件库（4 个组件 + 4 个 Hooks + 12 个工具函数）
- ✅ **npm Workspaces**：monorepo 管理，组件热更新
- ✅ **TypeScript**：完整的类型定义和类型安全
- ✅ **Vite (Rolldown)**：极速构建和热模块替换

### 其他特性
- ✅ **Rust 扩展**：预留高性能原生模块支持
- ✅ **文档站点**：VitePress 构建的完整文档系统

## 配置说明

### 数据库配置

默认使用 SQLite（`Radish.db` 和 `Radish.Log.db`），首次运行自动创建。

切换到 PostgreSQL：编辑 `Radish.Api/appsettings.Development.json`：

```json
{
  "Databases": [
    {
      "ConnId": "Main",
      "DbType": 4,
      "ConnectionString": "Host=localhost;Port=5432;Database=radish;Username=postgres;Password=yourpassword"
    }
  ]
}
```

### 环境变量

关键配置可通过环境变量覆盖：

```bash
# 数据库连接
export ConnectionStrings__Default="Host=localhost;Port=5432;..."

# 雪花 ID（多实例部署时必须不同）
export Snowflake__WorkId=1
export Snowflake__DataCenterId=0

# Redis
export Redis__Enable=true
export Redis__ConnectionString="localhost:6379"
```

更多配置细节参见 [开发规范](radish.docs/docs/architecture/specifications.md)。

## 开发规范要点

- **先写接口，再写实现**：遵循 IService/IRepository 契约模式
- **实体不出仓储层**：Service 层必须将实体映射为 DTO/ViewModel
- **Controller 不直接访问 Repository**：所有数据访问通过 Service 层
- **配置统一读取**：使用 `AppSettings.RadishApp()` 或 `IOptions<T>`
- **日志使用 Serilog 静态方法**：避免注入 `ILogger<T>`（除非框架要求）

完整规范详见 [architecture/specifications.md](radish.docs/docs/architecture/specifications.md)。

## 发版流程

Radish 采用日历版本号格式：`vYY.M.RELEASE`（如 `v26.1.1` = 2026年1月第1版）

### 版本号配置文件

| 组件 | 配置文件 | 字段 |
|------|----------|------|
| 后端 (.NET) | `Directory.Build.props` | `<Version>` |
| 前端根项目 | `package.json` | `version` |
| radish.client | `radish.client/package.json` | `version` |
| radish.console | `radish.console/package.json` | `version` |
| @radish/ui | `radish.ui/package.json` | `version` |
| radish.docs | `radish.docs/package.json` | `version` |
| Rust 扩展 | `radish.lib/Cargo.toml` | `version` |

### 发版步骤

```bash
# 1. 确保代码已合并到 master 分支
git checkout master
git pull origin master

# 2. 更新所有版本号（以 v26.2.1 为例）
# 后端：编辑 Directory.Build.props
#   <Version>26.2.1</Version>
#   <AssemblyVersion>26.2.1</AssemblyVersion>
#   <FileVersion>26.2.1</FileVersion>

# 前端：更新所有 package.json 的 version 字段为 "26.2.1"
# Rust：更新 radish.lib/Cargo.toml 的 version 为 "26.2.1"

# 3. 验证构建
dotnet build Radish.slnx -c Release
npm run type-check

# 4. 提交版本号变更
git add -A
git commit -m "chore: bump version to v26.2.1"

# 5. 创建 Git 标签
git tag -a v26.2.1-release -m "Release v26.2.1: 简要描述"
git push origin master
git push origin v26.2.1-release

# 6. 在 GitHub 创建 Release（包含 Release Notes）
```

### 版本标识

| 后缀 | 说明 | 示例 |
|------|------|------|
| `-dev` | 开发版本 | `v26.1.1-dev` |
| `-beta` | 公测版本 | `v26.2.1-beta` |
| `-rc` | 发布候选 | `v26.3.1-rc` |
| `-release` | 正式发布 | `v26.3.1-release` |

热更新格式：`vYY.M.RELEASE.DDXX`（如 `v26.2.1.1203` = 12日第3次更新）

详细规范参见 [版本号规范](radish.docs/docs/architecture/specifications.md#项目版本号规范)。

## 贡献

欢迎提交 Issue 和 Pull Request！

请确保：
1. 代码遵循项目 [开发规范](radish.docs/docs/architecture/specifications.md)
2. 单元测试通过（`dotnet test`）
3. 提交前运行 `npm run lint --prefix radish.client`
4. 在 [开发日志](radish.docs/docs/changelog/) 中记录重大变更

## 许可

[待定]

## 相关链接

- [在线文档](radish.docs/docs/README.md) - 完整文档目录
- [API 文档](https://localhost:5000/scalar) - Scalar 交互式文档（推荐通过 Gateway 访问，内部由 Radish.Api 提供 `/scalar`，旧 `/api/docs` 路径仅做重定向兼容）
- [开发计划](radish.docs/docs/development-plan.md) - 迭代规划与里程碑
