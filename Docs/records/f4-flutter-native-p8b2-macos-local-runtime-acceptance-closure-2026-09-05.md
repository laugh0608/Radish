# Flutter Native P8-B2 macOS local runtime acceptance 关闭记录

> 日期：2026-09-05（Asia/Shanghai）
>
> 源码基线：`dev@ad476699`
>
> 结论：`P8-B2 macOS local runtime acceptance Go`

## 1. 范围与候选

本批按 [P8-B readiness](/records/f4-flutter-native-p8b-macos-platform-foundation-readiness-2026-09-05) 对 B1 形成的 macOS 工程执行本地真实运行验收，并关闭运行中发现的平台边界：

- 候选身份保持 `Radish` / `com.radish.client` / `26.8.2+1` / macOS `10.15+`；
- 宿主为 Apple Silicon macOS `26.6.2`，工具链为 Flutter `3.44.0` 与 Xcode `26.6`；
- 只连接 `development + https://localhost:5000`，通过显式本地开发证书 opt-in 启动本地 Gateway / Auth / API；
- 运行候选为完成签名校验的 standalone Debug `Radish.app`，临时注册 `radish` URL scheme 后独立启动；`flutter run / attach` 会修改或注入 framework 并破坏嵌套签名，不作为本批真实候选证据；
- 没有连接生产 Gateway，没有生成 Developer ID、notarization、DMG / PKG、App Store 或外部分发状态，也没有启动 Windows / Linux VM。

## 2. 运行中修正

真实运行揭示并关闭了五组 B1 编译门禁无法证明的边界：

1. macOS runner 同时注册 native auth 与 Forum follow-up channel；当前桌面端没有原生 Forum pending handoff，返回 `nil`，不再因 channel 缺失阻断启动。
2. adaptive navigation 使用稳定 shell key，expanded / medium / compact resize 不再重建页面 body，Docs 已选内容和查询状态可跨窗口档位保留。
3. `NSWorkspace.shared.open(url)` 使用正确的 URL opener；非法 scheme fail closed，系统拒绝打开时返回 `open_failed`，日志不输出认证 URL 内容。
4. OIDC attempt 清理先判断条目是否存在，使缺失 Keychain item 的清理幂等；logout 将 attempt cleanup、browser logout 与 local session cleanup 分离，本地 session 不再因浏览器或 attempt 清理失败而滞留。
5. 无 Apple Developer Program 会员、ad-hoc 签名的本地候选无法使用 Data Protection Keychain 的 application identifier / access group。macOS 因此显式使用非同步的本机登录 Keychain：`first_unlock_this_device`、`synchronizable: false`、`usesDataProtectionKeychain: false`。session 与 OIDC attempt 仍由系统加密 Keychain 持有，不回退到 preferences 或内存；未来有正式签名与 entitlement 时重新评估 Data Protection Keychain。

## 3. 真实运行矩阵

| 范围 | 结果 |
| --- | --- |
| guest / TLS | Gateway 公开读取与 development localhost TLS opt-in 通过 |
| OIDC 热 callback | 系统浏览器登录后回到已运行 app，登录用户为种子 `Admin`（公开 ID `20001`） |
| OIDC 冷 callback | 浏览器停留期间完全退出 app，callback 冷启动 app 并恢复认证 |
| Keychain | authorization attempt 在打开浏览器前写入、callback 后消费；session 在退出重启后恢复 |
| preferences | theme、Forum recent 与 Docs recent 在退出重启后恢复；最终还原到测试前基线 |
| logout | app 立即回到 guest，浏览器 end-session 发出，本地 session 与 attempt 均清除 |
| callback replay | 登出后重放相同 state 被拒绝为“找不到对应的登录尝试，或登录尝试已经过期”，保持 guest |
| 窗口档位 | expanded `1280 × 800`、medium `800 × 720`、compact `590 × 720` 均通过，resize 保留 Docs body 状态 |
| 主题 | `default`、`guofeng`、`theme-dark-night`、`theme-sakura` 均实际切换；`guofeng` 重启恢复通过 |
| 键鼠与焦点 | hover、滚动、可见 scrollbar、Tab / Shift+Tab、Enter / Space、Escape、`Ctrl + 3` 与搜索输入通过 |
| 窗口行为 | 最小化后 callback 激活、Zoom、进入 / 退出系统全屏、frame 恢复与关闭最后窗口退出均通过 |

Keychain 证据只核对 service / account、创建与删除状态以及时间元数据，没有读取 session、token、code、PKCE verifier 或其他秘密内容。使用的 service 为 `flutter_secure_storage_service`，account 为 `radish.auth.session.v1` 与 `radish.auth.oidc_attempt.v1`。

## 4. 数据影响

本批登录与公开读取使用现有本地种子数据，没有创建帖子、评论、附件、订单、权益或其他业务对象。前后对账如下：

| 数据 | 验收前 | 验收后 | 结论 |
| --- | ---: | ---: | --- |
| OpenIddict applications | 3 | 3 | 不变 |
| OpenIddict authorizations | 156 | 159 | `+3`，保留为本批登录协议审计事实 |
| OpenIddict tokens | 2744 | 2774 | `+30`，保留为本批登录 / refresh / logout 生命周期事实 |
| UserBrowseHistory rows / ViewCount | `9 / 186` | `9 / 186` | 不变 |
| posts / total ViewCount | `2 / 253` | `2 / 259` | 公开读取计数 `+6`；两个既有帖子分别为 `+5` 与 `+1` |
| comments | 2 | 2 | 不变 |
| attachments / DownloadCount | `15 / 2330` | `15 / 2330` | 不变 |
| user `20001` active benefits | 0 | 0 | 不变 |

OpenIddict 生命周期行和公开帖子浏览计数属于本批真实运行的预期审计 / 读取副作用，不直接删除或改写数据库历史。除此之外没有业务写入需要清理。

## 5. 验证与清理

| 验证 | 结果 |
| --- | --- |
| auth / persistence / adaptive navigation 定向测试 | `23 / 23` 通过 |
| `flutter test` | `443 / 443` 通过 |
| `flutter analyze` | 通过，0 问题 |
| macOS RunnerTests | `5 / 5` 通过 |
| `flutter build macos --debug` | 通过；standalone app 深度签名校验通过 |
| `flutter build macos --release` | 通过 |
| 文档与仓库卫生 | `npm run check:docs`、`git diff --check` 通过 |

运行结束后已停止 Radish app、Gateway、Auth 与 API；`5000 / 5100 / 5200` 无监听，目标进程不存在。临时 LaunchServices 注册已注销，测试创建的浏览器空白页已关闭，未触碰用户原有页面。两个 Keychain item 均不存在；`com.radish.client` preferences 通过先删除 domain 再导入基线，最终只保留测试前 `NSWindow Frame RadishMainWindow`。一次性 `/private/tmp` baseline 已删除。

## 6. 结论与下一步

P8-B2 已关闭，macOS 本地运行态结论为 `Go`。这证明本地 development 环境中的系统浏览器 OIDC、安全持久化、三档窗口、四主题、桌面输入和生命周期闭环，不等于正式签名、公证、安装包或分发已完成。

下一顺位进入 `P8-C Windows toolchain + platform foundation readiness`：先只读审计 Windows 日常开发 VM 与 CleanBase VM 的系统、Flutter / Visual Studio C++ Desktop workload / ATL、构建链和清理边界。任何工具安装、依赖变更、平台工程生成、项目启动或 VM 基线修改仍须按范围单独授权。
