# Flutter Android MVP 内测分发前置整理记录

- 记录日期：2026-05-01
- 记录人：laugh0608
- 整理范围：`Phase 2-3 Flutter 客户端 MVP` 在第七批首个小闭环收口后的 Android 内部 / 小范围 RC 分发前置判断

### 整理摘要

- 本轮不新增 Flutter / Android 业务功能，不改动客户端代码
- 以内测 RC 前置为目标，整理当前已具备条件、仍缺材料、最小验收入口与非阻断项
- Android MVP 当前仍可判断为内部 / 小范围 RC 候选链路成立
- 外部分发仍等待真实签名材料、测试 Gateway、测试账号 / 测试数据、分发对象与反馈回收方式
- 缺少上述外部分发材料时，应保持“等待前置材料”的判断，不回头扩张系统通知栏推送、完整通知中心、发帖、完整评论提交、点赞、投票或编辑治理

### 当前已具备条件

1. Android MVP 主链路已有连续收口记录，覆盖登录、退出、会话恢复、四个主 tab 真实读取、forum feed / detail / 评论阅读、轻回应最小闭环、最小 forum notification 回流、profile / docs / discover 复访与返回上下文
2. Flutter 客户端已具备 Gateway 环境切换能力，可通过 `--dart-define=RADISH_ENVIRONMENT=...` 与 `--dart-define=RADISH_GATEWAY_BASE_URL=...` 指定测试环境
3. Android release 包身份、应用显示名、`INTERNET` 权限、release signing 读取逻辑与签名配置诊断入口已经落地
4. `:app:checkReleaseSigningConfig` 已作为真实签名材料前置检查入口，缺字段、示例值或 keystore 文件缺失时会快速失败
5. [Flutter Android RC 分发前置清单](/guide/flutter-android-rc-distribution) 已明确内测 RC 与外部分发阻塞条件的边界

### 分发前必须补齐

1. **真实签名材料**
   - 在 `Clients/radish.flutter/android/` 准备本机或安全密钥位置下的 `key.properties`
   - 准备真实 keystore，并确认 `.jks` / `.keystore` 不进入版本库
   - 执行 `.\gradlew.bat :app:checkReleaseSigningConfig` 并记录结果
2. **测试 Gateway**
   - 准备稳定可访问的 HTTPS Gateway
   - 通过 `RADISH_GATEWAY_BASE_URL` 显式指定，不默认使用本机开发地址
   - 测试环境证书信任链应由环境解决，不依赖本机开发证书放行开关
3. **测试对象与反馈闭环**
   - 明确测试账号、测试数据、测试设备、分发对象、APK 交付方式与反馈回收方式
   - 当前不默认进入商店发布、AAB 上传或生产发布
4. **批次级回归留痕**
   - 记录自动化验证、签名检查、release APK 构建命令、Gateway 基址、真机复核结果、已知非阻断项与未执行项

### 建议最小验证入口

准备实际内测 RC APK 前，建议至少执行：

```powershell
cd Clients/radish.flutter
flutter analyze
flutter test
```

```powershell
cd Clients/radish.flutter/android
$env:JAVA_HOME='D:\Program Files\JetBrains\Android Studio\jbr'
.\gradlew.bat :app:testDebugUnitTest
.\gradlew.bat :app:checkReleaseSigningConfig
```

```powershell
cd Clients/radish.flutter
flutter build apk --release --dart-define=RADISH_ENVIRONMENT=testing --dart-define=RADISH_GATEWAY_BASE_URL=<测试 Gateway>
```

APK 安装后，真机复核至少覆盖：

1. 启动、登录、退出、会话恢复
2. `discover / forum / docs / profile` 基础读取
3. forum feed、forum detail、评论阅读、评论分页与评论定位
4. forum detail 原地登录续接与轻回应发布
5. 最小 forum notification 回流
6. profile 最近 forum / docs 复访
7. docs 搜索、搜索结果详情返回与 Android Back 返回分流
8. discover 文档直达、论坛精选直达与快捷入口返回上下文

### 当前非阻断项

- 系统通知栏推送
- 完整通知中心、通知管理、标记已读与删除
- 发帖、完整评论提交、点赞、投票、编辑治理
- 聊天、完整商城工作台、创作器
- 完整浏览历史中心、删除 / 清空与跨端同步治理
- Windows / Linux 平台目录与桌面端 Flutter 分发
- 商店发布、AAB 上传、生产发布留痕

### 本轮执行情况

- 自动化执行：未执行
- release APK 构建：未执行
- 签名配置检查：未执行
- Android 真机复核：未执行
- 原因：本轮仅做第七批之后的内测分发前置文档整理，且真实签名材料、测试 Gateway 与分发对象尚未在本轮提供

### 结论

- Android MVP 当前可以继续按“内部 / 小范围 RC 候选”推进前置材料准备
- 下一步若准备实际分发，应先补齐真实签名材料、测试 Gateway、测试对象与反馈闭环，再按 [Flutter Android RC 分发前置清单](/guide/flutter-android-rc-distribution) 执行签名检查、自动化验证、release APK 构建与真机验收
- 若前置材料仍缺失，应保持等待状态，不把等待分发材料误判为 Flutter 产品功能阻塞
