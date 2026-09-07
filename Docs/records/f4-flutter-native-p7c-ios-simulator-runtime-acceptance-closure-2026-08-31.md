# Flutter Native P7-C iOS Simulator 运行态验收关闭（2026-08-31）

## 1. 结论

- **状态**：`Simulator Go`
- **当前裁决**：同一份 iOS Simulator 候选已在专用临时 iPhone 17e 与 iPad mini 上完成真实运行态验收。系统 Safari OIDC、UIScene 冷 callback、Keychain session / PKCE、Dart preferences、loopback TLS、compact / medium / expanded、旋转、真实键盘、四主题、logout 与非法 callback fail closed 均通过。
- **范围边界**：本结论只关闭 iOS Simulator 开发门禁，不代表真机、Apple Team、正式签名、provisioning、TestFlight、App Store、外部分发或 production `Go`。这些事项继续后置 P7-D，并在实际推进前单独完成 readiness 与授权。
- **代码影响**：本批没有修改 Dart、iOS 原生代码、平台工程或依赖；只生成本地 Simulator 候选、执行真实 Smoke、恢复受控业务状态并补充文档。

## 2. 唯一候选与工具链

| 项目 | 结果 |
| --- | --- |
| source commit | `ff54b12dad5d0697b5bde0869a5f8c6a62edab46` |
| version / build | `26.8.2+1` |
| bundle identifier | `com.radish.client` |
| environment | `development` |
| Gateway | `https://localhost:5000` |
| local certificate opt-in | `RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES=true` |
| Runner executable SHA-256 | `2fd300a815acc87dcbfe50a74bc3f1d4b1ab85a644beeb257f8a4e95fce52356` |
| build time | `2026-08-31T21:13:37+08:00` |
| Flutter / Dart | `3.44.0 / 3.12.0` |
| Xcode / iOS runtime | `26.6 (17F113) / 26.5 (23F77)` |
| executable / signature | universal `x86_64 arm64`；ad-hoc Simulator signature；`TeamIdentifier=not set` |

两台设备均安装上述同一份 `Runner.app`，没有以不同源码、define 或构建结果拼接 phone / tablet 证据。

### 2.1 readiness 候选命令修正

readiness 记录原建议使用 `flutter build ios --simulator --debug --no-codesign`。真实运行发现该产物虽可安装和启动，但 `flutter_secure_storage` 访问 Simulator Keychain 时 `SecItem` 返回 `-34018`，导致 shell 保持认证恢复态，无法验证 P7-B 的安全存储边界。

在不修改源码、依赖和三个 Dart define 的前提下，去掉 `--no-codesign` 重新构建后，Xcode 为 Simulator 产物生成 ad-hoc 签名；`TeamIdentifier` 仍为空，不涉及 Apple Team、开发者证书或 provisioning。Keychain 随后恢复正常，P7-C 全矩阵使用的即为上表哈希。由此冻结后续规则：**需要验证 Keychain 的 iOS Simulator 运行候选必须保留 Xcode 默认 ad-hoc Simulator 签名，不得使用 `--no-codesign` 产物替代。**

## 3. 共用预检

| 编号 | 结果 | 运行事实 |
| --- | --- | --- |
| C0 源码与候选 | `Pass` | 工作树从 `ff54b12d` 装配；候选哈希、版本、bundle、define 与签名已冻结；两台设备安装同一产物 |
| C1 服务与 TLS | `Pass` | Gateway / Auth / API 分别在 `5000 / 5200 / 5100` 提供本地组合宿主；只把 Gateway 当前公共证书加入两台临时设备，Safari 与 App 均可完成 HTTPS 链路 |
| C2 数据基线 | `Pass` | 种子账号、OIDC redirect、主题权益及可能触达的业务 / OpenIddict 计数已在运行前记录；证据未保存账号秘密、token、code、state、verifier、cookie 或 Keychain 内容 |
| C3 设备隔离 | `Pass` | 只创建 `Radish_P7C_iPhone_17e_26_5` 与 `Radish_P7C_iPad_mini_A17_Pro_26_5`；既有同型号设备未复用、未修改，结束后仅删除这两个临时设备 |

## 4. iPhone compact 主矩阵

临时设备：`Radish_P7C_iPhone_17e_26_5`，iOS `26.5`，逻辑尺寸 `390 × 844`，命中 compact portrait。

| 编号 | 结果 | 运行事实 |
| --- | --- | --- |
| P1 fresh anonymous launch | `Pass` | fresh 安装匿名启动稳定；App / safe area、五个一级入口、滚动和 compact 导航正常，无横向溢出 |
| P2 匿名阅读与本机回访 | `Pass` | 匿名 Forum / Docs detail 可读；两类 recent 与内置主题偏好在 terminate / relaunch 后保留，结束时恢复 `guofeng` |
| P3 OIDC 取消 | `Pass` | Safari 中取消且不产生 callback 后保持匿名，App 给出可恢复反馈，没有伪造登录成功 |
| P4 Keychain + UIScene 冷 callback | `Pass` | 发起登录后在 Safari callback 确认界面 terminate App；确认 Open 后由 `radish://` callback 冷启动 App，并从 Keychain 取回同一 PKCE attempt 完成登录，没有丢失或 replay |
| P5 session 冷恢复 | `Pass` | 登录为 `account20001` 后 terminate / relaunch 仍恢复正确会话；长时间交互期间既有 refresh 路径持续可用，没有暴露 token |
| P6 真实输入与可逆写入 | `Pass` | Forum 评论编辑器连续输入、键盘与焦点稳定，未提交测试评论；Profile 地址由空值临时改为 `p7c` 后经正式 UI 恢复为空值 |
| P7 四主题与权益 | `Pass` | `default / guofeng / theme-dark-night / theme-sakura` 均在代表面真实应用且语义正常；结束时权益主题均停用并恢复 `guofeng` |
| P8 logout 与非法 callback | `Pass` | 正常 logout callback 后冷启为匿名；无 pending attempt 时注入受控非法 callback，只显示“登录尝试不存在或已过期”类稳定反馈并保持访客，符合 fail closed |

## 5. iPad medium / expanded 补充矩阵

临时设备：`Radish_P7C_iPad_mini_A17_Pro_26_5`，iOS `26.5`。portrait 原生截图为 `1488 × 2266` px、设备 `@2x`，即逻辑尺寸 `744 × 1133`，命中 medium；landscape 为逻辑尺寸 `1133 × 744`，命中 expanded。

| 编号 | 结果 | 运行事实 |
| --- | --- | --- |
| T1 portrait medium | `Pass` | fresh iPad 没有继承 iPhone recent；五个一级入口、长内容、safe area、滚动和 portrait 软件键盘正常，无严重裁切或横向溢出 |
| T2 landscape expanded | `Pass` | 旋转后使用 expanded header / navigation；Forum detail 主内容与 context rail 不重叠。打开评论表单并输入时，横屏键盘、焦点、滚动与 rail 仍可用；输入已删除且未提交 |
| T3 OIDC 与 session | `Pass` | 首次 callback 因操作间隔超过 `15` 分钟 TTL 被正确拒绝；立即 Retry 后 Safari SSO 产生新 callback 并完成登录。terminate / relaunch 后仍恢复 `account20001`，证明 iPad 独立 Keychain 链路成立 |
| T4 本机 recent | `Pass` | iPad 上打开 Forum / Docs detail 后 terminate / relaunch，账户菜单同时出现两类继续阅读入口；状态来自当前 iPad，没有错误继承 iPhone |
| T5 代表主题面 | `Pass` | `guofeng` 与权益主题 `theme-dark-night` 覆盖阅读 / 表单代表面，medium / expanded 结构保持；结束时停用权益主题并恢复 `guofeng` |

首次 iPad 授权尝试的 TTL 拒绝属于既有 `15` 分钟安全契约的预期 fail-closed 证据，不是产品失败；同设备上的新尝试已立即闭环，因此 T3 通过。

## 6. 数据复核与清理

### 6.1 可逆业务状态

| 项目 | 运行前 | 运行后 | 结论 |
| --- | --- | --- | --- |
| Profile 地址 | 空字符串 | 空字符串 | 已通过正式 UI 恢复；仅审计更新时间变化 |
| active theme | 无 | 无 | dark / sakura 权益仍存在但均未激活 |
| BrowseHistory | `8` 行，计数合计 `141` | 同基线 | 未产生异常浏览历史写入 |
| Posts | `2` 行，views 合计 `250`，comments 合计 `2` | `2` 行，views 合计 `253`，comments 合计 `2` | `+3` 为本批真实公开阅读计数；无测试评论 |
| Comments | `2` 行 | `2` 行 | 无新增或删除 |
| Attachments | `15` 行，downloads 合计 `2330` | 同基线 | 无下载副作用 |

OpenIddict authorizations 由 `152` 增至 `156`，tokens 由 `2719` 增至 `2744`；这些增量对应本批正常登录、刷新、登出、过期尝试和受控非法 callback 的本地协议审计轨迹，不含业务脏数据，也未用直接数据库删除掩盖生命周期事实。

### 6.2 环境清理

- 两台设备均先正常 logout 并冷启确认匿名，再精确删除；设备内 App、Safari cookie、Keychain、preferences 与受信任公共证书随临时设备一并清除。
- `./start.sh` 组合宿主已通过启动终端 `Ctrl+C` 停止，`5000 / 5100 / 5200` 均无监听。
- 临时 Gateway 公共证书、iPad 原生截图与其他本地临时证据已删除；仓库没有保存凭据、token、浏览器会话、Keychain 内容、证书私钥、Simulator 数据或数据库副本。
- 既有 iPhone 17e 与 iPad mini Simulator 均保持原有 `Shutdown` 状态；没有执行全 runtime 或全设备范围清理。

## 7. 门禁裁决与下一步

P7-C 以 `Simulator Go` 关闭。它证明 P7-B 的 iOS runtime owner 可以在 phone / tablet、三档布局、系统浏览器、Keychain / preferences、旋转与真实输入环境中工作，也没有暴露需要回写 Flutter Pencil 设计源的共享结构偏差。

下一顺位进入 **P7-D iOS 真机、签名与分发 readiness**。readiness 只审计并冻结 Apple Team、bundle / capability、证书与 provisioning、真机、构建配置、安装、TestFlight / App Store / 外部分发、证据和清理边界；在方案确认和独立授权前，不读取或写入签名材料，不连接或安装到真机，不创建 distribution archive，也不产生任何外部分发状态。
