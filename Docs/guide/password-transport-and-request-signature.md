# 密码传输与请求签名临时评审

> 状态：`临时专题 / 待后续归并`
>
> 记录日期：`2026-06-18`（Asia/Shanghai）
>
> 适用范围：登录密码、注册密码、支付口令、商城购买、萝卜币转账，以及未来开放 API / 服务端到服务端调用的请求签名边界。

## 当前结论

1. 前后端请求体中的密码字段在应用层是明文字段。
   - Auth 登录 / 注册表单提交 `password`、`confirmPassword`。
   - 支付口令接口提交 `newPassword`、`currentPassword`、`confirmPassword` 或 `paymentPassword`。
   - `@radish/http` 当前直接 `JSON.stringify(data)`，没有字段级 RSA / AES 加密，也没有浏览器侧 `sign` 包装。
2. 外部访问的传输安全边界是 HTTPS / TLS。
   - 本地和生产前端默认通过 Gateway 公开入口访问：`https://localhost:5000` 或 `https://radishx.com`。
   - 生产不应公开暴露 `http://localhost:5100`、`http://localhost:5200` 这类直连 HTTP 入口。
3. 数据库存储不是明文。
   - 登录密码使用 `PasswordHasher` 的 Argon2id 哈希。
   - 支付口令存储 `PasswordHash` + `Salt`，不是明文；但当前实现是 `SHA256(salt + password)`，对 6 位数字口令的离线抗爆破能力弱于 Argon2id。
4. 审计日志已有基础脱敏，但仍需补前端日志治理。
   - API 审计中顶层字段包含 `password / pwd / secret / token / apikey / api_key` 时会整段请求体脱敏。
   - `radish-pit` 转账表单当前存在把 `TransferFormData` 写入 debug 日志的路径，存在泄露 `paymentPassword` 的风险；该项可作为低风险维护修复优先处理。

## 浏览器前端不做通用 sign

浏览器 SPA 不能保存真正的签名密钥。把参数混合后做 `sign` 再发给后端，如果密钥、算法和拼接规则都在前端，攻击者可以直接读取、复用或绕过。

因此当前不建议为普通 Web 请求增加“浏览器前端参数签名”作为主要加固手段：

- 不把 `sign` 当作密码保护手段。
- 不把客户端 hash 后的密码作为协议值发送；它会变成等价密码，仍可能被重放。
- 不在前端 `.env`、运行时配置或 JS bundle 中放签名密钥。
- 不用统一签名中间件掩盖服务端鉴权、权限、幂等、限流和参数校验问题。

当前 Web 请求的正确安全模型仍是：

```text
浏览器 / App
  -> HTTPS / TLS
  -> Gateway / Auth / Api
  -> Bearer Token / Cookie / Antiforgery / 服务端权限校验
  -> 慢哈希存储 / 业务幂等 / 审计与限流
```

## 短期治理建议

1. 前端敏感日志脱敏。
   - 移除或脱敏包含 `paymentPassword`、`password`、`currentPassword`、`newPassword`、`confirmPassword` 的日志参数。
   - 后续可在统一 logger 增加递归敏感字段清理，避免新代码误传对象。
2. 支付口令哈希升级。
   - 后续将支付口令迁移到 Argon2id 或等价慢哈希。
   - 利用 `PasscodeVersion` 做兼容升级：旧版本验证通过后写回新版本哈希。
3. 支付 / 转账幂等与重放边界。
   - 商城购买、萝卜币转账等写操作后续可增加 `idempotencyKey`、业务上下文绑定和短窗口重复提交保护。
   - 该能力优先解决重复点击、网络重试和请求重放带来的业务一致性问题，不替代支付口令验证。
4. 生产入口收口。
   - 生产外部访问必须走 HTTPS Gateway / 反向代理。
   - API / Auth 直连 HTTP 只允许作为内部或本地开发入口，不作为公开入口。

## 前端敏感日志治理口径

本批只处理统一 logger 的出参脱敏，不改变接口协议、请求体结构、密码存储、支付口令验证或业务幂等。

- 覆盖范围：`radish.client` 与 `radish.console` 的统一 `log.debug / info / warn / error / table`，以及 `@radish/http` 内部直接 `console.error` 的错误对象输出。
- 脱敏字段：`paymentPassword`、`paymentPasscode`、`password`、`pwd`、`passcode`、`currentPassword`、`newPassword`、`confirmPassword`、`oldPassword`、`accessToken`、`refreshToken`、`idToken`、`token`、`secret`、`apiKey`、`api_key`。
- 匹配规则：字段名大小写不敏感，并忽略 `_`、`-` 等分隔符，例如 `new_password` 与 `newPassword` 等价。
- 处理方式：递归复制普通对象和数组，敏感字段值替换为 `[REDACTED]`；循环引用替换为 `[Circular]`；`Error` 对象保留 `name / message / stack` 并脱敏其可枚举附加字段。
- 验证入口：`radish.client`、`radish.console` 与 `@radish/http` 各自补敏感日志 node 测试；开发中优先跑对应 workspace 测试、类型检查、`git diff --check` 和变更文件仓库卫生检查。

## 未来可评审的加强项

1. 开放 API / 服务端到服务端签名。
   - 适用场景：第三方应用、服务端回调、Webhook、开放平台 SDK、跨服务内部调用。
   - 推荐形态：`HMAC-SHA256(method + path + query + bodyHash + timestamp + nonce)`。
   - 密钥只放后端或第三方服务端后台，不能下发到浏览器前端。
   - 服务端需要校验时间窗口、nonce 去重、body hash、应用状态、权限范围和速率限制。
2. 高风险动作二次验证。
   - 支付口令重置、大额资产操作、高风险账号字段变更、管理员敏感操作可评估 WebAuthn / Passkeys、TOTP、短信 / 邮箱 OTP 或设备确认。
   - 二次验证应按风险分层接入，不作为所有普通请求的默认负担。
3. 安全会话与资产治理专题。
   - 安全会话、资产、高风险账号字段继续不进入 Console 普通系统设置。
   - 若要开放配置，应先完成风险等级、二次确认、审计、回滚和发布验证口径。

## 当前不做

- 不做浏览器通用 RSA / AES 字段加密。
- 不做浏览器通用参数 `sign`。
- 不把 hash 后的密码当作登录或支付凭证。
- 不把签名密钥、支付安全开关或高风险资产设置放进前端配置。
- 不因为暂不做前端签名而放松 HTTPS、服务端权限、限流、审计、慢哈希和幂等治理。

## 后续归并口径

如果本专题进入正式治理，应拆分归并到以下文档：

- 登录密码存储与验证：[密码安全](/guide/password-security)
- OIDC / Token / 会话安全：[鉴权与授权指南](/guide/authentication)
- 支付与萝卜币风控：[萝卜币安全与扩展](/guide/radish-coin-security-tech)
- 开放 API 签名与第三方接入：未来开放平台专题
