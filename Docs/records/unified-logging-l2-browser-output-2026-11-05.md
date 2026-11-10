# L2 浏览器日志最终输出实施与本地验收

> 日期：2026-11-05（Asia/Shanghai）。结论：**本批完成**；L2 整体未关闭。方案见[浏览器输出收口](../features/unified-logging-l2-browser-output-plan.md)，现行契约见[前端日志说明](../guide/frontend-logging.md)和[事件契约第 74 节](../features/unified-logging-contract.md)。

## 1. 授权与候选

所有者先要求按当前规划启动准备，随后明确“批准，按这份方案开始实现”。范围包含公共子路径、固定事件目录、自由文本省略、三级 / diagnostic、HTTP 语义回归、本地验证、文档及 dev 常规提交。

准备基线为 `1551ad5242f2c18e0c76611c80b5222d190d6ccc`；实施在 dev，使用现有 Node `v24.16.0` 和已安装依赖。实际代码由包含本记录的提交保存，工作区源码与验证日志 SHA-256 清单另存本地证据目录。没有安装或更新依赖，没有启动服务 / 容器、真实浏览器 Smoke、外部通信或生产操作。

## 2. 完成内容

- `@radish/http/logging` 提供公共工厂、安全摘要和类型；Client / Console 的 logger 仅绑定固定来源、目录与 env，旧 sanitizer 路径仅重导出。没有新增 workspace、外部包或 lockfile 变更。
- Client 328 项、Console 153 项已审阅固定说明，覆盖两应用当前 484 处直接调用。动态翻译、模板字符串、条件说明及可变标签改为固定说明与受控字段；既有调用签名保留。48 处日志调用调整不改变周围业务分支或用户提示。
- 普通应用事件按三级使用固定编码，结合精确 tag / message 定位；HTTP 使用五个专用事件编码。未知输入不回显，字段按当前事件的类型、范围、枚举取值；完整实体、认证对象、正文、身份及财务明细不保留。
- 11 处既有 Info 中六处过程日志转为 debug / diagnostic，五处状态与结果保留 Info。诊断必须同时满足 env.isDev 与 env.debug；生产 runtime debug 不再放开诊断。
- Error 文本访问器、原始堆栈、自由对象不直接输出；允许安全资源类别与行列号。最终 console 只接收固定格式串和序列化快照，限制参数、属性、异常、帧与 8 KiB UTF-8 大小；超限先保留说明、裁剪位置，标记 truncated。
- 实际 error-handler / apiFetch 接线保持原回调、参数身份、响应和错误对象、一次刷新重放、失效事件及 idle_session_expired 原因。输出器故障和日志反射重入不干扰业务控制流。

## 3. 验证结果

| 验证 | 结果 | 证明范围 |
| --- | --- | --- |
| 准备期旧 sanitizer 基线 | 9 / 9 | 旧敏感字段测试通过，但不能覆盖最终输出缺口 |
| 本批日志专项 | 52 / 52 | 公共策略、真实应用 logger、真实 HTTP 模块与目录覆盖；为下列 workspace 测试的子集，不重复计入总数 |
| `npm test --workspace=@radish/http` | 81 / 81 | HTTP 契约、日志、刷新与错误处理回归 |
| `npm test --workspace=radish.client` | 562 / 562 | Client 原有测试及新增 logger 接线 / 目录覆盖 |
| `npm test --workspace=radish.console` | 143 / 143 | Console 原有测试及新增 logger 接线 / 目录覆盖 |
| HTTP type-check | 通过 | `tsc --noEmit` 覆盖 src |
| Client 显式 app / node type-check | 通过 | 不以根 tsconfig 空覆盖代替应用检查 |
| Console 显式 app / node / strict type-check | 通过 | 分别确认应用、Vite 配置和既有 strict 子集 |
| Client / Console build | 两者通过 | `tsc -b && vite build`，公共源码子路径能参与真实生产打包 |
| `lint:changed` + HTTP 完整 Lint | 通过 | changed-only 仅覆盖 UI / Client / Console，HTTP 另跑；零 ESLint warning / error |
| Repo Quality / docs / diff | 通过 | 文本卫生、根入口同步、文档本地链接、`git diff --check` |

三 workspace 合计 **786 passed / 0 failed / 0 skipped**。日志专项包括两应用 Development / Production × debug 开 / 关、全部 481 项目录的真实出口回放、tag / message / Error / 键名合成标记、访问器与 Proxy、原型键、循环 / 超深对象、输出故障 / 重入、辅助计时容量、字节预算与 HTTP 行为。

另对 28 个触达的业务源码文件与准备基线进行 AST 对照：48 处日志调用有变化，排除日志调用后的语法树无差异。此检查确认业务源码改动范围，不替代实际行为测试；公共日志模块和测试自身不纳入这项等价比较。

Node 行为测试使用已有 TypeScript 在内存转译真实产品模块，替换 env、console、fetch 和 window；没有复制 logger 实现当测试对象。目录覆盖测试是 AST 静态检查；它与行为证据分开。原生浏览器 / PC / mobile、真实网络登录和服务运行不在本批证据内。

两套 build 均有大 chunk 提醒：Client 超过配置的 800 kB、Console 超过 500 kB。构建退出码均为 0；本批未调整阈值或扩入打包拆分，也未用本次结果证明体积相对基线无变化。

## 4. 开发中失败与修正

- 初次 Client 全量回归 561 passed / 1 failed：ForumPostComposer 既有国际化契约禁止源文件包含中文；新增固定日志说明触发此检查。改用固定英文说明并同步目录后，定向与完整回归通过，没有削弱原断言。
- 补跑 HTTP 完整 Lint 发现辅助 table 参数未使用；明确保留调用签名并忽略数据，修正后通过。没有为了跳过检查修改 ESLint 规则。
- 复核发现超量异常与位置裁剪需要明确标记，补充 truncated，并在 8 KiB 超限时优先保留固定业务说明；新增参数反射重入与数组索引 getter 回归。最终测试和构建均针对修正后的代码重新执行。

## 5. 证据与剩余边界

本地证据目录为 `artifacts/logging-l2-browser/2026-11-05/`，包含三 workspace 完整测试、52 项专项、两应用 build、28 文件范围复核、47 份源码 / 测试 / package 输入与日志 SHA-256 清单。显式类型检查、Lint 和 Repo Quality 结果也由本次命令输出确认。目录为本地忽略产物，不作为仓库内可分发附件。

- 没有浏览器日志遥测上传；本地安全快照不作为服务器 RuntimeLogEvent 入库协议，不伪造租户、部署或 trace。
- 第三方 SignalR / 浏览器自身日志未接管；JavaScript 实参在进入 logger 前的求值，以及已有业务错误解读 / 非 Error 包装仍保持原边界。
- 本批只关闭浏览器公共出口切片，不替 L1 传输上界 / 磁盘故障、L2 其余来源、L3–L6、Native 平台或真实 Smoke 担保。生产候选开关不变。
- 临时清单探针未进入产品目录；保留本地测试 / build 证据。没有后台服务需要停止，没有新增分支、PR、tag、Release、镜像、部署或 push。
