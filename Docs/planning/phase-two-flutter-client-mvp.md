# Phase 2-3 Flutter 客户端 MVP

> 状态：当前主线
>
> 最后更新：2026-04-29（Asia/Shanghai）
>
> 关联文档：
>
> - [开发路线图](/development-plan)
> - [当前进行中](/planning/current)
> - [第二开发阶段：社区深化与多端化](/planning/phase-two-community-multiplatform)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [前端设计文档](/frontend/design)

## 1. 目标

`Phase 2-3` 不回答“移动 Web 是否还能再补一点”，而是回答三件事：

1. Radish 是否开始拥有独立于 WebOS 的原生客户端入口
2. Android 高频路径是否能用原生导航、原生手势和原生生命周期承载
3. 仓库是否为后续 Android / Windows / Linux 扩展建立清晰、可维护的 Flutter 基础

## 2. 第一批边界

第一批固定只做 **范围定义 + 工程骨架**，不直接铺完整业务页。

### 2.1 首批纳入

- Android 起步的客户端壳层范围定义
- Flutter 工程最小目录结构与基础入口
- 导航壳层占位：`discover / forum / docs / profile`
- 环境配置与 API 基础地址语义
- 认证存储接口占位与后续接线边界
- 与 Web 侧共享主题语义的最小 Theme 基线

### 2.2 首批不纳入

- Windows / Linux 同批次落地
- 聊天、完整通知中心、完整商城工作台、创作器
- “移动版 WebOS”
- 复刻桌面窗口系统
- 为 Flutter 额外设计一套独立 BFF

## 3. 仓库落点

Flutter 客户端当前固定落在：

```text
Clients/radish.flutter/
```

该目录独立于 `Frontend/` 的 npm workspaces，避免把原生客户端混入 Web 构建链路。

## 4. 首批信息架构

第一批只建立最小客户端壳层，而不是同时交付完整产品页。

```text
Radish Flutter Shell
├── Discover
├── Forum
├── Docs
└── Profile
```

说明：

- `Discover` 作为高价值内容分发入口
- `Forum` / `Docs` / `Profile` 先提供客户端导航占位与信息架构
- 登录态、通知回流与更深业务链路在后续批次继续接入

## 5. 复用约束

Flutter 客户端第一批固定遵循以下约束：

1. 优先复用现有 API、认证契约与公开内容路由语义
2. 不复刻 WebOS 窗口交互，不把桌面工作台直接搬进原生端
3. 主题语义复用 Radish 品牌口径，但不强求与 Web 结构完全同形
4. Android 跑通后，再评估 Windows / Linux 的平台扩展节奏

## 6. 本批交付物

第一批默认交付：

- `Docs/` 真相源文档切换到 `Phase 2-3`
- `Clients/radish.flutter/` 最小工程骨架
- 壳层导航、环境配置、认证存储、Theme 与页面占位
- Flutter MVP 规划文档本页

截至 `2026-04-22` 的已落地事实补充：

- 第一批骨架当前已完成，`Clients/radish.flutter/` 已建立独立入口、壳层导航、环境配置与最小 Theme 基线
- 第二批当前已完成一轮真实业务接线收口：应用启动会话恢复 gate、匿名态 / 已登录态三态、最小登录 / 登出 / OIDC 回调、`forum` 公开只读阅读链路与最小来源回流均已落地
- 当前 forum 原生读取链路明确复用现有 `/api/v1/Post/GetList` 契约，只承载匿名列表阅读、`latest / hottest` 排序、基础分页、加载态与错误态
- Android 平台目录当前已生成并纳入仓库，Flutter Android Debug APK 构建已通过
- Android 模拟器最小联调已确认可通过 Gateway `https://localhost:5000` 访问公开只读接口；联调前需要先执行 `adb reverse tcp:5000 tcp:5000`
- Android 真机最小联调当前也已完成一轮人工收口：`discover / forum / docs / profile` 四个主 tab 已可进入，`discover -> docs` 与 `discover -> profile` 的公开跳转当前可正常完成；期间暴露的 `Profile` 统计卡与壳层状态区窄屏溢出已修复并通过复测
- Flutter 当前已补齐真实会话恢复基础能力：Android 本地会话持久化、启动恢复、Access Token 过期判断，以及 refresh token 刷新失败后的匿名回落
- Flutter forum 当前已继续从“公开只读 feed”推进到“公开只读帖子详情”最小链路：列表卡片可原生 handoff 到详情页，详情页当前承载正文、作者/分类/时间、基础统计与原生返回，不同时接入评论、互动提交与编辑治理
- Flutter `discover / docs / profile` 当前已分别接通公开摘要分发、公开文档阅读与公开个人资料读取，四个主 tab 都已完成首批真实只读页面替换，不再停留在统一占位说明
- Flutter forum 当前已继续从“公开只读帖子详情”推进到“公开只读评论阅读”：根评论分页、子评论分页、作者 / 评论作者原生公开 profile 跳转当前都已落地
- Flutter forum 评论精确定位最小闭环当前也已落地：带 `commentId` 打开帖子详情时，原生端会先解析公开评论定位信息，再自动补载目标根评论页与子评论页并滚动到目标评论
- Flutter forum 外部详情 handoff 当前已完成最小收口：原生壳层已支持从 shell 层直接透传 `postId + commentId` 打开 forum 详情，并复用现有评论定位链路落到目标评论
- Flutter forum 外部 handoff 当前已继续先收口到 `public profile` 的最近帖子 / 最近评论：profile 不再发散出独立详情跳转，而是统一复用共享 `ForumDetailHandoffTarget`
- Flutter forum 真实来源接线当前已进一步从 discover 的最小演示入口推进到更接近真实来源的原生壳层 / 宿主层：Android 宿主当前可在启动时透传 forum 通知来源，壳层当前也已支持“最近阅读继续”这一条 browse history 续接能力
- Flutter forum detail 打开路径当前也已统一收口：forum feed、public profile、notification 宿主 handoff 与 browse history 壳层续接当前都已接到同一套原生 handoff 目标，继续保持 `postId / commentId` 的字符串口径
- Flutter 当前已补齐最小登录 UI、显式登出与 Android 浏览器 OIDC 回调闭环：原生壳层当前会通过系统浏览器发起 `/connect/authorize`，以 `radish://oidc/callback` 回跳并完成授权码换 Token；登出当前也已统一走 `/connect/endsession` + `radish://oidc/logout-complete`
- Flutter Android 最小登录连续性当前也已补齐：浏览器取消登录后，壳层会显式展示原生认证提示；`AppBar` 状态区当前已收口到可换行的壳层状态条；从 `profile` 或 forum 详情来源发起登录后，成功返回时也会自动续接回原始 tab / handoff 目标
- Flutter forum detail 当前也已补齐最小原地登录入口：匿名用户可直接在详情页发起登录，登录目标会以可持久化 follow-up 状态保留 `postId / commentId`，浏览器往返或壳层重建后仍能回到当前帖子 / 评论上下文
- Android 真机当前也已完成一轮围绕 forum detail 登录入口的人工联调：详情页登录、取消登录后的显式提示、重试登录与成功回到当前 detail 上下文这几条主路径均已通过
- Android MVP 当前人工验证范围已收口到真实可测链路：登录、退出、会话恢复、`discover / docs / profile` 基础读取、forum feed、forum detail、评论阅读、评论分页与 detail 原地登录续接，以及已登录壳层最小 forum notification 回流都已纳入当前可验收面
- Android MVP 当前可测链路已完成一轮人工验收：登录和退出逻辑确认正常，forum 评论区显示问题已修复并在真机确认；最小 forum notification 回流也已完成真机人工联调
- Flutter 当前已补齐一个最小可测 forum notification 来源：已登录壳层会读取当前用户最新可跳 forum 的通知，解析 `voExtData` 中字符串化的 `postId / commentId`，并通过既有 `ForumDetailHandoffTarget(source: notification)` 打开原生 forum detail
- 最小 forum notification 回流当前已完成真机人工联调：`Forum notification` 入口回到 forum detail / 评论上下文的逻辑确认正常，系统通知栏推送与完整通知中心继续不纳入当前批次
- Android MVP 本地 release APK 发布候选当前已完成首轮收口：Android 包身份为 `com.radish.client`，应用显示名为 `Radish`，release signing 读取逻辑与密钥忽略边界已落地，release 包已补齐 `INTERNET` 权限并完成真机安装联调
- 当前 release APK 真机复核已确认登录、基础读取与样式显示正常；本轮自动化验证覆盖 `flutter analyze`、`flutter test`、Android Studio JBR 下的 `.\gradlew.bat :app:testDebugUnitTest` 与 `flutter build apk --release`
- Flutter 环境切换能力当前已完成首轮收口：客户端启动配置会读取 `RADISH_ENVIRONMENT` 与 `RADISH_GATEWAY_BASE_URL` 这组 `--dart-define` 构建参数，并继续保持 API / Auth / Gateway 同源；本机 Android + `localhost` Gateway 会保留本地开发证书放行边界，测试 / 正式 Gateway 默认不放宽证书校验
- Android RC 签名配置诊断与分发前置清单当前已完成首轮收口：Gradle 当前提供 `:app:checkReleaseSigningConfig` 检查正式签名材料，`key.properties` 不存在时本地 release 构建仍回落 debug signing；若 `key.properties` 存在但缺字段、仍是示例值或找不到 keystore，会快速失败并提示修正；清单见 [Flutter Android RC 分发前置清单](/guide/flutter-android-rc-distribution)
- Flutter 第三批中文文案基线当前已完成：主 tab、壳层登录态、登录提示、forum / docs / profile / discover 首批真实页面的标题、摘要、空态与错误态已统一到中文主文案；当前不引入完整 i18n 框架
- Flutter 第三批个人复访入口当前已产品化：`profile` 承载最近 forum 阅读与公开主页复访入口，继续复用既有 `ForumDetailHandoffTarget` 与 follow-up 状态，不把开发态来源说明当作正式用户文案
- Flutter forum detail 当前已接入轻回应墙最小读写闭环：详情页按“正文 -> 轻回应 -> 评论区”展示，匿名可读取最近轻回应，已登录可发布一句轻回应，并复用详情页原地登录续接；删除、举报、完整评论提交、点赞、投票、编辑治理与完整通知中心继续不纳入本批
- Flutter 第三批 Android 真机复核当前已通过：中文文案、个人复访入口与轻回应发布在真实 Gateway 下确认正常；复核中发现的同一帖子详情返回列表后无法再次打开的问题已完成 handoff 消费状态修复与定向测试覆盖

## 7. 第二批范围（已收口）

第二批当前固定收口到两类事情：

1. **最小登录 / 会话恢复边界**
   - 当前已完成启动恢复、匿名态 / 已登录态建模、Android 本地会话持久化、refresh token 恢复回落，以及最小登录 UI / 显式登出 / 浏览器 OIDC 回调闭环
   - 当前也已补齐登录取消后的显式提示、窄屏壳层状态区收口，以及 `profile / forum detail` 两类最小登录后续接；forum detail 内也已具备可手工触发的最小原地登录入口
   - 当前仍未进入更完整的账户治理、多平台原生登录深化与通知中心登录回流联调
2. **首批真实页面接线**
   - forum 当前已从公开列表继续收口到公开详情、评论分页、子评论分页、作者跳转、评论精确定位、public profile 详情回跳，以及 `notification / browseHistory` 的首批壳层 / 宿主 handoff
   - `discover / docs / profile` 首批最小真实页面接线与 Android 真机联调当前都已完成；forum 的首批真实来源接线当前也已完成一轮收口，后续优先转向 Android MVP 当前可测链路的稳定性复核，而不是回头扩壳层占位
   - `notification / commentId` 这类依赖真实通知来源的宿主深链联调，当前已补最小可测来源并完成真机人工联调：已登录壳层读取最新 forum 通知并打开原生 detail；系统通知栏推送、完整通知中心与通知管理继续不纳入本批

## 8. 当前第三批范围

第三批固定收口到产品层回补，不扩完整社区互动：

1. **中文文案基线**
   - 当前已完成中文主文案基线，保留代码标识、API 字段、日志、测试名与技术路径英文
   - 当前不引入完整多语言资源体系、文案运营配置或大规模视觉改版
2. **个人复访入口产品化**
   - 当前已把 profile 内最近 forum 阅读与公开主页复访入口转成正式用户入口
   - 当前不扩完整浏览历史中心、通知中心或跨端同步治理
3. **一个最小轻互动闭环**
   - 当前已选择 forum detail 轻回应墙作为最小闭环，复用既有后端 / Web 侧 `PostQuickReply` 契约
   - 当前只做最近轻回应读取、已登录发布与匿名原地登录引导，不做删除、举报、完整评论提交、点赞、投票或编辑治理

## 9. 下一顺位

当前第三批完成并通过一轮 Android 真机复核后，优先顺序建议为：

1. 先把第三批 Android 真机复核结论与 handoff 回归修复纳入收口记录
2. Android RC 外部分发继续等待真实签名材料、测试 Gateway 与外部分发对象；在条件缺失前，不把外部分发作为业务开发阻断项
3. 若继续进入第四批，优先选择“复访深化 + 已登录轻互动回看”这一条小闭环，不同时扩完整通知中心、系统推送、发帖、完整评论提交、点赞、投票或编辑治理
4. Windows / Linux 平台目录与更深原生能力继续后置，需等 Android MVP 产品闭环与分发条件更稳定后再评估

截至 `2026-04-28` 的第四批建议：

1. 优先选择“复访深化 + 已登录轻互动回看”作为第四批小闭环
2. 首批落点建议放在 `profile`：展示我发布的轻回应 / 最近轻回应上下文，并继续复用现有 forum detail handoff 回到原帖
3. 本批仍不开放发帖、完整评论提交、点赞、投票、编辑治理、完整通知中心或系统通知栏推送

截至 `2026-04-28` 的第四批首个落地事实：

1. `profile` 已接入我的轻回应回看：已登录且查看我的主页时，会读取现有 `/api/v1/PostQuickReply/GetMine` 契约并展示最近轻回应、原帖标题与发布时间
2. 我的轻回应条目已复用 `ForumDetailHandoffTarget` 回到原帖详情，并新增 `myQuickReply` 来源标签；公开主页不展示该个人区块
3. 我的轻回应回看已继续补齐分页复访：`profile` 中该区块支持继续加载更多轻回应，分页失败只在区块内提示，不拖垮公开资料、公开帖子和公开评论读取
4. 本轮仅完成“回看 + 继续加载 + 回到上下文”的轻闭环，不开放删除、举报、完整评论提交、点赞、投票、编辑治理、完整通知中心或系统通知栏推送
5. 当前自动化验证已通过 `flutter test` 与 `flutter analyze`；首个回看闭环与分页复访的 Android 真机复核均已确认正常

截至 `2026-04-28` 的第四批第二个落地事实：

1. `profile` 最近公开评论已接入分页复访：公开主页与我的主页都可以继续加载更多公开评论
2. 分页加载出的评论继续复用 `ForumDetailHandoffSource.publicProfileComment` 回到帖子详情与评论上下文，不新增独立评论详情页；真机复核发现的“能打开帖子但停在顶部”问题已收口为 forum detail 目标评论异步渲染后的滚动重试
3. 加载更多失败只在“最近公开评论”区块内提示，不拖垮公开资料、最近公开帖子或我的轻回应区块
4. 本轮仍保持只读复访边界，不开放评论提交、点赞、删除、举报、编辑治理、完整通知中心或系统通知栏推送
5. 当前自动化验证已通过 `flutter test`、`flutter analyze` 与 `git diff --check`；Android 真机定位复核已确认正常

截至 `2026-04-29` 的第四批第三个落地事实：

1. `profile` 最近公开帖子已接入分页复访：公开主页与我的主页都可以继续加载更多公开帖子
2. 分页加载出的帖子继续复用 `ForumDetailHandoffSource.publicProfilePost` 回到同一套原生 forum detail，不新增独立帖子详情页或个人页专属打开路径
3. 加载更多失败只在“最近公开帖子”区块内提示，不拖垮公开资料、最近公开评论或我的轻回应区块
4. 本轮仍保持只读复访边界，不开放发帖、评论提交、点赞、删除、举报、编辑治理、完整通知中心或系统通知栏推送
5. 当前自动化验证已通过 `flutter test`、`flutter analyze` 与 `git diff --check`；Android 真机复核已确认最近公开帖子、最近公开评论与我的轻回应三条 profile 复访路线从详情返回后都会回到 profile

截至 `2026-04-29` 的第四批第四个落地事实：

1. `profile` 最近阅读上下文已接入我的主页：已登录态可从我的主页继续打开最近一次 forum 阅读目标
2. 该入口复用现有 `ForumDetailHandoffTarget` 与最近阅读存储，不新增存储协议；从 profile 内点击时会临时使用 `profileRecentBrowse` 来源保留 profile 返回上下文
3. 本轮仍只承载单个最近阅读上下文，不扩展完整浏览历史中心、多条记录列表、删除、清空或跨端同步治理
4. 当前自动化验证已通过 `flutter test`、`flutter analyze` 与 `git diff --check`；Android 真机复核已确认最近阅读入口可回到对应详情，返回后仍留在 profile

截至 `2026-04-29` 的第四批第五个落地事实：

1. Flutter docs 最近阅读与直达复访已接入原生壳层：`discover` 文档精选可直接打开 docs 详情，不再只能先进入文档列表
2. docs 详情新增轻量 `DocsDetailHandoffTarget` 来源语义：从 discover 打开后返回 discover，从我的 `profile` 最近文档打开后返回 profile，从 docs 列表打开后仍回到 docs 列表
3. Android 侧新增单条最近文档 target 本地持久化，shell 状态条与我的 `profile` 均可继续打开最近公开文档
4. 本轮仍保持公开只读边界，不扩展文档搜索增强、目录树、编辑、发布、回收站、版本历史、完整浏览历史中心、多条记录列表、删除、清空或跨端同步治理
5. 当前自动化验证已通过 `flutter test`、`flutter analyze` 与 `git diff --check`；Android 真机复核已确认 discover 文档直达、docs 列表详情返回、profile 最近文档复访与重启恢复均正常

截至 `2026-04-29` 的第四批第六个落地事实：

1. Flutter docs 正文内公开文档链接已接入原生详情跳转：正文中的 Markdown 文档链接与裸 `/docs/:slug` 路径会继续打开 Flutter docs 详情
2. 从 docs 列表内联详情点击文档内链时，会在当前 docs 页切换到目标文档；从 discover / profile 最近文档等 route 详情点击文档内链时，会 push 新的 docs 详情 route，返回后回到上一层来源详情
3. 本轮新增 `DocsDetailHandoffSource.docsLink` 来源标签，用于标记文档内链打开路径；正文代码块仍按只读文本渲染，不参与 docs 链接跳转
4. 本轮仍保持公开只读边界，不扩展外部浏览器打开、完整 Markdown 引擎、目录树、编辑、发布、回收站、版本历史、完整浏览历史中心、多条记录列表、删除、清空或跨端同步治理
5. 当前自动化验证已通过 `flutter test`、`flutter analyze` 与定向 `flutter test test/docs_page_test.dart test/smoke_test.dart`；Android 真机待补验

截至 `2026-04-29` 的第四批第七个落地事实：

1. Flutter docs 关键词搜索复访已接入原生 docs tab：列表页当前可输入关键词，并通过现有 `/api/v1/Wiki/GetList?keyword=...` 查询公开文档
2. 搜索结果继续复用原生 docs 列表卡、分页、刷新与详情打开路径；从搜索结果打开文档仍以 `DocsDetailHandoffSource.docsList` 记录最近文档上下文
3. 搜索状态会保留在当前 docs 列表的分页与刷新中，清除搜索后回到公开文档列表；空结果只在 docs 列表内提示，不影响 discover / profile 的最近文档直达
4. 本轮仍保持公开只读边界，不扩展搜索建议、历史搜索、全文高亮、目录树、编辑、发布、回收站、版本历史、完整浏览历史中心、多条记录列表、删除、清空或后端搜索改造
5. 当前自动化验证已通过 `flutter test`、`flutter analyze` 与定向 `flutter test test/docs_page_test.dart test/smoke_test.dart`；Android 真机待补验
