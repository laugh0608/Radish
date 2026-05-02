# Flutter Android RC 分发前置清单

> 适用范围：`Phase 2-3 Flutter 客户端 MVP` 的 Android 测试环境 / 外部分发前置。
>
> 当前清单只处理 RC / release 前分发准备，不纳入商店发布、系统通知栏推送、完整通知中心或正式生产发布流程。
>
> 个人开发阶段暂缓 testing Gateway、真机 APK 安装与外部分发验收；这些动作统一留到正式 release 包发布前再执行。该暂缓只针对真实设备安装、release 包发布和外部分发验收，不禁止开发中继续使用 Android Studio 模拟器 / AVD 做功能验证。

## 前置边界

- 不提交真实 `android/key.properties`、`.jks` 或 `.keystore`
- 不把 Flutter 客户端改成独立 BFF
- 不在 Android RC 前置批次扩展聊天、商城工作台、创作器或桌面治理能力
- 测试 / 正式 Gateway 必须通过 `--dart-define=RADISH_GATEWAY_BASE_URL=...` 指定

## Android MVP 收口状态

截至 `2026-05-01`，Android MVP 已具备一条可复核的原生只读主链路：

1. 启动恢复、匿名态 / 已登录态、登录、退出与会话恢复
2. `discover / forum / docs / profile` 四个主 tab 的真实读取
3. forum feed、forum detail、评论阅读、评论分页、子评论分页、作者跳转与评论精确定位
4. forum detail 原地登录续接与轻回应最小读写闭环
5. 已登录壳层最小 forum notification 回流
6. `profile` 我的轻回应、最近公开帖子、最近公开评论、最近 forum 阅读轻量复访与最近 docs 阅读复访
7. docs 最近阅读、discover 文档直达、docs 正文内链、docs 关键词搜索复访与 docs 搜索体验增强
8. discover 论坛精选直达 forum detail，以及 discover “进入论坛 / 进入文档”快捷入口的 Android Back 返回 discover 上下文
9. docs 搜索结果进入内联详情后的 Android Back 返回搜索列表，不触发根层退到桌面

当前 Android MVP 可作为“内部 / 小范围 RC 候选”继续准备，但个人开发阶段不强制推进外部分发验收。testing Gateway、真机 APK 安装、分发对象与反馈闭环统一留到正式 release 包发布前再补。开发中仍可按需使用 Android Studio 模拟器 / AVD 连接本机或指定 Gateway 做功能验证；这类模拟器验证属于开发阶段验证，不等同于 release 前真机 APK 安装验收。当前批次回归记录见：[Flutter Android MVP 第七批首个小闭环变更回归记录（2026-05-01）](/guide/flutter-android-mvp-regression-record-2026-05-01)；内测分发前置整理见：[Flutter Android MVP 内测分发前置整理记录（2026-05-01）](/guide/flutter-android-internal-rc-prep-record-2026-05-01)。

截至 `2026-05-02`，当前已补一轮正式域名临时 smoke：使用 `https://radishx.com`（服务端版本 `v26.3.2-release`）在 Android Studio 虚拟机与 Android 真机验证帖子、文档和用户公开信息基础只读读取，未见异常。该结论只作为正式 HTTPS 域名兼容性补充，不替代正式 release 包发布前的 testing Gateway、release APK 构建、真机安装、登录 / 通知 / 写入链路与批次级外部分发回归。记录见：[Flutter Android MVP 正式域名临时 smoke 记录（2026-05-02）](/guide/flutter-android-mvp-radishx-smoke-record-2026-05-02)。

第二十批后已补 [Flutter Android MVP RC 补验评估记录（2026-05-02）](/guide/flutter-android-mvp-rc-supplemental-assessment-2026-05-02)：当前明确区分“已具备的 RC 候选能力”“开发阶段可先补的预检 / 材料整理”与“必须等 testing Gateway、release APK 和真机安装后才能完成的 release 前验收”。该评估不新增业务功能，也不改变系统通知栏推送、完整通知中心、发帖、完整评论提交、点赞、投票或编辑治理继续后置的范围判断。

第二十一批后已补 [Flutter Android MVP 第八至第二十批验证索引（2026-05-02）](/guide/flutter-android-mvp-validation-index-2026-05-02)：该索引把第八至第二十批的 Dart 定向测试、`smoke_test`、`flutter analyze`、Android JVM 单测、文档型验证与 release 前缺口统一收口，便于正式 release 包发布前生成批次级回归留痕。

## 个人开发阶段口径

- 近期默认不部署 testing Gateway，不组织外部分发对象，也不要求每个产品小闭环后安装 APK 做真机验收
- 本机自动化验证、Flutter analyzer、Android JVM 单测与必要的本地构建预检继续保留为开发阶段主要保障
- Android Studio 模拟器 / AVD 功能测试可在开发中继续按需执行，用于验证页面、导航、登录回跳或 Gateway 接线；但它不替代正式 release 包发布前的真机 APK 安装验收
- 使用既有正式域名做临时 smoke 时，需明确服务端版本、验证范围与未覆盖项；若服务端版本旧于当前 Flutter MVP，不把新链路失败直接判为客户端回归
- 真机 APK 安装、testing Gateway release 构建、测试账号 / 测试数据、分发对象、反馈闭环与批次级外部分发回归统一在正式 release 包发布前补齐
- 这条分发线暂缓不构成 Flutter 产品功能阻塞，也不应被解释为需要提前扩系统通知栏推送、完整通知中心、发帖、完整评论提交、点赞、投票或编辑治理

## 内测 RC 前置判断

截至 `2026-05-01`，Android MVP 内部 / 小范围 RC 候选判断如下；个人开发阶段暂不推进真实外部分发验收。

### 已具备

- Android MVP 主链路已有连续批次验证记录，当前可作为内测候选继续准备
- Android release 包身份、应用显示名、`INTERNET` 权限、Gateway 环境切换、release signing 读取逻辑与签名配置诊断入口已落地
- 当前客户端仍复用现有 API / Auth / Gateway 同源语义，不需要为 RC 分发临时新增 Flutter 专属 BFF
- 当前非阻断功能边界已经明确，不把系统通知栏推送、完整通知中心、发帖、完整评论提交、点赞、投票或编辑治理拉入内测前置范围

### 正式 release 包发布前必须补齐

- 真实签名材料，并通过 `.\gradlew.bat :app:checkReleaseSigningConfig`
- 稳定可访问的测试 Gateway，并在 release 构建时通过 `RADISH_GATEWAY_BASE_URL` 显式指定
- 测试账号、测试数据、测试设备、分发对象、APK 交付方式与反馈回收方式
- 一份批次级回归留痕，记录自动化结果、签名检查、release APK 构建参数、Gateway 基址、真机结论、已知非阻断项与未执行项

### 暂缓时的处理

- 个人开发阶段可暂缓 testing Gateway、分发对象、反馈闭环与真机安装，不判断为当前业务功能阻塞
- 暂缓真机安装不等于暂停模拟器功能测试；Android Studio 模拟器 / AVD 可继续作为开发中人工验证入口，验证结果需按普通开发验证记录，不写作 release 真机验收结论
- 若缺少真实签名材料，不判断为可外部分发；可继续做本地 debug-signing release 构建验证，但必须标注不能作为外部分发包
- 若缺少测试 Gateway，不使用本机开发地址伪装测试环境；正式 release 包发布前再补测试环境或只记录本机联调结果
- 若缺少分发对象与反馈闭环，不进入外部投放；正式 release 包发布前再明确测试范围与回收方式
- 以上暂缓均不应回头扩张 Flutter 产品范围

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

正式 release 包发布前的外部分发真正依赖以下条件；个人开发阶段可暂缓，不应被误判为业务功能阻塞：

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

在正式 release 包发布前，至少确认：

1. `flutter analyze` 通过
2. `flutter test` 通过
3. `.\gradlew.bat :app:testDebugUnitTest` 通过，且使用 Android Studio JBR
4. `.\gradlew.bat :app:checkReleaseSigningConfig` 通过
5. `flutter build apk --release --dart-define=RADISH_ENVIRONMENT=testing --dart-define=RADISH_GATEWAY_BASE_URL=<测试 Gateway>` 可产出 APK
6. APK 安装后可完成登录、退出、会话恢复、`discover / docs / profile` 基础读取、forum feed / detail / 评论阅读，以及最小 forum notification 回流

当前仍不把系统通知栏推送、完整通知中心、商店发布、AAB 上传或生产发布留痕纳入 Android MVP RC 前置阻断项。

## 建议执行顺序

准备一次 Android MVP RC / release 外部分发时，建议按以下顺序执行：

1. 确认测试 Gateway、测试账号、测试数据与外部分发对象已经就绪
2. 准备正式签名材料，并执行 `.\gradlew.bat :app:checkReleaseSigningConfig`
3. 执行 Dart 与 Android 平台自动化验证
4. 使用测试 Gateway 构建 release APK
5. 在真机安装 APK，按 `Clients/radish.flutter/README.md` 的 Android 真机人工验证 checklist 复核
6. 记录 APK 构建参数、Gateway 基址、自动化结果、真机结论与已知非阻断项

个人开发阶段可暂缓执行本节；正式 release 包发布前如果任一前置条件缺失，应保持“RC / release 外部分发等待前置材料”的判断，不回头扩大 Flutter 功能范围。
