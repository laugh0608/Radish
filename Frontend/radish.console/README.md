# Radish Console - 后台管理系统

## 功能概述

Radish Console 是 Radish 项目的管理员控制台，当前已落地并接入权限治理的模块包括：

- `Dashboard` - 仪表盘与快捷入口
- `Applications` - OIDC 客户端管理
- `Users` - 用户列表与详情入口（仅保留已落地能力）
- `Roles` - 角色管理
- `Products` - 商品管理
- `Orders` - 订单管理
- `Tags` - 标签管理
- `Stickers` - 表情包管理
- `SystemConfig` - 系统配置
- `Hangfire` - 定时任务后台入口

## 当前状态

当前 Console 不再处于“基础脚手架待实现”阶段，而是处于 **权限治理 V1 收口阶段**。

当前重点是：

- 统一路由、菜单、搜索、按钮权限口径
- 对齐前端权限常量、后端资源映射与 `DbMigrate` 种子
- 共享上传接口边界已按方案 B 完成最小收口
- 清理未落地能力的伪入口
- 为本阶段形成明确的收口清单

## 目录结构

```text
Frontend/radish.console/
├── src/
│   ├── api/                # API 客户端
│   ├── components/         # Console 专用组件
│   ├── constants/          # Console 权限常量等
│   ├── hooks/              # usePermission 等 Hook
│   ├── pages/              # 页面组件
│   ├── router/             # 路由定义、权限元数据、RouteGuard
│   ├── services/           # token / 认证相关服务
│   ├── types/              # TypeScript 类型定义
│   └── utils/              # logger 等工具
├── public/
├── package.json
└── vite.config.ts
```

## 技术要点

- **路由**：`React Router`
- **权限消费**：`CurrentUserVo.VoPermissions` + `usePermission`
- **路由守卫**：`RouteGuard`
- **HTTP 客户端**：统一使用 `@radish/http` / `@radish/ui` 能力
- **特殊上传**：仅在需要进度回调时使用 `XMLHttpRequest`，并从 `getApiClientConfig()` 获取配置

## 开发约定

### 环境配置

- `.env.development` - 开发环境配置
- `.env.production` - 生产环境配置
- `.env.local` - 本地覆盖配置（不提交）
- `.env.local.example` - 本地配置示例

### 启动方式

```bash
npm run dev --workspace=radish.console
```

### 权限校验

```bash
npm run check:console-permissions
```

### 注意事项

- 普通 API 调用不要再自定义 fetch 封装
- 未落地能力不要提前暴露按钮、权限常量或页面入口
- 若页面新增真实后端依赖，需同步检查：前端权限常量、`ConsolePermissions`、`DbMigrate`
- 权限链路改动后，先运行 `npm run check:console-permissions` 再提交

## 相关文档

- `Docs/guide/console-system.md`
- `Docs/guide/console-permission-governance.md`
- `Docs/guide/console-permission-coverage-matrix.md`
- `Docs/planning/current.md`
