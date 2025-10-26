# 开发日志

## 第一阶段

### 2025.10.26

- feat(angular/mobile): 新增移动端底部导航（自动从路由构建）、BottomSheet 子菜单与“更多”分组；滚动隐藏/显示；加入“管理”虚拟组；语言入口归入“更多”；桌面端新增明暗主题切换工具；移动端列表卡片化与小屏模态全屏。
- fix(angular/mobile): 修复子菜单交互与导航可靠性、语言切换即时生效并设置 html[lang]、Toolbar 注入与按钮丢失、并发请求降噪、分组检测等问题。
- chore(dev): 本地联调默认策略统一为“Host 以 HTTPS 提供 API(44342)，前端默认 HTTP(4200/5173)”，CORS 建议双协议且 Host 自动补全；为避免 44342 冲突，取消同端口双协议监听。配套文档与示例环境更新。
- docs(backend/frontend/SSO): 同步本地联调与证书信任说明、CORS/Redirect 示例、预检自测命令等；根 README_CN/EN 增补策略说明。
- fix(host,angular): 切换字体资源到国内镜像并增加全局字体回退，修复移动端叠层问题。
- feat(host): 重构 Host 首页（Radish Landing）：Hero 渐变与轻动效、响应式网格卡片、浅/深主题与持久化、移动端功能面板、最近访问、密度切换（含自动）、健康检查按钮、拖拽排序编辑、隐藏与显隐管理、区块标题与 I18N。
- fix(host): 修正 Hero full‑bleed/边缘过渡与容器布局细节；优化移动端操作区排布与卡片工具栏遮挡。
- docs(host): 新增 Host 首页功能说明文档（docs/Host-Home-Features.md），并在 docs/README.md 加入链接，完善导航。

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
