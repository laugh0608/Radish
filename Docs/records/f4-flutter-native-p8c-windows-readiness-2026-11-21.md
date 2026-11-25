# Flutter Native P8-C Windows 只读盘点（2026-11-21）

基线为 `dev@9349e611`。已取得 UTM 配置、磁盘及快照元数据，并完成日常 Windows VM 的来宾盘点。日常 VM 已关机，Windows CleanBase 全程停止。P8-C 下一步待确认工具布局与可用容量，再按缺件申请安装授权。[日志出口评审](./unified-logging-exit-review-2026-11-21.md) · [Windows 实施与验收方案](../features/flutter-native-p8c-windows-plan.md)

## 宿主侧盘点

宿主为 Darwin arm64，UTM 4.7.5。通过 `utmctl list`、UTM AppleScript 只读 `configuration`、`plutil` 与两块系统盘的 `qemu-img info --output=json` 核对下表。两台均使用 QEMU `virt` / `aarch64`、4 vCPU、8 GiB RAM、Shared 网络，无配置端口转发。共享模式为 WebDAV，本轮未验证实际挂载。

| UTM 登记名 | 角色及最终状态 | NVMe 系统盘 |
| --- | --- | --- |
| `Windows11-ARM64` | 日常开发 VM，已停止 | 80 GiB 虚拟容量，宿主实际占用约 60.2 GiB |
| `Windows11-ARM64-CleanBase` | 干净基线，保持停止，后续从副本验收 | 80 GiB 虚拟容量，宿主实际占用约 14.2 GiB |

两块 QCOW2 的内部快照列表为空、无 backing 文件，dirty flag 为 false。两台 UTM 包内均有 EFI 和 TPM 数据，未见挂起状态文件。宿主 Data 卷首次盘点时约有 273 GiB 可用。

## 来宾版本与工具链

| 核对项 | 日常 VM 实测 | 缺口及恢复条件 |
| --- | --- | --- |
| Windows 与 C: | Windows 11 Professional 25H2，build `26200.9711`。NTFS 总计 78.86 GiB，可用 **13.29 GiB**（14,275,252,224 字节）。`Win32_Processor.Architecture=12`，与 UTM ARM64 配置一致 | 13.29 GiB 能否容纳安装峰值、Flutter 缓存和编译产物，待依据固定 layout 测算。Windows CleanBase 的 OS build 和来宾空间从未来获批的验收副本获取 |
| Git / SDK | Git `2.55.0.windows.3`，PE `0xaa64`，`git --version` 成功。Windows SDK 目录 `10.0.26100.0`，已安装组件版本 `10.1.26100.7705`，核对了 `Windows.h`、ARM64 `Kernel32.lib` / `ucrt.lib` 和 ARM64 `rc.exe` | 可复用现有 Git 和 Windows SDK，本轮尚无编译证据 |
| Visual Studio | Build Tools 2022 `17.14.39`（实例 `17.14.37614.0`），`VisualStudio.17.Release`，完整、可启动，未要求重启。已登记 `VCTools` workload、ARM64 MSVC 和 SDK 26100。MSVC 目录 `14.44.35207`，ARM64 `cl.exe` 文件版本 `19.44.35228.0`、PE `0xaa64` | x86/x64 C++ 工具、ARM64 ATL 和 CMake 项缺失；`atlbase.h`、ARM64 `atls.lib` 未找到。保留实例并补这三个组件 |
| Flutter / Dart、CMake / Ninja | Agent 的 PATH、VS 内置路径、常见安装目录与两个用户配置目录的常见工具位置中均未检出；用户目录范围为定向探测 | Flutter 3.44.0 / Dart 3.12.0 待准备，CMake 和 Ninja 随 VS CMake 项补齐后核对版本及路径 |
| ARM64 引导进程 | guest agent 调用的 Windows PowerShell 在 `PROCESSOR_ARCHITECTURE` 中报告 `AMD64`，.NET 报告 `X64`。来宾 CPU 由 WMI 确认为 ARM64，ARM64 Git、MSVC 和 SDK 也有 PE 证据 | 在目标用户会话验证可用的原生 ARM64 `pwsh.exe`、环境和 PATH；若缺件再确认 ARM64 PowerShell 制品。用实际 `dart.exe` PE 架构关闭 Flutter 误选 x64 的风险 |
| Windows 安全存储 | 锁定插件 `flutter_secure_storage_windows 4.2.2` 的本地缓存源码使用用户级 DPAPI，Credential Manager 属于兼容旧实现。偏好由 `shared_preferences_windows 2.4.1` 独立持久化 | 待 Windows 使用候选产品身份实测路径、重启、退出、不同用户及报错日志边界 |

官方 3.44.0 Windows SDK ZIP 内含 x64 Dart。固定 framework `559ffa3f75e7402d65a8def9c28389a9b2e6fe42` 的 launcher 可下载指定 engine 的 ARM64 Dart；前序盘点对 engine `4c525dac5ebe5971c5708ef73558ed8edcf4a362` 的 ARM64 Dart、Debug 和 Release engine 工件做 HEAD 均返回 200。[Win32_Processor 架构值](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-processor#properties) · [历史来宾盘点](./f4-flutter-native-p8a-desktop-platform-readiness-2026-09-05.md#4-windows-vm-事实) · [官方 Flutter 版本元数据](https://storage.googleapis.com/flutter_infra_release/releases/releases_windows.json)

## 待授权命令

**先固定 Visual Studio layout 与容量。** 在独立分期目录使用现有 `17.14.39` 对应的已验签 Build Tools bootstrapper，记录候选工件、版本与占用。若该版本工件不可达，先明确下一可用版本及对既有实例的更新影响。C: 现有 13.29 GiB，须合计 layout 存放、安装峰值、任务缓存和构建空间，按实测决定 layout 位置和是否另行处理来宾空间。

```powershell
$missingComponents = @(
  'Microsoft.VisualStudio.Component.VC.Tools.x86.x64'
  'Microsoft.VisualStudio.Component.VC.ATL.ARM64'
  'Microsoft.VisualStudio.Component.VC.CMake.Project'
)
$layoutArgs = @('--layout', $vsLayout, '--lang', 'zh-CN', '--wait')
foreach ($component in $missingComponents) { $layoutArgs += @('--add', $component) }
& $verifiedVsBootstrapper @layoutArgs
```

**再修改日常 VM 的现有实例。** 确认容量、精确 layout 补丁及日常 VM 恢复点后，才授权持久启动与运行 `modify`。组件范围限上述三项，原 ARM64 MSVC 与 SDK 复用。保留 layout 摘要、安装结果和三个组件的 `vswhere` 回读；恢复按新增组件清单或已确认备份执行。

```powershell
$vsInstallPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools'
$vsArgs = @('modify', '--installPath', $vsInstallPath, '--noWeb', '--passive', '--norestart', '--wait')
foreach ($component in $missingComponents) { $vsArgs += @('--add', $component) }
& "$vsLayout\vs_buildtools.exe" @vsArgs
```

**Flutter SDK 独立授权。** 从目标 Windows 用户执行 `Get-Command pwsh`，核对 PE、进程环境和 Flutter batch launcher 的实际解析。批准 SDK 获取后，执行 `git clone --depth 1 --branch 3.44.0 https://github.com/flutter/flutter.git "$p8cRoot\sdk\flutter"` 并核对固定 framework / engine SHA。在验证过的 ARM64 环境执行 `flutter --version`、`flutter precache --windows`、`flutter doctor -v`、`flutter devices`，再读 Dart PE 头。SDK 和缓存限任务目录，回退时按确切目录移除。runner 与 Radish 服务仍由后续批次授权。[工具批命令与验收](../features/flutter-native-p8c-windows-plan.md#2-日常-vm-工具与构建)

## 取证与关机

CLI 首次因 UTM AppleEvent 超时 `-1712`，重试出现 `-609`，本机日志有 AppKit `NSWindow.m:15387` / `_changeJustMain` 异常。切换图形入口时误启动 `Debian13-ARM64-CleanBase` 的 disposable 会话，经关机请求及随后终止该会话确认停止。将 UTM 恢复到前台后，原命令 `utmctl start Windows11-ARM64 --disposable` 成功，窗口显示“一次性模式”。Windows CleanBase 未启动。[UTM 一次性模式](https://docs.getutm.app/advanced/disposable/)

日常 Windows 通过 guest agent 运行只读探测，输出两份脱敏 JSON。辅助 `IsWow64Process2` 探针未取得返回值，来宾架构采用 WMI 与 ARM64 工具 PE 交叉核对。发出来宾临时文件清理后通过 `shutdown.exe /s /t 0` 正常关机。关机前快速回查只确认 6 条临时路径中的 1 条，其余路径随一次性会话丢弃。最终 `utmctl list` 确认两台 Windows 及误启的 Debian 均为 `stopped`。三块原系统盘最后修改时间均早于本轮，Windows 两盘的 QCOW2 分配量与盘点前一致。日常 Windows 的 UTM TPM 状态文件 `Data/tpmdata` 在关机时更新了修改时间，未对该文件内容做前后对比。工具安装、runner 生成和 Radish 服务启动均留待后续授权。

原始证据位于本机 `artifacts/flutter-p8c-windows-readiness/2026-11-21-vC8mwnfd/`，校验清单已复核。

| 证据 | SHA-256 |
| --- | --- |
| `inventory.json`， Windows build、卷、组件、文件与架构初检 | `50860ff67ad994a6ec13a66ba6219052c14852a8873648728cde35e08d02ad3f` |
| `identity.json`， WMI CPU 架构、引导环境与 VS 实例补核 | `4cefcb5d90f40fe4cfc08aba4f803f8651ad43394575b1caf8f89a63c5cfeb6b` |
