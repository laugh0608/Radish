# Flutter Native P7-C iOS Simulator 运行态验收 readiness（2026-08-31）

## 1. 结论

- **状态**：`readiness 已完成，运行方案待确认`
- **当前裁决**：P7-B 已具备进入 P7-C 的代码、平台工程与本机构建基线；本次只完成只读事实审计和运行方案冻结，没有启动 Gateway / Auth / API、Simulator，没有安装或运行 App，也没有产生新的运行态验收结论。
- **推荐下一步**：项目所有者确认本记录后，再为同一任务单独授权服务启动、两个专用临时 Simulator、应用安装和真实 Smoke。P7-C 通过只代表 iOS Simulator 平台风险关闭，不代表真机、签名、TestFlight、App Store、外部分发或 production `Go`。
- **架构判断**：没有发现需要先修改运行时代码或引入新依赖的阻断。P7-C 应验证 P7-B 新增的 iOS 原生边界，不重复执行 P6 已覆盖的完整 Dart 业务矩阵。

## 2. 已审计事实

### 2.1 代码与平台边界

- Flutter shell 在启动和回到前台时消费平台 pending callback；iOS `SceneDelegate` 同时承接冷启动和运行中 URL callback，`AppDelegate` 只保存一份待消费 payload 并通过 MethodChannel 交给 Dart。
- callback parser 只接受 `radish://oidc/callback` 与 `radish://oidc/logout-complete`；state、PKCE、redirect、过期和 replay 校验继续 fail closed，授权尝试 TTL 为 `15` 分钟。
- iOS session 与 OIDC authorization attempt 使用 Keychain，accessibility 为 `first_unlock_this_device`；Forum / Docs recent 等非敏感回访状态使用 Dart `shared_preferences`。iOS Runner 的 Debug / Profile 与 Release 均已绑定 Keychain entitlements。
- iOS 本地开发默认目标为 `https://localhost:5000`。只有显式传入 `RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES=true`，且目标仍为 loopback host 时，App 内 API / token 请求才允许本地开发证书；该开关不能替代系统 Safari 对 authorize 页面证书的信任。
- 当前 P7-B `Runner.app` 是 build-only 证据，没有带入 P7-C 所需的显式本地证书 define，因此不得直接作为运行候选。

### 2.2 本机 Simulator 事实

- 已安装并可用的 runtime 为 `iOS 26.5`（`com.apple.CoreSimulator.SimRuntime.iOS-26-5`）。
- `iPhone 17e` 与 `iPad mini (A17 Pro)` device type 均可用；审计时所有现有 Simulator 都是 `Shutdown`。
- 仓库外已有同型号 Simulator，但它们不是 P7-C 资产。本批不复用、不擦除、不改动任何既有设备，运行时只创建以下两个专用临时设备：

| 临时设备名 | Device type | 主要断点 | 方向 |
| --- | --- | --- | --- |
| `Radish_P7C_iPhone_17e_26_5` | `com.apple.CoreSimulator.SimDeviceType.iPhone-17e` | compact | portrait |
| `Radish_P7C_iPad_mini_A17_Pro_26_5` | `com.apple.CoreSimulator.SimDeviceType.iPad-mini-A17-Pro` | medium / expanded | portrait / landscape |

实际逻辑尺寸和命中的 Flutter breakpoint 必须在运行时记录，不由设备型号名称推断。两个设备共用同一份候选 `Runner.app`，避免 phone / tablet 证据来自不同源码或构建参数。

### 2.3 服务、认证与测试数据

- 本地组合宿主仍由 `./start.sh` 启动 Gateway、Auth 与 API；预期入口分别为 `https://localhost:5000`、`http://localhost:5200`、`http://localhost:5100`。启动属于运行态操作，仍需本任务单独授权。
- 本地 developer seed 已启用，`radish-client` 已注册 `radish://oidc/callback` 与 logout callback；P7-C 使用既有本地种子管理员完成需要权益的矩阵，不把账号密码、authorization code、token、state、verifier、cookie 或 Keychain 内容写入证据。
- 本地种子管理员当前具备 `theme-dark-night` 与 `theme-sakura` 权益，审计基线没有 active theme。P7-C 结束时必须恢复为无 active theme。
- 运行前应记录本批可能触达的数据基线：种子管理员可编辑资料、主题权益激活状态、受控 Forum / Docs 计数、附件下载计数、浏览历史，以及 OpenIddict authorization / token 的基线 ID 或计数。快照只保留清理所需标识，不保存密钥或令牌正文。

## 3. 唯一候选与本地 TLS

### 3.1 候选装配

取得运行授权后，从当时干净的 `dev` HEAD 构建一次 Debug Simulator 候选：

```bash
cd Clients/radish.flutter
flutter build ios --simulator --debug --no-codesign \
  --dart-define=RADISH_ENVIRONMENT=development \
  --dart-define=RADISH_GATEWAY_BASE_URL=https://localhost:5000 \
  --dart-define=RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES=true
```

候选证据至少固定 source commit、Flutter / Dart / Xcode 版本、三个 Dart define、bundle identifier、版本、Runner executable SHA-256 和构建时间。运行中如修改任何 Dart、iOS 原生代码、依赖或 build define，该候选立即失效，必须重新构建并重跑受影响矩阵。

### 3.2 证书边界

1. 服务健康后，从 `localhost:5000` 的实际 TLS handshake 只导出当前 Gateway 公共证书到受控临时目录，不读取或导出私钥。
2. 使用 `simctl keychain add-root-cert` 只把该公共证书加入两个 P7-C 临时 Simulator。
3. 如果系统 Safari 仍不能在不绕过安全错误的情况下打开 authorize 页面，立即停止并记录证书根因；不得改用 HTTP、关闭 TLS 校验或扩大 App 的 bad-certificate 允许范围。
4. 删除两个临时 Simulator 时一并清除其受信任证书、Safari cookie、Keychain 和 App 数据；本机临时公共证书文件随后删除。

## 4. 运行矩阵

### 4.1 共用预检

| 编号 | 验证 | 通过条件 |
| --- | --- | --- |
| C0 | 源码与候选 | 工作树干净；commit、define、bundle 信息与 executable SHA-256 已冻结；两台设备安装同一候选 |
| C1 | 服务与 TLS | Gateway / Auth / API 健康；Safari 可正常打开 authorize 页面；App 仅通过 loopback 开发证书 opt-in 通信 |
| C2 | 数据基线 | 测试账号、OIDC redirect 与必要种子数据存在；受影响业务数据和 OpenIddict 基线已记录且不含秘密 |
| C3 | 设备隔离 | 只创建、启动和清理两个命名的 P7-C 临时 Simulator；既有 Simulator 保持不变 |

### 4.2 iPhone compact 主矩阵

| 编号 | 场景 | 通过条件 |
| --- | --- | --- |
| P1 | fresh anonymous launch | 首次启动无 crash；App 名称 / 图标、safe area、五个一级入口、滚动与 compact 导航正常，无横向溢出 |
| P2 | 匿名阅读与本机回访 | 匿名 Forum / Docs detail 可读；Forum / Docs recent 和内置 `default / guofeng` 偏好在 terminate / relaunch 后保留 |
| P3 | OIDC 取消 | 从系统 Safari 返回但不产生 callback 时，App 保持匿名并给出可恢复反馈，不伪造登录成功 |
| P4 | Keychain + UIScene 冷回调 | 发起登录后，在 Safari 停留期间 terminate App；再用本地种子管理员完成登录。`radish://` callback 必须冷启动 App，并从 Keychain 取回同一 PKCE attempt 完成换 token |
| P5 | session 冷恢复 | 登录后 terminate / relaunch 仍保持正确账号；在 access token 生命周期跨越后再次回前台或冷启时可通过既有 refresh 机制恢复，不泄露 token |
| P6 | 真实输入与可逆写入 | Profile 地址经正式 UI 做一次临时修改并恢复原值；Forum 评论编辑器只验证点击、键盘、连续输入和焦点稳定，不提交测试评论 |
| P7 | 四主题与权益 | `default / guofeng` 和有权益的 `theme-dark-night / theme-sakura` 在代表页可见且语义正常；结束前停用权益主题并恢复审计基线 |
| P8 | logout 与非法 callback | 正常 logout callback 后冷启仍为匿名；没有 pending attempt 时注入受控 callback 必须 fail closed，不能建立会话 |

### 4.3 iPad medium / expanded 补充矩阵

| 编号 | 场景 | 通过条件 |
| --- | --- | --- |
| T1 | portrait medium | 记录实际逻辑尺寸与 breakpoint；五个一级入口、长内容、safe area、滚动和键盘无严重裁切或横向溢出 |
| T2 | landscape expanded | 旋转到 landscape 后记录实际尺寸与 breakpoint；导航、主内容与 context rail 不重叠，焦点 / 键盘和滚动仍可用 |
| T3 | OIDC 与 session | 完成一次正常系统浏览器登录，并验证 terminate / relaunch 后的安全 session 恢复；不能以 iPhone 结果替代 |
| T4 | 本机 recent | Forum / Docs recent 在同一临时 iPad 的 terminate / relaunch 后保留，且不错误继承 iPhone 设备状态 |
| T5 | 代表主题面 | 至少覆盖内置主题和一个权益主题的阅读 / 表单代表面；布局变化不破坏 medium / expanded 结构 |

P7-C 重点是 UIScene callback、Keychain / preferences、Safari TLS、phone / tablet 断点、旋转和 iOS 键盘。P6 已闭合的完整 Forum CAS、全页面业务能力和 Android 行为不在本批机械重跑。

## 5. 证据边界

- 保留一份按 `C0–C3 / P1–P8 / T1–T5` 编号的通过 / 失败表、问题与裁决；截图只保留必要的匿名壳层、callback 完成后 App 状态、三档布局、主题和键盘代表面。
- 每张截图记录设备名、UDID、runtime、方向、逻辑尺寸、候选 SHA-256 和时间；登录输入页、密码、浏览器 cookie、token、state、verifier、authorization code 与 Keychain 内容不得进入截图或原始日志。
- 命令输出和网络日志只摘录健康状态、状态码与稳定错误，不保存完整 header、cookie、query secret 或响应 token。
- P7-C 最终记录必须区分：静态 / build 既有事实、当前真实 Simulator 事实、未验证项和后置的 P7-D 边界。

## 6. 停止线与失败处理

出现任一情况立即停止相应矩阵并先做根因诊断，不以临时兼容或放宽安全边界换取通过：

1. callback、state、PKCE、redirect、TTL、replay、Keychain session 或 refresh 恢复不符合 fail-closed 契约；
2. App 接受无 pending attempt 的 callback，或运行需要把账号、密码、token、证书私钥嵌入 App / 脚本 / 证据；
3. Safari 无法建立本地 HTTPS 信任，或只能通过关闭 TLS / 扩大 bad-certificate 范围继续；
4. compact / medium / expanded 出现阻断主任务的溢出、safe area 覆盖、旋转错位、键盘立即关闭、输入失焦或不可恢复 crash；
5. 需要修改运行时代码、iOS 工程、依赖或 build define；修改后先完成定向测试、全量 Flutter 门禁与新候选构建，再重跑受影响矩阵；
6. 需要安装 / 更新 SDK、package、CocoaPods、Simulator runtime 或其他工具链；必须另行说明并授权。

## 7. 清理与复核

1. 两台设备分别走正常 logout，确认再次冷启为匿名。
2. 通过正式 UI 恢复 Profile 可见字段；停用本批激活的权益主题，恢复运行前无 active theme 基线。
3. 对照运行前快照检查 Forum / Docs、附件计数、浏览历史和 OpenIddict 增量。优先使用产品 UI 和会话生命周期自然清理；若仍需直接数据库删除，必须只针对已证明属于 P7-C 的精确 delta，并在执行前再次取得授权，禁止整库替换或宽泛清理。
4. 用启动终端的 `Ctrl+C` 停止组合宿主，确认 `5000 / 5100 / 5200` 不再监听。
5. 只 terminate / shutdown / delete `Radish_P7C_iPhone_17e_26_5` 与 `Radish_P7C_iPad_mini_A17_Pro_26_5`；不得按 runtime 或全设备范围清理。
6. 删除临时公共证书与包含敏感信息的原始证据；仓库只保留脱敏的验收记录和必要截图。
7. 复核 `git status`，确认没有 DerivedData、Simulator 数据、证书、日志、数据库副本或测试凭据进入工作树。

## 8. 授权边界

项目所有者确认本记录，只代表接受 P7-C 的运行矩阵和风险边界。执行下列动作仍需要在下一轮单独授权：

- 启动 Gateway / Auth / API；
- 创建、启动、安装、操作和删除上述两个专用 Simulator；
- 构建并安装带本地开发 define 的唯一候选；
- 从本地 Gateway handshake 导出公共证书并写入两个临时 Simulator 的 keychain；
- 运行真实 OIDC、输入、可逆资料写入、主题激活 / 停用和清理；
- 如发生异常增量，执行任何直接数据库清理。

不在本记录授权范围：Apple Team、真机、正式签名、TestFlight、App Store、外部分发、production 配置或生产数据。
