# AGENTS 指南

## 快速认知
- **技术栈**: ASP.NET Core 10 + SQLSugar + PostgreSQL (本地 SQLite) / React 19 + Vite + TypeScript (WebOS 桌面化 UI)
- **前端**: npm workspaces 管理 `radish.client` (WebOS)、`radish.console` (管理后台)、`radish.ui` (共享组件库)
- **协作分支**: `dev` (主开发分支)
- **文档源**: `radish.docs/docs/` 为唯一真相源，架构/需求优先查阅 `framework.md` 和索引 `README.md`
- **语言**: 所有对话响应/输出尽量使用中文（代码/技术标识保持原文，必要时可使用英文）

## 包管理与测试规范

**AI 协作规则**:

**禁止直接执行**:
- ❌ 包安装: `dotnet add package`, `npm install`
- ❌ 项目启动: `dotnet run`, `npm run dev` (用于测试)

**正确做法**:
- ✅ 告知用户需要安装的包: "请安装：`dotnet add package Serilog.AspNetCore --version 8.0.0`"
- ✅ 告知启动方式: "请启动 API 测试：`dotnet run --project Radish.Api`"

**可执行操作**:
- ✅ 代码读写、构建 (`dotnet build`, `npm run build`)、测试 (`dotnet test`, `npm run test`)、代码检查、git 操作

## 仓库结构

### 后端项目
- **宿主**: `Radish.Api` (Web API)、`Radish.Gateway` (门户&网关)、`Radish.Auth` (OIDC 认证)
- **业务分层**: `Service/Repository/Core/Model` (业务逻辑/数据/领域/模型)
- **基础设施**: `Common` (仅外部包依赖)、`Extension` (宿主扩展/Autofac/AutoMapper/Redis/Serilog)、`IService/IRepository` (接口契约)、`Shared` (常量/枚举)
- **测试**: `Radish.Api.Tests` (xUnit)
- **Rust扩展**: `Radish.Core/radish-lib` (统一原生库，构建后拷贝到 `Radish.Api/bin/`)

**层级依赖**: Common → Shared → Model → Infrastructure → IRepository/Repository → IService/Service → Extension → Api/Gateway/Auth

### 前端项目
- `radish.client`: WebOS 桌面，面向用户
- `radish.console`: 管理后台，面向管理员
- `radish.ui`: 共享 UI 组件库
- `radish.docs`: VitePress 文档站
- **依赖**: client 和 console 依赖 `@radish/ui`，通过 npm workspaces 热更新

## 环境与启动

**基础环境**: .NET SDK 10、Node.js 24+、PostgreSQL 16+ (或 SQLite)

**快速启动**:
```bash
pwsh ./start.ps1  # 或 ./start.sh - 交互式菜单
```

**注意**: 项目可以编译，但在 WSL + Windows 混合环境下启动时需谨慎，可能会因为网络/证书/端口/文件系统等因素导致项目一直启动失败或卡住。

**常用命令**:
```bash
# 后端
dotnet build Radish.slnx -c Debug
dotnet run --project Radish.Api       # http://localhost:5100
dotnet run --project Radish.Gateway   # https://localhost:5000
dotnet run --project Radish.Auth      # http://localhost:5200
dotnet test Radish.Api.Tests

# 前端
npm install                           # 根目录
npm run dev --workspace=radish.client # http://localhost:3000
npm run dev --workspace=radish.console # http://localhost:3200
npm run type-check --workspace=@radish/ui
```

**默认端口**:
- API `http://localhost:5100` (内部)
- Auth `http://localhost:5200` (内部)
- Gateway `https://localhost:5000` (外部唯一入口)
- Frontend `http://localhost:3000`
- Console `http://localhost:3200`
- Docs `http://localhost:3100`
- Scalar `/scalar` (Gateway: `https://localhost:5000/scalar`，API 直连: `http://localhost:5100/scalar`)

## 配置与数据库

**配置加载**: `appsettings.json` → `appsettings.{Environment}.json` → `appsettings.Local.json` (Git 忽略) → 环境变量

**关键配置**:
- `Snowflake.WorkId/DatacenterId`: 每环境唯一 (0-30)
- `Databases`: 至少 `ConnId=Main` 和 `ConnId=Log`
- `Redis.Enable`: true (Redis) / false (内存缓存)

**数据库共享**:
- API 和 Auth **共享** `Radish.db` 和 `Radish.Log.db` (业务数据)
- Auth 独享 `Radish.OpenIddict.db` (OIDC 数据，EF Core)
- 所有数据库在 `DataBases/` 目录

**多租户**: 字段级 (`ITenantEntity`) / 表级 (`[MultiTenant(Tables)]`) / 库级 (`[MultiTenant(DataBases)]`)

**配置读取**: `AppSettings.RadishApp("Section", "Key")` 或实现 `IConfigurableOptions`

## 编码规范

### 代码质量
**文件行数**: 500-1000 行/文件，硬限 1000 行，超过需重构（提取 Hooks/组件/Service/工具函数）

### 架构约束

- **先接口后实现**: IService/IRepository → Service/Repository，继承 BaseService/BaseRepository 复用 CRUD
- **责任边界**: Common 仅外部包，Extension 容纳访问 Model/Service 的工具
- **实体不离仓储**: 实体在 `Model/Models`，Controller 返回 DTO/Vo (`Model/ViewModels`)，AutoMapper 映射在 `Extension/AutoMapperExtension`
- **Service 层数据库访问约束**:
  - ❌ 严禁: `_repository.Db.Queryable` 或 `_repository.DbBase.Queryable`
  - ✅ 正确: 通过 Repository 方法
  - **仓储扩展策略** (优先级):
    1. **优先**: 扩展 BaseRepository 泛型方法 (`QueryDistinctAsync`, `QuerySumAsync`) - 跨实体复用
    2. **次选**: 创建实体专属仓储 (`UserRepository : BaseRepository<User>`) - 复杂查询/联表/性能优化
- **Controller 不直接注入 Repository**: 数据访问通过 IService
- **代码风格**: C# 4 空格/文件范围命名空间/nullable；React TypeScript/`const` 组件/避免 `var`/`useState`+`useMemo`+`useEffect`
- **前端架构**: WebOS 桌面 UI (顶栏/Dock/图标/窗口)，`VITE_API_BASE_URL` 管理接口地址
- **Rust 扩展**: `Radish.Core/radish-lib`，构建后拷贝到 `Radish.Api/bin/`

## 开发流程

1. `Radish.Model` 添加实体/DTO/ViewModel，`Radish.Shared` 扩展常量/枚举
2. `IRepository/Repository` 定义实现仓储，SqlSugar 特性标注多租户/分表
3. `IService/Service` 补齐接口实现，AutoMapper/ICaching/IUnitOfWork 组织业务逻辑
4. `Radish.Api` 控制器注入 IService 暴露 API，维护 `Radish.Api.http` 示例
5. 更新 `Extension` (AutoMapper/Autofac) 或 `Infrastructure` (租户/连接配置)
6. 编写测试 (`Radish.Api.Tests`)，前端同步 DTO 更新页面/Hook
7. 重大变更追加文档 (`radish.docs/docs/`)，`changelog/` 记录影响面

## 测试与质量

- **后端**: `dotnet test Radish.Api.Tests`，`--filter` 筛选单测
- **前端**: Vitest + React Testing Library (规划中)，提交前 `npm run lint`
- **热重载**: `dotnet watch --project Radish.Api`，`npm run dev --prefix radish.client`
- **验证**: `.http` 手工验证、SqlSugar Profile、Gateway 健康检查

## 文档与提交

**文档更新**: 流程/配置/脚本/规范变更同步 `radish.docs/docs/` (`architecture/specifications.md`、`guide/`、`deployment/guide.md`)

**更新日志规范**:
- **时区**: Asia/Shanghai (UTC+8)
- **内容**: 简洁明了，只写重点
- **格式**: 月/周组织 (`2026-01/week1.md`)，月度总览 (`2026-01.md`)
- **重点**: 核心功能/技术亮点/重要变更，不含实现细节

**提交规范**: Conventional Commits (`feat:`, `fix:`, `chore:`, `docs:`)，禁止 AI 签名/Co-Authored 标记，单一主题，必要时拆分

**合规**: 禁止提交敏感数据 (连接串/证书/`.user`)，部署参考 `deployment/guide.md`

## 参考资料

**核心文档** (radish.docs/docs/):
- `architecture/framework.md` - 架构设计
- `architecture/specifications.md` - 开发规范详细
- `frontend/design.md` - 前端设计
- `guide/` - 配置/认证/日志/网关
- `development-plan.md` - 里程碑/周计划
- `changelog/` - 更新日志
- `deployment/guide.md` - 部署指南
- `frontend/ui-library.md` - UI 组件库

**详细指南**: `CLAUDE.md` (AI 协作者详细说明)
