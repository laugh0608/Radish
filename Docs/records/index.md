# 记录与验收索引

- [Rust 图片处理替代实施与验证](./rust-image-replacement-2026-10-20.md)

本页是 `Docs/records/` 的统一入口，收纳批次级回归记录、人工验收清单、发布 / 回滚记录、checklist / template 与 spike / 评估观察记录。如果你要找的是“当前系统怎么工作”，请先回到 [Guide 手册索引](/guide/)。
## 一、通用模板与总索引

- [2026-11-12 通知与实时推送最终输出核验与实施](./unified-logging-l2-notification-realtime-output-audit-2026-11-12.md)：四个来源、五个事件的正式旧链和候选 JSONL 修复及本地回归

- [2026-11-12 公开查询与报表最终输出核验与实施](./unified-logging-l2-public-query-output-audit-2026-11-12.md)：四个来源、11 个事件的正式旧链和候选 JSONL 修复及本地回归

- [2026-11-11 商城、库存与支付口令最终输出核验与实施](./unified-logging-l2-shop-output-audit-2026-11-11.md)：四个生成来源、七个事件码的缺口证据、命名身份 / 字段隔离修复与本地回归；真实链路门禁保留

- [2026-11-11 奖励、币与经验最终输出核验与实施](./unified-logging-l2-reward-output-audit-2026-11-11.md)：六个生成来源、13 个事件码的缺口复现、字段隔离修复与本地回归；真实链路门禁保留

- [2026-11-11 Outbox 最终输出本地收口](./unified-logging-l2-outbox-output-2026-11-11.md)：分派 / 重试 / 死信字段隔离、显式失败分类与 main / chat SQLite 回归；真实采集链路和 PostgreSQL 验收保留

- [2026-11-10 功能完成主线收口](./planning-closeout-2026-11-10.md)：日志四批、Native 与连接衔接、阶段出口和后置；仅文档调整

- [2026-11-05 日终代码与文档回顾](./day-end-doc-review-2026-11-05.md)：六个提交、浏览器与后端日志 / L3-A 本地成果、指南修正及下一有限批次建议

- [2026-11-05 Controller 最终输出本地收口](./unified-logging-l2-controller-output-2026-11-05.md)：14 个精确来源、逐事件白名单、scope 污染与原业务回归；其他来源及真实运行门禁保留

- [2026-11-05 Controller 最终输出准备](./unified-logging-l2-controller-output-readiness-2026-11-05.md)：14 个来源有限目录、Reaction 旧链 scope 缺口复现及命令输出例外核对；实施结果见上方收口记录

- [2026-11-05 L2 剩余自有操作与直接异常消费者](./unified-logging-l2-owned-operations-2026-11-05.md)：七个来源收口、API 异常消费出口补漏及本地验证；保留真实运行与其他已登记来源门禁

- [2026-11-05 L3-A 接收与幂等持久化本地实施](./unified-logging-l3-ingest-local-2026-11-05.md)：默认关闭的内网接收、事务与迁移；临时 SQLite / 内存 HTTP 验证，真实 PostgreSQL / 监听 / collector 未执行

- [2026-11-05 L2 浏览器日志最终输出收口](./unified-logging-l2-browser-output-2026-11-05.md)：公共安全出口、两应用固定目录、三级 / diagnostic、HTTP 语义与本地自动化验收；未执行真实 Smoke 或遥测上传

- [2026-11-04 日终代码与文档回顾](./day-end-doc-review-2026-11-04.md)：八个提交、SQLite 本次平台收口、Hangfire 最终输出修正、指南同步及浏览器日志下一步

- [2026-11-04 L2 Hangfire 最终输出修正](./unified-logging-l2-hangfire-output-2026-11-04.md) · [结果 JSON](./unified-logging-l2-hangfire-output-2026-11-04.json)：两个精确来源 / 三条出口收口，Debug / Release 各 222 项及 52 项 Node 契约通过，L2 整体未关闭

- [2026-11-04 Hangfire 来源与源码交付复核](./hangfire-source-delivery-review-2026-11-04.md) · [摘要 JSON](./hangfire-source-delivery-review-2026-11-04.json)：869 文件 / 753 blobs / 42 项 feed 留存通过，原 PostgreSql DLL commit 声明补证；匹配 PDB、Windows 与正式入口仍未关闭，询证草稿未发送

- [2026-11-04 SQLite 模块修正候选单次托管验收](./sqlite-hosted-native-module-run-2026-11-04.md) · [结果 JSON](./sqlite-hosted-native-module-run-2026-11-04.json)：七 job 全部通过，492 项测试、48 份 publish、156 份冷进程 / 模块清单及清理完整；Windows 本批缺口关闭，未重试 / 发布 / 部署

- [2026-11-04 SQLite 探针模块识别本地修正](./sqlite-probe-module-local-repair-2026-11-04.md)：上轮结果已提交；模块 / PE / CLR 识别与失败清单留存完成，两配置各 82 项、32 项 Node、8 份探针编译 / 24 个本机冷 mode 通过，未 push / 远端验收

- [2026-11-04 SQLite 夹具候选单次托管验收](./sqlite-hosted-native-fixture-run-2026-11-04.md) · [结果 JSON](./sqlite-hosted-native-fixture-run-2026-11-04.json)：6 项通过，Windows Release 60 项通过后首个冷探针报告模块数量为 2；七份证据归档与清理完整，未重试

- [2026-11-04 Windows SQLite 夹具本地修正](./sqlite-windows-fixture-local-repair-2026-11-04.md)：三个迁移用例统一关闭临时连接池，Debug / Release 严格构建及各 60 项定向测试通过；同步 Hangfire 上游进展，未 push / 远端验收

- [2026-11-01 规划结构统一](./planning-structure-alignment-2026-11-01.md)：能力地图、状态来源与保留历史摘要。

- [2026-10-31 日终代码与文档回顾](./day-end-doc-review-2026-10-31.md)：18 个提交、代码 / 文档校准，以及 Windows SQLite 夹具和 Razor 身份门禁接续事项

- [SQLite 修正后的第二次托管验收](./sqlite-hosted-native-second-run-2026-10-31.md) · [结果 JSON](./sqlite-hosted-native-second-run-2026-10-31.json)：6 项通过，Windows Release 三项临时 SQLite 删除失败；七份 artifact 校验与清理完成，未重试

- [SQLite 四类托管失败的本地成组修正](./sqlite-hosted-validation-repair-2026-10-31.md) · [结果 JSON](./sqlite-hosted-validation-repair-2026-10-31.json)：50 项 Node、12 组真实 MSBuild 参数、四宿主探针严格编译与 ARM64 C / C++ 链接通过；未 push / 远端运行

- [Windows runner image 询证与关闭记录](./hangfire-windows-runner-image-inquiry-review-2026-10-31.md) · [英文 Issue 草稿](./hangfire-windows-runner-image-inquiry-draft-2026-10-31.md)。上游更正根因，`20261101.353.1` 独立复测通过，[#15201](https://github.com/actions/runner-images/issues/15201) 以 `completed` 关闭。Radish Windows Razor 验证留在维护队列。
- [SQLite 托管三平台与 AMD64 镜像单次验证](./sqlite-hosted-native-run-2026-10-31.md) · [结果 JSON](./sqlite-hosted-native-run-2026-10-31.json)：Linux x64 通过，其余六项失败；七份 artifact 已校验，清理完成，未重试
- [SQLite 托管执行契约复核与本地修正](./sqlite-hosted-execution-contracts-2026-10-31.md)：批准 SHA、失败证据、清理结果、实际编译器与 notices 留痕；38 项本地检查通过，未 push / dispatch
- [Windows Razor 诊断 inventory 与身份门禁](./hangfire-windows-razor-inventory-diagnostic-2026-10-31.md) · [结果 JSON](./hangfire-windows-razor-inventory-diagnostic-2026-10-31.json)：20 组契约通过，文件 / Machine 环境冲突触发门禁失败；证据与清理完成，未重试 / verify
- [Windows Razor image 身份诊断增强](./hangfire-windows-razor-image-identity-diagnostics-2026-10-31.md)：只读来源核对后补齐本地采集、来源一致性门禁及 12 组 PowerShell 契约；31 项 Node 通过，新候选未 push / dispatch
- [Windows Razor 第二次 inventory 与身份差异](./hangfire-windows-razor-inventory-second-2026-10-31.md) · [结果 JSON](./hangfire-windows-razor-inventory-second-2026-10-31.json)：修复 SHA 已 push；8 组 PowerShell 契约、库存、归档与清理通过，image 两处版本不一致，verify 未执行
- [Windows Razor inventory 本地修复](./hangfire-windows-razor-inventory-repair-2026-10-31.md) · [结果 JSON](./hangfire-windows-razor-inventory-repair-2026-10-31.json)：专项改用 windows-2022 / VS 2022，修复命令身份、补齐早期证据与 8 组 PowerShell 契约接线；28 项 Node 通过，未重跑 Windows
- [Windows Razor 首次 inventory 与失败清理](./hangfire-windows-razor-inventory-2026-10-31.md) · [结果 JSON](./hangfire-windows-razor-inventory-2026-10-31.json)：指定 SHA 已 push；实际 image 为 VS 2026，VS 2022 限定查询为空，未完成库存；失败证据已归档、清理通过，未重跑或 verify
- [Windows Razor 本地工具与限定 CI](./hangfire-windows-razor-local-implementation-2026-10-31.md) · [结果 JSON](./hangfire-windows-razor-local-implementation-2026-10-31.json)：25 项 Node / 11 组 C# 契约、两组原 Core 片段和四类拒绝通过；未执行 Windows / 远端
- [Windows Razor 准备快照](./hangfire-windows-razor-readiness-2026-10-31.json) · [分段执行方案](../features/hangfire-windows-razor-validation-plan.md)：官方 runner 清单、11 项包、23 对输入与生成文件 / LF 与 CRLF、24 个包内 DLL；未运行 Windows 或改 CI
- [Hangfire 本机源码重建与验证](./hangfire-rebuild-2026-10-31.md) · [结果 JSON](./hangfire-rebuild-2026-10-31.json)：42 项封存、五目标字节一致重放、18 组最终调用及清理完成；Windows Razor 与正式源码入口未关闭
- [Hangfire 重建准备清单](./hangfire-rebuild-readiness-2026-10-31.json) · [实施方案](../features/hangfire-rebuild-delivery-plan.md)：保留准备期 34 个精确源文件、SDK / ref pack、42 项候选及旧输入审计；实际 tree 字段更正、构建和运行结果见上一条
- [PostgreSQL 回归与托管 CI 本地接线](./postgres-regression-2026-10-31.md) · [结果 JSON](./postgres-regression-2026-10-31.json)：PG17 两配置全量、PG16 两配置专项与四组 worker 通过，任务资源清理完成；本地 CI 验证通过，未 push / 远端触发
- [PG / 剩余原生平台准备快照](./postgres-native-readiness-2026-10-31.json) · [PG 执行方案](../features/postgres-regression-execution-plan.md) · [托管 runner 方案](../features/sqlite-hosted-native-validation-plan.md)：缓存、43 个 PG 源码方法、工具精确输入与 Hangfire 留存缺口；未启动服务、未修改 CI 或远端触发
- [SQLite Linux 双架构执行与清理](./sqlite-linux-platform-validation-2026-10-31.md)：两配置 / 架构各 60 项定向测试、32 份 publish、120 个冷进程与八 final 镜像通过；代理恢复及本任务资源清理完成
- [SQLite Linux 双架构准备快照](./sqlite-linux-platform-readiness-2026-10-31.json) · [精确输入清单](./sqlite-linux-platform-inputs-2026-10-31.json) · [已批准执行方案](../features/sqlite-linux-platform-validation-plan.md)：保留当时尚未执行的盘点事实，后续运行和清理结果见上一条
- [SQLite 原生依赖统一实施与本机验证](./sqlite-native-unification-2026-10-31.md)：双配置全量、通用 / 本机 RID 产物、实际加载、分发材料及其他平台未关闭范围
- [2026-10-26 日终代码与文档回顾](./day-end-doc-review-2026-10-26.md)：当日 7 个提交、代码与文档对照、验证边界及下一步建议

- [2026-10-21 日终代码与文档回顾](./day-end-doc-review-2026-10-21.md)：当日 9 个提交、PR #75 集成、Mapperly 替代、隔离验证、上传配额修复及接续事项
- [NoDrive 正式采用](./nodrive-adoption-2026-10-26.md) · [NoDrive 来源补证与分发预审](./nodrive-source-review-2026-10-26.md) · [重点 NuGet notices 分发接线](./nuget-notices-distribution-2026-10-26.md)
- [剩余依赖发行边界核对](./dependency-distribution-boundaries-2026-10-26.md)：Hangfire 精确源码快照、库替换 / 授权边界、NoDrive 询证与 SNI 分发事项
- [LGPL 狭义例外与本机验证](./lgpl-exception-validation-2026-10-26.md)：已批准许可补充、离线 native 重建、双配置严格构建及四宿主发布核对
- [Hangfire 源码 / 构建输入补证](./hangfire-source-inputs-2026-10-26.md)：PDB checksum、Cronos 声明、锁定输入与 SQLite 测试 / 发布差异
- [Hangfire Core 源码替换验证](./hangfire-core-replacement-2026-10-26.md)：获批的分析器调整、严格构建、接口对照与三组真实 SQLite 注册 / 执行
- [Mapperly 映射迁移](./mapperly-migration-2026-10-21.md) · [NoDrive / Valkey 隔离验证](./nodrive-valkey-validation-2026-10-21.md) · [上传配额边界修复](./upload-quota-boundary-fix-2026-10-21.md)

- [2026-10-20 第三方依赖许可盘点](./dependency-license-audit-2026-10-20.md)：947 条依赖、重点许可边界与待补证据；[逐包 CSV](./dependency-license-inventory-2026-10-20.csv)
- [2026-10-20 dev → master 合并前检查（历史停止线）](./master-pr-regression-2026-10-20.md)：49 个提交的批次检查、NuGet 安全修复及 Six Labors 许可证停止线
- [2026-10-15 日终代码与文档回顾](./day-end-doc-review-2026-10-15.md)：六个日志 L2 开发提交、完整批次索引、运行指南校准与下次恢复事项

- [2026-10-11 日终代码与文档回顾](./day-end-doc-review-2026-10-11.md)：11 个日志 L2 开发提交、完整批次索引、文档校准与 10 月 12 日事项
- [2026-10-07 日终代码与文档回顾](./day-end-doc-review-2026-10-07.md)：7 个日志 L2 开发提交、全部批次 / 方案索引、文档校准与 10 月 8 日事项
- [2026-10-02 日终代码与文档回顾](./day-end-doc-review-2026-10-02.md)：19 个日志 L2 开发提交与完整批次索引、文档校准、独立问题及 10 月 3 日事项
- [2026-10-01 dev → master 批次回归](./master-pr-regression-2026-10-01.md)：近期部署 / Console 修复及日志 L1 / L2 集成前检查
- [2026-09-28 日终代码与文档回顾](./day-end-doc-review-2026-09-28.md)：15 个日志 L2 开发提交、批次证据索引、文档状态校准与明天事项

- [2026-09-23 日终回顾与日志 L2 八组批次记录](./day-end-doc-review-2026-09-23.md)（9 个开发 / 验证提交）
- [2026-09-19 日终回顾](./day-end-doc-review-2026-09-19.md) · [统一日志 L1 契约与采集首轮实验](/records/unified-logging-l1-contract-and-transport-2026-09-19) · [采集安全与故障边界](/records/unified-logging-l1-guarded-collector-2026-09-19) · [L2 生成入口](/records/unified-logging-l2-producer-entry-2026-09-19)
- [专题回归索引](/guide/regression-index)
- [2026-09-06 项目全面审阅](/records/project-review-2026-09-06) · [工程改进候选（待确认）](/planning/engineering-improvement-candidates) · [F4 旧入口流水](/records/f4-planning-entry-history-2026-09-06)
- [人工验收模板](/records/manual-acceptance-template)
- [回归结论记录模板](/records/regression-result-template)
- [变更回归记录模板](/records/change-regression-record-template)
- [F4-A 生产六链路证据采集记录模板](/records/f4-a-production-evidence-capture-template)
- [`PR -> master` 最小执行清单](/records/master-pr-minimal-checklist)

## 二、公开壳层与内容阅读验收

- [forum-public-mobile-acceptance](/records/forum-public-mobile-acceptance) · [docs-public-acceptance](/records/docs-public-acceptance)
- [discover-public-acceptance](/records/discover-public-acceptance)
- [profile-public-acceptance](/records/profile-public-acceptance)
- [leaderboard-public-acceptance](/records/leaderboard-public-acceptance)
- [shop-public-acceptance](/records/shop-public-acceptance)
- [public-shell-change-regression-record-2026-04-16](/records/public-shell-change-regression-record-2026-04-16)

## 三、Flutter Native 记录

- [Flutter Native P8-B2 macOS local runtime acceptance 关闭](/records/f4-flutter-native-p8b2-macos-local-runtime-acceptance-closure-2026-09-05) · [Flutter Native P8-B1 macOS platform foundation 实施](/records/f4-flutter-native-p8b1-macos-platform-foundation-implementation-2026-09-05) · [Flutter Native P8-B macOS local platform foundation readiness](/records/f4-flutter-native-p8b-macos-platform-foundation-readiness-2026-09-05) · [Flutter Native P8-A desktop platform readiness](/records/f4-flutter-native-p8a-desktop-platform-readiness-2026-09-05) · [Flutter Native P7-D2 / D3 外部前置条件暂缓](/records/f4-flutter-native-p7d2-d3-external-prerequisite-deferral-2026-09-01) · [Flutter Native P7-D1 minimal readiness 实施](/records/f4-flutter-native-p7d1-internal-testflight-minimal-readiness-implementation-2026-09-01) · [Flutter Native P7-D Internal TestFlight-only 项目所有者裁决](/records/f4-flutter-native-p7d-internal-testflight-owner-scope-2026-09-01) · [Flutter Native P7-D iOS 真机、签名与分发 readiness](/records/f4-flutter-native-p7d-ios-device-signing-distribution-readiness-2026-09-01) · [Flutter Native P7-C iOS Simulator 运行态验收关闭](/records/f4-flutter-native-p7c-ios-simulator-runtime-acceptance-closure-2026-08-31) · [Flutter Native P7-C iOS Simulator 运行态验收 readiness](/records/f4-flutter-native-p7c-ios-simulator-runtime-acceptance-readiness-2026-08-31) · [2026-08-30 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-30) · [Flutter Native P7-B iOS platform foundation 实施记录](/records/f4-flutter-native-p7b-ios-platform-foundation-implementation-2026-08-30) · [Flutter Native P7-A iOS 平台 readiness](/records/f4-flutter-native-p7a-ios-platform-readiness-2026-08-30) · [Flutter Native P6 Android AVD 门禁项目所有者关闭](/records/f4-flutter-native-p6-android-avd-gate-owner-closure-2026-08-30) · [Flutter Native P6-B Android AVD 运行态验收关闭](/records/f4-flutter-native-p6b-android-avd-runtime-acceptance-closure-2026-08-30) · [2026-08-29 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-29) · [Flutter Native P6-B Android AVD 第一轮运行态验收](/records/f4-flutter-native-p6b-android-avd-runtime-acceptance-2026-08-29)
- [2026-08-27 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-27) · [Flutter Native P6-A Android 本地 RC 候选装配](/records/f4-flutter-native-p6a-android-local-rc-candidate-assembly-2026-08-27) · [Flutter Native P6 Android UI RC readiness](/records/f4-flutter-native-p6-android-ui-rc-readiness-2026-08-27) · [Flutter Native P5-E 成组静态门禁实现](/records/f4-flutter-native-p5e-grouped-static-gate-implementation-2026-08-27) · [P5-E readiness](/records/f4-flutter-native-p5e-grouped-static-gate-readiness-2026-08-27) · [Flutter Native P5-D3 Browse History 实现](/records/f4-flutter-native-p5d3-browse-history-implementation-2026-08-27) · [2026-08-24 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-24) · [Flutter Native P5-D3 Browse History readiness](/records/f4-flutter-native-p5d3-browse-history-readiness-2026-08-24) · [P5-D2 Leaderboard 实现](/records/f4-flutter-native-p5d2-leaderboard-implementation-2026-08-24) · [P5-D2 readiness](/records/f4-flutter-native-p5d2-leaderboard-readiness-2026-08-24) · [P5-D1 Wallet / Experience 实现](/records/f4-flutter-native-p5d1-wallet-experience-implementation-2026-08-24) · [P5-D1 readiness](/records/f4-flutter-native-p5d1-wallet-experience-readiness-2026-08-24) · [P5-C3 Commerce Private 实现](/records/f4-flutter-native-p5c3-commerce-private-implementation-2026-08-24) · [P5-C3 readiness](/records/f4-flutter-native-p5c3-commerce-private-readiness-2026-08-24) · [P5-C2 Commerce Browse / Transaction 实现](/records/f4-flutter-native-p5c2-commerce-browse-transaction-implementation-2026-08-24) · [P5-C2 readiness](/records/f4-flutter-native-p5c2-commerce-browse-transaction-readiness-2026-08-24) · [P5-C1 Docs Reader 实现](/records/f4-flutter-native-p5c1-docs-reader-implementation-2026-08-23) · [P5-C1 readiness](/records/f4-flutter-native-p5c1-docs-reader-readiness-2026-08-23) · [P5-B2 Identity / Revisit 实现](/records/f4-flutter-native-p5b2-identity-revisit-implementation-2026-08-23) · [P5-B2 readiness](/records/f4-flutter-native-p5b2-identity-revisit-readiness-2026-08-23) · [P5-B1 Forum Feed / Compose 实现](/records/f4-flutter-native-p5b1-forum-feed-compose-implementation-2026-08-23) · [P5-A 页面族拆批就绪审计](/records/f4-flutter-native-p5a-page-family-readiness-2026-08-23) · [P4-B5 成组静态门禁](/records/f4-flutter-native-p4b5-grouped-static-gate-2026-08-23) · [P4-B4 Forum Detail 实现](/records/f4-flutter-native-p4b4-forum-detail-implementation-2026-08-23) · [P4-B3 Discover 实现](/records/f4-flutter-native-p4b3-discover-implementation-2026-08-23) · [P4-B2 Adaptive Shell 实现](/records/f4-flutter-native-p4b2-adaptive-shell-implementation-2026-08-23) · [P4-B1 Theme / Shared 实现](/records/f4-flutter-native-p4b1-theme-shared-implementation-2026-08-23) · [P4-A 实施就绪审计](/records/f4-flutter-native-p4a-readiness-2026-08-23) · [P3 代表设计](/records/f4-flutter-native-p3-representative-design-2026-08-19) · [P2 主题与自适应技术基座](/records/f4-flutter-native-p2-theme-adaptive-foundation-2026-08-19) · [P1 全页面事实审计](/records/f4-flutter-native-p1-full-page-fact-audit-2026-08-19) · [MVP 回归记录](/records/flutter-android-mvp-regression-record-2026-05-01)
- [flutter-android-mvp-profile-recent-docs-record-2026-05-01](/records/flutter-android-mvp-profile-recent-docs-record-2026-05-01)
- [flutter-android-mvp-profile-revisit-sections-record-2026-05-01](/records/flutter-android-mvp-profile-revisit-sections-record-2026-05-01)
- [flutter-android-mvp-profile-section-density-record-2026-05-01](/records/flutter-android-mvp-profile-section-density-record-2026-05-01)
- [flutter-android-mvp-docs-detail-readonly-context-record-2026-05-01](/records/flutter-android-mvp-docs-detail-readonly-context-record-2026-05-01)
- [flutter-android-mvp-forum-detail-readonly-context-record-2026-05-01](/records/flutter-android-mvp-forum-detail-readonly-context-record-2026-05-01)
- [flutter-android-mvp-profile-empty-copy-record-2026-05-02](/records/flutter-android-mvp-profile-empty-copy-record-2026-05-02)
- [flutter-android-mvp-profile-long-text-record-2026-05-02](/records/flutter-android-mvp-profile-long-text-record-2026-05-02)
- [flutter-android-mvp-radishx-smoke-record-2026-05-02](/records/flutter-android-mvp-radishx-smoke-record-2026-05-02)
- [flutter-android-internal-rc-prep-record-2026-05-01](/records/flutter-android-internal-rc-prep-record-2026-05-01)
- [flutter-android-mvp-refresh-experience-checklist-2026-05-02](/records/flutter-android-mvp-refresh-experience-checklist-2026-05-02)
- [flutter-android-mvp-validation-index-2026-05-02](/records/flutter-android-mvp-validation-index-2026-05-02)
- [flutter-android-mvp-rc-acceptance-checklist-2026-05-03](/records/flutter-android-mvp-rc-acceptance-checklist-2026-05-03)
- [flutter-android-mvp-rc-acceptance-record-2026-05-04](/records/flutter-android-mvp-rc-acceptance-record-2026-05-04)
- [flutter-android-mvp-rc-supplemental-assessment-2026-05-02](/records/flutter-android-mvp-rc-supplemental-assessment-2026-05-02)

## 四、宿主运行、部署与发布记录

- [test-latest 部署反馈修复（2026-09-19）](/records/test-deployment-maintenance-2026-09-19)

- [m14-host-runtime-checklist](/records/m14-host-runtime-checklist)
- [m14-host-maintenance-record-example](/records/m14-host-maintenance-record-example)
- [m14-deployment-review-record-template](/records/m14-deployment-review-record-template)
- [m14-deployment-review-record-2026-04-06](/records/m14-deployment-review-record-2026-04-06)
- [GitHub Release 自动化实现记录（2026-08-23）](/records/github-release-automation-2026-08-23) · [m15-release-record-template](/records/m15-release-record-template)
- [m15-release-record-2026-04-06](/records/m15-release-record-2026-04-06)
- [m15-release-record-2026-07-12](/records/m15-release-record-2026-07-12)
- [m15-release-record-v26.7.1.1201-2026-07-12](/records/m15-release-record-v26.7.1.1201-2026-07-12) / [1202](/records/m15-release-record-v26.7.1.1202-2026-07-12)
- [m15-release-record-v26.7.1.1203-2026-07-12](/records/m15-release-record-v26.7.1.1203-2026-07-12)
- [m15-release-record-v26.7.1.1204-2026-07-12](/records/m15-release-record-v26.7.1.1204-2026-07-12) · [v26.8.1 Release Candidate 回归记录](/records/v26.8.1-release-candidate-regression-record-2026-08-15) · [M15 v26.8.1 发布记录](/records/m15-release-record-v26.8.1-2026-08-15)
- [production-database-migration-deployment-orchestration-2026-07-25](/records/production-database-migration-deployment-orchestration-2026-07-25)
- [m15-test-rollback-rehearsal-2026-04-06](/records/m15-test-rollback-rehearsal-2026-04-06)
- [m15-prod-rollback-playbook-2026-04-06](/records/m15-prod-rollback-playbook-2026-04-06)

## 五、身份语义与兼容性检查

- [identity-claim-external-compat-checklist](/records/identity-claim-external-compat-checklist)
- [identity-claim-regression-playbook](/guide/identity-claim-regression-playbook)

## 六、历史阶段与首版 dev 留痕

- [dev-first-regression-checklist](/records/dev-first-regression-checklist) · [dev-first-regression-record](/records/dev-first-regression-record)

## 七、专题清单与一次性观察（最新：[Flutter Native P7-D Internal TestFlight-only 项目所有者裁决](/records/f4-flutter-native-p7d-internal-testflight-owner-scope-2026-09-01)）

- [P7-D Internal TestFlight-only 项目所有者裁决](/records/f4-flutter-native-p7d-internal-testflight-owner-scope-2026-09-01) · [P7-D iOS 真机、签名与分发 readiness](/records/f4-flutter-native-p7d-ios-device-signing-distribution-readiness-2026-09-01) · [P7-C iOS Simulator 运行态验收关闭](/records/f4-flutter-native-p7c-ios-simulator-runtime-acceptance-closure-2026-08-31) · [P7-C iOS Simulator 运行态验收 readiness](/records/f4-flutter-native-p7c-ios-simulator-runtime-acceptance-readiness-2026-08-31) · [2026-08-30 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-30) · [P7-B iOS platform foundation 实施记录](/records/f4-flutter-native-p7b-ios-platform-foundation-implementation-2026-08-30) · [P7-A iOS 平台 readiness](/records/f4-flutter-native-p7a-ios-platform-readiness-2026-08-30) · [P6 Android AVD 门禁项目所有者关闭](/records/f4-flutter-native-p6-android-avd-gate-owner-closure-2026-08-30) · [P6-B Android AVD 运行态验收关闭](/records/f4-flutter-native-p6b-android-avd-runtime-acceptance-closure-2026-08-30) · [2026-08-29 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-29) · [P6-B Android AVD 第一轮运行态验收](/records/f4-flutter-native-p6b-android-avd-runtime-acceptance-2026-08-29) · [2026-08-27 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-27)
- [2026-08-24 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-24) · [2026-08-23 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-23)
- [F4 `dev -> master` 合并前回归记录](/records/f4-dev-master-pr-regression-2026-08-22) · [2026-08-19 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-19) · [F4-R-T03 Web 四主题成组运行态验收](/records/f4-r-t03-web-four-theme-grouped-runtime-acceptance-2026-08-15) · [F4-R Web 主题基线与专题退出门禁审计](/records/f4-r-web-theme-exit-readiness-audit-2026-08-15) · [F4-R R3-F02 自服务与边界页成组运行态验收](/records/f4-r-r3-f02-grouped-runtime-acceptance-2026-08-15) · [F4-R R3-F02-C 错误与路由边界实现及静态门禁](/records/f4-r-r3-f02-c-error-route-boundary-implementation-2026-08-15) · [F4-R R3-F02-B 自服务权威状态实现与静态门禁](/records/f4-r-r3-f02-b-self-service-authoritative-state-implementation-2026-08-15) · [F4 2026-08-13 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-13) · [F4-R R3-F02-A OIDC 回流信任门禁实现与静态门禁](/records/f4-r-r3-f02-a-oidc-return-trust-gate-implementation-2026-08-13) · [F4-R R3-F02 自服务与边界页设计前代码事实与风险拆批审计](/records/f4-r-r3-f02-self-service-boundary-readiness-audit-2026-08-13) · [F4-R R3-C05 Console 仪表与治理派生成组运行态验收](/records/f4-r-r3-c05-console-dashboard-governance-stage-acceptance-2026-08-13) · [F4-R R3-C05-D Experience 权威台账治理实现](/records/f4-r-r3-c05-d-experience-authoritative-ledger-governance-implementation-2026-08-13) · [F4-R R3-C05-C Documents 权威治理实现](/records/f4-r-r3-c05-c-documents-authoritative-governance-implementation-2026-08-13) · [F4-R R3-C05-B Channel Discoverability 权威公开资格治理实现](/records/f4-r-r3-c05-b-channel-discoverability-authoritative-governance-implementation-2026-08-13) · [F4 2026-08-12 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-12) · [商城商品历史实现参考](/records/shop-product-implementation-reference) · [F4-R R3-C05-A Console Dashboard 权威调度面实现](/records/f4-r-r3-c05-a-console-dashboard-authoritative-dispatch-implementation-2026-08-12) · [R3-C05 Console 仪表与治理派生设计前审计](/records/f4-r-r3-c05-console-dashboard-governance-readiness-audit-2026-08-12) · [F4-R R3-C04-F Console Coins 权威调账与流水治理实现](/records/f4-r-r3-c04-f-console-coins-implementation-2026-08-12) · [F4-R R3-C04-E Console Stickers 权威媒体资源治理实现](/records/f4-r-r3-c04-e-console-stickers-implementation-2026-08-12) · [F4-R R3-C04-D Console Products 权威列表与独立上下架实现](/records/f4-r-r3-c04-d-console-products-implementation-2026-08-12) · [F4-R R3-C04-C Console Applications 权威目录与一次性 Secret 实现](/records/f4-r-r3-c04-c-console-applications-implementation-2026-08-12) · [F4-R R3-C04-B Console Users 权威列表与聚合详情实现](/records/f4-r-r3-c04-b-console-users-implementation-2026-08-12) · [F4 2026-08-11 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-11) · [F4-R R3-C04-A Categories / Tags 与共享响应式资源表面实现](/records/f4-r-r3-c04-a-console-taxonomy-implementation-2026-08-11) · [R3-C04 Console 普通资源设计前代码事实与风险拆批审计](/records/f4-r-r3-c04-console-ordinary-resources-readiness-audit-2026-08-11) · [R3-P06 Public Shop 浏览与 Leaderboard 正式实现及 Gateway 验收](/records/f4-r-r3-p06-public-shop-leaderboard-implementation-2026-08-11) · [R3-P05 Public Docs 与 Legal 正式实现及 Gateway 验收](/records/f4-r-r3-p05-public-docs-legal-implementation-2026-08-11)
- [F4-R R3-P04 Public Forum 浏览族正式实现与 Gateway 验收](/records/f4-r-r3-p04-public-forum-browse-implementation-2026-08-11) · [R3 路由继承实施分批审计](/records/f4-r-r3-route-inheritance-batch-audit-2026-08-11) · [F4-R R2-A02 Author 列表、修订与 Forum Compose 正式实现及 Gateway 验收](/records/f4-r-r2-a02-author-list-revisions-forum-compose-implementation-2026-08-11) · [F4 2026-08-10 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-10) · [R2-A02 局部代表设计](/records/f4-r-r2-a02-author-list-revisions-forum-compose-representative-design-2026-08-10) · [能力门禁实现](/records/f4-r-r2-a02-author-list-revisions-forum-compose-capability-gate-implementation-2026-08-10) · [设计前代码事实与能力覆盖审计](/records/f4-r-r2-a02-author-list-revisions-forum-compose-readiness-audit-2026-08-10) · [F4-R R2-W02 Private 仪表 / 任务侧栏正式实现与 Gateway 验收](/records/f4-r-r2-w02-private-dashboard-task-rail-implementation-2026-08-10) · [局部代表设计](/records/f4-r-r2-w02-private-dashboard-task-rail-representative-design-2026-08-10) · [前端能力门禁实现](/records/f4-r-r2-w02-private-dashboard-task-rail-capability-gate-implementation-2026-08-10) · [设计前代码事实与能力覆盖审计](/records/f4-r-r2-w02-private-dashboard-task-rail-readiness-audit-2026-08-10) · [F4-R R2-P03 Public 只读详情变体正式实现与 Gateway 验收](/records/f4-r-r2-p03-public-read-only-detail-variants-implementation-2026-08-10) · [F4 2026-08-09 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-09) · [F4-R R2-P03 商品评价与公开等级能力门禁实现](/records/f4-r-r2-p03-product-review-public-level-capability-gate-implementation-2026-08-09) · [范围审计](/records/f4-r-r2-p03-product-review-public-level-scope-audit-2026-08-09) · [Public 只读详情变体局部代表设计](/records/f4-r-r2-p03-public-read-only-detail-variants-representative-design-2026-08-09) · [首批能力门禁实现](/records/f4-r-r2-p03-public-read-only-detail-variants-capability-gate-implementation-2026-08-09) · [设计前代码事实与能力覆盖审计](/records/f4-r-r2-p03-public-read-only-detail-variants-readiness-audit-2026-08-09) · [R2-C03 Console 设置与权限矩阵正式实现](/records/f4-r-r2-c03-console-settings-permissions-implementation-2026-08-09) · [局部代表设计](/records/f4-r-r2-c03-console-settings-permissions-representative-design-2026-08-09) · [代码能力门禁实现](/records/f4-r-r2-c03-console-settings-permissions-capability-gate-implementation-2026-08-09) · [设计前代码事实与能力覆盖审计](/records/f4-r-r2-c03-console-settings-permissions-readiness-audit-2026-08-09)
- [F4-R R1-C02 Console 案件治理 / 审计成组实现](/records/f4-r-r1-c02-console-moderation-implementation-2026-08-09) · [正式代表设计](/records/f4-r-r1-c02-console-moderation-representative-design-2026-08-09) · [前端能力门禁实现](/records/f4-r-r1-c02-console-moderation-capability-gate-implementation-2026-08-09) · [设计前代码事实与能力覆盖门禁](/records/f4-r-r1-c02-console-moderation-readiness-audit-2026-08-09) · [F4 2026-08-08 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-08) · [F4-R R1-C01 Console 订单表格—明细成组实现与运行态验收](/records/f4-r-r1-c01-console-orders-implementation-2026-08-08) · [设计前代码事实与能力覆盖门禁](/records/f4-r-r1-c01-console-orders-readiness-audit-2026-08-08) · [F4-R R1-W01 Private 消息工作区成组实现与运行态验收](/records/f4-r-r1-w01-messages-web-implementation-2026-08-08) · [F4-R R1-W01 消息工作区能力门禁修复](/records/f4-r-r1-w01-messages-capability-gate-implementation-2026-08-08) · [F4-R R1-W01 消息工作区设计前代码事实与能力覆盖门禁](/records/f4-r-r1-w01-messages-readiness-audit-2026-08-08) · [F4-R R1-A01 Author 编辑代表页成组实现与运行态验收](/records/f4-r-r1-a01-author-editor-implementation-2026-08-08) · [F4-R R1-A01 Author 能力门禁修复](/records/f4-r-r1-a01-author-capability-gate-implementation-2026-08-08) · [F4-R R1-A01 Author 设计前代码事实与能力覆盖门禁](/records/f4-r-r1-a01-author-readiness-audit-2026-08-08) · [F4-R R1-P02 帖子详情成组实现与运行态验收](/records/f4-r-r1-p02-public-detail-implementation-2026-08-08) · [F4-R 正式 Web 能力覆盖复核](/records/f4-r-formal-web-capability-coverage-audit-2026-08-06) · [F4-R R1-P02 Public 详情与互动审计](/records/f4-r-r1-p02-public-detail-interaction-audit-2026-08-05) · [F4-R R1-P01 公开发现成组实现](/records/f4-r-r1-p01-public-discover-implementation-2026-08-05) · [F4-R R1-P01 mobile 与统一公开读模型设计](/records/f4-r-r1-p01-mobile-public-read-model-design-2026-08-05) · [F4 2026-08-04 R1-P01 设计收口与文档审阅](/records/f4-day-end-doc-review-2026-08-04) · [F4-R R1-P01 公开社区发现结构研究](/records/f4-r-r1-p01-public-discover-structure-study-2026-08-04) · [F4 2026-08-03 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-08-03) · [F4 2026-07-30 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-30) · [F4-S-D 公开排行榜治理成组验收](/records/f4-s-d-leaderboard-public-governance-stage-acceptance-2026-07-30) · [F4-S 公开排行榜治理代码侧验收](/records/f4-s-leaderboard-public-governance-code-acceptance-2026-07-30) · [F4 2026-07-29 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-29) · [F4-Q 论坛标签公开发现 D 成组验收](/records/f4-q-d-forum-tag-public-discovery-stage-acceptance-2026-07-29) · [F4-P 论坛帖子收藏 D 成组验收](/records/f4-p-d-forum-post-bookmark-stage-acceptance-2026-07-29) · [F4 2026-07-28 日终提交回顾与文档审阅](/records/f4-day-end-doc-review-2026-07-28) · [F4-O 论坛回答生命周期 D 成组验收](/records/f4-o-d-forum-answer-lifecycle-stage-acceptance-2026-07-28) · [2026-07-27](/records/f4-day-end-doc-review-2026-07-27) · [F4-N D 成组验收](/records/f4-n-d-forum-content-reward-stage-acceptance-2026-07-27) · [2026-07-26](/records/f4-day-end-doc-review-2026-07-26) · [2026-07-25](/records/f4-day-end-doc-review-2026-07-25)
- F4-M 论坛内容版本完整性与作者恢复：[D 成组验收](/records/f4-m-d-forum-content-revision-stage-acceptance-2026-07-26) · [C 正式 Web](/records/f4-m-c-forum-content-revision-web-2026-07-26) · [B 服务端权威契约](/records/f4-m-b-forum-content-revision-server-contract-2026-07-26)；F4-L Wiki 附件隐私与生命周期：[D 成组验收](/records/f4-l-d-wiki-attachment-stage-acceptance-2026-07-26) · [C 正式 Web 受保护资源](/records/f4-l-c-wiki-attachment-web-protected-assets-2026-07-26) · [B 服务端权威契约](/records/f4-l-b-wiki-attachment-server-contract-2026-07-25)
- F4-K 用户屏蔽与关系交互隔离：[D 成组验收](/records/f4-k-d-user-block-stage-acceptance-2026-07-25) · [C 正式 Web](/records/f4-k-c-user-block-web-2026-07-25) · [B 服务端权威契约](/records/f4-k-b-user-block-server-contract-2026-07-25)
- F4-I 内容治理：[D 成组验收](/records/f4-i-d-content-moderation-case-stage-acceptance-2026-07-25) · [C 正式页面](/records/f4-i-c-content-moderation-case-web-workspace-2026-07-24) · [B 服务端权威契约](/records/f4-i-b-content-moderation-server-contract-2026-07-21) · [当日日终回顾](/records/f4-day-end-doc-review-2026-07-21)
- F4-J 治理申诉与处置纠正：[D 成组验收](/records/f4-j-d-content-moderation-appeal-stage-acceptance-2026-07-25) · [C 正式页面](/records/f4-j-c-content-moderation-appeal-web-workspace-2026-07-25) · [B 服务端权威契约](/records/f4-j-b-content-moderation-appeal-server-contract-2026-07-25)
- F4-G Wiki 作者协作：[D 成组验收](/records/f4-g-d-wiki-author-collaboration-stage-acceptance-2026-07-20) · [C 正式页面](/records/f4-g-c-wiki-author-console-pages-2026-07-20) · [B 服务端契约](/records/f4-g-b-wiki-author-collaboration-server-contract-2026-07-20) · [当日日终回顾](/records/f4-day-end-doc-review-2026-07-20)
- [f4-day-end-doc-review-2026-07-19](/records/f4-day-end-doc-review-2026-07-19)
- F4-F 聊天轻量阅读回执：[D 成组验收](/records/f4-f-d-chat-message-read-receipt-stage-acceptance-2026-07-19) · [C 正式页面](/records/f4-f-c-chat-message-read-receipt-web-workspace-2026-07-19) · [B 服务端权威契约](/records/f4-f-b-chat-message-read-receipt-server-contract-2026-07-19)
- [f4-e-d-chat-message-pin-stage-acceptance-2026-07-19](/records/f4-e-d-chat-message-pin-stage-acceptance-2026-07-19)
- [f4-e-c-chat-message-pin-web-workspace-2026-07-19](/records/f4-e-c-chat-message-pin-web-workspace-2026-07-19)
- [f4-e-b-chat-message-pin-server-contract-2026-07-19](/records/f4-e-b-chat-message-pin-server-contract-2026-07-19)
- [f4-d-d-chat-message-reaction-stage-acceptance-2026-07-19](/records/f4-d-d-chat-message-reaction-stage-acceptance-2026-07-19)
- [f4-d-c-chat-message-reaction-web-workspace-2026-07-19](/records/f4-d-c-chat-message-reaction-web-workspace-2026-07-19)
- [f4-d-b-chat-message-reaction-server-contract-2026-07-19](/records/f4-d-b-chat-message-reaction-server-contract-2026-07-19)
- [f4-c-d-chat-message-search-stage-acceptance-2026-07-19](/records/f4-c-d-chat-message-search-stage-acceptance-2026-07-19)
- [f4-c-c-chat-message-search-web-workspace-2026-07-19](/records/f4-c-c-chat-message-search-web-workspace-2026-07-19)
- [f4-c-b-chat-message-search-server-contract-2026-07-19](/records/f4-c-b-chat-message-search-server-contract-2026-07-19)
- [通知系统历史方案索引](/records/notification-legacy/)
- [f4-day-end-doc-review-2026-07-18](/records/f4-day-end-doc-review-2026-07-18)
- [f4-a-first-real-usage-evidence-attribution-2026-07-18](/records/f4-a-first-real-usage-evidence-attribution-2026-07-18)
- [f3-d-i18n-stage-acceptance-2026-07-17](/records/f3-d-i18n-stage-acceptance-2026-07-17)
- [f3-i18n-day-end-doc-review-2026-07-16](/records/f3-i18n-day-end-doc-review-2026-07-16)
- [f3-c8-shared-feedback-upload-shell-static-closure-2026-07-16](/records/f3-c8-shared-feedback-upload-shell-static-closure-2026-07-16)
- [f3-i18n-day-end-doc-review-2026-07-15](/records/f3-i18n-day-end-doc-review-2026-07-15)
- [f2-theme-system-stage-acceptance-2026-07-14](/records/f2-theme-system-stage-acceptance-2026-07-14)
- [f1-e-shop-entitlement-stage-acceptance-2026-07-14](/records/f1-e-shop-entitlement-stage-acceptance-2026-07-14)
- [p3-12-day-end-doc-review-2026-07-12](/records/p3-12-day-end-doc-review-2026-07-12)
- [p3-12-e8-pre-master-integration-review-2026-07-11](/records/p3-12-e8-pre-master-integration-review-2026-07-11)
- [p3-12-e8-b-limited-product-matrix-audit-2026-07-11](/records/p3-12-e8-b-limited-product-matrix-audit-2026-07-11)
- [p3-12-e8-q0-security-exposure-implementation-plan-2026-07-10](/records/p3-12-e8-q0-security-exposure-implementation-plan-2026-07-10)
- [p3-12-f-q1-a-reliable-post-transaction-task-plan-2026-07-11](/records/p3-12-f-q1-a-reliable-post-transaction-task-plan-2026-07-11)
- [p3-12-f-q1-a-candidate-validation-2026-07-11](/records/p3-12-f-q1-a-candidate-validation-2026-07-11)
- [p3-12-f-q1-b-api-error-contract-audit-plan-2026-07-11](/records/p3-12-f-q1-b-api-error-contract-audit-plan-2026-07-11)
- [p3-12-f-q1-c-file-access-token-governance-plan-2026-07-11](/records/p3-12-f-q1-c-file-access-token-governance-plan-2026-07-11)
- [p3-12-f-q2-a-time-semantics-migration-plan-2026-07-12](/records/p3-12-f-q2-a-time-semantics-migration-plan-2026-07-12)
- [p3-12-f-q2-b-database-evolution-plan-2026-07-12](/records/p3-12-f-q2-b-database-evolution-plan-2026-07-12)
- [p3-12-f-q2-b-database-evolution-validation-2026-07-12](/records/p3-12-f-q2-b-database-evolution-validation-2026-07-12)
- [p3-12-f-q2-c-version-governance-plan-2026-07-12](/records/p3-12-f-q2-c-version-governance-plan-2026-07-12)
- [p3-12-f-release-go-candidate-runtime-validation-2026-07-12](/records/p3-12-f-release-go-candidate-runtime-validation-2026-07-12)
- [p3-6-public-growth-observation-record-template](/records/p3-6-public-growth-observation-record-template)
- [p3-6-public-growth-observation-record-2026-05-20](/records/p3-6-public-growth-observation-record-2026-05-20)
- [p3-6-public-growth-observation-record-2026-05-18](/records/p3-6-public-growth-observation-record-2026-05-18)
- [p3-7-webos-workspace-screening-record-2026-05-21](/records/p3-7-webos-workspace-screening-record-2026-05-21)
- [p3-9-b-login-mobile-main-path-record-2026-06-07](/records/p3-9-b-login-mobile-main-path-record-2026-06-07)
- [p3-9-c-public-visitor-sharing-record-2026-06-07](/records/p3-9-c-public-visitor-sharing-record-2026-06-07)
- [p3-9-d-console-troubleshooting-governance-record-2026-06-07](/records/p3-9-d-console-troubleshooting-governance-record-2026-06-07)
- [p3-9-e-release-candidate-pr-prep-record-2026-06-08](/records/p3-9-e-release-candidate-pr-prep-record-2026-06-08)
- [p3-9-f-post-merge-release-candidate-prep-record-2026-06-08](/records/p3-9-f-post-merge-release-candidate-prep-record-2026-06-08)
- [p3-9-release-candidate-main-path-acceptance-matrix-2026-06-07](/records/p3-9-release-candidate-main-path-acceptance-matrix-2026-06-07)
- [p3-10-b8-pet-phase-b-contract-record-2026-06-15](/records/p3-10-b8-pet-phase-b-contract-record-2026-06-15)
- [p3-10-b8-pet-release-candidate-regression-record-2026-06-15](/records/p3-10-b8-pet-release-candidate-regression-record-2026-06-15)
- [p3-10-b9-user-identity-first-batch-record-2026-06-15](/records/p3-10-b9-user-identity-first-batch-record-2026-06-15)
- [p3-10-stage-smoke-record-2026-06-17](/records/p3-10-stage-smoke-record-2026-06-17)
- [p3-10-d-pre-merge-validation-record-2026-06-19](/records/p3-10-d-pre-merge-validation-record-2026-06-19)
- [p3-10-d-web-feed-pr-prep-record-2026-06-19](/records/p3-10-d-web-feed-pr-prep-record-2026-06-19)
- [p3-10-next-product-governance-review-2026-06-21](/records/p3-10-next-product-governance-review-2026-06-21)
- [p3-10-stage-closure-prep-record-2026-06-21](/records/p3-10-stage-closure-prep-record-2026-06-21)
- [p3-11-release-candidate-acceptance-matrix-2026-06-21](/records/p3-11-release-candidate-acceptance-matrix-2026-06-21)
- [p3-11-light-revisit-gap-audit-2026-06-21](/records/p3-11-light-revisit-gap-audit-2026-06-21)
- [p3-11-stage-closure-decision-record-2026-06-21](/records/p3-11-stage-closure-decision-record-2026-06-21)
- [p3-12-a-webos-web-function-asset-inventory-2026-06-21](/records/p3-12-a-webos-web-function-asset-inventory-2026-06-21)
- [p3-12-b1-account-shop-web-plan-2026-06-21](/records/p3-12-b1-account-shop-web-plan-2026-06-21)
- [p3-12-c1-webos-residual-cleanup-2026-06-21](/records/p3-12-c1-webos-residual-cleanup-2026-06-21)
- [p3-12-b2-personal-center-web-plan-2026-06-21](/records/p3-12-b2-personal-center-web-plan-2026-06-21)
- [p3-12-b3-forum-author-web-plan-2026-06-21](/records/p3-12-b3-forum-author-web-plan-2026-06-21)
- [p3-12-b4-doc-author-ownership-plan-2026-06-22](/records/p3-12-b4-doc-author-ownership-plan-2026-06-22)
- [p3-12-b4-2-console-doc-governance-design-2026-06-22](/records/p3-12-b4-2-console-doc-governance-design-2026-06-22)
- [p3-12-b4-d1-stage-smoke-record-2026-06-22](/records/p3-12-b4-d1-stage-smoke-record-2026-06-22)
- [p3-12-b5-web-workbench-entry-design-2026-06-22](/records/p3-12-b5-web-workbench-entry-design-2026-06-22)
- [p3-12-b6-identity-contract-convergence-design-2026-06-22](/records/p3-12-b6-identity-contract-convergence-design-2026-06-22)
- [p3-12-d1-unified-ui-design-prep-2026-06-22](/records/p3-12-d1-unified-ui-design-prep-2026-06-22)
- [p3-12-d2-public-web-unified-design-source-2026-06-24](/records/p3-12-d2-public-web-unified-design-source-2026-06-24)
- [p3-12-d3-private-web-workflows-design-source-2026-06-25](/records/p3-12-d3-private-web-workflows-design-source-2026-06-25)
- [p3-12-d4-web-ui-foundation-design-source-2026-06-25](/records/p3-12-d4-web-ui-foundation-design-source-2026-06-25)
- [p3-12-d5-console-governance-workbench-redesign-2026-06-27](/records/p3-12-d5-console-governance-workbench-redesign-2026-06-27)
- [p3-12-d6-console-visual-code-prep-2026-06-27](/records/p3-12-d6-console-visual-code-prep-2026-06-27)
- [p3-12-d7-mobile-navigation-design-source-alignment-2026-06-28](/records/p3-12-d7-mobile-navigation-design-source-alignment-2026-06-28)
- [p3-12-d8-radish-client-visual-first-implementation-2026-06-28](/records/p3-12-d8-radish-client-visual-first-implementation-2026-06-28)
- [p3-12-d9-radish-client-private-transaction-visual-implementation-2026-06-29](/records/p3-12-d9-radish-client-private-transaction-visual-implementation-2026-06-29)
- [p3-12-d10-radish-client-notification-message-visual-implementation-2026-06-29](/records/p3-12-d10-radish-client-notification-message-visual-implementation-2026-06-29)
- [p3-12-d11-radish-client-circle-pet-visual-implementation-2026-06-29](/records/p3-12-d11-radish-client-circle-pet-visual-implementation-2026-06-29)
- [p3-12-d12-radish-client-author-workflow-visual-implementation-2026-06-29](/records/p3-12-d12-radish-client-author-workflow-visual-implementation-2026-06-29)
- [p3-12-d13-radish-client-private-author-visual-closure-2026-06-29](/records/p3-12-d13-radish-client-private-author-visual-closure-2026-06-29)
- [p3-12-d14-radish-console-visual-first-implementation-2026-06-29](/records/p3-12-d14-radish-console-visual-first-implementation-2026-06-29)
- [p3-12-d15-radish-console-order-table-visual-migration-2026-06-29](/records/p3-12-d15-radish-console-order-table-visual-migration-2026-06-29)
- [p3-12-d16-radish-console-user-table-visual-migration-2026-06-29](/records/p3-12-d16-radish-console-user-table-visual-migration-2026-06-29)
- [p3-12-d17-radish-console-product-table-visual-migration-2026-06-29](/records/p3-12-d17-radish-console-product-table-visual-migration-2026-06-29)
- [p3-12-d18-radish-console-document-governance-visual-boundary-2026-06-29](/records/p3-12-d18-radish-console-document-governance-visual-boundary-2026-06-29)
- [p3-12-d19-radish-console-taxonomy-list-visual-migration-2026-06-29](/records/p3-12-d19-radish-console-taxonomy-list-visual-migration-2026-06-29)
- [p3-12-d20-radish-console-sticker-visual-migration-2026-06-30](/records/p3-12-d20-radish-console-sticker-visual-migration-2026-06-30)
- [p3-12-d21-radish-console-table-visual-static-closure-2026-06-30](/records/p3-12-d21-radish-console-table-visual-static-closure-2026-06-30)
- [p3-12-d22-console-complex-page-boundary-2026-06-30](/records/p3-12-d22-console-complex-page-boundary-2026-06-30)
- [p3-12-d23-radish-console-role-permission-visual-migration-2026-06-30](/records/p3-12-d23-radish-console-role-permission-visual-migration-2026-06-30)
- [p3-12-d24-radish-console-governance-workbench-outer-visual-convergence-2026-06-30](/records/p3-12-d24-radish-console-governance-workbench-outer-visual-convergence-2026-06-30)
- [p3-12-d25-radish-console-governance-workbench-internal-style-convergence-2026-06-30](/records/p3-12-d25-radish-console-governance-workbench-internal-style-convergence-2026-06-30)
- [p3-12-d26-radish-console-governance-static-closure-2026-06-30](/records/p3-12-d26-radish-console-governance-static-closure-2026-06-30)
- [p3-12-d27-radish-console-system-ops-shell-2026-06-30](/records/p3-12-d27-radish-console-system-ops-shell-2026-06-30)
- [p3-12-d28-radish-console-stage-static-closure-2026-06-30](/records/p3-12-d28-radish-console-stage-static-closure-2026-06-30)
- [p3-12-d29-radish-console-deep-form-static-closure-2026-06-30](/records/p3-12-d29-radish-console-deep-form-static-closure-2026-06-30)
- [p3-12-d30-radish-console-detail-drawer-static-closure-2026-06-30](/records/p3-12-d30-radish-console-detail-drawer-static-closure-2026-06-30)
- [p3-12-d31-radish-console-stage-smoke-2026-06-30](/records/p3-12-d31-radish-console-stage-smoke-2026-06-30)
- [p3-12-d32-radish-console-data-smoke-and-auth-webroot-2026-06-30](/records/p3-12-d32-radish-console-data-smoke-and-auth-webroot-2026-06-30)
- [p3-12-d33-radish-console-table-readability-first-closure-2026-06-30](/records/p3-12-d33-radish-console-table-readability-first-closure-2026-06-30)
- [p3-12-d34-radish-console-ops-table-static-closure-2026-06-30](/records/p3-12-d34-radish-console-ops-table-static-closure-2026-06-30)
- [p3-12-d35-radish-console-table-interaction-code-closure-2026-06-30](/records/p3-12-d35-radish-console-table-interaction-code-closure-2026-06-30)
- [p3-12-d36-ui-topic-gap-and-exit-criteria-2026-06-30](/records/p3-12-d36-ui-topic-gap-and-exit-criteria-2026-06-30)
- [p3-12-d37-ui-design-source-gap-matrix-2026-07-01](/records/p3-12-d37-ui-design-source-gap-matrix-2026-07-01)
- [p3-12-d38-ui-boundary-and-stage-acceptance-plan-2026-07-01](/records/p3-12-d38-ui-boundary-and-stage-acceptance-plan-2026-07-01)
- [p3-12-d39-gateway-pc-mobile-stage-acceptance-2026-07-01](/records/p3-12-d39-gateway-pc-mobile-stage-acceptance-2026-07-01)
- [p3-12-d40-ui-topic-exit-decision-2026-07-01](/records/p3-12-d40-ui-topic-exit-decision-2026-07-01)
- [p3-12-d41-page-development-gap-source-audit-2026-07-01](/records/p3-12-d41-page-development-gap-source-audit-2026-07-01)
- [p3-12-d42-docs-author-action-closure-2026-07-02](/records/p3-12-d42-docs-author-action-closure-2026-07-02)
- [p3-12-d43-public-private-data-state-closure-2026-07-02](/records/p3-12-d43-public-private-data-state-closure-2026-07-02)
- [p3-12-d44-console-deep-action-review-2026-07-02](/records/p3-12-d44-console-deep-action-review-2026-07-02)
- [p3-12-d45-mobile-responsive-sampling-2026-07-02](/records/p3-12-d45-mobile-responsive-sampling-2026-07-02)
- [p3-12-d46-pencil-ui-implementation-completion-review-2026-07-02](/records/p3-12-d46-pencil-ui-implementation-completion-review-2026-07-02)
- [p3-12-d47-ui-evidence-and-candidate-validation-checklist-2026-07-02](/records/p3-12-d47-ui-evidence-and-candidate-validation-checklist-2026-07-02)
- [p3-12-d48-ui-candidate-preflight-validation-2026-07-02](/records/p3-12-d48-ui-candidate-preflight-validation-2026-07-02)
- [p3-12-d49-ui-candidate-gateway-smoke-2026-07-02](/records/p3-12-d49-ui-candidate-gateway-smoke-2026-07-02)
- [p3-12-d50-ui-gap-recheck-and-next-implementation-order-2026-07-02](/records/p3-12-d50-ui-gap-recheck-and-next-implementation-order-2026-07-02)
- [p3-12-d51-radish-client-private-author-mobile-ui-first-closure-2026-07-02](/records/p3-12-d51-radish-client-private-author-mobile-ui-first-closure-2026-07-02)
- [p3-12-d52-radish-client-public-mobile-ui-alignment-2026-07-02](/records/p3-12-d52-radish-client-public-mobile-ui-alignment-2026-07-02)
- [p3-12-d53-public-web-pencil-first-alignment-closure-2026-07-02](/records/p3-12-d53-public-web-pencil-first-alignment-closure-2026-07-02)
- [p3-12-d54-private-author-pencil-first-alignment-2026-07-04](/records/p3-12-d54-private-author-pencil-first-alignment-2026-07-04)
- [p3-12-d55-console-responsive-ui-gap-pullback-2026-07-04](/records/p3-12-d55-console-responsive-ui-gap-pullback-2026-07-04)
- [p3-12-d56-console-gateway-grouped-page-smoke-2026-07-04](/records/p3-12-d56-console-gateway-grouped-page-smoke-2026-07-04)
- [p3-12-d57-console-deep-interaction-data-state-review-2026-07-04](/records/p3-12-d57-console-deep-interaction-data-state-review-2026-07-04)
- [p3-12-d58-ui-candidate-acceptance-prep-2026-07-04](/records/p3-12-d58-ui-candidate-acceptance-prep-2026-07-04)
- [p3-12-d59-ui-candidate-validation-execution-2026-07-04](/records/p3-12-d59-ui-candidate-validation-execution-2026-07-04)
- [p3-12-d60-pencil-page-ui-and-function-gap-review-2026-07-04](/records/p3-12-d60-pencil-page-ui-and-function-gap-review-2026-07-04)
- [p3-12-d61-public-web-discover-pencil-first-implementation-2026-07-04](/records/p3-12-d61-public-web-discover-pencil-first-implementation-2026-07-04)
- [p3-12-d62-private-author-workbench-first-implementation-2026-07-04](/records/p3-12-d62-private-author-workbench-first-implementation-2026-07-04)
- [p3-12-d62-me-content-history-first-implementation-2026-07-05](/records/p3-12-d62-me-content-history-first-implementation-2026-07-05)
- [p3-12-d62-assets-orders-inventory-first-implementation-2026-07-05](/records/p3-12-d62-assets-orders-inventory-first-implementation-2026-07-05)
- [p3-12-d62-notifications-messages-first-implementation-2026-07-05](/records/p3-12-d62-notifications-messages-first-implementation-2026-07-05)
- [p3-12-d62-circle-pet-first-implementation-2026-07-05](/records/p3-12-d62-circle-pet-first-implementation-2026-07-05)
- [p3-12-d62-forum-docs-author-first-implementation-2026-07-05](/records/p3-12-d62-forum-docs-author-first-implementation-2026-07-05)
- [p3-12-d63-console-governance-workbench-first-implementation-2026-07-05](/records/p3-12-d63-console-governance-workbench-first-implementation-2026-07-05)
- [p3-12-d63-console-commerce-ops-first-implementation-2026-07-05](/records/p3-12-d63-console-commerce-ops-first-implementation-2026-07-05)
- [p3-12-d63-console-document-governance-first-implementation-2026-07-05](/records/p3-12-d63-console-document-governance-first-implementation-2026-07-05)
- [p3-12-d63-console-user-management-table-crud-first-implementation-2026-07-05](/records/p3-12-d63-console-user-management-table-crud-first-implementation-2026-07-05)
- [p3-12-d63-console-rbac-permission-matrix-first-implementation-2026-07-05](/records/p3-12-d63-console-rbac-permission-matrix-first-implementation-2026-07-05)
- [p3-12-d63-console-grouped-static-closure-and-deferred-gap-review-2026-07-05](/records/p3-12-d63-console-grouped-static-closure-and-deferred-gap-review-2026-07-05)
- [p3-12-d63-console-gateway-grouped-smoke-2026-07-05](/records/p3-12-d63-console-gateway-grouped-smoke-2026-07-05)
- [p3-12-d64-ui-candidate-acceptance-prep-2026-07-05](/records/p3-12-d64-ui-candidate-acceptance-prep-2026-07-05)
- [p3-12-d65-ui-candidate-validation-execution-2026-07-05](/records/p3-12-d65-ui-candidate-validation-execution-2026-07-05)
- [p3-12-d66-ui-candidate-runtime-validation-2026-07-05](/records/p3-12-d66-ui-candidate-runtime-validation-2026-07-05)
- [p3-12-d67-product-maturity-topic-scope-2026-07-05](/records/p3-12-d67-product-maturity-topic-scope-2026-07-05)
- [p3-12-e1-product-maturity-gap-audit-2026-07-05](/records/p3-12-e1-product-maturity-gap-audit-2026-07-05)
- [p3-12-e2-e3-a-trust-governance-public-commitments-2026-07-05](/records/p3-12-e2-e3-a-trust-governance-public-commitments-2026-07-05)
- [p3-12-e3-b-workbench-community-activity-queue-2026-07-05](/records/p3-12-e3-b-workbench-community-activity-queue-2026-07-05)
- [p3-12-e3-c-notification-action-queue-deepening-2026-07-05](/records/p3-12-e3-c-notification-action-queue-deepening-2026-07-05)
- [p3-12-e4-a-privacy-safety-boundary-hardening-2026-07-05](/records/p3-12-e4-a-privacy-safety-boundary-hardening-2026-07-05)
- [p3-12-e4-b-privacy-safety-governance-boundary-audit-2026-07-06](/records/p3-12-e4-b-privacy-safety-governance-boundary-audit-2026-07-06)
- [p3-12-e5-a-recoverable-error-feedback-hardening-2026-07-06](/records/p3-12-e5-a-recoverable-error-feedback-hardening-2026-07-06)
- [p3-12-e5-a-recoverable-error-feedback-second-batch-2026-07-06](/records/p3-12-e5-a-recoverable-error-feedback-second-batch-2026-07-06)
- [p3-12-e5-a-recoverable-error-feedback-third-batch-2026-07-06](/records/p3-12-e5-a-recoverable-error-feedback-third-batch-2026-07-06)
- [p3-12-e5-a-journey-validation-first-pass-2026-07-06](/records/p3-12-e5-a-journey-validation-first-pass-2026-07-06)
- [p3-12-e5-b-controlled-write-journey-validation-2026-07-06](/records/p3-12-e5-b-controlled-write-journey-validation-2026-07-06)
- [p3-12-e6-release-candidate-entry-decision-2026-07-06](/records/p3-12-e6-release-candidate-entry-decision-2026-07-06)
- [p3-12-e7-ui-copy-maturity-gap-audit-2026-07-06](/records/p3-12-e7-ui-copy-maturity-gap-audit-2026-07-06)
- [p3-12-e7-console-public-ui-copy-gap-audit-2026-07-07](/records/p3-12-e7-console-public-ui-copy-gap-audit-2026-07-07)
- [p3-12-e7-a-console-density-mobile-task-flow-plan-2026-07-07](/records/p3-12-e7-a-console-density-mobile-task-flow-plan-2026-07-07)
- [p3-12-e7-b-public-terminology-cleanup-2026-07-07](/records/p3-12-e7-b-public-terminology-cleanup-2026-07-07)
- [p3-12-e7-c-public-information-density-first-closure-2026-07-07](/records/p3-12-e7-c-public-information-density-first-closure-2026-07-07)
- [p3-12-e7-c-public-discover-forum-density-closure-2026-07-07](/records/p3-12-e7-c-public-discover-forum-density-closure-2026-07-07)
- [p3-12-e7-d-auth-consent-information-hierarchy-2026-07-07](/records/p3-12-e7-d-auth-consent-information-hierarchy-2026-07-07)
- [p3-12-e7-e-ui-copy-maturity-closure-decision-2026-07-08](/records/p3-12-e7-e-ui-copy-maturity-closure-decision-2026-07-08)
- [p3-12-e7-post-closure-release-preflight-2026-07-08](/records/p3-12-e7-post-closure-release-preflight-2026-07-08)
- [p3-12-e8-product-shape-gap-decision-2026-07-08](/records/p3-12-e8-product-shape-gap-decision-2026-07-08)
- [p3-12-day-end-doc-review-2026-07-06](/records/p3-12-day-end-doc-review-2026-07-06)
- [p3-12-day-end-doc-review-2026-07-05](/records/p3-12-day-end-doc-review-2026-07-05)
- [wog-1-write-operation-inventory-2026-06-20](/records/wog-1-write-operation-inventory-2026-06-20)
- [wog-2-content-interaction-consistency-plan-2026-06-20](/records/wog-2-content-interaction-consistency-plan-2026-06-20)
- [wog-3-inventory-benefit-reliability-plan-2026-06-20](/records/wog-3-inventory-benefit-reliability-plan-2026-06-20)
- [wog-4-reward-business-key-uniqueness-plan-2026-06-20](/records/wog-4-reward-business-key-uniqueness-plan-2026-06-20)
- [wog-5-management-write-version-semantics-plan-2026-06-20](/records/wog-5-management-write-version-semantics-plan-2026-06-20)
- [wog-6-cross-client-idempotency-contract-plan-2026-06-20](/records/wog-6-cross-client-idempotency-contract-plan-2026-06-20)
- [p3-8-d-flutter-next-candidate-matrix-2026-05-28](/records/p3-8-d-flutter-next-candidate-matrix-2026-05-28)
- [webos-workspace-p0-p1-screening-record-2026-05-18](/records/webos-workspace-p0-p1-screening-record-2026-05-18)
- [console-ui-consistency-evaluation-2026-05-18](/records/console-ui-consistency-evaluation-2026-05-18)
- [daily-handoff-2026-07-07](/records/daily-handoff-2026-07-07)
- [daily-handoff-2026-07-06](/records/daily-handoff-2026-07-06)
- [daily-handoff-2026-07-04](/records/daily-handoff-2026-07-04)
- [daily-handoff-2026-07-01](/records/daily-handoff-2026-07-01)
- [daily-handoff-2026-06-30](/records/daily-handoff-2026-06-30)
- [daily-handoff-2026-06-29](/records/daily-handoff-2026-06-29)
- [daily-handoff-2026-06-28](/records/daily-handoff-2026-06-28)
- [daily-handoff-2026-06-23](/records/daily-handoff-2026-06-23)
- [daily-handoff-2026-06-27](/records/daily-handoff-2026-06-27)
- [daily-handoff-2026-06-24](/records/daily-handoff-2026-06-24)
- [daily-handoff-2026-06-25](/records/daily-handoff-2026-06-25)
- [daily-handoff-2026-06-22](/records/daily-handoff-2026-06-22)
- [daily-handoff-2026-06-21](/records/daily-handoff-2026-06-21)
- [daily-handoff-2026-06-20](/records/daily-handoff-2026-06-20)
- [daily-handoff-2026-06-19](/records/daily-handoff-2026-06-19)
- [daily-handoff-2026-06-18](/records/daily-handoff-2026-06-18)
- [daily-handoff-2026-06-17](/records/daily-handoff-2026-06-17)
- [daily-handoff-2026-06-16](/records/daily-handoff-2026-06-16)
- [daily-handoff-2026-06-15](/records/daily-handoff-2026-06-15)
- [daily-handoff-2026-06-14](/records/daily-handoff-2026-06-14)
- [daily-handoff-2026-06-13](/records/daily-handoff-2026-06-13)
- [daily-handoff-2026-06-10](/records/daily-handoff-2026-06-10)
- [daily-handoff-2026-06-09](/records/daily-handoff-2026-06-09)
- [daily-handoff-2026-06-08](/records/daily-handoff-2026-06-08)
- [daily-handoff-2026-06-06](/records/daily-handoff-2026-06-06)
- [daily-handoff-2026-06-04](/records/daily-handoff-2026-06-04)
- [daily-handoff-2026-06-03](/records/daily-handoff-2026-06-03)
- [daily-handoff-2026-06-02](/records/daily-handoff-2026-06-02)
- [daily-handoff-2026-06-01](/records/daily-handoff-2026-06-01)
- [daily-handoff-2026-05-31](/records/daily-handoff-2026-05-31)
- [daily-handoff-2026-05-28](/records/daily-handoff-2026-05-28)
- [daily-handoff-2026-05-27](/records/daily-handoff-2026-05-27)
- [daily-handoff-2026-05-26](/records/daily-handoff-2026-05-26)
- [daily-handoff-2026-05-25](/records/daily-handoff-2026-05-25)
- [daily-handoff-2026-05-24](/records/daily-handoff-2026-05-24)
- [daily-handoff-2026-05-23](/records/daily-handoff-2026-05-23)
- [daily-handoff-2026-05-22](/records/daily-handoff-2026-05-22)
- [daily-handoff-2026-05-21](/records/daily-handoff-2026-05-21)
- [daily-handoff-2026-05-20](/records/daily-handoff-2026-05-20)
- [daily-handoff-2026-05-19](/records/daily-handoff-2026-05-19)
- [daily-handoff-2026-05-18](/records/daily-handoff-2026-05-18)
- [flutter-android-rc-distribution](/records/flutter-android-rc-distribution)
- [tauri-webos-desktop-distribution-evaluation-2026-05-05](/records/tauri-webos-desktop-distribution-evaluation-2026-05-05)
- [shop-console-management-acceptance-record-2026-05-12](/records/shop-console-management-acceptance-record-2026-05-12)
- [comment-tree-compat-retirement-checklist](/records/comment-tree-compat-retirement-checklist)
- [react-capacitor-tauri-spike-record-2026-05-04](/records/react-capacitor-tauri-spike-record-2026-05-04)
找“今天改完要跑什么”先看[专题回归索引](/guide/regression-index)；找规范化回归记录写法先看[变更回归记录模板](/records/change-regression-record-template)；Flutter Android MVP 验证事实与发布 / 部署 / 回滚历史分别见[Flutter 验证索引](/records/flutter-android-mvp-validation-index-2026-05-02)和本页第四部分。
