# Flutter Native P7-D iOS 真机、签名与分发 readiness（2026-09-01）

## 1. 结论

- **状态**：`readiness 已完成，真机 / 分发实施 No-Go`
- **当前裁决**：P7-C 已证明同一 Dart / iOS runtime owner 可在 iPhone 与 iPad Simulator 上成立；仓库的 bundle、URL callback、Keychain entitlement、图标、版本和 Release configuration 也具备继续产品化的基础。但当前分发环境仍可静默回落到 localhost / 示例域名，Flutter 没有账号删除、内容举报和用户屏蔽入口，站内胡萝卜余额又可购买并解锁数字主题 / 权益而没有 StoreKit。上述事实在外部 TestFlight / App Store 前必须先关闭或取得明确适用性裁决。
- **推荐下一步**：先进入 `P7-D1 iOS distribution compliance capability gate`，按已冻结边界实现 fail-closed 分发配置、账号删除、Flutter UGC 举报 / 屏蔽，并由项目所有者裁决 iOS 数字权益购买采用 StoreKit 还是在外部分发渠道关闭。D1 方案确认前不修改架构、接口或运行时行为。
- **范围边界**：本批只完成仓库、本机非敏感工具链和官方规则的只读审计。没有读取 Apple Team、Keychain signing identity、证书、private key 或 provisioning 内容；没有登录 Apple 账号、连接 / 注册真机、安装 App、创建 archive / IPA、写入 App Store Connect 或上传构建。

> 后续裁决：项目所有者已于同日确认近期只使用 Internal TestFlight，账号删除、完整 Flutter UGC、StoreKit 与 App Store metadata 后置 D4；调整后的门禁与分层见 [P7-D Internal TestFlight-only 项目所有者裁决](/records/f4-flutter-native-p7d-internal-testflight-owner-scope-2026-09-01)。本记录保留原始全分发审计事实，不再作为近期 D1 的范围定义。

## 2. 仓库与本机构建事实

### 2.1 App identity 与平台工程

| 项目 | 当前事实 | readiness 判断 |
| --- | --- | --- |
| version / build | `26.8.2+1` | App Store Connect 每次上传仍需唯一 build number，不能把当前 `+1` 直接视为可重复上传策略 |
| bundle identifier | `com.radish.client` | 可作为 explicit App ID 候选；是否已在目标 Apple Team 注册尚未读取 |
| display name | `Radish` | 原生 `Info.plist` 已固定 |
| minimum iOS | `13.0` | Runner project 与 Flutter 基线一致 |
| device family | iPhone + iPad（`1,2`） | 真机与 TestFlight 必须同时按声明的平台族验收，不能只测 phone |
| URL callback | `radish://oidc/callback`、`radish://oidc/logout-complete` | URL scheme owner 已存在；目标环境 OpenIddict client redirect 仍需按真实 Gateway 核对 |
| scenes | UIScene，multiple scenes disabled | P7-C 已验证冷 callback；真机仍需复验系统 Safari 与 terminate / relaunch |
| app icon | `AppIcon` 含 `1024 × 1024`，无 alpha | 具备 archive 静态检查基础 |

Runner 有 `Debug / Release / Profile`，shared `Runner` scheme 的 ArchiveAction 使用 `Release`。通用 device Release settings 可解析为 `iphoneos26.5`、`arm64`、`SKIP_INSTALL=NO`、`com.radish.client`、`26.8.2+1`、iPhone / iPad 和 `Runner/Release.entitlements`。本机 Xcode `26.6 (17F113)`、iPhoneOS SDK `26.5`，满足 Apple 当前 iOS upload 要求的 Xcode `16+` 工具链下限。

### 2.2 签名与 capability

- Runner target 没有提交 `DEVELOPMENT_TEAM`、固定 provisioning profile 或真实签名材料；只有 project-level `iPhone Developer` identity hint。RunnerTests 的 `CODE_SIGN_STYLE=Automatic` 不能替代 Runner target 的签名裁决。
- Debug / Profile 使用 `Runner/DebugProfile.entitlements`，Release 使用 `Runner/Release.entitlements`；两者当前都只声明空 `keychain-access-groups`。这与 `flutter_secure_storage 10.3.1` iOS Keychain 接线要求一致，也没有提前引入 Push、Associated Domains、App Groups 或其他未使用 capability。
- 仓库没有 `.mobileprovision`、`.p12`、`.cer`、`.ipa`、`.xcarchive`、`ExportOptions.plist`、fastlane 配置或签名 secret。应继续保持签名材料在仓库外，由 Xcode / Apple account owner 管理。
- 本机通用 device destination 只有 `Any iOS Device` placeholder，没有已连接 iPhone / iPad。readiness 不把 placeholder、Mac 上 Designed for iPad / iPhone 或 P7-C ad-hoc Simulator signature 当作真机证据。

Apple 官方区分 [Apple Development 与 Apple Distribution 证书](https://developer.apple.com/help/account/certificates/certificates-overview/)；开发证书用于设备开发，分发证书用于指定设备测试或提交 App Store Connect，且账号、证书与相关材料属于敏感资产。实际实施优先让 Xcode automatic signing 管理 development / distribution provisioning；只有自动签名不适用时才冻结手工 profile，不把 team id 或 profile UUID 写死进共享工程。

### 2.3 插件 privacy manifest

iOS SwiftPM 当前只解析 Flutter framework、`flutter_secure_storage_darwin 0.3.2` 与 `shared_preferences_foundation 2.5.7`：

- `flutter_secure_storage_darwin` 自带 `PrivacyInfo.xcprivacy`，当前未声明 tracking、collection 或 required-reason API；
- `shared_preferences_foundation` 自带 manifest，声明 `UserDefaults` required-reason category 与 `1C8F.1`；
- App target 当前没有自有 `PrivacyInfo.xcprivacy`。这不自动构成错误；实际 D1 / archive 前必须扫描 App 自有 native API 和最终 archive privacy report。只有 App 自己使用 required-reason API 时才在 App manifest 精确声明，不复制插件声明或添加宽泛理由。

Apple 要求 App 和第三方 SDK 分别在自己的 privacy manifest 中描述数据收集与 required-reason API；自 2024-05-01 起，未描述 required-reason API 的 build 不会被 App Store Connect 接受，详见 [Privacy manifest files](https://developer.apple.com/documentation/bundleresources/privacy-manifest-files) 与 [Describing use of required reason API](https://developer.apple.com/documentation/bundleresources/describing-use-of-required-reason-api)。

## 3. 当前阻断与产品风险

### 3.1 分发环境没有 fail closed

`AppEnvironment.fromDartDefines()` 当前默认 `development + https://localhost:5000`；`production` fallback 是 `https://gateway.radish.example`，非法 Gateway define 会静默退回环境默认值。README 中的 production 示例也仍是占位域名。由此可能产生“Release / archive 构建成功，但真实设备永远连接 localhost 或示例域名”的伪候选。

P7-D1 必须建立分发构建契约：

1. `testing / production` 的分发候选必须显式传入项目所有者确认的 HTTPS Gateway；缺失、非法、loopback 或保留示例域名时构建 / 启动 fail closed。
2. `RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES` 在 testing / production 分发候选中必须为 false；不能用开发证书 opt-in 绕过真实 TLS。
3. 构建记录必须保存 environment 与非秘密 Gateway origin，但不得保存 token、账号、证书或 API key。
4. 当前文档中出现过不同真实 / 示例域名；本批不猜测 production / testing endpoint，待项目所有者明确后再实现。

这是构建与运行时行为变更，必须在 D1 方案确认后实施。

### 3.2 账号创建存在，账号删除缺失

Flutter 使用系统 Safari 打开 Auth 登录页；`Radish.Auth/Views/Account/Login.cshtml` 明确提供 Register 入口，因此 App 支持账号创建。仓库审计没有发现用户自助账号删除 endpoint、生命周期、本人 UI 或 Flutter direct completion link。

Apple 规定支持账号创建的 App 必须允许用户在 App 内发起账号删除，并要求处理与账号关联、法律不要求保留的数据，包括用户生成内容，详见 [Offering account deletion in your app](https://developer.apple.com/support/offering-account-deletion-in-your-app/)。D1 需要先定义删除语义、保留义务、审计、撤销 / 冷静期、OIDC session 与公开 UGC 处理，再建立 Flutter 内可完成的入口；不能把“发邮件联系客服”当作默认替代。

该能力涉及账号、数据与运行时规则，必须单独确认专题边界后实现。

### 3.3 Flutter UGC 缺少举报与屏蔽入口

Radish 已有成熟服务端能力：

- `POST /api/v1/ContentModeration/Report` 支持 Post、Comment、PostAnswer、PostQuickReply、ChatMessage、Product 与 ProductReview；
- `POST /api/v1/UserBlock/Block|Unblock` 与 `GET /api/v1/UserBlock/GetMine` 提供统一屏蔽真相；
- Web 已有 `ContentReportModal`、用户屏蔽和本人列表实现。

Flutter 当前允许发帖、回答、评论、轻回应和商品购买，但没有消费上述举报 / 屏蔽契约。Apple [App Review Guidelines 1.2](https://developer.apple.com/app-store/review/guidelines/) 要求 UGC / 社交 App 具备不当内容过滤、举报并及时响应、屏蔽滥用用户和公开联系信息。

D1 应复用既有 API 和服务端治理，不新建移动专属 BFF 或第二套关系真相：在可见 UGC 与公开用户上下文提供举报 / 屏蔽，保持 LongId string、登录回流、稳定 operation key、局部失败、解除与本人列表；同时为 App Store metadata 提供真实可访问的 Support URL / 联系方式。服务端内容过滤与及时治理事实仍需在 App Review notes / 运行证据中可解释。

### 3.4 数字权益购买需要商店策略裁决

Flutter 商品详情当前直接调用 `Shop/Purchase`，要求支付口令并扣除站内胡萝卜余额；商品可发放 Badge、Title、Theme 与其他数字权益，主题权益可在 App 内激活并改变功能呈现。iOS 工程和 Dart 依赖中没有 StoreKit / In-App Purchase。

Apple [App Review Guidelines 3.1.1](https://developer.apple.com/app-store/review/guidelines/) 当前要求解锁 App 内功能、数字内容或虚拟货币使用 In-App Purchase。Radish 的具体业务与 storefront 适用性需要产品所有者和必要的法律 / 商务判断，readiness 不作规避性解释。

外部 TestFlight / App Store 候选前必须二选一并形成文档化裁决：

1. **StoreKit 路线**：建立 IAP product mapping、server-side transaction validation、订单 / 胡萝卜账本 / entitlement 幂等对账、restore、refund / revoke、账号迁移和 App Review 说明；或
2. **iOS 分发关闭路线**：在明确的 distribution channel capability gate 下隐藏 / 禁止数字商品购买与可能被视为绕过 IAP 的引导，只保留经裁决允许的既有权益读取 / 使用边界。

两条路线都会改变产品与运行时边界，必须由项目所有者先确认，不能由 readiness 自动选择。内部本地 development 真机 Smoke 可以在不提交商店的前提下验证现有购买，但不得据此宣称 TestFlight / App Store `Go`。

### 3.5 App Store metadata、privacy 与 export compliance 尚未形成

- App Store Connect 的 iOS privacy policy URL 为必填，且数据收集回答必须覆盖 App 与第三方代码；当前仓库只能证明 Web 有 legal 页面，不能证明目标 production URL、隐私选择 / 删除 URL 与联系渠道已公开可访问。参见 [Manage app privacy](https://developer.apple.com/help/app-store-connect/manage-app-information/manage-app-privacy)。
- `Info.plist` 当前没有 `ITSAppUsesNonExemptEncryption`。App 使用 HTTPS 与密码学依赖，但是否属于豁免及需要何种文件必须在上传前准确判断；不能为了消除提示直接写 false。Apple 会在 TestFlight / App Store 提交时要求 export compliance 信息，参见 [Complying with Encryption Export Regulations](https://developer.apple.com/documentation/Security/complying-with-encryption-export-regulations)。
- 尚未建立 App Store app record、description、screenshots、age rating、review notes、review account、Support URL、privacy answers 或 beta test information 的已验证事实；这些都是外部状态，实际读取 / 创建需单独授权。

## 4. P7-D 分层实施方案

### 4.1 P7-D1：distribution compliance capability gate

目标是在不接触 Apple 账号 / 签名的前提下，关闭会让 archive 必然不可提交或不可运行的仓库能力缺口：

1. 分发环境与 Gateway fail-closed，补构建 / 单测门禁；
2. 设计并实现账号删除全链路与 Flutter 入口；
3. Flutter 接入既有内容举报、用户屏蔽 / 解除和本人屏蔽列表；
4. 按项目所有者裁决实现 StoreKit 或 iOS distribution commerce gate；
5. 审计 App 自有 required-reason API、privacy manifest、数据收集表、Support / Privacy / deletion URL 与 export compliance decision checklist；
6. Flutter 全量、analyze、iOS no-sign device build、RunnerTests、plist / entitlement / archive-preflight 静态门禁通过。

D1 可能涉及架构、API、数据库、账户生命周期和交易契约，实施前必须先形成专题方案并由项目所有者确认；依赖安装仍需另行授权。

### 4.2 P7-D2：development-signed 真机 Smoke

D1 关闭后，再由项目所有者单独授权 Apple account 与设备范围：

1. 明确唯一 Apple Team、目标 iPhone / iPad、设备 owner、是否注册和 automatic signing 策略；
2. 使用 Apple Development + development provisioning 安装同一 source / define 候选；记录 bundle、version / build、device / OS、签名类别和 executable SHA-256，不记录 Team 私密材料或 device UDID 全文；
3. 使用项目所有者确认的真实 testing HTTPS Gateway，验证 fresh install、Safari OIDC 冷 callback、Keychain session / PKCE、terminate / relaunch、background / foreground、logout、键盘、旋转 / iPad、举报 / 屏蔽、账号删除安全入口和 commerce gate；
4. 测试数据必须可识别、可逆；真机 App、provisioning、浏览器 session、受控业务 delta 与临时证据按授权范围精确清理。

Apple 设备可由 Xcode 自动或账号后台注册；device / profile 额度与状态属于外部资源，只有在明确授权后才读取或变更，参见 [Devices overview](https://developer.apple.com/help/account/devices/devices-overview)。

### 4.3 P7-D3：signed archive 与 Internal TestFlight

真机 Gate 通过后，单独创建 App Store Connect 外部状态：

1. 确认 explicit App ID `com.radish.client`、App Store app record、Apple Distribution 与 App Store Connect provisioning；automatic signing 优先，手工 profile 仅在必要时使用。Apple 当前要求 App Store Connect profile 绑定匹配 bundle 的 explicit App ID 与 distribution certificate，参见 [Create an App Store Connect provisioning profile](https://developer.apple.com/help/account/provisioning-profiles/create-an-app-store-provisioning-profile/)。
2. 冻结唯一 source、approved production / testing Gateway、`26.8.2` 与全新 build number；执行 Release archive / validation，核对签名链、entitlements、privacy report、AppIcon、symbols、export compliance 与敏感信息扫描。
3. 只有项目所有者明确授权后上传。Apple 用 bundle ID + version 关联 build，并要求唯一 build string；当前 iOS 上传 build 需 Xcode `16+`，详见 [Upload builds](https://developer.apple.com/help/app-store-connect/manage-builds/upload-builds)。Flutter 官方流程由 `flutter build ipa` 生成 `.xcarchive` 与 `.ipa`，参见 [Build and release an iOS app](https://docs.flutter.dev/deployment/ios)。
4. processing 与 compliance 问题关闭后，只先开放 Internal TestFlight。TestFlight build 最长可测试 `90` 天，内部 tester 上限 `100`；外部测试不是同一门禁，参见 [TestFlight overview](https://developer.apple.com/help/app-store-connect/test-a-beta-version/testflight-overview)。

### 4.4 P7-D4：External TestFlight / App Store

仅在 Internal TestFlight、privacy / deletion / UGC / commerce、metadata 与真实 device 回归全部关闭后进入：

- external beta information、review notes、review account 与联系渠道完整；
- 首个 external build 按 Apple 当前规则接受 Beta App Review，外部 tester 上限 `10,000`；
- production 域名、Privacy / Support / deletion URL 可公开访问，App privacy 与 export compliance 回答准确；
- iPhone / iPad screenshots、age rating、content rights、价格 / availability 和版本说明齐全；
- App Review 提交、release mode 与任何 production rollout 由项目所有者单独授权。

## 5. 候选、证据与失效规则

每一层都必须冻结并记录：

- source commit、工作树状态、Flutter / Dart / Xcode / SDK；
- `CFBundleIdentifier`、`CFBundleShortVersionString`、唯一 `CFBundleVersion`；
- environment、Gateway origin、local certificate flag；
- build configuration、archive / executable SHA-256、签名类别与 entitlements 摘要；
- device family、测试设备类别 / OS、矩阵结果、已知限制、数据基线和清理结果。

不得记录 Apple Account、密码、2FA、private key、certificate export、provisioning 原文、API key、token、cookie、OIDC code / state / verifier 或完整 device identifier。Dart、iOS 工程、依赖、define、签名类型、entitlements、version / build 或目标 Gateway 任一改变，当前层候选即失效；修改后先回到对应静态门禁，再重跑受影响设备 / TestFlight 矩阵。

## 6. 当前停止线与授权边界

readiness 完成不授权以下操作：

1. 读取本机 signing identity、证书、private key、provisioning profile 或 Apple Team；
2. 登录 Apple Developer / App Store Connect，创建 / 修改 App ID、device、certificate、profile、app record、tester 或 metadata；
3. 连接、注册、安装或操作任何真实 iPhone / iPad；
4. 创建 signed archive / IPA、validation、upload、TestFlight 分发或 App Review submission；
5. 写入 production / testing 域名、账号删除规则、StoreKit / commerce gate 或 privacy / export compliance 结论；
6. 安装 / 更新 Flutter package、CocoaPods、Xcode component、SDK 或其他依赖。

下一轮若进入 P7-D1，应先给出账号删除与 iOS commerce 的具体方案、接口 / 数据影响和验证边界，等待项目所有者确认；依赖变更另行授权。

## 7. 本批验证

- `git status --short --branch`：开始时 `dev...origin/dev`，工作树干净；
- `plutil -lint`：Runner `Info.plist`、DebugProfile / Release entitlements 语法通过；
- `xcodebuild -list -project Runner.xcodeproj`：Runner / RunnerTests、Debug / Release / Profile 与 shared scheme 可解析；
- `xcodebuild -showBuildSettings ... -sdk iphoneos -configuration Release CODE_SIGNING_ALLOWED=NO`：通用 device Release settings 可解析；
- 仓库签名 / archive / privacy / fastlane 文件扫描：没有签名材料、archive / IPA 或上传配置；
- Flutter / Web / backend 定向检索：举报与屏蔽服务端 / Web 契约存在，Flutter consumer 缺失；账号删除能力缺失；Flutter 数字权益购买存在且 StoreKit 缺失；
- Apple / Flutter 官方规则复核：证书、设备、profile、privacy、required-reason API、account deletion、UGC、IAP、export compliance、upload 与 TestFlight 分层已核对。

本批没有启动服务、Simulator / AVD，没有构建、安装、连接真机、创建 archive 或访问 Apple 账号。
