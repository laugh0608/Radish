# Flutter Android MVP profile 区块顺序与信息密度微调回归记录

- 记录日期：2026-05-01
- 记录人：laugh0608
- 变更范围：`Phase 2-3 Flutter 客户端 MVP` 下的 profile 区块顺序、复访空态与区块描述文案

### 变更摘要

- 我的主页区块顺序收口为最近复访 / 最近文档 / 最近阅读 / 我的轻回应 / 最近公开帖子 / 最近公开评论
- 公开主页继续只展示最近公开帖子与最近公开评论，不显示个人复访区块
- 无最近 forum / docs 记录时，使用一个紧凑“最近复访”空态卡承载最近文档与最近阅读说明
- 收短最近文档、最近阅读、我的轻回应、最近公开帖子与最近公开评论区块描述文案
- smoke 测试中的 profile 帖子 / 评论回跳点击目标改为真实 `FilledButton`，避免页面高度变化后点击文本中心导致误判

### 影响专题

- Flutter Android MVP
- Flutter profile 复访入口
- Flutter forum / docs 原生回跳上下文

### 自动化执行

- `flutter test test/profile_page_test.dart`：通过
- `flutter test test/smoke_test.dart`：通过
- `flutter analyze`：通过
- `git diff --check`：通过

说明：

- 本轮只改 Flutter profile 展示层与对应 widget / smoke 测试
- 本轮未触达后端接口契约、数据库迁移、Android 原生 MethodChannel、身份协议输出或 Gateway 配置，因此未追加后端 baseline、host baseline、Android JVM 单测或身份专题回归

### 人工验收

- 执行情况：未执行
- 摘要：
  - 个人开发阶段暂缓真机 APK 安装与 testing Gateway 验收
  - 正式 release 包发布前再按 RC 清单补齐真机安装、Gateway 联调与外部分发回归

### 结论

- `profile` 区块顺序与信息密度微调已完成代码与自动化验证，可作为第十批首个窄范围小闭环收口
- 本轮继续保持只读复访边界，不扩展完整浏览历史中心、删除、清空、筛选、跨端同步、系统通知栏推送或完整通知中心

### 风险 / 后置项

- 真机安装与真实 Gateway 联调暂缓到正式 release 包发布前
- 完整浏览历史中心、删除 / 清空、跨端同步治理、系统通知栏推送、完整通知中心、发帖、完整评论提交、点赞、投票与编辑治理继续后置
