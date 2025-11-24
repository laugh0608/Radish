# Radish

<p align="center">
  <img src="./docs/images/RadishAcg-256.png" alt="萝卜娘" width="256">
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
pwsh ./local-start.ps1    # Windows/PowerShell
./local-start.sh          # Linux/macOS

# 方式 2：手动启动后端
dotnet restore
dotnet run --project Radish.Api/Radish.Api.csproj

# 方式 3：手动启动前端
npm install --prefix radish.client
npm run dev --prefix radish.client
```

启动后：
- **后端 API**：https://localhost:7110 或 http://localhost:5165
- **API 文档**：https://localhost:7110/api/docs (Scalar UI)
- **前端页面**：https://localhost:58794

### 常用命令

```bash
# 后端开发
dotnet watch --project Radish.Api           # 热重载
dotnet test Radish.Api.Tests                # 运行测试
dotnet build Radish.slnx -c Debug           # 构建解决方案

# 前端开发
npm run dev --prefix radish.client          # 开发服务器
npm run build --prefix radish.client        # 生产构建
npm run lint --prefix radish.client         # 代码检查
```

## 项目结构

```
Radish/
├── docs/                        # 📚 完整文档（开发规范、架构设计、部署指南等）
├── radish.client/               # ⚛️ React 前端应用
├── Radish.Api/                  # 🌐 ASP.NET Core API 宿主
├── Radish.Service/              # 💼 应用服务层（业务逻辑编排）
├── Radish.Repository/           # 💾 数据访问层（SQLSugar 实现）
├── Radish.Core/                 # 🏛️ 领域模型层
├── Radish.Model/                # 📦 实体、DTO、视图模型
├── Radish.Common/               # 🔧 通用工具（日志、配置、缓存）
├── Radish.Extension/            # 🔌 扩展功能（Swagger、AutoMapper、AOP）
├── Radish.Infrastructure/       # 🏗️ 基础设施（SqlSugar 扩展、多租户）
├── Radish.IService/             # 📋 服务接口契约
├── Radish.IRepository/          # 📋 仓储接口契约
├── Radish.Shared/               # 🌍 前后端共享常量、枚举
├── Radish.Api.Tests/            # 🧪 单元测试
└── Radish.slnx                  # 📁 解决方案文件
```

## 文档导航

完整的开发文档位于 `docs/` 目录：

### 核心文档
- 📘 [**开发规范**](docs/DevelopmentSpecifications.md) - 目录职责、分层依赖、代码约定
- 📗 [**架构设计**](docs/DevelopmentFramework.md) - 技术选型、分层架构、数据持久化
- 📙 [**开发计划**](docs/DevelopmentPlan.md) - 里程碑与迭代计划
- 📕 [**开发日志**](docs/DevelopmentLog.md) - 阶段性进展与决策记录

### 专项文档
- 🔐 [**认证授权指南**](docs/AuthenticationGuide.md) - JWT 认证、角色权限、API 授权
- 🎨 [**前端设计**](docs/FrontendDesign.md) - 桌面化 UI、React 架构、跨端策略
- 🚪 [**Gateway 规划**](docs/GatewayPlan.md) - API 网关改造方案与实施路线
- 🚀 [**部署指南**](docs/DeploymentGuide.md) - 容器化、CI/CD、生产部署

### 其他资源
- 📖 [**文档索引**](docs/README.md) - 所有文档的完整目录
- 🤝 [**贡献指南**](AGENTS.md) - 参与项目开发的指引
- 🤖 [**AI 开发助手配置**](CLAUDE.md) - Claude Code 工作指南

## 关键特性

- ✅ **分层架构**：清晰的职责分离（API → Service → Repository → Database）
- ✅ **多租户支持**：字段级、表级、库级三种隔离模式
- ✅ **认证授权**：JWT + 基于角色的 API 权限控制
- ✅ **日志系统**：Serilog 结构化日志 + SQL 审计日志
- ✅ **缓存策略**：Redis / 内存缓存自动切换
- ✅ **AOP 拦截**：服务层自动日志、事务、异常处理
- ✅ **桌面化前端**：React 19 + macOS 风格交互体验
- ✅ **Rust 扩展**：预留高性能原生模块支持

## 配置说明

### 数据库配置

默认使用 SQLite（`Radish.db` 和 `RadishLog.db`），首次运行自动创建。

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

更多配置细节参见 [开发规范](docs/DevelopmentSpecifications.md)。

## 开发规范要点

- **先写接口，再写实现**：遵循 IService/IRepository 契约模式
- **实体不出仓储层**：Service 层必须将实体映射为 DTO/ViewModel
- **Controller 不直接访问 Repository**：所有数据访问通过 Service 层
- **配置统一读取**：使用 `AppSettings.RadishApp()` 或 `IOptions<T>`
- **日志使用 Serilog 静态方法**：避免注入 `ILogger<T>`（除非框架要求）

完整规范详见 [DevelopmentSpecifications.md](docs/DevelopmentSpecifications.md)。

## 贡献

欢迎提交 Issue 和 Pull Request！

请确保：
1. 代码遵循项目 [开发规范](docs/DevelopmentSpecifications.md)
2. 单元测试通过（`dotnet test`）
3. 提交前运行 `npm run lint --prefix radish.client`
4. 在 [DevelopmentLog.md](docs/DevelopmentLog.md) 中记录重大变更

## 许可

[待定]

## 相关链接

- [在线文档](docs/README.md) - 完整文档目录
- [API 文档](https://localhost:7110/api/docs) - Scalar 交互式文档（需启动后端）
- [项目计划](docs/DevelopmentPlan.md) - 迭代规划与里程碑
