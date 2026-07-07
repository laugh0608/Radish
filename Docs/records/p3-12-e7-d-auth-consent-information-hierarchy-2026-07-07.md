# P3-12-E7-D Auth 授权页信息层级收口记录

> 日期：2026-07-07（Asia/Shanghai）
>
> 主线：`P3-12-E7 正式 UI 与文案成熟度专项审计`
>
> 范围：Auth 授权确认页 UI / 文案 / 信息结构治理。本批不改授权协议、登录回流、权限、接口、后端运行时行为、审计、错误模型或安全契约。

## 背景

`P3-12-E7` 首批审计确认 Auth 授权页把 `Client ID`、回调来源和原始 `scope` 放在首屏高权重区域，普通用户在授权时需要先理解技术信息，才能确认“谁要访问、我用哪个账号、会开放什么、确认或取消后会怎样”。这会降低 Console / Scalar 等授权场景的确认效率，也不符合正式产品对授权页的信任表达要求。

本批按 `E7-D` 处理 Auth 授权页信息层级，不进入 `P3-12-F`，不创建 tag，不进入 M15 测试或生产部署。

## Pencil 同步

- 用户已打开 `Docs/frontend/design-sources/web-ui-foundation.pen`。
- 已新增画板：`E7-D - Auth Consent Information Hierarchy`。
- 用户反馈授权页观感过大后，画板已二次收紧为紧凑授权卡参考：桌面与移动共用同一条纵向确认路径，保留应用 / 账号 / 权限 / 风险 / 技术信息的理解顺序，不照搬参考图配色或品牌。
- 用户继续反馈页面仍偏抽象后，画板已三次具体化为真实授权决策样例：直接展示账号、应用、开发方、返回域名、允许后的具体用途和不会获得的敏感内容，减少概念说明。
- 用户继续反馈 UI 过挤、底部按钮与边框重叠后，画板已四次舒展：桌面卡片加宽加高，权限改为两列，减少嵌套边框，并把确认 / 取消按钮放入独立动作 footer，保留明确底部安全区。
- 画板固定桌面与移动两种授权确认结构：
  - 首屏优先展示授权目标、开发方、返回目标和当前账号。
  - 权限区展示用户可理解的权限含义，原始 `scope` 降为次级技术标签。
  - `Client ID`、完整回调地址和原始 scope 下沉到“技术信息”详情区域。
  - 移动端保留单列确认路径，主动作优先为“允许继续”，次动作为“取消授权”。
- Pencil `snapshot_layout` 已对该画板执行结构检查，结果为 `No layout problems.`。

## 代码改动

| 文件 | 处理 |
| --- | --- |
| `Radish.Auth/Views/Authorization/Consent.cshtml` | 四次舒展授权确认页：卡片最大宽度放宽，主内容间距和 padding 增加；账号 / 应用核对区减少分隔边框；桌面权限改为两列；确认 / 取消按钮进入独立动作区并增加底部安全距离。 |
| `Radish.Auth/Resources/Errors.resx` / `Errors.zh.resx` / `Errors.en.resx` | 更新授权标题、说明和按钮文案，改为“确认是否允许访问”“允许继续 / 取消授权”等用户动作语言。 |
| `Docs/frontend/design-sources/web-ui-foundation.pen` | 新增 E7-D Auth 授权页信息层级参考画板。 |
| `Docs/frontend/design-sources/README.md`、`Docs/frontend/web-ui-foundation-design.md` | 记录 web-ui foundation 中新增的 Auth 授权确认参考画板。 |

## 保持不变

- `AuthorizationController.Authorize()` 未修改。
- `ConsentViewModel` 未修改。
- `decision=accept/deny` 提交值未修改。
- 隐藏字段 `client_id`、`redirect_uri`、`response_type`、`scope`、`state` 仍按原样提交。
- 拒绝授权仍走 OpenIddict `access_denied`；允许授权仍使用原请求 scopes 生成授权结果。
- 未新增 API、权限键、审计日志、后端错误模型、运行时配置或数据库结构。

## 验证记录

- `dotnet build Radish.Auth/Radish.Auth.csproj`：通过；保留既有 `Microsoft.OpenApi 2.0.0` 高危漏洞 warning（`Radish.Auth` / `Radish.Extension` 传递提示），本批未改依赖。
- `npm run check:repo-hygiene:changed`：通过，未发现文本卫生问题。
- `git diff --check`：通过。

本批未执行真实 Gateway smoke；如后续需要真实授权页 PC / mobile 复核，必须由用户当轮明确说明 API / Auth / Gateway / 前端已经启动。

## 后续判断

- `E7-D` 已按用户截图反馈完成紧凑授权卡、具体授权决策和按钮安全区四次收口，下一步不直接进入 `P3-12-F`。
- 先根据 E7-A 至 E7-D 的结果做 `P3-12-E7` 收束判断。
- 若真实页面复核、用户截图或自动化检查继续命中 Public / Console / Auth 的 UI 或文案阻断，再回拉对应页面族；若命中接口、错误模型、后端日志、权限、审计或运行时契约缺口，先补小方案并等待确认。
