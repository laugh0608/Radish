# Tauri + WebOS 桌面安装包个人开发阶段验证记录（2026-05-05）

> 文档定位：本文属于一次性阶段评估 / 验证记录，用于保留 Tauri 桌面壳在个人开发阶段的判断事实，不作为当前默认说明书入口。
>
> 日常协作优先查看 [Guide 手册索引](/guide/)；需要历史验证事实时再结合 [记录与验收索引](/records/)。

> 状态：个人开发阶段验证通过；正式公开分发事项后置
>
> 时间：2026-05-05（Asia/Shanghai）
>
> 关联文档：
>
> - [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [React 复用路线 Capacitor / Tauri Spike 记录（2026-05-04）](/records/react-capacitor-tauri-spike-record-2026-05-04)
> - [验证基线说明](/guide/validation-baseline)
> - [专题回归索引](/guide/regression-index)

## 1. 背景结论

`Phase 2-3 Android MVP` 已完成第一轮 RC 验收，个人开发阶段暂不组织 Android 内测对象、反馈回收或测试账号体系。当前下一步更适合推进不依赖外部用户的桌面端路线判断：`Tauri 壳 + WebOS 桌面工作台`。

本轮目标不是把 Tauri 桌面壳直接切成公开发布产品线，而是确认个人开发阶段能否把 `Tauri 壳 + WebOS 桌面工作台` 作为 Windows 桌面安装包路线保留下来。判断重点收束为：能否构建 installer、能否安装、能否启动 WebOS `/desktop`、能否完成本地 Auth 登录、能否正常访问已登录功能、能否重装和卸载。

## 2. 当前已具备

- `Clients/radish-tauri` 已建立 Tauri v2 平铺工程结构，配置文件位于 `Clients/radish-tauri/tauri.conf.json`
- Tauri 默认 UI 入口已切到 `/desktop`，由 WebOS 桌面工作台承载，不再以 `/docs` 公开阅读页作为桌面产品样例
- 系统浏览器登录 / 登出优先使用 `http://127.0.0.1:48801` loopback 回跳，`radish://` deep link 仅保留为兼容路径
- 首轮命令级验证与第二轮人工验收已覆盖：`radish.client` 构建、`cargo build`、`cargo build --release`、GUI 启动、WebOS 布局、窗口生命周期观察、登录 / 登出回跳
- 当前本机已安装 `cargo-tauri` CLI，`cargo tauri --version` 返回 `tauri-cli 2.11.0`
- `cargo tauri build` 已生成 Windows NSIS installer：`Clients/radish-tauri/target/release/bundle/nsis/Radish Tauri Spike_0.1.0_x64-setup.exe`
- installer 已完成一轮本机安装、启动、普通用户卸载与同身份覆盖安装验证：可安装、可打开 WebOS `/desktop`、普通用户卸载无残留、同身份覆盖安装后仍可启动；修复 Windows release 启动伴随命令行窗口的问题后，启动不再出现额外控制台窗口
- 当前已完成正式桌面包候选身份补验，`tauri.conf.json` 的 `productName` / 窗口标题已切为 `Radish`，`identifier` 已切为 `com.radish.desktop`
- 正式候选身份下已通过命令级构建并生成 Windows NSIS installer：`Clients/radish-tauri/target/release/bundle/nsis/Radish_0.1.0_x64-setup.exe`
- 正式候选身份下已完成人工安装、登录、已登录功能访问、关闭浏览器后重试登录、覆盖安装和卸载验证，暂未发现问题
- 正式候选包生产构建默认基址已从 `https://your-domain.com` 占位收口到 `https://radishx.com`
- 已新增本地 Auth 验收构建模式：`npm run build:tauri-local --workspace=radish.client` 使用 `Frontend/radish.client/.env.tauri-local`，将 Gateway / Auth / SignalR 基址固定到 `https://localhost:5000`，用于在生产 Auth 客户端注册暂未更新时继续验证 Tauri loopback 登录
- 浏览器登录页被关闭后，同一路径的后续登录点击会复用等待中的 `127.0.0.1:48801` loopback listener，避免 5 分钟等待窗口内再次点击登录因端口占用失败

## 3. 当前调整边界

- 已将 `tauri.conf.json` 中的 `productName`、窗口标题与 `identifier` 切到正式桌面包候选身份
- 本次身份切换只用于验证安装目录、卸载项、同身份覆盖安装和 `radish://` 协议注册清理，不等同于公开发布版
- 不引入自动更新、公钥、发布源或签名配置
- 不新增托盘、菜单、后台驻留或文件系统能力
- 不继续安装新的 Tauri 依赖或引入新的原生能力
- 本地 Auth 验收包只用于本机联调，不改变公开分发口径；个人开发阶段接受测试环境通过即可收口，生产 Auth redirect URI、签名、自动更新与公开下载分发不进入当前批次

此前保留 spike 身份的原因是：当时只完成 Windows NSIS installer 首轮验证，正式候选身份下的安装、登录、重装、卸载与协议注册清理还没有跑完。当前 `Radish` / `com.radish.desktop` 已完成候选身份人工补验，本轮即可按个人开发阶段通过收口；自动更新、签名与公开分发不作为当前开发主线投入项。

## 4. 验证清单

| 项目 | 当前状态 | 后续口径 | 判断口径 |
| --- | --- | --- | --- |
| Windows release exe | 已通过 `cargo build --release` 生成 | `cargo build --release` | 只能说明 Rust / Tauri 壳可编译，不代表 installer 可分发 |
| Windows installer bundle | 第二轮候选身份人工补验通过 | `cargo tauri build` | Spike 身份已完成本机安装 / 启动 / 普通用户卸载 / 同身份覆盖安装验证；`Radish` 候选身份已完成安装 / 登录 / 功能访问 / 覆盖安装 / 卸载验证 |
| 产品身份 | 已切正式候选身份并完成人工补验 | `Radish` / `com.radish.desktop` installer 补验 | 只验证候选桌面包身份，不等同公开发布 |
| 代码签名 | 不进入当前批次 | 正式公开分发前再评估 | 个人开发阶段不为签名消耗主线精力 |
| Windows SmartScreen | 本机未复现拦截 | 正式公开分发前再复核 | 当前普通用户安装未出现“未知发布者 / SmartScreen”提示；该项不阻塞个人开发阶段继续推进 |
| 自动更新 | 不进入当前批次 | 正式公开分发或版本分发压力出现后再评估 | 个人开发阶段可通过重新下载安装包覆盖安装解决 |
| 托盘 / 菜单 | 未接入 | 有明确桌面工作流需求时再设计 | 不作为安装包路线成立前置 |
| 外部链接 | 已接入 opener 能力 | 人工确认外链走系统浏览器 | 登录与外链都应优先复用系统浏览器 |
| deep link | 候选身份人工补验通过 | 后续只在相关功能变更时复核 | loopback 是桌面登录主路径，deep link 是兼容路径；本轮人工反馈未发现协议注册、卸载清理或旧 Spike 抢占问题 |
| installer 体积 | 已记录首轮与候选身份 installer 体积 | 后续构建显著变化时再记录 | Spike 身份 NSIS installer 约 `2.54 MiB`；`Radish` 候选身份 installer 为 `2,666,972 bytes`，约 `2.54 MiB` |

## 5. 后续可执行命令

当前已可继续复用的命令：

```powershell
npm run type-check --workspace=radish.client
npm run test --workspace=radish.client
npm run build --workspace=radish.client
npm run build:tauri-local --workspace=radish.client

cd Clients/radish-tauri
cargo build
cargo build --release
```

当前本机已确认 `cargo-tauri` CLI 可用。若其他环境尚未安装，需先由开发者手动安装 Tauri CLI：

```powershell
cargo install tauri-cli --locked
```

随后执行：

```powershell
cd Clients/radish-tauri
cargo tauri build
```

说明：安装 Tauri CLI 属于依赖安装动作，应由开发者手动执行或明确批准后再执行。当前本机已经完成安装，并已执行 `cargo tauri build` 生成 NSIS installer；个人开发阶段不继续追签名、自动更新或正式公开分发结论。

## 6. 首轮 installer 验证结果

- 命令：`cargo tauri build`
- 结果：通过
- installer：`Clients/radish-tauri/target/release/bundle/nsis/Radish Tauri Spike_0.1.0_x64-setup.exe`
- installer 大小：`2,661,263 bytes`，约 `2.54 MiB`
- 安装验证：通过
- 启动验证：通过，可打开 WebOS `/desktop`
- 普通用户卸载验证：通过，无残留
- 同身份覆盖安装验证：通过，覆盖安装后仍可启动 `/desktop`，且不出现额外命令行窗口
- 权限上下文备注：管理员权限安装后再用普通权限卸载，曾出现 `radish-tauri.exe` / `uninstall.exe` 残留；改用普通用户安装和普通用户卸载后无残留。该现象归类为安装 / 卸载权限上下文不一致风险，不作为当前 Tauri / NSIS 配置缺陷处理
- 控制台窗口：已通过 `windows_subsystem = "windows"` 修复，release / installer 启动不再伴随命令行窗口
- SmartScreen / 未签名提示：本机普通用户安装未出现“未知发布者 / SmartScreen”提示；该结论仅代表当前本机安装链路，正式公开分发前再按下载来源、签名、信誉与系统策略重新复核
- 仍待验证：无个人开发阶段阻断项；代码签名、自动更新与公开分发不进入当前批次
- 构建备注：当前 `cargo tauri build` 出现 `__TAURI_BUNDLE_TYPE variable not found in binary` 警告；项目尚未接入 updater，因此不影响本轮 installer 和启动验证。若后续接入自动更新，需要单独处理该警告。

## 6.1 正式候选身份命令级结果

- 身份：`productName = Radish`，`identifier = com.radish.desktop`
- 默认 Gateway / Auth / SignalR 基址：`https://radishx.com`
- 命令：`npm run type-check --workspace=radish.client`、`npm run test --workspace=radish.client`、`npm run build --workspace=radish.client`、`cargo build`、`cargo build --release`、`cargo tauri build`
- 结果：通过
- installer：`Clients/radish-tauri/target/release/bundle/nsis/Radish_0.1.0_x64-setup.exe`
- installer 大小：`2,666,972 bytes`，约 `2.54 MiB`
- 备注：`npm run build --workspace=radish.client` 在沙盒内曾因 `spawn EPERM` 失败，提权重跑后通过；`cargo tauri build` 在沙盒内曾因 NSIS 下载 / socket 权限失败，提权重跑后通过
- 人工补验：旧 `Radish Tauri Spike` 卸载、`Radish` 候选 installer 安装、启动 `/desktop`、登录 / 登出 loopback、关闭浏览器后再次点击登录、普通用户卸载、同身份覆盖安装、`radish://` 协议注册与卸载清理均未发现问题

## 6.2 本地 Auth 验收包

- 用途：生产 `radishx.com` 的 `radish-client` 尚未登记 `http://127.0.0.1:48801/oidc/callback` 时，先使用本机 Gateway / Auth 验证 Tauri loopback 登录链路
- 前端构建命令：`npm run build:tauri-local --workspace=radish.client`
- 环境文件：`Frontend/radish.client/.env.tauri-local`
- 默认 Gateway / Auth / SignalR 基址：`https://localhost:5000`
- 后续 installer 命令：`cd Clients/radish-tauri` 后执行 `cargo tauri build`
- 本轮结果：已通过 `npm run build:tauri-local --workspace=radish.client` 与 `cargo tauri build` 生成本地 Auth 验收 installer：`Clients/radish-tauri/target/release/bundle/nsis/Radish_0.1.0_x64-setup.exe`
- installer 大小：`2,666,723 bytes`，约 `2.54 MiB`
- dist 核对：`Frontend/radish.client/dist` 中未发现 `https://radishx.com` 或 `https://your-domain.com`，可检索到 `https://localhost:5000`
- 使用前置：本机需运行 Gateway `https://localhost:5000`、Auth `http://localhost:5200` 与 API `http://localhost:5100`；启动服务命令由开发者手动执行
- 判断边界：该包只用于本机登录 / 登出 / 安装包交互验收，不作为公开分发候选；个人开发阶段接受测试环境通过即可收口

## 6.3 正式候选身份人工补验记录

- 验收包：`Clients/radish-tauri/target/release/bundle/nsis/Radish_0.1.0_x64-setup.exe`
- 身份：`Radish` / `com.radish.desktop`
- 安装验证：通过，安装过程未发现问题
- 安装身份验证：通过，安装目录、开始菜单 / 快捷方式与卸载项均按 `Radish` 候选身份验收，未发现异常
- 启动与访问验证：通过，可正常启动 WebOS 桌面并访问功能
- 登录验证：通过，系统浏览器 loopback 登录可完成，客户端可进入已登录状态
- 关闭浏览器后重试登录：通过，关闭登录浏览器后再次发起登录未发现问题
- 覆盖安装验证：通过，重新安装未发现问题
- 卸载验证：通过，卸载未发现问题
- `radish://` 协议注册与卸载清理：通过，未发现协议注册、卸载清理或旧 `Radish Tauri Spike` 抢占问题
- 结论：`Radish` / `com.radish.desktop` 正式候选身份下，Windows NSIS installer 的安装、登录、已登录功能访问、覆盖安装、卸载与 deep link 清理人工补验通过；Tauri + WebOS 桌面安装包路线在个人开发阶段验证通过

## 7. 个人开发阶段收口判断

个人开发阶段若要标记为完成，至少需要补齐：

1. `cargo tauri build` 可生成 Windows installer bundle
2. 记录 installer 路径、文件大小、安装结果、卸载结果与是否能启动 WebOS `/desktop`
3. 测试环境下登录、已登录功能访问、关闭浏览器后重试登录通过
4. 正式候选身份下确认安装目录、开始菜单 / 快捷方式、卸载项均显示 `Radish`
5. 正式候选身份下确认 `radish://` 协议注册指向 `Radish`，卸载后协议注册被清理，旧 `Radish Tauri Spike` 不再抢占协议

当前第 1 至第 5 项已完成。Spike 身份安装 / 启动 / 普通用户卸载 / 同身份覆盖安装验证、候选身份 installer 生成、installer 体积、本机 SmartScreen 观察，以及 `Radish` 候选身份的安装 / 登录 / 功能访问 / 覆盖安装 / 卸载 / deep link 协议注册清理人工补验均已完成。代码签名、自动更新、托盘 / 菜单取舍与公开分发链路不进入当前主线。

## 8. 当前建议

个人开发阶段，`Tauri 壳 + WebOS 桌面工作台` 的 Windows installer 与正式候选身份补验已经通过：`Radish` / `com.radish.desktop` 可完成安装、启动、登录、已登录功能访问、覆盖安装、卸载和 `radish://` 协议注册清理。后续不再继续消耗主线精力处理签名、自动更新、SmartScreen、生产 Auth 或公开发布细节；除非准备真实对外分发，否则桌面安装包路线先转为已验证能力，开发精力回到产品功能推进。
