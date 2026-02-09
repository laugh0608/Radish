# Radish Client

Radish 社区平台的前端应用，基于 WebOS 架构设计，提供类似桌面操作系统的用户体验。

## 🚀 快速开始

```bash
# 安装依赖
npm install

# 启动开发服务器
npm run dev

# 构建生产版本
npm run build
```

## ⚙️ 环境配置

项目使用 Vite 环境变量进行配置管理。

### 配置文件

- `.env.development` - 开发环境配置（已提交）
- `.env.production` - 生产环境配置（已提交）
- `.env.local` - 本地覆盖配置（不提交，需手动创建）
- `.env.local.example` - 本地配置示例（已提交）

### 可配置项

```bash
# API 基础 URL
VITE_API_BASE_URL=https://localhost:5000

# SignalR Hub URL
VITE_SIGNALR_HUB_URL=https://localhost:5000

# 是否启用 Mock 数据
VITE_ENABLE_MOCK=false

# 是否启用调试模式
VITE_DEBUG=true

# 功能开关
VITE_FEATURE_DARK_MODE=true
VITE_FEATURE_I18N=false
```

### 本地开发配置

如需自定义本地配置，复制 `.env.local.example` 为 `.env.local`：

```bash
cp .env.local.example .env.local
# 然后编辑 .env.local 修改配置
```

**注意**：`.env.local` 不会提交到 Git，可以安全地存放个人配置。

### 在代码中使用

```typescript
// 方式 1: 直接使用
const apiUrl = import.meta.env.VITE_API_BASE_URL;

// 方式 2: 通过 env 工具（推荐）
import { env } from '@/config/env';
const apiUrl = env.apiBaseUrl;
const isDebug = env.debug;
```

## 📱 访问地址

- **WebOS Desktop**: `http://localhost:3000/` - 默认桌面系统
- **组件展示**: `http://localhost:3000/?showcase` - UI 组件库预览
- **OIDC Demo**: `http://localhost:3000/?demo` - 认证流程演示

## 📚 文档

完整文档请访问：[Docs/radish.docs/docs](../../Docs/radish.docs/docs/)

- [WebOS 快速开始指南](../../Docs/radish.docs/docs/frontend/webos-quick-start.md) - 详细的使用指南
- [UI 组件库文档](../../Docs/radish.docs/docs/frontend/ui-library.md) - UI 组件使用文档
- [前端设计文档](../../Docs/radish.docs/docs/frontend/design.md) - WebOS 架构设计
- [开发规范](../CLAUDE.md) - 项目开发规范

## 🎨 技术栈

- **框架**: React 19 + TypeScript
- **构建工具**: Vite (Rolldown)
- **状态管理**: Zustand
- **UI 组件**: 自研组件库 (CSS Modules)
- **图标系统**: @iconify/react
- **窗口系统**: react-rnd
- **国际化**: react-i18next

## 📦 项目结构

```
src/
├── desktop/              # 桌面系统核心
├── apps/                # 子应用
├── widgets/             # 桌面小部件
├── stores/              # 状态管理
├── shared/ui/           # 通用 UI 组件
└── api/                 # API 客户端
```

## 🔧 开发命令

```bash
npm run dev              # 启动开发服务器
npm run build            # 构建生产版本
npm run preview          # 预览生产构建
npm run lint             # 代码检查
```

## 📝 相关项目

- [radish.docs](../../Docs/radish.docs/) - 项目文档
- [Radish.Api](../Radish.Api/) - 后端 API 服务
- [Radish.Auth](../Radish.Auth/) - OIDC 认证服务
- [Radish.Gateway](../Radish.Gateway/) - API 网关

---

更多信息请查看 [完整文档](../../Docs/radish.docs/docs/)
