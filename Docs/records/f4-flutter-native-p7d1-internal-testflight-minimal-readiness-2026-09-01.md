# Flutter Native P7-D1 Internal TestFlight minimal readiness（2026-09-01）

## 1. 结论

- **状态**：`Readiness 已确认，实施完成`
- **当前结论**：Internal TestFlight 仍为 `No-Go`。项目所有者已批准临时复用生产 `https://radishx.com` 并接受数据隔离风险；D1 运行时门禁与 repository preflight 已按本记录实施，结果见 [D1 实施记录](/records/f4-flutter-native-p7d1-internal-testflight-minimal-readiness-implementation-2026-09-01)。D2 Apple Team / 真机与 D3 archive / upload 仍需独立授权。
- **实施建议**：优先建立与生产隔离的 HTTPS testing Gateway，并把其 `RADISH_PUBLIC_URL`、OIDC Issuer、官方客户端回调和 Flutter `RADISH_GATEWAY_BASE_URL` 固定为同一 origin。若项目所有者决定临时复用 `https://radishx.com`，必须显式承担真实数据、旧服务版本与写入隔离风险，不能由仓库历史记录自动推定。
- **本批范围**：只冻结 fail-closed 环境契约、静态 preflight、build number、privacy / export compliance 技术判断、D2 / D3 授权和验收矩阵；不访问 Apple 账号、签名材料、App Store Connect 或真实设备，不构建 archive / IPA，不启动或部署 Gateway。

## 2. 已核对事实

### 2.1 Gateway

- `Docs/` 与部署编排把 `RADISH_PUBLIC_URL` 定义为 Gateway、Frontend、OIDC Issuer 与回调的部署态单一真相源。
- `https://radishx.com` 有生产部署与历史 smoke 记录；仓库没有任何证据把它批准为 testing 环境。
- `https://test-gateway.example`、`https://gateway.example`、`https://gateway.radish.example` 和 `https://test.radish.example.com` 都是示例值，不可进入分发候选。
- 当前 `AppEnvironment.fromDartDefines()` 对缺失或非法 Gateway 静默回退：testing 可落到 `https://localhost:5000`，production 可落到保留示例域名；非法布尔值也会被当作未配置。这是 D1 首要阻断。

### 2.2 版本与 iOS 工程

- `version.json` 是产品版本真相源，当前为 `productVersion=26.8.2`、`flutterBuildNumber=1`；`Clients/radish.flutter/pubspec.yaml` 已同步为 `26.8.2+1`。
- 当前没有 archive / upload 记录，因此 build `1` 尚未被证明占用；D3 冻结首个上传候选时仍需先在 App Store Connect 核对，再决定使用或递增。
- Runner 继续固定 `com.radish.client`、iOS `13.0+`、iPhone / iPad family、Release entitlements 与完整 AppIcon；仓库未写入 Team、certificate、profile 或 ExportOptions。
- 仓库目前没有 Internal TestFlight preflight 脚本，也没有 App target 自有 `PrivacyInfo.xcprivacy`；依赖侧 privacy manifest 只能在依赖解析与最终 archive 中复核，不能凭源码扫描代替 archive privacy report。

### 2.3 Encryption / privacy 技术事实

- App 网络只使用 Dart / iOS 系统 HTTPS/TLS；OIDC PKCE 使用 `SHA-256`，认证状态使用 iOS Keychain，源码没有自定义 cipher、密钥交换、加解密协议或面向用户的加密功能。
- 上述事实支持“只使用系统 / 标准且通常可豁免的加密能力”的技术方向，但不是法律结论。本轮不向 `Info.plist` 猜测写入 `ITSAppUsesNonExemptEncryption`。
- D3 上传前必须基于当时依赖锁、最终 archive 与 Apple questionnaire 重新确认：是否只使用豁免加密、是否需要文档、回答人与证据时间；任何新增 VPN、消息端到端加密、自定义 crypto 或相关 SDK 都会使该结论失效。

## 3. 待确认的 D1 实施契约

### 3.1 不新增 distribution channel define

继续使用既有三项 define，不增加与 environment 重叠的第四个渠道开关：

- `RADISH_ENVIRONMENT=development|testing|production`
- `RADISH_GATEWAY_BASE_URL=<origin>`
- `RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES=true|false`

`testing` 与 `production` 本身就是分发态。这样可避免“environment 是 testing、channel 却是 development”一类互相矛盾的组合。

### 3.2 运行时 fail closed

确认后修改 `AppEnvironment.fromDartDefines()`：

1. 只接受 `development / testing / production`；未知或空白显式值失败。
2. development 未提供 Gateway 时保留 `https://localhost:5000`；但只要显式提供非法 URL 就失败，不再静默回退。
3. testing / production 必须显式提供 Gateway，不设任何 localhost、生产或示例 fallback。
4. 分发 Gateway 必须是纯 HTTPS origin：不得包含 user info、query、fragment 或非根 path；必须是可部署 DNS host，拒绝 loopback、IP literal、`.local`、`.localhost`、`.test`、`.example`、`.invalid` 及其他保留示例 host。
5. testing / production 的本地开发证书开关固定为 false；显式 true 或非法布尔值立即失败。
6. API、Auth、Gateway 与公开复制链接继续从同一规范化 origin 派生，不引入 Flutter 专属 BFF 或第二套 Auth base URL。

定向测试至少覆盖缺失 define、非法 environment、HTTP、loopback、IP、示例域名、path / query / fragment、非法证书布尔值、testing 显式 true、合法 HTTPS testing origin、development 默认与 Android loopback 开发证书边界。

### 3.3 仓库静态 preflight

确认后新增只读的 `Scripts/check-flutter-ios-internal-testflight.mjs` 与根脚本入口。preflight 只验证候选输入与仓库事实，不调用 Apple 服务、不读取 Keychain / signing identity / provisioning：

- Gateway 复用与运行时相同的 HTTPS origin / 保留 host 拒绝规则；
- environment 必须为 `testing`，local certificate 必须显式为 `false`；
- `version.json`、Flutter pubspec、Runner build settings 的 version / build / bundle 一致；
- build number 为正整数，并由候选记录声明“在 App Store Connect 未占用”；
- Release entitlement 只包含预期能力，Debug entitlement 不得误用于 Release；
- `Info.plist`、AppIcon、iPhone / iPad family、deployment target、dSYM 配置与 privacy manifest inventory 可解析；
- 仓库不存在 certificate、private key、provisioning、archive、IPA、token、ExportOptions secret 或签名材料误提交。

preflight 通过只表示“可以申请 D2 / D3 外部操作授权”，不表示已签名、已上传或 TestFlight `Go`。最终 archive 仍需在独立授权后核对 `codesign`、embedded profile、resolved entitlements、privacy report、symbols、SHA-256 与实际 build metadata。

### 3.4 build number

- `version.json.flutterBuildNumber` 继续作为 repository truth；不引入日期、Git commit 或本机时间动态生成的第二套 build number。
- 每个准备上传的候选在 source freeze 时显式递增并提交；同一 build number 不装配第二个不同 source / define archive。
- processing 失败、验证失败或已上传候选被放弃后，下一个上传候选必须使用更大的 build number；不回退、不复用。
- 当前 `+1` 只视为“尚未证明占用”，不能在未访问 App Store Connect 时写作首个 TestFlight build 已确定。

## 4. D2 / D3 授权清单

以下动作必须逐层单独授权，D1 确认不自动授权后续层：

| 层级 | 允许动作 | 明确不允许 |
| --- | --- | --- |
| D1 implementation | 修改 Dart 环境校验、单测、静态 preflight 与文档；运行测试、analyze、无签名 Simulator / device settings 静态检查 | 读取 Apple Team / certificate / profile，连接真机，archive / upload |
| D2 account preflight | 只读核对有效会员、目标 Team、explicit App ID 与目标测试设备范围 | 创建 distribution certificate、App Store Connect build 或 tester group |
| D2 device smoke | 经授权使用 automatic development signing，在明确设备上安装同一 testing 候选 | distribution archive、upload、external tester |
| D3 App Store Connect setup | 经授权创建或核对 app record、distribution signing / provisioning、Internal Testing group | external group、public link、Beta App Review、App Review |
| D3 archive / upload | 经授权生成唯一 signed archive，validation、upload、export compliance 处理和 Internal Only 分配 | customer distribution、production rollout、App Store submission |

任何授权都必须明确 Team / app / device 类别 / build / Gateway / 有效期 / 清理条件；凭据、2FA、private key、provisioning 原文、token、cookie 和完整 device identifier 不进入仓库或记录。

## 5. 同一候选与验收矩阵

D2 与 D3 复用同一 source、version / build、testing Gateway 和 release define；签名类别变化必须记录，但不得顺带修改 Dart 或业务配置。

最低 phone 矩阵：

1. 首装、启动、HTTPS 与公开读取；
2. 系统 Safari OIDC、callback、PKCE、Keychain session；
3. 登录中终止 App 后冷 callback、terminate / relaunch 恢复；
4. 私域读取、发帖 / 评论 / 商品购买等当前已存在的高风险写入；
5. logout、token 清理、非法 / 过期 callback fail closed；
6. compact 布局、键盘、旋转、四主题与 reduced motion 基线。

Runner 声明 iPad family；有可用 iPad 时补 medium / expanded、旋转、键盘与核心读写代表矩阵。无 iPad 时必须在候选记录中写明缺口，不能用 Simulator 结论冒充真机结论。

TestFlight build 默认有效期按 Apple 当前规则记录为 `90` 天；候选失效条件包括 source、Dart / iOS 工程、依赖锁、define、Gateway、version / build、entitlements、签名或服务契约变化。停止时移除 internal group 分配、停用不再需要的测试账号 / 数据，并按授权精确清理设备和外部状态。

## 6. 项目所有者待确认输入

两项输入均已由项目所有者确认：

1. 当前只能复用生产入口，批准 `https://radishx.com` 作为近期 Internal TestFlight 唯一 Gateway，并接受真实数据隔离风险。
2. 按第 3 节实施 fail-closed + static preflight；D1 已完成，未访问 Apple 账号或真实设备。

## 7. 本批验证与停止线

- `git status --short --branch`：开始时 `dev...origin/dev [ahead 2]`，工作树干净；
- 定向检索部署域名、Flutter define、版本 owner、iOS build settings、privacy / encryption 和既有脚本；
- `plutil -p`：Runner `Info.plist` 与 Release entitlements 可解析；
- 未启动服务、Simulator / AVD，未构建 App，未访问 Apple 账号、签名材料、App Store Connect 或真实设备。

下一步不是 archive，而是单独完成 D2 development-signed 真机 Smoke readiness 与授权。
