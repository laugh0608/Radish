# 专题回归索引

> 本页用于回答两个问题：
>
> 1. 改了某个模块后，除了 `validate:baseline` 之外还该跑哪些专题回归？
> 2. 规范化后的 `HttpTest` 脚本名分别对应哪个主题？

## 使用顺序

建议按以下顺序执行：

1. 先跑自动化基线：
   - 日常改动先跑 `npm run validate:baseline:quick`
   - 跨层改动再跑 `npm run validate:baseline`
   - 宿主 / 配置 / `DbMigrate` 相关改动再跑 `npm run validate:baseline:host`
   - 若需要在本地复现当前 `Repo Quality` 最小门禁，再跑 `npm run validate:ci`
   - 若当前处于首版 `dev` 收官或准备判断“可发内部开发版”，优先同时查看 [首版 dev 总回归与发布前检查单](/records/dev-first-regression-checklist)
   - 若自动化失败点集中在 `Repo Quality`、`validate:ci`、contract 自校验或受限环境边界，优先看 [Repo Quality 故障分诊手册](/guide/repo-quality-troubleshooting)
2. 再按改动主题补专题回归：
   - 需要账号、Token、运行中宿主的，优先看本页对应 `HttpTest`
   - 需要人工观察 UI、窗口跳转、深链或实时行为的，再补专题文档中的人工验收顺序
   - 当前已统一模板的样板专题见：[人工验收模板](/records/manual-acceptance-template)
   - 若本轮是首轮收口或补丁复验，建议再补一条 [回归结论记录模板](/records/regression-result-template) 对应的结果记录
   - 若需要给周志、PR 或评审补一页完整记录，优先按 [变更回归记录模板](/records/change-regression-record-template) 整理
   - 当前公开壳层的批次级记录样例见：[公开壳层首轮收口与真实联调复核变更回归记录（2026-04-16）](/records/public-shell-change-regression-record-2026-04-16)
   - 当前 Flutter Android MVP 的批次级记录样例见：[Flutter Android MVP 第七批首个主链路变更回归记录（2026-05-01）](/records/flutter-android-mvp-regression-record-2026-05-01)、[Flutter Android MVP profile 最近文档轻量多条列表变更回归记录（2026-05-01）](/records/flutter-android-mvp-profile-recent-docs-record-2026-05-01)、[Flutter Android MVP docs 详情只读上下文补强记录（2026-05-01）](/records/flutter-android-mvp-docs-detail-readonly-context-record-2026-05-01)、[Flutter Android MVP forum detail 来源上下文与错误态补强记录（2026-05-01）](/records/flutter-android-mvp-forum-detail-readonly-context-record-2026-05-01)、[Flutter Android MVP profile 空态人称与文档口径复核记录（2026-05-02）](/records/flutter-android-mvp-profile-empty-copy-record-2026-05-02) 与 [Flutter Android MVP profile 公开主页长文本窄屏显示复核记录（2026-05-02）](/records/flutter-android-mvp-profile-long-text-record-2026-05-02)；准备内测分发时优先补看 [Flutter Android MVP 内测分发前置整理记录（2026-05-01）](/records/flutter-android-internal-rc-prep-record-2026-05-01)、[Flutter Android MVP RC 补验评估记录（2026-05-02）](/records/flutter-android-mvp-rc-supplemental-assessment-2026-05-02)、[Flutter Android MVP 第八至第二十批验证索引（2026-05-02）](/records/flutter-android-mvp-validation-index-2026-05-02) 与 [Flutter Android MVP RC 验收记录（2026-05-04）](/records/flutter-android-mvp-rc-acceptance-record-2026-05-04)，使用正式域名临时 smoke 时可参考 [Flutter Android MVP 正式域名临时 smoke 记录（2026-05-02）](/records/flutter-android-mvp-radishx-smoke-record-2026-05-02)
   - React 复用路线历史结论、Capacitor Android 已清理代码的 spike 记录或 PC/Tauri 历史验证相关问题，优先参考 [React 复用路线 Capacitor / Tauri Spike 记录（2026-05-04）](/records/react-capacitor-tauri-spike-record-2026-05-04)、[Tauri + WebOS 桌面安装包第二轮分发评估清单（2026-05-05）](/records/tauri-webos-desktop-distribution-evaluation-2026-05-05) 与 [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)；当前 PC/Tauri 后置且不再绑定 WebOS
   - 若本轮涉及 `Backend Guard / Identity Guard` 或 `validate:ci` 分流，记录时默认补清“后端 / 身份语义命中原因”和“失败归类 / 受限环境边界”

## 按改动主题选择回归

| 改动主题 | 什么时候补跑 | `HttpTest` / 脚本入口 | 专题文档 / 人工验收入口 | 说明 |
| --- | --- | --- | --- | --- |
| forum 公开移动入口 / 公开内容壳层 | `/forum`、`/forum/category/:categoryId`、`/forum/search`、`/forum/post/:postId` 的公开壳层路由、搜索过滤、列表卡片、分类上下文、详情阅读节奏、返回链路、分页状态改动后 | `npm run type-check --workspace=radish.client`、`npm run build --workspace=radish.client` | [forum 公开移动入口人工验收清单](/records/forum-public-mobile-acceptance)、[前端多壳层策略](/frontend/shell-strategy) | 当前以前端 UI / 路由人工验收为主；若同时触达论坛读取接口，再并行补 `Forum.Core / Forum.Comment` |
| docs 公开阅读 / 公开搜索 / 公开分享首批 / 公开内容壳层 | `/docs`、`/docs/search`、`/docs/:slug` 的公开壳层路由、关键词搜索、分页、目录/搜索回跳、复制公开链接、正文阅读或文档内链跳转改动后 | `npm run type-check --workspace=radish.client`、`npm run build --workspace=radish.client` | [docs 公开阅读首批人工验收清单](/records/docs-public-acceptance)、[文档系统](/guide/document-system)、[前端多壳层策略](/frontend/shell-strategy) | 当前以公开壳层 UI / 路由人工验收为主；公开 `/docs` 不承接编辑、发布、回收站、版本回滚和权限治理。若同时触达 Wiki 读取接口，再并行补 `Radish.Api.Wiki.http` |
| docs 正式 Web 作者入口 | `/docs/mine`、`/docs/compose`、`/docs/edit/:id`、`/docs/revisions/:id` 的登录回流、作者入口可见性、在线文档创建 / 编辑、版本查看或固定文档只读保护改动后 | `npm run type-check --workspace=radish.client`、`npm run build --workspace=radish.client`；涉及 route state / 登录回流时补 `authReturnPath`、`entryRoute`、`publicRouteState`、`realUsagePathContracts` 相关 Node 契约测试 | [文档系统](/guide/document-system) | 作者入口只承接正文创建 / 编辑和版本查看；发布、下架、归档、删除、恢复、访问策略、版本回滚和 Markdown 导入导出归 Console 文档治理 |
| 个人公开页首批 / 公开内容壳层 | `/u/:id` 的公开壳层路由、资料首屏、页签分页、复制公开链接、作者跳转、登录态识别或关注按钮显示改动后 | `npm run type-check --workspace=radish.client`、`npm run build --workspace=radish.client` | [个人公开页首批人工验收清单](/records/profile-public-acceptance)、[前端多壳层策略](/frontend/shell-strategy) | 当前以公开壳层 UI / 路由人工验收为主；若同时触达关系链接口或桌面个人主页，再并行补 `Community / User.Profile` |
| 公开榜单首批 / 公开内容壳层 | `/leaderboard`、`/leaderboard/:type` 的公开壳层路由、榜单切换、分页、“我的排名”显示或用户榜单跳转改动后 | `npm run type-check --workspace=radish.client`、`npm run build --workspace=radish.client` | [公开榜单首批人工验收清单](/records/leaderboard-public-acceptance)、[前端多壳层策略](/frontend/shell-strategy) | 当前以公开壳层 UI / 路由人工验收为主；若同时触达排行榜接口或桌面排行榜，再并行补 `Leaderboard / Experience` |
| 公开商城浏览 / 正式 Web 交易回流 | `/shop`、`/shop/products`、`/shop/product/:productId`、`intent=purchase`、`/shop/orders`、`/shop/order/:orderId`、`/shop/inventory` 的公开浏览、登录回流、订单或背包路径改动后 | `npm run type-check --workspace=radish.client`、`npm run build --workspace=radish.client`，涉及 route state / 登录回流时补对应 Node 契约测试 | [公开商城浏览首批人工验收清单](/records/shop-public-acceptance)、[商城正式 Web 回流与 WebOS 深链保留](/guide/shop-web-return-paths)、[前端多壳层策略](/frontend/shell-strategy) | 公开 canonical 仍只用 `/shop/product/:productId`；购买 intent、订单和背包属于登录态私域路径。若同时触达 WebOS 商城窗口，再并行补 WebOS 历史深链回归 |
| Flutter Android MVP / 原生客户端壳层 | `Clients/radish.flutter` 下 Android 壳层、docs / forum / discover / profile 原生页面、handoff、Android Back、OIDC 回调或本地复访状态改动后 | `flutter test test/<topic>_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze`、必要时补 `.\gradlew.bat :app:testDebugUnitTest` | [Flutter Android RC 分发前置清单](/records/flutter-android-rc-distribution)、[Flutter Android MVP 第七批首个主链路变更回归记录（2026-05-01）](/records/flutter-android-mvp-regression-record-2026-05-01)、[Flutter Android MVP docs 详情只读上下文补强记录（2026-05-01）](/records/flutter-android-mvp-docs-detail-readonly-context-record-2026-05-01)、[Flutter Android MVP forum detail 来源上下文与错误态补强记录（2026-05-01）](/records/flutter-android-mvp-forum-detail-readonly-context-record-2026-05-01)、[Flutter Android MVP profile 空态人称与文档口径复核记录（2026-05-02）](/records/flutter-android-mvp-profile-empty-copy-record-2026-05-02)、[Flutter Android MVP profile 公开主页长文本窄屏显示复核记录（2026-05-02）](/records/flutter-android-mvp-profile-long-text-record-2026-05-02)、[Flutter Android MVP 内测分发前置整理记录（2026-05-01）](/records/flutter-android-internal-rc-prep-record-2026-05-01)、[Flutter Android MVP RC 补验评估记录（2026-05-02）](/records/flutter-android-mvp-rc-supplemental-assessment-2026-05-02)、[Flutter Android MVP 第八至第二十批验证索引（2026-05-02）](/records/flutter-android-mvp-validation-index-2026-05-02)、[Flutter Android MVP 正式域名临时 smoke 记录（2026-05-02）](/records/flutter-android-mvp-radishx-smoke-record-2026-05-02)、[Flutter Android MVP RC 验收记录（2026-05-04）](/records/flutter-android-mvp-rc-acceptance-record-2026-05-04)、`Clients/radish.flutter/README.md` | 日常开发优先跑 Dart 定向测试与 smoke；涉及 Android 原生 handoff、OIDC callback、签名或 release 构建时再补 Android Studio JBR 下的 Gradle 单测、签名检查与 release APK 真机复核；使用正式域名临时 smoke 时需标明服务端版本、可验范围与未覆盖项；准备内测分发时还需记录签名材料、测试 Gateway、分发对象与反馈闭环 |
| PC/Tauri 后置增强壳与 WebOS `/desktop` 保留入口 | `Clients/radish-tauri`、Tauri 配置、`tauriBridge`、桌面 OIDC loopback、deep link、Tauri 构建链路、WebOS `/desktop` 保留入口或历史 Tauri + WebOS 验证路径改动后 | `npm run type-check --workspace=radish.client`、`npm run test --workspace=radish.client`、`npm run build --workspace=radish.client`、`cargo build`、涉及 release exe 时补 `cargo build --release`；涉及 installer 时补 `cargo tauri build` | [React 复用路线 Capacitor / Tauri Spike 记录（2026-05-04）](/records/react-capacitor-tauri-spike-record-2026-05-04)、[Tauri + WebOS 桌面安装包第二轮分发评估清单（2026-05-05）](/records/tauri-webos-desktop-distribution-evaluation-2026-05-05)、[多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)、[前端多壳层策略](/frontend/shell-strategy)、`Clients/radish-tauri/README.md` | 历史 Tauri + WebOS GUI 启动、WebOS 桌面布局、窗口生命周期观察、系统浏览器登录 / 登出 loopback 回跳、Windows NSIS installer 构建、安装与启动已人工验收通过；当前 PC/Tauri 后置且不再绑定 WebOS，若后续重启应以纯 Web 增强体验重新定义默认入口、签名、自动更新、SmartScreen、托盘、菜单、卸载 / 升级、`radish://` 协议注册清理和正式分发验证 |
| 身份语义 / Claim / Auth 协议输出 | `CurrentUser`、Claim 常量、Auth 输出、`userinfo`、Token 解析、协议消费者改动后 | `npm run validate:identity`、`npm run check:identity-claims`、`Radish.Api.AuthFlow.http` | [身份语义防回归回归手册](/guide/identity-claim-regression-playbook)、[身份语义 Phase 4 实施与回滚窗口](/guide/identity-claim-phase4-rollout-window) | 默认先跑 `validate:baseline + validate:identity`；若触达协议输出或官方消费者，再补 `AuthFlow` 与官方顺序回归 |
| 认证 / OIDC / 基础烟雾 | 登录、Token、网关入口、基础连通性改动后 | `Radish.Api.AuthFlow.http`、`Radish.Api.Smoke.http`、`Radish.Api.Tenant.http` | [验证基线说明](/guide/validation-baseline) | 所有需要 Bearer Token 的专题都默认先从 `AuthFlow` 取 token；若本轮涉及身份语义 / Claim 口径，同时优先补 `check:identity-claims` 与 `AuthFlow` 回归 |
| 社区关系链 / 内容治理 / 分发流 | 关注、举报、审核、分发流、个人主页关系链改动后 | `Radish.Api.Community.http` | [社区主线验收清单](/features/community-m12-p0-acceptance)、[论坛应用功能说明](/features/forum-features) | 当前 `Community` 脚本承接原 `Forum.http` 中的关系链与治理段 |
| 论坛核心主链 / 分类标签 / 帖子编辑历史 | 分类、标签、发帖、帖子列表 / 详情、帖子编辑历史改动后 | `Radish.Api.Forum.Core.http` | [论坛应用功能说明](/features/forum-features)、[论坛编辑历史（专题）](/features/forum-edit-history) | 论坛主链基础能力统一看 `Forum.Core` |
| 论坛评论 / 回复 / 评论编辑历史 | 评论树、回复、评论编辑、评论历史改动后 | `Radish.Api.Forum.Comment.http` | [论坛编辑历史（专题）](/features/forum-edit-history)、[`GetCommentTree` 兼容入口退场清单](/records/comment-tree-compat-retirement-checklist) | 评论链路与帖子主链已拆分，避免混在同一文件里；旧评论树兼容入口已退役，默认只回归根评论分页、子评论懒加载与评论编辑历史主链 |
| 论坛投票 | 投票模型、发帖附带投票、状态筛选、票数 / 截止排序、投票提交、结束投票改动后 | `Radish.Api.Forum.Poll.http` | [论坛投票 MVP 设计方案](/features/forum-poll-mvp) | 当前脚本已覆盖投票视图、状态筛选、票数 / 截止排序、重复投票拦截与结束投票；涉及欢迎 App 交互时再补专题文档中的人工验收 |
| 论坛问答 | 问答帖发布、回答提交、采纳、问答视图 / 排序改动后 | `Radish.Api.Forum.Question.http` | [论坛问答 MVP 设计方案](/features/forum-qa-mvp) | `P4-ext` 首轮回归继续复用该脚本 |
| 论坛抽奖 / 浏览记录 | 抽奖参与、开奖、中奖通知、浏览记录写入 / 回看改动后 | `Radish.Api.Forum.Lottery.http`、`Radish.Api.User.Profile.http` | [论坛抽奖 MVP 设计方案](/features/forum-lottery-mvp) | 当前最完整的“脚本 + 人工验收顺序”样例 |
| 聊天室 REST 主链 | 频道列表、历史分页、发送、撤回、在线成员、未读同步改动后 | `Radish.Api.Chat.http` | [聊天室 App 文档总览](/features/chat-app-index)、[聊天室 App 实施路线图](/features/chat-app-roadmap) | 实时事件仍需结合运行中的 ChatHub 做人工观察 |
| 文档治理 / Wiki / Console Documents | `WikiController` 管理端接口、`WikiDocumentService` 治理逻辑、`Frontend/radish.console/src/pages/Documents`、`console.docs.*` 权限、发布 / 下架 / 归档 / 删除 / 恢复 / 访问策略 / 版本回滚 / Markdown 导入导出改动后 | `Radish.Api.Wiki.http`、`npm run build --workspace=radish.console`、`npm run check:console-permissions`；涉及后端授权边界时补 `dotnet test Radish.Api.Tests --filter WikiDocumentManagementServiceTests` 与 `dotnet test Radish.Api.Tests --filter AuthorizationBoundaryTests` | [文档系统](/guide/document-system)、[Console 权限覆盖矩阵](/guide/console-permission-coverage-matrix) | Console 只承接治理动作；正文日常创建 / 编辑归正式 Web 作者入口，公开 `/docs` 保持只读 |
| 表情包 / Sticker | 表情包分组、条目 CRUD、批量上传、记录使用改动后 | `Radish.Api.Sticker.http` | [表情包系统设计方案](/features/emoji-sticker-system) | 贴图上传依赖附件能力时应连同附件脚本一起跑 |
| 附件上传 / 访问令牌 / 分片 | 上传、删除、临时令牌、分片、护栏校验改动后 | `Radish.Api.Attachment.Upload.http`、`Radish.Api.Attachment.Manage.http`、`Radish.Api.Attachment.Guardrail.http`、`Radish.Api.Attachment.Chunk.http`、`Radish.Api.Attachment.Token.http`、`test-attachment-upload.ps1`、`test-attachment-upload.sh` | [文件上传设计](/features/file-upload-design) | 附件链当前是最依赖测试素材与本地宿主的专题之一 |
| 限流 | 限流策略、边界返回、策略映射改动后 | `Radish.Api.RateLimit.Core.http`、`Radish.Api.RateLimit.Policy.http`、`Radish.Api.RateLimit.Edge.http`、`test-rate-limit.ps1`、`test-rate-limit.sh` | [文件上传设计](/features/file-upload-design) | 附件 / 高并发接口改动后经常要顺手回归这一层 |
| 其他专项 | 萝卜币、经验值、神评、多租户等专项改动后 | `Radish.Api.Coin.http`、`Radish.Api.Experience.http`、`Radish.Api.CommentHighlight.http`、`Radish.Api.Tenant.http` | 对应专题文档 | 当前更多是专题性校验，不纳入默认日常链 |

## 当前规范化脚本名

### 论坛

- `Radish.Api.Community.http`
- `Radish.Api.Forum.Core.http`
- `Radish.Api.Forum.Comment.http`
- `Radish.Api.Forum.Poll.http`
- `Radish.Api.Forum.Question.http`
- `Radish.Api.Forum.Lottery.http`

### 附件

- `Radish.Api.Attachment.Upload.http`
- `Radish.Api.Attachment.Manage.http`
- `Radish.Api.Attachment.Guardrail.http`
- `Radish.Api.Attachment.Chunk.http`
- `Radish.Api.Attachment.Token.http`

### 限流

- `Radish.Api.RateLimit.Core.http`
- `Radish.Api.RateLimit.Policy.http`
- `Radish.Api.RateLimit.Edge.http`

### 文档

- `Radish.Api.Wiki.http`

## 历史名称对照

以下旧名称在规范化后已不再作为当前入口使用：

- 历史 `Radish.Api.Forum.http`
  - 当前按主题拆为 `Community / Forum.Core / Forum.Comment / Forum.Poll / Forum.Question / Forum.Lottery`
- 历史 `Radish.Api.Attachment.http`
  - 当前按主题拆为 `Attachment.Upload / Manage / Guardrail / Chunk / Token`
- 历史 `Radish.Api.RateLimit.http`
  - 当前按主题拆为 `RateLimit.Core / Policy / Edge`

如果某份专题文档仍引用旧名称，以本页和 `Radish.Api.Tests/HttpTest/README.md` 为准，并在下一次触达对应专题时顺手修正文档。
