# Auth 授权确认页信息层级说明

> 适用范围：`Radish.Auth` 的 OIDC 显式授权确认页。
>
> 当前页面：`Radish.Auth/Views/Authorization/Consent.cshtml`
>
> 相关资源：`Radish.Auth/Resources/Errors.*.resx`

本页说明授权确认页的用户决策层级和文案规则。它只约束授权页 UI、文案和信息组织，不改变 OIDC 协议、登录回流、scope、权限、接口、审计、错误模型或后端运行时行为。

## 用户决策顺序

授权确认页必须帮助用户快速判断五件事：

1. 谁在请求访问：展示请求应用名称、开发方和应用说明。
2. 我正在用哪个账号授权：展示当前登录身份。
3. 确认后会回到哪里：展示返回域名或回调目标的用户可理解形式。
4. 允许后会开放什么：把原始 scope 翻译为具体用途，例如确认登录身份、读取基础资料、访问 Radish 服务、保持本次登录。
5. 什么不会发生：明确不会获得密码或支付口令，不会自动修改账号资料、资产或授予新的管理权限。

`Client ID`、完整 `redirect_uri` 和原始 `scope` 只作为次级技术信息提供给需要核对的人，不应放在首屏最高权重区域。

## 页面结构

当前授权页固定为一张居中决策卡：

- 头部：Radish 标识、应用名称、语言切换和授权标题。
- 核对区：当前账号、请求应用、开发方、返回位置。
- 权限区：逐条解释允许后的具体用途，并展示权限数量。
- 风险提示：说明不会开放的敏感内容，以及异常时应取消授权。
- 技术信息：使用可展开详情承载 `Client ID`、完整回调地址和原始 scope。
- 动作区：主动作是“允许继续”，次动作是“取消授权”。

移动端保持同一条纵向确认路径，不拆成多页，不把技术信息放到主动作前，也不让底部按钮贴边或遮挡卡片边框。

## 语言与资源契约

- Login、Register、Consent 的固定文案与 DataAnnotations 校验统一由 `IStringLocalizer<Errors>` 和 `Radish.Auth/Resources/Errors*.resx` 提供，不在 Razor 中维护 `isZh` 双分支。
- 默认、`zh`、`en` 三份 Auth 资源必须保持 key 完整一致；动态应用名称、开发方、redirect host 和用户输入只做安全编码，不自动翻译。
- 语言入口调用 `Account/SetLanguage` 写入 `.AspNetCore.Culture`，Cookie 为一年有效、`HttpOnly`、`Essential`、`SameSite=Lax`，HTTPS 下使用 Secure。
- 语言切换只允许回到经过 `Url.IsLocalUrl` 校验的本地 `returnUrl`；非法或缺失回跳统一回登录页。
- OIDC 发起端可以携带 `culture / ui_locales`，但授权页最终以 Auth Request Culture 和文化 Cookie 为渲染真相源。
- 权限数量使用本地化复数表达；英文长应用名、redirect URI 和权限说明必须允许换行，不遮挡允许 / 取消动作。

## 文案规则

- 主标题使用“允许某应用访问你的账号？”这类用户动作语言。
- 主按钮使用“允许继续”，取消按钮使用“取消授权”。
- 风险提示应说明取消授权不会退出当前 Radish 账号。
- 原始协议词可以出现在技术信息里，例如 `Client ID`、`redirect_uri`、`scope`；正文区应优先使用账号、应用、返回位置、权限用途等用户可理解词。
- 不认识应用、账号不正确或返回位置异常时，文案应明确建议取消授权。

## 保持不变的契约

授权页 UI 调整不得改变这些行为：

- `decision=accept` 和 `decision=deny` 的提交值。
- 隐藏字段 `client_id`、`redirect_uri`、`response_type`、`scope`、`state`。
- 允许授权后继续按原请求 scopes 生成授权结果。
- 拒绝授权继续返回 OpenIddict `access_denied`。
- 登录回流、客户端注册、scope 权限、后端审计、错误模型和运行时配置。

如果后续需求必须修改上述契约，应先补充小方案并确认接口、安全和回归范围，再进入代码。
