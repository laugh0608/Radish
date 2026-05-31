# Radish Flutter

`radish.flutter` 是 `Phase 2-3 Flutter 客户端 MVP` 的仓库落点。

当前状态是 Android MVP 第一轮已完成，Flutter 后续定位收束为 Android / iOS 移动原生安装包路线；Windows / macOS / Linux 桌面安装包不再按 Flutter 默认扩平台推进，优先走 `Tauri 壳 + WebOS 桌面工作台`。

## 当前范围

- Android 起步的原生客户端壳层；iOS 后续单独评估
- `discover / forum / docs / profile` 四个高价值入口的首批真实只读页面
- 最小登录、退出、会话恢复、Android 本地会话持久化与浏览器 OIDC 回调
- forum feed、forum detail、评论分页、子评论分页、评论发布 / 回复、作者跳转与 detail 原地登录续接
- forum detail 轻回应发布后局部体验：发布成功后即时前插到轻回应墙并给出局部成功反馈，发布失败只在轻回应区提示；从轻回应区发起登录后会回到当前轻回应区继续发布
- discover 论坛精选直达：`discover` 中的论坛精选帖子可直接打开原生 forum detail，并按发现页来源返回
- `profile` 我的轻回应回看：已登录态可在我的主页查看最近轻回应、继续加载更多，并回到对应 forum detail
- `profile` 最近阅读轻量多条列表：已登录态可在我的主页继续打开最近多条 forum 阅读目标，并从详情返回 profile
- `profile` 最近公开帖子回看：公开主页与我的主页可继续加载更多公开帖子，并回到对应 forum detail
- `profile` 最近公开评论回看：公开主页与我的主页可继续加载更多公开评论，并回到对应 forum detail / 评论上下文
- docs 最近阅读与直达复访：`discover` 文档精选、docs 列表与我的 `profile` 最近文档可打开原生 docs 详情，并按来源返回
- docs 正文内链跳转：公开文档正文中的 `/docs/:slug`、完整公开 URL、`docs/:slug`、`./:slug` 与普通相对 slug 文档链接会继续打开原生 docs 详情；页内锚点、附件路径和非 docs 链接仍按文本展示
- docs 关键词搜索复访：docs tab 可搜索公开文档，搜索结果继续复用原生列表、分页、刷新与详情打开路径
- leaderboard 公开主页回流：经验榜用户可打开原生公开主页，并通过 Android 返回键回到榜单
- shop 公开商品列表与只读详情：发现页可进入原生公开商城列表，列表项和商城精选商品可打开原生只读详情，并通过 Android 返回键回到原来源；公开商城仍不开放购买和支付操作
- 原生公开详情链接复制：forum detail、docs detail 与 shop detail 展示完整公开链接并支持复制；复制口径为当前 Gateway Base URL 加 Web 公开路由，不接入系统分享 SDK
- 公开主页来源返回：从发现、论坛作者和榜单进入原生公开主页后，Android Back 会回到原来源；公开主页继续打开帖子 / 评论详情并返回后，仍保留原 profile 来源 tab
- 已登录态通知只读列表：原生壳层会读取当前用户最近站内通知，展示标题、内容、类型、已读状态和时间；forum 通知可复用 forum detail handoff 打开 `postId / commentId`，系统等不可跳通知保持只读展示
- 已登录态商城订单 / 背包只读入口：我的页可打开订单列表、订单详情和背包，背包权益 / 道具可查看来源订单或来源商品；当前不开放购买、取消订单、支付口令、权益激活或道具使用
- 已登录态胡萝卜资产只读入口：我的页可查看可用余额、冻结余额、累计统计和最近流水；当前不开放转账、打赏、调账或支付操作
- 已登录态经验记录只读入口：我的页可查看等级、当前经验、总经验、升级进度、冻结状态和最近经验流水；当前不开放经验调整、冻结治理或管理员复核
- 环境配置、认证存储与主题基线
- 与现有 Web / API 契约一致的复用边界
- Android 模拟器经 Gateway `https://localhost:5000` 的最小联调入口

## 当前不含

- iOS 产品化工程
- Windows / macOS / Linux 桌面安装包平台目录
- 聊天、完整通知中心、完整商城工作台、完整资产中心、创作器
- “移动版 WebOS”

## 公开路由与链接口径

Flutter 原生详情不定义 Flutter 专属公开 URL。forum detail、docs detail 与 shop detail 展示和复制的公开链接统一由当前 `RADISH_GATEWAY_BASE_URL` 加 Web 公开路由组成：

- forum detail：仅当帖子返回 `Post.PublicId` 时生成 `/forum/post/:publicId` 公开复制链接；旧 `postId` 仍可作为打开兼容 fallback，但不作为分享地址展示
- docs detail：使用 `/docs/:slug`；16 位以上纯数字旧 long slug 只承担兼容打开能力，不生成公开复制链接
- shop detail：使用 `/shop/product/:productId`

复制链接不携带来源 tab、目标评论、内部 handoff、`radish://` deep link 或 API 地址。当前只提供完整链接展示和剪贴板复制，不接系统分享 SDK、海报生成或分享统计。

原生 docs 正文内链只承接公开文档阅读：`/docs/:slug`、完整公开 URL、`docs/:slug`、`./:slug` 与普通相对 slug 会继续打开原生 docs detail；页内锚点、附件路径和非 docs 链接按文本展示，不扩外部浏览器跳转或附件治理。

## 目录概览

```text
Clients/radish.flutter/
├── android/
├── lib/
│   ├── app/
│   ├── core/
│   ├── features/
│   └── shared/
├── test/
├── analysis_options.yaml
├── pubspec.yaml
└── .gitignore
```

## 后续接线顺序

1. Flutter 环境切换能力：支持通过构建参数指定本机 / 测试 / 正式 Gateway
2. Android 正式签名材料与外部分发前置准备：详见 [Flutter Android RC 分发前置清单](../../Docs/guide/flutter-android-rc-distribution.md)
3. Android MVP 产品化深化：测试对象、反馈回收、已知问题列表、版本说明、release 前验收与分发留痕
4. iOS 作为移动安装包后续单独评估，不与 Android 第一轮完成结论混成同一批
5. Windows / macOS / Linux 桌面安装包转入 `Tauri + WebOS` 第二轮评估，不在本目录生成 Flutter 桌面平台工程

## Flutter 环境切换

客户端默认使用本机开发 Gateway：

```text
https://localhost:5000
```

构建或运行时可通过 `--dart-define` 指定目标环境。当前保持 API / Auth / Gateway 同源，统一由 `RADISH_GATEWAY_BASE_URL` 派生，不额外引入 Flutter 专属 BFF。

可用参数：

- `RADISH_ENVIRONMENT`：环境名称，默认 `development`，常用值为 `development` / `testing` / `production`
- `RADISH_GATEWAY_BASE_URL`：Gateway 基址，需包含 `https://` 或 `http://`，末尾 `/` 会自动收口
- `RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES`：是否允许本机开发证书，默认仅 Android + `localhost` Gateway 自动开启

本机 Android 开发态示例：

```powershell
flutter run --dart-define=RADISH_ENVIRONMENT=development --dart-define=RADISH_GATEWAY_BASE_URL=https://localhost:5000
```

测试环境 RC 构建示例：

```powershell
flutter build apk --release --dart-define=RADISH_ENVIRONMENT=testing --dart-define=RADISH_GATEWAY_BASE_URL=https://test-gateway.example
```

正式环境 RC 构建示例：

```powershell
flutter build apk --release --dart-define=RADISH_ENVIRONMENT=production --dart-define=RADISH_GATEWAY_BASE_URL=https://gateway.example
```

## 平台目录说明

Android 平台目录已经生成。当前不建议在本目录补齐 Windows / macOS / Linux 平台工程；桌面安装包路线以 `Frontend/radish.client` 的 WebOS 工作台复用和 Tauri 壳层能力评估为准。

## Android 模拟器联调

Flutter Android 开发态统一通过 Gateway 访问后端，入口保持为：

```text
https://localhost:5000
```

在 Android 模拟器中，`localhost` 默认指向模拟器自身。为保持 Gateway 本地开发证书的 `localhost` 主机名不漂移，启动应用前需要先建立 ADB 反向端口映射：

```powershell
adb reverse tcp:5000 tcp:5000
```

如果当前终端找不到 `adb`，使用 Android SDK 的完整路径执行，例如：

```powershell
D:\MyKits\android\platform-tools\adb.exe reverse tcp:5000 tcp:5000
```

不要把 Flutter 默认开发入口改成 `https://10.0.2.2:5000`。该地址虽然能指向宿主机，但会让请求主机名从 `localhost` 变成 `10.0.2.2`，与本地 Gateway 开发 HTTPS 证书不一致，容易在 TLS 握手阶段出现 `HandshakeException`。

## Android release 签名配置

Android MVP RC 构建当前允许在未配置正式签名时回落到 debug signing，以便本地继续执行：

```powershell
flutter build apk --release
```

正式分发前，需要在 `Clients/radish.flutter/android/` 下复制示例配置：

```powershell
Copy-Item key.properties.example key.properties
```

并填写真实签名信息：

```properties
storeFile=upload-keystore.jks
storePassword=<keystore-password>
keyAlias=<key-alias>
keyPassword=<key-password>
```

`storeFile` 路径相对 `Clients/radish.flutter/android/` 解析。真实 `key.properties`、`.jks` 与 `.keystore` 文件不得提交到仓库。

一旦 `key.properties` 存在，Gradle 会要求字段完整、不能使用示例占位值，并确认 `storeFile` 指向的 keystore 文件存在。仅检查签名材料是否已具备外部分发前置条件时，执行：

```powershell
cd android
$env:JAVA_HOME='D:\Program Files\JetBrains\Android Studio\jbr'
.\gradlew.bat :app:checkReleaseSigningConfig
```

若暂未准备正式签名材料，请不要保留半成品 `key.properties`；删除该文件后，本地 `flutter build apk --release` 会继续回落到 debug signing。

## Android 真机人工验证 checklist

当前 Android MVP 人工验证只覆盖已经具备真实入口、真实数据或可稳定手工触发的链路。已登录态当前具备通知只读列表、商城订单 / 背包、胡萝卜资产和经验记录入口，可用于验证移动端登录态只读承接与来源返回；forum 通知仍可验证回到 forum detail 与 `commentId` 定位。

第十五批至第十七批已完成 `discover / forum / docs / profile` 主 tab 的刷新体验一致性收口。开发阶段可优先使用 Android Studio 模拟器 / AVD 做功能验证；若需要验证刷新失败局部提示，可在已有内容成功加载后临时断开 Gateway 或网络再点击刷新。该验证只记录为开发阶段人工检查，不替代正式 release 包发布前的真机 APK 安装验收。

前置条件：

- 后端宿主、Gateway 与 Auth 已由人工按项目启动命令启动；不要由 AI 直接执行 `dotnet run` 或 `npm run dev`
- Android 设备能访问 `https://localhost:5000` 对应的本地 Gateway；模拟器联调前已执行 `adb reverse tcp:5000 tcp:5000`
- 测试账号可完成浏览器 OIDC 登录，并能从 `radish://oidc/callback` 回到应用
- 当前环境中至少存在可公开读取的 forum 帖子；评论链路验证需要选择一条已有根评论或子评论的帖子；轻回应验证需要选择一条可发布轻回应的帖子
- 若验证通知回流，需要使用另一个账号在 Web / 桌面端评论或回复目标用户的帖子 / 评论，确保接收账号产生一条 forum 相关通知；系统等不可跳通知只需确认保持只读展示

建议按以下顺序验收：

1. 启动应用，确认启动恢复 gate 后进入匿名态或已恢复会话态，壳层状态条不溢出
2. 进入 `discover / docs / profile`，确认首批真实只读内容可读取，基础跳转可返回原 tab
3. 在 discover 已有摘要时点击“刷新发现”，确认刷新中仍展示上次可用摘要；若刷新失败，应只显示“刷新发现失败”局部提示，不切到整页错误
4. 从 discover 点击“进入论坛”，确认可切到论坛列表；按 Android 返回键应先回到 discover，而不是退出到桌面
5. 从 discover 点击“进入文档”，确认可切到文档列表；按 Android 返回键应先回到 discover，而不是退出到桌面
6. 从 discover 的论坛精选帖子点击“打开帖子”，确认进入原生 forum detail，返回后仍回到 discover
7. 进入 docs，确认关键词搜索、分页、刷新与清除搜索可用；已有文档列表刷新时应保留旧列表，刷新失败只显示“刷新文档失败”局部提示；从搜索结果打开文档详情后，返回仍保留搜索列表上下文
8. 在 docs 详情正文中点击 `/docs/:slug`、完整公开 URL、`docs/:slug`、`./:slug` 或普通相对 slug 文档链接，确认会打开原生 docs 详情；页内锚点、附件路径和非 docs 链接应保持文本展示；从 docs 列表内联详情进入时应切换当前详情，从 discover / profile 来源详情进入时应 push 新详情并可逐层返回
9. 在根层按 Android 返回键回到桌面，再点桌面图标打开应用，确认应用恢复到 Flutter 页面而不是卡在原生启动页
10. 进入 forum feed，确认 `latest / hottest`、分页加载、空态或错误态文案正常；已有帖子列表刷新时应保留旧列表，刷新失败只显示“刷新论坛失败”局部提示
11. 从 forum feed 打开帖子详情，确认正文、作者、分类、时间与基础统计可读，原生返回正常
12. 在帖子详情读取评论区，确认根评论分页、子评论展开、评论作者跳转公开 profile 可用；没有评论时应显示明确空态
13. 已登录态在帖子详情发布一条根评论，确认不刷新正文或轻回应，新评论前插到评论区并出现局部成功提示；发布失败时应只在评论输入区提示
14. 已登录态从根评论或子评论点击回复，确认回复输入区显示目标用户与内容快照，提交后对应根评论回复数与子评论列表局部更新
15. 在帖子详情确认轻回应位于正文之后、评论区之前；匿名态可读取最近轻回应；已登录态发布一句轻回应后应保持当前位置，不刷新正文或评论，新轻回应应显示在轻回应墙顶部并出现局部成功提示
16. 匿名态在帖子详情发起登录，取消登录后应出现显式提示；重试登录成功后应回到原帖子上下文，并可继续发布轻回应或评论
17. 进入 profile，确认最近 forum 阅读、最近文档、公开主页复访、商城订单 / 背包、胡萝卜资产和经验记录入口是正式用户文案，可回到对应详情、公开主页或只读列表；已有公开资料刷新时应保留公开资料、公开帖子、公开评论、我的轻回应与复访区块，刷新失败只显示“刷新资料失败”局部提示
18. 在我的 profile 中确认“最近阅读”展示最近多条 forum 阅读上下文；重复打开同一帖子或评论应前置并去重；点击任意条目应回到对应帖子详情，返回后仍留在 profile
19. 在 profile 中确认“最近公开帖子”可继续加载更多；点击任意帖子应回到对应帖子详情
20. 在 profile 中确认“最近公开评论”可继续加载更多；点击任意评论应回到对应帖子，并自动定位到目标根评论或子评论位置，而不是停在帖子顶部
21. 在我的 profile 中确认“我的轻回应”只在已登录我的主页展示，公开主页不展示；若有多页轻回应，可继续加载更多；点击任意条目应回到对应帖子详情
22. 已登录态执行退出，确认浏览器登出回调后回到匿名态，后续重新进入 profile 或 detail 登录入口仍可用
23. 接收账号登录 Flutter 后，确认壳层出现通知入口；点击后应展示最近站内通知，选择 forum 通知可打开对应帖子，并在通知包含 `commentId` 时落到目标评论上下文；系统等不可跳通知保持只读展示
24. 在我的 profile 中打开“查看商城订单”和“查看背包”，确认订单详情、背包来源订单 / 商品可按来源返回，不出现购买、支付口令、权益激活或道具使用入口
25. 在我的 profile 中打开“查看胡萝卜资产”，确认余额概要、累计统计、最近流水、空态 / 错误态和返回 profile 正常，不出现转账、打赏、调账或支付入口
26. 在我的 profile 中打开“查看经验记录”，确认等级概要、升级进度、冻结状态、最近流水、空态 / 错误态和返回 profile 正常，不出现经验调整、冻结治理或管理员复核入口
27. 关闭并重启应用，确认会话恢复或匿名回落符合当前 token 状态，复访入口仍符合最近状态

当前结果：

- Android 真机已确认 forum notification 回到 forum detail / 评论上下文逻辑正常
- Android release APK 已完成一轮真机安装与本机 Gateway 联调复核，登录、基础读取与样式显示均正常
- Android release 包身份当前为 `com.radish.client` / `Radish`
- 第三批中文文案、个人复访入口与轻回应发布已完成一轮 Android 真机复核，真实 Gateway 下确认正常
- 本轮复核发现并修复同一帖子详情返回列表后无法再次打开的问题；当前已补 `forum_page_test.dart` 定向回归测试
- 第六批“forum detail 轻回应发布后局部体验补强”已完成代码、自动化验证与 Android 真机复核：发布成功后不整页刷新，新轻回应即时前插并显示局部成功反馈；发布失败只在轻回应区提示
- P3-8-D “forum detail 轻回应登录回流”已完成代码与自动化验证：匿名态从轻回应区发起登录后，会回到当前帖子轻回应区并提示可继续发布；本轮不扩展完整评论、发帖、点赞或投票能力
- P3-8-D “Flutter 公开商品只读详情”已完成代码与自动化验证：发现页商城精选商品可打开原生商品详情，详情只展示公开信息和只读购买边界，Android Back 返回发现页；本轮不扩展完整商城、购买、订单、背包、支付口令或权益激活
- P3-8-D “Flutter 登录态通知只读列表”已完成代码与自动化验证：已登录壳层展示最近站内通知，forum 通知可打开对应帖子 / 评论并返回打开前 tab，系统等不可跳通知只读展示；本轮不扩展完整通知中心、已读 / 删除、系统推送、聊天通知或商城通知
- P3-8-D “Flutter 登录态商城订单 / 背包只读入口”已完成代码与自动化验证：我的页可打开订单列表、订单详情和背包，背包来源订单 / 商品可返回原上下文；本轮不扩展购买、取消订单、支付口令、权益激活或道具使用
- P3-8-D “Flutter 登录态胡萝卜资产只读入口”已完成代码与自动化验证：我的页可查看可用余额、冻结余额、累计统计和最近流水；本轮不扩展转账、打赏、调账或支付
- P3-8-D “Flutter 登录态经验记录只读入口”已完成代码与自动化验证：我的页可查看等级、经验进度、冻结状态和最近经验流水；本轮不扩展经验调整、冻结治理或管理员复核
- P3-8-D “Flutter 论坛详情评论发布与回复”已完成代码与自动化验证：已登录用户可在原生帖子详情发布根评论、回复根评论或子评论，提交后局部更新评论区，失败只在评论输入区提示；本轮不扩展发帖编辑器、点赞、投票、审核治理或富文本评论
- P3-8-D “Flutter 论坛评论登录回流与来源回归”已完成代码与自动化验证：匿名态从评论区发起登录后会回到当前评论输入区；从公开主页评论上下文进入详情并回复后，Android Back 仍返回公开主页来源
- P3-8-D “Flutter 公开商城列表只读入口”已完成代码与自动化验证：发现页可进入公开商城列表，列表项可打开只读商品详情并返回商城列表；本轮不扩展购买、订单、背包、支付口令或权益激活
- P3-8-D “Flutter 原生公开详情分享入口”已完成代码与自动化验证：forum detail、docs detail 与 shop detail 支持完整公开链接展示和复制；本轮不扩展系统分享 SDK
- P3-8-D “Flutter 原生公开文档阅读链路”已完成代码与自动化验证：只读 Markdown 阅读器可识别 Web 公开路由、完整公开 URL 与相对文档链接并打开原生 docs detail；非 docs 链接继续按文本展示
- 第四批“我的轻回应回看”及分页复访已完成代码、自动化验证与 Android 真机复核，真实 Gateway 下确认正常
- 第四批“最近阅读上下文”已接入我的 profile，并已完成代码、自动化验证与 Android 真机复核：第四批只承载最近一次 forum 阅读目标
- 第五批“profile 最近阅读轻量多条列表”已完成代码、自动化验证与 Android 真机复核：我的 profile 当前可展示最近多条 forum 阅读上下文，Android 本地持久化兼容旧单条记录并按 `postId + commentId` 去重；本轮仍不扩展完整浏览历史中心、删除、清空、同步治理或 docs / forum 混合时间线
- 第四批“最近公开评论回看”分页复访已完成代码、自动化验证与 Android 真机复核；目标评论定位时机修复后，公开评论条目可打开对应帖子并落到目标根评论或子评论位置
- 第四批“最近公开帖子回看”分页复访已完成代码、自动化验证与 Android 真机复核；最近公开帖子、最近公开评论与我的轻回应三条 profile 复访路线从详情返回后都会回到 profile
- 第四批 docs 最近阅读与直达复访已完成代码、自动化验证与 Android 真机复核；discover 文档直达、docs 列表详情返回、profile 最近文档复访与重启恢复均确认正常
- 第四批 docs 正文内链跳转与 docs 关键词搜索复访已完成代码、自动化验证与 Android 真机复核；真机复核发现长 slug 窄屏溢出与搜索详情返回退出问题后，已补代码修复与回归测试并复测通过；调试态根层返回后重新打开卡启动页的问题已收口为 Android 根层 Back 退后台；当前已通过 `flutter test`、`flutter analyze`、`git diff --check` 与定向 docs / smoke 回归
- 第七批“docs 搜索体验增强”已完成代码、自动化验证与 Android 真机复核：搜索词会 trim 归一化，搜索 / 清除 / 翻页会回到结果顶部，从搜索结果打开内联详情后返回会恢复搜索列表上下文；真机复核发现的 Android Back 退桌面问题已修复并补 `docs_page_test.dart` 与 `smoke_test.dart` 覆盖；批次记录见 [Flutter Android MVP 第七批变更回归记录](../../Docs/guide/flutter-android-mvp-regression-record-2026-05-01.md)
- 第四批 discover 论坛精选直达已完成代码与定向测试补齐：发现页论坛精选帖子可直接打开原生 forum detail，并以 `discover` 来源返回发现页；discover 的“进入论坛 / 进入文档”快捷入口也已补齐 Android Back 返回 discover 的轻量来源上下文；仍不开放发帖、评论提交、点赞、投票或编辑治理
- 第十五批至第十七批刷新体验一致性已完成代码与自动化验证：`discover / forum / docs / profile` 在已有内容刷新时保留上次可用内容，刷新失败只显示局部提示，刷新成功后清理旧提示；模拟器验证清单见 [Flutter Android MVP 刷新体验开发阶段验证清单](../../Docs/guide/flutter-android-mvp-refresh-experience-checklist-2026-05-02.md)
- 通知入口只验证站内最近通知读取和 forum 来源回流，不代表完整通知中心、已读 / 删除或系统通知栏推送已纳入 Android MVP

暂不作为当前阻断项：

- 系统通知栏推送触发的 `notification` handoff
- 完整通知中心、聊天、商城工作台、创作器、发帖编辑器、点赞、投票、编辑或其他桌面治理能力

## Android 平台测试 JDK 约束

Flutter Dart 层测试继续在本目录执行：

```powershell
flutter test
```

Android 平台侧 JVM 单元测试不要直接使用本机默认 `java`。当前 Windows 本机默认 JDK 可能是过新的 `25.x`，Gradle / Kotlin DSL 会在解析阶段失败。执行 Android 平台测试时固定使用 Android Studio 自带 JBR：

```powershell
cd android
$env:JAVA_HOME='D:\Program Files\JetBrains\Android Studio\jbr'
.\gradlew.bat :app:testDebugUnitTest
```

如果 Android Studio 安装路径不同，先把 `JAVA_HOME` 指向对应安装目录下的 `jbr`，不要改用 Oracle / 系统默认 JDK。
