# Flutter Android MVP profile 最近文档轻量多条列表变更回归记录

- 记录日期：2026-05-01
- 记录人：laugh0608
- 变更范围：`Phase 2-3 Flutter 客户端 MVP` 下的 profile 最近文档轻量多条列表、docs 最近文档本地持久化兼容与 Android 本地存储桥

### 变更摘要

- 我的 `profile` 中最近文档从单条入口扩展为轻量多条列表，最多保留 5 条
- 最近文档按 `slug` 去重，重复打开同一文档时会前置并刷新标题上下文
- Android 本地持久化兼容旧单条 `docs_recent_document_target`，新增列表存储 `docs_recent_document_targets`
- 壳层状态条仍只展示最新一条“继续阅读文档”，profile 内展示多条最近文档
- 从 profile 最近文档打开详情时继续使用 `DocsDetailHandoffSource.profileRecentDocument`，详情返回后仍回到 profile

### 影响专题

- Flutter Android MVP
- Flutter docs 公开只读阅读与最近文档复访
- Flutter profile 复访入口
- Android 本地 MethodChannel 存储桥

### 自动化执行

- `flutter test test/profile_page_test.dart test/smoke_test.dart`：通过
- `flutter test test/docs_page_test.dart`：通过
- `flutter test`：通过
- `flutter analyze`：通过
- `.\gradlew.bat :app:testDebugUnitTest`：通过，使用 Android Studio JBR
- `npm run check:identity-impact:staged`：通过，命中 `Docs/guide/regression-index.md` 与 `Docs/planning/current.md` 的默认执行面文档 / 门禁资产类别
- `npm run check:repo-hygiene:staged`：通过，已检查 10 个 staged 变更文件
- `git diff --check`：通过

说明：

- 本轮触达 Android `MainActivity` 的 docs follow-up MethodChannel 存储桥，因此补充 Android 平台 JVM 单测
- 本轮未触达后端接口契约、数据库迁移、身份协议输出或 Gateway 配置；身份影响门禁仅命中文档默认执行面，因此未追加后端 baseline、host baseline 或身份专题回归

### 后续收口复核

- 复核日期：2026-05-01
- `flutter test test/profile_page_test.dart test/smoke_test.dart`：通过
- `flutter test test/docs_page_test.dart`：通过
- `flutter analyze`：通过
- `.\gradlew.bat :app:testDebugUnitTest`：通过，使用 Android Studio JBR

说明：

- 本次复核只确认第八批小闭环的代码与自动化状态仍成立，不追加客户端功能
- 本次仍不执行 testing Gateway release APK 构建、真机安装或外部分发验收；这些动作暂缓到正式 release 包发布前

### 人工验收

- 执行情况：未执行
- 摘要：
  - 用户明确要求暂时跳过真机安装环节
  - 本轮以内存 store、Flutter widget / smoke 测试与 Android JVM 单测覆盖最近文档多条列表、去重、profile 打开详情与返回上下文

### 部署复核

- 执行情况：未执行
- 摘要：
  - 本轮未进入 testing Gateway release APK 构建、真机安装、外部分发或生产部署

### 结论

- `profile` 最近文档轻量多条列表已完成代码与自动化验证，可作为第八批首个窄范围小闭环收口
- 本轮继续保持公开只读边界，不扩展完整浏览历史中心、删除、清空、跨端同步、搜索历史、目录树、编辑、发布、版本历史或后端搜索改造
- Android MVP RC 分发线在个人开发阶段暂缓：testing Gateway、测试对象、反馈闭环与真机安装复核统一留到正式 release 包发布前

### 风险 / 后置项

- 真机安装与真实 Gateway 联调本轮按用户要求跳过，正式 release 包发布前再补齐
- 系统通知栏推送、完整通知中心、完整浏览历史中心、发帖、完整评论提交、点赞、投票、编辑治理、Windows / Linux 平台工程与商店发布继续后置
