# Flutter Native P7-D Internal TestFlight-only 项目所有者裁决（2026-09-01）

## 1. 裁决

- **状态**：`已确认`
- **项目所有者决定**：当前没有 App Store 上架能力，iOS 近期目标只到 **Internal TestFlight**；不开放 External TestFlight，不提交 App Review，不上架 App Store。
- **计划影响**：账号删除、完整 Flutter UGC 举报 / 用户屏蔽、StoreKit / IAP、App Store privacy answers、商品页 metadata、截图、age rating、review account 与公开 Support / deletion metadata 不再阻断近期 Internal TestFlight，统一后置到未来 External TestFlight / App Store readiness。
- **安全边界**：本裁决不是绕过 Apple 规则的声明。现有商城与数字权益只允许在受信任内部测试者中作为工程能力验证，不写作 IAP 合规，不使用 external tester、public link 或客户分发。
- **授权边界**：本次确认只调整项目路线和文档，不授权登录 Apple Developer / App Store Connect、读取签名材料、连接真机、创建 archive、上传 build 或邀请 tester。

## 2. 近期仍必须关闭的门禁

Internal TestFlight 仍是 Apple 托管分发，不等同于 Simulator 或免费 Personal Team 真机安装。近期门禁固定为：

1. **Testing environment fail closed**：Internal TestFlight build 必须显式使用项目所有者确认的真实 HTTPS Gateway；缺失、非法、loopback 或示例域名立即失败，`RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES` 必须为 false。
2. **Apple Developer Program 与 App Store Connect 基础**：需要有效会员、目标 Team、explicit App ID、App Store Connect app record、Apple Distribution 与匹配 provisioning；实际读取或创建仍需单独授权。
3. **Archive preflight**：唯一 source、version / build、Release define、bundle、entitlements、AppIcon、privacy manifest / privacy report、符号、签名链与敏感信息扫描通过；每次 upload 使用唯一 build number。
4. **Export compliance**：即使只做 TestFlight，也必须准确回答 encryption / export compliance；未作判断前不写 `ITSAppUsesNonExemptEncryption` 猜测值。
5. **Internal-only 分发**：优先上传为 Apple 标记的 `TestFlight Internal Only` build；只加入 Internal Testing group，只邀请 App Store Connect 内部用户，不创建 external group 或 public link。
6. **真实设备回归**：同一候选至少覆盖一台 iPhone；由于 Runner 声明 iPad family，如有可用 iPad 应补 phone / tablet 代表矩阵。OIDC、Keychain、terminate / relaunch、TLS、logout、键盘、旋转与当前核心业务必须在真实 testing Gateway 下成立。
7. **证据与清理**：内部 build、tester group、设备、测试数据和外部状态必须有明确 owner、有效期、停止 / 失效与清理记录；不得把凭据、证书、provisioning 原文、token 或完整 device identifier 写入仓库。

Apple 当前允许最多 `100` 名 App Store Connect 内部用户参与 Internal TestFlight，build 可测试 `90` 天；标记为 Internal Only 的 build 不能转为 external testing 或 customer distribution，详见 [Add internal testers](https://developer.apple.com/help/app-store-connect/test-a-beta-version/add-internal-testers)。TestFlight build 仍需 provisioning 中包含 application identifier，并需要处理 export compliance，详见 [TestFlight overview](https://developer.apple.com/help/app-store-connect/test-a-beta-version/testflight-overview) 与 [Provide export compliance information for beta builds](https://developer.apple.com/help/app-store-connect/test-a-beta-version/provide-export-compliance-information-for-beta-builds/)。

## 3. 调整后的 P7-D 分层

### P7-D1：Internal TestFlight minimal readiness

- 冻结 approved testing Gateway 与 fail-closed build contract；
- 冻结 build number、privacy / entitlement / export compliance preflight；
- 冻结 Apple account、Team、App ID、app record、certificate / profile、internal group 和证据的授权清单；
- 不实现账号删除、StoreKit 或完整 Flutter UGC 能力，不访问 Apple 外部状态。

### P7-D2：development-signed 真机 Smoke

- 经单独授权读取目标 Team 与设备边界，优先 automatic signing；
- 使用真实 testing Gateway 在同一 source / define 候选上完成 development-signed 真机安装和核心 runtime matrix；
- 不创建 distribution archive，不上传 App Store Connect。

### P7-D3：TestFlight Internal Only

- 经独立授权创建 / 核对 App ID、app record、distribution signing 与 provisioning；
- 生成、验证并上传唯一 signed archive；
- 处理 processing / export compliance 后，只分配 Internal Testing group；
- 在 TestFlight 安装版本上复验关键矩阵并记录 `Internal TestFlight Go / No-Go`。

### P7-D4：External TestFlight / App Store（无限期后置）

重新进入 D4 时，必须按当时 Apple 规则重新 readiness。账号删除、UGC 举报 / 屏蔽、StoreKit / commerce、App privacy、Support / Privacy / deletion URL、metadata、Beta App Review 与 App Review 都在 D4 重新成为前置门禁；不能沿用本次 Internal-only 裁决宣称外部分发合规。

## 4. 当前停止线

- 未确认真实 testing Gateway 前，不修改 distribution define 或装配 archive。
- 未取得单独授权前，不登录 Apple 账号、不读取 Team / certificate / provisioning、不连接或注册设备。
- 未完成 D1 preflight 前，不创建 signed archive / IPA 或上传 build。
- Internal TestFlight 不开放非 App Store Connect 内部用户，不创建 external tester group、public link 或 App Review submission。
- 任何未来 external / customer distribution 请求都必须先恢复 D4 合规门禁并重新确认范围。
