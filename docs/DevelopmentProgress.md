# 开发日志

## 第一阶段

### 2025.10.24

- feat(config): 按环境加载 .env，抽离敏感项并提供示例文件（Host/DbMigrator 支持 .env.development 与 .env.product，忽略本地 .env.development）。
- fix(swagger): 未登录访问 /swagger 时使用 Cookie 挑战，避免回调 HTML 导致 YAML 解析错误；允许已登录会话访问 Swagger，修正 Swagger/Scalar 客户端 RootUrl。
- docs(swagger): 保留 OIDC 所需的 /connect 路径并隐藏不必要的 ABP Schemas，对文档仅保留项目 API，隐藏基础设施端点。
- chore(frontend): CI 严格安装策略与贡献者设置收尾。

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
