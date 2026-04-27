# Flutter Android RC 分发前置清单

> 适用范围：`Phase 2-3 Flutter 客户端 MVP` 的 Android 测试环境 / 外部分发前置。
>
> 当前清单只处理 RC 分发准备，不纳入商店发布、系统通知栏推送、完整通知中心或正式生产发布流程。

## 前置边界

- 不提交真实 `android/key.properties`、`.jks` 或 `.keystore`
- 不把 Flutter 客户端改成独立 BFF
- 不在 Android RC 前置批次扩展聊天、商城工作台、创作器或桌面治理能力
- 测试 / 正式 Gateway 必须通过 `--dart-define=RADISH_GATEWAY_BASE_URL=...` 指定

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
