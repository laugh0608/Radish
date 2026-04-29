# Radish Flutter

`radish.flutter` 是 `Phase 2-3 Flutter 客户端 MVP` 的仓库落点。

当前主线是 `Phase 2-3 Flutter 客户端 MVP`。Android MVP 当前优先验证已经接通的真实可测链路，不复刻 WebOS 桌面工作台。

## 当前范围

- Android 起步的原生客户端壳层
- `discover / forum / docs / profile` 四个高价值入口的首批真实只读页面
- 最小登录、退出、会话恢复、Android 本地会话持久化与浏览器 OIDC 回调
- forum feed、forum detail、评论分页、子评论分页、作者跳转与 detail 原地登录续接
- `profile` 我的轻回应回看：已登录态可在我的主页查看最近轻回应、继续加载更多，并回到对应 forum detail
- `profile` 最近阅读上下文：已登录态可在我的主页继续打开最近一次 forum 阅读目标，并从详情返回 profile
- `profile` 最近公开帖子回看：公开主页与我的主页可继续加载更多公开帖子，并回到对应 forum detail
- `profile` 最近公开评论回看：公开主页与我的主页可继续加载更多公开评论，并回到对应 forum detail / 评论上下文
- 已登录态最小 forum notification 来源读取：原生壳层会读取当前用户最新可跳 forum 的通知，并复用 forum detail handoff 打开 `postId / commentId`
- 环境配置、认证存储与主题基线
- 与现有 Web / API 契约一致的复用边界
- Android 模拟器经 Gateway `https://localhost:5000` 的最小联调入口

## 当前不含

- Windows / Linux 平台目录的完整生成文件
- 聊天、完整通知中心、完整商城工作台、创作器
- “移动版 WebOS”

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
3. Android MVP 稳定后，再评估 Windows / Linux 平台目录与更深原生能力

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

## 平台目录生成说明

Android 平台目录已经生成。后续如需补齐 Windows / Linux 平台工程，可在本目录执行：

```bash
flutter create --platforms=windows,linux .
```

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

当前 Android MVP 人工验证只覆盖已经具备真实入口、真实数据或可稳定手工触发的链路。已登录态当前具备一个最小 forum notification 来源入口，可用于验证通知来源回到 forum detail 与 `commentId` 定位；第三批新增的中文文案、个人复访入口与 forum 轻回应最小闭环也纳入下一轮真机复核。

前置条件：

- 后端宿主、Gateway 与 Auth 已由人工按项目启动命令启动；不要由 AI 直接执行 `dotnet run` 或 `npm run dev`
- Android 设备能访问 `https://localhost:5000` 对应的本地 Gateway；模拟器联调前已执行 `adb reverse tcp:5000 tcp:5000`
- 测试账号可完成浏览器 OIDC 登录，并能从 `radish://oidc/callback` 回到应用
- 当前环境中至少存在可公开读取的 forum 帖子；评论链路验证需要选择一条已有根评论或子评论的帖子；轻回应验证需要选择一条可发布轻回应的帖子
- 若验证 notification 回流，需要使用另一个账号在 Web / 桌面端评论或回复目标用户的帖子 / 评论，确保接收账号产生一条 forum 相关通知

建议按以下顺序验收：

1. 启动应用，确认启动恢复 gate 后进入匿名态或已恢复会话态，壳层状态条不溢出
2. 进入 `discover / docs / profile`，确认首批真实只读内容可读取，基础跳转可返回原 tab
3. 进入 forum feed，确认 `latest / hottest`、分页加载、空态或错误态文案正常
4. 从 forum feed 打开帖子详情，确认正文、作者、分类、时间与基础统计可读，原生返回正常
5. 在帖子详情读取评论区，确认根评论分页、子评论展开、评论作者跳转公开 profile 可用；没有评论时应显示明确空态
6. 在帖子详情确认轻回应位于正文之后、评论区之前；匿名态可读取最近轻回应，已登录态可发布一句轻回应
7. 匿名态在帖子详情发起登录，取消登录后应出现显式提示；重试登录成功后应回到原帖子上下文，并可继续发布轻回应
8. 进入 profile，确认最近 forum 阅读和公开主页复访入口是正式用户文案，可回到对应详情或公开主页
9. 在我的 profile 中确认“最近阅读”只作为最近一次 forum 阅读上下文展示；点击后应回到对应帖子详情，返回后仍留在 profile
10. 在 profile 中确认“最近公开帖子”可继续加载更多；点击任意帖子应回到对应帖子详情
11. 在 profile 中确认“最近公开评论”可继续加载更多；点击任意评论应回到对应帖子，并自动定位到目标根评论或子评论位置，而不是停在帖子顶部
12. 在我的 profile 中确认“我的轻回应”只在已登录我的主页展示，公开主页不展示；若有多页轻回应，可继续加载更多；点击任意条目应回到对应帖子详情
13. 已登录态执行退出，确认浏览器登出回调后回到匿名态，后续重新进入 profile 或 detail 登录入口仍可用
14. 接收账号登录 Flutter 后，确认壳层出现 `Forum notification` 入口；点击后应打开对应帖子，并在通知包含 `commentId` 时落到目标评论上下文
15. 关闭并重启应用，确认会话恢复或匿名回落符合当前 token 状态，复访入口仍符合最近状态

当前结果：

- Android 真机已确认 `Forum notification` 回到 forum detail / 评论上下文逻辑正常
- Android release APK 已完成一轮真机安装与本机 Gateway 联调复核，登录、基础读取与样式显示均正常
- Android release 包身份当前为 `com.radish.client` / `Radish`
- 第三批中文文案、个人复访入口与轻回应发布已完成一轮 Android 真机复核，真实 Gateway 下确认正常
- 本轮复核发现并修复同一帖子详情返回列表后无法再次打开的问题；当前已补 `forum_page_test.dart` 定向回归测试
- 第四批首个小闭环“我的轻回应回看”及分页复访已完成代码、自动化验证与 Android 真机复核，真实 Gateway 下确认正常
- 第四批“最近阅读上下文”已接入我的 profile，并已完成代码、自动化验证与 Android 真机复核：当前只承载最近一次 forum 阅读目标，不扩展完整浏览历史列表、删除或清空治理
- 第四批“最近公开评论回看”分页复访已完成代码、自动化验证与 Android 真机复核；目标评论定位时机修复后，公开评论条目可打开对应帖子并落到目标根评论或子评论位置
- 第四批“最近公开帖子回看”分页复访已完成代码、自动化验证与 Android 真机复核；最近公开帖子、最近公开评论与我的轻回应三条 profile 复访路线从详情返回后都会回到 profile
- 该入口只验证站内最新 forum 通知读取，不代表完整通知中心或系统通知栏推送已纳入 Android MVP

暂不作为当前阻断项：

- 系统通知栏推送触发的 `notification` handoff
- 完整通知中心、聊天、商城工作台、创作器、评论提交、点赞、投票、编辑或其他桌面治理能力

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
