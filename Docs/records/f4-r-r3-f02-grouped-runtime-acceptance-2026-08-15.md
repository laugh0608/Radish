# F4-R R3-F02 自服务与边界页成组运行态验收

> 日期：2026-08-15
>
> 状态：Gateway 成组运行态验收与同类根因修正已完成，`R3-F02` 关闭
>
> 依据：[设计前审计](/records/f4-r-r3-f02-self-service-boundary-readiness-audit-2026-08-13)、[A OIDC 回流信任门禁](/records/f4-r-r3-f02-a-oidc-return-trust-gate-implementation-2026-08-13)、[B 自服务权威状态](/records/f4-r-r3-f02-b-self-service-authoritative-state-implementation-2026-08-15)、[C 错误与路由边界](/records/f4-r-r3-f02-c-error-route-boundary-implementation-2026-08-15)

## 1. 结论

`R3-F02` 已形成设计审计、A / B / C 代码实现、静态门禁与 Gateway 运行态闭环：

- OIDC 授权、取消、深链返回、非 Console 身份停止线与回调参数清理保持同一可信链路；
- Console Settings / Profile 和 Client `/me` 使用种子 Admin 的真实权威数据，中英文与 PC / mobile 均可用；
- Client 未知顶层地址不再误入 `/desktop`，Console 区分匿名、非 Console 用户、已授权用户与路由异常；
- 两端根级错误与路由错误均显示稳定诊断编号并可恢复，不用 404 或默认值掩盖运行时异常；
- 验收发现的三个共同根因已在原有 owner 内修正并完成全量回归，没有新增 API、权限、数据库、migration、依赖或 Pencil 画板。

本轮只读取种子数据并执行本地登录 / 授权，没有修改角色、权限或业务数据。验收结束后已退出账号，清除 Client / Console 令牌、OIDC 临时状态和 Auth Cookie，只保留语言、主题与桌面最近应用等非登录偏好。

## 2. 运行环境与身份

- 启动入口：`./start.sh` 的完整本地开发宿主组合；Gateway `https://localhost:5000`、API `http://localhost:5100`、Auth `http://localhost:5200`、Client `http://localhost:3000`、Console `http://localhost:3100/console/`；
- 健康检查：Gateway、API、Auth 的 `/health` 均返回 `200`；
- Admin：种子账号用于 Console Settings / Profile、Client `/me`、OIDC 允许 / 取消与错误恢复；
- 非 Console 用户：`test` 种子账号成功完成 OIDC，但被 Console 准确拒绝，没有临时授予角色或页面权限；
- 匿名：覆盖 Client / Console 未知路由与 Console 登录入口；
- 受限 operator：本地种子数据没有既有受限 operator，本轮遵循“不为视觉样本修改权限”停止线，由 Console 权限契约测试覆盖页面级 permission denied。

## 3. Gateway 页面矩阵

### 3.1 OIDC 与身份分型

- 从 Console 未知深链发起 Admin 登录，Auth 登录与授权确认后回到原始未知地址；授权表单完整保留 `state`、PKCE `code_challenge / code_challenge_method` 与扩展参数；
- 点击“取消授权”后，Console 回调稳定显示“已取消本次登录授权”，URL 清除 `error / error_description / state`，React StrictMode 第二次 Effect 不再把错误覆盖成“授权码缺失”；
- `test` 完成同一 OIDC 流程后回到原未知深链，页面明确显示“当前账号未开通 Console 访问权限”，没有退化为匿名登录、普通 404 或自动跳转；
- state 不匹配、缺少 code、过期尝试、重放与 token 失败继续由 `@radish/http` 全量 OIDC 测试覆盖；浏览器验收不伪造第二套认证状态。

### 3.2 自服务权威状态

- Console Settings 显示真实时区 `Asia/Shanghai` 与当前语言；未编辑时保存动作禁用，后置能力只展示说明，不提供伪写入口；
- Console Profile 显示 Admin、公开句柄、角色、用户 ID、注册时间与“接口未提供”的最后登录边界；PC 三列结构在 mobile 收敛为单列主任务优先；
- Client `/me` 显示真实账号、成长、资产与最近回访数据；中英文切换保持当前身份和页面状态；
- 首次失败 `unavailable`、刷新失败 `stale`、dirty / busy 与离开保护由 B 批状态契约和全量测试覆盖；本轮浏览器使用现有种子数据复核 ready 与响应式代表面，不为制造失败态写入业务数据。

### 3.3 错误与路由边界

- Client 未知顶层地址进入独立 Not Found，中文 / 英文、PC / mobile、键盘焦点与安全返回动作通过；明确 `/desktop` 继续进入历史 WebOS；
- Console 匿名未知路由显示登录与前台返回，Admin 未知路由保留 Console 授权动作，`test` 则进入非 Console 身份停止页；
- 通过受控阻断懒加载模块分别触发 Client 根级 ErrorBoundary 与 Console `RouteRuntimeError`，两端均显示诊断编号，恢复 / 刷新动作通过；阻断规则和缓存设置随后恢复；
- 最终新开稳定页签未发现页面级 warning 或 error。浏览器插件自身的遥测网络超时不计入产品页面日志。

## 4. 视图与工具限制

- PC：`1920 × 1080`，Client / Console 页面没有横向溢出；
- Mobile：`390 × 844` CSS viewport，Client `/me`、Console Settings / Profile、未知路由和非 Console 停止页没有横向溢出或关键内容遮挡；
- 双语：匿名边界、Admin 自服务与 `test` 停止页均覆盖 `zh-CN / en-US`；
- 当前浏览器能力只能设置 CSS viewport，不能设置 DPR；移动复核实际为 DPR `1`，因此只证明移动布局宽度，不冒充 `390 × 844 @ DPR 3` 物理高分屏验收。

## 5. 运行态发现与修正

### 5.1 Consent 丢失 PKCE 参数

授权确认页原先只回传少量固定隐藏字段，POST 丢失 `code_challenge`，OpenIddict 以 `ID2054` 拒绝授权。Auth 现从已解析的 `OpenIddictRequest` 生成隐藏参数列表，完整回传 OIDC 请求参数，同时排除表单动作和防伪字段；定向测试固定 PKCE 与 `ui_locales` 契约。

### 5.2 StrictMode 覆盖授权取消错误

Console callback 首次 Effect 已消费并清理授权错误，开发态 StrictMode 第二次执行随后把它覆盖为缺少授权码。共享 OIDC callback 现只在内存中短暂保留同一 client / redirect 的失败结果，第二次执行消费同一稳定错误；新授权会清理旧失败，不把敏感错误写入 storage。

### 5.3 Profile mobile 与 Form 挂载时序

Console Profile 原三列布局在 `390px` 下压缩重叠，并在资料 Form 尚未挂载时调用 `setFieldsValue` 触发 Ant Design warning。mobile 现明确切换单列，主任务先于摘要；权威资料先排队，Form 挂载后再应用字段值。新开 Profile 页签日志恢复为 `0 warning / 0 error`。

## 6. 验证结果

- 后端：`1279 passed / 41 skipped`；
- `@radish/http`：`48 / 48`；
- Client：`557 / 557`；
- Console：`137 / 137`；
- `@radish/http`、Client、Console TypeScript 通过；Client / Console ESLint 通过；两端 production build 通过；
- Console build 仍有仓库既有 `500 kB` chunk 提示，Client 仍有既有 `800 kB` chunk 提示，没有新增构建错误；
- `npm run validate:identity` 通过，包含身份扫描、LongId 守卫与 `35 / 35` 后端定向测试；
- `npm run check:host-runtime -- --details`、`npm run validate:baseline:quick`、文档、changed hygiene 与 `git diff --check` 均通过。

## 7. 下一步

`R3-F02` 关闭后，F4-R 的正式 Web R1 / R2 / R3 页面族已形成实现与运行态闭环。下一顺位进入 `F4-R Web 主题基线与专题退出门禁审计`：先核对已确认的 `guofeng` 灰玉目标与旧胭脂运行时偏离，汇总四主题、双语、PC / mobile、可访问性和 reduced-motion 证据并提出最终收口方案。Web 基线稳定后再进入 Flutter Dart 语义映射与高价值移动路径审计。
