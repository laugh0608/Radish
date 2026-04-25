# Radish Flutter

`radish.flutter` 是 `Phase 2-3 Flutter 客户端 MVP` 的仓库落点。

当前主线是 `Phase 2-3 Flutter 客户端 MVP`。Android MVP 当前优先验证已经接通的真实可测链路，不复刻 WebOS 桌面工作台。

## 当前范围

- Android 起步的原生客户端壳层
- `discover / forum / docs / profile` 四个高价值入口的首批真实只读页面
- 最小登录、退出、会话恢复、Android 本地会话持久化与浏览器 OIDC 回调
- forum feed、forum detail、评论分页、子评论分页、作者跳转与 detail 原地登录续接
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

1. Android MVP 当前可测链路稳定性复核
2. 最小 forum notification 来源读取与 `commentId` 回流人工验收
3. Android MVP 稳定后，再评估 Windows / Linux 平台目录与更深原生能力

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

## Android 真机人工验证 checklist

当前 Android MVP 人工验证只覆盖已经具备真实入口、真实数据或可稳定手工触发的链路。已登录态当前具备一个最小 forum notification 来源入口，可用于验证通知来源回到 forum detail 与 `commentId` 定位。

前置条件：

- 后端宿主、Gateway 与 Auth 已由人工按项目启动命令启动；不要由 AI 直接执行 `dotnet run` 或 `npm run dev`
- Android 设备能访问 `https://localhost:5000` 对应的本地 Gateway；模拟器联调前已执行 `adb reverse tcp:5000 tcp:5000`
- 测试账号可完成浏览器 OIDC 登录，并能从 `radish://oidc/callback` 回到应用
- 当前环境中至少存在可公开读取的 forum 帖子；评论链路验证需要选择一条已有根评论或子评论的帖子
- 若验证 notification 回流，需要使用另一个账号在 Web / 桌面端评论或回复目标用户的帖子 / 评论，确保接收账号产生一条 forum 相关通知

建议按以下顺序验收：

1. 启动应用，确认启动恢复 gate 后进入匿名态或已恢复会话态，壳层状态条不溢出
2. 进入 `discover / docs / profile`，确认首批真实只读内容可读取，基础跳转可返回原 tab
3. 进入 forum feed，确认 `latest / hottest`、分页加载、空态或错误态文案正常
4. 从 forum feed 打开帖子详情，确认正文、作者、分类、时间与基础统计可读，原生返回正常
5. 在帖子详情读取评论区，确认根评论分页、子评论展开、评论作者跳转公开 profile 可用；没有评论时应显示明确空态
6. 匿名态在帖子详情发起登录，取消登录后应出现显式提示；重试登录成功后应回到原帖子上下文
7. 已登录态执行退出，确认浏览器登出回调后回到匿名态，后续重新进入 profile 或 detail 登录入口仍可用
8. 接收账号登录 Flutter 后，确认壳层出现 `Forum notification` 入口；点击后应打开对应帖子，并在通知包含 `commentId` 时落到目标评论上下文
9. 关闭并重启应用，确认会话恢复或匿名回落符合当前 token 状态

当前结果：

- Android 真机已确认 `Forum notification` 回到 forum detail / 评论上下文逻辑正常
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
