# Flutter Android MVP RC 补验评估记录

- 记录日期：2026-05-02
- 记录人：laugh0608
- 整理范围：`Phase 2-3 Flutter 客户端 MVP` 第二十批收口后的 Android RC / release 前补验评估
- 记录性质：文档口径收口，不新增 Flutter / Android 业务功能

## 整理摘要

本轮在第二十批 `profile` 公开主页长文本窄屏显示复核之后，对 Android MVP release 前置材料做一次只读补验评估。

结论保持不变：当前 Android MVP 仍可作为内部 / 小范围 RC 候选继续准备；个人开发阶段不把 testing Gateway、release APK 真机安装、外部分发对象或反馈闭环作为继续小批次开发的阻塞项。但正式 release 包发布前，仍必须按 RC 清单补齐 testing Gateway、release APK、真机安装和批次级回归留痕。

本轮新增的 `https://radishx.com` 正式域名临时 smoke 结论，可补强正式 HTTPS 域名兼容性判断，但不替代完整 RC / release 验收。

## 当前已具备

1. Android MVP 主链路已有连续批次验证记录，覆盖登录、退出、会话恢复、`discover / forum / docs / profile` 真实读取、forum feed / detail / 评论阅读、轻回应最小闭环、最小 forum notification 回流、profile / docs / discover 复访与返回上下文
2. Flutter 客户端已支持通过 `RADISH_ENVIRONMENT` 与 `RADISH_GATEWAY_BASE_URL` 指定本机 / 测试 / 正式 Gateway，API / Auth / Gateway 继续保持同源
3. Android release 包身份、应用显示名、`INTERNET` 权限、release signing 读取逻辑与 `:app:checkReleaseSigningConfig` 诊断入口已落地
4. 真实签名材料此前已由用户本机补齐并通过 `:app:checkReleaseSigningConfig`，真实 `key.properties`、`.jks` 与 `.keystore` 不进入版本库
5. 第八批至第二十批均保持窄范围复访、只读体验、刷新体验或文本显示补强，没有引入新的后端 API、Flutter 专属 BFF 或重交互治理能力
6. `https://radishx.com`（服务端版本 `v26.3.2-release`）已完成 Android Studio 虚拟机与 Android 真机临时 smoke，帖子、文档和用户公开信息基础只读读取未见异常

## 可在开发阶段先补

以下事项不依赖正式 testing Gateway 或外部分发对象，可在后续开发中按需继续补：

1. 对第八批至第二十批做批次级验证索引整理，明确各批已执行的 Dart 定向测试、smoke、analyze、Android JVM 单测或文档验证状态；当前索引见 [Flutter Android MVP 第八至第二十批验证索引（2026-05-02）](/guide/flutter-android-mvp-validation-index-2026-05-02)
2. 在本机或允许的提权验证环境中补跑 `flutter analyze`、`flutter test`、必要的定向测试和 Android Studio JBR 下的 `.\gradlew.bat :app:testDebugUnitTest`
3. 在 Android Studio 模拟器 / AVD 中继续做开发阶段功能检查，尤其是刷新失败局部提示、返回上下文、长文本窄屏显示和公开只读读取
4. 继续使用 `https://radishx.com` 做公开只读临时 smoke，但记录时必须标明服务端版本、验证范围与未覆盖项
5. 准备 release 前验收所需的测试账号、测试数据、测试帖子 / 评论 / 文档、测试设备与反馈表结构

这些动作只能形成开发阶段或预检结论，不能写作正式 release 前真机 APK 安装验收。

## 必须等 release 前补齐

以下事项必须等 testing Gateway、release APK 与真实测试设备条件就绪后再执行：

1. 使用稳定 testing Gateway 构建 release APK，并通过 `RADISH_GATEWAY_BASE_URL=<测试 Gateway>` 显式指定目标环境
2. 在真机安装 release APK，确认 APK 签名、安装、升级、包身份、应用名、权限和 `radish://oidc/callback` 回跳真实可用
3. 复核登录、退出、会话恢复、OIDC 浏览器往返、应用重启恢复和 Android Back 分流
4. 复核 `discover / forum / docs / profile` 四个主 tab 的基础读取、刷新中旧内容保留、刷新失败局部提示与刷新成功清理旧提示
5. 复核 forum feed / detail、评论阅读、评论分页、子评论展开、评论定位、detail 原地登录续接、轻回应发布和最小 forum notification 回流
6. 复核 profile 最近 forum / docs 复访、我的轻回应、最近公开帖子、最近公开评论、公开主页长文本窄屏显示和来源返回上下文
7. 记录自动化结果、签名检查、release APK 构建命令、Gateway 基址、真机结论、已知非阻断项与未执行项

## 暂不纳入 RC 阻断

以下能力仍不作为当前 Android MVP RC / release 前置阻断项：

1. 系统通知栏推送
2. 完整通知中心、通知管理、标记已读与删除
3. 发帖、完整评论提交、点赞、投票、编辑治理
4. 聊天、完整商城工作台、创作器
5. 完整浏览历史中心、删除 / 清空与跨端同步治理
6. iOS 移动端评估与 Tauri + WebOS 桌面安装包评估
7. 商店发布、AAB 上传、生产发布留痕

若上述能力要进入建设，需先回到 `Docs/planning/current.md` 重新定义批次，不应借 RC 补验阶段临时扩张范围。

## 结论

第二十一批建议收口为“RC 补验评估”：只整理 release 前材料、验证入口、临时正式域名 smoke 与未覆盖项，不新增业务功能。

当前下一步仍有两条可选路径：

1. 若继续开发，优先选择窄范围复访或只读体验补强，继续避免扩展重交互治理
2. 若准备 release / RC 外部分发，先补齐 testing Gateway、测试账号 / 数据、release APK 构建、真机安装验收与批次级回归记录

在上述前置材料未齐时，保持“内部 / 小范围 RC 候选链路成立，外部分发验收等待 release 前置材料”的判断。
