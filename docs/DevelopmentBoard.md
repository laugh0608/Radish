# 开发看板（可执行任务拆分）

> 本看板聚焦“执行级任务”，与路线图分离：
> - 路线图（按周目标与范围）见 docs/DevelopmentPlan.md
> - 变更/日志见 docs/DevelopmentProgress.md
> - 运行与联调细节见 backend/frontend 指南

快速入口
- 路线图: [DevelopmentPlan.md](DevelopmentPlan.md)
- 进度日志: [DevelopmentProgress.md](DevelopmentProgress.md)
- 后端指南: [backend/README.md](backend/README.md)
- React 指南: [frontend/react/README.md](frontend/react/README.md)
- Angular 指南: [frontend/angular/README.md](frontend/angular/README.md)

任务命名规范
- ID: `W<周>-<域>-<序号>`，域示例：BE（后端）、FE-REACT、FE-ANG、OPS、DOC、TEST。
- 每项含：Owner（可填用户名/群组，初始可 `TBD`）、Estimate（人天）、Deps（依赖）。
- 清单采用 `[ ]` 勾选；验收标准（Acceptance）与交付物（Deliverables）明确可验证结果。

任务模板
- 标题（ID + 名称）
  - Owner: TBD | Estimate: 0.5d | Deps: -
  - Checklist:
    - [ ] 子任务A
  - Acceptance: 列出验证方式/指标
  - Deliverables: 产出物（代码/配置/文档/截图等）

---

## Week 1｜基线与联调打通（W1）

### W1-OPS-1 本地证书与环境基线
- Owner: TBD | Estimate: 0.5d | Deps: -
- Checklist:
  - [ ] 生成并信任本机 HTTPS 证书（`dotnet dev-certs https --trust`）
  - [ ] 仓库 `dev-certs/` 证书可用（前端可选 HTTPS）
  - [ ] 验证 `https://localhost:44342` 能打开（Host 未启动时忽略）
- Acceptance: 浏览器可访问 Host HTTPS，证书受信；前端使用 HTTP 正常跨域
- Deliverables: 终端日志/截图、必要脚本文档更新（如需）

### W1-BE-1 Host/DbMigrator .env 初始化
- Owner: TBD | Estimate: 0.5d | Deps: W1-OPS-1
- Checklist:
  - [ ] 复制 `src/Radish.HttpApi.Host/.env.example` → `.env` 并配置：
        `ConnectionStrings__Default`、`App__CorsOrigins`
  - [ ] 复制 `src/Radish.DbMigrator/.env.example` → `.env` 与 Host 对齐
  - [ ] 启动 Host 打印 CORS 允许来源（含 http/https、5173/4200）
- Acceptance: 启动日志显示“CORS allowed origins: …”并与预期匹配
- Deliverables: `.env` 文件（不入库）、日志片段贴入 DevelopmentProgress.md

### W1-BE-2 DbMigrator 运行与基础种子
- Owner: TBD | Estimate: 0.5d | Deps: W1-BE-1
- Checklist:
  - [ ] `cd src/Radish.DbMigrator && dotnet run`
  - [ ] 确认初始数据/索引（若有）执行完成
- Acceptance: 迁移完成、异常为零；重复运行幂等
- Deliverables: 迁移日志片段、如有新增索引/种子在文档标注

### W1-FE-REACT-1 React 本地联调
- Owner: TBD | Estimate: 0.5d | Deps: W1-BE-1
- Checklist:
  - [ ] `react/.env.local` 设置 `VITE_API_BASE_URL=https://localhost:44342`
  - [ ] `npm run dev` 启动并访问首页
  - [ ] 调用 `/api/abp/application-configuration` 成功
- Acceptance: 控制台无 CORS 错误；能读取本地化/认证配置
- Deliverables: 截图与步骤简记（可补到 React 指南）

### W1-FE-ANG-1 Angular 本地联调
- Owner: TBD | Estimate: 0.5d | Deps: W1-BE-1
- Checklist:
  - [ ] `angular` 目录 `npm install && npm run start`
  - [ ] 验证动态环境加载；访问主页面正常
  - [ ] 调用 `/api/abp/application-configuration` 成功
- Acceptance: 控制台无 CORS 错误；能读取本地化/认证配置
- Deliverables: 截图与步骤简记（可补到 Angular 指南）

### W1-TEST-1 基线测试与健康检查
- Owner: TBD | Estimate: 0.5d | Deps: W1-BE-1
- Checklist:
  - [ ] `dotnet build`、`dotnet test` 全绿
  - [ ] 为 Host 增加最小健康检查用例或手工 curl 验证
- Acceptance: CI 本地通过；健康检查 200/204
- Deliverables: 测试通过记录、必要的测试代码（如有）

### W1-DOC-1 文档对齐与自测命令
- Owner: TBD | Estimate: 0.5d | Deps: W1-BE-1, W1-FE-REACT-1, W1-FE-ANG-1
- Checklist:
  - [ ] 在 `docs/DevelopmentProgress.md` 记录联调结论与端口/协议
  - [ ] 如有偏差，补充 `docs/backend/README.md` 的 CORS/预检命令
- Acceptance: 文档与现状一致；新人可按文档直接跑通
- Deliverables: 文档更新 PR（本地提交）

---

## Week 2｜领域与 MongoDB（W2）

### W2-BE-1 领域建模（Category/Post/Comment）
- Owner: TBD | Estimate: 1.0d | Deps: W1 完成
- Checklist:
  - [ ] 在 `src/Radish.Domain` 建立聚合/实体/值对象与不变式
  - [ ] 定义必要领域事件（PostCreated/PostLiked 等）
- Acceptance: 领域对象满足不变式，构造/行为与用例一致
- Deliverables: 领域类与注释；示例用法（注释/测试）

### W2-BE-2 Domain.Shared 权限与本地化
- Owner: TBD | Estimate: 0.5d | Deps: W2-BE-1
- Checklist:
  - [ ] `src/Radish.Domain.Shared` 定义权限常量（Post.Create 等）
  - [ ] 增补本地化键值与默认资源登记
- Acceptance: 权限键在应用层可引用；本地化生效
- Deliverables: 常量与资源文件；模块登记代码

### W2-BE-3 Mongo 仓储与索引
- Owner: TBD | Estimate: 1.0d | Deps: W2-BE-1
- Checklist:
  - [ ] 在 `src/Radish.MongoDB` 实现 `ICategoryRepository`、`IPostRepository`、`ICommentRepository`
  - [ ] 设计并创建索引（分类/创建时间/状态/作者等）
- Acceptance: 基础 CRUD 与分页查询可用；索引生效（可查计划）
- Deliverables: 仓储实现与索引创建代码

### W2-BE-4 DbMigrator 索引与种子
- Owner: TBD | Estimate: 0.5d | Deps: W2-BE-3
- Checklist:
  - [ ] 在 `src/Radish.DbMigrator` 初始化默认分类与必要索引
  - [ ] 可重复运行且幂等
- Acceptance: 迁移后 DB 状态符合预期；重复运行无副作用
- Deliverables: Migrator 代码与日志片段

### W2-TEST-1 领域与仓储测试
- Owner: TBD | Estimate: 1.0d | Deps: W2-BE-3
- Checklist:
  - [ ] 领域不变式单测（Shouldly）
  - [ ] 仓储集成测试（分页/过滤/排序）
- Acceptance: `dotnet test` 通过；覆盖关键路径
- Deliverables: `test/Radish.*.Tests` 用例与结果

### W2-DOC-1 文档与进度同步
- Owner: TBD | Estimate: 0.25d | Deps: W2 完成
- Checklist:
  - [ ] 更新 `docs/DevelopmentProgress.md`（W2 小结）
  - [ ] 如有必要，补充 `docs/DevelopmentFramework.md` 的字段/事件描述
- Acceptance: 文档与实现一致
- Deliverables: 文档更新

---

## Week 3｜应用层与 API（W3）

### W3-BE-1 Application.Contracts（DTO/接口）
- Owner: TBD | Estimate: 1.0d | Deps: W2 完成
- Checklist:
  - [ ] 定义 `CategoryDto`、`CreateUpdateCategoryDto`、`PostDto`、`CommentDto`
  - [ ] 定义分页/过滤/排序输入模型与返回结果模型
  - [ ] 定义 `ICategoryAppService`、`IPostAppService`、`ICommentAppService`
- Acceptance: 合同完整、语义清晰；Swagger 反射后结构合理
- Deliverables: `src/Radish.Application.Contracts` 代码

### W3-BE-2 Application 实现与权限
- Owner: TBD | Estimate: 1.5d | Deps: W3-BE-1
- Checklist:
  - [ ] 实现各 AppService，封装流程编排与转换
  - [ ] 权限保护（`[Authorize]` 或检查器）与策略名称对齐
  - [ ] 触发领域事件（发帖/点赞/浏览等）
- Acceptance: 基本用例在 Swagger/Scalar 可授权调试通过
- Deliverables: `src/Radish.Application` 代码

### W3-BE-3 API 暴露与文档过滤
- Owner: TBD | Estimate: 0.75d | Deps: W3-BE-2
- Checklist:
  - [ ] Host 暴露 RESTful API；版本/分组对齐
  - [ ] Swagger/Scalar 授权流程可用；仅保留项目 API
- Acceptance: 授权后可完整调试；文档干净、RootUrl 正确
- Deliverables: `src/Radish.HttpApi.*` 与 Host 配置

### W3-TEST-1 应用层与 API 测试
- Owner: TBD | Estimate: 0.75d | Deps: W3-BE-2
- Checklist:
  - [ ] 应用服务用例（含权限）单测/集成测试
  - [ ] 关键 API 冒烟（手动或自动）
- Acceptance: `dotnet test` 通过；Swagger 操作路径可复现
- Deliverables: `test/Radish.*.Tests` 用例与记录

### W3-DOC-1 文档与样例
- Owner: TBD | Estimate: 0.5d | Deps: W3-BE-3
- Checklist:
  - [ ] 更新 `DevelopmentProgress.md`（W3 小结与接口截图）
  - [ ] 在后端指南补充 Swagger/Scalar 登录授权步骤
- Acceptance: 文档可指导新人完成授权调试
- Deliverables: 文档更新

> 说明：管理端（Angular）对 Category 的最小管理可在 W3 先做“验证性对接”（可选），正式功能在 W7 完成。

