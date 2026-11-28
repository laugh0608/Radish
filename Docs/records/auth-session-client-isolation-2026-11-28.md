# 普通 Web 与独立客户端的凭据隔离验收

2026-11-28 从 `dev` 提交 `8daa7264` 构建五个本地候选镜像。OrbStack 项目使用一次性 PostgreSQL 16 和 Redis 7，`dbmigrate apply / verify` 均成功。Chromium 153 的独立资料仅经本机 Gateway 访问候选。PC、移动 Web 及独立客户端的凭据隔离取得实测证据，Scalar 页面登录和 Auth 重定向日志仍需修复。[验收矩阵](../features/auth-session-overnight-recovery-plan.md#修复验收)

## 普通 Web 与浏览器存储

PC（1920 × 1080）和移动触屏视口（390 × 844、DPR 3）分别从个人页登录。两端均使用 `radish-web`，服务端回调返回 `302`，取短令牌和读取受保护账号均返回 `200`。页面没有向 `/connect/token` 发送兑换请求。会话状态响应含 `Cache-Control: no-store`。

两端的 `__Host-Radish-Web-Session` 均为 host-only、`HttpOnly`、`Secure`、`SameSite=Lax`、`Path=/`。页面 JavaScript 读取不到该 Cookie。登录后的 localStorage、sessionStorage 均无旧 Web access / refresh 项，当前来源下的 IndexedDB 数据库和 Cache Storage 缓存数量均为零。两套前端的 251 个构建文件均未命中本轮生成的客户端机密。11 月 25–26 日两端隔夜重开后的结果与本轮一致。[既有浏览器记录](./auth-session-candidate-2026-11-25.md#登录与-auth-重建)

## 独立客户端与回调

认证库中，`radish-web` 是唯一登记 `/web/session/callback` 的 confidential 客户端。`radish-client`、`radish-console`、`radish-scalar` 均为 public，分别登记原生、Console 和 Scalar 对应的回调。冷启动后由操作系统交付 `radish://` 并从 Keychain 恢复 PKCE 的设备侧证据见[iOS 模拟器记录](./f4-flutter-native-p7c-ios-simulator-runtime-acceptance-closure-2026-08-31.md)。

| 入口 | 候选实测 |
| --- | --- |
| Console | 管理员经 `radish-console` 和 `/console/callback` 回到用户页，兑换和受保护账号读取均为 `200`。Console 写入自己的 refresh 项。单独查询普通 Web 状态为 `401`，没有普通 Web Cookie，新增普通 Web 会话为零。 |
| 历史 WebOS | 从 `/desktop` 频道深链触发 `radish-client` 登录，经 `/oidc/callback` 和浏览器侧兑换回到聊天室。`GetUserByHttpContext` 返回 `200`，账号与租户均匹配。WebOS 持有自己的旧 access / refresh 项，普通 Web 状态为 `401`，新增普通 Web 会话为零。 |
| Scalar 页面 | `/scalar/v1` 和 `/openapi/v1.json` 均为 `200`。点击授权后，浏览器选用 `radish-scalar`，但授权、令牌和回调地址都指向 `https://localhost:5000`。候选登记的回调在 `https://web-session.test:35443`。浏览器在离开候选前拦下导航，当前页面登录验收失败。[Scalar 配置](../../Radish.Extension/OpenApiExtension/ScalarSetup.cs) |
| Scalar 协议对照 | 另用登记的候选地址完成 `radish-scalar` 授权码流程，回调和受保护账号读取均为 `200`。兑换只发放该流程的短期凭据，没有 refresh token。相同浏览器查询普通 Web 状态为 `401`，认证库新增普通 Web 会话为零。 |
| 旧 Flutter 协议与原生回调 | 移动 Chromium 按 Flutter 的 `radish-client`、scopes 和 PKCE S256 发起交互登录。测试端接收 Auth 发往 `radish://oidc/callback` 的真实回调，校验原 state，按原客户端登记兑换成功并读取受保护账号。相同浏览器查询普通 Web 状态为 `401`，没有普通 Web 会话 Cookie，新增普通 Web 会话为零。本轮覆盖浏览器到原生协议回调的交接。[Flutter 登录契约](../../Clients/radish.flutter/lib/core/auth/native_auth_controller.dart) |

从 PostgreSQL 按客户端核对签发到过期的时间差，`radish-client` 和 `radish-console` 的 access / refresh 分别是 300 / 1500 秒，`radish-scalar` 的 access 是 300 秒。普通 Web 样本为 300 / 2592000 秒，浏览器从 `/web/session/token` 收到的字段只有 `voAccessToken` 和 `voExpiresAt`。

## 回调、来源与日志

PC 保留有效登录，在另一回环来源打开健康页并从那里读取候选的会话状态。Chromium 实际请求未附带 Web 会话 Cookie，网络层收到 `403`，页面脚本读不到响应。成功登录后，本轮登录关联 Cookie 已清除；使用已消费的 state、合成无效码且无关联 Cookie 的请求返回 `400`。最终 PostgreSQL 有 6 条已激活的 Web 会话、0 条待消费登录事务。

测试固定使用同源登录和服务端一次性事务。SQLite 用例向控制器再次提交已完成的合成回调，覆盖 `400`、无第二枚 Cookie、原会话版本和凭据摘要不变，模拟兑换端只被调用一次。跨来源、缺少来源或防伪值不匹配的写请求，以及每条上游路径允许转发的 Cookie，分别由控制器与 Gateway 的定向用例覆盖。[回调回归](../../Radish.Api.Tests/Auth/WebSessionPersistenceTest.cs) · [来源和转发回归](../../Radish.Api.Tests/Auth/WebSessionControllerTest.cs)

Auth 在执行登录重定向时把带查询串的目标写入了控制台和 `/app/Logs` 文件。记录中可识别 `state`、`nonce` 和 PKCE challenge。成组复验采到的四个回调 state 在 Auth 两个出口均精确命中，来源为 MVC 的 `RedirectResult` 和 `LocalRedirectResult` 执行日志。[登录返回地址的生成](../../Radish.Auth/Controllers/AuthorizationController.cs)

同组在内存中对照客户端机密、Web 会话 Cookie、防伪值，以及实际观察到的三枚 access token、三枚旧客户端 refresh token 和四个回调授权码。在浏览器控制台、六个容器的标准日志，以及 API、Auth 和 Gateway 的应用日志文件中，上述样本均为零命中。服务端专属 Web refresh token 保留加密状态，未取出其明文作比对。验收仅对采样出口和样本作结论。

## 回归与复验条件

34 项后端定向测试、16 项 Web 与共享 HTTP 测试、20 项 Flutter 认证与会话测试全部通过。首次浏览器预检受宿主代理影响，随后在测试资料所在的进程设置了仅作用于本机候选的代理例外。登录限流返回 `429` 后，等原 15 分钟窗口自然恢复再取样。

后续复验须同时证明 Auth 两个日志出口均不再含登录参数，以及用户直接从 Scalar 页面授权后可在已登记的 Gateway 上完成回调、兑换和受保护读取。正式开关继续关闭。

已清理本项目的 6 个容器、4 个卷、1 个网络和 5 个镜像标签，关闭临时浏览器并移除本轮证书、资料和工作目录。`127.0.0.1:35443` 已释放。
