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

## 📱 访问地址

- **WebOS Desktop**: `http://localhost:3000/` - 默认桌面系统
- **组件展示**: `http://localhost:3000/?showcase` - UI 组件库预览
- **OIDC Demo**: `http://localhost:3000/?demo` - 认证流程演示

## 📚 文档

完整文档请访问：[radish.docs/docs](../radish.docs/docs/)

- [WebOS 快速开始指南](../radish.docs/docs/WebOSQuickStart.md) - 详细的使用指南
- [组件库文档](../radish.docs/docs/ComponentLibrary.md) - UI 组件使用文档
- [前端设计文档](../radish.docs/docs/FrontendDesign.md) - WebOS 架构设计
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

- [radish.docs](../radish.docs/) - 项目文档
- [Radish.Api](../Radish.Api/) - 后端 API 服务
- [Radish.Auth](../Radish.Auth/) - OIDC 认证服务
- [Radish.Gateway](../Radish.Gateway/) - API 网关

---

更多信息请查看 [完整文档](../radish.docs/docs/)
