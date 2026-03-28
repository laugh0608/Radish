# 2026-03 第四周 (03-23 至 03-29)

## 2026-03-23 (周一)

### 共享 Toast 自动消失进度条增强

- **`@radish/ui` 共享 `Toast` 已补剩余时间进度条**：当 `duration > 0` 时，通知底部会显示与自动关闭时长同步收缩的进度条，用于提示用户该提示还有多久会自动消失。
- **退出动画已修正为“先淡出，再移除”**：原有 `Toast` 在进入关闭状态时会直接从 React 树移除，导致 `fadeOut` 样式实际上无法生效；当前已改为先进入 `fadeOut`，再在 300ms 动画结束后统一回收。
- **共享能力直接覆盖现有调用点**：`radish.client` 与 `radish.console` 里现有 `toast.success / error / info / warning / custom` 调用无需逐页修改，即可自动获得剩余时间可视反馈。

### 文档口径同步

- **组件入口文档已补齐 `Toast` / 通知能力说明**：`frontend/ui-library.md` 已从仅列旧版基础组件，更新为包含 `Toast` 与 `Notification` 系列的当前高频组件口径。
- **通知规划文档已同步当前交互现状**：`notification-realtime.md` 已明确共享 `Toast` 当前已支持“自动消失 + 剩余时间进度条”，而“点击跳转 / 手动关闭”仍保留为后续可选增强，不再继续以“完全未定”描述。
- **通知前端集成文档已补当前实现说明**：`notification-frontend.md` 已明确标注为“早期设计稿 + 当前实现入口”，并补充首版联调时应重点关注的未读数、删除、跳转与多端同步检查项。

### 通知中心首版联调补丁

- **点击通知改为静默已读**：从通知列表点击进入业务页时，不再额外弹出“标记已读成功”提示，避免阅读通知本身时被二次提示打断。
- **删除未读通知后补服务端未读数同步**：通知应用删除通知成功后，当前会重新同步服务端未读数量，降低“列表已删但 Dock 角标仍旧滞后”的口径漂移风险。
- **实时新通知已接入全局 Toast 预览**：`notificationHub` 收到 `NewNotification` 后，当前会弹出标题 + 内容摘要的轻量 Toast，便于在不打开通知中心的情况下感知新消息到达。
- **通知前端文档已补 Smoke 顺序**：`notification-frontend.md` 已新增通知中心最小人工验收顺序，覆盖实时接收、Toast 预览、Dock 未读角标、列表、已读 / 删除与多端同步。

### 通知中心真实 Smoke 结果

- **用户已完成一轮真实首版 Smoke**：覆盖新通知 Toast 预览、Dock 未读角标、通知列表显示、点击跳转并静默已读、删除后角标同步与多端同步。
- **验收反馈已明确通过**：用户于 `2026-03-23` 直接确认“都通过了，没问题”。
- **规划口径已同步收口**：通知中心当前从“待联调复核”转入“等待总回归确认”，后续以回归维护为主，不继续扩张首版交互范围。

### 欢迎 App 长文案 i18n 收口

- **欢迎页主体内容已完成双语资源化**：平台概览、上手路径、社区约定与开源说明均已切入 `i18n`，语言切换不再只覆盖桌面壳层与短标签。
- **长文案已从组件和数据文件中抽离**：当前欢迎页文案已收束到独立翻译资源模块，欢迎应用组件层只保留结构、图标和状态，不再散落大段硬编码文本。
- **欢迎页构建验证已通过**：本轮 `radish.client` 类型检查与系统环境构建均已通过，可作为规划口径中“欢迎页长文案已进入 i18n”的现实依据。

### 认证 / OIDC / Gateway 基础入口真实 Smoke 结果

- **用户已完成一轮真实首版 Smoke**：覆盖 `radish-client / radish-console / radish-scalar` 的登录、回调、登出，以及 `Gateway / Scalar` 入口打开与基础授权链路确认。
- **验收反馈已明确通过**：用户于 `2026-03-23` 直接确认“都通过，测试了没问题”。
- **规划口径已同步收口**：认证 / OIDC / Gateway 基础入口当前从“待联调复核”转入“等待总回归确认”，后续以回归维护为主，不继续扩张首版范围。

### 本轮验证

- ✅ `npm run type-check --workspace=@radish/ui` 通过。
- ✅ `npm run build --workspace=radish.client` 已在系统环境构建通过。
- ✅ 通知中心真实首版 Smoke 已完成并通过。
- ✅ 欢迎 App 长文案 i18n 已完成并通过类型检查 / 构建验证。
- ✅ 认证 / OIDC / Gateway 基础入口真实首版 Smoke 已完成并通过。

## 2026-03-24 (周二)

### Console 入口权限与后台模块补边界

- **`console.access` 已从“单独可见”收口为“入口标记 + 真实页面权限联动”**：普通角色现在必须同时具备 `console.access` 和至少一个真实 Console 页面访问权限才会看到并允许进入 Console；后端权限快照也会自动补齐或剔除入口权限，避免脏授权。
- **分类与标签后台已明确拆分**：`radish.console` 当前分别提供 `/categories` 与 `/tags` 两个独立管理页面，分类管理已支持分页、新增、编辑、启停、排序、软删除与恢复，权限键、资源映射与种子也已与标签彻底拆开。
- **内容治理 / 胡萝卜 / 经验等级后台首版已纳入 Console**：当前已补 `Moderation`、`Coins`、`Experience` 三组页面、权限常量、路由与后端资源映射；治理后台明确采用“集成在 Console，不拆独立 App”的方案。
- **默认 Test 角色已收口为普通用户基线**：`DbMigrate` 当前会回收 `Test` 角色全部 `RoleConsoleResource`，并回收除 `GetUserByHttpContext` 外的多余 API 权限，避免测试用户误见 Console 或误拥有标签管理等后台能力。
- **旧开发库需注意历史脏授权残留**：若本地开发数据库已保留旧的 `RoleConsoleResource / RoleModulePermission` 记录，需要重新执行种子或手工清理后再验证 `Test` 角色与 Console 入口收口结果。
- **内容治理举报对象已补齐到四类**：当前审核台已统一接入 `Post / Comment / ChatMessage / Product` 举报链路，前台帖子详情、评论区、聊天室消息与商品详情也都已提供举报入口。
- **Console 菜单图标已补齐**：`内容治理`、`胡萝卜管理`、`经验等级` 菜单当前已分别补上图标，侧栏信息层级更完整。

### 社区资料与论坛详情展示修复

- **公开个人主页已改为真实拉取公开资料**：查看其他用户主页时，前端不再只依赖窗口参数，而是通过公开资料接口拉取头像、昵称、加入时间等信息，并恢复关注 / 取消关注入口。
- **论坛帖子详情与评论头像已补齐**：帖子详情当前会对单帖回填作者头像与问答回答作者头像；评论头像已改为优先使用真实附件 URL，坏图时再回退兜底头像。
- **关系链头像与关注通知已补齐**：粉丝 / 关注列表已改为展示真实头像；用户被关注时会新增“新增粉丝”通知。
- **聊天室自己消息样式已调整为镜像布局**：当前用户消息已改为与左侧消息对称排布，头像位于右侧边缘，用户名 / 时间 / 回复 / 撤回位于头像与消息气泡之间。
- **经验查询边界已补一层收口**：`GetMyExperience` 现仅允许查询自己，Console 需要查看其他用户经验时改走 `GetUserExperience/{userId}`。

### 文档同步

- **Console 文档已更新当前入口判定与覆盖矩阵**：`console-system`、`console-permission-governance`、`console-permission-coverage-matrix` 已同步 `console.access` 新语义、`Test` 角色普通用户基线，以及 `Moderation` 四类举报对象范围。
- **论坛文档已同步当前实现口径**：论坛功能文档已补齐“帖子详情 / 评论 / 粉丝列表真实头像”“关注通知”“治理对象扩展到聊天室消息与商品”等说明。

### 本轮验证

- ✅ `npx tsc -b Frontend/radish.console` 通过。
- ✅ `npx tsc -b Frontend/radish.client` 通过。
- ⚠️ `npm run build --workspace=radish.console` 已通过 `tsc -b`，但 `vite build` 在当前沙盒环境下因 `spawn EPERM` 中断。
- ⚠️ `dotnet build` 已通过首用目录权限问题，但在当前沙盒内仍出现无编译错误的 MSBuild 异常退出，暂未拿到可用于判定代码错误的有效失败信息。

## 2026-03-25 (周三)

### 首版最小 Docker 资产补齐

- **已补齐四个首版镜像入口**：当前仓库已新增 `Radish.Auth/Dockerfile`、`Radish.Gateway/Dockerfile`、`Frontend/Dockerfile`，并对 `Radish.Api/Dockerfile` 做了可构建性收口，首版最小镜像链不再停留在“只有 API Dockerfile”的状态。
- **前端容器已收口为“单镜像双前端”最小方案**：`Frontend/Dockerfile` 当前会在构建阶段安装 Node 24，统一构建 `radish.client` 与 `radish.console`，最终由 `Frontend/scripts/serve-static.mjs` 同时托管 `/` 与 `/console/`，避免首版阶段再额外引入 Nginx / SSR / 多前端容器复杂度。
- **最小 Compose 已落地**：仓库新增 `Deploy/docker-compose.yml`，当前已能统一编排 `gateway / api / auth / frontend` 四个服务，并以 SQLite + 内存缓存作为首版最小交付口径。

### Docker 构建验证

- **Compose 配置展开已通过**：`docker compose -f Deploy/docker-compose.yml config` 可正常输出最终编排结果，说明服务、端口、卷挂载与环境变量结构当前无明显语法问题。
- **前端镜像构建已通过**：`docker compose -f Deploy/docker-compose.yml build frontend` 已完成，`radish.client` 与 `radish.console` 的生产构建均在镜像内成功产出。
- **后端三镜像构建已通过**：`docker compose -f Deploy/docker-compose.yml build api auth gateway` 已完成，`Auth / Gateway / Api` 当前均能成功发布为镜像。
- **API 发布冲突已顺手收口**：首次构建时，`Radish.Api` 因项目引用 `Radish.Auth` 而在 `dotnet publish` 阶段触发重复 `appsettings.json` 的 `NETSDK1152`；当前已在 Dockerfile 中显式收口，构建可继续通过。

### 规划口径同步

- **状态矩阵已同步更新**：`Docker 镜像构建链` 当前已从“待补齐”转为“待联调复核”，因为资产和 build 级验证都已存在，下一步不再是补文件，而是做运行态 Smoke 与交付口径确认。
- **部署文档已改为真实资产口径**：`deployment/guide.md` 当前已同步四个 Dockerfile、前端静态托管脚本与最小 Compose，不再继续沿用“只有 API 镜像 + PostgreSQL 示例”的旧描述。

### Gateway 本地 Docker HTTPS 口径收口

- **Compose 联调入口已统一回项目主口径**：`Deploy/docker-compose.yml` 不再把 Gateway 临时降为 `http://localhost:5000`，当前已统一为 `https://localhost:5000` 作为本地最小容器链的唯一对外入口。
- **Gateway 镜像已补开发证书**：`Radish.Gateway/Dockerfile` 当前会把 `Certs/` 带入镜像，并使用新增的 `dev-gateway-cert.pfx` 为容器内 `5000` 端口提供 TLS，避免浏览器直接访问时再出现 `ERR_SSL_PROTOCOL_ERROR`。
- **反代场景已补 Forwarded Headers 支持**：`Radish.Gateway` 当前已显式启用 `X-Forwarded-For / Proto / Host` 识别，后续无论是本地 Compose 直连 HTTPS，还是生产环境在 Nginx / Caddy 后由外层终止 TLS，都能保持请求 Scheme 与重定向行为一致。

### Gateway 容器内 HTTP / HTTPS 模式切换

- **Gateway HTTPS 重定向已改为显式配置开关**：`Radish.Gateway` 当前新增 `GatewayRuntime:EnableHttpsRedirection` 运行时配置，dev / prod 不再需要靠手改代码切换 `UseHttpsRedirection()`。
- **Compose 已拆为基础文件 + 环境覆盖文件**：`Deploy/docker-compose.yml` 当前只保留共享结构，新增 `Deploy/docker-compose.dev.yml` 与 `Deploy/docker-compose.prod.yml` 分别承载“本地内部 HTTPS”与“生产内部 HTTP”两种默认口径。
- **开发与生产默认口径已分离**：本地联调默认使用 `docker-compose.dev.yml`，保持 `https://localhost:5000` 唯一对外入口；生产默认使用 `docker-compose.prod.yml`，保持“外层反代 HTTPS、Gateway 容器内 HTTP”的更常规部署方式。

### OIDC 官方客户端回调地址收口

- **官方客户端回调地址不再写死 `https://localhost:5000`**：`OpenIddictSeedHostedService` 当前已改为根据 `OpenIddict:Server:Issuer` 动态生成 `radish-client / radish-console / radish-scalar` 的 Gateway 回调地址。
- **生产域名现在会自动进入客户端种子**：当 `Deploy/docker-compose.prod.yml` 通过 `RADISH_PUBLIC_URL` 覆盖 `OpenIddict__Server__Issuer` 后，Auth 启动时会同步把对应域名写入 RedirectUris / PostLogoutRedirectUris。
- **`prod` 口径下不再支持用 `http://localhost:5000` 直接测登录**：官方客户端回调当前会跟随公开域名注册，生产联调必须使用与 `RADISH_PUBLIC_URL` 一致的 HTTPS 域名，否则会被 OpenIddict 按 `redirect_uri` 不匹配拒绝。
- **开发直连回调仍然保留**：`http://localhost:3000` 与 `http://localhost:3100` 的前端开发服务器回调当前仍保留，用于宿主机 Vite 开发联调。

### Docker 生产交付口径收尾

- **已补生产反代配置文件**：`Deploy/nginx.prod.conf` 当前已作为随仓库交付的 Nginx 样例落地，默认采用“宿主机 Nginx 终止 HTTPS，再回源 `127.0.0.1:5000`”的口径，并显式保留 `Host`、`X-Forwarded-For`、`X-Forwarded-Proto` 与 `X-Forwarded-Host`。
- **已补生产环境变量样例**：`Deploy/.env.prod.example` 当前已覆盖 `RADISH_PUBLIC_URL`、Auth 证书挂载目录、签名 / 加密证书路径与密码，`Deploy/docker-compose.prod.yml` 也已同步接入这些变量，不再继续回落到开发证书。
- **部署文档已完成 dev / prod 启动方式收口**：`Docs/deployment/guide.md` 当前已明确 `base + dev` 与 `base + prod` 的适用场景、启动命令、`--env-file` 用法，以及当前 `Deploy/` 下真实存在的 Nginx / Compose 交付文件，减少交付口径继续漂移。
- **Compose 静态展开已覆盖 dev / prod 两套组合**：`docker compose -f Deploy/docker-compose.yml -f Deploy/docker-compose.dev.yml config` 与 `docker compose --env-file Deploy/.env.prod.example -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml config` 当前都可正常展开，说明这轮新增变量和覆盖关系没有引入明显语法问题。

## 2026-03-26 (周四)

### 首版待复核主线真实 Smoke 结果

- **用户已完成一轮更大范围首版 Smoke**：本轮覆盖 WebOS 桌面与应用容器、论坛基础、社区 `P0`、`Console V1`，以及 `radish.client` 的国风视觉基线 / 主题切换 / `i18n`。
- **验收反馈已明确通过**：用户于 `2026-03-26` 直接确认“全部都没啥问题，可以收口了”。
- **规划口径已同步收口**：以上功能线当前从“待联调复核”转入“等待总回归确认”，后续以回归维护和总回归记录整理为主，不继续扩张新切片。

### 文档口径同步

- **状态矩阵已更新**：`dev-first-status-matrix.md` 当前已把 `WebOS`、论坛基础、社区 `P0`、`Console V1`、国风视觉基线、主题切换与 `i18n` 统一改为“已完成”。
- **当前规划页已收束重点**：`planning/current.md` 与 `development-plan.md` 当前已把首版剩余重点进一步收束到总回归记录维护、内部开发版判断与上线前交付复核。
- **首版总回归检查单已补齐**：当前已新增 `guide/dev-first-regression-checklist.md`，把自动化门槛、可直接复用的 Smoke 记录、发布前检查项与统一记录格式收束到一个入口。
- **首版总回归记录已补齐**：当前已新增 `guide/dev-first-regression-record.md`，把本轮真实 Smoke 与后续自动化验证结果统一沉淀成一份阶段记录。
- **本周周志已补本轮结论**：避免“用户已确认通过，但规划页仍停在待复核”的口径漂移继续存在。

### 本轮验证

- ✅ WebOS 桌面与应用容器首版烟雾联调已完成并通过。
- ✅ 论坛基础与社区 `P0` 首版烟雾联调已完成并通过。
- ✅ `Console V1` 首版烟雾联调已完成并通过。
- ✅ `radish.client` 国风视觉基线 / 主题切换 / `i18n` 首版烟雾联调已完成并通过。

### 首版验证基线复跑结果

- **`npm run validate:baseline` 已于 `2026-03-26` 复跑并通过**：前端 `type-check`、`radish.client` 最小测试、Console 权限扫描、身份语义扫描、后端构建与 `Radish.Api.Tests` 当前均已通过。
- **中途暴露的测试阻塞已完成收口**：`PostControllerTest` 的两个 `GetById` 用例曾因严格 `Mock` 未补 `CommentService.QueryAsync` 调用而失败；当前已补测试桩并完成定向复验。
- **`npm run validate:baseline:host` 已继续通过**：`DbMigrate doctor / verify` 只读自检当前均已通过，验证基线已完成本轮收口。
- **当前工程结论已同步收口**：业务 / 体验主线 Smoke 与验证基线均已完成本轮收口，首版剩余重点进一步收束到总回归记录维护、内部开发版判断与上线前交付复核。

### 最小 CI 门禁真实合并闭环

- **最新一次 `master` PR 已完成三项质量门禁并成功合并**：`Repo Hygiene`、`Frontend Lint`、`Baseline Quick` 当前均已全绿，`fix(test): 收口首版回归与验证基线` 已以 `37fe89c` 合入 `master`。
- **`dev` 已同步 `master` 合并结果**：当前已补一条回灌提交 `cbd0f8a`，用于把 `master` 的 squash / merge 结果同步回 `dev`，避免后续 PR 继续重复出现同一批冲突。
- **规划口径已继续更新**：`首次 CI/CD` 当前已从“待联调复核”转入“已完成”，首版 `dev` 已明确为“可发内部开发版”；真实外部反代域名 / Auth 证书 / OIDC 回调链路复核已后置到 `CI/CD` 完善且具备 Docker 镜像推送能力后再正式执行。

### 首版内部开发版判断

- **当前判断已正式收口为“可发内部开发版”**：基于业务 / 体验主线已完成本轮 Smoke、`validate:baseline` 与 `validate:baseline:host` 已通过、最小 `CI` 门禁已完成真实合并闭环，以及当前无已知阻塞主线的 `P0 / P1` 问题，首版 `dev` 当前已具备内部开发版发布条件。
- **下一阶段主线已明确为 `CI/CD` 与 Docker 镜像推送链路**：真实外部反代域名、Auth 证书与 OIDC 回调链路当前暂不阻塞内部开发版判断；待具备真实部署条件后，再正式补联调记录。

### GHCR 后端镜像工作流资产

- **已补 `Docker Images` workflow**：当前已新增 `.github/workflows/docker-images.yml`，覆盖 `PR -> build only`、`push dev -> backend push`、`push v* -> backend release push` 三类触发，后端镜像默认目标为 `GHCR`。
- **前端运行时配置注入已补齐**：`Frontend/scripts/serve-static.mjs` 当前会在请求 `/runtime-config.js` 时动态返回运行时配置脚本，`radish.client / radish.console` 已优先读取运行时配置，`frontend` 统一镜像推送不再受“公开地址写死在构建产物里”限制。
- **下一步只剩真实 GHCR 产物验证与前端纳管**：先验证 `dev` / `v*` 的后端镜像真实产物，再把 `frontend` 纳入 `Docker Images` workflow 的推送规则。

## 2026-03-27 (周五)

### Frontend 纳入统一 GHCR 推送

- **`frontend` 已接入 `Docker Images` workflow 的统一推送规则**：当前 `push -> dev` 与 `push -> v*` 会在构建 `Frontend/Dockerfile` 后同步推送 `ghcr.io/<owner>/radish-frontend`，tag 规则与后端保持一致：`dev-latest` / `dev-<shortsha>`、`<tag>` / `latest`。
- **手动补跑入口已补 `push_frontend` 开关**：`workflow_dispatch` 现在不再只能手动推送后端镜像；当当前 ref 为 `dev` 或 `v*` tag 时，可按需单独补跑 `frontend` 推送。
- **后端 GHCR 真实产物验证结论已固化为前置事实**：当前已确认 `radish-api / radish-auth / radish-gateway` 可通过 `docker pull` 获取，因此本轮工作不再继续补后端 workflow 结构，而是转向前端镜像统一推送的接入与验证。

### Frontend GHCR 与镜像体积收口

- **`frontend` GHCR 首次真实产物已完成验证**：当前已确认 `ghcr.io/<owner>/radish-frontend` 可通过 `docker pull` 获取，包权限、可见性与 tag 规则已完成一轮真实验证。
- **前端镜像已从“构建机镜像”收口为轻量运行时镜像**：`Frontend/Dockerfile` 当前已改为纯 `Node 24` 多阶段构建，最终镜像只保留静态产物与 `serve-static.mjs`，不再把 `.NET SDK`、源码与 `node_modules` 一并带入最终层。
- **本地构建体积已明显下降**：用户已确认本地重新构建后的前端镜像体积约为 `300MB`，不再维持此前约 `2.2GB` 的过重状态。

### 文档口径同步

- **部署与规划文档已同步更新**：`deployment/guide.md`、`planning/current.md`、`planning/dev-first-status-matrix.md`、`development-plan.md` 与 `guide/dev-first-regression-record.md` 当前已统一改为“前后端 GHCR 已验证、前端镜像体积已收口、剩余事项转为上线前交付复核”的口径。
- **当前下一步已重新收束**：首版 `dev` 的剩余工程事项不再是“验证前端 GHCR 首次真实产物”，而是“冻结统一镜像推送口径，并在条件具备后执行上线前交付复核”。

### 本轮验证

- ✅ `Docker Images` workflow 与相关文档已完成静态复查。
- ✅ `frontend` GHCR 首次真实产物已完成 `docker pull` 验证。
- ✅ 前端镜像本地构建体积已收口到约 `300MB`。

## 2026-03-28 (周六)

### 测试 / 生产部署口径统一

- **部署形态已正式拆为三类**：开发运行继续默认使用 IDE / 宿主机直跑；测试部署采用“Gateway 容器内 HTTPS”；生产部署采用“外部 Nginx 终止 HTTPS、容器内部 HTTP”。
- **测试部署已新增独立 Compose 覆盖**：仓库当前已新增 `Deploy/docker-compose.test.yml` 与 `Deploy/.env.test.example`，用于 `https://IP:port` 或测试域名直连的最小容器部署。
- **测试环境浏览器证书告警已作为已知约束固化**：测试部署的 Gateway TLS 证书采用自签名自动生成方案，浏览器默认不信任属于预期行为，不再继续把这类告警误判为部署异常。

### Gateway TLS 与 Auth OIDC 证书自动生成

- **Gateway 测试 TLS 证书现支持自动生成并复用**：`Gateway` 容器启动前会在挂载目录中检查目标 `pfx`，缺失时按 `RADISH_PUBLIC_URL` 的 host 生成带 SAN 的自签名证书；若已存在则直接复用。
- **Auth OIDC signing / encryption 证书现支持自动生成并复用**：`Auth` 容器启动前会在挂载目录中检查 signing / encryption `pfx`，缺失时自动生成，已存在时保持复用，不会因重启导致 key 漂移。
- **生产部署默认允许首次自动生成 OIDC 证书**：`Deploy/docker-compose.prod.yml` 当前已允许 `Auth` 在首次启动时把 OIDC 证书写入 `RADISH_AUTH_CERTS_DIR`；后续多实例部署时必须改为共享同一组证书。

### 文档与资产同步

- **Docker 镜像已补容器启动脚本**：`Radish.Auth/Dockerfile` 与 `Radish.Gateway/Dockerfile` 当前都会带入 `Scripts/docker` 下的证书初始化脚本。
- **部署指南已改为开发 / 测试 / 生产三套口径**：`deployment/guide.md` 与 `guide/authentication.md` 当前已同步 Gateway TLS 证书、Auth OIDC 证书与持久化复用策略。

### 部署编排与入口文档继续收口

- **本地容器编排已改名为 `local`**：原 `Deploy/docker-compose.dev.yml` 当前已正式改为 `Deploy/docker-compose.local.yml`，只承载“本地构建镜像并启动验证”的职责，不再与日常开发运行混淆。
- **测试 / 生产部署已完全切到远程镜像口径**：`Deploy/docker-compose.yml` 当前只保留共享结构与 `image:` 引用；`Deploy/docker-compose.test.yml` 与 `Deploy/docker-compose.prod.yml` 默认走 `config + pull + up`，部署机不再承担现场 `build` 职责。
- **镜像变量入口已补齐**：`Deploy/.env.test.example` 与 `Deploy/.env.prod.example` 当前已补 `RADISH_FRONTEND_IMAGE / RADISH_API_IMAGE / RADISH_AUTH_IMAGE / RADISH_GATEWAY_IMAGE`，测试环境默认面向 `dev-*` 镜像，生产环境明确要求固定 release tag。
- **根目录入口文档已同步**：`README.md`、`deployment/guide.md`、`guide/gateway.md`、`guide/configuration.md` 以及规划页当前都已统一为“开发运行直跑、`local` 做本地容器验证、`test / prod` 拉取远程镜像”的口径。

### GitHub Actions 触发口径收口

- **`Repo Quality` 已停止响应普通 `dev` push**：当前仅保留 `pull_request -> master / dev` 与手动触发，避免日常同步 `dev` 时重复消耗 Actions 资源。
- **`Docker Images` 已改为只响应规范 tag**：当前仅在 `push v*-dev / v*-test / v*-release` 或手动补跑对应 tag 时推送 `GHCR` 镜像，不再沿用“`push dev` 自动推送”的旧口径。
- **镜像浮动别名已按轨道拆分**：开发轨道产出 `<tag> + dev-latest`，测试轨道产出 `<tag> + test-latest`，正式发布轨道产出 `<tag> + latest`。
- **版本规范与部署样例已同步**：`Docs/architecture/specifications.md`、`README.md`、`Deploy/.env.test.example`、`Deploy/.env.prod.example` 与部署指南当前都已统一到 `-dev / -test / -release` 三条镜像轨道。

### Tag 驱动测试部署验收通过

- **`v26.3.1-test` 已完成从构建到部署的整链验证**：当前已确认测试 tag 可以成功触发 `Docker Images` workflow，并产出 `radish-api / radish-auth / radish-gateway / radish-frontend` 四个测试轨道镜像。
- **测试环境已确认使用远程镜像启动**：`Deploy/docker-compose.yml + Deploy/docker-compose.test.yml` 当前已可从 `GHCR` 拉取 `v26.3.1-test` 远程镜像并完成 `base + test` 启动，不再停留在仅本地镜像验证阶段。
- **用户已完成一轮真实测试部署 Smoke**：登录、回调、权限与核心页面当前均已通过，`base + test` 测试部署链路已完成本轮收口。
- **当前下一步已继续收束**：后续重点不再是验证测试部署是否可用，而是组织 `dev -> master` 发布 PR、产出 `v*-release` 镜像，并执行生产口径首轮 Smoke。

### `v26.3.2-test / v26.3.2-release` 阶段验收通过

- **最新测试轨道已完成更新验收**：`v26.3.2-test` 当前已完成 tag 驱动镜像构建、`GHCR` 拉取与 `base + test` 真实部署验收，登录 / 回调 / 权限 / 核心页面以及 `DbMigrate` 初始化链路当前均已通过。
- **正式发布轨道已完成真实部署与发布验收**：`v26.3.2-release` 当前已完成 release 镜像产出、`GHCR` 拉取与 `base + prod` 生产口径部署验收，发布与部署验收均已通过。
- **`radish-dbmigrate` 已补齐首次真实拉取与初始化验证**：本轮不再停留在“等待下一次规范 tag 验证”的状态，容器化初始化、`dbmigrate -> api/auth -> gateway` 启动顺序，以及 `local / test / prod` 三套口径当前都已形成真实交付事实。
- **当前阶段结论已更新**：首版 `dev`、测试部署与正式发布链路均已完成本轮收口，下一阶段主线切换到论坛 / 聊天等社区体验与运营能力优化。

### DbMigrate 容器化初始化补齐

- **已补 `Radish.DbMigrate` 独立镜像入口**：当前仓库已新增 `Radish.DbMigrate/Dockerfile`，用于容器部署时执行 `apply` 一次性初始化任务，不再要求宿主机必须安装 `dotnet`。
- **三套容器口径已统一接入初始化步骤**：`Deploy/docker-compose.yml` 当前已收口为 `dbmigrate -> api/auth -> gateway` 的启动顺序，`local / test / prod` 都会先初始化共享业务库，再启动登录链路相关宿主。
- **`radish-dbmigrate` 已纳入 GHCR 发布链**：`Docker Images` workflow 当前会与 `radish-api / radish-auth / radish-gateway / radish-frontend` 一起产出 `radish-dbmigrate` 镜像，测试与生产样例变量也已同步补齐。
- **当前问题口径已修正**：生产环境之前出现的 `SQLite Error 1: 'no such table: User'`，根因已明确为容器部署未先初始化共享业务库；后续同类场景将由 `dbmigrate apply` 自动兜底。
- **首次真实部署验证已补齐**：最新 `v26.3.2-test / v26.3.2-release` 验收中，`radish-dbmigrate` 已完成真实拉取、初始化与启动顺序验证，不再仅停留在静态配置或 workflow 资产层面。

### 发布态细节收口

- **登录页测试账号提示已改为显式配置**：`Radish.Auth` 当前新增 `AuthUi:ShowTestAccountHint`，开发默认保留提示；`Deploy/docker-compose.test.yml` 显式注入 `true`，`Deploy/docker-compose.prod.yml` 显式注入 `false`，不再依赖域名形态或环境名隐式推断。
- **容器 / 发布态日志目录识别已修复**：`LogContextTool` 当前改为优先使用宿主入口程序集名识别项目目录名，本地开发仍保留 `.csproj` 扫描兜底，避免发布目录或 Docker 容器继续把日志落到 `Logs/Unknown/`。
- **示意环境文件默认镜像地址已收口**：`Deploy/.env.test.example` 当前默认指向 `ghcr.io/laugh0608/...:test-latest`，`Deploy/.env.prod.example` 默认指向 `ghcr.io/laugh0608/...:latest`，同时继续保留“正式环境优先固定版本 tag”的文档口径。

### 社区体验优化首批收口

- **论坛置顶最小闭环已接通**：当前复用既有 `Post.IsTop` 字段，不新增表结构；管理员可在论坛帖子详情区执行置顶 / 取消置顶，列表页继续沿用既有“置顶优先，再按当前排序规则”的口径。
- **聊天室图片已改为“先入草稿、点击发送再统一发出”**：用户选择图片后，当前不会立即发消息，而是先作为待发送附件显示在输入区；此时仍可继续输入文字、移除待发图片，并在点击发送时与文字一并发出，也支持纯图片发送。
- **聊天室待发图片已纳入频道草稿恢复**：当前按频道持久化 `content + replyTarget + pendingImage`，切换频道后会自动恢复；若上传完成时用户已切到其他频道，图片也会回写到原频道草稿，降低误发风险。
- **论坛发帖分类摘要不同步问题已修复**：发帖弹窗当前已统一维护 `categoryId + categoryName` 的快照状态，顶部“帖子设置”摘要不再依赖分类列表二次查找，初选、切换、清空与恢复草稿时都能即时同步显示。
- **最小回归已补齐**：本轮已新增帖子置顶控制器测试，管理员置顶成功与非管理员 `403` 当前均有自动化兜底；`radish.client` 也已完成类型检查与生产构建，确认论坛 / 聊天前端改动可通过当前构建链路。
- **相关设计文档已同步**：论坛功能说明、分类 / 标签专题，以及聊天室 App 总览 / UI 模块 / 路线图当前都已对齐到本轮实现，避免设计口径继续停留在旧交互。

### 本轮验证

- ✅ `docker compose -f Deploy/docker-compose.yml -f Deploy/docker-compose.local.yml config` 通过。
- ✅ `docker compose --env-file Deploy/.env.test.example -f Deploy/docker-compose.yml -f Deploy/docker-compose.test.yml config` 通过。
- ✅ `docker compose --env-file Deploy/.env.prod.example -f Deploy/docker-compose.yml -f Deploy/docker-compose.prod.yml config` 通过。
- ✅ `v26.3.2-test / v26.3.2-release` 已完成 tag 驱动镜像构建、远程镜像拉取，以及 `base + test` / `base + prod` 真实部署验收。
