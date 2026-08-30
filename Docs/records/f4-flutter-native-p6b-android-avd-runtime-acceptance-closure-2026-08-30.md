# Flutter Native P6-B Android AVD 运行态验收关闭记录

> 状态：`P6-B Go`；可在单独授权后进入 `P6-C` 同哈希真机验收
>
> 日期：2026-08-30（Asia/Shanghai）
>
> 前置记录：[P6-B Android AVD 第一轮运行态验收](/records/f4-flutter-native-p6b-android-avd-runtime-acceptance-2026-08-29)

## 1. 结论

P6-B 第二轮关闭了第一轮留下的两个阻断项：fresh medium AVD 的系统浏览器 OIDC 回流，以及 compact 根评论真实输入法下的连续 revision CAS。

OIDC 现场证据确认，首个 medium 尝试从打开 Chrome 到 callback 共约 `15 分 55 秒`，已经超过当前明确的 `15 分钟` authorization attempt TTL，因此 fail closed 符合契约，不是 pending attempt 在 Activity / Flutter owner 重建时丢失。紧接着发起的新尝试立即完成 callback、授权码兑换和登录，authenticated 私域读取、冷启动恢复与主题持久化均成立。

compact 根评论首次真实聚焦暴露出另一个运行时缺陷：输入法弹出改变 `MediaQuery.viewInsets` 后，评论卡片在每次 build 中创建新的 `GlobalKey`，导致编辑 composer、`FocusNode` 与 controller 被替换，键盘立即关闭。修正后由 `ForumDetailPage` 按评论 ID 持有稳定 key；widget 回归和 API 35 compact AVD 均确认输入法持续显示、编辑框保持焦点，两次连续保存把同一根评论从 revision `1 -> 2 -> 3`，证明客户端消费第一次响应的新 revision 后再提交第二次 CAS。

最终 release APK 已同时安装到 compact / medium API 35 AVD。P6-B 第一轮和第二轮的 default 主链路、四主题代表面、compact / medium 自适应、认证恢复和高风险写入已按受影响范围完成复验，因此本记录给出 **P6-B Go**。这只允许进入同一哈希的 P6-C 真机验收；正式签名、AAB、外部分发、商店、iOS 和 desktop 继续 `No-Go`。

## 2. 最终候选与复验范围

| 项目 | 结果 |
| --- | --- |
| 源码 | `dev` / `4acb7dbf` |
| 版本 | `26.8.2+1` |
| APK | `Clients/radish.flutter/build/app/outputs/flutter-apk/app-release.apk` |
| 字节大小 | `82,913,651` bytes |
| SHA-256 | `b08d0f5e0aea5d873bf61018e1ba8c1b654971fa94567e40343c9396fb2cc174` |
| 环境 | `development + https://localhost:5000` |
| compact | `Pixel_9_Pro` / API 35 / ARM64 / `1280x2856` portrait |
| medium | 临时 `Radish_Medium_API_35` / API 35 / ARM64 / `1600x2560` tablet |
| 接线 | 两台 AVD 均安装最终 APK，并建立 `adb reverse tcp:5000 tcp:5000` |
| 服务 | Gateway `https://localhost:5000`、API `http://localhost:5100`、Auth `http://localhost:5200` |
| 身份 | 只使用本地 developer seed；记录不包含密码、token、PKCE verifier、state 或 Cookie |

第二轮 medium authenticated、私域读取和四主题代表面先在直接前驱 APK `585cb01b5413bced60cf55aae02f013e3a96db5c8eb126626c555d7bb3efeac1` 上完成。随后唯一运行时代码变化是评论 ID 到稳定 `GlobalKey` 的 owner 修正；最终哈希重新覆盖了受影响的 compact Forum Detail 输入 / CAS，并在 medium 重新覆盖安装、OIDC、authenticated Shell、冷启动与既有 `guofeng` 偏好持久化。未受该局部评论子树影响的 medium 私域和主题证据按风险复验原则保留，不把前驱 APK 继续称为当前候选。

## 3. OIDC 诊断与关闭

### 3.1 时间线结论

- medium Chrome 打开 authorization request：约 `16:53:30`。
- 本地开发证书确认和浏览器交互后 callback 到达：约 `17:09:25`。
- 总时长约 `15 分 55 秒`，超过 `15 分钟` TTL；客户端拒绝该 callback 符合 state、PKCE、redirect、超时与一次性消费的 fail-closed 边界。
- `17:09:43` 发起的新尝试立即完成 OIDC callback 与 token exchange；没有发现 authorization attempt 因 Activity resume、Flutter owner 重建或系统浏览器往返而提前消失。

因此 `a4a7ae09` 把 TTL 从 `5` 分钟调整为 `15` 分钟的修正与现场一致；本轮没有继续放宽超时、持久化明文凭据、内嵌登录页或绕过 state / PKCE 校验。

### 3.2 medium authenticated 结果

- 订单、背包、Wallet、Experience 和账号浏览历史均完成真实读取；Wallet 显示权威余额与 `8 / 8` 条流水。
- `am force-stop` 后冷启动恢复 authenticated Shell，通知与账户入口仍可见，没有 `invalid_grant`。
- `default / guofeng / theme-dark-night / theme-sakura` 代表面完成 `1600x2560` 复核；最终恢复 `guofeng`。
- 最终哈希重新安装后再次登录并冷启动，authenticated Shell 保持；主题 Dialog 显示 `guofeng` 为“当前使用”，确认升级安装与重启持久化。

## 4. 评论编辑器焦点根因与修正

### 4.1 根因

`_ForumCommentCard` 与 `_ForumChildCommentCard` 原先在 `build()` 中创建新的 `GlobalKey` 并注册。Android 输入法打开会改变 `MediaQuery.viewInsets`，触发详情重建；新 key 使评论卡片子树被替换，`ForumCommentEditComposer` 的 controller / `FocusNode` 随即 dispose，表现为键盘弹出后立即关闭、评论框失焦。

### 4.2 修正

- `ForumDetailPage` 继续作为评论定位 owner，并通过 `_commentKeys.putIfAbsent(commentId, GlobalKey.new)` 按评论 ID 返回稳定 key。
- root / child comment card 只消费 `commentKeyFor`，不再在 build 中创建 key。
- 既有定位滚动仍在取得目标评论 key 时调度，没有增加全局状态、API、数据库字段、依赖或平台分支。
- 回归测试主动打开评论编辑器键盘、保存原 `FocusNode`、注入 `FakeViewPadding(bottom: 300)` 模拟 `viewInsets` 变化，并断言重建后仍是同一节点且 `hasFocus=true`。

### 4.3 compact 真实结果

- 最终哈希下点击根评论编辑器后，Android `dumpsys input_method` 同时确认 `mInputShown=true`、`mIsInputViewShown=true`，UI hierarchy 中目标 `EditText focused=true`；等待后键盘不再自动关闭。
- 受控评论 `2074106180321411072` 的本地旧夹具存在实体 `ContentRevision=NULL`、但基线 revision 已为 `1` 的不一致。第一次保存按契约返回 `409`，没有产生 revision、submission 或内容写入。
- 经明确授权把该单行夹具临时对齐到已有基线 `1` 后，第一次保存 `P6-RC-COMMENT-v1-20260830`：实体 `ContentRevision=2`、`EditCount=1`，新增 revision `2`。
- 不刷新详情再次编辑并保存 `P6-RC-COMMENT-v2-20260830`：实体 `ContentRevision=3`、`EditCount=2`，新增 revision `3`。第二次成功直接证明本地节点已消费第一次返回的 revision `2`。

## 5. 自动化与构建

| 门禁 | 结果 |
| --- | --- |
| Forum Detail 定向 | `33 / 33` |
| Flutter 全量 | `428 / 428` |
| `flutter analyze` | 零问题 |
| Android JVM | `7 / 7`；本轮未修改 Android 平台代码 |
| release APK | 构建通过；最终哈希见第 2 节 |
| `git diff --check` | 通过 |

最终构建命令保持：

```bash
flutter build apk --release \
  --dart-define=RADISH_ENVIRONMENT=development \
  --dart-define=RADISH_GATEWAY_BASE_URL=https://localhost:5000
```

## 6. 数据恢复与环境清理

评论 CAS 结束后已在单一 SQLite transaction 中完成精确恢复：

- 删除本轮两个 `ForumCommentEdit` submission：`2094014564898177024`、`2094015003907588096`。
- 删除本轮两个 `CommentContentRevision`：`2094014564948508672`、`2094015003945336832`。
- 目标评论恢复为原内容、`EditCount=0`、`ContentRevision=NULL`、修改人 / 修改时间均为 `NULL`。
- 原基线 revision `2081370772319899648`、原创建 submission `2074106180283662336` 和既有 outbox `2085899780122214401` 保留；没有新增 edit history、revision attachment 或 outbox。
- compact 输入法 subtype 已恢复为原中文拼音配置。

本轮服务、ADB reverse / forward、两台 AVD、临时 medium AVD 与主机 / 设备临时 helper 的最终停止和删除结果在提交前复核；不会清除既有 compact AVD、种子用户、真实设备 App / data 或浏览器凭据。

## 7. P6-B 退出与下一顺位

P6-B 退出条件已满足：

1. compact / medium 使用同一最终 APK，公开读取、authenticated 回流、冷启动、输入法和高风险 Forum CAS 均有真实 Android 证据。
2. default 主链路与四主题代表矩阵未发现阻断级认证、写入、导航、遮挡、横向溢出或共享视觉 owner 偏差。
3. 本轮运行时根因有静态回归；最终 APK 由通过全量测试与 analyze 的源码构建。
4. 受控数据已精确恢复，环境按授权边界清理。

下一顺位为 `P6-C Android physical-device RC acceptance`：必须使用 SHA-256 `b08d0f5e…2cc174` 的同一 APK，在项目所有者提供并授权的真实 Android 设备上复核安装、系统浏览器 OIDC、会话 / Back / 冷启动、输入法、高风险写入和四主题代表面。任何运行时代码变化都会使本哈希失效并要求按影响范围重跑 P6-B；P6-C 完成前仍不能宣称 Android 新版 UI 本地 / 内部 RC Go。
