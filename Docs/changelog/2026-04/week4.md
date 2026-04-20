# 2026-04 第四周 (04-20 ~ 04-27)

## 2026-04-20 (周一)

### `Phase 2-3` 公开只读页面第二批接线继续推进

- **Flutter `discover / docs / profile` 当前已完成最小真实页面接线**：原生壳层不再只停留在占位说明，`discover` 已接通真实摘要分发入口，`docs` 已接通公开文档列表读取，`profile` 已接通公开资料读取，第二批真实业务接线范围继续保持在高价值只读边界内。
- **Flutter 业务接线当前继续刻意保持“先收口，再扩张”**：forum、discover、docs、profile 目前都只承载公开阅读，不同时进入详情树、编辑、创作器、商城工作台或完整桌面语义，避免在 Android 起步阶段把 Flutter 拉成“移动版 WebOS”。

### Android 平台目录与 Gateway 联调入口收口

- **Flutter Android 平台目录当前已正式生成并纳入仓库**：Android debug APK 构建已通过，`Clients/radish.flutter` 不再停留在“只有 Dart 壳层，没有 Android 工程”的状态。
- **Android 模拟器联调入口当前已统一对齐到 Gateway `https://localhost:5000`**：Flutter 开发态不再默认直连 `5100 / 5200`；API、Auth 与 Gateway 基址当前都统一收口到 Gateway 对外入口。
- **Android 模拟器联调排障口径当前已明确写入文档**：启动应用前需要先执行 `adb reverse tcp:5000 tcp:5000`，以保持模拟器侧 `localhost:5000` 映射到宿主机 Gateway，并避免 `10.0.2.2` 与本地开发 HTTPS 证书主机名不一致导致 TLS 握手失败。

### 最小会话恢复边界从内存占位推进到真实持久化

- **Flutter 会话恢复链路当前已完成第一轮真实闭环**：原生壳层已补齐 Android 本地会话持久化、应用启动恢复、Access Token 过期判断，以及 refresh token 刷新失败后的匿名回落，会话恢复不再只是 `InMemorySessionStore` 占位。
- **当前仍刻意不进入完整登录 UI 与浏览器 OIDC 回调全链路**：本轮只补“已有会话如何恢复”的真实边界，显式登录入口、完整 OIDC 浏览器跳转、登出治理与更多账号动作继续留待下一轮推进，避免同时拉入 `intent-filter`、外部浏览器回调与更多原生集成风险。

### 文档、验证与下一顺位

- **规划与说明文档当前已完成一轮同步**：Flutter README、Gateway 指南、`planning/current`、`phase-two-community-multiplatform` 与 `phase-two-flutter-client-mvp` 当前都已回写 Android 联调与会话恢复的最新事实。
- **本轮最小验证已通过**：`flutter analyze`、`flutter test` 与 `flutter build apk --debug` 当前均已通过；测试侧已覆盖“无会话 / 有效会话 / 过期刷新成功 / 过期刷新失败”这四条主路径。
- **下一步主线当前已进一步收束**：在“会话能存、能恢复、能刷新”之后，后续更适合优先补最小登录发起与回写闭环，让 Flutter 会话边界从“能恢复已有会话”推进到“能真实获得会话”。
