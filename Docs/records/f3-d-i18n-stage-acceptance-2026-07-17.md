# F3-D i18n 专题验收记录

> 日期：2026-07-17（Asia/Shanghai）
> 状态：阶段主矩阵通过；Console 受权内页待管理员会话补验
> 范围：正式 Web `zh / en` 语言运行时、Public / Private / Auth、Console 认证与无权限边界、PC / mobile 代表视图

## 1. 验收结论

F3 已完成三端语言运行时、业务域资源、结构化错误与 locale formatter 的成组实现。本轮通过 Gateway 真实启动复核了 Public、普通用户 Private、Auth 与 Console OIDC 边界：中文 / 英文切换、刷新保持、client / Console 设备语言共享、登录 / 注册 / 授权确认往返、官方客户端元数据、页面标题、日期数字和用户内容原文边界均符合专题约束，代表页面未发现横向溢出或新的 `P0/P1` 阻断。

Console 受权内页没有写成已通过。本机 Chrome 没有可复用的本地管理员会话，新开 Console 会进入正式 Auth 登录页；本轮没有通过修改角色、数据库或权限绕过认证。因此 F3 仍保留一个明确证据项：在管理员会话下补验 Dashboard、用户、内容治理、订单、应用、个人资料与 Hangfire 的 `zh / en × PC / mobile` 代表矩阵。该项通过后再正式关闭 F3 并进入 F4。

## 2. 本轮实现与治理

### 2.1 Auth 与 OIDC

- 官方 `radish-client / radish-console / radish-scalar` 的显示名、说明和开发者改为按稳定 client ID 解析 `.resx`；未知或外部客户端继续使用已配置元数据，不擅自翻译第三方内容。
- Login、Register、Consent 统一消费官方客户端词元，账号页之间跳转保留当前 culture。
- `SetLanguage` 同步改写外层账号路由与内层本地 authorize returnUrl 的 `culture / ui-culture`，避免语言切换后 OIDC 回流恢复旧语言。
- 补充嵌套 returnUrl、culture cookie、官方客户端资源 fallback 与中英文资源测试。

### 2.2 Console 正式页面与共享反馈

- OIDC callback、权限守卫、not-found、ErrorBoundary、客户端返回入口和路由级无权限反馈进入共享 `shell` 资源。
- Applications、UserProfile 与 Hangfire 形成独立 `applications / profile / systemTools` 中英文域资源；系统词元、日期和数字按当前 locale 展示，应用元数据、账号字段、任务数据和其他运营内容保持来源原文。
- HTML 初始 title 改为中性值，运行时再由路由与语言状态接管，避免加载阶段泄漏单一语言。

### 2.3 门禁暴露的 LongId 契约漂移

`validate:ci` 的 LongId 扫描发现分片上传响应和公开 head product identity 仍允许 JavaScript `number`。后端 `long` 已通过统一 JSON converter 以字符串输出，前端 `LongId` 和真实消费者也都按字符串使用，因此这两处类型恢复为 `string` 契约，阻止超过安全整数范围的 ID 被静默舍入。

## 3. 运行态矩阵

| 链路 | 结果 | 代表证据 |
| --- | --- | --- |
| Public | 通过 | Discover、Forum、Docs 在中文 / 英文和 PC `1920 × 1080`、mobile `390 × 844` 下切换正常；系统壳层与 title 随语言变化，帖子、分类和文档正文保持原文，无横向溢出 |
| Private | 通过 | 普通用户 `/me` 在双语 PC / mobile 下保持认证状态；日期、数字和胡萝卜金额按 locale 展示，用户等级名等配置内容保持原文 |
| Auth | 通过 | Login、Register、Consent 的中英文文案、官方 Console 元数据、账号页互跳、外层与嵌套 culture、授权回流均保持一致 |
| Console 认证边界 | 通过 | 普通用户真实完成 Console OIDC 后得到本地化无权限页；callback toast、页面 title、返回入口与 mobile 宽度正常，没有绕过权限进入管理端 |
| Console 受权内页 | 待补验 | 当前没有管理员浏览器会话；Dashboard、用户、治理、订单、应用、个人资料与 Hangfire 只完成资源、测试、lint、type-check 和 production build，尚未形成受权运行态证据 |
| 跨应用偏好 | 通过 | client 与 Console 共用 `radish_lang`；语言切换后刷新和跨应用进入保持一致，Auth 通过 culture cookie 与 OIDC 参数承接 |

移动结论基于 `390 × 844` CSS 视口，不宣称真实设备 DPR 覆盖。运行态只验证代表路径，不把用户内容、运营内容或固定 Docs 正文翻译为另一种语言。

## 4. 自动化、构建与宿主检查

- `npm run validate:ci -- --report`：通过。四个前端 workspace 共 508 项测试通过；后端 818 项测试通过，12 项 PostgreSQL 环境用例按配置跳过；Identity 定向回归 31 项通过，LongId、运行时与协议扫描无残留。
- `radish.console`：56 项测试、type-check、零 warning lint 与 production build 通过。
- `radish.client`：415 项测试、type-check 与 CI lint 通过；LongId 契约修正后追加 production build。
- Auth 定向资源与嵌套 culture 测试通过；解决方案构建 0 warning / 0 error。
- `npm run check:host-runtime -- --details --report`：Gateway、API、Auth 三个健康入口均返回 HTTP 200。
- changed-only 仓库卫生与 `git diff --check` 通过。

## 5. 数据影响与清理

本地验收创建了一个无管理权限的普通测试账号，用于真实注册、登录、Consent、Private 页面和 Console 权限拒绝旅程。该账号没有获得管理角色，没有调整余额、商品、订单或其他业务数据，当前保留供后续回归使用；登录口令未写入文档或提交内容。

本轮没有创建临时管理员、没有修改现有用户角色，也没有通过数据库注入授权。Gateway、API、Auth、client 与 Console 在用户当轮授权下启动，验收结束后全部停止。

## 6. 后续验收与阶段切换

下一轮在用户提供本地管理员登录态并重新授权启动服务后，执行只读 Console 管理态矩阵：

1. PC / mobile 分别检查 Dashboard、用户、内容治理、订单、应用、个人资料与 Hangfire；
2. 覆盖中文 / 英文壳层、长标题、表格 / 抽屉、日期数字、空态、无权限和 not-found；
3. 复核不翻译运营名称、备注、审计内容和任务数据；
4. 更新本记录和规划入口，正式关闭 F3，再进入 F4 真实使用观察、反馈归因与完整功能选题。
