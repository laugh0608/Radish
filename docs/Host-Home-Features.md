# Host 首页功能说明（Radish.HttpApi.Host）

- 适用范围：`src/Radish.HttpApi.Host` 的首页（路径 `/`）。
- 页面定位：作为网关/门户首页，聚合系统入口（Swagger、Scalar、Angular 管理端、React 前台等），提供便捷操作与状态反馈。
- 技术栈：ABP Razor Pages + LeptonX Lite 主题，配合前端脚本与本地存储实现个性化能力。

## 页面结构
- 顶部 Hero：品牌名称与欢迎语、主题/密度/语言切换、账号操作、编辑与隐藏管理入口（大屏直接展示，移动端折叠为按钮面板）。
- 最近访问：自动记录最近打开的应用（最多 2 条），支持“仅显示收藏”和“清空”。
- 收藏：集中展示已收藏的应用卡片（位于“最近访问”与“应用列表”之间）。
- 应用列表：读取 OpenIddict 中登记的应用（DisplayName、ClientUri、LogoUri），以卡片形式展示并提供工具按钮。
- 隐藏管理：集中查看/恢复被隐藏的应用（弹窗）。

## 应用展示与跳转
- 数据来源：在页面加载时查询 OpenIddict 应用列表并传入视图模型。
  - 代码：`src/Radish.HttpApi.Host/Pages/Index.cshtml.cs:12`、`src/Radish.HttpApi.Host/Pages/Index.cshtml.cs:22`、`src/Radish.HttpApi.Host/Pages/Index.cshtml.cs:26`
- Swagger/Scalar 链接补全：
  - 若客户端为 `Radish_Swagger` 或 `Radish_Scalar`，会在卡片链接末尾自动补全 `/swagger` 或 `/scalar`，确保直达文档页。
  - 代码：`src/Radish.HttpApi.Host/Pages/Index.cshtml:201`、`src/Radish.HttpApi.Host/Pages/Index.cshtml:205`
- 登录态跳转参数：
  - 若当前已登录，跳转链接会追加 `sso=true`（向后兼容 `sso=1`），用于提示目标前端按约定进行静默/交互登录。
  - 代码：`src/Radish.HttpApi.Host/Pages/Index.cshtml:198`
- 相对 Logo 支持：`LogoUri` 以 `/` 开头时，按 `wwwroot` 相对路径解析展示图标。
  - 代码：`src/Radish.HttpApi.Host/Pages/Index.cshtml:219`

## 顶部操作（大屏 + 移动端）
- 主题切换（浅色/深色）：
  - 样式变量：`src/Radish.HttpApi.Host/wwwroot/global-styles.css:17`
  - 行为脚本：`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:61`
- 界面密度（自动/舒适/紧凑）：
  - 样式：`src/Radish.HttpApi.Host/wwwroot/global-styles.css:116`
  - 行为脚本：`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:68`
- 语言切换：通过 ABP 语言切换端点刷新当前页。
  - 代码：`src/Radish.HttpApi.Host/Pages/Index.cshtml:66`
- 账号与会话：未登录显示“登录”，已登录显示“我的账户 / 退出登录”。
  - 代码：`src/Radish.HttpApi.Host/Pages/Index.cshtml:76`、`src/Radish.HttpApi.Host/Pages/Index.cshtml:83`
- 移动端操作面板：将上述操作折叠为按钮组，便于触达。
  - 代码：`src/Radish.HttpApi.Host/Pages/Index.cshtml:131`

## 卡片工具与交互
- 收藏：分区展示（位于“最近访问”与“应用列表”之间），状态保存在本地（浏览器 LocalStorage）。
  - 代码：`src/Radish.HttpApi.Host/Pages/Index.cshtml:185`、`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:161`
- 工具区布局：固定两行两列（2x2），在窄宽度下不遮挡标题；不使用“更多(⋯)”折叠。
  - 样式：`src/Radish.HttpApi.Host/wwwroot/global-styles.css:114`
  - 代码：`src/Radish.HttpApi.Host/Pages/Index.cshtml:233`、`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:111`
- 复制链接：一键复制卡片跳转地址。
  - 代码：`src/Radish.HttpApi.Host/Pages/Index.cshtml:234`、`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:139`
- 健康检查：基于卡片的 `ClientUri` 推断同源地址，调用 `{origin}/health-status` 获取健康状态并提示（Healthy/Degraded/Unhealthy）。
  - 代码：`src/Radish.HttpApi.Host/Pages/Index.cshtml:235`、`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:142`
- 隐藏/恢复：卡片可快速隐藏；通过“隐藏管理”弹窗批量查看并恢复。
  - 代码：`src/Radish.HttpApi.Host/Pages/Index.cshtml:236`、`src/Radish.HttpApi.Host/Pages/Index.cshtml:250`、`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:168`、`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:171`
- 编辑模式 + 拖拽排序：开启编辑后可拖拽卡片变更顺序，保存于本地。
  - 代码：`src/Radish.HttpApi.Host/Pages/Index.cshtml:247`、`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:175`

## 最近访问
- 自动记录最近开启的应用（最多 2 条），可选择“仅显示收藏”，支持清空。
  - 区块：`src/Radish.HttpApi.Host/Pages/Index.cshtml:173`
  - 逻辑：`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:146`、`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:148`、`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:161`、`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:164`

## 个性化与本地存储
- 所有个性化偏好均保存在浏览器 LocalStorage，仅影响当前浏览器的当前用户：
  - 主题：`radish.theme`
  - 密度：`radish.density`
  - 排序：`radish.order`
  - 收藏：`radish.favs`
  - 隐藏：`radish.hidden`
  - 最近访问：`radish.recent`（以及 `radish.recent.onlyFavs`）
  - 元信息缓存：`radish.meta`
  - 定义位置：`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:5`

## 健康检查与端点
- Host 内置健康检查：
  - JSON 端点：`/health-status`（可通过 `App:HealthCheckUrl` 配置覆盖）。
  - UI 端点：`/health-ui` 与 API：`/health-api`（HealthChecks.UI）。
  - 配置：`src/Radish.HttpApi.Host/HealthChecks/HealthChecksBuilderExtensions.cs:19`、`src/Radish.HttpApi.Host/HealthChecks/HealthChecksBuilderExtensions.cs:39`、`src/Radish.HttpApi.Host/appsettings.json:12`
- 卡片“❤”按钮：对目标系统同源的 `/health-status` 发起 GET 请求，并基于状态弹出通知。

## 如何把业务系统加入首页
- 在 OpenIddict 中登记应用（推荐使用数据迁移/种子维护）：
  - 参考：`src/Radish.DbMigrator/appsettings.json:21`（Angular/React/Swagger/Scalar 的示例 `ClientId` 与 `RootUrl`）。
  - 创建逻辑：`src/Radish.Domain/OpenIddict/OpenIddictDataSeedContributor.cs:157`、`src/Radish.Domain/OpenIddict/OpenIddictDataSeedContributor.cs:173`、`src/Radish.Domain/OpenIddict/OpenIddictDataSeedContributor.cs:183`、`src/Radish.Domain/OpenIddict/OpenIddictDataSeedContributor.cs:200`
- 字段说明：
  - `DisplayName`：卡片标题。
  - `ClientUri`：卡片入口地址（支持相对 Logo：`/images/...`）。
  - `LogoUri`：卡片图标地址（`/` 开头将从 Host `wwwroot` 解析）。
  - Swagger/Scalar：`ClientUri` 最终应指向具体文档页（`/swagger`、`/scalar`），已在种子逻辑中自动处理。
- 变更后重跑迁移/种子或在管理端同步更新，首页会自动读取最新配置。

## 主题与资源注入
- 全局样式和脚本通过 ABP Bundling 注入，无需在页面手动引用：
  - 样式：`/global-styles.css` → `src/Radish.HttpApi.Host/RadishHttpApiHostModule.cs:262`
  - 脚本：`/global-scripts.js` → `src/Radish.HttpApi.Host/RadishHttpApiHostModule.cs:267`

## 品牌与本地化
- 品牌名称来自本地化资源：`AppName`。
  - 代码：`src/Radish.HttpApi.Host/RadishBrandingProvider.cs:16`
- 页面中所有固定文案均走资源本地化（`Radish` 资源），可在本地化文件中维护。

## 常见问题
- 点击 Swagger/Scalar 仅回到根路径？
  - 已在首页渲染逻辑与 OpenIddict 种子中统一补全文档页路径。
- 健康检查失败？
  - 确保目标系统实现了 `{origin}/health-status` 端点，且允许跨源访问（如有需要）。
- 首页顺序或收藏丢失？
  - 这些为浏览器本地存储项，请检查是否更换了浏览器/设备或清理了站点数据。
- 顶部“密度”下拉在全屏时被遮挡？
  - 原因：Hero 容器使用了 `overflow: hidden`；已在下拉展开时临时允许溢出（`dropdown-open`），收起后恢复。
  - 代码：`src/Radish.HttpApi.Host/wwwroot/global-styles.css:72`、`src/Radish.HttpApi.Host/wwwroot/global-scripts.js:68`

---
如需将该文档链接加入 docs 首页或 README，我可以一并补充目录导航。
