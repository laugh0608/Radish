# Flutter Android MVP RC 验收清单（2026-05-03）

> 适用范围：`Phase 2-3 Flutter 客户端 MVP` 的 Android MVP 第一轮收口验收。
>
> 本清单用于把当前判断从“持续做小闭环”切到“Android MVP 可交付候选”。执行时配合 [Flutter Android RC 分发前置清单](/guide/flutter-android-rc-distribution)、[Flutter Android MVP 第八至第二十批验证索引（2026-05-02）](/guide/flutter-android-mvp-validation-index-2026-05-02) 与 [当前进行中](/planning/current) 一并使用。

## 1. 当前目标

本轮 RC 验收要回答的不是“还有没有更多局部体验可以继续补”，而是：

1. Android MVP 的高频链路是否已经形成完整产品闭环
2. 当前工程是否具备真实 release 构建、安装与验收条件
3. 是否可以给出一份明确的 `go / no-go` 结论

## 2. MVP 结束条件

### 2.1 产品闭环

以下能力应视为本轮 MVP 完成判断的核心闭环：

1. 登录、退出与会话恢复
2. `discover / forum / docs / profile` 四个主 tab 的真实读取
3. `forum` 列表、详情、评论分页、子评论分页、`commentId` 定位与来源返回
4. `docs` 列表、搜索、详情、正文内链与来源返回
5. `profile` 公开资料、公开帖子 / 评论、最近阅读、最近文档与我的轻回应回看
6. `forum detail` 轻回应最小读写闭环
7. 最小 `forum notification` 回流

### 2.2 工程质量

以下命令级验证应有明确结果，不接受“理论上应该通过”：

1. `flutter analyze`
2. `flutter test`
3. `flutter test test/smoke_test.dart`
4. `.\gradlew.bat :app:testDebugUnitTest`
5. `.\gradlew.bat :app:checkReleaseSigningConfig`
6. `flutter build apk --release --dart-define=RADISH_ENVIRONMENT=testing --dart-define=RADISH_GATEWAY_BASE_URL=<testing-gateway>`
7. `git diff --check`

### 2.3 真实交付形态

以下条件本轮必须真实覆盖：

1. 使用 `testing Gateway`，而不是只停留在本机开发地址
2. 使用 `release APK`，而不是只看 debug 包或模拟器
3. 完成至少一轮真机安装与关键链路复核

### 2.4 阻断标准

以下情况不能宣布本轮 MVP 收口完成：

1. 存在 `P0 / P1` 级功能阻断
2. release 构建、签名检查或真机安装不可稳定复现
3. 高价值链路无法在 testing Gateway 上跑通

### 2.5 本轮明确不纳入阻断

以下能力当前不作为 RC 阻断项：

- 系统通知栏推送
- 完整通知中心与通知管理
- 发帖、完整评论提交、点赞、投票与编辑治理
- 聊天、完整商城工作台、创作器
- 完整浏览历史中心、删除 / 清空与跨端同步治理
- Windows / Linux 平台扩展
- Flutter 专属 BFF

## 3. 验收前记录项

执行本轮验收前，先记录以下上下文：

1. Git 提交点
2. 验收日期与执行人
3. `RADISH_ENVIRONMENT`
4. `RADISH_GATEWAY_BASE_URL`
5. 测试账号 / 测试数据
6. 测试设备型号与 Android 版本
7. APK 产物路径与签名状态

## 4. 命令级验收步骤

建议执行顺序如下：

1. 运行 `flutter analyze`
2. 运行 `flutter test`
3. 运行 `flutter test test/smoke_test.dart`
4. 运行 `.\gradlew.bat :app:testDebugUnitTest`
5. 运行 `.\gradlew.bat :app:checkReleaseSigningConfig`
6. 运行 `flutter build apk --release --dart-define=RADISH_ENVIRONMENT=testing --dart-define=RADISH_GATEWAY_BASE_URL=<testing-gateway>`
7. 运行 `git diff --check`

验收记录至少要写明：

- 命令
- 结果
- 关键输出摘要
- 若失败，失败点与是否阻断

## 5. 真机验收步骤

### 5.1 安装与启动

1. 安装 release APK 成功
2. 首次启动正常
3. 冷启动正常
4. 重启后会话恢复正常

### 5.2 认证链路

1. 登录成功
2. 取消登录时提示正常
3. 退出成功
4. 重新登录成功

### 5.3 主 tab 与只读链路

1. `discover` 可正常读取
2. `forum` 列表、详情、评论阅读可正常读取
3. `docs` 列表、搜索、详情与正文内链可正常读取
4. `profile` 公开资料、公开帖子 / 评论、最近阅读与最近文档可正常读取

### 5.4 交互与回流链路

1. `forum detail` 的 `commentId` 定位正常
2. forum / docs / profile / discover 的来源返回正常
3. 轻回应发布正常
4. 最小 `forum notification` 回流正常

## 6. 问题分级

### 6.1 阻断项

符合以下任一条件，可判为 `no-go`：

1. 无法登录、退出或恢复会话
2. 任一主 tab 无法稳定读取
3. forum / docs 的高价值详情链路失败
4. release 构建、签名或真机安装失败
5. testing Gateway 下出现可稳定复现的 `P0 / P1` 缺陷

### 6.2 非阻断项

以下类型可进入 `known issues`，但需明确记录：

1. 低优先级文案问题
2. 非关键窄屏显示瑕疵
3. 不影响主链路的轻微刷新提示或空态问题
4. 已明确后置的范围项

## 7. 验收输出物

本轮至少产出以下三份结果：

1. 一份 RC 验收记录
2. 一份已知问题清单
3. 一份 `go / no-go` 结论

若为 `no-go`，需要额外写明：

1. 阻断项列表
2. 阻断项所属类别：环境、构建、功能、体验
3. 下一轮只允许做的定点修复范围

## 8. 收口结论模板

### 8.1 Go

- Android MVP 高价值链路已通过 RC 验收
- 当前可将 `Phase 2-3 Android MVP` 标记为“第一轮完成”
- 后续只在下一阶段规划中决定是否继续做 Android 内测产品化深化或 Windows / Linux 扩展

### 8.2 No-Go

- Android MVP 尚未达到第一轮完成标准
- 当前只回到阻断项定点修复
- 未经重新评估，不开启新的 Flutter 微体验批次或额外产品扩项
