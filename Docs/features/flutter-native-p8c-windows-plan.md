# Flutter Native P8-C Windows 实施与验收方案

状态为待授权。目标是 Windows 11 ARM64 本地平台基础与运行验收。日常 `Windows11-ARM64` 承担工具、构建和第一轮运行，`Windows11-ARM64-CleanBase` 作为候选验收副本的来源。Git 基线为 `dev@9349e611`。[本轮盘点](../records/f4-flutter-native-p8c-windows-readiness-2026-11-21.md) · [Native 专题](./flutter-native-product-ui-design.md)

## 1. 日常来宾只读复核

获准后从宿主运行 `utmctl start Windows11-ARM64 --disposable`。优先从 UTM 控制台在来宾的原生 PowerShell 中查询。若 QEMU guest agent 可用，也可用 `utmctl exec` 运行同一清单，仅将版本、组件、架构和容量写入任务临时报告，用 `utmctl file pull` 取回并精确删除来宾文件。结束时正常关闭来宾，再运行 `utmctl status Windows11-ARM64`。原 CleanBase 继续停止。

```powershell
Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' |
  Select-Object EditionID, DisplayVersion, CurrentBuild, UBR
[System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
[System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
Get-Volume -DriveLetter C | Select-Object Size, SizeRemaining
Get-Command git, flutter, dart, cmake, ninja, cl -ErrorAction SilentlyContinue |
  Select-Object Name, Source, Version
git --version
$programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
$vswhere = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
$vsComponents = @(
  'Microsoft.VisualStudio.Workload.VCTools'
  'Microsoft.VisualStudio.Component.VC.Tools.x86.x64'
  'Microsoft.VisualStudio.Component.VC.Tools.ARM64'
  'Microsoft.VisualStudio.Component.VC.ATL.ARM64'
  'Microsoft.VisualStudio.Component.VC.CMake.Project'
  'Microsoft.VisualStudio.Component.Windows11SDK.26100'
)
if (Test-Path $vswhere) {
  & $vswhere -products '*' -version '[17.0,18.0)' -format json
  & $vswhere -products '*' -version '[17.0,18.0)' -requires @vsComponents -format json
}
Get-ChildItem (Join-Path $programFilesX86 'Windows Kits\10\Include') -Name
```

在实际 VS 实例下核对 ARM64 `cl.exe`、`atlmfc\include\atlbase.h` 和 `atlmfc\lib\arm64`，查询 VS 自带 CMake 和 Ninja 的版本。Windows SDK 同查 `Lib\10.0.26100.0` 下的 `um\arm64` 和 `ucrt\arm64`。存在 Flutter 时读取 `bin\cache\flutter.version.json`、`bin\cache\dart-sdk\version` 及 `dart.exe` 的 PE 头。可能写缓存的 `flutter doctor -v` 和 `flutter devices` 放在工具批执行。若 guest agent 和控制台均不可用，恢复条件为可进入原生 PowerShell。若空间不足，记录 C: 可用量和安装估算，待腾出足够空间或单独批准扩容。

## 2. 日常 VM 工具与构建

工具安装、工程改动和运行分别确认范围。复用现有 Git、Build Tools 2022 17.14.39、ARM64 MSVC 与 Windows SDK 26100。Flutter doctor 识别需要补齐 x86/x64 C++ 和 CMake 项，原生插件编译另需 ARM64 ATL。先根据固定 layout 核算 C: 现有 13.29 GiB 能否覆盖安装与构建，再确认日常 VM 的恢复方案、layout 精确版本及是否需重启。两盘当前没有内部快照。[实际缺件与命令](../records/f4-flutter-native-p8c-windows-readiness-2026-11-21.md#待授权命令) · [VS Build Tools 组件](https://learn.microsoft.com/en-us/visualstudio/install/workload-component-id-vs-build-tools?preserve-view=true&view=vs-2022)

```powershell
# $missingComponents 取自只读预检确认的组件缺口
$vsArgs = @('modify', '--installPath', $vsInstallPath, '--noWeb', '--passive', '--norestart', '--wait')
foreach ($component in $missingComponents) { $vsArgs += @('--add', $component) }
& "$vsLayout\vs_buildtools.exe" @vsArgs
```

Flutter 固定 3.44.0 / Dart 3.12.0。该版官方 Windows 预打包 ZIP 内含 x64 Dart，Windows build 以运行 Dart 的 ABI 选目标。通过原生 ARM64 PowerShell 引导固定 Flutter tag，并实测 ABI。ARM64 Dart、Debug / Release engine 端点估算分别约 150、32、63 MiB，工具布局、Git、其他缓存和构建占用随后按实际预检计算。环境修改限任务进程和目录。[Flutter Windows 工具要求](https://docs.flutter.dev/platform-integration/windows/setup)

本轮 guest agent 调用的 Windows PowerShell 报告 `AMD64`，Flutter 引导批应在目标用户会话核对 `Get-Command pwsh`、`pwsh.exe` 的 ARM64 PE 与进程环境。Flutter 3.44.0 launcher 优先从 PATH 选 `pwsh.exe`。以引导后的 Dart PE `0xaa64` 为门禁。

`$p8cRoot` 使用任务用户的 `%LOCALAPPDATA%\Radish-P8C\<runId>`。宿主以 `git archive --format=zip --output=<本轮源码包> <已确认源码 SHA>` 传输已冻结源码，来宾展开到 `src`，用源码 SHA 和 zip 哈希绑定构建输入。

```powershell
git clone --depth 1 --branch 3.44.0 https://github.com/flutter/flutter.git "$p8cRoot\sdk\flutter"
git -C "$p8cRoot\sdk\flutter" rev-parse HEAD
$env:PATH = "$p8cRoot\sdk\flutter\bin;$env:PATH"
$env:PUB_CACHE = "$p8cRoot\pub-cache"
flutter --version
flutter precache --windows
flutter doctor -v
flutter devices

Set-Location "$p8cRoot\src\Clients\radish.flutter"
flutter create --platforms=windows --no-pub .
node tool/prepare-serif-font.mjs
flutter pub get --enforce-lockfile
flutter analyze --no-pub
flutter test --no-pub
flutter build windows --debug --no-pub
$candidateDefines = @(
  '--dart-define=RADISH_ENVIRONMENT=development'
  '--dart-define=RADISH_GATEWAY_BASE_URL=https://localhost:5000'
  '--dart-define=RADISH_ALLOW_LOCAL_DEVELOPMENT_CERTIFICATES=false'
)
flutter build windows --release --no-pub @candidateDefines
```

核对 framework `559ffa3f75e7402d65a8def9c28389a9b2e6fe42`、engine `4c525dac5ebe5971c5708ef73558ed8edcf4a362`，用 VS `dumpbin /headers` 核对 Dart、runner、Flutter 及原生插件 DLL 为 ARM64。运行候选取 `build\windows\arm64\runner\Release` 完整目录，用 `Get-FileHash -Algorithm SHA256` 生成逐件及归档清单，关联源码 SHA、编译参数与工具版本。若 Flutter 回退 x64、锁文件漂移、ARM64 ATL 缺失或发现随包运行依赖缺口，记录失败依据，待缺口关闭后再给 ARM64 结论。生成 runner 后审阅 diff，仓库范围为 Windows runner、所需共享平台接线及验证。

## 3. 平台契约

| 面向 | 待实施方案 |
| --- | --- |
| Runner 与产品身份 | 使用标准 Win32 runner / CMake / 插件注册，显式接入 Windows platform services。窗口和产品名 `Radish`，EXE 为 `Radish.exe`，AppUserModelID 为 `com.radish.client`。VERSIONINFO 固定 `CompanyName=Radish`、`ProductName=Radish`，版本从 pubspec `26.8.2+1` 派生，已有品牌图标生成多规格 ICO。 |
| OIDC | 沿用 `radish-client`、PKCE S256、`radish://oidc/callback` 与 `radish://oidc/logout-complete`。通过 Win32 Shell API 打开系统浏览器，只接受既定 HTTP(S) 授权和登出 URL。协议 handler 限制 URI 长度，核对 scheme、authority、路径和参数结构，拒绝重复关键参数。Dart 继续校验 attempt、state、时限、PKCE 和一次性消费。 |
| 冷启动 / 热回调 | runner 持有单窗口。冷启动暂存经过校验的 callback，待 method channel 就绪后交付。热回调通过当前登录用户及会话限定的本机单实例 IPC，传给现有实例并通知 Dart 消费、激活窗口。验证启动竞态、最小化、退出期间回调及重放。授权 URI、code、token、verifier 和完整启动参数均属于敏感数据。 |
| 持久化 | 使用锁定插件的用户级 DPAPI，显式 `WindowsOptions(useBackwardCompatibility: false)`。session 和 OIDC attempt 分键，显式退出清理对应键。密文预期位于 `%APPDATA%\Radish\Radish\flutter_secure_storage.dat`，以实际产品资源和运行证据复核。Credential Manager 与旧 `.secure` 文件属于兼容实现，本批无 Windows 历史状态迁移。非敏感复访、主题和窗口几何使用 preferences 边界。用合成坏文件副本验证认证失败、插件可能移除坏文件和本地报错可见范围。 |
| 窗口与输入 | 建议初始客户区 1280 × 800、最小 390 × 600 DIP，支持 Per-Monitor DPI，窗口恢复钳制在可用工作区。跨过 600 / 1024 DIP 复核导航、滚动和当前编辑状态。验证最大化、最小化、任务栏恢复、系统关闭、键鼠和中文 IME。 |
| 关闭与再启 | X / Alt+F4 保存非敏感窗口信息、释放 IPC 并使进程退出，保留安全会话。显式退出账号清理本地会话与待授权状态，浏览器登出取消 / 失败后继续保持访客。分别验证 app 和 guest 重启、跨重启会话恢复与冷 callback。 |

候选注册限任务本地 Windows 用户。预检 `HKCU\Software\Classes\radish`、合并视图 `HKCR\radish` 与当前首选处理器。没有冲突时创建本轮 key，默认值为 `URL:Radish Protocol`，`URL Protocol` 置空，`shell\open\command` 为 `"<候选完整路径>\Radish.exe" "%1"`。遇现有处理器则停止该用户的注册并改由获批隔离用户执行。以真实浏览器验证实际命中完整候选和单次消费。[原生 OAuth](https://datatracker.ietf.org/doc/html/rfc8252)

## 4. 隔离服务与 CleanBase 候选

日常 VM 先验证，再通过 `utmctl clone Windows11-ARM64-CleanBase --name "Radish-P8C-<runId>"` 建一次性副本，按新 UUID 操作。副本起机后先核对来宾 OS、用户、空间及工具基线。通过 `utmctl file push <VM UUID> '<来宾文件>' < '<本机文件>'` 传入冻结候选、测试后端与必要脚本，并在来宾重算哈希。副本只运行制品。如果缺 VC++ ARM64 runtime，先确认实际依赖、正式来源、版本和安装影响，再申请补件。

采用同一来宾内的隔离服务，三个回环端点为 Gateway `https://localhost:5000`、API `http://localhost:5100`、Auth `http://localhost:5200`。宿主使用已安装的 SDK `10.0.300` 从同一源码发布 `win-arm64` 自包含后端，CleanBase 副本无需 SDK。先冻结所需 Windows runtime pack 版本、缓存缺件、来源和下载体积，再给 restore 单独授权。校验原生 `e_sqlite3.dll` 的 RID、SHA 和架构。包不可达时等待指定源或已有制品。

```bash
for app in DbMigrate Auth Api Gateway; do
  dotnet restore "Radish.$app/Radish.$app.csproj" -r win-arm64 -p:SelfContained=true
  dotnet publish "Radish.$app/Radish.$app.csproj" -c Release -r win-arm64 --self-contained true --no-restore -o "$p8c_stage/Radish.$app"
done
```

测试根目录含 `Radish.slnx` 路径标记、四个产物目录与共享配置。所有数据库、证书、日志及上传指向独占任务目录，给 API 和 Auth 分别配置绝对 `DataProtection__KeysPath`，API 的 `FileStorage__Local__BasePath` 同样指向本轮目录。通过 `New-SelfSignedCertificate`、`Export-PfxCertificate`、`Export-Certificate`、`Import-Certificate` 制作短有效期 localhost TLS 证书，公钥只信任于任务用户 `CurrentUser\Root`，记录精确 thumbprint。数据影响限本轮六库及开发夹具，禁止连接生产或现有用户库、第三方通知和真实账号。

获准运行后，以 `Get-NetTCPConnection` 核对三个端口空闲。各宿主用自己的产物目录与 `ASPNETCORE_ENVIRONMENT=Development` 启动。Gateway 通过 `Kestrel__Certificates__Default__Path` 及对应密码配置绑定 TLS。API / Auth 注入同一 `OpenIddict__Encryption__SigningCertificatePath`，Auth 另配 `OpenIddict__Encryption__EncryptionCertificatePath`。两者用本轮专用开发 PFX 及一次性密码，与 TLS 信任条目分别登记。统一将 `RADISH_PUBLIC_URL`、`GatewayService__PublicUrl`、`OpenIddict__Server__Issuer` 和 Dart Gateway 设为 `https://localhost:5000`，命中 API 的共享证书验签路径。运行按 PID 登记，核查监听、OIDC issuer / endpoints 与证书。先以 `doctor` 和绝对路径复核六库归属，再为 DbMigrate 注入 `RadishDeployment__Stage=local`、`Seed__DeveloperDefaultsEnabled=true`，执行下面的写入和后续核验。

```powershell
& "$p8cStage\Radish.DbMigrate\Radish.DbMigrate.exe" doctor
& "$p8cStage\Radish.DbMigrate\Radish.DbMigrate.exe" apply
& "$p8cStage\Radish.DbMigrate\Radish.DbMigrate.exe" verify
# 分别在所属目录和对应环境启动
& "$p8cStage\Radish.Auth\Radish.Auth.exe" --urls http://localhost:5200
& "$p8cStage\Radish.Api\Radish.Api.exe" --urls http://localhost:5100
& "$p8cStage\Radish.Gateway\Radish.Gateway.exe" --urls https://localhost:5000
& "$p8cCandidate\Radish.exe"
```

## 5. 判据、证据和回收

| 门禁 | 通过条件与证据 |
| --- | --- |
| 工程与干净来宾 | 日常 VM 的 lockfile、analyze、Dart / runner 回归和 Debug / Release 通过。候选目录逐件校验，PE 架构与版本资源匹配，CleanBase 副本可运行完整独立候选。 |
| OIDC 与隔离 | 实测公开请求、TLS 不信任失败与恢复、浏览器登录和私域读取。冷启动、已运行、最小化各验证回跳，错误 state、缺 attempt、非法路径、重放和取消保持 fail-closed。显式退出后 app 重启仍为访客。留脱敏状态码、布尔断言及截图。 |
| 安全存储 | 同一用户关 app 和重启 VM 后恢复会话，冷启动只消费一次 attempt。第二任务用户启动为访客，并用合成密文副本复核跨用户不可读。核查本地日志及 preferences 无敏感标记，只保留路径、键存在状态和清理断言。 |
| 窗口与输入 | compact / medium / expanded 各记录实际 DIP 与 DPI，覆盖四主题代表面、Ctrl+1–5、连续 Tab / Shift+Tab、方向键、Enter / Space / Esc、hover、滚轮、scrollbar、焦点与中文 IME。保留代表截图、窗口和偏好恢复断言、退出后的进程检查。 |

回收时先关闭候选和该轮 Gateway、Auth、API，核验登记 PID 和三个端口；只关闭本轮浏览器页或专用 profile。核对当前协议 handler 仍指向本轮候选，再删除本轮创建的 key。用退出流程核实两个安全键已清理，再移除已登记测试用户的应用数据与 profile。按 thumbprint 移除 `CurrentUser\Root` / `CurrentUser\My` 的 TLS 项；删除 Auth 测试材料、一次性 PFX、六库和 WAL、Data Protection、日志、传输包与本轮任务目录。获批日常工具可留用，回退按清单移除本轮 SDK / cache 和确切新增 VS 组件，或恢复事先确认的日常 VM 备份。验收副本只按标记的 UUID 与路径回收，删除应包含在该轮授权中。宿主保留脱敏报告、截图、哈希和清理断言。

工程与运行两道门禁均通过才记录 `P8-C Windows local runtime Go`。签名、其他架构、安装包和分发分开验收。P8-D、P8-E 与客户端连接沿原顺位。
