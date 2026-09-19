# 2026-09-19 统一日志 L2 生成入口接入

## 本批边界

承接 [L1 采集验证](./unified-logging-l1-guarded-collector-2026-09-19.md)，完成 L2 的入口层子项：API / Auth / Gateway 配置加载后的引导与运行共享统一输出，Node 静态服务器接入同一事件策略。没有开启生产默认链路，也没有将整个 L2 标记完成。

## 实现结果

- `RuntimeLoggingSession` 在配置加载后创建唯一 Serilog logger，在宿主构建前即可接收事件；`ILogger<T>`、Serilog 静态调用和生命周期事件共用此实例。退出时释放 logger 和生命周期注册。
- `RuntimeSerilogSink` 不调用模板渲染或异常 ToString，不遍历解构对象；只取有限内建标量，再交由公共策略按键、类型和范围校验。来源来自宿主配置，trace / span 优先来自 Serilog / Activity。
- 未登记的 Information / Debug / Trace 只进入显式开发诊断；未登记 Warning / Error 保留 `runtime.unclassified` 安全摘要。Fatal 为 Error，并保留 isFatal。不能将未分类摘要当作已完成业务事件码迁移。
- `RuntimeLogOutput` 与 Node 对应实现先生成单行 JSON，再同步写输出；不增加应用内异步队列。写入 / 序列化失败不替代业务结果，应急通道只输出 `pipeline.output_failed` 和计数，每个输出实例至多每分钟一次；应急介质也失效时只保留内存计数，不递归。
- 新路径跳过旧的 ReadFrom.Configuration、终端 / 文件 / 数据库 sinks 和 SelfLog 文件；SelfLog 原文只触发安全应急计数。旧路径暂时保留，默认仍使用旧路径。
- Node 请求拒绝和失败调用点删除路径、IP、转发头及异常原文；普通健康检查不输出日志。旧文本路径同样只接收固定事件码，统一路径输出完整规范事件。
- Frontend 镜像复制 Node 运行适配及唯一策略文件，目录关系与仓库保持一致；没有另存一份手工维护的策略。

配置及调用契约见[事件契约](../features/unified-logging-contract.md)。本批没有修改业务审计写入、查询 API、告警或数据库表。

## 验证

| 验证 | 结果与范围 |
| --- | --- |
| .NET 定向测试 | 30 项通过：原契约 21 项 + 适配 9 项；覆盖 ILogger scope、Serilog、引导 / 生命周期、三级映射、诊断过滤、标记秘密、并发 JSONL、介质失败与应急限频 |
| Node 契约 / 输出测试 | 27 项通过：原契约 22 项 + 输出 5 项；覆盖循环对象、失败计数、严格配置以及复制到镜像目录结构后的模块 / 策略加载 |
| 静态服务器回归 | 4 项通过：Client / Console / health、非法请求、统一输出隐私和请求失败后健康检查可用 |
| 新路径互斥 | 测试设置旧 File / Database 开启及旧 Fatal 阈值，仍仅由新输出接收，且不要求 SQLSugar 注册 |
| 构建 | 定向测试编译 API、Auth、Gateway、DbMigrate 及相关依赖；未安装或更新包 |

沙盒曾拒绝 MSBuild 管道和测试夹具的回环监听；在限定范围提权后通过。HTTP 测试只使用临时文件及 127.0.0.1 随机端口，没有启动真实业务宿主、数据库或采集容器。镜像目录布局测试不等于 Docker 镜像构建或部署验收。

## 未关闭项与下一批

1. `RadishLogging.Enabled` 默认 false。候选路径尚不能生产切换：既有 SQL / Service AOP / seed / DbMigrate / Rust 裸输出仍有旁路，业务调用点也未完整登记事件码。
2. 本批引导覆盖从配置加载完成到宿主退出；配置加载前错误、顶层未处理启动异常、OOM / 进程强杀不在已验收范围。下一批统一顶层异常所有权及安全终止处理。
3. 下一批优先治理 SQL 普通诊断与慢查询独立开关、AOP / 事务去重、DbMigrate 命令结果和诊断分离；然后治理 seed / 后台任务 / Rust 边界及业务事件分类。
4. 安全异常类型 / 错误码 / 栈帧尚未开放，本批不会把被裁剪的异常正文包装成“完整可排障信息”。
5. 同步写标准流可能受操作系统管道背压影响，不宣称业务线程永不等待。应急计数目前在进程内，后续独立管道指标与 Console 需暴露缺口。
6. L1 正式传输上界及磁盘故障门禁，L3 入库、L4 查询、L5 告警、L6 切换仍按主专题推进。
