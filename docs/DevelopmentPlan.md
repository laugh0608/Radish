# 开发路线图（按周计划）

> 本计划依据当前仓库目录结构（Radish.Application*、Radish.Domain*、Radish.HttpApi*、Radish.HttpApi.Host、Radish.MongoDB、Radish.DbMigrator）与文档约定（.env 管理、CORS、React/Angular、MongoDB、OpenIddict、Docker 等）制订；与 docs/DevelopmentFramework.md 的总体架构一致，但聚焦“按周可交付”。

相关文档
- 开发进度日志: [DevelopmentProgress.md](DevelopmentProgress.md)

## 目标与范围

- 基线：.NET 9 + ABP，MongoDB，OpenIddict + JWT，Swagger/Scalar，React（Vite）与 Angular（ABP UI）。
- 统一配置：后端以 `.env` 为主；CORS 通过 `App__CorsOrigins`；前端本地默认 HTTP，Host 统一 HTTPS；证书位于 `dev-certs/`。
- 交付标准：每周具备可演示的闭环（后端 API + 至少一端 UI），并同步更新最小文档与测试。

## 按周计划（12 周）

### 第 1 周｜基线与联调打通
- 后端：准备 `src/Radish.HttpApi.Host/.env` 与 `src/Radish.DbMigrator/.env`；运行 DbMigrator；Host 启动并打印 CORS 允许来源。
- 前端：React/Angular 本地可运行；React 配置 `VITE_API_BASE_URL=https://localhost:44342`；Angular 使用 `dynamic-env.json` 联调。
- 文档与测试：补充本地联调与证书自测（curl 预检、dotnet dev-certs）；新增最小健康检查测试；确保 `dotnet build/test` 全绿。
- 验收：Swagger/Scalar 可打开并访问；React/Angular 能请求应用配置并通过 CORS。

### 第 2 周｜领域建模与数据层（Category/Post/Comment）
- 领域：在 `Radish.Domain` 定义聚合/实体/值对象与不变式；在 `Radish.Domain.Shared` 维护本地化键与权限常量。
- 数据：在 `Radish.MongoDB` 实现仓储与必要索引（按分类/创建时间/状态等）；DbMigrator 增加默认数据与索引。
- 测试：聚合与仓储单测（Shouldly + NSubstitute）。
- 验收：CRUD 与分页查询可用；索引生效；测试通过。

### 第 3 周｜应用层与 API（Category/Post/Comment）
- 合同：`Radish.Application.Contracts` 定义 DTO 与接口（分页/过滤/排序模型）。
- 服务：`Radish.Application` 实现权限检查、流程编排与事件发布（发帖、点赞、浏览等）。
- API：Host 暴露 RESTful 接口；Swagger/Scalar 分组与文档过滤（仅项目 API）。
- 验收：授权后可在 Swagger/Scalar 调试完整接口；关键操作带权限校验。

### 第 4 周｜Host 网关与文档硬化
- 身份：完善 OpenIddict 配置与数据种子（Swagger/Scalar ClientUri/RedirectUri 正确）。
- 文档：Swagger/Scalar 登录授权流程打通；统一 API 版本与分组；异常映射为 ProblemDetails。
- 性能：为高频查询添加短缓存策略（只读列表等）。
- 验收：Swagger/Scalar 授权可调试；异常/权限响应一致；热点接口可观测。

### 第 5 周｜React 基础与认证集成
- 架构：全局状态（Auth + I18n + Query/Cache）、路由守卫、基础布局与主题。
- 认证：登录/注册/刷新令牌；按权限动态控制入口/按钮。
- 页面：首页/分类页、帖子列表（分页/过滤/排序）。
- 验收：登录后可浏览列表并正确显示状态；无权限按钮隐藏。

### 第 6 周｜React 论坛核心交互
- 详情：帖子详情（内容/作者/时间/计数）；评论区（扁平或楼中楼）。
- 交互：新发帖/编辑帖（富文本）、评论/回复、点赞/取消点赞；错误/空态/骨架屏。
- 质量：端到端冒烟（可选 Playwright）。
- 验收：发帖-评论-点赞路径闭环；核心 UX 可用。

### 第 7 周｜Angular 管理端（MVP）
- 分类管理：列表/排序/启停/编辑；权限联动。
- 内容管理：帖子与评论搜索/筛选/审核（发布/草稿、置顶、精华）。
- 联调：与 Host 权限与文案一致；移动端规范复用（如需）。
- 验收：管理操作可用；权限与 UI 控制一致。

### 第 8 周｜容器化与一键启动
- Dockerfile：Host、DbMigrator、React、Angular；前端使用 Nginx 承载产物。
- Compose：`mongodb`、`backend-api`、`db-migrator`、`react-frontend`、`angular-admin` 的网络/卷/端口/依赖与环境变量。
- 文档：`docker-compose up --build -d` 自测与常见问题；健康检查。
- 验收：一键启动后链路（DB→迁移→API→前端）完整可用。

### 第 9 周｜积分系统
- 领域：`UserPoints`、`PointTransaction` 与 `PointCalculator`。
- 事件：订阅发帖、点赞、评论被采纳等事件更新积分。
- 应用/UI：`PointAppService`；Admin 用户积分管理；React 个人中心展示与积分历史。
- 验收：事件→积分变更链路可用；前后台一致。

### 第 10 周｜商城系统
- 领域：`ShopItem`、`UserInventory`；`ShopManager` 购买/激活/禁用（扣减积分）。
- 应用/UI：商品列表/详情/购买；我的物品；效果应用（头像框/昵称色/签名尾巴）。
- 验收：购买-库存-效果激活闭环；积分扣减准确。

### 第 11 周｜可观测性、性能与安全
- 观测：结构化日志、健康检查、基础指标；关键 API 耗时与错误率观测。
- 性能：索引优化、缓存策略、分页与投影；Mongo 查询计划复核。
- 安全：输入验证、XSS/CSRF、防刷/限流、CORS 白名单审计；最小权限角色模板。
- 验收：关键页面 P95 ≤ 200ms（本地/容器）；无高风险扫描项。

### 第 12 周｜硬化收尾与文档交付
- 测试：单元/集成/端到端覆盖补全；核心路径稳定。
- 文档：开发指南、联调说明、Docker 部署、权限矩阵、数据字典与架构说明。
- 准入：版本清单、CHANGELOG、里程碑验收记录；后续 Roadmap（搜索、消息、上传、SEO、i18n 等）。
- 验收：文档齐备、测试通过、可演示与可部署。

## 每周交付与验收标准
- 源码与可运行产物：后端 API 与至少一端 UI 的功能闭环。
- 文档：更新 `docs/`（含 DevelopmentProgress.md）；必要的配置样例（.env.example、CORS、自测命令）。
- 测试：`dotnet test` 通过；前端 lint/build 通过；必要端到端冒烟。
- 验收：Swagger/Scalar 授权调试关键接口；权限控制与错误处理就绪；容器化阶段具备一键启动。

## 并行与协作建议
- 前后协同：应用合同（DTO/接口）一经稳定即同步给前端；提供 Swagger JSON 用于前端 Mock。
- 并行推进：React 与 Angular 可并行；Docker 编排可提前孵化并逐周集成。
- 规范与本地化：新增权限/词条统一在 `Radish.Domain.Shared` 维护；变更同步至 docs/README.md 索引。

## 风险与前置条件
- 环境：.NET 9 SDK、Node 18/20、MongoDB 6+；本地信任 HTTPS 证书。
- 配置：后端 `.env` 为唯一可信来源（关键项必须给出）；CORS 需包含两端口与 http/https 协议。
- 安全：仓库不提交机密；仅提交 `.env.example`；生产另行配置证书与密钥。

## 变更记录
- 2025-10-29：创建文档，首次纳入 12 周按周计划；与 DevelopmentFramework.md 对齐并使用 Radish.* 命名。

---

## 执行看板（任务拆分）

> 看板聚焦“执行级任务”，与上文的按周目标互补：
> - 按周目标：见本文上方“按周计划（12 周）”章节
> - 进度/日志：见 [DevelopmentProgress.md](DevelopmentProgress.md)

快速入口
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

### Week 1｜基线与联调打通（W1）

#### W1-OPS-1 本地证书与环境基线
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

#### W1-BE-1 Host/DbMigrator .env 初始化
- Owner: TBD | Estimate: 0.5d | Deps: W1-OPS-1
- Checklist:
  - [ ] 复制 `src/Radish.HttpApi.Host/.env.example` → `.env` 并配置：
        `ConnectionStrings__Default`、`App__CorsOrigins`
  - [ ] 复制 `src/Radish.DbMigrator/.env.example` → `.env` 与 Host 对齐
  - [ ] 启动 Host 打印 CORS 允许来源（含 http/https、5173/4200）
- Acceptance: 启动日志显示“CORS allowed origins: …”并与预期匹配
- Deliverables: `.env` 文件（不入库）、日志片段贴入 DevelopmentProgress.md

#### W1-BE-2 DbMigrator 运行与基础种子
- Owner: TBD | Estimate: 0.5d | Deps: W1-BE-1
- Checklist:
  - [ ] `cd src/Radish.DbMigrator && dotnet run`
  - [ ] 确认初始数据/索引（若有）执行完成
- Acceptance: 迁移完成、异常为零；重复运行幂等
- Deliverables: 迁移日志片段、如有新增索引/种子在文档标注

#### W1-FE-REACT-1 React 本地联调
- Owner: TBD | Estimate: 0.5d | Deps: W1-BE-1
- Checklist:
  - [ ] `react/.env.local` 设置 `VITE_API_BASE_URL=https://localhost:44342`
  - [ ] `npm run dev` 启动并访问首页
  - [ ] 调用 `/api/abp/application-configuration` 成功
- Acceptance: 控制台无 CORS 错误；能读取本地化/认证配置
- Deliverables: 截图与步骤简记（可补到 React 指南）

#### W1-FE-ANG-1 Angular 本地联调
- Owner: TBD | Estimate: 0.5d | Deps: W1-BE-1
- Checklist:
  - [ ] `angular` 目录 `npm install && npm run start`
  - [ ] 验证动态环境加载；访问主页面正常
  - [ ] 调用 `/api/abp/application-configuration` 成功
- Acceptance: 控制台无 CORS 错误；能读取本地化/认证配置
- Deliverables: 截图与步骤简记（可补到 Angular 指南）

#### W1-TEST-1 基线测试与健康检查
- Owner: TBD | Estimate: 0.5d | Deps: W1-BE-1
- Checklist:
  - [ ] `dotnet build`、`dotnet test` 全绿
  - [ ] 为 Host 增加最小健康检查用例或手工 curl 验证
- Acceptance: CI 本地通过；健康检查 200/204
- Deliverables: 测试通过记录、必要的测试代码（如有）

#### W1-DOC-1 文档对齐与自测命令
- Owner: TBD | Estimate: 0.5d | Deps: W1-BE-1, W1-FE-REACT-1, W1-FE-ANG-1
- Checklist:
  - [ ] 在 `docs/DevelopmentProgress.md` 记录联调结论与端口/协议
  - [ ] 如有偏差，补充 `docs/backend/README.md` 的 CORS/预检命令
- Acceptance: 文档与现状一致；新人可按文档直接跑通
- Deliverables: 文档更新 PR（本地提交）

## 扩展专题：端到端加密聊天/帖子（E2EE）

> 目标：为论坛提供“加密帖子”和“加密聊天室/私信”的端到端加密（E2EE）能力，支持给用户生成密钥对（默认前端生成私钥并仅本地保存），发布内容以收件人公钥加密，使“仅指定用户或持有私钥者”可解密阅读。服务端对明文零知，仅负责密文存储与访问控制。该能力通过 Feature Flag 渐进式上线。

### 范围与约束
- 范围：后端（ABP 分层）+ React/Angular 前端 + 密钥管理流程与备份。
- 加密算法建议：
  - 内容加密：AES-256-GCM（WebCrypto 可用）。
  - 密钥封装：ECIES/非对称封装（P-256 或 Curve25519；如受限则使用 RSA-OAEP-256）。
  - 口令派生：PBKDF2-HMAC-SHA256（优先 Argon2id，如可用）。
- 兼容性：优先使用浏览器 WebCrypto SubtleCrypto；不支持场景回退为“明文/仅 ACL”。
- 搜索/审核：密文不可全文检索；仅依赖元信息与用户主动举报快照；可选“监管密钥”仅在测试环境启用。

### 里程碑与任务

#### E2EE-1 密钥基础设施（用户密钥对）
- Owner: TBD | Estimate: 2d | Deps: 登录/用户资料
- Checklist:
  - [ ] 前端首次开启 E2EE 时生成密钥对（KeyId、算法、创建时间），私钥仅本地保存（IndexedDB/Keychain）。
  - [ ] 可选备份：以用户口令派生密钥对私钥进行 AES-GCM 加密后上传备份（服务端仅存密文）。
  - [ ] 后端新增实体 `UserEncryptionKey`：`UserId`、`KeyId`、`Algorithm`、`PublicKey`、`CreatedAt`、`RevokedAt`。
  - [ ] 开放 API：发布/撤销公钥、按 `UserId` 查询公钥列表与当前 `KeyId`。
  - [ ] 密钥轮换：允许并行多把公钥，标记当前生效；撤销旧 KeyId。
- Acceptance: 用户可生成并发布公钥；服务端可查询；轮换/撤销流程可用。

#### E2EE-2 加密帖子（Recipients 白名单）
- Owner: TBD | Estimate: 2d | Deps: E2EE-1
- Checklist:
  - [ ] Domain：在 `Post` 增加 `IsEncrypted`、`Ciphertext`、`EncryptionMeta`（含 `alg`、`keyId`、`recipients` 数组：`[{ userId, keyId, wrappedKey }]`、`iv`、`tag`）。
  - [ ] 应用层：发布加密帖时，前端生成随机 `contentKey`（对称），对每位收件人公钥做密钥封装（wrappedKey），提交密文与元信息；服务端仅做 ACL/元信息校验与存储。
  - [ ] 读取：前端拉取密文与自己的 `wrappedKey`，以本地私钥解封装 `contentKey` 并解密正文；失败时给出恢复/重试指引。
  - [ ] UI：发帖支持“加密发布”开关与收件人选择；列表标示“加密”。
- Acceptance: 白名单内用户可解密阅读；非收件人仅见占位/受限提示；服务端不触达明文。

#### E2EE-3 加密聊天室/私信（群聊密钥）
- Owner: TBD | Estimate: 3d | Deps: E2EE-1
- Checklist:
  - [ ] 结构：聊天室维度维护 `RoomKey`（对称）及其轮换序列；为每位成员保存一份 `wrappedKey`（基于成员公钥封装）。
  - [ ] 成员变更：新成员加入时为其封装当前 `RoomKey` 或触发轮换；成员退出/移除触发轮换，使其无法解密新消息。
  - [ ] 消息：每条消息使用当前 `RoomKey` + AES-GCM 加密；服务器仅保存密文与必要元信息（`roomKeyVersion`）。
  - [ ] 前端：加入/退出/轮换的自动重协商与提示；离线后重连恢复。
- Acceptance: 群聊仅成员可读；成员变更后前成员无法解密新消息；消息解密无明显卡顿。

#### E2EE-4 备份与恢复（私钥/密钥包）
- Owner: TBD | Estimate: 1d | Deps: E2EE-1
- Checklist:
  - [ ] 导出加密备份：以口令派生密钥加密私钥包（含 KeyId/算法/指纹），本地下载或安全云存储。
  - [ ] 导入恢复：在新设备导入备份后验证指纹并恢复解密能力。
  - [ ] 安全提示：遗失口令或备份将无法恢复历史密文（设计即如此）。
- Acceptance: 新设备可通过备份恢复并成功解密历史加密帖/消息。

#### E2EE-5 合规与运营开关
- Owner: TBD | Estimate: 1d | Deps: E2EE-2/E2EE-3
- Checklist:
  - [ ] Feature Flag：`App__Features__E2EE`（默认关闭，仅测试环境开启）。
  - [ ] 可选“监管密钥”（测试环境）：为指定监管账户附加一份 `wrappedKey`，用于问题排查；生产默认关闭。
  - [ ] 日志与隐私：避免在日志/埋点中泄露任何明文或密钥材料。
- Acceptance: 在不同环境下开关生效；关闭监管时平台无法解密内容。

### ABP/实体/接口改动点（梳理）
- Domain（Radish.Domain*）：
  - `UserEncryptionKey` 聚合/实体。
  - `Post`、`Message` 增加加密相关字段（不改变明文帖/消息存储结构）。
- Application（Radish.Application*）：
  - 公钥发布/撤销/查询 AppService。
  - 加密帖发布/读取、聊天室密钥轮换/成员管理应用服务。
- HttpApi（Radish.HttpApi*）：
  - 公钥 API、加密帖/消息读写 API（仅返回密文与元数据）。
- MongoDB（Radish.MongoDB）：
  - 索引：`UserEncryptionKey.UserId`、`KeyId`；`Post`/`Message` 的加密标识与时间序列优化。
- 前端（react/、angular/）：
  - 密钥生成/存储（IndexedDB）、备份/恢复、解密渲染与错误处理；UI 开关与指纹展示。

### 风险与缓解
- 私钥丢失：不可恢复历史内容；提供备份与二次确认提示。
- 浏览器兼容：优先使用 WebCrypto；低版本回退为“不可用/提示升级”。
- 搜索与审核：密文不可检索；辅以标签/元信息与用户主动举报快照；可选测试环境监管密钥。
- 性能：大内容建议分块或只对正文加密；密钥封装对收件人数线性增长，限制最大收件人数或采用群聊密钥。

### 交付物与验证
- API 文档（Swagger/Scalar 分组：E2EE）。
- 前端演示：发加密帖、解密渲染、群聊密钥轮换演示。
- 自动化：核心加解密与元信息校验的单测；端到端冒烟（可选）。
