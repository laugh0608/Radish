# 开发快速开始

本文面向第一次在本地接手 Radish 的开发者，提供最短路径：环境准备、首次初始化、最小验证、启动方式与常用入口。

## 1. 前置环境

- `.NET SDK 10`
- `Node.js 24+`
- `Git`
- 可选：`PostgreSQL 16+`、`Redis`

默认本地开发不要求先准备 PostgreSQL 或 Redis。仓库共享默认配置已经提供 `SQLite + 内存缓存` 的开箱即用路径。

## 2. 默认本地形态

- 后端共享配置位于仓库根目录 [`appsettings.Shared.json`](</D:/Code/Radish/appsettings.Shared.json>)
- 宿主默认配置位于各项目 `appsettings.json`
- 本地敏感覆盖统一使用 `appsettings.Local.json`
- 前端采用 npm workspaces，包含：
  - `Frontend/radish.http`
  - `Frontend/radish.client`
  - `Frontend/radish.console`
  - `Frontend/radish.ui`

## 3. 首次初始化

### 3.1 安装前端依赖

```bash
npm install
```

### 3.2 按需补本地覆盖配置

如果只想先跑通默认链路，这一步可以先跳过。  
如需覆盖数据库、Redis、证书或域名等本地差异，请在对应宿主目录创建 `appsettings.Local.json`：

- `Radish.Api/appsettings.Local.json`
- `Radish.Auth/appsettings.Local.json`
- `Radish.Gateway/appsettings.Local.json`

如果本地直接执行 `Radish.DbMigrate apply`，并且需要创建 `system / admin / test` 开发默认账号，也可以在仓库根目录创建未提交的 `appsettings.Local.json`，只覆盖：

```json
{
  "RadishDeployment": {
    "Stage": "local"
  },
  "Seed": {
    "DeveloperDefaultsEnabled": true
  }
}
```

配置约束与示例见 [配置管理](/guide/configuration)。

## 4. 推荐最小验证

首次接手或切换分支后，建议至少先执行：

```bash
dotnet build Radish.slnx -c Debug
npm run type-check --workspace=@radish/ui
npm run type-check --workspace=@radish/http
npm run validate:baseline:quick
```

如果只改动前端，也可以先跑对应 workspace 的 `type-check` 或 `build`。完整验证口径见 [验证基线](/guide/validation-baseline)。

## 5. 启动方式

### 5.1 一键组合启动

```bash
pwsh ./start.ps1
./start.sh
```

`start.sh` 在 Linux/macOS 下会先显示交互式菜单，再按所选动作检查 `dotnet` / `npm` 等依赖。组合启动会把相关服务置于后台并记录进程组 / 子进程树，按下 `Ctrl+C` 时会先优雅停止，超时后强制清理残留进程，避免后端端口继续被占用。

### 5.2 单服务启动

```bash
dotnet run --project Radish.Api
dotnet run --project Radish.Gateway
dotnet run --project Radish.Auth

npm run dev --workspace=radish.client
npm run dev --workspace=radish.console
```

如果你只想调接口，通常先启动 `Gateway + Api + Auth`；如果你只想改前端页面，则再单独启动 `radish.client` 或 `radish.console`。

## 6. 常用地址

- Gateway：`https://localhost:5000`
- Web 功能地图：`https://localhost:5000/workbench`
- WebOS 历史入口：`https://localhost:5000/desktop`
- API：`http://localhost:5100`
- Auth：`http://localhost:5200`
- Client：`http://localhost:3000`
- Console：`http://localhost:3100`
- Scalar：`https://localhost:5000/scalar`

## 7. 接口与手工回归入口

- API 文档入口：`/scalar`
- OpenAPI JSON：
  - `https://localhost:5000/openapi/v1.json`
  - `https://localhost:5000/openapi/v2.json`
- 手工回归脚本目录：[`Radish.Api.Tests/HttpTest`](</D:/Code/Radish/Radish.Api.Tests/HttpTest>)
- 该目录使用说明：[`Radish.Api.Tests/HttpTest/README.md`](</D:/Code/Radish/Radish.Api.Tests/HttpTest/README.md>)

## 8. 推荐阅读顺序

1. [架构总览](/architecture/overview)
2. [开发规范](/architecture/specifications)
3. [API 说明索引](/guide/api-index)
4. [数据库总览](/guide/database-overview)
5. [本地运行与排障手册](/guide/operations-runbook)
6. [当前进行中](/planning/current)

如果你是接手某个具体模块，再补读对应 `guide/`、`features/` 和 `frontend/` 专题页即可。
