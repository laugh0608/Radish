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
  - [x] 生成并信任本机 HTTPS 证书（`dotnet dev-certs https --trust`）
  - [ ] dev-certs 文件存在：`dev-certs/localhost.crt` 与 `dev-certs/localhost.key`
  - [ ] 可选校验：`openssl x509 -in dev-certs/localhost.crt -noout -subject -issuer -dates`
  - [ ] React（HTTPS 开发）：`cd react && DEV_HTTPS=1 npm run dev`，日志出现“using ../dev-certs/localhost.{key,crt}”且本地地址为 `https://localhost:5173`
  - [ ] React 证书验证：浏览器访问 `https://localhost:5173` 证书受信，或 `curl -vk https://localhost:5173` 握手成功
  - [ ] Angular（HTTPS 开发）：`cd angular && npm run start:https` 成功启动，绑定 `../dev-certs/localhost.{key,crt}`
  - [ ] Angular 证书验证：浏览器访问 `https://localhost:4200` 证书受信，或 `curl -vk https://localhost:4200`
  - [ ] 如不受信：执行 `mkcert -install` 并按 `dev-certs/README.md` 重新生成证书
  - [x] 验证 `https://localhost:44342` 能打开（Host 未启动时忽略）
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

---

## Week 4｜Host 网关与文档硬化（W4）

### W4-BE-IDENT-1 OpenIddict 配置与数据种子
- Owner: TBD | Estimate: 1.0d | Deps: W3-BE-3
- Checklist:
  - [ ] 配置 Swagger/Scalar 的 ClientUri/RedirectUri（包含 /swagger 与 /scalar 对应回调）
  - [ ] 确保 OIDC 端点可访问，Cookie 挑战与 Token 授权均正常
  - [ ] 迁移脚本确保幂等更新已有记录
- Acceptance: Swagger/Scalar 登录授权流程完整可用
- Deliverables: Host 配置与种子代码、截图与说明

### W4-BE-SWAG-1 Swagger/Scalar 文档过滤与版本分组
- Owner: TBD | Estimate: 0.75d | Deps: W4-BE-IDENT-1
- Checklist:
  - [ ] 仅暴露项目 API，隐藏 ABP 基础设施端点
  - [ ] 根路径/反向代理下的 RootUrl 正确
  - [ ] 按版本/功能分组规范化
- Acceptance: 文档整洁、可授权调试、无 404/回环
- Deliverables: 文档配置与截图

### W4-BE-ERR-1 全局异常映射为 ProblemDetails
- Owner: TBD | Estimate: 0.5d | Deps: W3-BE-2
- Checklist:
  - [ ] 统一领域异常/验证异常返回结构
  - [ ] 错误码/错误键与本地化键对齐
- Acceptance: 关键错误路径返回一致、含追踪标识
- Deliverables: 异常中间件/过滤器与示例响应

### W4-BE-CACHE-1 列表只读缓存策略
- Owner: TBD | Estimate: 0.5d | Deps: W3-BE-2
- Checklist:
  - [ ] 为热门/首页列表增加短 TTL 缓存
  - [ ] 暴露命中率指标（可选）
- Acceptance: 命中率提升，响应更稳定
- Deliverables: 缓存配置与测量记录

### W4-TEST-1 授权与错误路径冒烟
- Owner: TBD | Estimate: 0.5d | Deps: W4-BE-IDENT-1, W4-BE-ERR-1
- Checklist:
  - [ ] Token 获取、受保护接口 401/403 行为
  - [ ] 异常返回结构断言
- Acceptance: `dotnet test` 通过；Swagger 手动验证
- Deliverables: 测试用例与记录

### W4-DOC-1 后端授权与错误文档
- Owner: TBD | Estimate: 0.25d | Deps: W4 完成
- Checklist:
  - [ ] backend/README.md 增补 Swagger/Scalar 登录步骤
  - [ ] 错误响应样例/规范
- Acceptance: 文档指导可复现
- Deliverables: 文档更新

---

## Week 5｜React 基础与认证集成（W5）

### W5-FE-REACT-AUTH-1 认证上下文与令牌刷新
- Owner: TBD | Estimate: 1.0d | Deps: W4 完成
- Checklist:
  - [ ] AuthContext：登录/登出/刷新令牌与持久化
  - [ ] 拦截器：自动附带 Token 与 401 处理
- Acceptance: 登录后续请求自动授权，过期能平滑刷新
- Deliverables: Auth 模块与示例页面

### W5-FE-REACT-ROUTE-1 路由守卫与受保护路由
- Owner: TBD | Estimate: 0.5d | Deps: W5-FE-REACT-AUTH-1
- Checklist:
  - [ ] 受保护路由与未授权重定向
  - [ ] 未登录提示与回跳
- Acceptance: 未授权访问被拦截且 UX 友好
- Deliverables: 路由守卫代码与用例

### W5-FE-REACT-LAYOUT-1 布局与主题
- Owner: TBD | Estimate: 0.75d | Deps: -
- Checklist:
  - [ ] Header/Nav/Footer 架构
  - [ ] 明暗主题与持久化
- Acceptance: 基础布局可复用；主题在刷新后保持
- Deliverables: 布局组件与样式

### W5-FE-REACT-LIST-1 分类与帖子列表
- Owner: TBD | Estimate: 1.0d | Deps: W3-BE-3
- Checklist:
  - [ ] 分类列表与导航
  - [ ] 帖子列表分页/筛选/排序
  - [ ] 骨架屏与空态
- Acceptance: 列表体验流畅，接口数据正确
- Deliverables: 列表页面与服务封装

### W5-TEST-1 前端单测（认证/路由）
- Owner: TBD | Estimate: 0.5d | Deps: W5-FE-REACT-AUTH-1
- Checklist:
  - [ ] Auth hooks/guard 单测
  - [ ] 列表组件基础快照
- Acceptance: 单测通过；基础覆盖
- Deliverables: 前端测试与报告

### W5-DOC-1 React 指南更新
- Owner: TBD | Estimate: 0.25d | Deps: W5 完成
- Checklist:
  - [ ] 环境变量/联调说明
  - [ ] 认证集成与路由守卫示例
- Acceptance: 文档清晰可复现
- Deliverables: 文档更新

---

## Week 6｜React 论坛核心交互（W6）

### W6-FE-REACT-DETAIL-1 帖子详情页
- Owner: TBD | Estimate: 0.75d | Deps: W5-FE-REACT-LIST-1
- Checklist:
  - [ ] 标题/内容/作者/计数展示
  - [ ] 初次加载与重载策略
- Acceptance: 详情渲染正确，状态稳定
- Deliverables: 详情页面

### W6-FE-REACT-RTE-1 富文本编辑器集成
- Owner: TBD | Estimate: 1.0d | Deps: -
- Checklist:
  - [ ] 编辑器接入与内容校验/清洗
  - [ ] 图片上传占位（后端上传服务留空）
- Acceptance: 可发布/编辑正文，XSS 基础防护
- Deliverables: 编辑器组件与发布页面

### W6-FE-REACT-COMMENT-1 评论与回复
- Owner: TBD | Estimate: 1.0d | Deps: W6-FE-REACT-DETAIL-1
- Checklist:
  - [ ] 评论列表分页（扁平或楼中楼）
  - [ ] 发表评论与回复流程
- Acceptance: 评论交互顺畅，状态与计数同步
- Deliverables: 评论组件/页面

### W6-FE-REACT-LIKE-1 点赞/取消点赞
- Owner: TBD | Estimate: 0.5d | Deps: -
- Checklist:
  - [ ] 端点调用与乐观更新
  - [ ] 防抖/节流，避免刷接口
- Acceptance: 点赞状态与数值正确
- Deliverables: 点赞逻辑与接口封装

### W6-TEST-1 E2E 冒烟（可选 Playwright）
- Owner: TBD | Estimate: 0.75d | Deps: W6 完成
- Checklist:
  - [ ] 登录-发帖-评论-点赞链路
  - [ ] 常见负例
- Acceptance: 冒烟用例通过
- Deliverables: 测试脚本与报告

### W6-DOC-1 文档与限制清单
- Owner: TBD | Estimate: 0.25d | Deps: W6 完成
- Checklist:
  - [ ] 使用说明与已知限制
- Acceptance: 文档可指导演示
- Deliverables: 文档更新

---

## Week 7｜Angular 管理端 MVP（W7）

### W7-FE-ANG-CAT-1 分类管理（CRUD/排序/启停）
- Owner: TBD | Estimate: 1.0d | Deps: W3-BE-3
- Checklist:
  - [ ] 列表/新建/编辑/删除
  - [ ] 排序与启停
- Acceptance: 管理功能闭环
- Deliverables: 分类管理界面与服务

### W7-FE-ANG-POST-1 帖子审核与状态管理
- Owner: TBD | Estimate: 1.0d | Deps: W3-BE-3
- Checklist:
  - [ ] 发布/草稿/置顶/精华
  - [ ] 搜索/筛选
- Acceptance: 审核流程与状态切换正确
- Deliverables: 帖子管理界面

### W7-FE-ANG-COMMENT-1 评论审核
- Owner: TBD | Estimate: 0.5d | Deps: W3-BE-3
- Checklist:
  - [ ] 审核/删除/恢复
- Acceptance: 常见操作可用
- Deliverables: 评论管理界面

### W7-FE-ANG-PERM-1 权限联动与角色映射
- Owner: TBD | Estimate: 0.5d | Deps: W2-BE-2
- Checklist:
  - [ ] 角色-权限匹配
  - [ ] UI 控制按权限显隐
- Acceptance: 权限闭环正确
- Deliverables: 权限配置与界面联动

### W7-TEST-1 Angular 单元/端到端
- Owner: TBD | Estimate: 0.75d | Deps: W7 完成
- Checklist:
  - [ ] 关键组件单测
  - [ ] e2e 核心路径
- Acceptance: 测试通过
- Deliverables: 测试与报告

### W7-DOC-1 管理端使用说明
- Owner: TBD | Estimate: 0.25d | Deps: W7 完成
- Checklist:
  - [ ] 用户指南与截图
- Acceptance: 文档清晰
- Deliverables: 文档更新

---

## Week 8｜容器化与一键启动（W8）

### W8-OPS-DCKR-1 Host Dockerfile（多阶段）
- Owner: TBD | Estimate: 0.75d | Deps: -
- Checklist:
  - [ ] 多阶段构建与运行时镜像瘦身
  - [ ] 证书/环境变量与 `ASPNETCORE_URLS`
- Acceptance: 镜像可独立运行
- Deliverables: Host Dockerfile 与说明

### W8-OPS-DCKR-2 DbMigrator 一次性容器
- Owner: TBD | Estimate: 0.5d | Deps: -
- Checklist:
  - [ ] 启动等待 Mongo，幂等执行
- Acceptance: 先于 API 正常完成
- Deliverables: DbMigrator Dockerfile 与 entrypoint

### W8-OPS-DCKR-3 React 构建与 Nginx 托管
- Owner: TBD | Estimate: 0.75d | Deps: -
- Checklist:
  - [ ] 生产构建与 Nginx 配置
- Acceptance: 前端容器可访问
- Deliverables: React Dockerfile 与 nginx.conf

### W8-OPS-DCKR-4 Angular 构建与 Nginx 托管
- Owner: TBD | Estimate: 0.75d | Deps: -
- Checklist:
  - [ ] 生产构建与 Nginx 配置
- Acceptance: 管理端容器可访问
- Deliverables: Angular Dockerfile 与 nginx.conf

### W8-OPS-COMP-1 docker-compose.yml 编排
- Owner: TBD | Estimate: 0.75d | Deps: 上述镜像
- Checklist:
  - [ ] mongodb/backend-api/db-migrator/react-frontend/angular-admin
  - [ ] 端口/卷/网络/depends_on/env 与健康检查
- Acceptance: `docker-compose up --build -d` 一键拉起
- Deliverables: compose 文件与环境示例

### W8-TEST-1 一键启动自测
- Owner: TBD | Estimate: 0.5d | Deps: W8-OPS-COMP-1
- Checklist:
  - [ ] 全链路冒烟：DB→迁移→API→前端
- Acceptance: 正常运行无报错
- Deliverables: 自测记录

### W8-DOC-1 Docker 部署说明
- Owner: TBD | Estimate: 0.25d | Deps: W8 完成
- Checklist:
  - [ ] 启动/停止/清理/常见问题
- Acceptance: 文档可指导部署
- Deliverables: 文档更新

---

## Week 9｜积分系统（W9）

### W9-BE-DOM-1 积分领域建模
- Owner: TBD | Estimate: 1.0d | Deps: W2 完成
- Checklist:
  - [ ] `UserPoints`、`PointTransaction` 与 `PointCalculator`
- Acceptance: 模型与不变式完备
- Deliverables: 领域代码与注释

### W9-BE-EVT-1 事件订阅与积分变更
- Owner: TBD | Estimate: 0.75d | Deps: W9-BE-DOM-1
- Checklist:
  - [ ] 订阅 Post/Comment 相关事件并更新积分
- Acceptance: 事件触发→积分更新正确
- Deliverables: 事件处理器与日志

### W9-BE-APP-1 积分应用服务与 API
- Owner: TBD | Estimate: 0.75d | Deps: W9-BE-DOM-1
- Checklist:
  - [ ] 查询当前积分与积分历史
- Acceptance: Swagger 可调试
- Deliverables: AppService 与契约

### W9-FE-ANG-1 管理端用户积分管理
- Owner: TBD | Estimate: 0.75d | Deps: W9-BE-APP-1
- Checklist:
  - [ ] 查询/调整/历史
- Acceptance: 管理功能可用
- Deliverables: Angular 页面

### W9-FE-REACT-1 个人中心积分与历史
- Owner: TBD | Estimate: 0.75d | Deps: W9-BE-APP-1
- Checklist:
  - [ ] 个人中心显示积分
  - [ ] 历史记录列表
- Acceptance: 展示正确、分页正常
- Deliverables: React 页面

### W9-TEST-1 积分链路测试
- Owner: TBD | Estimate: 0.75d | Deps: W9 完成
- Checklist:
  - [ ] 事件→积分变更单测/集成
- Acceptance: `dotnet test` 通过
- Deliverables: 测试与报告

### W9-DOC-1 积分文档
- Owner: TBD | Estimate: 0.25d | Deps: W9 完成
- Checklist:
  - [ ] 规则说明与接口示例
- Acceptance: 文档可指导使用
- Deliverables: 文档更新

---

## Week 10｜商城系统（W10）

### W10-BE-DOM-1 商城领域建模
- Owner: TBD | Estimate: 1.0d | Deps: W9 完成
- Checklist:
  - [ ] `ShopItem`、`UserInventory` 与 `ShopManager`
- Acceptance: 模型与流程清晰
- Deliverables: 领域代码

### W10-BE-APP-1 商城应用服务与 API
- Owner: TBD | Estimate: 0.75d | Deps: W10-BE-DOM-1
- Checklist:
  - [ ] 商品列表/详情/购买/激活/禁用/库存
- Acceptance: Swagger 可调试
- Deliverables: AppService 与契约

### W10-FE-ANG-1 管理端商品与库存管理
- Owner: TBD | Estimate: 0.75d | Deps: W10-BE-APP-1
- Checklist:
  - [ ] 商品 CRUD/上下架/价格
  - [ ] 用户库存查询/调整
- Acceptance: 管理功能可用
- Deliverables: Angular 页面

### W10-FE-REACT-1 前端商城与我的物品
- Owner: TBD | Estimate: 1.0d | Deps: W10-BE-APP-1
- Checklist:
  - [ ] 商城列表与详情
  - [ ] 购买/我的物品/激活与效果应用（头像框/昵称色/签名尾巴）
- Acceptance: 购买→库存→效果激活闭环
- Deliverables: React 页面与样式

### W10-TEST-1 购买链路与异常
- Owner: TBD | Estimate: 0.75d | Deps: W10 完成
- Checklist:
  - [ ] 积分不足/重复购买等异常
- Acceptance: 规则与错误返回正确
- Deliverables: 测试与报告

### W10-DOC-1 商城文档
- Owner: TBD | Estimate: 0.25d | Deps: W10 完成
- Checklist:
  - [ ] 流程图与使用说明
- Acceptance: 文档可指导使用
- Deliverables: 文档更新

---

## Week 11｜可观测性/性能/安全（W11）

### W11-OPS-LOG-1 结构化日志与关联标识
- Owner: TBD | Estimate: 0.5d | Deps: -
- Checklist:
  - [ ] 统一日志格式与请求关联 ID
- Acceptance: 日志便于排障与溯源
- Deliverables: 日志配置与示例

### W11-OPS-HEALTH-1 健康检查与探针
- Owner: TBD | Estimate: 0.5d | Deps: -
- Checklist:
  - [ ] liveness/readiness 端点
- Acceptance: Compose/K8s 可探活
- Deliverables: 健康检查配置

### W11-BE-PERF-1 查询与索引优化
- Owner: TBD | Estimate: 1.0d | Deps: -
- Checklist:
  - [ ] 查询计划核对、投影与分页优化
- Acceptance: 关键接口 P95 改善
- Deliverables: 优化记录与指标

### W11-BE-CACHE-1 缓存与失效策略
- Owner: TBD | Estimate: 0.5d | Deps: -
- Checklist:
  - [ ] 失效/刷新/并发雪崩保护
- Acceptance: 缓存稳定、无一致性问题
- Deliverables: 策略与监测

### W11-SEC-1 安全增强
- Owner: TBD | Estimate: 1.0d | Deps: -
- Checklist:
  - [ ] 输入验证/XSS/CSRF/限流
  - [ ] 角色最小权限模板
- Acceptance: 安全扫描通过基本项
- Deliverables: 配置与文档

### W11-TEST-1 性能与安全基线
- Owner: TBD | Estimate: 0.75d | Deps: W11 完成
- Checklist:
  - [ ] 压测脚本与报告
  - [ ] 基础安全检查清单
- Acceptance: 指标达标或有行动项
- Deliverables: 报告

### W11-DOC-1 指标与阈值文档
- Owner: TBD | Estimate: 0.25d | Deps: W11 完成
- Checklist:
  - [ ] 定义关键指标与阈值
- Acceptance: 文档形成共识
- Deliverables: 文档更新

---

## Week 12｜硬化收尾与交付（W12）

### W12-TEST-1 覆盖率与关键路径补测
- Owner: TBD | Estimate: 1.0d | Deps: -
- Checklist:
  - [ ] 单元/集成/E2E 覆盖关键场景
- Acceptance: 回归稳定，关键路径稳定
- Deliverables: 报告与追踪

### W12-DOC-1 文档与交付物
- Owner: TBD | Estimate: 0.75d | Deps: -
- Checklist:
  - [ ] 开发指南/联调/部署/权限矩阵/数据字典/架构说明
- Acceptance: 文档齐备可交付
- Deliverables: 文档清单

### W12-REL-1 版本与里程碑验收
- Owner: TBD | Estimate: 0.5d | Deps: -
- Checklist:
  - [ ] CHANGELOG/版本号/里程碑记录
- Acceptance: 验收通过
- Deliverables: 版本与记录

### W12-CLEAN-1 收尾清理与风格统一
- Owner: TBD | Estimate: 0.5d | Deps: -
- Checklist:
  - [ ] 删除无用代码/TODO 清理/格式化
- Acceptance: 代码整洁一致
- Deliverables: 清理提交

### W12-DEMO-1 演示素材
- Owner: TBD | Estimate: 0.5d | Deps: -
- Checklist:
  - [ ] Demo 脚本、截图/录屏、示例数据
- Acceptance: 可对外演示
- Deliverables: 演示资产
