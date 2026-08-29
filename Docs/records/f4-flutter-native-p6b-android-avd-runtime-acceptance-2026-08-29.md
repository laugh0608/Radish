# Flutter Native P6-B Android AVD 运行态验收（第一轮）

> 状态：第一轮取证与契约修正已完成；P6-B 门禁未关闭
>
> 日期：2026-08-29（Asia/Shanghai）
>
> 前置记录：[P6-A Android 本地 RC 候选装配](/records/f4-flutter-native-p6a-android-local-rc-candidate-assembly-2026-08-27)

## 1. 结论

P6-B 第一轮在 API 35 compact phone 与临时 medium tablet AVD 上完成真实服务、APK 安装、公开读取、OIDC、资料与 Forum 代表写入取证，并暴露出五处不能靠重试或人工备注绕过的运行时契约缺口：Flutter API 请求没有统一续签并对单次 `401` 重试、公开资料仍调用旧私有路径、作者编辑未携带内容 revision、轻回应前端上限与服务端 `10` 字不一致，以及地址空字符串无法表达“清空”。本轮已按项目现有 owner 修正，并为相关契约补齐自动化覆盖。

修正后重新构建的 APK 在 compact AVD 上通过会话恢复、地址清空、帖子正文 CAS 更新和恰好 `10` 字轻回应；medium AVD 通过匿名公开内容与宽屏无溢出取证，但在本地开发证书中断后的外部浏览器 OIDC 回流中仍出现“找不到对应的登录尝试，或登录尝试已经过期”。因此本轮结论是 **P6-B No-Go / 继续修复**，不能继承 P6-A 旧哈希，也不能进入 P6-C 真机验收。

统一登录继续由系统浏览器承接，不把账号密码表单复制进 App。当前缺陷位于 fresh AVD 的 OIDC pending attempt / callback 生命周期，不改变集中认证、SSO、Cookie 与凭据隔离边界。

## 2. 候选与环境

| 项目 | 结果 |
| --- | --- |
| 源码基线 | `dev` / `4aa04ac3` 加本轮已批准、尚未提交的契约修正 |
| 新 APK | `Clients/radish.flutter/build/app/outputs/flutter-apk/app-release.apk`；`82,913,651` bytes |
| SHA-256 | `ed5f57abfe265c08c2bc2020443143ce1bf1b4ba44c741a7767d88b7b0f9d950` |
| 环境 | `development + https://localhost:5000` |
| compact | `Pixel_9_Pro` / API 35 / `emulator-5554` / `1280x2856` portrait |
| medium | 临时 `Radish_Medium_API_35` / API 35 / `emulator-5556` / `1600x2560` tablet |
| 接线 | 两台 AVD 均安装同一新 APK，并建立 `adb reverse tcp:5000 tcp:5000` |
| 服务 | Gateway `https://localhost:5000`、API `http://localhost:5100`、Auth `http://localhost:5200`；Gateway `/health` 为 `Healthy` |
| 身份 | 只使用本地 developer seed；不记录 token、密码或浏览器 Cookie |

P6-A 固定哈希 `d7b1b9d1…34200` 已因运行时代码变化失效。后续若继续修改运行时代码，必须再次重建并冻结新哈希，不能把本轮 `ed5f57ab…950` 的运行证据无条件继承给下一候选。

## 3. 本轮契约修正

### 3.1 Auth 与公开资料

- `HttpRadishApiClient` 通过单一 token resolver 在请求前处理临近过期会话，并只对携带 bearer token 的首次 `401` 强制续签后重试一次。
- `SessionController` 合并同一 access token 的并发 refresh，按 session epoch 隔离迟到结果；只有 `invalid_grant` 清空会话，网络或临时服务失败保留当前会话并表达 issue。
- Flutter 公开资料、公开统计、公开帖子和公开评论统一改用 `identifier` 与现有 Public API，不再误用需要本人 / 管理权限的旧路径。

### 3.2 Forum 写入

- 帖子详情和评论模型消费 `voContentRevision`；作者帖子 / 根评论编辑提交 `expectedContentRevision`，解析服务端返回的新 revision，并只在成功后更新本地节点 revision。
- 同一编辑失败重试仍复用原 `clientSubmissionId`；内容、目标、账号或成功结果变化后生成新 key。
- Flutter 轻回应输入上限从 `24` 收敛为服务端权威上限 `10`。

### 3.3 Profile 地址语义

- `UpdateMyProfile` 的 `Address` 固定为：字段省略或 `null` 保持原值，空字符串清空，非空字符串 `Trim()` 后写入。
- Flutter 与 Web 编辑器均发送修剪后的地址字符串，不再把空字符串折叠为“未提供”。

## 4. 运行态结果

### 4.1 compact 通过项

- 本地开发证书继续页、系统浏览器 OIDC 和回到 App 后的会话恢复成立；旧 refresh token 被另一登录轮换后，App 能给出可恢复登录路径。
- Admin 资料地址先写入 `P6-RC-ADDRESS`，再从编辑器清空；数据库最终为 `User.Id=20001`、`UserAddress=''`、长度 `0`。
- 受控帖子 `2093676818593742848` 的正文从 revision `1` 更新为 `P6-RC-20260829 forum body v2`，数据库最终 `ContentRevision=2`、`EditCount=1`，App 显示保存成功。
- 轻回应输入恰好 `P6B10CHARS`，UI 从“还可输入 10 字”到“还可输入 0 字”，发布后数据库长度为 `10`。
- 根评论作者编辑器、保存与取消动作可见；本轮 ADB 输入法坐标自动化没有形成可信的第二次运行时写入，因此不把根评论运行时 CAS 记为通过。两次连续编辑 revision `1 -> 2` 的行为由 widget / repository 自动化覆盖。

### 4.2 medium 结果与阻断

- fresh medium AVD 可首装、启动和匿名读取；横向主导航、公开内容卡片、长正文与列表在 `1600x2560` tablet 布局中无横向溢出或崩溃。
- medium 可打开统一登录页，且种子账号认证成功；由于 Chrome 首次被本地开发证书页中断，回到 App 时 pending login attempt 已无法匹配，App 正确显示“关闭 / 重试登录”恢复动作。
- 再次重试仍未关闭 authenticated 回流，因此 medium 登录态、私域页面和写入矩阵不能记为通过。这是下一轮首个阻断项。

### 4.3 尚未关闭的 P6-B 范围

第一轮旧哈希实际采集了 compact / medium 的 default 与四主题代表面，以及 Docs、Shop、Profile、Wallet / Experience、History、Leaderboard 等多条读取 / 购买链路；但随后发生 Auth、Profile 与 Forum 运行时代码修正，这些截图只能保留为问题定位和未受影响视觉参考。新哈希目前只完成 4.1 的 compact 定向复验与 4.2 的 medium 匿名复验，不能直接继承为同哈希完整门禁。

- medium authenticated OIDC、会话恢复、私域页面与写入；
- default 主题剩余完整业务矩阵与 compact 根评论运行时 CAS；
- `guofeng / theme-dark-night / theme-sakura` 在 compact / medium 的代表运行矩阵；
- 登录取消、登出、冷启动、根层 Back、输入法与主题持久化的成组人工复核。

以上未执行或未通过项不能由静态测试、compact 单设备结果或 Web 证据替代。

## 5. 自动化与构建

| 门禁 | 结果 |
| --- | --- |
| Flutter 定向 | `120 / 120` |
| Flutter 全量 | `427 / 427` |
| `flutter analyze` | 零问题 |
| 后端身份 / Forum 定向 | `52 / 52` |
| Web 全量 | `557 / 557` |
| Web type-check / lint | 通过 |
| `Radish.Api` / `Radish.Gateway` / `Radish.Auth` | 分别构建通过，均 `0 warning / 0 error` |
| release APK | 构建通过；哈希见第 2 节 |
| `git diff --check` | 通过 |

`Radish.slnx` 聚合构建在有还原和 `--no-restore` 两种方式下均于约 `5:01` 结束，只打印 `0 warning / 0 error` 而返回失败；关闭 build server 后结果不变。三个本轮实际宿主独立构建全部通过，因此暂记为聚合工程 / 本机工具链异常，不把它伪记成通过，也不继续用重复全量构建消耗验收时间。

## 6. 数据与环境清理

- 资料地址已恢复为空字符串。
- 本轮受控帖子、根评论、两条轻回应、标签关系、内容 revision 与 submission record 已按项目所有者明确授权使用精确 ID 从本地 SQLite 删除；复核相关计数均为 `0`。
- 没有清除种子用户、既有业务数据、购买审计、真实设备 App / data 或浏览器凭据。
- 三宿主监听、两台 AVD 与 ADB reverse / forward 均已关闭；临时 medium AVD 已删除，既有 compact AVD 保留。
- 本轮 `Radish P6-B Temporary AVD CA` 已从 compact 用户受信任凭据精确卸载；medium 随临时 AVD 删除。两台设备下载目录证书、主机临时私钥 / 证书与原始取证目录均已删除，不能恢复。

## 7. 下一顺位

1. 定向诊断 fresh Android AVD 的 OIDC pending attempt 在 Chrome 证书中断、Activity 生命周期与 callback 回流之间为何丢失；保持系统浏览器统一登录边界。
2. 修复后重跑 Auth 定向测试、Flutter 全量与 release 构建，冻结新的唯一 APK 哈希。
3. 用新哈希先关闭 medium authenticated 回流与 compact 根评论 CAS，再完成 default 剩余矩阵和三套非默认主题代表矩阵。
4. 只有 P6-B 全部关闭后，才单独确认 P6-C 同哈希真机验收；正式签名、外部分发、iOS 与 desktop 继续后置。
