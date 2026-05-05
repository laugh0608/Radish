# Tauri + WebOS 桌面安装包第二轮分发评估清单（2026-05-05）

> 状态：Windows installer 首轮通过，签名 / 自动更新待后续评估
>
> 时间：2026-05-05（Asia/Shanghai）
>
> 关联文档：
>
> - [多端客户端路线评估方案](/planning/multiplatform-client-route-evaluation)
> - [前端多壳层策略](/frontend/shell-strategy)
> - [React 复用路线 Capacitor / Tauri Spike 记录（2026-05-04）](/guide/react-capacitor-tauri-spike-record-2026-05-04)
> - [验证基线说明](/guide/validation-baseline)
> - [专题回归索引](/guide/regression-index)

## 1. 背景结论

`Phase 2-3 Android MVP` 已完成第一轮 RC 验收，个人开发阶段暂不组织 Android 内测对象、反馈回收或测试账号体系。当前下一步更适合推进不依赖外部用户的桌面端路线判断：`Tauri 壳 + WebOS 桌面工作台`。

本轮目标不是把 Tauri 桌面壳直接切成正式产品线，而是补齐桌面分发链路的判断入口，明确后续需要验证的 installer、代码签名、自动更新、Windows SmartScreen、托盘、菜单与正式分发体积。

## 2. 当前已具备

- `Clients/radish-tauri` 已建立 Tauri v2 平铺工程结构，配置文件位于 `Clients/radish-tauri/tauri.conf.json`
- Tauri 默认 UI 入口已切到 `/desktop`，由 WebOS 桌面工作台承载，不再以 `/docs` 公开阅读页作为桌面产品样例
- 系统浏览器登录 / 登出优先使用 `http://127.0.0.1:48801` loopback 回跳，`radish://` deep link 仅保留为兼容路径
- 首轮命令级验证与第二轮人工验收已覆盖：`radish.client` 构建、`cargo build`、`cargo build --release`、GUI 启动、WebOS 布局、窗口生命周期观察、登录 / 登出回跳
- 当前本机已安装 `cargo-tauri` CLI，`cargo tauri --version` 返回 `tauri-cli 2.11.0`
- `cargo tauri build` 已生成 Windows NSIS installer：`Clients/radish-tauri/target/release/bundle/nsis/Radish Tauri Spike_0.1.0_x64-setup.exe`
- installer 已完成一轮本机安装、启动、普通用户卸载与同身份覆盖安装验证：可安装、可打开 WebOS `/desktop`、普通用户卸载无残留、同身份覆盖安装后仍可启动；修复 Windows release 启动伴随命令行窗口的问题后，启动不再出现额外控制台窗口

## 3. 当前不调整

- 不修改 `tauri.conf.json` 中的 `productName`、窗口标题或 `identifier`
- 不把 `Radish Tauri Spike` / `com.radish.tauri.spike` 提前改成正式桌面包身份
- 不引入自动更新、公钥、发布源或签名配置
- 不新增托盘、菜单、后台驻留或文件系统能力
- 不继续安装新的 Tauri 依赖或引入新的原生能力

保留 spike 身份的原因是：当前只完成 Windows NSIS installer 首轮验证，签名、升级、deep link 协议注册、卸载细节与正式分发身份仍未完整收口，过早切换正式包身份会影响系统安装记录、协议注册和后续升级判断。

## 4. 分发评估清单

| 项目 | 当前状态 | 后续验证入口 | 判断口径 |
| --- | --- | --- | --- |
| Windows release exe | 已通过 `cargo build --release` 生成 | `cargo build --release` | 只能说明 Rust / Tauri 壳可编译，不代表 installer 可分发 |
| Windows installer bundle | 首轮通过 | `cargo tauri build` | 已生成 NSIS installer，并完成本机安装 / 启动 / 普通用户卸载 / 同身份覆盖安装验证 |
| 产品身份 | 仍为 spike | 修改 `productName` / `identifier` 前单独评审 | installer 真实可用前不切正式身份 |
| 代码签名 | 未验证 | 准备证书后验证签名产物 | 个人开发阶段可暂缓；外部分发前必须明确未签名风险 |
| Windows SmartScreen | 本机未复现拦截 | 未签名 / 已签名 installer 分别人工安装 | 当前普通用户安装未出现“未知发布者 / SmartScreen”提示；公开分发后仍需按下载来源、签名、信誉与系统策略重新确认 |
| 自动更新 | 未接入 | 单独评估 Tauri updater、发布源、公钥和回滚策略 | 当前小产品阶段可后置，不作为桌面路线成立前置 |
| 托盘 / 菜单 | 未接入 | 单独设计最小菜单、退出语义和后台驻留边界 | 不应在分发未跑通前扩成交互功能建设 |
| 外部链接 | 已接入 opener 能力 | 人工确认外链走系统浏览器 | 登录与外链都应优先复用系统浏览器 |
| deep link | 已保留 `radish://` 兼容路径 | installer 后补协议注册验证 | loopback 是桌面登录主路径，deep link 是兼容路径 |
| 正式分发体积 | 已记录首轮 installer 体积 | 生成 installer 后记录文件大小 | 当前 NSIS installer 约 `2.54 MiB`；后续正式身份、签名或 updater 接入后需重新记录 |

## 5. 后续可执行命令

当前已可继续复用的命令：

```powershell
npm run type-check --workspace=radish.client
npm run test --workspace=radish.client
npm run build --workspace=radish.client

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

说明：安装 Tauri CLI 属于依赖安装动作，应由开发者手动执行或明确批准后再执行。当前本机已经完成安装，并已执行 `cargo tauri build` 生成 NSIS installer；本轮仍不能给出代码签名、deep link 协议注册或自动更新结论。

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
- SmartScreen / 未签名提示：本机普通用户安装未出现“未知发布者 / SmartScreen”提示；该结论仅代表当前本机安装链路，公开分发后仍需结合下载来源、签名、信誉与系统策略重新复核
- 仍待验证：代码签名、deep link 协议注册、自动更新
- 构建备注：当前 `cargo tauri build` 出现 `__TAURI_BUNDLE_TYPE variable not found in binary` 警告；项目尚未接入 updater，因此不影响本轮 installer 和启动验证。若后续接入自动更新，需要单独处理该警告。

## 7. 第二轮通过标准

第二轮分发评估若要标记为完成，至少需要补齐：

1. `cargo tauri build` 可生成 Windows installer bundle
2. 记录 installer 路径、文件大小、安装结果、卸载结果与是否能启动 WebOS `/desktop`
3. 未签名状态下记录 Windows SmartScreen / 安装告警表现
4. 明确代码签名是否暂缓，以及暂缓对个人分发和公开发布的影响
5. 明确自动更新是否后置，以及后置时桌面包如何提示版本更新
6. 明确托盘 / 菜单是否进入首批桌面包，不进入时保持普通窗口应用语义

当前第 1 项、安装 / 启动 / 普通用户卸载 / 同身份覆盖安装验证、installer 体积和本机 SmartScreen 观察已完成；签名、自动更新、deep link 协议注册与托盘 / 菜单取舍仍待后续补验。

## 8. 当前建议

个人开发阶段建议先保持 Tauri 桌面壳为候选路线，不急于切正式产品身份。下一次若继续桌面端，应进入正式产品身份评估，再决定是否改 `productName`、`identifier`、代码签名、自动更新和托盘 / 菜单。
