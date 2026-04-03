# 首版 dev 总回归记录

> 本页用于沉淀当前首版 `dev` 收官阶段的统一回归结论。
>
> 关联文档：
>
> - [首版 dev 总回归与发布前检查单](/guide/dev-first-regression-checklist)
> - [首版 dev 功能矩阵状态表](/planning/dev-first-status-matrix)
> - [当前进行中](/planning/current)
> - [开发路线图总览](/development-plan)
> - [2026-03 / week4](/changelog/2026-03/week4)

## 总回归收口补充记录（2026-04-02）

- 记录日期：2026-04-02
- 记录人：项目协作记录
- 范围：社区主链回归收口与首版 `dev` 文档口径统一
- 触发原因：社区主链已完成多轮回归，当前需要把“等待总回归确认”的旧口径统一回写为事实结论，避免规划页、状态矩阵与回归记录继续落后于真实状态

### 变更摘要

- 确认论坛、聊天室、通知中心、认证登录稳定性、附件访问边界，以及论坛评论分页等本轮触达范围当前无新的明显阻塞问题。
- 确认首版 `dev` 范围内的业务主线、体验主线与发布链路当前均已完成本轮统一回归确认。
- 回写 `planning/current.md`、`development-plan.md`、`planning/dev-first-scope.md`、`planning/dev-first-status-matrix.md` 与本页关联文档，统一把“等待总回归确认”收束为“总回归已完成，转入稳定维护”。
- 明确下一步不再继续扩张社区体验尾项，而是转入下一里程碑入口重审。

### 本轮确认结论

- 社区主链回归：完成
- 首版 `dev` 文档口径：已统一
- 当前工程判断：继续保持“可发内部开发版”结论不变
- 当前阶段更准确的表述应为：`社区主链回归收口完成，转入下一里程碑入口重审`

### 下一动作

1. 维持当前规划页、总回归记录、状态矩阵与周志口径一致，避免事实再次漂移。
2. 继续以 `validate:baseline` / `validate:baseline:host` 与 `master` PR 门禁作为跨层改动默认回归入口。
3. 优先完成身份语义 Phase 4 的启动前提确认；若前提暂不满足，再先按 [M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline) 收束下一条主线。

## 首版 dev 总回归记录（2026-03-26）

- 记录日期：2026-03-26
- 记录人：项目协作记录
- 范围：首版 `dev` 总回归
- 触发原因：首版 `dev` 收官阶段需要把已通过的真实 Smoke、当前验证基线结果与工程判断收束成统一记录，避免继续依赖会话口头结论

### 变更摘要

- 汇总 `2026-03-23`、`2026-03-25`、`2026-03-26` 已通过的首版 Smoke 结果。
- 复跑 `npm run validate:baseline` 与 `npm run validate:baseline:host`，确认当前工程门槛是否已具备“可发内部开发版”判断基础。
- 补记最新一次 `master` PR 的最小 `CI` 门禁真实通过、PR 合并与 `dev` 回灌结果，避免继续停留在“门禁已接通但仍待确认闭环”的口径。
- 基于真实执行结果回写当前首版 `dev` 的工程结论，而不是继续沿用“应该差不多通过”的泛化口径。

### 影响范围

- 认证 / OIDC / Gateway 基础入口
- WebOS 桌面与应用容器
- 论坛基础
- 社区 `P0`
- `Console V1`
- `radish.client` 国风视觉基线 / 主题切换 / `i18n`
- 通知中心
- Docker 镜像构建链与最小交付口径
- 验证基线与最小 `CI` 门禁判断

### 自动化执行

- `npm run validate:baseline`：通过
  - 前端 `type-check`：通过
  - `radish.client` 最小 node 测试：通过，`6/6`
  - `Console` 权限链路扫描：通过
  - 身份语义防回归扫描：通过
  - 后端解决方案构建：通过
  - 后端测试：通过，`213/213`
- `npm run validate:baseline:host`：通过
  - 复用 `full` 验证结果并追加 `DbMigrate doctor / verify`
  - `DbMigrate doctor`：通过
  - `DbMigrate verify`：通过
- `master` Pull Request 最小 `CI` 门禁：通过
  - 最新一次用于合并的 `master` PR 已完成 `Repo Hygiene`、`Frontend Lint`、`Baseline Quick`
  - PR 已合并到 `master`：`37fe89c`
  - `dev` 已同步 `master` 合并结果：`cbd0f8a`

### 直接引用的已有通过记录

- `2026-03-23`：通知中心真实首版 Smoke 通过
- `2026-03-23`：认证 / OIDC / Gateway 基础入口真实首版 Smoke 通过
- `2026-03-25`：Docker 镜像构建链与最小交付口径 Smoke 通过
- `2026-03-26`：WebOS / 论坛基础 / 社区 `P0` / `Console V1` / 国风视觉基线 / 主题切换 / `i18n` 首版烟雾联调通过

### 本轮新增补验

- 首次复跑 `npm run validate:baseline` 时暴露 `2` 个 `PostControllerTest` 失败
- 补齐 `PostControllerTest` 对 `CommentService.QueryAsync(...)` 的严格 `Mock` 后，重新执行 `npm run validate:baseline`
- 继续执行 `npm run validate:baseline:host`
- 未新增额外 UI Smoke；本轮用户侧手工联调结论继续复用同日已确认通过的真实记录

### 已收口问题

- `Radish.Api.Tests.Controllers.PostControllerTest.GetById_Should_Return_Full_Poll_Detail_When_PostExists`
- `Radish.Api.Tests.Controllers.PostControllerTest.GetById_Should_Pass_AnswerSort_When_RequestContains_AnswerSort`
- 上述两个失败用例的根因是：
  - `PostController.GetById` 当前会在 `FillPostAvatarAndInteractorsAsync` 中新增调用 `IBaseService<Comment, CommentVo>.QueryAsync(...)`
  - 对应严格 `Mock` 尚未补齐该调用的 `setup`
- 当前处理结果：
  - 已在 `PostControllerTest` 中补齐 `commentServiceMock.QueryAsync(...)` 的空列表返回
  - 定向测试已恢复通过
  - `validate:baseline` 与 `validate:baseline:host` 已恢复全通过

### 当前结论

- 结果：可发内部开发版
- 工程判断：当前业务主线与体验主线 Smoke、`full` 验证基线，以及 `host` 只读自检均已完成当前批次收口
- 当前状态更准确的表述应为：`首版 dev 已具备可发内部开发版条件，当前转入内部开发版后观察与上线前交付复核阶段`

## 测试部署补充记录（2026-03-28）

- 记录日期：2026-03-28
- 记录人：项目协作记录
- 范围：`v26.3.1-test` tag 驱动镜像发布与 `base + test` 真实部署验收
- 触发原因：在“可发内部开发版”判断之后，需要把测试部署是否真实可用补成事实记录，避免只停留在“镜像已可构建 / 可拉取”的阶段

### 执行摘要

- 推送 `v26.3.1-test` 后，`Docker Images` workflow 已成功产出 `radish-api / radish-auth / radish-gateway / radish-frontend` 四个测试轨道镜像。
- 测试环境已基于 `Deploy/docker-compose.yml + Deploy/docker-compose.test.yml` 从 `GHCR` 拉取远程镜像，并完成 `base + test` 启动。
- 用户已完成一轮真实测试部署 Smoke：登录、回调、权限与核心页面当前均已通过。

### 当前结论

- 测试部署链路已从“镜像可构建 / 可拉取”推进到“可部署、可登录、可回归”的状态。
- 当前发布准备可进一步收束为：组织 `dev -> master` 发布 PR、产出 `v*-release` 镜像，并执行生产口径首轮 Smoke。

### 风险 / 后置项

- 最小 `CI` 门禁已完成真实合并闭环，但后续新增跨层改动仍需继续依赖 `master` PR 门禁作为默认质量入口
- 当前 `dotnet build/test` 仍存在较多警告，但本轮未表现为阻塞项；是否继续清理，后续按优先级单列，不并入当前首版门槛判断
- 本轮未重新组织新的业务侧总串联 Smoke，仍以 `2026-03-23 ~ 2026-03-26` 已沉淀记录为准
- 当前已完成 `GHCR` 前后端镜像 workflow 真实产物验证：`radish-api / radish-auth / radish-gateway / radish-frontend` 当前均已可通过 `docker pull` 获取；`radish-dbmigrate` 已纳入统一 GHCR 推送口径，待下一次规范 tag 完成首次真实拉取验证；`frontend` 侧的运行时配置注入已完成，`Frontend/Dockerfile` 也已收口为轻量多阶段运行时镜像，本地构建验证体积约 `300MB`
- 真实外部反代域名、Auth 证书与 OIDC 回调链路当前仍未在生产口径下补联调记录；该项已明确后置到 `v*-release` 产出后执行的生产口径首轮 Smoke，不阻塞当前内部开发版与测试部署判断

### 下一动作

1. 维持当前 `master` PR 质量门禁与 `validate:baseline` 作为内部开发版默认回归入口。
2. 如近期再发生跨层改动，优先重新执行 `npm run validate:baseline`；涉及宿主 / 配置时再补 `npm run validate:baseline:host`。
3. 维持当前统一镜像推送与轻量前端镜像口径；如后续再改 Dockerfile / workflow / Compose，优先复核镜像可拉取性与前端镜像体积。待具备真实外部域名、Auth 证书与镜像推送条件后，再补记真实外部反代域名、Auth 证书与 OIDC 回调链路的联调结果。

## 正式发布补充记录（2026-03-28）

- 记录日期：2026-03-28
- 记录人：项目协作记录
- 范围：`v26.3.2-test` 与 `v26.3.2-release` 的发布、部署与验收结果补记
- 触发原因：在“可发内部开发版”与首轮测试部署验收之后，需要补齐最新测试轨道与正式发布轨道的真实验收记录，避免规划页仍停留在“release 待验证”或 “dbmigrate 待下一次 tag 验证”的旧口径

### 执行摘要

- `v26.3.2-test` 已完成 tag 驱动镜像构建、`GHCR` 拉取与 `base + test` 真实部署验收。
- `v26.3.2-release` 已完成 release 镜像产出、`GHCR` 拉取与 `base + prod` 生产口径部署验收。
- 用户已确认 release 与部署验收均已通过，登录 / 回调 / 权限 / 核心页面当前无明显阻塞问题。
- `radish-dbmigrate` 已在本轮补齐首次真实拉取、初始化与启动顺序验证，`local / test / prod` 三套口径当前都已形成真实交付事实。

### 本轮补齐事实

- `DbMigrate` 容器化初始化当前已不再只是“静态编排正确”或 “workflow 已纳入统一发布链”，而是已经过真实部署验证。
- `AuthUi__ShowTestAccountHint` 当前已作为显式配置项稳定用于测试与正式发布，不再依赖环境名或域名形态隐式推断。
- 容器 / 发布态日志目录识别修复当前已进入真实发布环境，不再优先落到 `Logs/Unknown/`。
- `Deploy/.env.test.example` / `Deploy/.env.prod.example` 的默认镜像地址与 tag 示例已和当前 `test-latest / latest` 轨道口径对齐，正式环境仍推荐固定到明确 `v*-release`。

### 当前结论

- 结果：`首版 dev`、测试部署与正式发布链路均已完成当前批次收口
- 工程判断：当前最小 CI、镜像产出、`DbMigrate` 初始化、测试部署与正式发布验收均已形成闭环，后续不再把“首次真实发布验证”作为待办事项
- 下一阶段更准确的表述应为：`发布链路转入回归维护，当前主线切换到论坛 / 聊天等社区体验与运营能力优化`

### 下一动作

1. 继续以 `master` PR 质量门禁与 `validate:baseline` / `validate:baseline:host` 作为跨层改动默认回归入口。
2. 维持当前 `DbMigrate -> Api/Auth -> Gateway`、`AuthUi__ShowTestAccountHint` 与日志目录识别等发布口径冻结，避免文档与真实行为再次漂移。
3. 推进论坛置顶、聊天室图片草稿发送与论坛发帖分类状态同步等发布后优化项。
