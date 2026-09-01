# Flutter Native P7-D1 Internal TestFlight minimal readiness 实施（2026-09-01）

## 1. 结论

- **状态**：`D1 Go`
- **Gateway 裁决**：项目所有者确认当前没有独立 testing 环境，批准 Internal TestFlight 临时复用生产 `https://radishx.com`，并接受真实数据隔离风险。
- **工程结果**：Flutter testing / production 环境已从静默 fallback 改为 fail closed；Internal TestFlight define 已进入版本控制；repository preflight 已覆盖 Gateway、version / build、Release settings、entitlements、AppIcon、privacy manifest inventory 与 Apple 分发敏感制品。
- **分发结论**：Internal TestFlight 仍为 `No-Go`。D1 只关闭仓库内门禁；D2 Apple Developer membership / Team / development signing / 真机和 D3 App Store Connect / distribution archive / upload 均未授权、未执行。

## 2. 项目所有者确认与风险边界

项目所有者在 D1 readiness 后明确确认：

1. 当前只能复用生产入口；
2. 批准 `https://radishx.com` 作为近期 Internal TestFlight 唯一 Gateway；
3. 接受内部测试与生产数据共用所产生的数据隔离风险。

该批准只关闭 Gateway 决策，不把生产环境改写为真正隔离的 testing 部署。D2 / D3 运行态必须额外固定：

- 专用内部测试账号，不使用普通真实用户账号；
- 测试内容、订单、评论和其他写入使用可定位标记并记录原始值；
- 不执行无法精确恢复的破坏性操作；
- 每轮记录新增 / 修改对象和清理结果；
- 生产 Gateway 版本、OIDC、测试账号和写入契约必须在真机前重新只读核对，历史 smoke 不替代当日事实。

## 3. 实施内容

### 3.1 受版本控制的 Internal TestFlight define

新增 `Clients/radish.flutter/config/internal-testflight.json`，唯一内容为：

- `RADISH_ENVIRONMENT=testing`
- `RADISH_GATEWAY_BASE_URL=https://radishx.com`
- `RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES=false`

后续 D2 unsigned / development-signed build 与 D3 distribution archive 必须使用同一文件，避免命令行手写漂移。修改该文件会使当前候选与设备 / TestFlight 证据失效。

### 3.2 `AppEnvironment` fail closed

`Clients/radish.flutter/lib/core/config/app_environment.dart` 已完成：

- environment 只接受 `development / testing / production`；显式空值或未知值失败；
- development 缺省继续使用 `https://localhost:5000`，但显式非法 Gateway 不再回退；
- testing / production 必须显式提供 Gateway；不再回退到 localhost 或 `gateway.radish.example`；
- distribution Gateway 必须是纯 HTTPS origin，拒绝 user info、path、query、fragment、loopback、IP literal、保留示例域名与非规范 URL；
- testing / production 的本地开发证书开关必须为 false；非法布尔值失败；development 也只能对 loopback Gateway 开启；
- API、Auth、Gateway 和公开链接继续共用同一 origin。

`AppEnvironment.production()` 的保留示例域名常量入口已删除，防止未来绕过 compile-time define 门禁。

### 3.3 repository preflight

新增：

- `Scripts/check-flutter-ios-internal-testflight.mjs`
- `Scripts/check-flutter-ios-internal-testflight.test.mjs`
- `npm run check:flutter-ios-internal-testflight`
- `npm run check:flutter-ios-internal-testflight:self-test`

preflight 固定验证：

1. define keys 精确、environment 为 testing、Gateway 精确等于已批准 origin、local cert 为字符串 false；
2. `version.json` 与 Flutter pubspec 的 `26.8.2+1` 一致；
3. Runner `com.radish.client`、iOS `13.0+`、iPhone / iPad、Release entitlements、dSYM 与 AppIcon 配置存在；
4. 1024x1024 marketing icon 文件与实际 PNG 尺寸成立；
5. 解析后的 `flutter_secure_storage_darwin` 与 `shared_preferences_foundation` privacy manifests 存在；
6. 仓库没有 Apple API key、provisioning、P12、IPA、xcarchive、ExportOptions 或 Flutter 路径下的证书 / private key 误提交；后端既有 development TLS `.pfx` 不被误判为 Apple signing material。

本检查刻意不访问 Apple 服务，因此不能证明 build `1` 在 App Store Connect 未占用，也不能替代最终 archive 的 resolved entitlements、privacy report、签名链、symbols 与 export compliance 判断。

## 4. 构建与 privacy 事实

使用版本控制 define 执行：

```bash
flutter build ios --release --no-codesign \
  --dart-define-from-file=config/internal-testflight.json
```

结果：

- 通用 iOS device Release 无签名构建通过，Flutter 报告 `Runner.app 64.0MB`；
- built `Info.plist`：`com.radish.client`、`26.8.2 (1)`、MinimumOS `13.0`、iPhone / iPad family；
- built App AOT 包含 `https://radishx.com` 与环境 fail-closed 字符串；
- App bundle 包含 Flutter、`flutter_secure_storage_darwin` 与 `shared_preferences_foundation` 三份 `PrivacyInfo.xcprivacy`；App target 当前没有自有 manifest；
- App bundle 没有 embedded provisioning profile；该产物不可安装、不可分发，也不是 D2 / D3 候选；
- Runner executable SHA-256：`c6ba3a053c64167514f182a07c54483461913de43a8163cccccc1122bd9ff063`；
- App AOT executable SHA-256：`d585bfff550de52d3daf0a531707a564a76a5aecba9447253505155ab3e68a3f`。

Flutter 不支持 iOS Simulator Release mode；对应命令在构建前被工具链拒绝，没有产生错误候选。最终改用上述 generic device `--no-codesign` 验证 Release 配置，没有连接或安装到真实设备。

## 5. 验证

- `flutter test test/app_environment_test.dart`：`7 / 7`；
- `flutter test`：`437 / 437`；
- `flutter analyze`：零问题；
- `npm run check:flutter-ios-internal-testflight:self-test`：`4 / 4`；
- `npm run check:flutter-ios-internal-testflight`：通过；
- `npm run check:version-contract`：`26.8.2` 通过；
- iOS generic device Release `--no-codesign`：通过；
- built metadata、privacy manifests、embedded provisioning 与 executable SHA-256 定向核对：通过。

本轮没有启动 Gateway、Simulator / AVD，没有访问生产数据，没有登录 Apple Developer / App Store Connect，没有读取 Team、certificate、profile 或本机 signing identity，没有连接真机、创建 archive / IPA 或上传 build。

## 6. D1 关闭与下一停止线

D1 代码与静态门禁关闭。下一顺位是 `P7-D2 development-signed 真机 Smoke readiness`，开始前需要项目所有者分别确认：

1. 当前是否具备有效的付费 Apple Developer Program membership；
2. 允许核对的 Apple Team / App ID 范围；
3. 可用于测试的一台 iPhone，以及是否有 iPad；
4. 生产 Gateway 上的专用测试账号、写入标记、原始值记录与清理边界；
5. 是否授权 Xcode 只读核对 Team / device readiness，以及后续 development signing 和真机安装。

未取得这些授权前，不读取 Apple 外部状态、不连接真机。D2 `Go` 也不会自动授权 D3 distribution archive、App Store Connect 或 TestFlight upload。
