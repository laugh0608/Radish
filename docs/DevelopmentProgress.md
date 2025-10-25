# 开发日志

## 第一阶段

### 2025.10.25

- feat(auth): Angular 端为主要路由加 authGuard，未登录访问自动跳转登录；保留 account 路由匿名访问，避免循环重定向。
- fix(host): 首页 Swagger/Scalar 卡片点击仅“原地刷新”——按应用 ClientId 自动补全 /swagger、/scalar。
- feat(openiddict): 数据种子将 Swagger/Scalar 的 ClientUri 初始化到具体文档页（/swagger、/scalar），并在迁移时同步更新已有记录；保留正确的 redirectUri（/swagger/oauth2-redirect.html、/scalar/...）。
- feat(backend): 启动时打印 CORS 允许来源；通过 .env 的 App__CorsOrigins 管理跨域来源，.env.example 预置本地来源示例（5173/4200）。
- refactor(config): 注释弃用 appsettings.json 的 App:CorsOrigins，统一改为 .env 的 App__CorsOrigins 管理。
- docs(backend/frontend/SSO): 同步统一 HTTPS 与 CORS 方案，完善 dev-certs 脚本与启动日志确认要点；提供 Host/DbMigrator 的 .env.example；为 React/Angular 指南补充配置要点。
- chore(dev): 统一本地 HTTPS 证书目录与引用；为 Angular/React 的 serve 配置证书。

### 2025.10.24

- feat(config): 按环境加载 .env，抽离敏感项并提供示例文件（Host/DbMigrator 支持 .env.development 与 .env.product，忽略本地 .env.development）。
- fix(swagger): 未登录访问 /swagger 时使用 Cookie 挑战，避免回调 HTML 导致 YAML 解析错误；允许已登录会话访问 Swagger，修正 Swagger/Scalar 客户端 RootUrl。
- docs(swagger): 保留 OIDC 所需的 /connect 路径并隐藏不必要的 ABP Schemas，对文档仅保留项目 API，隐藏基础设施端点。
- chore(frontend): CI 严格安装策略与贡献者设置收尾。

### 2025.10.23

- 导航/搜索：顶部导航中部改为搜索框，移动端抽屉提供搜索输入。
- Dock 底部栏：新增玻璃态半透明栏，宽度自适应，窄屏逐级隐藏；向下滚动隐藏、停止/向上滚动显示，提供返回顶部按钮。
- 交互/动效：桌面端支持 Dock 鱼眼放大动效；图标悬浮提示气泡、通知角标示例、右键/长按菜单。
- 布局/样式：主内容区根据 Dock 实际高度自适应底部留白；glass 效果与高光描边、投影；通过 ResizeObserver 写入 `--dock-inset-bottom` 并用于 body padding。
- 实现细节：useScrollDirection 阈值 4px、最小触发高度 80px、空闲显示延迟 260ms；仅 hover 设备通过 `matchMedia('(hover: hover)')` 启用动效；鱼眼放大使用高斯影响（最大缩放约 1.65，半径约 120px）。
- 影响范围：NavBar、BottomBar、DockToggle、StickyStack、全局样式 `index.css`；i18n 新增 `dock.*` 与 `menu.*` 文案。
- 今日提交：无（以上为工作区变更摘要）。

### 2025.10.20

refactor: 将react项目从js重构为ts

### 2025.10.8

feat: 暂时完成了 API 手动和自动控制器的版本控制

* feat: 完成了 Swagger 的多版本配置（鉴权认证还没做）
* feat: 完成了 Scalar 的多版本配置（鉴权认证还没做，而且还读取的是 `/swagger/xx/swagger.json` ）
* 目前主要是 ABP 框架的不兼容导致的，后面再研究怎么手动来实现：
  * 鉴权和 OIDC 认证
  * API 文档的注释
  * API 文档和 Schme 的过滤显示

### 2025.10.6

feat: 增加了 HttpApi.Host 项目首页的项目展示

### 2025.10.5

feat: 增加了 Scalar 配置

### 2025.10.4

docs: 完成了项目目录架构的初版

### 2025.9.29

build: 在 ABP Studio 中增加了 React 项目的启动脚本

### 2025.9.28

docs: 写了开发大纲

### 2025.9.27

feat: 决定使用 Radish 项目名称，创建 ABP 项目
