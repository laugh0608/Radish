# 当前进行中

> 本页只维护 **当前里程碑、当前主线、并行收尾项、近期不做项**。
>
> 最近实际产出请看 [开发日志](/changelog/)，总览请回到 [开发路线图](/development-plan)。

## 当前里程碑

- **里程碑**：`M14 宿主运行与最小可观测性基线`
- **当前主线**：`M14 宿主运行与最小可观测性基线`
- **当前阶段**：`截至 2026-04-05，社区主链多轮回归、首版 dev 文档收口，以及身份语义 Phase 4 的启动前提确认、首轮实施窗口与官方顺序真实回归均已完成；当前生产环境的外部兼容边界事实也已补齐：现网仅有 1 套 `v26.3.2-release` Docker 部署，使用 1Panel 默认 HTTPS 反向代理，无仓库外换 Token / 联调 / 巡检脚本，OpenIddict 仅有默认种子数据。当前结论已进一步收束为：Auth 输出双写已收缩、`userinfo` 已完成最小对齐、`radish-client / radish-console / Radish.Api.AuthFlow.http / radish-scalar` 已按官方顺序完成端到端回归，当前无需触发回滚，Phase 4 本轮正式收口并转入维护态；当前主线已切换到 `M14`，且“启动前 `validate:baseline:host` -> 启动后 `check:host-runtime`”的两段主路径、统一报告口径与最小运行态检查入口均已完成首轮收口。详见：[M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline)。`
- **复核日期**：`2026-04-05`

## 当前主线：M14 宿主运行与最小可观测性基线

### 当前执行原则

- 先主线，后细节：优先完成真正影响首版 dev 的核心功能主线、体验主线与工程门槛。
- 先闭环，后打磨：优先保证用户任务完整走通，再决定哪些细节放到首版 dev 之后继续完善。
- 不留坑，不留技术债：允许实现保持克制，但不允许把会阻塞主线或明显导致返工的结构性问题带进首版。
- 体验主线同步推进：国风视觉基线 / 主题切换 / `radish.client` i18n 已纳入首版范围，但必须收敛在核心页面与核心文案。
- 非阻塞增强统一后置：`P3-ext`、`P4-ext`、`P5-ext`、`Console-ext Phase 2+`、开放平台第三方接入 / SDK 等增强项统一后置。

### 当前执行入口

- [M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline)
- [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist)
- [验证基线说明](/guide/validation-baseline)
- [部署与容器指南](/deployment/guide)

### 本阶段目标

- 把 `validate:baseline:host`、`DbMigrate doctor / verify`、健康检查、日志与部署复核收束成统一执行顺序
- 明确宿主异常时的默认排障骨架，避免继续靠口头说明决定“先看哪里”
- 把测试部署与生产部署的最小运行态复核动作统一回链到同一入口
- 维持 Phase 4 的稳定维护边界；若部署形态、第三方客户端或反代规则变化，再回到 [身份语义 Phase 4 仓库外兼容边界确认清单](/guide/identity-claim-external-compat-checklist) 追加事实评审
- 明确 `M14` 当前不扩写为完整运维平台、完整可观测性平台或大而全 CI / E2E 工程

### 当前输出

- [x] 已将当前主线从身份语义 `Phase 4` 正式切换为 `M14`，并新增 [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist) 作为默认执行入口
- [x] 已将 `M14` 默认主路径固定为“启动前 `npm run validate:baseline:host`，启动后 `npm run check:host-runtime`”，把代码 / 配置 / 数据库前置问题与运行态 / 网关链路问题明确分层
- [x] 已统一启动前与启动后两段检查的报告能力：`validate:baseline:host` 与 `check:host-runtime` 当前均支持 `--report`、`--report-file`，并统一输出 `Summary / Actions` 两段
- [x] 已拆分 `Gateway` 健康端点语义：`/health` 当前固定用于最小后端宿主链检查，`/healthz` 固定用于更完整的扩展观测与结构化下游摘要
- [x] 已补 `Api / Auth / Gateway` 启动日志运行摘要：`Api` 当前会输出 JWT 验签模式 / Issuer / signing 证书摘要，`Auth` 会输出 OIDC Issuer / 密钥模式 / signing/encryption 证书摘要，`Gateway` 会输出最小探活与扩展观测目标摘要，且 `console-service` 已固定为 `/healthz + Degraded`
- [x] 已将首版 `dev` 的定义从“某条子线已收口”改为“所有纳入范围的核心功能主线至少完成 70%”
- [x] 已新增 [首版 dev 边界](/planning/dev-first-scope)，明确业务主线、体验主线与工程门槛
- [x] 已新增 [首版 dev 功能矩阵状态表](/planning/dev-first-status-matrix)，把首版范围内各功能线收束为 `已完成 / 待联调复核 / 待补齐 / 不纳入首版`
- [x] 已确认社区相关主功能当前已达到“可演示、可联调、可回归”的首轮收口标准，但仍需回到整产品首版视角继续补齐未完成主线
- [x] 已确认国风视觉基线 / 主题切换 / `radish.client` i18n 纳入首版范围，不再继续作为首版之后再考虑的 Later 项
- [x] 已冻结国风视觉规范与颜色参考：新增视觉主题规范、颜色角色参考与参考素材入口
- [x] 已在 `radish.client` 落地 `default / guofeng` 主题 Token、持久化切换骨架与桌面壳层首批接入
- [x] 已修复主题首轮落地后的 Dock 定位与桌面图标布局回归，当前桌面壳层恢复可用
- [x] 已完成 `radish.client` i18n 首轮高频覆盖：桌面壳层、应用注册、商城首页 / 商品 / 订单 / 背包 / 购买链路、论坛帖子列表 / 帖子详情讨论区 / 评论表单 / 评论树 / 帖子卡片 / 分类列表 / 编辑弹窗与历史弹窗、文档应用主链、聊天高频消息链路、通知中心主列表与共享通知组件，以及个人中心资料核心信息 / 浏览记录 / 关系链 / 头像上传 / 附件列表 / 钱包交易记录等高频组件已接入翻译键
- [x] 已继续收口论坛深层主题样式：评论树、评论表单与帖子卡片等高频深层组件已切回统一主题变量，残余宿主层回退文案也已改为共用翻译键
- [x] 已完成论坛发帖创作器体验重排：发帖入口已重构为正文优先的创作器工作区，支持 `Markdown / 富文本` 双模式、右侧帖子设置区、全屏创作，以及“富文本输入体验 + Markdown 统一存储”
- [x] 已完成评论编辑器与评论弹层体验重排：讨论区编辑器已改为轻量卡片式面板，评论弹层外框、顶部留白与底部动作区可见性已完成当前批次收口
- [x] 已完成一轮 `radish.client` i18n 自动化验证：`npm run type-check --workspace=radish.client` 通过，`npm run build --workspace=radish.client` 在系统环境构建通过
- [x] 已完成通知中心 / 个人中心 / 桌面壳层首轮语言与主题烟雾验证：`validate:baseline --quick` 通过，`radish.client` 系统环境构建通过，残余桌面余额 / 经验值文案与 Dock / 个人中心少量硬编码颜色已完成收口
- [x] 已完成通知中心一轮真实首版 Smoke：新通知 Toast 预览、Dock 未读角标、通知列表、点击跳转并静默已读、删除后角标同步与多端同步均通过，当前可转入总回归前维护
- [x] 已完成桌面壳层手工联调首轮收口：窗口层点击拦截已修复，Dock 顶部位置、状态按钮与桌面图标布局已按当前视觉方向重新对齐
- [x] 已完成欢迎 App 主题适配与内容口径重写：欢迎页主入口、平台概览 / 上手路径 / 社区约定 / 开源说明已切回统一主题 token，并移除旧版本号、占位 GitHub 链接与过时口径
- [x] 已完成欢迎 App 长文案 i18n：平台概览 / 上手路径 / 社区约定 / 开源说明已完成双语资源化，并通过 `radish.client` 类型检查与构建验证
- [x] 已补 Console 新一轮收尾边界：`console.access` 不再单独放行，分类与标签后台已拆分，内容治理 / 胡萝卜 / 经验等级后台首版已接入，相关资源映射与种子已同步
- [x] 已补社区资料与身份展示细节：查看他人主页时已走公开资料接口，帖子详情作者区与评论树节点已补头像显示，关注/取消关注入口已回到公开主页卡片
- [x] 已补 `M13` 首轮统一验证入口：`validate:baseline` / `quick` / `host`、验证基线说明页与最小脚本化收口
- [x] 已补身份语义 `Phase 5` 首轮工程化资产：新增 `check:identity-runtime`、`check:identity-protocol-output` 与 `validate:identity`，并补 [身份语义防回归回归手册](/guide/identity-claim-regression-playbook) 统一“默认必跑项 / 官方顺序回归 / CI 分层”口径；`Repo Quality` 也已补 `Identity Guard` 独立门禁
- [x] 已补身份语义 `Phase 5` 第二轮工程一致性收口：`check:identity-impact` 当前已统一为单一规则源，并已纳入 `validation-baseline / regression-index / dev-first-regression-record / development-plan / planning/current / PR template` 等默认执行面文档与门禁资产；本地 `validate:ci` 也已对齐为 `Repo Quality` changed-only + 条件 `validate:identity`，并补轻量自校验避免门禁判定再次漂移
- [x] 已补默认执行面第三轮收口：`.githooks/pre-commit` 当前已切到 `check:repo-hygiene:staged + lint:staged` 统一脚本入口；PR 模板、回归记录模板与专题回归索引也已同步纳入 `validate:ci` 与 staged 入口口径，避免“脚本已更新、提交前入口与记录模板仍停留旧写法”
- [x] 已补治理真相源第四轮对齐：`.github/rulesets/master-protection.json`、ruleset README、分支治理 ADR、部署指南与首版回归文档当前已统一改为 `Repo Hygiene / Frontend Lint / Baseline Quick / Identity Guard` 四项 required checks，避免 workflow 已升级而 ruleset / 文档仍停留在旧三项门禁
- [x] 已补 Repo Quality contract 第五轮自校验：新增 `check:repo-quality-contract` 与共享 contract 模块，当前会自动校验 workflow job 名、ruleset required checks 与本地 `validate:ci` 的门禁契约；`validate:baseline` / `quick` 也已纳入这层轻量守卫，避免门禁名称与默认执行面再次无声漂移
- [x] 已补 Repo Quality contract 第六轮语义守卫：当前 contract 不再只校验 required checks 名称，还会校验 `repo-quality.yml` 四个 job 的关键命令片段；`validate:ci` 在文档中的执行面说明也已同步补上 `check:repo-hygiene:changed`，避免“名字一致但执行语义已漂移”
- [x] 已补 Windows 共享执行层兼容性收口：新增共享 `process-runner`，默认脚本当前已尽量改为直连 `node / npm` 可执行体，减少对 `cmd.exe /c` 的依赖；受限沙盒下若仍禁止 Node 再拉起子进程，也已统一输出明确边界说明，避免把环境限制误判成门禁脚本回归
- [x] 已补 `Repo Quality` 单一故障分诊入口：新增 [Repo Quality 故障分诊手册](/guide/repo-quality-troubleshooting)，把 `check:repo-quality-contract`、`validate:ci`、身份语义条件触发、受限环境边界与历史 warning / DLL 锁的分流口径收束到同一页，并已同步到验证基线与专题回归索引
- [x] 已补 `Phase 5` 记录与维护动作闭环：`check:identity-impact` 当前可直接输出命中原因类别，`validate:ci` 会同步显示身份语义命中理由；PR 模板、变更回归记录模板、回归结论记录模板与故障分诊手册也已统一收口为“命中原因 + 失败归类”双字段，避免维护者仍需靠口头解释 `Identity Guard / validate:ci` 为什么命中、失败属于哪一类
- [x] 已完成一轮 `full` 验证：前端 `type-check`、`radish.client` 最小测试、Console 权限扫描、后端构建与 `Radish.Api.Tests` 共 195 个测试通过
- [x] 已补 `M15` 首轮最小 CI 门禁：`Repo Hygiene` / `Frontend Lint` / `Baseline Quick` / `Identity Guard` 已接入 GitHub Actions，并已在最新一次 `master` PR 上完成真实通过与合并闭环
- [x] 已补 `GHCR` 镜像 workflow 资产：`.github/workflows/docker-images.yml` 当前已收口为 `push v*-dev / v*-test / v*-release` 与手动补跑规范 tag 时推送；`frontend` 已接入统一 GHCR 推送规则，普通 `dev` push 不再触发镜像发布
- [x] 已补 `DbMigrate` 容器化入口：`Radish.DbMigrate/Dockerfile`、`radish-dbmigrate` GHCR 发布链与 `dbmigrate -> api/auth -> gateway` 启动顺序当前已纳入 `local / test / prod` 三套容器口径，并已完成 Compose 静态展开校验，避免首次部署因共享业务库缺表导致登录链路直接失败
- [x] 已确认 `GHCR` 后端镜像真实产物可用：当前已可通过 `docker pull` 获取 `radish-api / radish-auth / radish-gateway` 镜像，后端包权限、可见性与 tag 规则已完成一轮真实验证
- [x] 已确认 `GHCR` 前端镜像真实产物可用：当前已可通过 `docker pull` 获取 `radish-frontend` 镜像，前端包权限、可见性与 tag 规则已完成一轮真实验证
- [x] 已补前端运行时配置注入：`Frontend/scripts/serve-static.mjs` 当前会在请求 `/runtime-config.js` 时动态返回运行时配置脚本，`radish.client / radish.console` 已优先读取运行时配置，`Deploy/docker-compose.local.yml / docker-compose.test.yml / docker-compose.prod.yml` 也已补齐 `frontend` 运行时环境变量入口
- [x] 已收口前端镜像体积：`Frontend/Dockerfile` 当前已切为 Node 24 轻量多阶段镜像，最终镜像仅保留静态产物与运行时脚本，本地构建验证体积约 `300MB`
- [x] 已补 `master` 分支保护与 ruleset 资产，当前仓库已切换为“禁止直接 push、仅允许 PR 合并”的最小发布入口
- [x] 已补首版最小 Docker 资产：`Radish.DbMigrate / Radish.Api / Radish.Auth / Radish.Gateway / Frontend` Dockerfile、前端静态托管脚本，以及 `Deploy/docker-compose.yml / docker-compose.local.yml / docker-compose.test.yml / docker-compose.prod.yml` 已落地
- [x] 已完成一轮 Docker build 级验证：前端镜像与 `api / auth / gateway` 三个后端镜像均可构建，`Radish.Api` 发布阶段的重复 `appsettings.json` 冲突也已收口
- [x] 已完成一轮 Docker 运行态验证与交付口径收口：开发运行已明确独立于 Compose；Compose 当前已拆分 `local / test / prod` 三套默认口径，`Gateway` 已支持容器内 HTTP / HTTPS 模式切换与 Forwarded Headers；`base + local` 已完成本地容器验证，`base + test` 已收口“容器内 HTTPS + 自动生成测试 TLS / OIDC 证书”口径，`base + prod` 的 `RADISH_PUBLIC_URL -> OpenIddict__Server__Issuer -> 官方客户端回调地址` 配置链与反代口径也已对齐
- [x] 已将部署编排角色继续收口：`local` 当前仅用于本地容器构建 / 启动验证，`test / prod` 当前统一改为 `config + pull + up` 的远程镜像部署；根目录 `README` 与部署文档已同步到同一口径
- [x] 已完成附件协议与部署边界收口：贴图、Reaction、聊天图片、商品图标 / 封面、订单商品图标快照与论坛 / 评论 / Wiki 正文引用，当前已统一为 `attachmentId` / `attachment://{id}` / `/_assets/attachments/*` 口径；Gateway 也已补显式 `/_assets/attachments/**` 转发，换域名时不再需要手工修改附件类数据库字段
- [x] 已完成 `2026-03-26` 首版烟雾联调：WebOS 桌面与应用容器、论坛基础、社区 P0、Console V1，以及 `radish.client` 国风视觉基线 / 主题切换 / i18n 当前均已按首版视角复核通过，用户确认“全部都没啥问题，可以收口了”
- [x] 已补首版 `dev` 总回归记录：新增 [首版 dev 总回归记录](/guide/dev-first-regression-record)，统一沉淀 `2026-03-23 ~ 2026-03-26` 的 Smoke 结论与当前工程判断
- [x] 已复跑 `npm run validate:baseline`：前端类型检查、`radish.client` 最小测试、Console 权限扫描、身份语义扫描、后端构建与 `Radish.Api.Tests` 当前均已通过
- [x] 已补跑 `npm run validate:baseline:host`：`DbMigrate doctor / verify` 只读自检当前均已通过，验证基线已完成本轮收口
- [x] 已完成 `v26.3.2-test` 的真实测试部署验收：tag 驱动镜像构建、`GHCR` 拉取、`base + test` 启动、登录 / 回调 / 权限 / 核心页面以及 `DbMigrate` 初始化链路当前均已通过，测试部署链路已完成本轮收口
- [x] 已完成 `v26.3.2-release` 的真实发布与生产口径部署验收：release 镜像产出、`base + prod` 启动、登录 / 回调 / 权限 / 核心页面当前均已通过，正式发布链路已完成本轮收口
- [x] 已确认 `radish-dbmigrate` 完成首次真实拉取、初始化与容器化部署验收，不再停留在“等待下一次规范 tag 验证”的状态
- [x] 已完成身份语义 Phase 4 启动前提确认的第一份事实资产：新增 [身份语义 Phase 4 协议消费者矩阵](/guide/identity-claim-protocol-consumers)，当前已明确 `radish-client`、`radish-console` 与 `Radish.Api.AuthFlow.http` 属于最关键的直接协议消费者
- [x] 已完成身份语义 Phase 4 启动前提确认的第二份事实资产：新增 [身份语义 Phase 4 历史 Claim 保留矩阵](/guide/identity-claim-retention-matrix)，当前已明确标准长期保留字段与历史双写字段的收缩方向
- [x] 已完成身份语义 Phase 4 启动前提确认的第三份事实资产：新增 [身份语义 Phase 4 实施与回滚窗口](/guide/identity-claim-phase4-rollout-window)，当前已明确输出收缩顺序、官方回归顺序与默认回滚优先级
- [x] 已完成身份语义 Phase 4 最终启动评审：新增 [身份语义 Phase 4 最终启动评审](/guide/identity-claim-phase4-start-review)，当前已明确“仓库内输入已齐，且当前部署范围内的仓库外兼容边界已被事实关闭，因此允许正式启动实施”
- [x] 已补身份语义 Phase 4 仓库外兼容边界确认清单：新增 [身份语义 Phase 4 仓库外兼容边界确认清单](/guide/identity-claim-external-compat-checklist)，把外部脚本、网关映射、独立调用方与第三方客户端的确认项拆成可执行检查项
- [x] 已补身份语义 Phase 4 仓库外兼容边界首轮执行记录：新增 [身份语义 Phase 4 仓库外兼容边界首轮执行记录（仓库资产侧）](/guide/identity-claim-external-compat-first-pass)，并已结合当前生产环境事实补齐“单套 `v26.3.2-release` Docker 部署 + 1Panel 默认 HTTPS 反代 + 默认客户端种子 + 无仓库外脚本”的外部边界结论
- [x] 已完成身份语义 Phase 4 第 1 批实施：`AccountController / AuthorizationController` 当前已停止 `ClaimTypes.NameIdentifier / ClaimTypes.Name / ClaimTypes.Role / TenantId` 等历史双写输出，并补齐最小控制器回归测试
- [x] 已完成身份语义 Phase 4 第 2 批实施：`UserInfoController` 当前已在保持 `sub / name / email / role / tenant_id` 对外结构稳定的前提下完成最小对齐，并补齐标准优先 / legacy fallback 的最小测试
- [x] 已完成官方回归资产首轮收口：`radish-client / radish-console` 的 Token 直读规则当前已提取为可测试解析模块并锁定“标准优先 + 输入兼容 fallback”；`Radish.Api.AuthFlow.http`、`Scalar` 联调提示与鉴权文档也已同步到当前 Phase 4 口径
- [x] 已完成身份语义 Phase 4 官方顺序真实回归：`radish-client`、`radish-console`、`Radish.Api.AuthFlow.http` 与 `radish-scalar` 当前均已按实施窗口要求完成端到端验证，授权流程与权限链路均正常，本轮无需触发回滚
- [x] 已完成论坛置顶首批最小闭环：当前复用 `Post.IsTop`，管理员可在帖子详情区执行置顶 / 取消置顶，列表排序继续沿用既有置顶优先规则
- [x] 已完成聊天室图片“先入草稿、后统一发送”改造：图片上传后会先进入当前频道草稿区，支持继续输入文字、移除待发图片、切换频道后按频道恢复，再在点击发送时与文字一起发出
- [x] 已修复论坛发帖分类摘要不同步问题：发帖分类选择与顶部“帖子设置”摘要当前已统一走同一条 `categoryId + categoryName` 状态链，初选、切换、清空与恢复草稿时都会即时同步
- [x] 已补论坛发帖缺失项提示与标签确认引导：发布按钮在条件未满足时会展开设置区、提示缺失项并聚焦首个阻塞字段；标签输入只有进入已选列表才算生效，精确匹配现有标签时失焦可自动补入
- [x] 已补欢迎 App 游客安全模式与站点图标配置：未登录用户当前也可直接打开欢迎 App；SystemConfig 已支持 favicon 上传 / 恢复默认，并通过公开站点设置接口同步到 `radish.client / radish.console` 标签页图标
- [x] 已同步论坛 / 聊天设计文档：论坛功能说明、分类 / 标签专题、聊天室 App 总览 / UI 模块 / 路线图 当前已对齐到帖子置顶、图片草稿发送与分类摘要同步修复的最新实现
- [x] 已完成一轮社区技术债治理补强：当前用户缓存已与 access token 身份强绑定，切账号 / 租户 / token 会话时不再复用旧用户资料；登录态回退链路也已收紧为“仅保留与当前 token 同一身份的 store 用户”
- [x] 已对认证登录链路补一轮稳定性治理：`Account/Login` 当前已改为登录名精确查询，登录页提交后会立即禁用按钮并显示“登录中...”，SQLite 主库也已补 `busy_timeout / synchronous=NORMAL / WAL` 初始化与慢连接、慢查询、慢命令观测；旧库缺失的 `idx_user_login_active` 会由 `DbMigrate` 自动补齐
- [x] 已完成论坛首屏慢链路首轮治理：论坛首页已去掉前端串行阻塞，帖子列表改走避免长文本拖慢 SQLite 的专用查询路径，论坛热点索引也已补 `DbMigrate` 自愈
- [x] 已完成日志系统新一轮排障收口：文件日志当前已恢复按宿主项目写入 `Logs/{ProjectName}`，数据库日志口径已明确继续共享 `ConnId=Log`，`SkipTables` 在 `INSERT INTO` 场景下的失效与 SQL AOP 模板写入导致的 Serilog SelfLog 异常也已修复
- [x] 已统一禁用附件的公开访问判定：`/_assets/attachments/*` 与附件访问令牌下载当前都已在服务层统一校验 `IsEnabled && !IsDeleted`，避免 disabled 附件继续对外可访问
- [x] 已将个人中心 `GetUserStats` 收口为数据库聚合：帖子 / 评论数继续保持原返回结构，点赞统计已改走 `QuerySumAsync`，避免全量物化帖子与评论
- [x] 已完成论坛热点首批数据库化收口：全部帖子与投票帖子视图下的 `hottest` 排序当前已改为数据库排序分页；子评论懒加载也已补 `IsEnabled` 过滤，避免 disabled 子评论重新暴露
- [x] 已完成帖子列表最近互动人查询下沉：`FillPostAvatarAndInteractorsAsync` 当前已从 `PostController` 私有全量评论查询迁到服务 / 仓储侧批量读模型，按页帖子只查“每帖最近互动作者 Top N”，不再整页物化全部评论
- [x] 已完成评论详情主链路分页化：论坛帖子详情当前已改为“根评论分页 + 子评论懒加载”正式契约，前端主链改走 `GetRootComments`，替换整帖评论全量拉取后再组树的实现
- [x] 已完成旧评论树入口正式退役：`CommentController.GetCommentTree`、`ICommentService.GetCommentTreeWithLikeStatusAsync`、`CommentService.GetCommentTreeWithLikeStatusAsync` 与两个观察用 `HttpTest` 条目当前已删除；论坛评论主链仅保留根评论分页 + 子评论懒加载正式契约
- [x] 已完成论坛正文图片坏图链路收口：`MarkdownRenderer` 当前已统一放行 `attachment://` 与 `sticker://` 协议，数据库重建或重新发帖后不再因渲染层过滤导致正文图片变成坏图
- [x] 已完成论坛发帖正文 / 评论 / 聊天室输入的 `@提及` 联想补齐：发帖正文真实入口、评论编辑器与聊天室输入框当前都已提供匹配用户名列表、键盘确认与插入反馈
- [x] 已完成评论区与聊天室粘贴图片能力补齐：剪贴板图片当前可直接上传并进入对应输入链路，评论与聊天不再要求先手动保存到本地再选图
- [x] 已完成编辑帖子编辑器样式收口：编辑场景当前默认进入正文编辑态，并修复仅剩单行窄输入区的样式回归
- [x] 已完成通知头像与登录后资料刷新体验补齐：通知中心当前会为缺失 `voTriggerAvatar` 的通知补公开资料头像兜底；OIDC 登录回调完成后会先预热当前用户资料，再返回桌面刷新壳层头像与等级信息
- [x] 已完成聊天室图片发送反馈收口：图片发送成功后当前不再额外弹成功提示，仅保留失败提示，避免高频发送场景出现冗余打断

### 下一步拆分

- 第 1 步：固定 `M14` 的阶段定义、执行入口与周志口径，避免“规划写的是 M14，执行还按旧主线”再次出现
- 第 2 步：默认先走 [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist)，把 `validate:baseline:host` 固定为第一轮宿主验证入口
- 第 3 步：把 `DbMigrate doctor / verify -> 健康检查 -> 日志 -> Gateway / 反代 / OIDC 链路` 的排障顺序保持为单一口径，不再散落到多份文档
- 第 4 步：继续维持当前统一镜像推送、`local / test / prod`、`DbMigrate -> Api/Auth -> Gateway` 与 `AuthUi__ShowTestAccountHint` 的发布口径冻结状态，避免部署事实再次漂移
- 第 5 步：把测试部署与生产部署的最小复核动作统一回链到 [部署与容器指南](/deployment/guide) 与 `M14` 清单，不再把部署排障与代码回归混在一起
- 第 6 步：保持身份语义 `Phase 4` 的稳定维护边界；若后续出现新的部署环境、第三方客户端或自定义反代规则，再回到 [身份语义 Phase 4 仓库外兼容边界确认清单](/guide/identity-claim-external-compat-checklist) 追加事实确认
- 第 7 步：旧 `GetCommentTree` 兼容入口已完成正式退役；后续论坛评论相关改动默认只围绕 `GetRootComments + GetChildComments` 主链回归，不再把旧入口保留为观察项
- 第 8 步：继续把 `Repo Quality / validate:ci / Identity Guard / 受限环境边界` 的排查口径维持在 [Repo Quality 故障分诊手册](/guide/repo-quality-troubleshooting) 单一入口中维护，避免脚本说明与故障分流再次散落

### 今日整理结果（2026-04-05）

#### 维护池

- 身份语义 Phase 4 首轮实施与官方回归结果
- 聊天室 `P1`
- 通知中心
- `Console-ext` 一期
- `P3` 投票 MVP
- `P4` 问答 MVP 与 `P4-ext` 首轮闭环
- `P5` 抽奖 MVP 与个人中心浏览记录优化
- `DbMigrate` 当前 `doctor` / `verify` 与宿主解耦成果

#### 观察池

- 身份语义外部兼容边界：仅在部署形态、第三方客户端或反代规则变化时补事实，不预设新增实施
- `Repo Quality / validate:ci / Identity Guard` 默认执行面：继续维持同源规则与单一故障分诊入口

#### 后置池

- `P3-ext`、`P4-ext`、`P5-ext` 的进一步增强
- `Console-ext Phase 2+`
- `P2-ext Auto` 开源软件清单自动生成 / 发布物公告
- 治理前端化、分发增强、邮件通知系统

#### 下一主线入口判断

- 今天不直接启动新的功能主线，也不把已收口专题重新拉回当前页继续膨胀
- `M14` 当前已从候选入口切换为正式主线：宿主运行与最小可观测性基线
- `M13` 与 `M15` 继续保留在候选池中，但当前不改写为已启动阶段

### 当前结论

- 社区主功能收口只是首版 `dev` 的一部分，不再代表整产品已经达到首版边界
- 当前主线已从“身份语义 Phase 4 协议输出收敛”进一步切换为“`M14` 宿主运行与最小可观测性基线”
- 当前已完成 Phase 4 首轮仓库内实施、官方顺序真实回归与回滚窗口验证：Auth 输出双写已收缩、`userinfo` 已完成最小对齐、官方客户端直读规则与联调示例已完成同步，`radish-client / radish-console / Radish.Api.AuthFlow.http / radish-scalar` 当前均已通过真实回归，结论更新为“无需回滚，转入稳定维护”
- 截至 `2026-04-05`，当前进一步确认 `radish-client / radish-console / Radish.Api.AuthFlow.http / radish-scalar` 官方顺序真实回归均已通过，Phase 4 本轮已正式收口；后续不再继续扩张实施项，而是优先维持维护态，并转入 `M14` 第一轮执行入口、排障顺序与最小部署复核口径收口
- 截至 `2026-04-05`，`M14` 第一轮主路径也已从“入口与报告口径收口”进一步推进到“宿主健康语义与启动日志摘要收口”：当前可先看启动日志核对 JWT / OIDC / Gateway 探活模式，再看 `/health`、`/healthz` 做最小分诊
- 截至 `2026-04-06`，旧 `GetCommentTree` 兼容入口的正式删除批次已完成：兼容控制器、服务契约与观察用 `HttpTest` 均已移除；后续不再继续维护旧评论树兼容面，论坛评论主链只保留分页契约
- 通知中心已于 `2026-03-23` 完成一轮真实首版 Smoke，并已在本轮总回归中完成复核；当前转入稳定维护
- 认证基础入口虽已于 `2026-03-23` 完成一轮真实首版 Smoke，但 `2026-03-30` 又针对后台闲置恢复、慢登录与重复提交场景补了一轮治根治理；截至 `2026-04-02`，相关回归已完成当前批次确认，当前转入稳定维护
- 论坛首页与日志链路已于 `2026-03-31` 再完成一轮治根收口：论坛首屏已去掉前端串行阻塞并补专用查询路径，日志目录已恢复到 `Logs/{ProjectName}`，`SkipTables` 与 SQL AOP 模板异常当前也已对齐到可持续排障的状态
- 社区输入与资料展示体验已于 `2026-04-01` 完成一轮补齐收口：论坛正文图片坏图、发帖 / 评论 / 聊天室 `@提及`、评论 / 聊天室粘贴图片、通知头像兜底、登录后资料预热，以及聊天室图片发送冗余成功提示等问题当前均已对齐到最新实现
- 欢迎 App 已于 `2026-03-23` 完成长文案与开源说明资源化，当前语言切换已覆盖欢迎页主体内容
- 从 `2026-03-19` 起，国风视觉基线 / 主题切换 / `radish.client` i18n 明确纳入首版范围
- Docker 镜像构建链已于 `2026-03-25` 完成最小交付收口：build 级验证、开发运行与 `local / test / prod` 三套容器口径拆分、Gateway 容器内 HTTP / HTTPS 模式切换，以及 `RADISH_PUBLIC_URL` 驱动的官方 OIDC 回调地址链路均已完成，当前转入总回归前维护
- 附件业务口径已于 `2026-03-29` 完成“去 URL 真值化”收口：正文引用、媒体资源公开路径、Gateway 转发与换域名运维边界当前已对齐到同一口径，不再要求手工更新附件类数据库数据
- WebOS 桌面与应用容器、论坛基础、社区 P0、Console V1，以及 `radish.client` 国风视觉基线 / 主题切换 / i18n 已于 `2026-03-26` 完成一轮首版烟雾联调，并已在本轮总回归中完成收口确认；当前转入稳定维护
- `GHCR` 后端镜像首次真实产物验证已完成，当前已确认 `docker pull` 可用
- `GHCR` 前端镜像首次真实产物验证已完成，当前已确认 `docker pull` 可用
- `v26.3.2-test` 已完成真实测试部署验收，`v26.3.2-release` 也已完成正式发布与生产口径部署验收；当前已确认远程镜像拉取、容器启动、登录与核心链路均可用，`dbmigrate` 容器初始化链路已完成真实验证
- 前端镜像当前已收口为轻量多阶段运行时镜像，本地构建验证体积约 `300MB`
- 当前首版剩余重点已从“验证基线收口 + 社区主线优化”进一步切换为“保持发布链稳定维护 + 重审下一里程碑入口”；前端运行时配置注入、统一推送规则、测试部署、生产部署与镜像体积优化，以及社区主链回归当前均已完成本轮确认
- 本轮社区主链回归已达到“可转维护、可结束本轮主线”的状态；当前下一阶段已进一步明确为 `M14` 宿主运行与最小可观测性基线，而不是继续在本页内无边界膨胀社区增强项
- 首版总回归与发布前检查单：见 [首版 dev 总回归与发布前检查单](/guide/dev-first-regression-checklist)
- 首版总回归记录：见 [首版 dev 总回归记录](/guide/dev-first-regression-record)
- 上线前交付复核入口：见 [部署与容器指南](/deployment/guide)
- 首版 `dev` 是否达标，以 [首版 dev 边界](/planning/dev-first-scope) 为准，而不再以单一专题收口状态替代整体判断

### 当前不做

- [ ] 直接按旧版 `M13` 口径启动“可观测性与测试”整包工程
- [ ] 把当前 `M14` 直接扩写成完整运维平台、完整可观测性平台或大而全 CI / E2E 工程
- [ ] 继续把 `P3-ext`、`P4-ext`、`P5-ext` 的非阻塞细节增强当作当前主线持续推进
- [ ] 把 `Reaction Phase 2`、开放平台第三方接入 / SDK、邮件通知系统一并拉进当前首版边界
- [ ] 在未冻结首版功能矩阵前，同时并行启动多个新的横向治理项目

## 刚完成主线：P5 论坛抽奖 MVP + 个人中心浏览记录优化

### 本阶段目标

- 在投票与问答已完成收口的基础上，继续补一个与论坛主链紧密耦合、体量可控的活动型能力
- 保持“列表轻摘要、详情重交互”的论坛边界，不拆独立抽奖应用
- 通过复用现有评论区父评论，完成抽奖参与闭环，避免新增独立报名机制
- 并行补个人中心浏览记录首版，为用户侧提供最近浏览内容回看入口

### 当前输出

- [x] 已确认采用“论坛帖子附带抽奖”路径，不拆独立抽奖 App
- [x] 已确认本轮参与方式为“发布父评论参与”，子评论 / 回复不计入抽奖资格
- [x] 已确认本轮开奖方式为“发帖者手动开奖”，不先引入自动开奖任务
- [x] 已补抽奖方案文档：`forum-lottery-mvp.md`
- [x] 已确认个人中心浏览记录首版范围：帖子详情、商品详情、文档页
- [x] 抽奖后端模型、DTO / Vo 与详情返回扩展
- [x] 抽奖开奖接口与最小回归测试
- [x] 欢迎 App 中论坛发帖弹窗、帖子列表与帖子详情抽奖交互（提交：`5c80415`）
- [x] 个人中心浏览记录采集、展示与帖子 / 商品 / Wiki 跳转联动（提交：`26d67f3`）
- [x] `HttpTest` 与人工验收脚本补齐（提交：`c365fc1`）
- [x] 收口补丁已完成：开奖中奖通知、论坛通知点击复用窗口、帖子 / 商品浏览记录补录、抽奖通知展示优化（提交：`749e8c3`、`91532ef`）

### 收口后状态

- 当前最小实现、验收资产与联调补丁均已齐备，可从“当前进行中”移出
- 如后续继续推进，统一转入 `P5-ext`，不再把自动开奖、奖励联动、防刷公示提前拉回当前主线

### 当前结论

- `P5` 已达到“可演示、可联调、可回归、可转维护”的收口标准
- 浏览记录当前范围继续保持克制，不扩张为完整足迹分析系统
- 后续若要继续深挖抽奖能力，应以新阶段单列，而不是继续占用当前主线

### 当前不做

- [ ] 独立抽奖应用 / 活动页
- [ ] 自动开奖定时任务
- [ ] 奖励 / 萝卜币 / 商城联动
- [ ] 浏览记录的跨站外链深度跟踪

## 刚完成主线：P4-ext 论坛问答增强（首轮最小闭环）

### 本阶段目标

- 在已完成的论坛问答 MVP 基础上，补齐首轮最小增强范围，不扩张成完整问答平台
- 优先完成问答视图、问答排序与问题历史入口，形成一个新的最小可用闭环
- 保持“列表轻摘要、详情重交互”的论坛边界，不拆独立问答应用
- 继续让 `Console-ext`、OIDC 与开源软件清单维持稳定维护，不并行膨胀多个主线

### 当前输出

- [x] 已新增论坛帖子列表问答视图切换：支持在 `全部 / 问答` 之间切换
- [x] 已新增问答状态筛选：支持 `全部状态 / 待解决 / 已解决`
- [x] 已新增问答专用排序：支持 `最新 / 待解决优先 / 回答数`
- [x] 已新增问答详情回答排序切换：支持 `默认排序 / 最新回答`
- [x] 已新增问答详情回答轻筛选：支持 `全部回答 / 只看已采纳`
- [x] 已补问题历史入口：问答帖详情中可直接查看“问题历史”，并复用现有帖子编辑历史能力
- [x] 已补问题历史最小回归测试：覆盖 `GetEditHistory`、编辑写入历史与历史分页查询，降低“入口已接通但链路缺少自动化兜底”的风险
- [x] 已完成手工回归：问答视图、列表排序、详情回答区切换、轻筛选、问题历史与采纳链路当前无明显阻塞
- [x] 已完成 `HttpTest` 目录规范化：论坛、附件、限流脚本已按主题拆分，过期测试指南与报告已清理
- [x] 已同步最小必要文档口径，避免规划仍停留在“当前主线仍进行中”

### 当前结论

- `P4-ext` 首轮最小闭环已完成，不再继续在本轮扩张成完整问答平台
- 当前优先保持文档、测试脚本与问题修复对齐，不提前展开新的下一阶段规划
- 收口后补丁已完成：附件清理误判已修复，`Serilog` 文件锁冲突已缓解，受影响的聊天图片与问答回答图片已于 `2026-03-15` 手工恢复
- 后续若继续深挖问答能力，应重新单列更深增强项，而不是重复占用“当前主线”

### 当前不做

- [ ] 把 `Console-ext` 一期继续膨胀为完整 IAM 平台
- [ ] 立即推进开源软件清单自动生成 / 发布物公告
- [ ] 在未确认优先级前并行启动多个新主线

## 上一主线：P0 权限冻结后的短回归观察期（已完成）

### 本阶段目标

- 确认 `Console` 权限治理 V1 进入“冻结边界、只做回归维护”状态
- 对已治理页面进行短周期观察，确认不再出现新的权限裂缝
- 确认聊天室 `P1`、论坛投票 MVP 与论坛问答 MVP 均已达到“可转出当前主线”的收口标准
- 在 `P2-ext`、`P3-ext`、`P4-ext` 与 `P4-alt` 之间选定下一条更适合当前阶段的主线

### 收口清单

#### 已完成

- [x] 路由、菜单、搜索权限同源化
- [x] 页面级重复判断收口到 `RouteGuard`
- [x] `Dashboard / Applications / Users / Roles / Products / Orders / Tags / Stickers / SystemConfig / Hangfire` 页面权限闭环
- [x] `Dashboard` 权限种子闭环
- [x] `Hangfire` 权限种子闭环
- [x] `Users` 误暴露权限入口收口
- [x] `Products / Stickers` 真实在用辅助接口资源种子补齐
- [x] `Attachment/UploadImage` 共享接口边界已按方案 B 完成最小收口
- [x] 轻量扫描脚本 `npm run check:console-permissions` 已落地
- [x] 权限治理下一步口径已切换为“冻结边界后的回归维护”

#### 进行中

- [x] 短回归观察期内确认未出现新的权限裂缝
- [x] 对聊天室 `乐观发送 + 失败重试` 进行短回归确认
- [x] 修复聊天室服务端长整型 ID 精度问题，消除撤回 / 引用回复 / 历史分页的假失败
- [x] 修复聊天室输入区 `textarea` 挤压“图片 / 发送”按钮的问题
- [x] `P2` 最小首版已在欢迎 App 落地，提供项目级核心依赖与许可证说明入口
- [x] `P3` 已确认选择“论坛帖子附带投票”作为最小首版方向
- [x] 论坛投票后端第 1、2 步已完成：模型 / 契约打底 + 发帖链路接入投票创建
- [x] 论坛投票第 3、4 步已完成：投票接口 / 结果查询 / 前端详情与发帖交互已接通
- [x] 帖子列表投票摘要查询已改为批量回填，逐条补详情导致的 `N+1` 风险已收口
- [x] 论坛投票第 5 步已完成：补充回归测试、修复短时截止时间 UTC 口径问题，并完成 MVP 手工验收
- [x] 论坛问答 MVP 已完成：问答帖发布入口、列表 / 详情识别、回答提交、提问者采纳、详情交互反馈与 Markdown 回答输入均已联调收口
- [x] 论坛投票 / 问答列表卡片展示口径已统一：左侧只保留类型 / 状态标签，数量统一放入右侧统计区
- [x] 问答采纳异常语义已补齐：`AggregateException` 包装下的业务拒绝场景可稳定返回 400 / 403 / 404，不再误落 500
- [x] 聊天室 `P1` 已完成一轮手工冒烟联调，断线补拉、失败重试、图片链路、成员面板与头像展示当前无明显阻塞问题
- [x] 已确认聊天室与权限主链路进入稳定维护，可推进下一候选主线

#### 历史上的下一执行顺序

- `P4-ext`：论坛问答扩展（问答视图、排序与历史能力）
- `P3-ext`：论坛投票扩展（更完整测试、筛选 / 排序扩展）
- `M13`：验证基线与回归资产工程化（后续已完成重排）
- `P4-alt`：抽奖最小首版（后置评估）
- `Console-ext Phase 2+`：更完整权限中心、审计与共享接口治理
- `P2-ext Auto`：开源软件清单自动生成 / 发布物公告
- `Later`：邮件通知系统

#### 明确不做

- [ ] 完整权限树编辑器 / 大而全权限平台
- [ ] 系统监控大盘扩张
- [ ] 审计日志中心扩张
- [ ] 一次性纳管所有登录态共享接口

说明：以上是上一主线收口当时的历史结论；当前这类顺序仅保留为历史上下文，不代表本页此刻已启动新的当前主线。

### 退出条件

- 短回归观察期内未再出现新的前后端权限裂缝
- 权限链路变更已具备脚本化自检入口并完成一次实际回归使用
- 新功能开发不再继续横向扩张权限模型，仅做增量补齐
- `聊天室 P1` 可以在不返工权限主链路的前提下启动

### 当前结论

- `Console` 权限治理 V1 已按既定边界完成，进入“冻结边界、只做回归维护”
- 聊天室 `P1`、论坛投票 MVP、论坛问答 MVP 已完成本轮收口，可从当前主线移出
- `Console-ext` 一期已完成本轮联调与补边界，可转入稳定维护
- `P2-ext` 的内容扩展版已完成，不再作为当前主线；更重的自动生成版后置
- `P4-ext` 首轮最小闭环也已完成；当前不在本页提前指定新的下一实现入口

## 并行治理尾项

### 身份语义收敛尾项

- **当前状态**：主体已完成，Phase 4 首轮实施、官方顺序真实回归与回滚窗口验证均已完成，当前转入稳定维护
- **剩余任务**：
  - Phase 4：协议输出收敛（当前已完成 [最终启动评审](/guide/identity-claim-phase4-start-review) 与首轮官方回归；后续若部署形态变化，再按 [仓库外兼容边界确认清单](/guide/identity-claim-external-compat-checklist) 追加事实）
  - Phase 5：防回归资产接入脚本 / 校验流程（当前已完成首轮工程化入口、单一故障分诊入口，以及“命中原因 + 失败归类”记录闭环；后续再视项目阶段决定是否继续扩张到更重的 CI/CD / 宿主专题）

### 工程治理

- `DbMigrate` 解耦宿主已完成本轮收口（`Bootstrap` / `Runner` / `Doctor`）
- `DbMigrate doctor` / `verify` 已可用于只读检查配置、连接定义与 Seed 前置状态
- 持续减少“组合根直接绑宿主”的结构耦合

## 后续候选

> 当前主线已切换到 `M14 宿主运行与最小可观测性基线`，以下只保留后续候选池。

### `P1`：聊天室完善

- 已完成：`@mention`、图片上传 UI、引用回复、断线补拉与状态条、成员头像来源、草稿恢复、成员面板
- 已完成：`乐观发送 + 失败重试`
- 已完成：服务端字符串 ID / 本地临时负数 ID 契约对齐、输入区布局修复
- 已完成：一轮手工冒烟联调，断线补拉、失败重试、图片发送、成员面板与头像展示整体正常
- 当前状态：核心交互已补齐，当前无明显阻塞问题，已具备从短观察切换到下一候选主线的条件

### `P2`：开源软件清单声明组件

- 最小首版已落地到欢迎 App，当前提供项目级核心依赖、许可证与官方仓库入口
- 内容扩展版已完成：当前已覆盖更完整的静态清单与分类展示
- 当前实现仍采用静态清单，范围保持克制，不引入构建期自动扫描
- 若后续继续推进，则升级为“自动生成 / 发布物公告”后续阶段，并后置到首个 dev 版本之后再评估

### `P3`：抽奖 / 投票 / 问答最小首版

- 已确认先做“论坛帖子附带投票”，不拆独立投票应用
- 已完成 MVP 收口：
  - 投票主体 / 选项 / 投票记录实体与 DTO / Vo 契约
  - 帖子发布链路附带投票创建
  - `PollController`、投票提交与结果查询接口
  - 欢迎 App 中论坛发帖与详情页投票交互
  - 帖子详情 / 列表最小投票摘要回传
  - 列表卡片标签与数量展示口径统一
  - 帖子列表投票摘要批量回填，`N+1` 风险已收口
  - 截止时间 UTC 口径修复与最小回归补齐
  - 一轮手工冒烟验收通过
- 当前状态：`P3` 最小首版已完成，可从当前主线移出；`P3-ext` 仅保留为首个 dev 版本后的候选增强，不再占用当前主线

### `P4`：论坛问答 MVP

- 已完成 MVP 收口：
  - 问答帖发布、回答提交、提问者采纳最小闭环
  - 帖子列表问答状态与回答数摘要
  - 帖子详情回答区、最佳答案高亮与作者资料入口
  - 回答输入复用 Markdown 编辑器，支持附件与贴图
  - 采纳接口异常语义与列表卡片展示口径已完成收口
  - 一轮手工联调通过
- 当前状态：`P4` 最小首版已完成；`P4-ext` 后续仅在首个 dev 版本之后按优先级单独排期

### `P5`：论坛抽奖 MVP

- 已确认先做“论坛帖子附带抽奖”，不拆独立抽奖应用
- 已确认当前最小闭环：
  - 发帖附带抽奖
  - 父评论参与
  - 按用户去重统计参与资格
  - 发帖者手动开奖
  - 列表轻摘要与详情中奖名单
- 已补齐收口补丁：中奖通知、论坛通知窗口复用跳转、帖子 / 商品浏览记录补录、中奖通知展示优化
- 当前状态：`P5` MVP 已完成本轮收口；`P5-ext` 仅保留为首个 dev 版本后的候选增强

### `Console-ext`：权限配置后台与授权界面

- 当前状态：一期已完成本轮收口，`Console-ext Phase 2+` 后续转入首个 dev 版本后的 backlog
- 当前专题： [Console 权限 / 菜单 / 按钮管理一期设计方案](/guide/console-authorization-phase1)
- 一期范围：优先补角色授权面板，以及菜单 / 页面 / 按钮授权配置
- 当前边界：先不承诺完整权限树编辑器或大而全 RBAC 平台

## 当前不做

- 治理前端化（审核台、敏感词、自动策略）
- 分发增强（复杂召回 / 排序 / 观测压测）
- 邮件通知系统
