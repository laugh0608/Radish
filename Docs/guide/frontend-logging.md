# 前端日志与敏感字段脱敏

> 状态：L2 浏览器公共出口、三级与安全规则已实施；自动化验收范围见[本批记录](../records/unified-logging-l2-browser-output-2026-11-05.md)
>
> 最后更新：2026-11-05（Asia/Shanghai）

本文维护 `radish.client`、`radish.console` 与 `@radish/http` 的浏览器日志契约。整体目标见[统一日志专题](../features/unified-logging-governance-design.md)，本批范围见[实施方案](../features/unified-logging-l2-browser-output-plan.md)。浏览器日志不自动上传，后端候选开关不控制浏览器输出。

## 入口与职责

| 对象 | 入口 / 职责 |
| --- | --- |
| 公共实现 | `@radish/http/logging` 子路径；`src/browserLogger.ts` 负责模式与最终 console，`src/logSanitizer.ts` 负责安全摘要 |
| Client / Console | 各自 `src/utils/logger.ts` 绑定固定应用来源、事件目录与 env；业务继续使用 `log` |
| 固定事件目录 | 各自 `src/utils/browserLogCatalog.ts`；精确匹配固定 tag / message，逐事件登记字段规则 |
| 共享 HTTP | `client.ts` 刷新失败及 `error-handler.ts` 四个错误入口使用同一公共安全实现，无需等待应用初始化 |
| 旧 sanitizer 路径 | 两应用 `utils/logSanitizer.ts` 仅重导出公共实现，不再维护独立规则 |

业务禁止直接使用 console 输出。既有 `log.debug / info / warn / error` 调用签名保留；新调用的标签与说明必须是固定、安全的字面量，并同步登记。目录覆盖测试会拒绝未登记说明、动态说明和闲置条目。不要把翻译资源、服务端消息或全仓字符串自动导入信任目录。

## 模式与三级

| 内容 | Development + debug | Development 无 debug | Production（包括 runtime debug=true） |
| --- | --- | --- | --- |
| 实际结果 / 状态 Info | 精简 Info | 精简 Info | 精简 Info |
| debug | Info + diagnostic=true | 不处理载荷、不输出 | 不处理载荷、不输出 |
| warn / error | Warning / Error | Warning / Error | Warning / Error |
| group / table / time 系列 | 受控诊断辅助事件 | 不输出 | 不输出 |

应用适配层通过 `env.isDev && env.debug` 打开诊断。`log.debug` 是兼容入口，不再是第四种等级。初始化过程、搜索过程、调试定时器和登出开始已归入 diagnostic；刷新成功、登出完成等明确状态变化保留精简 Info。

辅助方法不再调用原生 group / table / time 输出任意标签或表格。它们记录固定动作摘要；time / timeEnd 仅接受登记的单消息标签，最多保留 32 个活动计时，重复、未开始和超限分别标记。当前业务没有辅助方法调用，不据此声称验证了真实页面计时交互。

## 安全输出

- 最终 console 参数为固定 `%s` 格式和一个序列化 JSON 快照；不传原始对象，后续修改对象不会改变已生成输出。
- 固定说明从相应应用目录取值。动态模板、用户输入、翻译结果和服务端 message 不作为说明输出；未知输入转为 `browser.log_unregistered`，保留严重程度、应用来源与省略标志。
- 只有当前事件登记的字段可输出，且须通过整数范围、布尔或封闭枚举校验。数字也不能脱离字段语义放行；不递归复制任意业务对象。
- 密码、口令、Token、Authorization / Cookie、完整认证对象、请求 / 响应正文、私聊 / 文档正文、用户身份与财务明细不输出。动态字段名、文件名、URL 和原始异常文本均省略；未知内容不会通过开启诊断恢复。
- `sanitizeLogValue / sanitizeLogArgs` 保留名称，语义已改为安全摘要。旧测试中任意 displayName / operator / message 原样保留的行为不再成立。
- Error 仅识别固定内建类型分类。不调用 name / message / stack / cause 的访问器或 toString / toJSON。栈仅解析自身数据属性，允许已知应用资源类别与行列号，不返回函数名、文件名、主机、绝对路径、query 或 hash；访问器栈和未知格式省略。
- 反射失败 / 撤销 Proxy 不能使日志抛出。字段只读取自身数据属性，不遍历未知对象键或访问器；恶意 Proxy trap 的自身副作用不属于日志工具可隔离的执行环境。
- 最多检查 32 个参数、保留 32 个业务属性、4 个异常摘要（每个最多 32 帧），不遍历业务对象图；输出结构最深为 4 层容器嵌套。单条 UTF-8 JSON 不超过 8 KiB。超限先去除位置细节并标记 truncated，仍超限时输出固定裁剪事件。
- console 被覆盖而抛错时，只隔离日志出口失败，不重试原文，不改变业务异常传播；重入不递归。此行为不保证覆盖任意第三方独立 logger、SignalR 内部输出或浏览器自身诊断。

JavaScript 会在调用 logger 前求值函数实参；诊断关闭只保证 logger 不再反射收到的载荷，不保证阻止调用方已经执行的计算。现有 HTTP 业务对刷新错误的解读、非 Error 抛出值的既有包装也不因本批变成新的输入协议。

## HTTP 事件与兼容

| 事件 | 允许上下文 |
| --- | --- |
| `http.operation_failed` / `http.network_failed` | 内建异常分类与受控位置 |
| `http.api_failed` | 已登记 API code；其他值标记 unknown |
| `http.response_failed` | 100–599 的整数 status |
| `http.token_refresh_failed` | network_error / invalid_token / server_error / unknown |

应用普通事件使用 `browser.application_info / application_warning / application_failed`，结合固定 tag / message 定位操作；debug 使用 Info 编码加 diagnostic。HTTP 保留现有 Error 严重程度，本批不重划业务错误告警类别。

安全快照只供浏览器本地 console 使用，不充当服务器 `RuntimeLogEvent` 入库协议，也不伪造部署、租户、trace 或采集身份。原 HTTP 返回、回调次数与参数、一次刷新重放、认证失效通知和用户提示保持；日志快照不传给业务回调。

## 验证与维护

变更公共实现、事件目录或 HTTP 输出时，运行三个 workspace 的测试、显式应用类型检查、Client / Console build 和相关 Lint。最终出口测试执行真实模块并替换 env / console / 网络边界；源码扫描仅证明目录覆盖，不代替行为测试。

`lint:changed` 当前没有收录 `@radish/http`，本批另外执行 `npm run lint --workspace=@radish/http`。其他验证覆盖限制见[验证基线](./validation-baseline.md)。测试包括生产 runtime debug、合成敏感标记、访问器 / Proxy、输出预算与故障、HTTP 返回及 Token 失效；本批没有启动服务或执行 PC / mobile 真实 Smoke。

## Node 静态宿主

`Frontend/scripts/serve-static.mjs` 通过 `logging/runtime-output.mjs` 记录生命周期和请求失败，统一路径由 `RadishLogging__Enabled=true` 显式启用，默认关闭；配置、三级及开发诊断限制见[事件契约](../features/unified-logging-contract.md)。Node 运行适配与唯一 JSON 策略随 Frontend 镜像复制，浏览器包不使用这个服务器输出入口。

请求拒绝 / 失败调用点不再传路径、IP、转发头或异常原文；健康检查不逐次输出。候选路径序列化单行 JSON，失败进入有限安全应急摘要；旧终端路径只显示固定事件码。Node、浏览器、Native 和其余来源分别验收，本批不改变 Node 接线。
