# Radish Console - 后台管理系统

## 📋 功能概述

Radish Console 是 Radish 项目的后台管理控制台，提供以下功能：

- **仪表盘** - 系统概览和快速导航
- **应用管理** - OIDC 客户端应用的完整 CRUD 操作
- **用户管理** - （待实现）
- **角色管理** - （待实现）

## 🏗️ 架构设计

### 目录结构

```
radish.console/
├── src/
│   ├── components/         # Console 专用组件
│   │   └── AdminLayout/    # 后台管理布局
│   ├── pages/              # 页面组件
│   │   ├── Dashboard/      # 仪表盘
│   │   ├── Applications/   # 应用管理
│   │   ├── Users/          # 用户管理（待实现）
│   │   └── Roles/          # 角色管理（待实现）
│   ├── api/                # API 客户端
│   │   ├── client.ts       # 通用 API 工具
│   │   └── clients.ts      # 客户端管理 API
│   ├── types/              # TypeScript 类型定义
│   │   └── oidc.ts         # OIDC 相关类型
│   ├── App.tsx             # 应用入口
│   └── main.tsx            # 主文件
```

### 组件来源

- **基础组件**: 从 `@radish/ui` 导入
- **Ant Design 组件**: 通过 `@radish/ui` re-export 使用
- **专用组件**: 在 `src/components` 中实现

## 🚀 开发指南

### 启动开发服务器

```bash
# 方式 1: 使用 npm workspace 命令（推荐）
npm run dev --workspace=radish.console

# 方式 2: 直接在 console 目录下
cd radish.console
npm run dev
```

访问地址: `http://localhost:3200`

### 通过 Gateway 访问

```bash
# 启动 Gateway（端口 5000）
cd Radish.Gateway
dotnet run

# 通过 Gateway 访问 console
# 浏览器访问: https://localhost:5000/console
```

## 📝 当前实现状态

### ✅ 已完成

1. **AdminLayout 后台布局**
   - 侧边栏菜单
   - 顶部用户信息
   - 响应式折叠
   - 用户下拉菜单

2. **应用管理页面**
   - 客户端列表展示
   - 新增客户端
   - 编辑客户端（部分）
   - 删除客户端（带确认）
   - 重置客户端密钥
   - 分页和刷新

3. **API 客户端工具**
   - 统一的 fetch 封装
   - 自动附加 Bearer Token
   - 响应解析工具

### ⏳ 待实现

1. **OIDC 认证集成**
   - 登录流程
   - Token 管理
   - 自动续期

2. **用户管理模块**
   - 用户列表
   - 用户 CRUD
   - 角色分配

3. **角色管理模块**
   - 角色列表
   - 角色 CRUD
   - 权限配置

## 🔧 技术栈

- **框架**: React 19 + TypeScript
- **构建工具**: Vite (Rolldown)
- **UI 组件**: Ant Design (通过 @radish/ui)
- **状态管理**: React useState (未来可能引入 Zustand)
- **HTTP 客户端**: Fetch API

## 📚 相关文档

- [CLAUDE.md](../CLAUDE.md) - 项目总体指南
- [开发计划](../radish.docs/docs/development-plan.md) - 项目里程碑与迭代计划
- [UI 组件库](../radish.docs/docs/frontend/ui-library.md) - @radish/ui 组件库说明

## ⚠️ 注意事项

1. **依赖管理**: 所有 npm 操作应在同一环境（Windows 或 WSL）下执行
2. **API 端点**: 默认使用 `https://localhost:5000`（Gateway）作为 API 基础 URL
3. **认证**: 当前使用 localStorage 存储 access_token（临时方案）
