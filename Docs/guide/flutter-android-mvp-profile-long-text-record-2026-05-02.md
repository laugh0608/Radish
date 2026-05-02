# Flutter Android MVP profile 公开主页长文本窄屏显示复核记录

- 记录日期：2026-05-02
- 记录人：laugh0608
- 变更范围：`Phase 2-3 Flutter 客户端 MVP` 第二十批 `profile` 公开主页长文本窄屏显示复核
- 记录性质：命令级回归记录补洞，不新增 Flutter / Android 业务功能

## 变更摘要

- 公开资料标题、用户名、用户 ID 已补受控显示
- 最近文档 slug、最近阅读 `postId + commentId` 已补受控显示
- 最近公开帖子标题 / 摘要 / 分类与最近公开评论快照已补受控显示
- 新增窄屏 widget 测试，构造长 `displayName / userName / userId / slug / postId / commentId / title / category / comment snapshot`，确认 profile 页面在窄屏仍可渲染并保持核心区块可访问
- 本轮只处理文本显示约束，不改变 profile 数据读取、回跳来源、分页、刷新、空态人称或加载更多局部失败逻辑

## 自动化执行

- `flutter test test/profile_page_test.dart test/smoke_test.dart`：通过
- `flutter analyze`：通过
- `git diff --check`：通过

说明：

- Flutter 命令在当前 Windows 沙盒中准备阶段失败，已按项目沙盒规则提权执行；该问题归为执行环境边界，不属于业务代码回归
- 本轮只补第二十批命令级记录，不新增 Dart 业务代码，不改变 API、状态模型、签名配置或 Gateway 切换逻辑

## 人工验收

- 执行情况：未执行
- 摘要：
  - 个人开发阶段暂缓 testing Gateway、release APK 构建、真机 APK 安装与外部分发验收
  - `https://radishx.com` 正式域名临时 smoke 已确认帖子、文档和用户公开信息基础只读读取未见异常，但该 smoke 不替代当前批次长文本窄屏 release APK 真机验收

## 结论

第二十批 `profile` 公开主页长文本窄屏显示复核已补齐独立命令级回归记录。当前验证覆盖 profile 定向测试、壳层 smoke、Flutter analyzer 与 diff 格式检查。

正式 release 包发布前仍需按 [Flutter Android RC 分发前置清单](/guide/flutter-android-rc-distribution) 补齐 testing Gateway、release APK、真机安装、登录 / 通知 / 写入链路与批次级外部分发回归。
