# 文档指南

欢迎使用 **Radish 文档站**。本页作为文档入口概览，类似 VitePress 官方站点的 `Guide` 页面，你可以从这里跳转到各个专题文档。

## 推荐阅读顺序

1. [项目概览](/)
   - 了解 Radish 的整体定位与能力构成
2. [开发规范](/architecture/specifications)
   - 查看目录结构、分层架构与依赖约束
3. [架构总览](/architecture/overview)
   - 快速理解系统拓扑与职责边界
4. [开发框架说明](/architecture/framework)
   - 了解技术栈选型、关键中间件和工程实现细节
5. [认证与权限](/guide/authentication)
   - 掌握登录、权限模型与网关侧控制
6. [前端设计](/frontend/design)
   - 理解 WebOS 桌面范式、窗口系统与组件规划
7. [Gateway 服务网关](/guide/gateway)
   - 了解统一服务入口与路由转发架构
8. [部署指南](/deployment/guide)
   - 理解容器化、环境配置与上线流程
9. [开发计划](/development-plan)
   - 了解阶段性目标与路线图
10. [开发日志](/changelog/)
   - 回顾每周/月的改动与迭代节奏

## 如何访问

- 通过 Gateway 对外入口访问文档：
  - `https://localhost:5000/docs` → 当前首页
  - `https://localhost:5000/docs/guide` → 本文档
- 本地开发时，radish.docs 运行在 `http://localhost:3100`，但推荐始终从 Gateway 入口访问，以模拟真实部署环境。

接下来，建议从上方列表中选择一个章节开始阅读，例如 [架构总览](/architecture/overview) 或 [开发规范](/architecture/specifications)。
