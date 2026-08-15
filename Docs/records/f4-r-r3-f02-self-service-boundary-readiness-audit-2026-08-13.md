# F4-R R3-F02 自服务与边界页设计前代码事实与风险拆批审计

> 日期：2026-08-13（Asia/Shanghai）
>
> 状态：代码事实审计、代表继承裁决与风险拆批已完成；`R3-F02-A / B` 已完成代码与静态门禁，下一步为 `R3-F02-C`
>
> 范围：Console Settings / Profile，Client / Console 登录与 OIDC 回流，Auth 登录回跳，Client / Console Not Found 与运行时错误边界；Flutter 只纳入共享 OIDC 协议兼容，不纳入本批视觉重构

## 1. 结论

`R3-F02` 的视觉继承成立，不需要新增或修改 Pencil：

- Console Settings / Profile 继续继承 `R1-C01 + R2-C03` 的壳层、表单分区与 Mobile 单任务语法；
- Login、OIDC callback、Not Found 与运行时错误继续继承 `R1-F01` 的共享状态、反馈和 PC / Mobile 边界；
- Auth Razor Login 已具备正式品牌面、客户端信息、双语切换和提交中状态，不需要升级为新的 R1 / R2；
- 本批真实缺口集中在协议信任、权威状态和错误理由，不是新的页面范式。

代码事实同时推翻了原计划中的“OIDC 协议边界无需调整”假设。项目文档把公开客户端固定为 Authorization Code + PKCE，但 Client、Console 与 Flutter 当前均未形成 `state + PKCE` 闭环；Auth 登录 POST 还会直接重定向未校验的 `returnUrl`。如果只做页面视觉收口，会把身份信任缺口继续隐藏在完成状态下。

因此实施顺位固定为：

1. `R3-F02-A OIDC 回流信任门禁`：先补齐所有正式公开客户端的 `state + PKCE`、Auth 本地回跳、授权错误与 Console 来源返回，再启用服务端 PKCE 要求；
2. `R3-F02-B 自服务权威状态`：收口 Settings / Profile 的真实能力、失败、dirty / busy 与 Mobile 主任务顺序；
3. `R3-F02-C 错误与路由边界`：收口 Client / Console 的 Not Found、权限拒绝和运行时错误；
4. 三批静态门禁通过后，再按授权启动当前任务服务并执行 Gateway PC / Mobile、双语与代表身份成组验收。

本次只修改规划与审计文档，没有修改运行时代码、数据库、权限、Pencil 或依赖，也没有启动服务 / 浏览器。

## 2. 正式 owner 与继承事实

| 表面 | 当前 owner | 代表继承 | 审计裁决 |
| --- | --- | --- | --- |
| Client 登录发起 | `Frontend/radish.client/src/services/auth.ts` 及论坛登录入口 | `R1-F01` | 视觉成立；协议参数不完整 |
| Console 登录发起 | `Frontend/radish.console/src/pages/Login/Login.tsx` | `R1-F01` | 视觉成立；协议参数与内部来源返回不完整 |
| Web OIDC callback | `Frontend/radish.http/src/oidc-callback.ts`，两端页面消费 | `R1-F01` | 共享 owner 正确；缺少状态校验、PKCE verifier 和授权错误分类 |
| Flutter OIDC | `Clients/radish.flutter/lib/core/auth/` | Web 稳定后做原生语义映射 | 不做视觉复制；必须参与同一服务端 PKCE 兼容门禁 |
| Auth 登录与回跳 | `Radish.Auth/Controllers/AccountController.cs` 与 Razor Login | `R1-F01` | 结构成立；POST 回跳信任边界不完整 |
| Console Settings | `Frontend/radish.console/src/pages/Settings/Settings.tsx` | `R1-C01 + R2-C03` | 继承成立；真实能力与默认占位混杂 |
| Console Profile | `Frontend/radish.console/src/pages/UserProfile/UserProfile.tsx` | `R1-C01 + R2-C03` | 继承成立；缺少离开保护与 unavailable 表达 |
| Client Not Found / Error | 入口路由、`WebStateSlot` 与根挂载 | `R1-F01` | 状态语法已存在；未知路由归属和根错误边界不完整 |
| Console Not Found / Error / denied | Router、`RouteGuard`、Not Found 与 ErrorBoundary | `R1-F01 + Console 壳层` | 理由需分型；不建立万能状态机 |

Settings / Profile 必须继续是登录用户自服务边界，不纳入 `console.*` 权限矩阵。Login、callback 与错误页也不得借视觉收口新增 Provider、Scope、Consent、账号恢复或管理能力。

## 3. R3-F02-A：OIDC 回流信任门禁

### 3.1 `state + PKCE` 当前没有闭环

当前事实：

- Client 与 Console 的 authorize URL 没有生成或发送 `state`、`code_challenge`、`code_challenge_method`；
- `@radish/http` callback 只提取 `code` 并直接兑换 token，没有校验 `state`，token 请求也没有 `code_verifier`；
- callback 清理只覆盖部分成功参数，授权服务器返回的 `error` / `error_description` 会残留在地址栏；
- OpenIddict 种子中 `radish-client` 的 PKCE requirement 仍被注释，Console 也未启用；
- Flutter authorize 与 token exchange 同样没有 `state` / PKCE；`radish-client` 又同时服务正式 Web、Tauri 历史兼容和 Flutter，不能先单独打开服务端 requirement 再等待客户端补齐。

实施契约：

1. 由共享 OIDC owner 生成密码学随机 `state` 与 `code_verifier`，派生 S256 `code_challenge`；浏览器尝试状态按 client 与单次流程隔离，只存会话级数据，不写入长期存储；
2. callback 必须先验证返回 `state`，再使用同次尝试的 `code_verifier` 兑换 token；状态或 verifier 缺失、失配、过期和重复消费均 fail closed；
3. 成功、授权拒绝或本地校验失败后都消费本次尝试，callback URL 清除 code、state、issuer、session 及授权错误参数；
4. Client 与 Console 使用同一共享协议实现；论坛等旁路登录入口只调用统一发起入口，不再自行拼接不完整 authorize URL；
5. Flutter 完成等价的随机数、S256、状态校验与一次性消费后，才统一为 `radish-client` 与 `radish-console` 启用 OpenIddict PKCE requirement；
6. Tauri 不恢复独立开发线，只随正式 Web 共享路径保持兼容；
7. 保持现有 client id、redirect URI、scope、consent、token 保存、logout 和 idle 会话语义，不新增认证能力。

若 Flutter 需要新增直接依赖，必须先给出精确包名、版本、命令及 `pubspec.lock` 影响并单独获得授权；本审计不安装依赖。

### 3.2 Auth 登录回跳必须限制为本地地址

`AccountController` 的登录 POST 当前在认证成功后直接 `Redirect(returnUrl)`。隐藏字段仍是外部输入，不能因为 GET 阶段曾解析 client metadata 就视为可信。

实施契约：

- 登录成功只允许回到当前 Auth 宿主的本地相对地址；有效 `/connect/authorize?...` 保持不变；
- 外部绝对地址、协议相对地址和其他非法地址必须被明确拒绝，不回退为静默首页跳转；
- 加入有效本地回跳与外部 / 协议相对回跳的 Controller 回归测试；
- 不改变凭据校验、锁定、客户端展示或 Consent 流程。

### 3.3 授权错误必须保留稳定理由

当前共享 callback 把 `error=access_denied` 等授权结果归为 `missing_code`。Client 错误后缺少恢复动作，Console 只显示原始错误和单一返回入口，无法区分用户取消、会话要求、状态校验失败与服务失败。

实施契约：

- 共享结果显式区分 `authorization_error`、`state_mismatch`、`attempt_missing_or_expired`、`token_request_failed` 与 `missing_access_token` 等稳定类型；
- 对允许展示的 OAuth 错误码做受控映射，原始 `error_description` 只作为安全诊断信息，不直接替代用户文案；
- Client / Console 使用各自语言资源表达原因，并按状态提供重新登录、安全返回或回到产品首页；
- callback 参数在结果形成后统一清理，避免凭据或错误详情停留在地址栏；
- 不把不同产品表面压成单个视觉组件，协议分类留在 `@radish/http`，产品动作由各端 owner 决定。

### 3.4 Console 必须恢复安全的内部来源

Console 受保护路由当前统一跳转 `/login?auto=1`，callback 成功后固定进入 `/`，因此深链登录会丢失原路径。Client 已有规范化、一次性消费的来源返回语法，可继承其安全原则，但不能直接复用 Public 路由白名单。

实施契约：

- 只记录 Console base 内的 pathname / search / hash，拒绝登录页、callback、自身循环和外部地址；
- 来源与 OIDC 尝试同样按一次流程保存，成功后消费一次；缺失或非法时回到 Console 默认授权首页；
- 有意的 idle 路径继续走现有 Auth 会话，不因本批增加 end-session，也不伪造新的“闲置退出”提示；
- 非 Console 用户仍进入明确的 Console access denied，不把身份拒绝退化为普通 Not Found。

### 3.5 A 批验证门禁

- `@radish/http`：挑战生成 / callback 解析、state 成功 / 缺失 / 失配 / 重放、授权取消、URL 清理与 verifier 兑换测试；
- Client / Console：authorize 参数、旁路入口统一、内部来源允许 / 拒绝 / 一次性消费、callback 成功与失败动作测试；
- Flutter：authorize 参数、callback state 校验、verifier 兑换与状态消费测试；
- Auth：本地 return URL 允许，外部绝对地址、协议相对地址与非法地址拒绝；
- OpenIddict：只有所有公开客户端消费者通过后才启用 requirement，并验证 Client / Console / Flutter 配置一致；
- 静态阶段不启动服务；真实登录、取消授权、缺失 / 过期 callback 与 logout 在最终成组验收覆盖。

## 4. R3-F02-B：自服务权威状态

### 4.1 Settings

当前真正可持久化的能力是时区、界面语言和密码修改；通知、主题、分页大小、双因素认证与会话时长目前是 disabled 或本地默认占位。首次读取失败只弹 toast，页面随后仍按默认值显示并允许保存 / 重置，会把“不可用”伪装成“有效默认设置”。

实施契约：

- 只有已有权威读写契约的时区、语言和密码作为可操作能力；后置能力改为紧凑的不可用 / 规划说明，不保留看似可提交的伪表单；
- 增加明确 `loading / ready / unavailable / stale` 所有权，首次失败 fail closed 并提供 retry；旧快照刷新失败时可以保留可读值，但冻结依赖陈旧权威状态的写入；
- 保存、重置和密码修改分别维护 busy 与结构化结果，失败不覆盖用户输入；
- 表单 dirty 时阻止意外离开，成功后只清理相应 dirty 状态；
- 不新增通知、2FA、会话管理或新的主题持久化接口。

### 4.2 Profile

Profile 首次失败已经会进入 unavailable 并支持 retry，这一边界应保留。当前主要缺口是编辑离开保护、更新 / 上传期间的 busy 边界，以及把 API 未提供的“最近登录”显示成“无记录”。

实施契约：

- 保留资料更新与头像上传的既有能力，不引入新的账号字段、版本列或数据库迁移；
- 编辑 dirty 时提供站内离开与浏览器关闭保护，写入期间冻结重复动作，失败保留草稿；
- 未由权威接口提供的最近登录信息表达为 unavailable 或移除，不伪装成真实空值；
- Mobile 先呈现当前编辑主任务，再呈现摘要 / 导航辅助信息；PC 保持分区工作面；
- 被本批触达的后端错误补齐稳定 Code / MessageKey，双语表面不直接依赖中文 `MessageInfo`。

### 4.3 B 批验证门禁

- Settings 首次失败 / retry、刷新 stale、真实字段保存、重置、密码失败、dirty / busy 与后置能力不可写；
- Profile 首次失败 / retry、编辑 / 上传失败保留、dirty / busy、unavailable 字段与 Mobile 主任务顺序；
- Settings / Profile 继续是 `authOnly`，不得出现 `console.*` 权限要求；
- PC 连续分区与 Mobile 单任务共用同一权威快照，不复制业务状态。

## 5. R3-F02-C：错误与路由边界

### 5.1 Client

`entryRoute` 当前会把未知顶层路径归为 `root`，而 `RootEntry` 承载 WebOS Shell；结果是未知 URL 可能进入历史桌面，而不是正式 Web Not Found。`main.tsx` 也没有根级 React ErrorBoundary。

实施契约：

- 只有 `/desktop` 及其明确兼容路径进入 WebOS root；未知正式路径进入独立 not-found entry；
- 复用 `WebStateSlot` 的状态语法表达 Not Found 与安全返回动作，不改写现有正式 Public / Private / Author 路由；
- 根级 ErrorBoundary 捕获渲染失败并提供重试 / 回首页，错误原因与 Not Found 保持不同；
- 加入已知路径不回归、未知路径不进入 WebOS 和异常恢复测试。

### 5.2 Console

Console 顶层 access denied 已有独立身份边界，但受权限保护的子路由会静默跳到默认授权页，丢失“无权访问目标”的理由；Not Found、ErrorBoundary 与 RouteGuard loading 仍使用彼此分散的内联表达。

实施契约：

- 明确区分未登录、非 Console 用户、Console 内权限拒绝、路由不存在和运行时错误；
- 权限拒绝保留目标理由，并提供安全的默认授权入口，不假装目标不存在；
- Not Found 在匿名与已登录场景分别提供可用动作，不在匿名边界暴露无意义的 Console 搜索；
- 共享 `R1-F01` 的状态语义与 token，但不建立承载所有身份 / 权限 / 路由状态的万能状态机；
- 保持现有权限键和 Route metadata，不扩大任何角色能力。

### 5.3 C 批验证门禁

- Client 已知入口、`/desktop`、未知路径、根渲染异常和恢复动作；
- Console 匿名、非 Console 用户、受限 operator、未知路由与运行时错误；
- `zh-CN / en-US`、PC / Mobile、键盘焦点与无横向溢出；
- 不用重定向掩盖 access denied，不用 Not Found 吞掉运行时错误。

## 6. 最终运行态矩阵

三批静态门禁关闭后，按当前任务的服务启动授权执行：

- 入口：Gateway `https://localhost:5000` 与 `/console/`；不以 Vite 直连替代正式回流；
- 视图：PC `1920 × 1080`、Mobile `390 × 844`；
- 语言：`zh-CN / en-US`；代表主题延续既有 `default / guofeng`，边界页不复制四主题全矩阵；
- 身份：匿名、种子 Admin、非 Console 用户、受限 operator；
- 身份链路：Client / Console PKCE 登录、授权取消、state 缺失 / 失配 / 重放、Console 深链返回、idle、logout；
- 自服务：Settings / Profile ready、首次 unavailable、retry、dirty / busy 与 Mobile 主任务顺序；
- 边界：未知 Client 路由、`/desktop` 隔离、Console permission denied / Not Found、两端 ErrorBoundary；
- 稳定态：无横向溢出，干净标签页 `0 warning / 0 error`。

真实 smoke 不制造业务数据；普通 OIDC 会话与现有自服务读取不视为业务造数。服务启动命令、端口、运行影响与清理方式仍需在启动前按项目规则再次说明并获得当轮明确授权。

## 7. 停止线与下一步

- 不新增 Provider、Scope、Consent、账号恢复、2FA、会话管理、通知设置、Profile 字段或 Console 权限键；
- 不新建数据库实体或 migration；OpenIddict requirement 只在所有正式消费者兼容后启用；
- 不修改 Pencil；若实施中出现新的壳层或响应式模型，立即停止并重新裁决 R1 / R2；
- 不把 Client、Console、Auth 与 Flutter 页面合并成一个组件，也不建立万能认证 / 状态机；共享只停留在协议事实与无业务视觉语义；
- 不改写 idle 与 logout 的既有安全语义，不借回流修复增加隐式 end-session；
- `SystemConfigStorageCoordinator.cs` 的三处既有 `DateTime.Now` 继续留在独立维护线。

`R3-F02-A` 已按确认方案完成实现与静态测试，详见[A 批实现记录](/records/f4-r-r3-f02-a-oidc-return-trust-gate-implementation-2026-08-13)；Flutter `crypto 3.0.7` 依赖变更已单独授权。`R3-F02-B` 也已关闭 Settings / Profile 权威状态，详见[B 批实现记录](/records/f4-r-r3-f02-b-self-service-authoritative-state-implementation-2026-08-15)。下一步等待确认 `R3-F02-C 错误与路由边界` 精确方案；最终真实 OIDC、自服务和错误路由矩阵仍在 C 批关闭后成组验收。
