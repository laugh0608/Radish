# Flutter Android MVP 刷新体验开发阶段验证清单（2026-05-02）

> 适用范围：`Phase 2-3 Flutter 客户端 MVP` 第十五批至第十八批的主 tab 刷新体验收口。
>
> 本记录用于开发阶段复核 `discover / forum / docs / profile` 的刷新态、局部失败提示与旧内容保留语义，不等同于正式 release 包发布前的真机 APK 安装验收。

## 背景

第十五批至第十七批已经分别完成：

- `discover` 刷新体验与局部 issue 清理复核
- `forum / docs` 主列表刷新体验一致性
- `profile` 主资料刷新体验一致性

第十八批不继续扩大产品功能，只把上述三批的开发阶段人工检查口径收口到 `Clients/radish.flutter/README.md`，避免后续模拟器验证遗漏刷新体验。

## 当前已完成的自动化验证

截至本记录整理时，刷新体验相关自动化验证已覆盖：

- `flutter test test/discover_page_test.dart`
- `flutter test test/forum_page_test.dart test/docs_page_test.dart`
- `flutter test test/profile_page_test.dart`
- `flutter test test/smoke_test.dart`
- `flutter analyze`
- `git diff --check`

这些验证已经覆盖已有内容刷新中保留旧内容、刷新失败保留旧内容并展示局部提示、刷新成功后清理旧提示等核心语义。

## 开发阶段模拟器检查

模拟器验证前置：

- 后端宿主、Gateway 与 Auth 由人工启动
- Android 模拟器访问本机 Gateway 前先执行 `adb reverse tcp:5000 tcp:5000`
- Flutter 客户端仍通过 `https://localhost:5000` 访问本机 Gateway

建议按以下顺序检查：

1. 进入 `discover`，等待摘要加载成功后点击“刷新发现”；刷新中应继续展示上次可用摘要，并出现轻量刷新态。
2. 在 `discover` 已有摘要后临时断开 Gateway 或网络，再点击“刷新发现”；页面应显示“刷新发现失败”，旧摘要仍可读。
3. 进入 `forum`，等待帖子列表加载成功后点击刷新；刷新中应保留旧帖子列表。
4. 在 `forum` 已有帖子列表后临时断开 Gateway 或网络，再点击刷新；页面应显示“刷新论坛失败”，旧帖子列表仍可读。
5. 进入 `docs`，确认普通列表刷新保留旧文档；在搜索结果列表中刷新时，也不应破坏搜索 / 分页上下文。
6. 在 `docs` 已有列表后临时断开 Gateway 或网络，再点击刷新；页面应显示“刷新文档失败”，旧文档列表仍可读。
7. 进入 `profile`，等待公开资料、公开帖子 / 评论与个人复访区块加载成功后点击“刷新资料”；刷新中应继续展示已有内容。
8. 在 `profile` 已有资料后临时断开 Gateway 或网络，再点击“刷新资料”；页面应显示“刷新资料失败”，旧资料、公开帖子、公开评论、我的轻回应与复访区块仍可读。
9. 恢复 Gateway 或网络后再次刷新对应页面；刷新成功后旧的刷新失败提示应消失。

## 范围边界

本轮不新增：

- 后端 API
- Flutter 专属 BFF
- 下拉刷新体系
- 独立重试队列
- 完整浏览历史中心
- 资料编辑、关注、发帖、完整评论提交、点赞、投票或治理能力

本轮不执行：

- `dotnet run` / `npm run dev`
- testing Gateway release APK 构建
- 真机 APK 安装
- 外部分发验收

上述 release 前动作继续按 [Flutter Android RC 分发前置清单](/guide/flutter-android-rc-distribution) 在正式 release 包发布前补齐。

## 后续建议

刷新体验已经完成一次主 tab 范围内的横向收口。下一步若继续推进 Flutter MVP，不建议再围绕同一刷新语义重复扩展；更合适的方向是选择一个新的窄范围只读体验补强点，或在准备 release 前按 RC 清单补齐真机安装与分发级回归留痕。
