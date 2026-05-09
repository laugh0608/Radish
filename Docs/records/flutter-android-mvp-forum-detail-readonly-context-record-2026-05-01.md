# Flutter Android MVP forum detail 来源上下文与错误态补强记录

- 记录日期：2026-05-01
- 记录人：laugh0608
- 变更范围：`Phase 2-3 Flutter 客户端 MVP` 下的 forum detail 只读上下文、详情错误态、评论定位失败提示与窄屏文本显示

### 变更摘要

- forum detail 正文卡新增轻量只读上下文，明确来源、公开地址、可选评论定位目标与只读边界
- forum detail 详情错误态补齐来源、目标 `/forum/post/:postId`、可选 `commentId` 与返回来源后重试口径
- 评论定位失败提示会带出目标评论 ID，并继续只作为局部提示，不拖垮帖子正文、轻回应与评论阅读
- forum detail 的公开地址 chip 与基础 meta 文本改为窄屏受控显示，避免长帖子 ID、长分类或长时间文本撑破布局

### 影响专题

- Flutter Android MVP
- Flutter forum 公开只读帖子详情
- Flutter forum notification / profile / discover / 最近阅读等 handoff 来源回看

### 自动化执行

- `flutter test test/forum_detail_page_test.dart`：通过
- `flutter test test/smoke_test.dart`：通过
- `flutter analyze`：通过
- `git diff --check`：通过

说明：

- 本轮只触达 Flutter forum detail 展示层与 widget / smoke 测试
- 本轮未触达后端接口契约、数据库迁移、Android 原生 MethodChannel、身份协议输出、Gateway 配置、签名配置或 release 构建脚本
- 本地执行 Flutter / Dart 命令时使用提权环境，以绕过当前 Windows 沙盒对 Flutter / Dart 子进程的限制；该问题归为当前执行环境边界，不属于业务代码回归

### 人工验收

- 执行情况：未执行
- 摘要：
  - 用户已明确个人开发阶段暂缓真机 APK 安装与 testing Gateway 验收
  - 该暂缓不限制开发中继续使用 Android Studio 模拟器 / AVD 做功能测试；模拟器验证与 release 前真机 APK 安装验收分开记录、分开判断
  - 本轮以 Flutter widget / smoke 测试覆盖 forum detail 来源上下文、详情错误态、评论定位失败局部提示、窄屏受控显示与壳层 handoff 回归

### 部署复核

- 执行情况：未执行
- 摘要：
  - 本轮未进入 testing Gateway release APK 构建、真机安装、外部分发或生产部署

### 结论

- forum detail 来源上下文与错误态补强已完成代码与自动化验证，可作为第十二批首个窄范围小闭环收口
- 本轮继续保持公开只读与最小轻回应边界，不新增后端 API、Flutter 专属 BFF、完整通知中心、系统通知栏推送、发帖、完整评论提交、点赞、投票或编辑治理
- Android MVP RC 分发线在个人开发阶段继续暂缓：testing Gateway、测试对象、反馈闭环与真机安装复核统一留到正式 release 包发布前
- 后续 Flutter MVP 功能开发仍可按需补 Android Studio 模拟器 / AVD 人工验证；只有正式 release 包发布前才要求补真机 APK 安装与分发级验收

### 风险 / 后置项

- 真机安装与真实 Gateway 联调本轮按用户要求跳过，正式 release 包发布前再补齐
- 完整通知中心、系统通知栏推送、完整浏览历史中心、发帖、完整评论提交、点赞、投票、编辑治理、iOS 移动端评估、Tauri + WebOS 桌面安装包评估与商店发布继续后置
