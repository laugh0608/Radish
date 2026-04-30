# Flutter Android RC 分发前置清单

> 适用范围：`Phase 2-3 Flutter 客户端 MVP` 的 Android 测试环境 / 外部分发前置。
>
> 当前清单只处理 RC 分发准备，不纳入商店发布、系统通知栏推送、完整通知中心或正式生产发布流程。

## 前置边界

- 不提交真实 `android/key.properties`、`.jks` 或 `.keystore`
- 不把 Flutter 客户端改成独立 BFF
- 不在 Android RC 前置批次扩展聊天、商城工作台、创作器或桌面治理能力
- 测试 / 正式 Gateway 必须通过 `--dart-define=RADISH_GATEWAY_BASE_URL=...` 指定

## Android MVP 收口状态

截至 `2026-04-30`，Android MVP 已具备一条可复核的原生只读主链路：

1. 启动恢复、匿名态 / 已登录态、登录、退出与会话恢复
2. `discover / forum / docs / profile` 四个主 tab 的真实读取
3. forum feed、forum detail、评论阅读、评论分页、子评论分页、作者跳转与评论精确定位
4. forum detail 原地登录续接与轻回应最小读写闭环
5. 已登录壳层最小 forum notification 回流
6. `profile` 我的轻回应、最近公开帖子、最近公开评论、最近 forum 阅读轻量复访与最近 docs 阅读复访
7. docs 最近阅读、discover 文档直达、docs 正文内链与 docs 关键词搜索复访
8. discover 论坛精选直达 forum detail，以及 discover “进入论坛 / 进入文档”快捷入口的 Android Back 返回 discover 上下文

当前 Android MVP 可作为“内部 / 小范围 RC 候选”继续准备，但外部分发仍需满足后续前置条件。

## 当前非阻断项

以下能力当前不作为 Android MVP RC 阻断项：

- 系统通知栏推送
- 完整通知中心、通知管理、标记已读与删除
- 发帖、完整评论提交、点赞、投票、编辑治理
- 聊天、完整商城工作台、创作器
- 完整浏览历史中心、删除 / 清空与多条记录同步治理
- Windows / Linux 平台目录与桌面端 Flutter 分发
- 商店发布、AAB 上传、生产发布留痕

这些能力不应在 RC 前置阶段被临时拉入范围；若要进入建设，需先回到 `Docs/planning/current.md` 重新定义批次。

## 外部分发阻塞条件

当前外部分发真正依赖以下条件，不应被误判为业务功能阻塞：

1. **真实签名材料**
   - 需要安全准备 `key.properties` 与 keystore
   - 必须通过 `:app:checkReleaseSigningConfig`
2. **测试 Gateway**
   - 需要一个稳定可访问的 HTTPS Gateway
   - 证书信任链应由环境解决，不默认依赖本机开发证书放行
3. **外部分发对象与渠道**
   - 需要明确测试账号、测试设备、分发对象与回收反馈方式
   - 当前不默认进入商店发布或生产分发
4. **最小回归留痕**
   - 外部分发前需记录自动化结果、APK 构建命令、Gateway 基址、真机复核结果与已知非阻断项
   - 可按本页“分发前最小验收”整理一份批次记录

## 签名材料准备

正式分发前，在 `Clients/radish.flutter/android/` 下准备：

```powershell
Copy-Item key.properties.example key.properties
```

填写真实签名信息：

```properties
storeFile=upload-keystore.jks
storePassword=<keystore-password>
keyAlias=<key-alias>
keyPassword=<key-password>
```

规则：

- `storeFile` 相对 `Clients/radish.flutter/android/` 解析
- `key.properties` 必须只保留在本机或安全的密钥管理位置
- `.jks` / `.keystore` 不进入版本库
- 若暂未准备正式签名材料，可删除 `key.properties`，本地 release 构建会回落到 debug signing
- 一旦 `key.properties` 存在，Gradle 会要求字段完整、不能使用示例占位值，且 `storeFile` 指向的文件必须存在

## 签名配置检查

仅检查签名材料是否满足外部分发前置：

```powershell
cd Clients/radish.flutter/android
$env:JAVA_HOME='D:\Program Files\JetBrains\Android Studio\jbr'
.\gradlew.bat :app:checkReleaseSigningConfig
```

预期：

- 正式签名材料完整时，任务通过并输出 keystore 路径
- 未准备 `key.properties` 时，任务失败并提示当前只能做本地 debug-signing RC 构建
- `key.properties` 存在但缺字段、仍是示例值或找不到 keystore 时，任务失败并列出具体问题

## 测试环境 RC 构建

测试环境 RC 构建必须显式指定 Gateway：

```powershell
cd Clients/radish.flutter
flutter build apk --release --dart-define=RADISH_ENVIRONMENT=testing --dart-define=RADISH_GATEWAY_BASE_URL=https://test-gateway.example
```

如果测试环境 Gateway 使用自签名证书，优先修正测试环境证书信任链；不要默认开启 `RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES`。该开关只用于本机 Android + `localhost` 开发联调边界。

## 分发前最小验收

在准备外部分发前，至少确认：

1. `flutter analyze` 通过
2. `flutter test` 通过
3. `.\gradlew.bat :app:testDebugUnitTest` 通过，且使用 Android Studio JBR
4. `.\gradlew.bat :app:checkReleaseSigningConfig` 通过
5. `flutter build apk --release --dart-define=RADISH_ENVIRONMENT=testing --dart-define=RADISH_GATEWAY_BASE_URL=<测试 Gateway>` 可产出 APK
6. APK 安装后可完成登录、退出、会话恢复、`discover / docs / profile` 基础读取、forum feed / detail / 评论阅读，以及最小 forum notification 回流

当前仍不把系统通知栏推送、完整通知中心、商店发布、AAB 上传或生产发布留痕纳入 Android MVP RC 前置阻断项。

## 建议执行顺序

准备一次 Android MVP RC 外部分发时，建议按以下顺序执行：

1. 确认测试 Gateway、测试账号、测试数据与外部分发对象已经就绪
2. 准备正式签名材料，并执行 `.\gradlew.bat :app:checkReleaseSigningConfig`
3. 执行 Dart 与 Android 平台自动化验证
4. 使用测试 Gateway 构建 release APK
5. 在真机安装 APK，按 `Clients/radish.flutter/README.md` 的 Android 真机人工验证 checklist 复核
6. 记录 APK 构建参数、Gateway 基址、自动化结果、真机结论与已知非阻断项

如果任一前置条件缺失，应保持“RC 外部分发等待前置材料”的判断，不回头扩大 Flutter 功能范围。
