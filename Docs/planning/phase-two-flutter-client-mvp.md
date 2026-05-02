# Phase 2-3 Flutter 客户端 MVP

> 状态：当前主线
>
> 最后更新：2026-05-02（Asia/Shanghai）
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

## 9. 第四批收口与下一顺位

当前第三批完成并通过一轮 Android 真机复核后，优先顺序建议为：

1. 先把第三批 Android 真机复核结论与 handoff 回归修复纳入收口记录
2. Android RC 外部分发在个人开发阶段暂缓；正式 release 包发布前再补 testing Gateway、测试对象、反馈闭环与真机验收，不把分发线作为业务开发阻断项
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
5. 真机复核发现长 slug 窄屏溢出与搜索详情返回退出问题，当前已完成代码修复并复测通过；调试态根层返回后重新打开卡启动页的问题也已收口为 Android 根层 Back 退后台；当前已通过 `flutter test`、`flutter analyze`、`git diff --check` 与定向 `flutter test test/docs_page_test.dart test/smoke_test.dart`

截至 `2026-04-29` 的第四批第七个落地事实：

1. Flutter docs 关键词搜索复访已接入原生 docs tab：列表页当前可输入关键词，并通过现有 `/api/v1/Wiki/GetList?keyword=...` 查询公开文档
2. 搜索结果继续复用原生 docs 列表卡、分页、刷新与详情打开路径；从搜索结果打开文档仍以 `DocsDetailHandoffSource.docsList` 记录最近文档上下文
3. 搜索状态会保留在当前 docs 列表的分页与刷新中，清除搜索后回到公开文档列表；空结果只在 docs 列表内提示，不影响 discover / profile 的最近文档直达
4. 本轮仍保持公开只读边界，不扩展搜索建议、历史搜索、全文高亮、目录树、编辑、发布、回收站、版本历史、完整浏览历史中心、多条记录列表、删除、清空或后端搜索改造
5. 真机复核发现长 slug 窄屏溢出与搜索详情返回退出问题，当前已完成代码修复并复测通过；调试态根层返回后重新打开卡启动页的问题也已收口为 Android 根层 Back 退后台；当前已通过 `flutter test`、`flutter analyze`、`git diff --check` 与定向 `flutter test test/docs_page_test.dart test/smoke_test.dart`

截至 `2026-04-30` 的第四批第八个落地事实：

1. Flutter discover 论坛精选直达已接入原生壳层：发现页中的论坛精选帖子可直接打开同一套原生 forum detail
2. 本轮新增 `ForumDetailHandoffSource.discover` 来源标签；从 discover 打开 forum detail 时会保留发现页 tab，详情返回后仍回到 discover，而不是落到论坛首页
3. discover “进入论坛 / 进入文档”快捷入口也已补齐轻量返回目标：从 discover 切到论坛或文档列表后，在根层按 Android Back 会先回 discover，而不是直接退到桌面；底部导航手动切 tab 不建立该返回目标
4. 论坛精选直达仍复用现有 `ForumDetailHandoffTarget` 与最近阅读记录，不新增 discover 专属详情页、独立路由系统或后端契约
5. 本轮仍保持公开只读边界，不扩展发帖、评论提交、点赞、投票、编辑治理、完整通知中心或系统通知栏推送
6. 当前已补 `discover_page_test.dart` 与 `smoke_test.dart` 定向覆盖，并通过 `flutter test test/smoke_test.dart`、`flutter analyze`、`git diff --check` 与 Android 真机复核

截至 `2026-04-30` 的第四批收口结论：

1. 第四批“复访深化 + 已登录轻互动回看”当前已完成一个可收口小批次：`profile` 我的轻回应、最近公开评论、最近公开帖子、最近 forum 阅读、最近 docs 阅读、docs 正文内链、docs 关键词搜索、discover 文档直达、discover 论坛精选直达与 discover 快捷入口返回上下文均已完成代码、自动化验证与 Android 真机复核
2. 第四批验证面已覆盖 `flutter test test/smoke_test.dart`、`flutter analyze`、`git diff --check` 与 Android 真机人工复核；真机复核中发现的问题均已回写为代码修复与定向测试
3. 第四批不再继续追加功能项；发帖、完整评论提交、点赞、投票、编辑治理、完整通知中心、系统通知栏推送、完整浏览历史中心和多条记录治理继续后置
4. 第五批候选仅进入判断池：`profile` 最近阅读轻量多条列表、docs 搜索体验小增强、forum detail 轻回应发布后局部体验补强
5. 下一步若继续开发，应先从第五批候选中选择一个窄范围小闭环，并重新明确验证入口；不要把第四批收口误判为继续扩张完整社区互动能力的信号

截至 `2026-04-30` 的 Android MVP 收口复核与 RC 前置整理：

1. Android MVP 当前已具备内部 / 小范围 RC 候选链路：登录、退出、会话恢复、四个主 tab 真实读取、forum feed / detail / 评论阅读、轻回应最小闭环、最小 forum notification 回流、profile / docs / discover 复访与返回上下文均已纳入已验证面
2. 外部分发在正式 release 包发布前仍依赖 testing Gateway、外部分发对象、真机验收与批次级回归留痕；这些前置条件在个人开发阶段暂缓，不应被误判为业务功能阻塞
3. RC 前置判断已收口到 [Flutter Android RC 分发前置清单](/guide/flutter-android-rc-distribution)，后续准备分发时按该清单执行，不在分发前置批次临时扩系统通知栏推送、完整通知中心、发帖、完整评论提交、点赞、投票或编辑治理
4. Android MVP 收口复核与 RC 前置清单整理作为维护项已完成，不再作为第五批功能候选；第五批若启动，应另选一个窄范围产品小闭环

截至 `2026-04-30` 的第五批首个落地事实：

1. `profile` 最近阅读轻量多条列表已接入我的主页：已登录态当前可在我的 `profile` 查看最近多条 forum 阅读上下文，而不是只保留单个最近帖子入口
2. 最近阅读列表继续复用 `ForumDetailHandoffTarget` 与本地最近阅读语义，不新增后端 API、Flutter 专属 BFF 或独立详情页；壳层状态条仍只展示最新一条“继续阅读论坛”，profile 内展示轻量多条列表
3. Android 本地持久化已兼容旧的单条 `forum_recent_browse_handoff`：新列表最多保留 5 条，按 `postId + commentId` 去重并保持最近打开优先；旧单条数据可作为列表首条回落
4. 从 profile 内打开任意最近阅读条目时继续使用 `profileRecentBrowse` 来源，详情返回后仍回到 profile，不落到论坛首页
5. 本轮仍不扩展完整浏览历史中心、删除、清空、筛选、跨端同步治理、docs / forum 混合时间线、系统通知栏推送、完整通知中心、发帖、完整评论提交、点赞、投票或编辑治理
6. 当前验证已通过 `flutter test test/profile_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter test`、`flutter analyze`、`git diff --check`、Android 平台 `.\gradlew.bat :app:testDebugUnitTest` 与 Android 真机人工复核；该小闭环可作为第五批首个落点收口

截至 `2026-04-30` 的第六批落地事实：

1. `forum detail` 轻回应发布后局部体验补强已完成代码与自动化验证：已登录用户发布轻回应成功后，不再通过整页刷新确认结果，也不重载帖子正文或评论列表
2. 新发布的轻回应会使用 `PostQuickReply/Create` 返回值即时前插到轻回应墙顶部，并在轻回应区展示“轻回应已发布，已显示在轻回应墙顶部。”的局部成功反馈
3. 发布失败继续只落在轻回应区内提示，正文、评论阅读与已有轻回应不会被错误态拖垮；用户输入保留，可直接重试
4. 本轮继续复用现有 `PostQuickReply` 契约、`ForumDetailHandoffTarget`、forum detail 原地登录续接与最近阅读语义，不新增后端 API、Flutter 专属 BFF 或独立互动系统
5. 本轮仍不开放删除、举报、完整评论提交、点赞、投票、编辑治理、完整通知中心或系统通知栏推送
6. 当前验证已通过 `flutter test test/forum_detail_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter test`、`flutter analyze`、`git diff --check` 与 Android 真机人工复核；该小闭环可作为第六批落点收口

截至 `2026-05-01` 的第七批首个落地事实：

1. Flutter docs 搜索体验小增强已完成代码与自动化验证：从搜索结果滚动到较深位置并打开内联文档详情后，返回列表会恢复原搜索结果附近的滚动上下文，而不是回到不确定位置
2. 搜索关键词提交时会先按现有 `Wiki/GetList?keyword=...` 契约做本地 trim 归一化，并把输入框同步为实际搜索词；清除搜索、切换分页与重新搜索时会回到结果顶部
3. 本轮继续复用现有 docs 列表、docs detail、`DocsDetailHandoffTarget` 与公开只读边界，不新增后端 API、Flutter 专属 BFF、搜索建议、历史搜索、全文高亮、目录树、编辑、发布、版本历史或完整浏览历史治理
4. 真机复核发现“从搜索结果进入文档详情后 Android Back 直接退到桌面”的壳层返回分流问题，当前已收口为 `DocsPage` 向 `RadishFlutterShell` 暴露内联详情返回处理器；文档 tab 处于内联详情态时，根层 Back 会优先回到搜索列表，不再触发退到桌面
5. 当前验证已通过 `flutter test test/docs_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze` 与 `git diff --check`；Android 真机复核已确认通过，该小闭环可作为第七批首个落点收口

截至 `2026-05-01` 的 Android MVP 内测分发前置整理：

1. 第七批首个小闭环收口后，当前下一步优先转向 Android MVP 内测分发前置整理，不启动第八批功能扩张
2. Android MVP 内部 / 小范围 RC 候选链路继续成立：登录、退出、会话恢复、四个主 tab 真实读取、forum feed / detail / 评论阅读、轻回应最小闭环、最小 forum notification 回流、profile / docs / discover 复访与返回上下文均已纳入已有验证面
3. 真实签名材料已由用户本机补齐并通过 `:app:checkReleaseSigningConfig`；testing Gateway、测试账号 / 测试数据、分发对象、反馈回收方式与真机安装复核在个人开发阶段暂缓，正式 release 包发布前再补，这些前置缺失不构成当前业务功能阻塞
4. 本轮已补 [Flutter Android MVP 内测分发前置整理记录（2026-05-01）](/guide/flutter-android-internal-rc-prep-record-2026-05-01)，并同步 [Flutter Android RC 分发前置清单](/guide/flutter-android-rc-distribution) 中的内测 RC 前置判断
5. 个人开发阶段暂缓 testing Gateway release APK 构建、真机验收与批次级外部分发回归留痕；上述动作统一留到正式 release 包发布前再按 RC 清单补齐，不回头扩系统通知栏推送、完整通知中心、发帖、完整评论提交、点赞、投票或编辑治理
6. 暂缓真机 APK 安装与外部分发验收，不代表暂停开发中的模拟器功能测试；后续 Flutter MVP 功能推进仍可按需使用 Android Studio 模拟器 / AVD 验证页面、导航、登录回跳与 Gateway 接线，模拟器验证不替代正式 release 包发布前的真机安装验收

截至 `2026-05-01` 的第八批首个落地事实：

1. `profile` 最近文档轻量多条列表已完成代码与自动化验证：已登录态我的 `profile` 当前可展示最近多篇公开文档阅读上下文，而不是只保留单个最近文档入口
2. 最近文档列表继续复用 `DocsDetailHandoffTarget` 与本地最近文档语义，不新增后端 API、Flutter 专属 BFF、独立文档详情页或完整浏览历史中心；壳层状态条仍只展示最新一条“继续阅读文档”
3. Android 本地持久化已兼容旧的单条 `docs_recent_document_target`：新列表最多保留 5 条，按 `slug` 去重并保持最近打开优先
4. 从 profile 内打开任意最近文档条目时继续使用 `DocsDetailHandoffSource.profileRecentDocument`，详情返回后仍回到 profile，不落到 docs 列表或 discover
5. 本轮仍不扩展完整浏览历史中心、删除、清空、筛选、跨端同步治理、搜索历史、目录树、编辑、发布、版本历史、后端搜索改造、系统通知栏推送、完整通知中心或 Flutter 专属 BFF
6. 当前验证已通过 `flutter test test/profile_page_test.dart test/smoke_test.dart`、`flutter test test/docs_page_test.dart`、`flutter test`、`flutter analyze`、`.\gradlew.bat :app:testDebugUnitTest` 与 `git diff --check`；后续收口复核已再次确认 profile / docs 定向测试、`flutter analyze` 与 Android JVM 单测通过；Android 真机安装按用户要求暂缓到正式 release 包发布前，该小闭环可作为第八批首个落点收口
7. 下一步保持产品小闭环优先：个人开发阶段不再等待 testing Gateway、测试账号 / 测试数据、分发对象、反馈闭环、testing release APK 或真机验收；若继续功能，仍只从窄范围复访或只读体验补强中选择，不把暂缓分发验收误判为系统通知栏推送、完整通知中心、发帖、完整评论提交、点赞、投票或编辑治理的扩张信号；开发中可继续使用 Android Studio 模拟器 / AVD 做功能测试，但不写作 release 前真机安装验收结论

截至 `2026-05-01` 的第九批首个落地事实：

1. `profile` 复访区块体验整理已完成代码与自动化验证：最近文档、最近阅读、我的轻回应、最近公开帖子与最近公开评论区块统一使用图标标题、共享空态和局部提示样式
2. 我的主页在没有最近 forum / docs 记录时，会在公开内容之后展示轻量空态，避免把公开帖子 / 评论回跳入口挤到过低位置；公开主页继续不显示个人复访区块
3. 加载更多失败继续只在对应区块内提示，不拖垮 profile 公开资料、公开帖子、公开评论或我的轻回应回看
4. 本轮仍不新增后端 API、Flutter 专属 BFF、完整浏览历史中心、删除、清空、筛选、跨端同步、系统通知栏推送或完整通知中心
5. 当前验证已通过 `flutter test test/profile_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze` 与 `git diff --check`；Android 真机 APK 安装按个人开发阶段口径暂缓到正式 release 包发布前

截至 `2026-05-01` 的第十批首个落地事实：

1. `profile` 区块顺序与信息密度微调已完成代码与自动化验证：我的主页优先呈现最近复访 / 最近文档 / 最近阅读 / 我的轻回应，再进入最近公开帖子与最近公开评论；公开主页只展示最近公开帖子与最近公开评论
2. 无最近 forum / docs 记录时，原先两个完整空态区块已收束为一个紧凑“最近复访”空态卡，减少对公开帖子和评论回跳入口的挤压
3. 复访、轻回应、公开帖子与公开评论区块描述文案已收短，减少重复边界说明；边界仍通过公开只读口径、回跳来源与回归记录约束
4. 本轮仍不新增后端 API、Flutter 专属 BFF、完整浏览历史中心、删除、清空、筛选、跨端同步、系统通知栏推送或完整通知中心
5. 当前验证已通过 `flutter test test/profile_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze` 与 `git diff --check`；Android 真机 APK 安装按个人开发阶段口径暂缓到正式 release 包发布前

截至 `2026-05-01` 的第十一批首个落地事实：

1. Flutter docs 详情只读上下文补强已完成代码与自动化验证：详情正文顶部新增轻量只读上下文，明确来源、公开地址与“仅阅读，不提供编辑、发布或版本治理入口”的边界
2. docs 详情 slug 与来源类型芯片改为受控宽度显示，长 slug 在窄屏详情页不再撑破布局；详情上下文中的公开地址也会限制为两行省略
3. docs 详情错误态新增目标 `/docs/:slug` 提示，并明确可返回来源后重试，避免只显示服务错误而丢失用户当前要打开的文档上下文
4. 本轮继续复用现有 docs 列表、docs detail、`DocsDetailHandoffTarget`、正文内链跳转与公开只读边界，不新增后端 API、Flutter 专属 BFF、目录树、编辑、发布、版本历史、完整 Markdown 引擎或完整浏览历史治理
5. 当前验证已通过 `flutter test test/docs_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze` 与 `git diff --check`；Android 真机 APK 安装按个人开发阶段口径暂缓到正式 release 包发布前

截至 `2026-05-01` 的第十二批首个落地事实：

1. Flutter forum detail 来源上下文与错误态补强已完成代码与自动化验证：详情正文顶部新增轻量只读上下文，明确来源、公开地址、可选评论定位目标与“仅阅读与最小轻回应，不提供评论提交、点赞、投票或编辑入口”的边界
2. forum detail 详情错误态新增目标 `/forum/post/:postId`、来源与可选 `commentId` 提示，并明确可返回来源后重试，避免只显示服务错误而丢失用户当前要打开的帖子 / 评论上下文
3. 评论定位失败提示会带出目标评论 ID，并继续作为局部提示保留帖子正文、轻回应和评论阅读，不把定位失败升级为整页失败
4. forum detail 的公开地址 chip 与基础 meta 文本改为受控宽度显示，长帖子 ID、长分类或长时间文本在窄屏详情页不再撑破布局
5. 本轮继续复用现有 forum detail、`ForumDetailHandoffTarget`、评论定位链路、轻回应墙与公开只读边界，不新增后端 API、Flutter 专属 BFF、完整通知中心、系统通知栏推送、发帖、完整评论提交、点赞、投票或编辑治理
6. 当前验证已通过 `flutter test test/forum_detail_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze` 与 `git diff --check`；Android 真机 APK 安装按个人开发阶段口径暂缓到正式 release 包发布前，开发中仍可按需补 Android Studio 模拟器 / AVD 功能测试

截至 `2026-05-02` 的第十三批首个落地事实：

1. Flutter discover 只读分发上下文补强已完成代码与自动化验证：发现页内容区新增轻量“发现上下文”卡，明确来源 `/discover`、公开只读分发角色与继续进入 forum / docs / profile 的阅读边界
2. discover 摘要条目的标题、摘要、meta 与 chip 已改为受控行数和受控宽度显示，长帖子标题、长文档 slug、长分类或长商品名在窄屏发现页不再撑破布局
3. 本轮继续复用现有 `DiscoverSnapshot`、`ForumDetailHandoffTarget`、`DocsDetailHandoffTarget` 与原生来源返回语义，不新增后端 API、Flutter 专属 BFF、独立路由系统或完整工作台能力
4. 本轮仍不扩完整通知中心、系统通知栏推送、发帖、完整评论提交、点赞、投票、编辑治理、购买、订单或背包能力
5. 当前验证已通过 `flutter test test/discover_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze` 与针对本次变更文件的 `git diff --check`；Android 真机 APK 安装按个人开发阶段口径暂缓到正式 release 包发布前

截至 `2026-05-02` 的第十四批首个落地事实：

1. Flutter discover 聚合摘要局部失败降级已完成代码与自动化验证：forum / docs / shop 三路摘要在原生聚合层独立保护，单一区块失败时不再拖垮整页发现页
2. `DiscoverSnapshot` 新增轻量 `sectionIssues`，用于记录局部区块失败；页面会展示“部分发现内容暂时不可用”，可用区块继续展示，失败区块保留原有空态并可通过刷新重试
3. 本轮继续复用现有 `Post/GetList`、`Wiki/GetList` 与 `Shop/GetProducts` 契约，不新增后端 API、Flutter 专属 BFF、独立重试队列或跨端同步治理
4. 仓储整体抛错的整页错误态继续保留，用于覆盖启动配置、网络层或不可恢复聚合异常；局部失败只处理单个公开摘要区块的可降级问题
5. 当前验证已通过 `flutter test test/discover_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze` 与针对本次变更文件的 `git diff --check`；Android 真机 APK 安装按个人开发阶段口径暂缓到正式 release 包发布前

截至 `2026-05-02` 的第十五批首个落地事实：

1. Flutter discover 刷新体验与局部 issue 清理复核已完成代码与自动化验证：发现页已有摘要时触发刷新会保留上次可用内容，并展示轻量“正在刷新发现内容”状态，不再把页面切回整页 loading
2. 刷新失败会作为局部提示展示“刷新发现失败”，旧 snapshot 继续可读；首次加载失败仍保留原有整页错误态，用于覆盖启动配置、网络层或不可恢复聚合异常
3. 成功刷新会以新 `DiscoverSnapshot` 为准清理旧 `sectionIssues`；当 forum / docs / shop 三路摘要全部降级时，页面仍展示“部分发现内容暂时不可用”与各区块空态，不误判为普通无内容
4. 本轮继续复用现有 `DiscoverFeedController`、`DiscoverSnapshot` 与三路公开摘要契约，不新增后端 API、Flutter 专属 BFF、独立重试队列、下拉刷新体系或跨端同步治理
5. 当前验证已通过 `flutter test test/discover_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze` 与 `git diff --check`；Android 真机 APK 安装按个人开发阶段口径暂缓到正式 release 包发布前

截至 `2026-05-02` 的第十六批首个落地事实：

1. Flutter forum / docs 主列表刷新体验一致性已完成代码与自动化验证：forum feed 与 docs 列表已有内容时点击刷新会保留上次可用列表，并展示轻量刷新态，不再把页面切回整页 loading
2. forum / docs 刷新失败会分别显示局部“刷新论坛失败”“刷新文档失败”，旧列表继续可读；首次加载失败仍保留原有整页错误态
3. 成功刷新会替换为新列表并清理旧刷新失败提示；分页、搜索、排序切换仍保持原有加载语义，不与本轮刷新体验收口混在一起
4. 本轮继续复用现有 `ForumFeedController`、`DocsFeedController` 与公开列表契约，不新增后端 API、Flutter 专属 BFF、下拉刷新体系或跨端同步治理
5. 当前验证已通过 `flutter test test/forum_page_test.dart test/docs_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze` 与 `git diff --check`；Android 真机 APK 安装按个人开发阶段口径暂缓到正式 release 包发布前

截至 `2026-05-02` 的第十七批首个落地事实：

1. Flutter profile 主资料刷新体验一致性已完成代码与自动化验证：已有公开资料时点击刷新会保留上次可用资料、公开帖子、公开评论、我的轻回应与复访区块，并展示轻量刷新态，不再把页面切回整页 loading
2. profile 刷新失败会显示局部“刷新资料失败”，旧内容继续可读；首次加载失败仍保留原有整页错误态
3. 成功刷新会替换为新资料并清理旧刷新失败提示；公开帖子、公开评论和我的轻回应的加载更多局部失败逻辑保持不变
4. 本轮继续复用现有 `ProfileController` 与公开资料契约，不新增后端 API、Flutter 专属 BFF、完整浏览历史、资料编辑、关注或治理能力
5. 当前验证已通过 `flutter test test/profile_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze` 与 `git diff --check`；Android 真机 APK 安装按个人开发阶段口径暂缓到正式 release 包发布前
