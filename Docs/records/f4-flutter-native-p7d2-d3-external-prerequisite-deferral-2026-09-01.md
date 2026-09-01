# Flutter Native P7-D2 / D3 外部前置条件暂缓记录

> 日期：2026-09-01（Asia/Shanghai）
>
> 结论：`P7-D2 / P7-D3 Paused by external prerequisites`；`P7-D1 Go` 保持有效

## 1. 项目所有者事实

项目所有者确认当前：

- 没有有效的 Apple Developer Program 付费会员；
- 没有可用于验收的真实 iPhone 或 iPad；
- 只有 iOS Simulator 等虚拟设备。

该事实不表示仓库实现失败，也不撤销 P7-C `Simulator Go` 或 P7-D1 minimal readiness `Go`。它只意味着当前无法取得 P7-D2 与 P7-D3 所要求的外部证据。

## 2. 对 P7-D2 / D3 的影响

- P7-D2 要求 Apple Team 范围内的 development signing、provisioning、真机安装与真实设备 Smoke；Simulator 不能产生这些证据。
- P7-D3 要求可用的 Apple Developer Program / App Store Connect 权限、distribution signing、唯一 build upload 和 Internal Only tester group；当前没有可执行入口。
- Apple 当前工作流仍要求先向 App Store Connect 上传 build，并使用包含 App ID 的 provisioning profile；App Store Connect 初始 Account Holder 也与 Apple Developer Program membership 关联。参见 [TestFlight overview](https://developer.apple.com/help/app-store-connect/test-a-beta-version/testflight-overview)、[App Store Connect workflow](https://developer.apple.com/help/app-store-connect/get-started/app-store-connect-workflow)与[会员能力对比](https://developer.apple.com/support/compare-memberships/)。
- 因此 Internal TestFlight 继续为 `No-Go`；不得把 Simulator、无签名 generic device Release build 或仓库 preflight 写作真机 / TestFlight 通过。

## 3. 暂缓期间保持的边界

- 保留 `config/internal-testflight.json`、distribution fail-closed 和 repository preflight，作为未来重新进入 D2 / D3 时的静态基线。
- 生产 `https://radishx.com` 仍只是已批准的候选 Gateway；没有 signed archive、upload 或 tester build，当前不会产生 TestFlight 真实数据。
- 不为无法执行的上传提前递增 `version.json.flutterBuildNumber`，不猜测写入 Apple Team、provisioning、ExportOptions 或 export-compliance 答案。
- iOS Simulator 与 Android AVD 可继续承担开发回归；它们不替代真机性能、系统权限、硬件行为、签名安装或 TestFlight 生命周期验收。
- P7-D4 External TestFlight / App Store 继续无限期后置。

## 4. 恢复条件

只有以下条件同时具备，才重新打开 P7-D2 / D3：

1. 有效 Apple Developer Program membership，以及允许使用的 Apple Team / App ID / App Store Connect 范围；
2. 至少一台可用于当前候选验收的真实 iPhone；iPad 仍为可选补充目标；
3. 重新对当时最新源码、版本、Gateway、privacy / entitlement 和 build number 执行 D1 preflight；
4. 项目所有者对 Apple 外部状态读取、签名、真机安装、production Gateway 写入、archive 和 upload 分阶段单独授权。

## 5. 下一路线

当前工程第一顺位改为 `Flutter Native post-P7 无 Apple 外部前置候选审计`。该审计只在以下两条既有产品边界中选定一个下一专题，不直接实施架构或平台工程：

- 继续完成不依赖真实移动设备的 Flutter Native 产品能力；
- 按既有 `desktop stage-gated` 约束评估 P8 Windows / macOS / Linux desktop platform readiness。

两条路线会明显改变实现范围，须由项目所有者确认后再冻结专题和实施边界。
