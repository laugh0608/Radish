# 开发路线图（总览）

> 本页只保留 **里程碑总览、当前主线、下一阶段入口**。
>
> 详细执行项请分别查看：
>
> - [当前进行中](/planning/current)
> - [未来规划 / Backlog](/planning/backlog)
> - [已完成摘要](/planning/archive)
> - [开发日志](/changelog/)

## 当前状态

- **当前里程碑**：`M14 宿主运行与最小可观测性基线`
- **当前主线**：`M14 宿主运行与最小可观测性基线`
- **当前阶段**：`截至 2026-04-05，社区主链多轮回归与首版 dev 文档收口已完成；身份语义 Phase 4 的仓库内前置资产、仓库资产侧首轮排查、首轮实施窗口与官方顺序真实回归也均已完成。当前结论已进一步收束为：Auth 输出双写已收缩、`userinfo` 已完成最小对齐，`radish-client -> radish-console -> Radish.Api.AuthFlow.http -> radish-scalar` 已按既定顺序完成端到端验证，无需触发回滚，Phase 4 本轮正式收口并转入维护态。当前主线已正式切换为 `M14`，且“启动前 `validate:baseline:host` -> 启动后 `check:host-runtime`”的两段主路径、统一报告口径与最小运行态检查入口均已完成首轮收口。详见：[M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline)。`
- **并行治理尾项**：
  - 身份语义 Phase 4：稳定维护与防回归治理准备（当前已完成 [最终启动评审](/guide/identity-claim-phase4-start-review)、首轮实施与官方顺序真实回归，结论为无需回滚并转入稳定维护）
  - `DbMigrate` 进入回归维护（解耦宿主 + `doctor` 校验已完成）

## 当前关注（2026-04-05）

### 当前阶段原则

1. **先主线，后细节**
   - 当前优先完成真正影响首版 dev 的核心功能主线、体验主线与工程门槛，不再把局部增强与次级治理持续拉成当前主线。

2. **先闭环，后打磨**
   - 主线优先达到“用户任务完整走通”的 70% 闭环标准，再决定哪些细节放到首版 dev 之后继续打磨。

3. **不留坑，不留技术债**
   - 允许实现保持克制，但不允许把会阻塞首版 dev 或会导致后续立刻返工的结构性问题留到后面。

4. **体验主线纳入首版，但要收敛范围**
   - 国风视觉基线 / 主题切换 / `radish.client` i18n 已纳入首版范围，但按“核心页面先落地、先做稳定可用”推进，不扩张成全站治理工程。
   - 当前 `radish.client` i18n 已完成桌面壳层、商城主链路、论坛高频讨论链路与边缘页、文档应用主链，以及聊天、通知中心、个人中心高频模块的首轮覆盖；通知中心 / 个人中心 / 桌面壳层首轮语言与主题烟雾验证也已完成，其中通知中心已完成新通知 Toast 预览、Dock 未读角标、列表、静默已读、删除同步与多端同步的一轮真实 Smoke，欢迎 App 也已完成长文案与开源说明资源化，后续转入更大范围首版联调复核与残余深层样式维护。

5. **非阻塞增强统一后置**
   - `P3-ext`、`P4-ext`、`P5-ext`、`Console-ext Phase 2+`、开放平台第三方接入 / SDK 等增强项统一后置到首版 dev 之后再评估。

### 本轮收口结论

1. **P0 权限冻结后的短回归观察期已完成**
   - `Console` 权限治理 V1 已确认进入“冻结边界、只做回归维护”状态
   - 权限链路变更优先通过 `npm run check:console-permissions` 自检
   - 本轮不再继续横向扩张新的权限族或共享接口映射
   - `权限 / 菜单 / 按钮管理 UI` 已从后续候选切换为 `Console-ext` 一期设计主线

2. **`Console-ext` 一期已完成本轮收口**
   - 已完成“角色授权面板 + 资源目录 + 资源到接口映射”的最小实现
   - 已接通角色授权页、当前已纳管 Console 接口动态权限校验与 Hangfire 权限收口
   - 已将 `console.access` 收口为“入口标记 + 真实后台能力联动”，解决测试用户仅因拥有入口权限就误见 Console 的问题
   - 最新一轮已补齐分类/标签分管，以及内容治理、胡萝卜、经验等级后台首版入口
   - 最新一轮已补齐 Console / Scalar 独立 OIDC 客户端确认页与官方客户端种子口径
   - 专题设计与补充口径见：[Console 权限 / 菜单 / 按钮管理一期设计方案](/guide/console-authorization-phase1)

3. **聊天室 P1 收口完成**
   - `@mention`、图片消息、引用回复、断线补拉、成员头像来源、乐观发送与失败重试已完成
   - 最新一轮已修复长整型 ID 精度问题与输入区布局挤压问题，并完成一轮手工冒烟联调
   - 当前无明显阻塞问题，可从当前主线移出，后续仅做稳定性回归维护

4. **身份治理尾项首轮实施已收口**
   - 当前最终启动评审已完成，且后续首轮实施与官方顺序真实回归也已完成；当前结论已更新为“无需回滚，转入稳定维护”
   - 当前已补 [身份语义 Phase 4 仓库外兼容边界确认清单](/guide/identity-claim-external-compat-checklist) 与 [首轮执行记录（仓库资产侧）](/guide/identity-claim-external-compat-first-pass)，并已补齐当前生产环境事实
   - 当前已完成首轮仓库内实施：`AccountController / AuthorizationController` 已停止历史双写输出，`UserInfoController` 已完成最小对齐，`radish-client / radish-console` 的 Token 直读规则也已提取为可测试解析模块
   - 当前 `Radish.Api.AuthFlow.http`、`Scalar` 联调提示与鉴权文档已同步到最新口径；官方回归资产不再停留在旧 scope、旧密码或旧双写说明
   - 当前已按官方顺序完成 `radish-client -> radish-console -> Radish.Api.AuthFlow.http -> radish-scalar` 真实端到端回归，授权流程与权限链路当前均正常，本轮无需触发回滚
   - 当前下一步已从“继续做官方回归”切换为“稳定维护 + 防回归治理准备”；首轮 `Phase 5` 工程化资产也已补齐：`check:identity-runtime`、`check:identity-protocol-output`、`validate:identity`、[身份语义防回归回归手册](/guide/identity-claim-regression-playbook) 与 `Identity Guard` CI 门禁当前已形成同一口径

5. **工程治理**
   - `DbMigrate` 已完成本轮解耦宿主、启动入口收口与 `doctor` 只读校验命令补齐
   - 持续减少“组合根直接绑宿主”的结构耦合

6. **P2 开源软件清单声明组件**
   - 欢迎 App 内的内容扩展版已落地，当前已覆盖更完整的静态依赖清单、分类展示与许可证说明
   - 当前仍采用静态清单方案，避免把中小体量补齐项膨胀为构建治理任务
   - 若后续继续推进，则后置为“自动生成 / 发布物公告”阶段，不再作为当前主线

7. **P3 论坛投票 MVP 已完成验收**
   - 已确认采用“挂在论坛帖子中”的实现路径，不单独拆独立投票 App
   - 数据模型 / DTO / Vo、发帖链路、投票接口、前端详情交互、列表轻量摘要与验收回归已全部收口
   - 列表卡片展示已统一为“左侧类型标签、右侧数量统计”，避免投票与问答在摘要层出现重复数字表达
   - 截止时间临近当前时刻的 UTC 口径问题已修复，手工联调已通过
   - 后续如继续推进，转入 `P3-ext`，并统一后置到首个 dev 版本之后再评估，不再占用当前主线

8. **P4 论坛问答 MVP 与 `P4-ext` 首轮最小闭环均已完成收口**
   - 已按“论坛帖子附带问答”的路径完成问答帖发布、回答提交、提问者采纳与详情交互补齐
   - 回答输入已复用现有 Markdown 编辑器，列表与详情均已补齐问答状态展示
   - 采纳接口已补齐事务包装异常的业务语义映射，“采纳自己的回答”等拒绝场景不再误返回 500
   - `P4-ext` 首轮最小范围已落地：问答视图、问答状态筛选、问答专用排序、回答区排序切换、轻筛选与问题历史入口均已接通
   - “问题历史”关键后端回归已补齐：`GetEditHistory`、编辑写历史、历史分页查询当前已有自动化兜底
   - 手工回归与 `HttpTest` 目录规范化已完成，当前结论为“首轮最小闭环已完成”；后续更深增强统一后置到首个 dev 版本之后再评估
   - 收口后补丁已完成：附件清理任务现会识别聊天图片与 Markdown 内上传引用，`Serilog` 文件写入已补 `shared: true`，受影响附件已完成手工恢复

9. **P5 论坛抽奖 MVP 已完成本轮收口**
   - 已确认采用“论坛帖子附带抽奖”的路径，不拆独立抽奖 App
   - 已确认参与方式采用“发布父评论参与”，回复评论不计入抽奖资格，候选池按用户去重
   - 已确认开奖方式采用“发帖者手动开奖”，不先引入自动开奖定时任务
   - 后端首轮骨架已完成并独立提交：已包含发帖附带抽奖、按帖子取抽奖详情、手动开奖与最小测试
   - 抽奖前端已完成并独立提交：`5c80415`，已接通发帖入口、列表摘要、详情卡片、开奖按钮与中奖名单
   - 抽奖与浏览记录 `HttpTest`、最小人工验收顺序已完成补齐：`c365fc1`
   - 最新联调补丁已完成：开奖中奖通知、论坛通知窗口复用、浏览记录补录与中奖通知展示优化（`749e8c3`、`91532ef`）
   - 当前结论已切换为：`P5` 达到“可演示、可联调、可回归、可转维护”的收口标准；后续如继续推进，则进入 `P5-ext`，并后置到首个 dev 版本之后再评估

10. **个人中心浏览记录优化已纳入本轮并行小项**
   - 个人中心将新增“浏览记录”标签页
   - 第一版先覆盖帖子详情、商品详情与文档页
   - 后端采集与个人中心读取接口已接通，浏览记录前端已完成并独立提交：`26d67f3`
   - 当前已支持个人中心列表、分页展示，以及帖子 / 商品 / Wiki 文档深链跳转
   - 当前范围保持克制：按对象去重，记录最近浏览时间与次数，不扩张到完整行为分析系统

11. **M13 及之后的规划已进入重审**
   - 旧版 `M13` “可观测性与测试”、`M14` “部署与运维”、`M15` “Gateway & BFF” 的定义形成时间较早
   - 结合当前仓库现状，需要先按真实代码、脚本与文档资产重写阶段目标，再决定是否启动；其中 `M14` 当前已先重定义为 [宿主运行与最小可观测性基线](/guide/m14-host-runtime-observability-baseline)
   - 当前优先级是“先完成规划校准”，而不是直接按旧版阶段名进入开发

12. **M13 首轮验证入口已完成最小落地**
   - 根目录已补齐 `npm run validate:baseline`、`validate:baseline:quick`、`validate:baseline:host`
   - 身份语义专题入口已继续收口：当前已补齐 `check:identity-runtime`、`check:identity-protocol-output` 与 `validate:identity`
   - 当前已新增 [身份语义防回归回归手册](/guide/identity-claim-regression-playbook)，明确“身份语义相关改动后默认必须跑什么”、哪些继续留在本地专题层、哪些已适合上 CI
   - 已补验证基线说明页，明确“日常提交前 / 合并前 / 宿主配置相关改动后”的分层使用建议
   - 最新已继续补齐身份语义 impact 判定的工程一致性：`check:identity-impact` 当前已统一为单一规则源，并已纳入 `validation-baseline / regression-index / dev-first-regression-record / development-plan / planning/current / PR template` 等默认执行面文档与门禁资产；本地 `validate:ci` 也已对齐为 `Repo Quality` changed-only + 条件 `validate:identity`
   - 最新已继续补齐默认执行面：`.githooks/pre-commit` 当前已改为直接调用 `check:repo-hygiene:staged` 与 `lint:staged`；PR 模板、回归记录模板与专题回归索引也已同步纳入 `validate:ci` 口径，避免脚本、hooks 与记录模板各说各话
   - 最新已补 `Repo Quality` contract 自校验：新增 `check:repo-quality-contract` 与共享 contract 模块，当前会自动核对 workflow job 名、ruleset required checks 与本地 `validate:ci` 门禁契约；`validate:baseline` / `quick` 也已接入，避免 required checks 与默认执行面再次无声漂移
   - 最新已继续补齐 workflow 语义守卫：contract 当前也会校验 `repo-quality.yml` 中四个 job 的关键命令片段，避免 changed-only、impact 判定或条件 `validate:identity` 在名称不变时无声漂移
   - 最新已补 Windows 共享执行层兼容性收口：默认脚本当前已统一走共享 `process-runner`，尽量避免再手写 `cmd.exe /c`；若当前环境从 Node 脚本层面禁止再拉起外部进程，也会输出统一边界提示，避免误判为门禁脚本本身回归
   - 最新已补单一故障分诊入口：新增 [Repo Quality 故障分诊手册](/guide/repo-quality-troubleshooting)，把 contract 漂移、`validate:ci` 默认执行面失败、身份语义条件触发、受限环境边界与历史 warnings / DLL 锁分流到同一页，降低后续维护继续依赖脚本细节记忆的成本
   - 最新已补 `Phase 5` 记录闭环：`check:identity-impact` 当前会输出命中原因类别，`validate:ci`、PR 模板、变更回归记录模板与回归结论记录模板也已统一要求记录“为什么命中身份语义影响面 / 失败属于哪一类”，避免 `Identity Guard` 与维护记录继续靠口头解释
   - 已完成一轮 `full` 验证：前端 `type-check`、`radish.client` 最小测试、Console 权限扫描、后端构建与 `Radish.Api.Tests` 195 个测试通过
   - 当前结论为：`M13` 不再只是规划名词，已具备第一版真实入口；接下来优先服务于首个 dev 版本的总回归与最小发布准备，而不是先扩张为更重的工程平台

13. **M15 首轮最小 CI 门禁已完成真实落地**
   - GitHub Actions 已新增 `Repo Quality` 工作流，当前已覆盖 `pull_request -> master / dev` 与手动触发；普通 `dev` push 不再触发该工作流
   - 当前已接通 `Repo Hygiene`、`Frontend Lint`、`Baseline Quick` 与 `Identity Guard` 四个最小质量门禁，并已把身份语义防回归从“baseline 的附属检查”提升为独立可见门禁
   - 最新已继续补齐治理真相源：ruleset 模板、ruleset README、分支治理 ADR、部署指南与首版回归文档当前都已对齐到四项 required checks，避免 workflow job 名、GitHub ruleset 与文档口径继续分叉
   - 当前已补 `Docker Images` 工作流：仅在 `push v*-dev / v*-test / v*-release` 与手动补跑规范 tag 时推送镜像，`radish-dbmigrate / radish-api / radish-auth / radish-gateway / radish-frontend` 已纳入统一 GHCR 口径，并已通过 `v26.3.2-test / v26.3.2-release` 补齐首次真实拉取、初始化与部署验收
   - `frontend` 侧的运行时配置注入已完成：静态服务当前会在请求 `/runtime-config.js` 时动态返回运行时配置脚本，`radish.client / radish.console` 已优先读取运行时配置，不再要求只能依赖构建期 `VITE_*`
   - `frontend` GHCR 首次真实产物已完成验证，当前已可通过 `docker pull` 获取；`Frontend/Dockerfile` 也已收口为轻量多阶段运行时镜像，本地构建验证体积约 `300MB`
   - 最新于 `2026-03-28`，`Deploy/docker-compose.local.yml / docker-compose.test.yml / docker-compose.prod.yml` 三套口径已继续收口：`local` 仅用于本地容器构建 / 启动验证，`test / prod` 统一使用远程镜像部署；当前三套容器编排都已纳入 `dbmigrate -> api/auth -> gateway` 的启动顺序，并已完成一轮 `docker compose config` 级静态校验，避免首次部署缺表导致登录链路直接失败
   - `master` 分支保护与 ruleset 资产已落地，当前已切换为“禁止直接 push、仅允许 PR 合并”的发布入口
   - 最新于 `2026-03-28`，`v26.3.2-test` 与 `v26.3.2-release` 已分别完成从 tag 驱动构建、GHCR 拉取到 `base + test`、`base + prod` 真实部署的整链验证，登录 / 回调 / 权限 / 核心页面与 `DbMigrate` 初始化链路当前均已通过
   - 当前结论为：首次 CI/CD 已完成真实合并、发布与部署闭环，并已支撑首版 `dev` / `test` / `release` 进入“可交付、可部署、可回归”的稳定状态；后续重点转为论坛 / 聊天等社区主线的发布后优化

14. **`radish.client` i18n 首轮高频覆盖已推进**
   - 桌面壳层已支持语言切换入口可见化，应用注册、桌面图标与窗口标题已切到翻译键
   - 商城首页、商品列表 / 详情、订单列表 / 详情、背包、购买弹窗与购买动作提示已完成首轮资源化
   - 论坛帖子列表、帖子详情讨论区、评论树、评论表单、评论卡片与帖子卡片等高频组件已完成首轮资源化
   - 聊天室高频消息链路、通知中心主列表与共享通知组件、个人中心资料核心信息 / 浏览记录 / 关系链 / 头像上传 / 附件列表 / 钱包交易记录已完成首轮资源化
   - 欢迎 App 的平台概览 / 上手路径 / 社区约定 / 开源说明长文案也已完成双语资源化，并通过 `radish.client` 类型检查与构建验证
   - 当前结论为：`radish.client` i18n 不再处于“待启动”状态，文档应用主链与论坛边缘页已完成当前批次收口；通知中心 / 个人中心与桌面壳层残余语言 / 主题烟雾验证也已完成，欢迎页主体内容已纳入语言切换范围，后续转入更大范围首版联调复核

15. **通知中心已完成总回归收口确认**
   - 已完成新通知 Toast 预览、Dock 未读角标、通知列表显示、点击跳转并静默已读、删除后角标同步与多端同步验证
   - 用户于 `2026-03-23` 明确确认“都通过了，没问题”
   - 当前结论为：通知中心已完成总回归收口确认，后续只做稳定维护，不继续扩张交互范围

16. **认证 / OIDC / Gateway 基础入口已完成总回归收口确认**
   - 已完成 `radish-client / radish-console / radish-scalar` 的登录、回调、登出，以及 `Gateway / Scalar` 入口可用性复核
   - 用户于 `2026-03-23` 明确确认“都通过，测试了没问题”
   - 当前结论为：认证基础入口已完成总回归收口确认，后续只做稳定维护，不继续扩张协议与入口范围

17. **WebOS / 论坛基础 / 社区 P0 / Console V1 / 体验主线已完成总回归收口确认**
   - 已于 `2026-03-26` 对 WebOS 桌面与应用容器、论坛基础、社区 P0、Console V1，以及 `radish.client` 国风视觉基线 / 主题切换 / i18n 做一轮首版烟雾联调
   - 用户明确确认“全部都没啥问题，可以收口了”
   - 当前结论为：以上功能线已完成总回归收口确认，后续只做稳定维护与记录冻结

18. **首版 `dev` 总回归记录已补，社区主链回归已完成本轮收口**
   - 当前已新增 [首版 dev 总回归记录](/guide/dev-first-regression-record)，统一沉淀 `2026-03-23 ~ 2026-03-26` 的真实 Smoke 结果与最新工程判断
   - 已于 `2026-03-26` 完成 `npm run validate:baseline` 与 `npm run validate:baseline:host`：前端类型检查、`radish.client` 最小测试、Console 权限扫描、身份语义扫描、后端构建 / 测试，以及 `DbMigrate doctor / verify` 当前均已通过
   - 当前结论为：业务 / 体验主线 Smoke、验证基线与社区主链回归当前均已完成本轮收口，首版剩余重点进一步收束到稳定维护、统一镜像推送口径冻结，以及下一里程碑入口重审

19. **`2026-04-05` 的规划重审已进入 `M14` 第一轮执行阶段**
   - 今天不直接启动新的功能主线，也不把已收口专题重新拉回当前主线
   - 当前已把后续事项重新拆成维护池 / 观察池 / 后置池：Phase 4 首轮实施结果、聊天室 `P1`、通知中心、`Console-ext` 一期、投票 / 问答 / 抽奖 MVP、浏览记录与 `DbMigrate` 统一转入维护池；旧 `GetCommentTree` 当前已完成观察期收口并进入正式删除窗口，观察池只保留身份语义外部兼容边界与 `Repo Quality` 默认执行面
   - 当前主线已正式切换为 [M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline)，并新增 [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist) 作为默认执行入口；`M13` 与 `M15` 暂继续保留在候选池
   - 用户已完成 `radish-client / radish-console / Radish.Api.AuthFlow.http / radish-scalar` 手工联调确认，本轮 `Phase 4` 可以正式收口，不再维持“继续观察官方回归”的表述

20. **`M14` 启动前 / 启动后两段主路径与报告口径已完成首轮收口**
   - 当前默认执行顺序已固定为：启动前先跑 `npm run validate:baseline:host`，宿主启动后再跑 `npm run check:host-runtime`
   - `validate:baseline:host` 与 `check:host-runtime` 当前都已支持 `--report`、`--report-file`，并统一输出 `Summary / Actions` 两段，便于把启动前与启动后结论写入同一份维护记录
   - `validate:baseline:host` 当前已明确分流 `baseline / doctor / verify` 三类失败；`check:host-runtime` 当前已承担 `Gateway / Api / Auth` 的最小运行态检查，并支持补充 `Gateway /healthz` 条目摘要
   - `Gateway` 当前已明确拆分 `/health` 与 `/healthz` 语义：前者用于最小后端宿主链检查，后者用于更完整的扩展观测；本轮重点是把这套主路径固定下来，而不是继续扩成大而全运维平台
   - 最新已继续补齐宿主启动摘要：`Api / Auth / Gateway` 当前会在启动日志里直接输出 JWT / OIDC / 下游探活目标运行摘要，其中 `console-service` 已固定为扩展观测层的 `/healthz + Degraded`，便于先核对运行模式，再进入健康检查分诊

### 暂不作为当前主线

- 治理前端化：审核台、敏感词与自动策略
- 分发增强：更复杂召回 / 排序 / 观测压测
- 邮件通知系统

## M14 当前切分

### 已完成子阶段

- `M12-P0` 社区主线最小闭环
- Console 权限治理 V1 主体建设
  - 页面权限闭环
  - 路由守卫收口
  - Dashboard / Hangfire / Users / Products / Stickers 等关键资源种子补齐
- 身份语义收敛主体完成
- 文档应用 `A` 阶段验收完成
- 身份语义 `Phase 4` 首轮实施、官方顺序真实回归与本轮正式收口

### 当前进行中子阶段

- `M14` 执行入口、报告口径与阶段文档收口
- `validate:baseline:host`、`DbMigrate doctor / verify`、`check:host-runtime`、健康检查、启动日志与部署复核顺序统一
- 身份语义 Phase 4 稳定维护与防回归治理准备
- 旧 `GetCommentTree` 兼容入口已完成观察期收口，下一批次执行正式删除窗口
- 文档、验证基线与发布口径冻结维护

### 后续候选子阶段

- `M13` 验证基线与回归资产工程化
- `M15` 最小交付与部署基线
- `P3-ext` 论坛投票扩展（首个 dev 版本后再评估）
- 论坛问答后续增强（首个 dev 版本后再评估）
- `P5-ext` 抽奖扩展（首个 dev 版本后再评估）
- `Console-ext Phase 2+` 更完整权限中心、审计与共享接口治理
- `P2-ext Auto` 开源软件清单自动生成 / 发布物公告（首个 dev 版本后再评估）
- `Gateway & BFF` 深化（暂缓专题）
- `Later` 邮件通知系统

## 进入下一里程碑前的遗留项评估

### 必须先清

- 身份语义 Phase 4 的最终启动评审：见 [身份语义 Phase 4 最终启动评审](/guide/identity-claim-phase4-start-review)

### 可转维护

- 身份语义 Phase 4 首轮实施与官方回归结果
- 聊天室 `P1`
- 通知中心
- `Console-ext` 一期
- `P3` 投票 MVP
- `P4` 问答 MVP 与 `P4-ext` 首轮闭环
- `P5` 抽奖 MVP 与浏览记录优化
- `DbMigrate` 当前 `doctor` / `verify` 能力

### 继续观察

- 身份语义外部兼容边界是否因部署形态变化而需要补事实
- `Repo Quality / validate:ci / Identity Guard` 默认执行面是否持续同源

### 明确后置

- `P3-ext` / `P4-ext` / `P5-ext` / `Console-ext Phase 2+` 的非阻塞增强细节
- `P5-ext` 自动开奖、奖励联动、防刷、公示与后台管理
- 邮件通知系统
- `Gateway & BFF` 深化
- “完整 CI / 完整容器交付 / 全量端到端测试” 这类超出当前资产现实的大而全工程目标

## 本阶段建议入口

- `M14` 阶段定义：见 [M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline)
- `M14` 默认执行清单：见 [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist)
- 首版边界与完成标准：见 [首版 dev 边界](/planning/dev-first-scope)
- 首版总回归与发布前检查单：见 [首版 dev 总回归与发布前检查单](/guide/dev-first-regression-checklist)
- 首版总回归记录：见 [首版 dev 总回归记录](/guide/dev-first-regression-record)
- 上线前交付复核入口：见 [部署与容器指南](/deployment/guide)
- 权限治理状态与清单：见 [Console 权限治理 V1](/guide/console-permission-governance)
- 当前执行项：见 [当前进行中](/planning/current)
- 论坛投票方案与进度：见 [论坛投票 MVP 设计方案](/features/forum-poll-mvp)
- 论坛问答方案与进度：见 [论坛问答 MVP 设计方案](/features/forum-qa-mvp)
- 论坛抽奖方案与进度：见 [论坛抽奖 MVP 设计方案](/features/forum-lottery-mvp)
- 本周实际产出：见 [2026-04 / week1](/changelog/2026-04/week1)
