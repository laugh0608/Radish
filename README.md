# Radish

<p align="center">
  <img src="./Docs/images/RadishAcg-256.png" alt="萝卜娘" width="256">
</p>

Radish 是面向小规模兴趣与创作者群体的现代社区产品。它以帖子、评论和问答承载内容生产与讨论，以聊天、关注和通知形成复访闭环，以 Docs 沉淀可长期阅读的知识；宠物、经验、资产和商城是可选激励层，不取代社区主轴。

正式产品入口采用 Web-first：PC 与 mobile 浏览器共享公开浏览、登录参与和私域复访主路径；Flutter 维持移动原生承接，WebOS 只保留历史兼容，Tauri / Rust 属于后置实验线。技术实现基于 ASP.NET Core 10 + SQLSugar + PostgreSQL，以及 React 19 + Vite + TypeScript。

## 当前状态

- **当前阶段**：`第三开发阶段：真实使用增长与长期契约治理`
- **当前主线**：`P3-12-F 正式版发布候选`
- **复核日期**：`2026-07-11`
- **当前结论**：
  - `Q0` 与 `E8-B` 有限产品矩阵已完成，当前无已知 `P0/P1`，项目已进入 `P3-12-F`
  - `Q1-A` 事务后可靠任务与 `Q1-B` API 错误契约已完成；`Q1-C` 文件令牌运行时已落地，待目标库 `apply / verify` 和 PostgreSQL 并发用例
  - Q1-C 环境门禁关闭后进入 `Q2-A` 时间语义，再推进 PostgreSQL / OpenIddict 升级与版本单一真值
  - 合并 `master`、创建 tag 与部署是三个独立决策；当前不因文档收口自动触发任何一个动作
  - Flutter 转维护线，WebOS 仅保留兼容入口，Tauri / Rust 继续后置，避免多端同时扩张稀释正式 Web 主线
  - 当前规划、优先级与范围以 `Docs/planning/current.md` 为准
- **当前验证基线**：
  - 快速基线：`npm run validate:baseline:quick`
  - 完整基线：`npm run validate:baseline`
  - 宿主 / 配置基线：`npm run validate:baseline:host`
  - 发布、部署与回滚维护线：`Docs/guide/m14-*`、`Docs/guide/m15-*`、`Docs/guide/post-m15-quality-baseline.md`

## 技术栈

- **后端**：ASP.NET Core 10、SQLSugar、FluentValidation、Serilog
- **数据库**：PostgreSQL 16（本地开发可用 SQLite）
- **前端**：React 19、Vite (Rolldown)、TypeScript
- **测试**：xUnit + Shouldly（后端）、`node --test` + TypeScript 类型检查 + `HttpTest`（前端 / 联调资产）
- **容器化**：已提供 `Radish.DbMigrate / Radish.Api / Radish.Auth / Radish.Gateway / Frontend` 五个 Dockerfile，以及本地容器验证 `Deploy/docker-compose.local.yaml` 与部署态 `Deploy/docker-compose.yaml`

## 快速开始

### 前置要求

- .NET 10 SDK
- Node.js 24+
- PostgreSQL 16+ （或使用默认的 SQLite）

### 启动项目

```bash
# 方式 1：使用一键脚本（推荐）
pwsh ./start.ps1    # Windows/PowerShell（单服务 1-7；组合：Gateway+Auth+API、Frontend+Console、或一键启动全部）
./start.sh          # Linux/macOS（交互式菜单；组合启动 Ctrl+C 会清理后台服务残留进程）

# 方式 2：手动启动后端
dotnet restore
dotnet run --project Radish.Api/Radish.Api.csproj

# 方式 3：手动启动前端
npm install
npm run dev --workspace=radish.client
```

启动后常见入口：
- **Gateway 门户**：https://localhost:5000  （统一入口，下挂各子系统；http://localhost:5001 会自动重定向到此地址）
- **纯 Web 默认入口**：https://localhost:5000/        （普通浏览器默认进入 `/discover`）
- **Web 功能地图**：https://localhost:5000/workbench  （正式 Web 功能总入口）
- **WebOS 历史入口**：https://localhost:5000/desktop  （保留桌面工作台能力）
- **固定文档源**：`Docs/`                              （仓库内固定项目文档）
- **文档应用**：内置到 WebOS 中，统一展示固定文档与在线文档
- **API 文档（Scalar）**：https://localhost:5000/scalar  （Gateway 转发到 Radish.Api 的 `/scalar`，`/api/docs` 作为旧路径已重定向到 `/scalar`）
- **控制台（radish.console）**：https://localhost:5000/console

如需直连后端服务（仅用于本机调试，下游服务不直接对外暴露）：
- **后端 API（内部调试）**：http://localhost:5100
- **API 文档（直连）**：http://localhost:5100/scalar
- **前端 dev**：http://localhost:3000
- **Console dev**：http://localhost:3100

### 常用命令

```bash
# 后端开发
dotnet watch --project Radish.Api           # 热重载
dotnet test Radish.Api.Tests                # 运行测试
dotnet build Radish.slnx -c Debug           # 构建解决方案
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- doctor  # 只读检查 DbMigrate 环境
dotnet run --project Radish.DbMigrate/Radish.DbMigrate.csproj -- seed    # 初始化结构与种子数据

# 前端开发（必须在项目根目录运行）
npm run dev --workspace=radish.client       # 前端开发服务器
npm run dev --workspace=radish.console      # 控制台开发服务器
npm run build --workspace=radish.client     # 生产构建
npm run lint --workspace=radish.client      # 代码检查
npm run test --workspace=radish.client      # 当前前端测试脚本

# 或使用快捷脚本
npm run dev:frontend                        # 启动 radish.client
npm run dev:console                         # 启动 radish.console

# UI 组件库开发
npm run type-check --workspace=@radish/ui   # 类型检查
npm run lint --workspace=@radish/ui         # 代码检查

# Windows 用户注意：
# 优先在仓库根目录通过 npm workspace 参数执行子项目命令，避免子目录依赖解析口径漂移。
```

在当前仓库环境中，若本机 `dotnet` 受用户目录、NuGet 审计或并发还原影响，优先使用根目录脚本包装命令：

```powershell
powershell -ExecutionPolicy Bypass -File Scripts\dotnet-local.ps1 build Radish.slnx -c Debug
powershell -ExecutionPolicy Bypass -File Scripts\dotnet-local.ps1 test Radish.Api.Tests
```

当前更推荐的统一验证入口是：

```bash
npm run validate:baseline:quick
npm run validate:baseline
npm run validate:baseline:host
```

### 仓库文本规范

- 仓库自有文本文件默认使用 `UTF-8` 无 `BOM`
- 默认使用 `LF`；`*.sln`、`*.slnx`、`*.ps1`、`*.psm1`、`*.psd1`、`*.bat`、`*.cmd` 使用 `CRLF`
- 保持文件末尾换行；Markdown 以外的文本避免尾随空格
- 规则来源：仓库根 `.editorconfig`、`.gitattributes` 与 `Scripts/check-repo-hygiene.mjs`
- 日常改动后优先运行 `npm run check:repo-hygiene:changed`；做历史文本治理时再运行 `npm run check:repo-hygiene`

### 配置文件约束

- 共享默认配置：根目录 `appsettings.Shared.json`
- 宿主默认配置：各宿主自己的 `appsettings.json`
- 本地覆盖配置：各宿主自己的 `appsettings.Local.json`（仅本地使用，禁止提交）
- 禁止新增或提交这三种之外的 `appsettings.*.json` 配置文件；部署差异统一通过 `appsettings.Local.json` 或环境变量覆盖

## 项目结构

```
Radish/
├── Docs/                            # 📚 固定项目文档（开发规范、架构设计、部署指南等）
├── Clients/radish.flutter/          # 📱 Flutter 移动原生客户端
├── Clients/radish-tauri/            # 🖥️ Tauri 桌面安装包壳层
├── Frontend/radish.client/               # ⚛️ React 正式 Web 应用（含 WebOS 历史兼容入口）
├── Frontend/radish.console/              # 🎛️ 管理控制台前端
├── Frontend/radish.ui/                   # 🎨 UI 组件库（共享组件、Hooks、工具函数）
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

完整的固定项目文档现位于 `Docs/` 目录：

### 核心文档
- 📘 [**开发规范**](Docs/architecture/specifications.md) - 目录职责、分层依赖、代码约定
- 📗 [**架构设计**](Docs/architecture/framework.md) - 技术选型、分层架构、数据持久化
- 📙 [**开发路线图**](Docs/development-plan.md) - 当前阶段主线、下一顺位与维护线
- 📒 [**第三开发阶段总纲**](Docs/planning/phase-three-real-usage-contract-governance.md) - 真实使用增长、契约治理与 P3-12 阶段边界
- 📓 [**P3-12-E8 发布工程收口**](Docs/planning/p3-12-e8-release-engineering-maturity-security-closure.md) - Q0、F 内 Release Go 与持续维护的分层门禁
- 📗 [**前端多壳层策略**](Docs/frontend/shell-strategy.md) - 公开内容、桌面工作台与 Flutter 客户端分工
- 📓 [**当前进行中**](Docs/planning/current.md) - 当前正式主线与并行维护项
- 📔 [**已完成摘要**](Docs/planning/archive.md) - 第一开发阶段与历史里程碑收口
- 📕 [**开发日志**](Docs/changelog/) - 按月份/周记录的开发历程
- ✅ [**验证基线**](Docs/guide/validation-baseline.md) - 当前统一验证入口、分层使用建议与边界说明

### 专项文档
- 🔐 [**认证与权限**](Docs/guide/authentication.md) - OIDC 认证流程与权限体系
- 🎨 [**前端设计**](Docs/frontend/design.md) - 正式 Web、Flutter 与历史兼容壳层的设计边界
- 🚪 [**Gateway 服务网关**](Docs/guide/gateway.md) - 统一服务入口与路由转发
- 🚀 [**部署指南**](Docs/deployment/guide.md) - 容器化、CI/CD、生产部署
- 当前部署口径：开发运行使用 IDE / `dotnet run` / `npm run dev`；本地容器验证使用 `Deploy/docker-compose.local.yaml`；测试与生产共用 `Deploy/docker-compose.yaml`，默认通过 `RADISH_IMAGE_TRACK=test/release` 拉取 `test-latest` / `release-latest`，需要可复现部署时再启用固定 `RADISH_IMAGE_TAG`；所有容器编排都会先执行 `dbmigrate apply` 初始化共享业务库
- 🧩 [**文件上传设计**](Docs/features/file-upload-design.md) - 文件上传与图片处理方案
- 🦀 [**Rust 扩展**](Docs/guide/rust-extensions.md) - radish-lib 使用指南

### 前端文档
- 🎨 [**UI 组件库**](Docs/frontend/ui-library.md) - @radish/ui 入口文档
- 🧱 [**组件说明**](Docs/frontend/components.md) - 组件与用法说明
- ⚡ [**快速参考**](Docs/frontend/quick-reference.md) - 常用 API 速查表

### 其他资源
- 📖 [**文档首页**](Docs/index.md) - 文档地图与推荐阅读路径
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
- ✅ **Web-first 体验**：React 19 同时承接 PC / mobile 正式 Web，`/desktop` 保留 WebOS 历史兼容
- ✅ **UI 组件库**：`@radish/ui` 共享组件、Hooks 与设计 token
- ✅ **npm Workspaces**：monorepo 管理，组件热更新
- ✅ **TypeScript**：完整的类型定义和类型安全
- ✅ **Vite (Rolldown)**：极速构建和热模块替换

### 其他特性
- ✅ **Rust 扩展**：预留高性能原生模块支持
- ✅ **统一文档系统**：`Docs/` 固定文档 + 正式 Web 公开阅读 / 作者态 + Markdown 导入导出链路

## 配置说明

### 数据库配置

默认使用 SQLite（`Radish.db` 和 `Radish.Log.db`），首次运行自动创建。

切换到 PostgreSQL：在本地创建或编辑 `Radish.Api/appsettings.Local.json`：

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

更多配置细节参见 [开发规范](Docs/architecture/specifications.md)。

## 开发规范要点

- **先写接口，再写实现**：遵循 IService/IRepository 契约模式
- **实体不出仓储层**：Service 层必须将实体映射为 DTO/ViewModel
- **Controller 不直接访问 Repository**：所有数据访问通过 Service 层
- **配置统一读取**：使用 `AppSettings.RadishApp()` 或 `IOptions<T>`
- **日志使用 Serilog 静态方法**：避免注入 `ILogger<T>`（除非框架要求）

完整规范详见 [architecture/specifications.md](Docs/architecture/specifications.md)。

## 发版流程

Radish 采用日历版本号格式：`vYY.M.RELEASE`（如 `v26.1.1` = 2026年1月第1版）

### 版本号配置文件

根目录 `version.json` 是唯一人工版本真值。`.NET`、npm workspaces、Rust、Tauri 与 Flutter 的 manifest / lockfile 均由它同步；Flutter 只保留独立递增的商店构建号。

### 发版步骤

```bash
# 1. 在 dev 准备候选版本
git checkout dev
git pull origin dev

# 2. 编辑 version.json，然后同步并校验所有生成字段
npm run version:sync
npm run check:version-contract

# 3. 验证构建
dotnet build Radish.slnx -c Release
npm run type-check

# 4. 提交版本号、生成字段和正式发布记录
git add -A
git commit -m "chore(release): prepare v26.7.1"

# 5. 通过 dev -> master PR 合并；随后在 master 复核并创建 Git 标签
git checkout master
git pull origin master
node Scripts/version-contract.mjs --tag v26.7.1-release
git tag -a v26.7.1-release -m "Release v26.7.1: 简要描述"
git push origin v26.7.1-release

# 6. 在 GitHub 创建 Release（包含 Release Notes）
```

### 版本标识

| 后缀 | 说明 | 示例 |
|------|------|------|
| `-dev` | 开发镜像 / 开发轨道版本 | `v26.1.1-dev` |
| `-test` | 测试部署 / 客户试用版本 | `v26.2.1-test` |
| `-release` | 正式发布 | `v26.3.1-release` |

热更新 tag 格式：`vYY.M.RELEASE.DDXX-(dev|test|release)`（如 `v26.2.1.1203-test` = 12日第3次测试轨道更新）

当前 GitHub Actions 的镜像发布只响应带环境后缀的 tag：`v*-dev`、`v*-test`、`v*-release`。普通 `dev` 分支 push 不再触发镜像构建发布。

详细规范参见 [产品版本与发布标识治理](Docs/guide/version-governance.md)。

## 贡献

欢迎提交 Issue 和 Pull Request！

请确保：
1. 代码遵循项目 [开发规范](Docs/architecture/specifications.md)
2. 单元测试通过（`dotnet test`）
3. 提交前至少运行与改动匹配的 `npm run type-check`、`npm run lint` 或专题 `HttpTest`
4. 在 [开发日志](Docs/changelog/) 中记录重大变更

## 许可

DaBaiLuoBo(laugh0608) 保留所有代码权力，详见 LICENSE 文件。

## 相关链接

- [Docs 文档目录](Docs/README.md) - 固定项目文档入口
- [API 文档](https://localhost:5000/scalar) - Scalar 交互式文档（推荐通过 Gateway 访问，内部由 Radish.Api 提供 `/scalar`，旧 `/api/docs` 路径仅做重定向兼容）
- [开发计划](Docs/development-plan.md) - 迭代规划与里程碑
