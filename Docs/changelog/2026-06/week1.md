# 2026 年 6 月第 1 周开发日志

## 2026-06-01

- `P3-8-D` 继续沿纯 Web + Flutter 主线推进，完成 Flutter 登录态“我的浏览记录 / 最近访问”只读入口：复用 `User/GetMyBrowseHistory` 私有接口，覆盖公开帖子 / 文档 / 商品详情承接、加载 / 空态 / 错误 / 刷新 / 加载更多 / 返回状态。
- Flutter 通知已读状态受控写入已完成：Flutter API 客户端补齐 `PUT` 契约，通知仓储复用 `Notification/MarkAsRead`，通知列表支持单条显式标记已读并局部更新状态；本轮不扩展批量已读、删除、通知设置、系统推送或完整通知中心。
- Flutter 纯文本发帖受控写入已完成：论坛页读取顶级分类，登录态用户可发布 `contentType=text` 的纯文本帖子，成功后刷新列表并打开新帖子详情；本轮不扩展富文本、附件、投票、抽奖、草稿箱、编辑或点赞。
- Flutter 单商品购买路径受控写入已完成：登录态用户可从原生商品详情检查购买资格、输入支付口令购买 `1` 件商品，成功后进入订单详情确认订单号、状态和扣款结果；匿名态购买入口会发起登录并回到当前商品详情。本轮不扩展购物车、退款、权益使用、通知中心、完整移动商城或 WebOS 新功能。
- 胡萝卜资产发放与调账口径已回拉治理：注册默认奖励统一收敛到 `CoinService.GrantRegistrationRewardAsync`，普通注册、登录补偿和首个管理员初始化复用同一契约；余额查询、发放、扣除和管理员调账前校验真实用户，避免 Console 对错误 UserId 建立余额后用户侧购买仍显示 `0`。
- Console 用户 ID 字符串安全已按 `ID Phase A` 回拉：当前登录用户、个人资料和用户列表中的 `VoUserId / uuid` 不再进入 JavaScript `number` 精度域，避免后台展示、用户详情跳转或调账动作指向错误用户。
- 今日提交回顾后已确认规划入口、`P3-8-D` 专题、`ID 与联邦路线图`、6 月开发日志和收工交接记录需要同步；本轮没有新增数据库结构、权限模型、视觉 token、Pencil 设计稿、部署配置或运行时环境变量。
- 今日验证覆盖 Flutter 定向测试、`flutter analyze`、必要时全量 `flutter test`，后端定向测试与 `dotnet build Radish.slnx -c Debug`，Console `type-check / test / build`，以及仓库级 `git diff --check`。
- 收工前补 [2026-06-01 收工回顾与明日事项](/records/daily-handoff-2026-06-01)：明日先做 Flutter 单商品购买真实数据复核；若购买 / 资产链路仍有阻断，优先修该链路，若通过则推进 `ID Phase A` 外部 LongId 字符串安全自动化守护。

## 2026-06-02

- Flutter 单商品购买真实数据人工复核已通过，确认登录态商品详情购买、支付口令、订单结果、余额 / 失败提示和返回状态可以闭合。
- Console 胡萝卜调账审计缺口已修复：按用户查询余额和管理员调账继续校验真实用户，避免错误 UserId 被建立余额后误导购买链路。
- `ID Phase A` 首批自动化守护已落地：新增 `npm run check:long-id-safety`，扫描 Console / Web / Flutter 高信号外部 LongId 边界，并接入 `validate:identity`；验证脚本已按 Windows 与 macOS / Linux 分流，避免跨平台误报。
- `radish.client` 登录会话内部 ID 口径已迁为字符串：`userStore / authBootstrap / tokenClaims / authSession` 等状态不再把当前用户和租户 ID 拉入 JavaScript 精度域。
- Flutter LongId 安全扫描已加强：Dart map 读取、`int.tryParse`、`_readInt / readInt`、`as int` 和 `toInt()` 等回潮形态纳入守护。
- Web 工作台购买回流已收紧资格检查：购买回流前读取 `CheckCanBuy` 结果，资格不通过时不再打开支付口令弹窗；Flutter 商城商品、订单和背包文案同步保持当前单商品购买与只读承接边界。
- Console 订单扣款流水追踪已补强：订单详情输出扣款流水 ID，可跳转到 `BusinessType=Order / BusinessId=OrderId` 的胡萝卜流水筛选结果；管理端流水查询支持业务上下文筛选，便于从订单定位扣款、从流水回看用户和商品上下文。
- 今日验证覆盖 `dotnet test Radish.Api.Tests --filter CoinServiceTest`、`npm run type-check --workspace=radish.console`、`npm run type-check --workspace=radish.client`、`npm run test --workspace=radish.client`、`npm run check:long-id-safety`、`npm run validate:identity`、Flutter 定向测试、`npm run check:repo-hygiene:changed` 与 `git diff --check`。
- 收工前补 [2026-06-02 收工回顾与明日事项](/records/daily-handoff-2026-06-02)：明日先做 `ID Phase A` 第三轮外部 ID 契约审计，若未发现高风险缺口，再回到购买 / 资产链路做跨端排障复核。

## 2026-06-03

- Console 订单 / 胡萝卜流水排障链路继续补强：订单详情展示扣款流水 ID，并可按 `BusinessType=Order / BusinessId=OrderId` 跳转到胡萝卜流水筛选；管理端流水查询支持业务上下文筛选。
- Console 角色授权链路按 `ID Phase A` 回拉：角色 ID、资源 ID、API 模块 ID 在角色列表、角色编辑、授权快照、资源树勾选、权限预览和保存请求中保持字符串契约；LongId 守护补 `RoleId / ResourceId / ApiModuleId` 与角色 API `voId` 回潮扫描。
- `P3-8-D` 排障复核已阶段收口：Flutter 订单、Console 订单、胡萝卜流水和用户详情四个入口可围绕同一笔购买按订单 ID、业务类型、业务 ID、扣款流水和用户上下文互相定位。
- Flutter 登录态个人资料编辑已完成：登录态“我的”页新增“编辑资料”入口，复用 `User/GetMyProfile` 与 `User/UpdateMyProfile` 私有契约，支持用户名、邮箱、展示名称、年龄和地址的加载、校验、保存中、失败提示与保存成功后刷新原生公开资料摘要；本轮不扩展头像上传、完整账号设置、密码修改或关注管理。
- 今日文档同步复核确认：`current.md`、`P3-8` 专题文档、6 月开发日志和日交接记录需要同步；没有新增数据库结构、权限模型、视觉 token、Pencil 设计稿、部署配置或运行时环境变量，因此架构说明、视觉规范、设计源文件和部署说明无需跟随更新。
- 今日验证覆盖 `npm run check:console-permissions`、`npm run build --workspace=radish.console`、`npm run check:long-id-safety`、`flutter test test/profile_page_test.dart`、`flutter test`、`flutter analyze`、`npm run check:repo-hygiene:changed` 与 `git diff --check`。
- 收工前补 [2026-06-03 收工回顾与明日事项](/records/daily-handoff-2026-06-03)：明日先做 Flutter 个人资料写入后的跨端展示一致性复核与必要修复，确认同一用户在 Flutter、纯 Web、Console 和论坛作者展示中的用户名、展示名称和头像口径一致。

## 2026-06-04

- Flutter 个人资料写入后的跨端展示一致性已完成治理与复核：论坛帖子、评论、回复目标和轻回应展示名回到当前用户资料口径，Console 用户详情同步展示名称字段。
- Flutter 纯文本发帖守护继续补齐：发帖成功后原生详情读取 `VoPublicId` 并展示 `/forum/post/:publicId` 公开链接；发帖失败时保留标题、标签和正文草稿，不误触发详情跳转或成功态。
- Flutter 订单 / 背包 / 钱包回流继续补强：订单详情扣款流水入口改为基于订单 ID；订单详情刷新失败保留现有订单上下文，背包来源订单 / 商品加载失败可明确返回背包，钱包按 `Order #orderId` 筛选无结果或刷新失败仍保留筛选上下文。
- Flutter 通知和购买后确认小闭环已推进：未读 forum 通知打开详情前会尝试单条标记已读，失败不阻断详情打开；订单详情新增“查看背包发放”入口，可从订单返回背包发放确认。
- Flutter 最近访问公开路由守护已完成：浏览记录中的 forum / docs 入口优先使用 `targetSlug` / `PublicId`，旧 `routePath` 只作为缺少公开标识时的回退，避免内部 LongId 回潮到公开详情入口。
- Console 授权 ID 集合守护已补强：`check-long-id-safety` 覆盖 `resourceIds / voGrantedResourceIds / selectedResourceIds` 等授权资源 ID 集合的 `number[]`、`string[] | number[]`、`Set<number>` 和 `.map(Number)` 回潮。
- 今日文档同步复核确认：`current.md`、`P3-8` 专题、`ID 与联邦路线图`、`Console 核心概念`、6 月开发日志、记录索引和日交接记录需要同步；没有新增数据库结构、权限模型、视觉 token、Pencil 设计稿、部署配置或运行时环境变量，因此视觉规范、设计源文件、部署说明和 API 说明书无需跟随更新。
- 今日验证覆盖 Flutter 定向测试、`flutter analyze`、`npm run check:console-permissions`、`npm run check:long-id-safety`、`npm run validate:identity`、`npm run check:repo-hygiene:changed` 与 `git diff --check`；`validate:identity` 中后端身份语义定向测试 14 个通过，输出仍有既有 XML 注释 warning。
- 收工前补 [2026-06-04 收工回顾与明日事项](/records/daily-handoff-2026-06-04)：明日优先审计 Flutter 纯文本发帖登录回流与草稿恢复；如运行时已满足，只补定向守护或文档结论。

## 2026-06-06

- Flutter 纯文本发帖登录回流与草稿恢复已收口：匿名态从发帖表单提交会保留页面存活期间的草稿，登录回调后回到发帖表单继续发布；提交失败继续保留草稿且不误打开详情。
- 公开 forum 详情分享预览一致性已补守护：旧 LongId 路径加载到 `VoPublicId` 后，运行时 canonical、OpenGraph 与 JSON-LD 会刷新到同一个 PublicId 公开路径，旧路径仅保留打开兼容。
- Flutter 背包来源回流 LongId 口径已回拉：来源订单 / 商品入口按正则规范化的字符串 LongId 承接，不再通过 `int.tryParse` 数值化，并补非规范来源 ID 守护。
- Console 订单 / 胡萝卜流水 / 商品排障回流继续补强：订单 URL 状态和商品 URL 状态均抽出可测 helper，详情关闭、分页、筛选、重置和相关订单跳转会保留合法 `returnTo`，LongId 查询参数保持字符串和同源相对路径约束。
- Console 角色授权链路已补并发保存守护：资源 / API 模块 ID、祖先补选、接口预览去重和保存 payload 排序继续保持字符串契约，保存中禁用保存按钮并阻止重复提交。
- 今日文档同步复核确认：`current.md`、`P3-8` 专题、Flutter handoff、Console 核心概念、公开 SEO 分享基线、`ID 与联邦路线图`、6 月开发日志、记录索引和日交接记录需要同步；没有新增数据库结构、权限模型、视觉 token、Pencil 设计稿、部署配置或运行时环境变量。
- 今日验证覆盖 `radish.console test / type-check / build`、Flutter 定向测试与 `flutter analyze`、`radish.client` 定向测试 / type-check、`validate:identity`、public head smoke self-test 与 `check:repo-hygiene:changed`；`validate:identity` 输出仍有既有 XML 注释 warning。
- 收工前补 [2026-06-06 收工回顾与明日事项](/records/daily-handoff-2026-06-06)：明日优先围绕购买 / 订单 / 背包跨端回流做一天级可验收批次，复核 Flutter、纯 Web / `/desktop` 与 Console 排障之间的返回、确认、筛选上下文和 LongId 字符串契约。

## 2026-06-07

- 主线从 `P3-8-D` 切到 `P3-9 真实使用主路径产品化与发布候选整备`：当前不再继续把购买 / 订单 / 背包、权限授权或 ID 守护作为无限期默认主线，而是按访客、登录移动用户和 Console 管理员三类真实路径做批次级验收、成组修复和发布候选准备。
- `P3-9-A` 已完成当前入口文档、P3-9 专题、发布候选主路径验收矩阵和 client 公开路径契约守护；首批矩阵明确入口可达、状态恢复、公开链接、身份契约、失败态、治理效率和范围控制。
- `P3-9-B` 已完成 Flutter 登录移动用户主路径整备：修正发现页商城商品 LongId 测试夹具，补登录态从发现商品详情购买 1 件、进入订单详情、返回商品详情再回到发现来源的壳层守护；晚间人工复核暴露商品详情购买检查失败态与余额不可见问题后，已补 Flutter API 失败态识别、商品详情余额展示和购买后余额刷新。
- `P3-9-C` 已完成访客公开访问与分享整备：公开 forum / docs / shop 结构化数据、canonical / OpenGraph / JSON-LD、公开链接和登录 / 工作台承接守护已补齐。
- `P3-9-D` 已完成 Console 管理员排障入口整备：用户、订单、商品、胡萝卜流水与角色授权路径继续保持字符串 ID、合法 `returnTo`、加载 / 保存 / 无权限状态和排障返回上下文。
- 发布候选路径自动化总回归已执行并通过：client / console / Flutter 定向验证、`validate:identity`、public head smoke self-test、`validate:baseline:quick` 与提权后的 `validate:baseline` 均通过；`validate:identity` 中后端身份语义定向测试 14 个通过，仍有既有 XML 注释 warning。
- 晚间人工复核后确认下一步先由用户复测 Flutter 登录态商品详情余额、购买资格检查、单商品购买、订单详情、背包发放和胡萝卜资产 / 流水；若无新增 `P0/P1`，转入 `PR -> master` 前扩大验证和合并材料准备。
- 跨端信息架构口径已补入 P3-9 专题和前端多壳层策略：Flutter 后续可评估 `发现 / 消息 / 更多 / 我的`，但不要求 Web / PC / Tauri 照搬；跨端一致性优先体现在任务归属、入口命名、登录恢复、返回语义和错误状态。
- 今日文档同步复核确认：`current.md`、P3-9 专题、P3-9-B 批次记录、前端多壳层策略、Flutter handoff 手册、6 月周志和月志需要同步；没有新增数据库结构、权限模型、视觉 token、Pencil 设计稿、部署配置或运行时环境变量，因此架构说明、视觉规范、部署说明和 API 说明书无需跟随更新。
- 今日验证覆盖 `flutter test test/radish_api_client_test.dart test/shop_product_detail_page_test.dart`、`flutter test`、`flutter analyze`、`npm run test --workspace=radish.client`、`npm run type-check --workspace=radish.client`、`npm run test --workspace=radish.console`、`npm run type-check --workspace=radish.console`、`npm run build --workspace=radish.client`、`npm run build --workspace=radish.console`、`npm run validate:identity`、`npm run validate:baseline:quick`、`npm run validate:baseline`、`npm run check:repo-hygiene:changed` 与 `git diff --check`。
