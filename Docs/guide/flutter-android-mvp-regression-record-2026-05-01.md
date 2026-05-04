# Flutter Android MVP 第七批首个小闭环变更回归记录

- 记录日期：2026-05-01
- 记录人：laugh0608
- 变更范围：`Phase 2-3 Flutter 客户端 MVP` 下的 docs 搜索体验增强、Android Back 返回分流修复与 Android MVP 当前批次收口留痕

### 变更摘要

- docs 搜索提交时会先做本地 trim 归一化，并把输入框同步为实际搜索词
- 搜索、清除搜索与分页切换会回到结果顶部，避免沿用旧滚动位置
- 从搜索结果滚动到较深位置打开内联文档详情后，返回会恢复原搜索结果附近的滚动上下文
- 修复真机复核发现的 docs 搜索结果进入详情后 Android Back 直接退到桌面的问题；当前文档 tab 处于内联详情态时，根层 Back 会优先回到搜索列表
- 同步 `Phase 2-3 Flutter 客户端 MVP`、当前进行中与路线图口径，明确第七批首个小闭环已完成代码、自动化验证与 Android 真机复核

### 影响专题

- Flutter Android MVP
- Flutter docs 公开只读阅读与关键词搜索复访
- Flutter 壳层 Android Back 返回分流
- Android MVP 当前可测链路与 RC 候选判断

### 身份语义影响面

- 命中情况：命中默认执行面文档与门禁资产
- 命中原因：本轮收口留痕更新了 `Docs/guide/regression-index.md`，属于 `check:identity-impact` 关注的默认执行面文档范围
- 处理结论：本轮未触达身份运行时代码、Auth 协议输出、Token 解析或官方协议消费者；身份专题回归不作为本批阻断项

### 自动化执行

- `flutter test test/docs_page_test.dart`：通过
- `flutter test test/smoke_test.dart`：通过
- `flutter analyze`：通过
- `git diff --check`：通过
- `npm run check:identity-impact:staged`：通过，命中 `Docs/guide/regression-index.md` 的默认执行面文档 / 门禁资产类别
- `npm run check:repo-hygiene:staged`：通过，已检查 4 个 staged 变更文件
- `npm run validate:baseline:quick`：未执行
- `npm run validate:baseline`：未执行
- `npm run validate:baseline:host`：未执行
- `npm run validate:ci`：未执行

说明：

- 本轮代码改动集中在 `Clients/radish.flutter` 的 docs 页面、壳层返回分流与定向测试，默认以 Flutter Dart 层测试、壳层 smoke 与静态分析作为开发中必要验证入口
- 本轮未触达后端接口契约、宿主配置、数据库迁移、身份协议输出或 Android 签名 / release 构建脚本，因此未追加后端 baseline、host baseline、Android 平台 JVM 单测或 release APK 构建

### 专题回归

- `Clients/radish.flutter/test/docs_page_test.dart`
  - 覆盖 docs 列表、搜索、内联详情返回、搜索结果滚动上下文恢复、长 slug 窄屏、文档内链与错误态
- `Clients/radish.flutter/test/smoke_test.dart`
  - 覆盖真实 `RadishApp` 壳层下 docs tab 搜索进入详情后的 Android Back 返回搜索列表，不触发退到桌面
- [Flutter Android RC 分发前置清单](/guide/flutter-android-rc-distribution)
  - 本轮只更新当前 RC 候选判断，不进入外部分发动作

### 人工验收

- 执行情况：已执行
- 摘要：
  - 在 Android 真机进入 `文档` tab
  - 执行关键词搜索，确认搜索结果正常出现
  - 从搜索结果打开文档详情后返回，确认回到搜索列表而不是退到桌面
  - 复核修复后的 Android Back 路径，确认第七批 docs 搜索体验增强可收口

### 部署复核

- 执行情况：未执行
- 记录入口：无
- 摘要：
  - 本轮未涉及测试部署、生产部署、宿主运行态、反代、证书或 Gateway 环境切换

### 故障归类 / 环境边界

- 归类：无阻断
- 说明：
  - 真机复核发现的 Android Back 退桌面问题已完成代码修复、自动化覆盖与真机复测
  - 本轮未出现默认执行面失败、身份语义专题失败或 contract 漂移
  - 本地执行 Flutter 命令时使用了提权环境，以绕过当前 Windows 沙盒对 Flutter / Dart 子进程与 Git 索引写入的限制；该问题归为当前执行环境边界，不属于业务代码回归

### 结论

- 第七批首个小闭环“Flutter docs 搜索体验增强”已完成收口，可转入稳定维护
- Android MVP 当前内部 / 小范围 RC 候选链路继续成立：已验证面覆盖登录、退出、会话恢复、四个主 tab 真实读取、forum feed / detail / 评论阅读、轻回应最小闭环、最小 forum notification 回流、profile / docs / discover 复访与返回上下文
- 外部分发仍等待真实签名材料、测试 Gateway、外部分发对象与正式分发批次记录；这些前置缺失不构成当前业务功能阻塞

### 风险 / 后置项

- 未执行完整 `flutter test`、Android 平台 JVM 单测、release APK 构建或签名配置检查；准备外部分发前仍需按 [Flutter Android RC 分发前置清单](/guide/flutter-android-rc-distribution) 补齐
- 系统通知栏推送、完整通知中心、发帖、完整评论提交、点赞、投票、编辑治理、完整浏览历史中心、iOS 移动端评估、Tauri + WebOS 桌面安装包评估与商店发布继续后置
- 下一批若继续做功能，建议仍保持窄范围；若准备内测分发，应优先补签名材料、测试 Gateway 与 release APK 级回归留痕
