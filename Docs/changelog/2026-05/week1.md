# 2026-05 第一周 (05-01 ~ 05-03)

## 2026-05-01 (周五)

### Flutter 第七批 docs 搜索体验增强收口

- **docs 搜索体验增强已完成收口**：搜索词提交会先做 trim 归一化，并同步输入框为实际搜索词；搜索、清除搜索与分页切换会回到结果顶部。
- **搜索结果详情返回上下文已补齐**：从搜索结果滚动到较深位置打开内联详情后，返回会恢复原搜索结果附近的滚动上下文。
- **Android Back 返回分流已修复**：真机复核发现的搜索结果进入详情后 Back 直接退到桌面问题已收口为 docs 内联详情优先消费根层返回。
- **验证与留痕已完成**：本轮已通过 `flutter test test/docs_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze` 与 `git diff --check`，并补 [Flutter Android MVP 第七批首个小闭环变更回归记录](/guide/flutter-android-mvp-regression-record-2026-05-01)。

### Android MVP 内测 RC 前置整理

- **内测 RC 前置判断已完成整理**：Android MVP 当前内部 / 小范围 RC 候选链路继续成立，覆盖登录、退出、会话恢复、四个主 tab 真实读取、forum feed / detail / 评论阅读、轻回应最小闭环、最小 forum notification 回流、profile / docs / discover 复访与返回上下文。
- **签名材料状态已更新**：真实签名材料已由用户本机补齐并通过 `:app:checkReleaseSigningConfig`；真实密钥文件与 `key.properties` 不进入版本库。
- **release 前置继续后置**：testing Gateway、测试账号 / 测试数据、分发对象、反馈闭环、testing release APK 与真机 APK 安装验收统一留到正式 release 包发布前。
- **模拟器验证边界已澄清**：暂缓真机 APK 安装与外部分发验收，不等于暂停开发中的 Android Studio 模拟器 / AVD 功能测试；模拟器验证可继续用于页面、导航、登录回跳或 Gateway 接线，但不替代 release 前真机安装验收。

### Flutter 第八批 profile 最近文档轻量多条列表

- **profile 最近文档已从单条入口扩展为轻量多条列表**：我的 `profile` 当前最多展示 5 条最近公开文档，按 `slug` 去重并保持最近打开优先。
- **Android 本地持久化兼容旧数据**：新增列表存储 `docs_recent_document_targets`，并兼容旧单条 `docs_recent_document_target`；壳层状态条仍只展示最新一条“继续阅读文档”。
- **回到上下文继续复用 docs detail handoff**：从 profile 最近文档打开详情时仍使用 `DocsDetailHandoffSource.profileRecentDocument`，返回后回到 profile。
- **本轮边界继续收紧**：不扩完整浏览历史中心、删除、清空、跨端同步、搜索历史、目录树、编辑、发布、版本历史、后端搜索改造或 Flutter 专属 BFF。

### Flutter 第九批 profile 复访区块体验整理

- **profile 复访区块展示口径已统一**：最近文档、最近阅读、我的轻回应、最近公开帖子与最近公开评论区块统一标题图标、空态和局部提示样式。
- **我的主页空态位置已收口**：没有最近 forum / docs 记录时，轻量空态放在公开内容之后，避免挤压公开帖子与评论回跳入口；公开主页继续不显示个人复访区块。
- **局部错误继续保持区块内降级**：加载更多失败仍只在对应区块提示，不拖垮 profile 公开资料、公开帖子、公开评论或我的轻回应回看。

### Flutter 第十批 profile 区块顺序与信息密度微调

- **我的主页区块顺序已重新整理**：当前顺序收口为最近复访 / 最近文档 / 最近阅读 / 我的轻回应 / 最近公开帖子 / 最近公开评论。
- **公开主页保持只读公开内容优先**：公开主页只展示最近公开帖子与最近公开评论，不展示个人复访区块。
- **复访空态改为紧凑卡片**：无最近 forum / docs 记录时，原先两个完整空态区块收束为一个紧凑“最近复访”空态卡，减少对公开内容入口的挤压。

### Flutter 第十一批 docs 详情只读上下文补强

- **docs 详情页补齐只读上下文**：详情正文顶部新增轻量只读上下文，明确来源、公开地址和“仅阅读，不提供编辑、发布或版本治理入口”的边界。
- **长 slug 窄屏显示已受控**：详情顶部 slug、来源类型 chip 与上下文中的公开地址均做受控宽度与省略显示，避免窄屏撑破布局。
- **详情错误态补目标上下文**：错误态会提示目标 `/docs/:slug`，并明确可返回来源后重试，避免只显示服务错误而丢失用户要打开的文档上下文。

### Flutter 第十二批 forum detail 来源上下文与错误态补强

- **forum detail 补齐只读上下文**：详情正文卡新增来源、公开地址、可选评论定位目标和只读边界说明，继续保持“仅阅读与最小轻回应”的客户端边界。
- **详情错误态补齐目标与来源**：错误态现在会显示目标 `/forum/post/:postId`、来源与可选 `commentId`，并明确返回来源后重试口径。
- **评论定位失败保持局部降级**：定位失败提示会带出目标评论 ID，但不拖垮帖子正文、轻回应和评论阅读。
- **窄屏文本溢出同步收口**：forum detail 的公开地址 chip 与基础 meta 文本已改为受控宽度显示，长帖子 ID、长分类或长时间文本不再撑破布局。
- **验证已完成**：本轮通过 `flutter test test/forum_detail_page_test.dart`、`flutter test test/smoke_test.dart`、`flutter analyze` 与 `git diff --check`，并补 [Flutter Android MVP forum detail 来源上下文与错误态补强记录](/guide/flutter-android-mvp-forum-detail-readonly-context-record-2026-05-01)。

### 当日收口判断

- **第八至第十二批均保持窄范围**：本日新增小闭环都围绕 profile 复访、docs 只读阅读或 forum detail 来源上下文，不新增后端 API、不新增 Flutter 专属 BFF，也不扩完整通知中心或完整社区互动。
- **真机与模拟器边界已统一写明**：真机 APK 安装、testing Gateway release 构建与外部分发验收留到正式 release 包发布前；Android Studio 模拟器 / AVD 仍可作为开发中功能测试入口。
- **下一步仍应沿小闭环推进**：后续若继续功能开发，仍优先选择窄范围复访或只读体验补强，不把暂缓分发验收误判为系统通知栏推送、完整通知中心、发帖、完整评论提交、点赞、投票或编辑治理的扩张信号。
