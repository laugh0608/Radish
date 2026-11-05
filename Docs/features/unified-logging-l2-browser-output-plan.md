# L2 浏览器日志最终输出收口方案

> 2026-11-05（Asia/Shanghai）。方案状态：**已批准实施**；公共实现、调用迁移和本地自动化验证已完成，结果见[实施记录](../records/unified-logging-l2-browser-output-2026-11-05.md)。准备核对与旧实现事实保留在第 1 节，现行规则见[前端日志说明](../guide/frontend-logging.md)。
>
> 本页属于[统一日志专题](./unified-logging-governance-design.md)的 L2 实施切片。当前实现事实见[前端日志说明](../guide/frontend-logging.md)，整体执行顺位见[当前规划](../planning/current.md)。不另设日志主线。

## 1. 目标与准备结论

统一 `@radish/http`、Client、Console 的浏览器日志安全出口，落实三级与 diagnostic；最终 console 参数只能包含受控事件快照。保留排障所需的固定事件说明、来源、允许字段和明确的省略标记，避免把未知内容原样交给浏览器控制台。

准备基线：`1551ad5242f2c18e0c76611c80b5222d190d6ccc`，分支 `dev`。本轮未安装依赖、未启动服务、未访问生产。

| 核对对象 | 已确认事实 | 本批处理 |
| --- | --- | --- |
| 三份 sanitizer | 敏感字段名可递归替换；裸字符串、Error.name / message / stack 原样保留，Object.entries 会读取 getter | 收敛为一份公共策略，禁止任意对象直接透传 |
| 两份 logger | message / tag、group / time 标签绕过 sanitizer；使用 DEBUG / INFO / WARN / ERROR 四级 | 统一安全出口与模式规则，保留旧调用签名 |
| 运行模式 | logger 仅用 env.debug；runtimeConfig.debug 可在生产构建开启诊断 | 日志适配层以 env.isDev 且 env.debug 决定诊断，生产不能被 debug 覆盖 |
| 共享 HTTP | client.ts 一处刷新失败输出；error-handler.ts 四处输出，部分直接携带 code / message | 五处改为固定事件及事件级允许字段 |
| 现有测试 | 三个 workspace 的九项测试只验证 sanitizer，没有调用 logger 最终出口 | 增加真实适配入口到 console 的行为测试 |

本轮使用 Node `v24.16.0` 执行上述九项测试，9 passed / 0 failed。另用已有 TypeScript 将两份真实 logger 在内存中转译，以 env 替身和 console 捕获执行 Production × debug=false / true 四组探针：四组均输出合成私密标记、均会因参数 getter 抛错而中断；两组 debug=true 均输出 debug。探针没有修改产品文件，结果是模块行为证据，不是浏览器 Smoke 或真实凭据泄露证据。

AST 定位当前两套应用直接使用 `log / logger` 的调用共 484 处（Client 331，Console 153）；其中 Client 22 处首参不是静态字面量。另有第二参数模板字符串等动态输入，不能以首参静态推定整条消息安全。未发现业务使用 group / table / time 系列方法。计数仅覆盖这两种直接调用写法，不宣称覆盖第三方输出或任意别名。

## 2. 公共实现与改动范围

1. 公共安全处理和输出工厂放在现有 `Frontend/radish.http/src/`，通过新增 `@radish/http/logging` 子路径导出；不新增 workspace、包或外部依赖。该子路径保持纯 TypeScript，不引用 React、应用 env、Node 宿主或 HTTP 请求初始化。
2. Client / Console 的 `utils/logger.ts` 只负责绑定固定应用来源、各自事件目录和 env 模式。两份 `utils/logSanitizer.ts` 移除重复实现；若保留既有导入路径，仅作明确的迁移重导出，不形成三套策略。
3. `client.ts`、`error-handler.ts` 直接使用同包公共输出；共享 HTTP 的五个错误事件不依赖应用初始化顺序，不增加 HTTP 客户端配置或全局可变诊断开关。
4. 为两套应用建立经过审阅的固定事件目录：只收录当前调用实际使用的安全固定标签、说明及必要字段规则。提取脚本可用于列清单，不得直接把全仓字符串或翻译资源自动当作可信模板。
5. 修改现有动态日志表达式为固定说明与受控字段；涉及论坛 / 文档上传、评论与商城翻译日志、profile、notificationHub、TokenService、StickerBatchUpload 等已定位调用。仅调整日志参数，不改页面、业务分支、API 或用户提示翻译。
6. 审阅现有 11 处 info：初始化过程、搜索过程与调试定时器归为 diagnostic；明确业务结果 / 状态变化保留精简 Info。对触达的示例，只修正真实存在的旧输出用法；不为凑范围修改无日志的 `UIComponentsExample.tsx`。

业务日志迁移限于上述公共规则生效所需的调用和目录；不趁机重构认证、聊天、商城或整套日志调用 API。新增日志优先固定事件定义，既有 `log.debug / info / warn / error` 重载保持可调用。

## 3. 输出规则与兼容取舍

### 3.1 固定文本与动态载荷

- 只有在对应应用事件目录中精确匹配的固定标签 / 消息，才能作为说明输出；实际输出取目录中的常量。不得用“字符串较短”“不含 token 字样”或宽泛正则认定自由文本安全。
- 动态模板、服务端 message、翻译结果、任意 Error 文本均不是受信模板。现有动态调用在本批转换为固定说明；用户提示仍使用原来的业务消息。
- 未登记输入产生固定 `browser.log_unregistered` 摘要，保留应用来源、原请求严重程度和省略标志，不回显原 tag / message / 参数。该行为只处理未知输入，不以大量业务日志降为同一句未知摘要作为验收通过。
- 现有静态调用必须逐项审阅并登记，或者在本批迁移到对应固定事件；增加静态覆盖检查，阻止已知调用漏登记以及新增动态消息悄悄恢复透传。
- 普通参数不再按任意键递归复制。只保留事件明确允许的字段及其类型 / 范围，例如 HTTP status、计数、耗时、枚举结果；未知键和值、请求 / 响应正文、认证对象、文件名、用户输入、身份与财务明细省略。数字和布尔值也不能脱离字段语义自动放行。
- 原有 `sanitizeLogValue / sanitizeLogArgs` 名称若保留，语义改为安全摘要；既有“displayName / operator / 任意 message 原样保留”断言相应更新，不能继续当作兼容要求。

这会减少原来可见的自由文本和对象详情，是本批明确的行为变化。排障依赖固定事件、已审核字段和位置摘要；不能通过打开 debug 恢复原始敏感载荷。

### 3.2 异常、访问器与大小边界

- 不直接读取不可信 Error 的 name / message / stack / cause，不调用 toString / toJSON，不遍历 getter；可识别的内建异常仅映射固定 failureKind，其他值用固定 unknown。Error 附带业务对象不解构。
- 受控属性通过自身数据属性描述符读取；访问器跳过，反射失败或撤销 Proxy 使用固定不可读取标记。对象异常不得改变 HTTP 返回或阻止原错误回调。
- 如需栈定位，只解析数据属性中的栈字符串；仅接受已知应用资源格式，将资源映射为固定类别并保留有界行列号，函数名、源行、绝对地址、query、hash 和未知格式全部省略。访问器形式不读取。Development / Production 使用相同安全规则，不以生产模式统一删除可安全表达的位置。
- 公共处理设置深度、属性数、数组长度与总输出预算；本批采用单事件 8 KiB UTF-8 JSON、最多 32 个受控属性、深度 4、数组最多 32 项、位置最多 32 帧。达到预算时输出固定裁剪标志；测试实际传给 console 的完整事件大小。
- console 仅接收构建完成的不可变安全快照，不传原对象引用，避免浏览器延迟展开显示后续变更。使用固定格式参数，输入不能成为 `%c / %s / %o` 控制串。
- 输出器抛错仅在日志边界内隔离，不重试输出原载荷，也不吞掉业务异常；调用方原有错误回调若抛错，仍按原语义传播。

### 3.3 三级、模式与辅助方法

| 调用 / 模式 | Development 且 debug 开 | Development 且 debug 关 | Production（包括 debug 开） |
| --- | --- | --- | --- |
| 明确业务结果 Info | 精简 Info | 精简 Info | 精简 Info |
| debug / 显式 diagnostic | Info + diagnostic=true | 不输出、不检查载荷 | 不输出、不检查载荷 |
| warn / error | Warning / Error 安全摘要 | 同左 | 同左 |
| group / table / time 系列 | 受控诊断辅助事件 | 不输出 | 不输出 |

`log.debug` 作为调用兼容入口保留，不再生成 DEBUG 等级；warn 规范化为 Warning。未发现业务使用自有 LogLevel 枚举，实施时复核后统一为三级，不修改 SignalR 自己的 LogLevel。

辅助方法保留签名，但不向 console.group / table / time 传任意原始标签或表格。以固定事件表达动作；table 只输出受控摘要。time / timeEnd 仅对登记标签维护有界计时状态（最多 32 个活动项），重复开始、未开始结束及超限使用固定状态标志；不保存原始未知标签。未登记辅助输入同样不能回显。

## 4. HTTP 五处事件与语义保持

| 原入口 | 固定事件 | 允许上下文 |
| --- | --- | --- |
| handleError | http.operation_failed | 受控 failureKind / 安全位置 |
| handleApiError | http.api_failed | 已登记 API code 分类；未知 code 只标记 unknown |
| handleNetworkError | http.network_failed | 受控 failureKind / 安全位置 |
| handleHttpError | http.response_failed | 有效整数 status（100–599） |
| apiFetch 刷新 catch | http.token_refresh_failed | TokenRefreshErrorType 封闭枚举或 unknown |

本批保留五处现有 Error 严重程度，避免同时重划 API 错误分类。模式不影响上述错误摘要，message 不进入日志；回调仍接收原 code / message / Error。

必须保持：请求头与序列化、原 Response 返回、一次认证刷新重放、onRequest / onResponse / onError、withErrorHandling 返回与原异常传播、网络 / 服务端刷新失败返回原响应、确认失效才派发 `auth:token-expired`，以及 `idle_session_expired` 原因。日志不能修改传入对象，也不能把脱敏对象送给业务回调。

日志安全与业务解读分开：既有业务仍会读取刷新错误类型和会话过期消息，本批不承诺把所有异常业务输入变成无 getter 的协议。回归要区分日志新引入的访问与既有业务访问，不能把协议改造藏在日志修复中。

## 5. 验收矩阵与命令

| 验收面 | 必须证明 |
| --- | --- |
| 真实出口 | 调用真实 Client / Console logger、HTTP error-handler 与 apiFetch，捕获最终 console 参数；不止单测公共 sanitizer |
| 模式 | 两应用 × Development / Production × debug 开 / 关；诊断关闭时访问器计数为零，Info 与 Warning / Error 按矩阵保留 |
| 敏感输入 | tag、message、数组、键名、Error 各字段、嵌套对象、URL / 编码内容 / 格式指令中的合成标记在最终输出均不可检出 |
| 恶意与复杂对象 | getter、toString / toJSON、Proxy 失败、循环、深层 / 超大对象、原型污染键与日志输出器抛错；调用不因日志中断、原对象不变 |
| 有用性与覆盖 | 静态调用已登记，动态调用已迁移；各事件保留必要上下文，未知内容明确省略，不以所有日志清空通过测试 |
| 异常位置 | 受支持资源格式只留下受控位置；未知格式、绝对路径、查询参数、伪造函数名与访问器栈均不透传 |
| HTTP 行为 | 回调次数 / 原参数身份、原返回 / 抛出对象、刷新成功重放、失效事件及网络 / 服务器错误回归；使用内存 fetch / window 替身，不访问真实网络 |
| 打包与兼容 | 公共子路径能被现有 Node 测试及 Vite 构建解析，两应用导出和旧调用签名有效，无新增外部依赖 |

沿用现有 node:test 与 TypeScript，不新增测试框架。适配层测试可以使用已有 TypeScript 在内存中转译，并替换 env / console / 网络边界；必须执行真实产品模块，不能复制 logger 逻辑当被测对象。源码断言仅用于调用目录覆盖，不能替代 HTTP 动态行为测试。

本批执行下列命令，实际数量和警告见实施记录：

```bash
npm test --workspace=@radish/http
npm test --workspace=radish.client
npm test --workspace=radish.console
npm run type-check --workspace=@radish/http
npm exec --no --workspace=radish.client -- tsc -p tsconfig.app.json --noEmit
npm exec --no --workspace=radish.client -- tsc -p tsconfig.node.json --noEmit
npm exec --no --workspace=radish.console -- tsc -p tsconfig.app.json --noEmit
npm exec --no --workspace=radish.console -- tsc -p tsconfig.node.json --noEmit
npm exec --no --workspace=radish.console -- tsc -p tsconfig.strict.json --noEmit
npm run build --workspace=radish.client
npm run build --workspace=radish.console
npm run lint:changed
npm run lint --workspace=@radish/http
npm run check:repo-quality:changed
git diff --check
```

类型检查显式覆盖应用配置，避免现有根 tsconfig 空覆盖误判。依赖缺失时记录阻塞，不自行安装。文档准备阶段只执行已有定向基线、只读探针与文档检查。

## 6. 退出、文档与授权边界

退出条件：本页五类入口完成公共接线，已知动态输入完成迁移，所有输出受控且保留有效说明；最终输出安全、模式和 HTTP 语义有行为测试，构建 / 类型 / Lint 通过，文档准确说明兼容变化。

实施后更新前端日志说明与[事件契约](./unified-logging-contract.md)，在 records 保存候选和实际验证证据，current 只更新状态与下一步。准备阶段不把草案规则写成现有实现，不关闭 L2 整体，不改变 L1、L3–L6 与 Native 顺位。

本批不接管第三方 SignalR / 浏览器自身输出，不增加浏览器遥测上传，不改变 Node 静态宿主、后端日志开关、数据库或部署。自动化 console 捕获不替代 PC / mobile 真实 Smoke；服务启动和 Smoke 如需追加，另行说明命令、端口与清理并取得当前任务授权。

所有者已明确批准本页方案并要求开始实现。本批在 dev 完成公共子路径、固定目录、未知文本省略、三级 / diagnostic、辅助方法与验收；依赖安装、服务启动、外部通信、PR / 发布部署不包含在本批执行中。

实施采用浅层白名单重建，业务对象图不递归；既有静态说明按来源和消息精确登记，普通应用事件使用三级对应的固定编码，HTTP 使用第 4 节五个专用编码。最终输出为浏览器本地 console 快照，不接入服务器入库协议。额外补跑 HTTP 完整 Lint，因为现有 changed-only 脚本没有覆盖该 workspace。
