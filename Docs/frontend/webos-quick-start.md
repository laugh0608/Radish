# Radish WebOS 工作台快速开始

本文只描述当前 `radish.client` 的桌面工作台事实，不再混入已经失效的历史 TODO。

## 1. 启动方式

```bash
npm run dev --workspace=radish.client
```

当前开发端口：

```text
http://localhost:3000/
```

如果你通过 Gateway 联调，则仍以 `https://localhost:5000/` 为统一外部入口。

路线复盘后，根路径 `/` 与默认浏览器入口转向纯 Web。普通浏览器访问 `/` 已进入 `/discover` 公开分发页；WebOS 工作台只作为 `/desktop` 保留入口。

## 2. 当前定位

`radish.client` 当前仍包含 **WebOS 桌面工作台实现**，但其产品定位已调整为 `/desktop` 保留入口：

- 面向桌面端与高交互场景
- 继续维护 Dock、窗口系统与桌面内应用打开体验
- 与纯 Web 壳层、Flutter 客户端壳层分工协作
- 不再作为新增功能默认承载层
- 不再作为 PC/Tauri 的默认 UI；未来 PC 客户端若重启，应以 Tauri 增强纯 Web 体验为主

更完整的壳层分工见 [前端设计](/frontend/design) 与 [前端多壳层策略](/frontend/shell-strategy)。

## 3. 当前可确认的工作台能力

- 状态栏、桌面图标、Dock、窗口管理器构成工作台骨架
- 新开窗口会基于当前视口计算默认位置
- 窗口拖动、缩放、最大化恢复会记忆最后一次正常态位置
- 带稳定业务参数的窗口会分开记忆布局，避免不同帖子、文档或用户主页互相污染
- 欢迎、论坛、文档、商城、排行榜等公开内容可以匿名进入
- 聊天室、个人主页、通知中心、等级详情、萝卜坑等私有能力仍要求登录

## 4. 目录定位

```text
Frontend/radish.client/src/
├── desktop/   # 桌面工作台核心：Shell、Dock、WindowManager、AppRegistry
├── apps/      # 桌面内应用
├── widgets/   # 工作台小部件与窗口容器
├── stores/    # 用户态、窗口态等全局状态
└── shared/    # client 专属共享逻辑
```

共享包职责：

- `Frontend/radish.ui`：UI 组件与样式语义
- `Frontend/radish.http`：统一 HTTP 客户端与认证续期

## 5. 调试时常看的点

### 当前用户态

- 登录态与用户信息统一由 OIDC 流程和前端用户状态共同驱动
- 未登录时公开应用可以直接打开，私有应用会在入口处拦截

### 窗口行为

- 先看窗口是否命中了稳定业务参数
- 再看是否是 `__navigationKey` 这类只用于刷新内容的临时参数
- 如果布局异常，优先排查窗口状态持久化与恢复逻辑，而不是先加 UI fallback

## 6. 开发时的最小验证

```bash
npm run type-check --workspace=radish.client
npm run build --workspace=radish.client
```

如果改动触达共享包，还应补：

```bash
npm run type-check --workspace=@radish/ui
npm run type-check --workspace=@radish/http
```

## 相关文档

- [前端设计](/frontend/design)
- [前端多壳层策略](/frontend/shell-strategy)
- [前端 workspace 开发指南](./development.md)
- [前端 API 客户端使用指南](./api-client.md)
