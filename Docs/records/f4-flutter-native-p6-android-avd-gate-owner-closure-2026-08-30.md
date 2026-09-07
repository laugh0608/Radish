# Flutter Native P6 Android AVD 门禁项目所有者关闭记录

> 状态：`Android UI AVD RC Go`；`P6-C` 真机验收后置且不再阻断进入下一阶段
>
> 日期：2026-08-30（Asia/Shanghai）
>
> 前置记录：[P6-B Android AVD 运行态验收关闭](/records/f4-flutter-native-p6b-android-avd-runtime-acceptance-closure-2026-08-30)

## 1. 裁决

项目所有者于 2026-08-30 明确确认：本阶段以 compact / medium 双 API 35 AVD 通过作为 Flutter Android 新版 UI 的开发退出门禁，暂不执行原计划中的 `P6-C Android physical-device RC acceptance`，允许进入下一阶段。

因此本记录给出 **Android UI AVD RC Go**，并把下一顺位切换为 `P7 iOS platform readiness`。这是一项阶段门禁裁决，不会把未执行的真机验收改写为已通过，也不会生成不存在的真机证据。

## 2. 继承证据

本裁决继承 P6-B 已关闭的同一最终候选：

| 项目 | 结果 |
| --- | --- |
| 源码 | `dev` / `4acb7dbf` |
| 版本 | `26.8.2+1` |
| APK | `Clients/radish.flutter/build/app/outputs/flutter-apk/app-release.apk` |
| 大小 | `82,913,651` bytes |
| SHA-256 | `b08d0f5e0aea5d873bf61018e1ba8c1b654971fa94567e40343c9396fb2cc174` |
| 自动化 | Forum Detail `33 / 33`、Flutter 全量 `428 / 428`、analyze 零问题、Android JVM `7 / 7`、release 构建通过 |
| 运行态 | compact / medium API 35 AVD 的系统浏览器 OIDC、私域读取、冷启动、四主题代表面、真实输入法、根评论连续 CAS `1 -> 2 -> 3` |
| 清理 | 受控业务数据精确恢复；服务、ADB reverse / forward、双 AVD 与临时设备已停止或删除 |

## 3. 结论边界

本轮允许：

1. 关闭 P6 当前开发门禁，不再等待 Android 真机才开始下一阶段。
2. 把双 AVD 结果表述为 Android 新版 UI 的本地开发 / AVD RC 证据。
3. 进入 iOS 平台 readiness、方案冻结和后续经确认的实现批次。

本轮不允许：

1. 表述为 `P6-C passed`、Android 真机兼容通过或真机安装通过。
2. 表述为正式签名、AAB、外部分发、商店提交或生产移动客户端 `Go`。
3. 用现有 debug-signed APK 代替未来分发候选。

## 4. 后置风险与恢复门禁

`P6-C` 从当前阻断门禁转为 Android 分发前后置门禁。后续出现以下任一条件时，必须基于当时的最新候选重新安排真实 Android 设备验收：

- 准备正式签名、AAB、外部分发、商店提交或受众内测；
- 出现设备厂商、系统版本、输入法、浏览器回流、网络证书或生命周期相关真实问题；
- 项目所有者重新要求真机复核。

一旦 P7 或后续开发修改 Dart、Android 平台代码、依赖或构建参数，当前 APK 哈希只保留为 P6 历史证据，不再作为未来 Android 分发候选。未来真机验收必须使用当时新建且完成受影响自动化 / AVD 回归的候选，不能为了维持旧哈希而冻结正常开发。

## 5. 下一顺位

下一顺位为 `P7-A iOS platform readiness`：先审计本机 Xcode / Simulator / CocoaPods、仓库平台目录、共享依赖与 Android 专属桥接边界；readiness 不自动授权生成 `ios/`、修改运行时代码、启动 Simulator、构建或执行真实 Smoke。
