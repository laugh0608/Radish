# ASP.NET Core DataProtection 日志治理（2026-10-15）

## 结论与范围

本批按已确认方案完成 L2 DataProtection 生成端治理：20 个精确来源、78 个已核对调用点的命名身份，以及各来源 unknown，共 98 个固定编码。共享入口增加桥接前保护，两条输出链在上下文合并后重建安全事件。保持密钥生成、轮换、撤销、持久化、Cookie 与加解密的框架语义。

Auth 启动回调、RuntimeExtension 裸输出、其他非日志诊断通道未扩展；未安装依赖、还原包或启动项目服务。`RadishLogging.Enabled` 默认 false，L2 未整体完成。稳定契约见[第 69 节](../features/unified-logging-contract.md#69-l2-aspnet-core-dataprotection-日志)。

## 接线与源码核对

- Auth 的 MVC / antiforgery / Cookie、API 的 Hangfire Cookie、Gateway 的 RazorPages 均注册 DataProtection provider、key manager 与预载 hosted service。只读核对使用本机程序集构造 ServiceCollection，不构建或启动产品宿主，不读取已有密钥库。Gateway 虽无显式 `AddDataProtection`，仍属于实际注册路径。
- 核对版本为 `Microsoft.AspNetCore.App 10.0.8`。官方 `dotnet/aspnetcore v10.0.8` 标签的 LoggingExtensions 共 79 个声明，其 Id / Name / Level 与本机程序集元数据一致；调用点审计发现 78 个来源与命名身份组合，以及两个无命名日志调用。52 / 79 只有声明，未发现调用点，不纳入已启用身份。
- 78 个组合不是 78 个始终会执行的事件。Windows、证书、容器、read-only 配置等依赖平台和配置；保留这些已核对精确来源的适配，不推定当前 macOS 运行了这些分支。
- ASP.NET Core DataProtection 与 OpenIddict DataProtection 集成包不同。后者随 7.2.0 包依赖存在，但当前没有注册对应能力。

## 精确来源与事件分组

下表来源均位于 `Microsoft.AspNetCore.DataProtection.` 下；编码均以 `data_protection.` 开头。每个来源另有一个 `<分组>.unknown`。完整 Id + Name 到编码映射位于 [DataProtectionLogIdentity.cs](../../Radish.Extension.Log/DataProtectionLogIdentity.cs)，真实生成器与期望身份独立列于 [DataProtectionLoggingTestSupport.cs](../../Radish.Api.Tests/ErrorHandling/DataProtectionLoggingTestSupport.cs)。

| 精确来源后缀 | 命名事件 Id | 编码分组 |
| --- | --- | --- |
| `TypeForwardingActivator` | 无；Debug 无命名调用 | `type_activation` |
| `EphemeralDataProtectionProvider` | 45 | `ephemeral_provider` |
| `XmlEncryption.NullXmlEncryptor` | 44 | `null_encryptor` |
| `XmlEncryption.DpapiXmlEncryptor` | 26 / 54 / 55 | `dpapi_encryptor` |
| `XmlEncryption.DpapiNGXmlDecryptor` | 42 / 43 | `dpaping_decryptor` |
| `XmlEncryption.DpapiXmlDecryptor` | 43 / 51 | `dpapi_decryptor` |
| `XmlEncryption.CertificateXmlEncryptor` | 28 / 29 / 30 | `certificate_encryptor` |
| `XmlEncryption.DpapiNGXmlEncryptor` | 27 / 56 | `dpaping_encryptor` |
| `KeyManagement.DefaultKeyResolver` | 12 / 13 / 14 / 53 / 73 | `key_resolver` |
| `KeyManagement.XmlKeyManager` | 15 / 16 / 17 / 18 / 19 / 20 / 21 / 22 / 23 / 24 / 25 / 32 / 33 / 34 / 35 / 36 / 58 / 59 / 61 / 62 / 63 / 64 / 78 / 80 | `key_manager` |
| `KeyManagement.KeyRingProvider` | 1 / 2 / 46 / 47 / 48 / 49 / 57 / 72 | `key_ring` |
| `KeyManagement.KeyRingBasedDataProtector` | 5 / 6 / 7 / 8 / 31 | `protector` |
| `Internal.KeyManagementOptionsPostSetup` | 61 / 62 / 63 / 64 | `options` |
| `Internal.DataProtectionHostedService` | 65 / 66 | `preload` |
| `Repositories.FileSystemXmlRepository` | 37 / 38 / 39 / 60 / 74 / 75 | `file_repository` |
| `Repositories.EphemeralXmlRepository` | 50 | `memory_repository` |
| `Repositories.RegistryXmlRepository` | 40 / 41 / 76 / 77 | `registry_repository` |
| `AuthenticatedEncryption.ManagedAuthenticatedEncryptorFactory` | 10 / 11 | `managed_encryptor` |
| `AuthenticatedEncryption.CngCbcAuthenticatedEncryptorFactory` | 3 / 4 | `cng_cbc_encryptor` |
| `AuthenticatedEncryption.CngGcmAuthenticatedEncryptorFactory` | 9 | `cng_gcm_encryptor` |

`FileSystemXmlRepository` 另有 Trace 无命名调用，进入 `file_repository.unknown`。`TypeForwardingActivator` 的 Debug 无命名调用进入 `type_activation.unknown`。61–64 在 XmlKeyManager 与 OptionsPostSetup 中重复使用不同名字；43 在两个 decryptor 来源中使用相同名字；匹配必须同时核对精确来源、整数 Id 和字符串 Name。

## 输出保护与边界

1. `AddSerilogSetup` 保留 Serilog 单例工厂的原创建方式，并依次包裹 DataProtection、OpenIddict、Health 保护；未命中类别直接返回原 logger。AddProvider、scope、IsEnabled 与 Dispose 所有权保持原有行为。
2. `DataProtectionLoggerFactory` 不枚举或格式化原 state，不调用 formatter，不读取异常 Message / Data / ToString。仅依据可信事件身份与异常类型生成安全 state；未知身份替换为固定 EventId，防止未知名称进入桥接；无异常时显式写入 null failureKind，阻止 scope 借此补入伪异常分类。
3. `DataProtectionLogSink` 覆盖候选 Runtime 输出、旧链代码 / 配置 WriteTo 和 AuditTo。在上下文合并后丢弃原消息、异常及所有非允许字段，只保留固定 EventCode、精确 SourceContext、infrastructure、Diagnostic、固定 failureKind 与原时间 / 等级 / 强类型 trace、span。重复、缺失或错误类型的 EventId 字段落入该来源 unknown。
4. XML 全文、密钥材料 / 标识、purposes、撤销理由、目录 / 文件 / 注册表路径、证书身份、算法 / 类型名与 scope 业务字段均不输出。前置保护不能消除框架调用日志前已完成的字符串 / XML 副本构造，也不阻止任意外部 enricher、destructurer 或独立 logger 的提前读取；scope 自身仍沿原桥接传递。
5. 低于 Warning 一律 diagnostic。候选 Production 或 Development 未开启 Diagnostics 时过滤；旧链保留原等级并遵循其原 minimum。Warning / Error 与未知高等级事件保留。预载失败 66 保留 Information，不升级成宿主启动失败；候选链同样按 diagnostic 过滤。普通 WriteTo 消费 sink 异常，AuditTo 仍传播 AggregateException。

## 操作与完成边界

- XmlKeyManager 58 创建、22 / 36 撤销、33 escrow 写入以及 FileSystem 39 写入发生在持久化完成前，编码使用 started；不伪造 created / persisted / completed。持久化失败时不修改返回、缓存失效或异常传播顺序。
- XmlKeyManager 24 的 XML 摘要与 25 的完整 XML 诊断均被安全重建。元数据损坏的 key 仍记录后跳过；descriptor 延迟导入失败仍抛出；revocation 处理失败 21 仍中止读取。不能用“都被 catch”推定它们有相同失败结果。
- .NET 10 密钥环可能在异步刷新期间返回旧缓存。撤销行为测试显式等待真实 RefreshCurrentKeyRing 完成，再核对拒绝与 DangerousUnprotect 的 revoked 标志；不声称撤销在下一次调用立即可见。轮换仍保留旧密钥，后继激活时间由原框架决定。
- 刷新故障测试通过真实 GetCurrentKeyRingCore 的时间参数触发过期，等待原刷新任务完成；验证原 IOException、47 Error、临时缓存延期与后续缓存命中。只驱动测试调用的时间，不修改进程时钟或框架开关。
- 预载只直接调用内存 DI 中该服务的 StartAsync / StopAsync，不启动产品宿主。读取失败被预载消费，随后的内存恢复可成功；该结果不是 Gateway / Auth / API 的完整启动验收。
- 文件测试在本次创建的临时目录中先制造最终 `.xml` 目标冲突，验证 39 先于最终移动 / 复制成功；随后制造删除失败，验证 75 与 false 返回。finally 清理测试目录，不触碰产品密钥目录。

## 验证

| 检查 | 结果 |
| --- | --- |
| 全解决方案 `dotnet build Radish.slnx -c Debug --no-restore --warnaserror -m:1 -nr:false -p:UseSharedCompilation=false` | 通过，0 warning / 0 error |
| 新增 DataProtection 三个测试类 | 59 个通过，包含 78 个命名身份在四模式中的真实生成器调用 |
| 新增用例 + RuntimeLog / SelfLog / Health / OpenIddict / Authentication / 自有 Auth 日志回归 | 473 个通过，0 failed / skipped |
| `npm run check:logging-contract` | 37 个通过，含 4 个新增跨 .NET / Node 共享样本 |
| `npm run check:docs`、`npm run check:repo-hygiene:changed`、`git diff --check` | 通过；记录索引原已超过 300 行软上限，本批为 316 行，未触及硬上限 |

覆盖旧链 / 候选链 × Development / Production；毒化 state / formatter / exception、20 来源 unknown、错误 / 重复身份、scope 冲突、强类型 trace / span、旧链 WriteTo / AuditTo 重建与输出失败传播，以及真实共享宿主 Build / Dispose。行为测试覆盖真实内存 protect / unprotect、不同用途 / 篡改 / 缺失密钥拒绝、撤销与显式允许已撤销密钥、畸形 key / revocation、延迟 descriptor 导入、创建 / 撤销存储失败、预载消费与恢复、自动生成关闭、过期 fallback、后继轮换、缓存刷新失败与延期、临时文件操作失败及证书 resolver 失败。

执行入口为 `Radish.Api.Tests/bin/Debug/net10.0` 中的 xUnit 进程内 runner；外层 subprocess 设置 90 秒超时。所有最终验证在沙盒中完成，无需为实现或测试提权。

```bash
dotnet Radish.Api.Tests.dll -noLogo -noColor -reporter verbose -longRunning 15 \
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

- Windows CNG、DPAPI / DPAPI-NG、Registry 仅调用安装版本的真实日志生成器；RegistryKey 参数使用 null，不打开 Windows 注册表。证书路径用失败 resolver 替身，不访问系统证书库，不宣称完成真实证书加解密或平台验收。
- 内存密钥 / 仓库和受控临时文件不替代真实部署密钥库、多进程轮换、磁盘耗尽、网络存储、生产 Cookie 登录或宿主启动验收。测试不绑定端口，不访问真实数据库。
- Auth ApplicationStarted 的 16 条 banner / 配置摘要与 RuntimeExtension 裸输出仍待独立确认。IdentityModel、外部 EventPipe / DiagnosticListener / Activity / metrics 不因本批受控；DbMigrate 命令结果 stdout 也不在此范围。
- 生产链未切换，L1 剩余门禁、L2 整体收口与 L3–L6 仍后置。
