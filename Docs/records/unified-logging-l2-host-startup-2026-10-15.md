# Auth / API / Gateway 启动摘要治理记录（2026-10-15）

## 范围与结论

项目所有者确认三宿主启动摘要成组治理。本批已完成实现、定向验证和文档同步；规则见[事件契约第 70 节](../features/unified-logging-contract.md#70-l2-auth--api--gateway-启动摘要)。生产候选开关未开启，L2 尚未整体完成。

| 宿主 | 原回调日志调用点 | 本批补充核对 | 结果 |
| --- | --- | --- | --- |
| Auth | 16：9 banner、7 配置摘要 | BuildStartupSummary 重读配置并解析路径 | 固定摘要，记录已选择密钥模式 |
| API | 16：8 banner、8 配置摘要 | 回调外另有 1 条 JWT 初始化日志，拼接 Issuer / 证书 Subject；旧摘要还探测证书文件 | 固定摘要，复用已加载签名证书状态 |
| Gateway | 15：8 banner、7 状态 / 目标调用点 | 目标日志处于循环，运行时条数不固定；日志专用读取 / 目标列表可移除 | 固定摘要，复用 HTTPS、delegation 和健康 tags 数量 |

共 47 处回调内调用点，加 API 的 1 处同类初始化日志，合计 48 处。Gateway 循环内的调用点按一处计算，不宣称每次运行恰好输出 48 行。既有 Auth seed、API Hangfire / 文档同步及 Gateway 关闭日志不在此摘要批次范围。

## 实现与安全边界

- [HostStartupLog](../../Radish.Extension.Log/HostStartupLog.cs) 为三宿主保存受控状态与数量，ApplicationStarted 通知时生成一个 Information、非 diagnostic 的 `runtime.startup_summary`。环境映射至 development / production / staging / other，不保存自定义环境正文。
- [HostStartupLogSink](../../Radish.Extension.Log/HostStartupLogSink.cs) 只匹配 `Radish.Extension.Log.HostStartupLog`，按固定 EventCode 识别摘要，其余同来源事件进入 `runtime.startup_unknown`。覆盖旧链代码 / 配置 WriteTo、AuditTo 和候选链；不通过来源前缀或消息内容推断身份。
- 共同属性仅为宿主枚举、环境枚举、监听地址数和 CORS 来源数。Auth 的密钥模式与 API 的验签模式分别白名单；Gateway 的 HTTPS / delegation 状态及健康目标数单独白名单。跨宿主属性、任意对象、错误数值类型、负数、未知属性和原始上下文全部丢弃。
- 保留原时间、原等级和强类型 trace/span；不继承 scope 中的自由文本关联字段，不渲染异常或原消息。未知事件低于 Warning 时为 diagnostic，已知摘要遵循现有主链等级门禁。仅对已接入 sink 的输出提供重建保障，不覆盖外部独立 logger 或任意 enricher 的提前求值。
- 公共策略增加 2 个编码、5 个封闭枚举属性及 3 个非负 int 范围数量属性，并增加 4 个 .NET / Node 共享样例。

## 状态来源与完成时机

| 状态 | 数据来源与语义 |
| --- | --- |
| Auth 密钥模式 | 在原 AddServer 配置委托主体完成后记录 development-keys / file-certificates；未执行到该点为 not-initialized。不为日志提前解析 options、读取文件或访问证书库，也不把选用开发密钥当成证书可用证明 |
| API 验签模式 | 根据原加载路径得到的 jwtSigningCertificate 是否存在记录 authority / local-certificate；不再读取证书 Subject 或重新计算配置摘要 |
| Gateway 状态 | 复用实际 enableHttpsRedirection、非 Windows 删除 delegation 注册的分支结果，以及实际健康 tags.Count；retained 只表示保留注册，不承诺可用 |
| CORS / 监听 | CORS 为已解析数组长度；监听为启动通知时 app.Urls.Count，不枚举地址内容，数量不承诺外部网络可达 |
| 完成事件 | 注册本身、停止通知、未发出 ApplicationStarted 的失败路径均不输出摘要；Auth 后续状态更新不补发第二次启动摘要 |

删除 Auth / API 仅用于旧正文日志的 BuildStartupSummary 和 record；保留健康检查依赖的 Issuer / 证书路径解析方法。实际认证、证书加载、时区、健康响应和代理配置流程不变。删除 Gateway 日志专用的重复配置读取与目标列表，实际健康注册流程保留。

回调由宿主作用域持有并释放，不拥有 logger。现有候选链 `runtime.started` / `runtime.stopped` 保持独立，未扩展旧链生命周期管理。WriteTo 继续消费 sink 异常，AuditTo 继续传播；候选输出失败继续进入独立 `pipeline.output_failed` 应急通道。

## 验证

| 检查 | 结果 |
| --- | --- |
| 全解决方案 `dotnet build Radish.slnx -c Debug --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false` | 通过，0 warning / 0 error |
| .NET 定向回归 | 510 passed，0 failed / skipped；包含 25 个本批启动日志用例、4 个新增共享样例及健康检查解析回归 |
| `npm run check:logging-contract` | 41 passed |
| `npm run check:docs` | 通过 |
| `npm run check:repo-hygiene:changed`、`git diff --check` | 通过；记录索引 318 行有篇幅软提醒（原为 316 行） |

启动用例覆盖旧链 / 候选链 × Development / Production、scope 冲突、强类型 trace/span、未知事件等级门禁、错误属性与不可格式化对象 / 异常、不可枚举地址集合、后续配置读取抛错、无效路径不被日志重新解析、延迟 Auth 状态、未启动 / 已释放注册、配置 WriteTo / AuditTo、输出故障以及独立生命周期事件。原正文摘要测试改为验证真实健康解析方法，含证书文件存在时基目录优先及不存在时 content-root 回退。

执行入口为 `Radish.Api.Tests/bin/Debug/net10.0` 的进程内 xUnit runner，外层 subprocess 设置 90 秒超时：

```bash
dotnet Radish.Api.Tests.dll -noLogo -noColor -reporter verbose -longRunning 15 \
  -class 'Radish.Api.Tests.ErrorHandling.HostStartupLoggingTests' \
  -class 'Radish.Api.Tests.HealthChecks.*' \
  -class 'Radish.Api.Tests.ErrorHandling.DataProtection*' \
  -class 'Radish.Api.Tests.RuntimeLog*' \
  -class 'Radish.Api.Tests.SerilogSelfLogTests' \
  -class 'Radish.Api.Tests.ErrorHandling.Health*' \
  -class 'Radish.Api.Tests.ErrorHandling.OpenIddict*' \
  -class 'Radish.Api.Tests.ErrorHandling.Authentication*' \
  -class 'Radish.Api.Tests.Auth.AuthAccountLoggingTests' \
  -class 'Radish.Api.Tests.Auth.AuthClientLoggingTests' \
  -class 'Radish.Api.Tests.Auth.AuthIdleSessionLoggingTests' \
  -class 'Radish.Api.Tests.Auth.AuthOwnedLogSinkTests'
```

## 证据限制与后续

- 本批通知使用可控 IHostApplicationLifetime，不启动三宿主，不证明完整宿主启动成功；认证、健康框架的既有内存行为测试作为相邻回归。没有安装依赖、开启服务 / 端口、访问真实数据库、密钥库或系统证书库。
- 后续配置抛错与无效路径测试只证明摘要不重新读取它们，不改变真实配置 / 证书加载失败的既有传播路径。摘要不是登录成功、健康检查、真实监听、Windows delegation 或部署验收证据。
- Common 的程序集加载失败与延迟类型枚举失败输出继续独立确认，重点是初始化前 reporter、静态初始化递归和原有异常消费边界；浏览器共享 HTTP / logger / 示例输出另成批次，未在本批修改。
- 不从三宿主摘要收口推定所有 Program 自有日志、非 Serilog 通道或整个 Auth 已完成。生产候选开关继续为 false，L1 剩余门禁、L2 整体收口与 L3–L6 仍后置。
