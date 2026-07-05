# P3-12-E1 正式产品成熟度矩阵与差距审计

> 日期：2026-07-05（Asia/Shanghai）
>
> 主线：`P3-12-E 正式产品成熟度与质量硬化`
>
> 性质：只读审计与差距矩阵。本轮不新增业务 API、权限键、数据库结构、路由语义或保存 / 提交载荷，不执行真实 Gateway smoke。

## 审计结论

Radish 已经从 `P3-12-D` 的页面族 UI 收束进入 `P3-12-E` 的正式产品成熟度硬化。E1 审计确认，当前产品已经具备社区首发的核心骨架：发帖 / 评论、聊天首发链路、关注 / 圈子、通知回流、举报与治理证据链、商城 / 资产 / 宠物 / 经验等私域复访模块均有源码或文档证据支撑。

但正式发布不能只以“页面可访问”和“无阻断 smoke”为结论。按社区产品主轴重新审计后，发布前必须先补齐最小用户承诺与错误恢复闭环，并把 Workbench、通知、Console 移动治理、冷启动、隐私边界、反骚扰、可访问性、运营默认值和旅程级验证纳入 E2-E6，而不是继续用“当前不做”掩盖。

本轮结论：

- `必须修复`：正式用户承诺 / 法务口径、帮助反馈与错误恢复里的可见假动作和诊断缺口。
- `发布前建议`：Workbench 真实活动中心、通知行动队列、Console 移动治理视图、冷启动、内容生命周期、隐私边界、反骚扰、可观测性、运营默认值、可访问性和用户旅程级验证。
- `后置专题`：跨域全局搜索 / 推荐、完整私聊或公开聊天室、完整钱包 / 退款售后、独立移动 Console、自动化治理平台、账号导出 / 注销等。后置项均需要保留替代路径或发布边界说明，不得只写“当前不做”。
- `已满足`：发帖创作器、评论 / 回答 / 轻回应基础链路、关注 / 圈子首发循环、论坛写入可靠性、用户身份公开索引和治理快照证据链的主体能力。

## 审计依据

- 规划入口：`Docs/planning/current.md`、`Docs/planning/p3-12-product-maturity-quality-hardening.md`。
- 前端源码：`Frontend/radish.client`、`Frontend/radish.console`、`Frontend/radish.http`。
- 后端源码：`Radish.Api`、`Radish.Gateway`、`Radish.Auth`、`Radish.DbMigrate`。
- 专题文档：`Docs/features`、`Docs/guide`、`Docs/records` 中 P3-10、P3-11、P3-12、WOG 相关记录。
- 测试证据：`Frontend/radish.client/tests`、`Radish.Api.Tests`。
- 重点信号：源码中的 `TODO`、`暂不支持`、`仅展示不保存`、通知跳转降级、Console 移动端响应式折叠、审核目标不支持回看、运行态 smoke 未覆盖写入动作等。

## 分类口径

- `必须修复`：进入 `P3-12-F` 前不能带入的正式发布缺口，通常是用户可见假动作、用户承诺缺失、错误恢复不可用或正式发布最小信任边界不成立。
- `发布前建议`：不一定阻塞 E2-E5 开工，但应在 E 期关闭，或在 E6 明确写入接受清单、限制说明和补验计划。
- `后置专题`：可在正式发布后继续做，但必须说明替代路径、首发边界和为什么不阻断正式发布。
- `已满足`：已有源码、专题文档和测试证据能支撑首发判断；仍需在 E6 做用户旅程级抽样验证。

## 审计矩阵

| # | 审计面 | 分类 | 源码或文档证据 | 用户影响 | 后续批次 | 验证口径 | 后置不阻断理由 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | 社区产品主线 | 发布前建议 | `Docs/planning/p3-12-product-maturity-quality-hardening.md` 已明确帖子、聊天、社交关系为主轴；`Frontend/radish.client/src/pages/PublicDiscoverApp.tsx`、论坛、圈子、消息、通知页面已形成入口骨架。 | 目前模块齐全，但 Docs、商城、资产、宠物、经验和 Workbench 仍有“各自成页”的惯性，用户可能不能理解它们如何服务社区贡献和复访。 | E3 | 以未登录、刚登录、有关注、有回复 / @我、有订单 / 宠物变化四类状态走查 `/discover`、`/forum`、`/circle`、`/messages`、`/notifications`、`/workbench`。 | - |
| 2 | 发帖创作器 | 已满足 | `Frontend/radish.client/src/apps/forum/components/PublishPostModal.tsx` 覆盖草稿、预览、上传、分类 / 标签、发布中和错误提示；`Radish.Api/Controllers/PostController.cs` 接入发布权限、标签约束和 `clientSubmissionId`；`Radish.Api.Tests/Services/ContentSubmissionServiceTest.cs`、`ForumContentWriteServiceTest.cs` 覆盖写入可靠性。 | 用户可以完成正式发帖主流程，并在重复提交或失败时得到基本反馈。 | E6 | 用户旅程验证覆盖发帖草稿恢复、附件上传失败、发布成功跳转、刷新后详情可读、重复点击不产生重复内容。 | - |
| 3 | 评论 / 回答 / 轻回应 | 已满足 | `Frontend/radish.client/src/apps/forum/components/CreateCommentForm.tsx` 支持 Markdown、@提及、图片 / 附件和预览；`Radish.Api/Controllers/CommentController.cs`、轻回应服务和测试覆盖评论、编辑、删除、点赞与高亮稳定性。 | 首发讨论链可用，能承载根评论、回复、轻回应和问答回答。 | E6 | 覆盖根评论、回复对象、@提及、轻回应冷却、重复提交、编辑历史和登录回流。 | - |
| 4 | 聊天参与 | 发布前建议 | `Frontend/radish.client/src/messages/MessagesApp.tsx`、`Frontend/radish.client/src/apps/chat/ChatApp.tsx` 支持频道、未读、@我、消息定位、引用、撤回、举报、草稿和断线补拉；`Radish.Api/Hubs/ChatHub.cs`、`ChannelMessageController.cs` 与 `ChatServiceTest` 有后端证据。 | 首发聊天链路已可用，但产品边界仍需明确是公共频道 / 私域会话 / 社区实时讨论；完整私聊、聊天室发现和订单沟通不能混为一谈。 | E3 / E5 | 覆盖 `/messages?channelId=&messageId=`、移动输入、断线重连、未读 / @我、通知回流和举报消息回看。 | 完整私聊或公开聊天室可后置，因为首发已有登录态频道消息和通知回流，公开讨论由论坛评论承接。 |
| 5 | 关注 / 圈子 | 已满足 | `Frontend/radish.client/src/circle/CircleApp.tsx` 提供关注动态、关注 / 粉丝列表、公开主页跳转和分页；`Radish.Api/Controllers/UserFollowController.cs`、`UserFollowServiceTest.cs` 覆盖关注、取关、状态、动态和汇总；`Docs/features/circle.md` 明确 `/circle` 私域关系链定位。 | 用户可从内容进入人、关注后在圈子看到关系动态，具备社区关系沉淀闭环。 | E6 | 验证帖子 / 评论作者公开主页、关注 / 取关反馈、圈子动态回到帖子、登录回流和无关注空态。 | - |
| 6 | Workbench 活动中心 | 发布前建议 | `Frontend/radish.client/src/workbench/WorkbenchApp.tsx` 的 `continueItems` 为静态入口；`Docs/records/p3-12-b5-web-workbench-entry-design-2026-06-22.md` 将其定义为功能地图；D62 记录虽补“继续处理队列”，但仍主要指向现有路由。 | 用户可能把 Workbench 理解为个人活动中心，但实际未读、待回复、@我、关注动态、草稿、订单 / 权益变化、宠物 / 经验反馈未来自真实状态。 | E3 | 至少接入通知未读、消息未读 / @我、关注动态、草稿或订单 / 宠物状态中的一组真实指标；若不做，应把文案收敛为功能地图。 | - |
| 7 | 通知行动队列 | 发布前建议 | `Frontend/radish.client/src/utils/notificationNavigation.ts` 支持聊天、论坛、用户、订单等目标分流；`notificationNavigation.test.ts` 覆盖主要跳转；`Docs/guide/notification-center.md` 仍保留弱上下文互动通知回 `/forum`、偏好 / 聚合 / 多端同步后置。 | 通知可以跳转多数目标，但还不是任务归类的行动队列；弱上下文通知和偏好设置会让用户不知道下一步。 | E3 / E5 | 覆盖帖子、评论、回答、聊天消息、关注对象、订单 / 权益、审核结果通知；弱上下文必须有清晰降级文案或补目标信息。 | - |
| 8 | Console 移动治理 | 发布前建议 | `Frontend/radish.console/src/components/AdminLayout/AdminLayout.css`、`Frontend/radish.console/src/pages/adminFeature.css` 有响应式折叠和表格滚动；D55 明确不是独立移动 Console，D66 只证明代表页面移动 CSS 视口未阻断。 | 移动端可看，但未证明审核、回看、手动处置、权限矩阵等治理任务在移动端可完成。 | E2 | 移动 `390x844` 覆盖举报审核、目标回看、手动处置、用户风险、订单异常、文档治理证据；高风险动作必须有确认和可退出路径。 | 独立移动 Console 可后置，因为正式首发可声明后台桌面优先，前提是移动低风险治理或只读排障边界明确。 |
| 9 | 审核证据链 | 发布前建议 | `Frontend/radish.client/src/components/ContentReportModal.tsx` 支持帖子、评论、轻回应、聊天消息、商品举报；Console `Moderation` helpers / columns 支持目标回看、快照、动作日志和 `Unsupported` 状态；`ContentModerationServiceTest.cs` 覆盖快照、聊天证据、并发处理和日志。 | 证据链主体已成型，但 `暂不支持回看` 类型必须逐项归因，否则审核人员会遇到无法复核的举报。 | E2 / E4 | 以种子举报覆盖 Post、Comment、PostQuickReply、ChatMessage、Product 的 Ready / Fallback / Unavailable / Unsupported；Unsupported 必须只出现在明确裁剪目标。 | - |
| 10 | 冷启动 | 发布前建议 | `Frontend/radish.client/src/locales/welcome.zh.ts` 仍偏 WebOS 桌面工作台和联调阶段；发现页、Workbench、个人中心已有空态，但未统一解释社区主线。 | 新用户、无关注用户、无内容用户可能不知道 Radish 的主要价值和第一步行动。 | E3 | 覆盖未登录、刚注册、无关注、无订单、无宠物状态；确认首屏给出看帖、发帖、关注、聊天、规则和帮助入口。 | - |
| 11 | 信息架构 / 全局查找 | 后置专题 | Console `GlobalSearch` 当前搜索菜单元数据；`Frontend/radish.console/src/components/NotFound/NotFound.tsx` 的“搜索内容”仍是 TODO；公开侧论坛、Docs、商城各有局部查找路径。 | 用户跨帖子、用户、标签、Docs、商品和通知查找时路径分散；Console 404 的搜索按钮会造成错误预期。 | E3 修可见假动作；跨域搜索另立后置专题 | E3 先让 NotFound 搜索按钮实际打开菜单搜索或移除；后置专题再定义跨域搜索索引、排序、权限过滤和空态。 | 不阻断正式首发，因为各核心域已有局部列表 / 搜索 / 导航，跨域统一搜索属于增长和效率专题。 |
| 12 | 内容生命周期 | 发布前建议 | `Docs/features/forum-edit-history.md` 明确首期不支持回滚历史版本；论坛编辑历史、软删除、治理快照、聊天撤回占位、Docs 版本治理已有分散证据。 | 用户能查看多数历史和处理结果，但删除 / 恢复 / 回滚 / 留存边界不够统一，容易影响信任。 | E4 | 建立帖子、评论、回答、Docs、附件、聊天消息生命周期表；验证创建、编辑、删除、恢复、审核、历史查看和公开分享边界。 | - |
| 13 | 隐私边界 | 发布前建议 | `Docs/guide/identity-claim-regression-playbook.md`、PublicId / DisplayHandle 相关记录和公开主页实现支撑公开身份语义；但资产、附件、经验、宠物、关注关系、聊天和 Console 可见范围缺少面向用户的统一说明。 | 用户可能误解哪些数据公开、登录可见、本人可见或仅 Console 可见。 | E4 | 输出公开 / 登录 / 本人 / Console 可见矩阵，并用公开主页、`/me`、资产、附件、宠物、聊天、圈子、Console 用户页验证不越权。 | - |
| 14 | 反骚扰 / 反滥用 | 发布前建议 | 论坛写入可靠性、轻回应冷却、重复提交窗口、举报和禁言 / 封禁已有实现与测试；聊天 / 关注 / 举报滥用 / 屏蔽拉黑 / 通知打扰边界仍需归类。 | 高频发帖、评论、聊天、关注骚扰或举报滥用可能降低社区信任。 | E4 | 覆盖发帖 / 评论 / 轻回应 / 聊天频率限制，举报滥用处理，禁言 / 封禁后的用户体验，通知打扰控制。 | 屏蔽 / 拉黑可后置，因为首发已有举报、治理动作和写入可靠性，前提是发布说明不承诺完整反骚扰平台。 |
| 15 | 帮助反馈与错误恢复 | 必须修复 | `Frontend/radish.console/src/components/NotFound/NotFound.tsx` 的搜索按钮仅 `console.log`；`Frontend/radish.console/src/components/ErrorBoundary/ErrorBoundary.tsx` 仍有监控 TODO，用户侧缺少错误编号和可复制诊断信息。 | 正式用户遇到 404、异常、上传 / 购买 / 权限失败时无法继续行动或向管理员提供可定位信息。 | E3 / E5 | 404 不展示假按钮；ErrorBoundary 和关键失败态提供重试、返回、退出登录或复制诊断信息；Console 日志可按时间、路由和用户定位。 | - |
| 16 | 可观测性与事故定位 | 发布前建议 | 后端已有 Serilog、审计日志、治理动作日志；前端统一 logger 存在，但 ErrorBoundary 监控 TODO、Hub 断连和用户问题定位材料尚未形成正式口径。 | 线上问题可能只能依赖开发者复现，无法从用户反馈定位请求、目标对象和时间点。 | E5 | 关键页面错误、接口失败、Hub 断连、Console 高风险动作、治理处理都能关联用户、目标、请求或审计记录；不泄露敏感数据。 | - |
| 17 | 运营默认值 | 发布前建议 | `Docs/guide/system-settings-governance.md`、`BootstrapServiceTest.cs`、`DeveloperDefaultsSeedPolicyTests.cs`、`IdentitySeedContractTests.cs` 覆盖设置和种子治理；默认频道、初始分类 / 标签、商城商品、经验规则、宠物配置、通知规则仍需按正式交付检查。 | 首次部署可能依赖开发者记忆补开关、种子或配置，导致正式环境冷启动不可用。 | E4 / E6 | 从空库或开发基线重放验证默认分类 / 标签、频道、商品、角色权限、系统设置、经验、宠物和通知规则可交付。 | - |
| 18 | 法务 / 用户承诺 | 必须修复 | 当前只在欢迎页规则和 `Docs/guide/shop-system.md` 技术说明中出现内容规范与“不支持退款”等片段，未看到面向用户的隐私政策、用户协议、社区公约、虚拟商品 / 资产说明、未成年人和敏感内容处理入口。 | 正式发布缺少基本信任承诺，用户不知道账号、内容、隐私、虚拟资产和退款 / 不退款边界。 | E3 / E6 | 最少补齐用户可访问的隐私、用户协议、社区公约、虚拟商品 / 资产与退款边界、未成年人 / 敏感内容处理口径，并从注册、欢迎、设置或帮助入口可达。 | - |
| 19 | 可访问性 | 发布前建议 | 论坛创作器、通知、圈子、Workbench、Console 局部有 `aria`、焦点和键盘处理；`Docs/features/chat-app-ui-modules.md`、`Docs/guide/shop-frontend.md` 有局部基线；尚无项目级 a11y 验收矩阵。 | 键盘、读屏、低视力、移动触控用户可能在创作、聊天、购买和治理任务中受阻。 | E5 / E6 | 覆盖键盘遍历、焦点可见、ARIA、颜色对比、表单错误读屏、触控尺寸和长文本，优先验证发帖、评论、聊天、通知、结算和 Console 审核。 | - |
| 20 | 用户旅程级验证 | 发布前建议 | `Frontend/radish.client/tests` 与 `Radish.Api.Tests` 已有大量单元 / 契约测试；D66 运行态补验明确未做写入动作，不覆盖发帖、购买、治理保存等完整旅程。 | 单点测试通过不能证明真实用户任务可连续完成，发布候选可能漏掉跨模块断点。 | E6 | 建立并执行注册登录、发帖评论、聊天回流、关注复访、举报审核、购买到账、资产变化、Docs 发布、Console 处理的旅程级矩阵。 | - |
| 21 | 低频模块回到社区主线 | 发布前建议 | Docs、商城、资产、宠物、经验、通知、Workbench 均已有正式 Web 页面；P3-12-E 专题要求它们服务社区内容生产、互动、关系和复访。 | 用户可能被商城、宠物、资产或 Docs 带入孤立流程，削弱社区主线。 | E3 | 每个低频模块至少有一个回到内容、关系、贡献记录、通知或 Workbench 的清晰路径；空态说明其社区价值。 | - |
| 22 | 完整钱包 / 退款 / 售后 / 资产风控 | 后置专题 | WOG-3、支付 / 转账幂等和商城文档支撑订单、背包、资产流水首发；`Docs/guide/shop-system.md` 当前说明虚拟商品不支持退款，完整售后和资产风控未展开。 | 首发交易可以完成，但用户对虚拟商品、退款、不退款、异常订单和资产争议的预期需要明确。 | E4 / E6 先补发布边界；完整能力后置 | 验证购买、发放、订单详情、资产流水、异常失败反馈和用户承诺文案一致。 | 不阻断首发，因为当前交易范围是虚拟商品和背包权益，已有幂等和发放可靠性；完整售后 / 风控属于交易平台扩展。 |
| 23 | 独立移动 Console | 后置专题 | D55、D56、D66 均把 Console 移动端定位为响应式后台复核，不是独立移动治理 App。 | 管理员在手机上只能完成有限治理或排障，不宜承诺完整移动后台。 | E2 明确桌面优先和移动低风险边界；独立 App 后置 | E2 验证移动端可读、可筛选、可查看证据和低风险处理；高风险动作可要求桌面完成。 | 不阻断首发，因为正式后台可以桌面优先，移动端作为排障 / 低风险治理补充即可。 |
| 24 | 自动化治理 / 敏感词 / 申诉平台 | 后置专题 | `Docs/guide/console-modules.md` 明确当前不新增批量治理、敏感词策略或自动化处罚平台；现有 Console 审核队列、手动动作和日志同页复核。 | 社区规模扩大后人工治理成本会上升，用户也需要更完整的申诉 / 解释机制。 | E4 先补最小解释与处理记录；完整平台后置 | 验证人工举报、处理理由、动作日志、目标回看和用户可理解反馈。 | 不阻断首发，因为初期社区规模可由人工审核、举报、禁言 / 封禁和证据链承接，自动化策略属于规模化运营专题。 |
| 25 | 旧 WebOS / radish-pit 通知残留 | 后置专题 | `Frontend/radish.client/src/apps/radish-pit/hooks/index.ts` 仍有通知 API、标记已读、全部已读 TODO；纯 Web `/notifications` 与 `notificationNavigation.test.ts` 已有正式入口和跳转测试。 | 若旧 WebOS 通知入口仍作为主路径展示，用户会遇到不可保存或不可同步的操作；若只是保留入口，影响较低。 | E3 先确认入口可见性；旧 WebOS 残留后置清理 | 验证默认产品路径只引导到 `/notifications`，旧入口不承诺未实现动作。 | 不阻断首发，因为正式通知中心已迁移到纯 Web 路由，旧 WebOS 桌面不是默认社区主路径。 |

## E2-E6 输入

### E2：Console 移动治理视图与高风险后台交互硬化

- 建立移动 Console 任务清单：举报 / 审核、帖子 / 评论 / 聊天目标回看、用户风险、订单 / 权益异常、文档治理证据。
- 明确桌面优先后台与移动低风险治理边界；不默认开发独立移动 Console App。
- 收口 `Moderation` 中 `Unsupported` 回看状态，确认每类目标是 Ready、Fallback、Unavailable 还是正式裁剪。

### E3：公开与私域真实任务链路产品力补强

- 优先补 Workbench：真实未读、@我、关注动态、草稿、订单 / 权益变化、宠物 / 经验状态，或将其文案改回功能地图。
- 优先补通知行动队列：被回复、被提及、聊天、关注、订单 / 权益、审核结果应能回到具体上下文。
- 处理冷启动、NotFound 假搜索、帮助入口、法务 / 用户承诺最小入口和低频模块回社区路径。

### E4：安全、权限与写入可靠性复核

- 输出公开 / 登录 / 本人 / Console 可见矩阵，覆盖资料、关注、经验、宠物、资产、附件、聊天和治理记录。
- 建立内容生命周期矩阵：创建、编辑、删除、恢复、回滚、审核、历史、留存、公开分享。
- 复核反骚扰和反滥用：发帖、评论、轻回应、聊天、关注、举报、通知打扰、禁言 / 封禁后体验。
- 复核运营默认值和空库可交付性：角色、权限、分类 / 标签、频道、商品、经验、宠物、通知和系统设置。

### E5：性能、稳定性与异常恢复补强

- 补 ErrorBoundary、关键失败态、Hub 断连、上传失败、重复点击和弱网恢复的用户可恢复路径。
- 建立最小事故定位材料：错误编号、时间、路由、用户、目标对象、请求或审计记录，不暴露敏感数据。
- 补可访问性抽样矩阵，优先覆盖创作、讨论、聊天、通知、结算和审核。

### E6：发布候选进入判断

- 执行用户旅程级验证矩阵：注册登录、发帖评论、聊天回流、关注复访、举报审核、购买到账、资产变化、Docs 发布、Console 处理。
- 汇总 `必须修复` 关闭证据、`发布前建议` 接受或关闭证据、`后置专题` 发布边界和不阻断理由。
- 只有 E1-E5 的阻断项关闭后，才进入 `P3-12-F 正式版发布候选整备`。

## 本轮验证

本轮只产出审计记录和入口文档更新，不执行真实 Gateway smoke。验证按文档批次口径执行：

- `npm run check:repo-hygiene:changed`
- `git diff --check`
