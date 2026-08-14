# F4-R R3-F02-A OIDC 回流信任门禁实现与静态门禁

> 日期：2026-08-13
>
> 状态：代码实现与静态门禁已完成；真实登录、取消授权、缺失 / 过期 callback 与 logout 留待 `R3-F02` 最终成组运行态验收
>
> 依据：[R3-F02 自服务与边界页设计前代码事实与风险拆批审计](/records/f4-r-r3-f02-self-service-boundary-readiness-audit-2026-08-13)

## 1. 结论

`R3-F02-A` 已关闭此前跨 Client、Console、Flutter 与 Auth 的 OIDC 回流信任缺口：

- Client 与 Console 共用 `@radish/http` 的密码学随机 `state`、PKCE S256、五分钟会话尝试和一次性 callback 消费；
- Flutter 以等价协议保存原生登录尝试，Android callback 回传 `state`，兑换 token 时提交同次 `code_verifier`；
- `radish-client` 与 `radish-console` 的 OpenIddict 种子已统一要求 PKCE，创建与既有应用更新路径均覆盖；
- Auth 登录 POST 在凭据查询前拒绝外部或协议相对 `returnUrl`，有效本地授权地址使用 `LocalRedirect`；
- 授权取消、状态失配、尝试缺失 / 过期、Token 网络或响应失败均形成稳定用户消息，原始 `error_description` 与 Token 响应详情不直接展示；
- Console 受保护深链在登录成功后只恢复同一 Console base 内的安全来源，并按当前标签页一次性消费。

本批没有新增 Provider、Scope、Consent、账号能力、数据库实体、migration、权限键或 Pencil 画板，也没有改变 client id、redirect URI、scope、token 保存、logout 与 idle 会话语义。

## 2. 共享浏览器协议

`Frontend/radish.http/src/oidc-callback.ts` 现在同时拥有 authorize 与 callback 协议事实：

1. 使用 Web Crypto 生成 32 字节随机 `state` 和 `code_verifier`，以 SHA-256 派生 S256 `code_challenge`；
2. 尝试按 client id 写入 `sessionStorage`，绑定 redirect URI 与五分钟有效期，不进入长期存储；
3. 固定协议参数在附加参数之后写入，调用方不能覆盖 `state`、challenge、client、redirect 或 scope；
4. callback 先清理地址栏中的 code、state、issuer、session 与授权错误参数，再读取并消费对应尝试；
5. state 缺失 / 失配、尝试缺失 / 过期、缺少 code 和重复消费均 fail closed；
6. Token 兑换提交同次 verifier；React Strict Mode 的同一轮重复 effect 只复用进行中的请求，不形成第二次兑换；
7. 原始上游错误仍可作为结构化诊断事实保留，但默认与正式产品消息均不直接展示。

Client 的统一登录入口已接入该 helper，论坛旁路 authorize 拼接已删除；callback 增加重新登录与安全返回。Console 使用同一协议实现，并由独立 `authReturnPath` owner 规范化来源：拒绝外部地址、登录 / callback 循环、反斜杠、控制字符及 query / fragment 中的凭据参数，成功后只消费一次。

## 3. Flutter 与 Android

- 经单独授权执行 `flutter pub add 'crypto:^3.0.7'`，新增直接依赖 `crypto 3.0.7`，lockfile 同步记录传递依赖 `typed_data 1.4.0`，没有升级其他现有约束；
- `NativeOidcAuthorizationAttempt` 持久化 state、verifier、redirect URI 与开始时间；新登录覆盖旧尝试，callback、退出和本地失败按协议消费或清理；
- 应用从外部浏览器恢复但尚无真实 callback 时保留未过期尝试，避免生命周期恢复先于 deep link 到达时误删 verifier；
- Android `MethodChannel` 使用同步落盘的小型 SharedPreferences 写入，保证打开外部浏览器前尝试已经持久化；take / clear 同样确认落盘结果；
- `AuthorizationCodeExchangeService` 强制接收 verifier；非 2xx、网络失败与无效 JSON 只返回稳定用户消息，不展示上游 response body。

## 4. Auth 与 OpenIddict

- `AccountController.Login` 在凭据查询和登录签发前调用 `Url.IsLocalUrl`，明确拒绝外部绝对地址与协议相对地址；有效本地 `returnUrl` 使用 `LocalRedirect`；
- 新增中英文 `auth.login.error.invalidReturnUrl`；测试同时证明非法地址不会触发凭据查询；
- OpenIddict seed 为 `radish-client` 与 `radish-console` 的创建 / 更新描述统一加入 `ProofKeyForCodeExchange` requirement；Scalar 调试客户端保持既有边界。

## 5. 验证

- 后端全量：`1275 passed / 41 skipped`；身份专项：`34 / 34`；
- `@radish/http`：`48 / 48`，type-check 与 Lint 通过；覆盖 challenge、state 成功 / 缺失 / 失配 / 过期 / 重放、授权拒绝、URL 清理、verifier、网络失败与无效响应；
- Client：`550 / 550`，Lint 与 production build 通过；
- Console：`130 / 130`，Lint 与 production build 通过；
- Flutter：`flutter analyze` 通过，全量 `209 / 209`；Android `testDebugUnitTest --offline` 在 JDK 21 下完成 Debug / Kotlin 编译与原生单测；
- `npm run validate:identity`、文档检查、changed hygiene 与 `git diff --check` 通过。

本批按审计约定只完成静态阶段，没有启动 Auth、Gateway、前端服务或浏览器。真实 OIDC 登录、取消授权、过期 / 缺失 callback、Console 深链与 logout 将在 `R3-F02-A / B / C` 全部关闭后的最终成组运行态验收一次覆盖。

## 6. 下一步

下一顺位进入 `R3-F02-B 自服务权威状态` 方案确认：审计 Settings 的动态规则、dirty / CAS / stale 边界与 Profile 的独立摘要快照，不扩大身份协议或视觉范围。
