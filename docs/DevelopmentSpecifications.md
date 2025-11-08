# 开发规范

## 项目结构约定

- docs/：项目文档，实际文件夹，映射解决方案中的 docs 目录，包含开发规范、设计文档等
- others/：其他资源文件，虚拟文件夹，只是解决方案中的文件夹，其中所有文件均为项目根目录下的，包括 Dockerfile、GitHub 配置、start.ps1 脚本 等
- radish.client：主要 - 前端 React 应用代码，TypeScript 编写
- Radish.Server：主要 - 后端服务代码，ASP.NET Core 编写
- Radish.Common：后端服务使用的普通工具类，例如基础日志、基础配置等
- Radish.Core：后端核心业务逻辑与算法类，保留模块，为后续流程模拟与算法实现做准备
- Radish.Extension：后端扩展功能模块类，例如 Swagger/Scalar、HealthCheck 等
- Radish.IRepository：后端数据访问接口类，定义数据访问层接口
- Radish.IServices：后端服务接口类，定义业务逻辑层接口
- Radish.Model：后端数据模型类，定义数据库实体模型
- Radish.Repository：后端数据访问实现类，具体实现数据访问接口
- Radish.Services：后端服务实现类，具体实现业务逻辑接口
- Radish.Shared：前后端共享的模型和工具类，例如 DTO、枚举等

## 分层依赖约定

- 前端项目（radish.client）仅依赖 npm 包
- 后端项目按层次结构依赖：
  - Radish.Server 依赖于 radish.client，用以同步启动前端应用
  - Radish.Repository 依赖于 Radish.Model
    - Radish.Model 依赖于 Radish.Common
  - Radish.Core 暂时保留，无直接依赖关系

## 项目依赖约定

- 前端依赖管理使用 npm 或 yarn，推荐使用 yarn 以提高安装速度和一致性
- 后端依赖管理使用 NuGet，推荐使用最新稳定版本的包（兼容 .NET 10）
- 所有第三方依赖需经过安全审查，确保无已知漏洞
- 定期更新依赖包，保持项目安全和性能
- 使用依赖锁定文件（如 package-lock.json 或 yarn.lock）确保团队成员使用相同版本的依赖
- 避免使用过时或不再维护的库，优先选择社区活跃且有良好文档支持的库
- 对于大型依赖，考虑使用按需加载或代码拆分以优化性能
- 记录所有依赖变更，确保团队成员了解更新内容和影响
