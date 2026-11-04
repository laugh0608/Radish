# Windows SQLite 临时测试夹具修正与本地验证

> 2026-11-04（Asia/Shanghai）。所有者批准 Windows SQLite 测试夹具修正、本地验证与文档同步；远端验收待新的完整 SHA 和执行范围明确后单独确认。方案归属为[成组修正第 8 节](../features/sqlite-hosted-validation-repair-plan.md#windows-fixture-repair)。

## 1. 代码与运行边界

本批在 `dev` 上实施，开始时工作区干净，基线完整 SHA 为 `97e9c4a696d671d768ab77f0048cc9496ac73cc8`。验证针对本批提交前工作区；该基线不包含本次修正，包含本批修正的新提交以 Git 历史为准。

唯一代码改动为 `Radish.Api.Tests/Auth/AuthOpenIddictMigrationTest.cs`：三个文件型 SQLite 用例统一通过 `CreateTemporarySqliteDatabaseConfig` 创建配置，以 `SqliteConnectionStringBuilder` 指定临时路径和 `Pooling = false`。`RuntimeDatabaseConfig` 与 EF 使用同一字符串；三个用例原有的 `await using`、迁移断言及 finally 中直接删除数据库均保留。

只读核对了相邻夹具：`SqliteNativeContractTests` 的文件型 WAL 用例已显式关闭连接池，所有连接、事务、命令与 reader 均在目录清理前释放；内存 SQLite 用例没有文件删除边界。没有修改 PostgreSQL 测试、生产连接池、NuGet / npm / Cargo 声明与锁文件、CI 编排器或 workflow，也没有新增通用夹具抽象、全局清池、GC、删除重试或异常吞并。

历史三项 Windows 失败见[第二次托管记录](./sqlite-hosted-native-second-run-2026-10-31.md#3-windows-新失败临时-sqlite-文件生命周期)。默认连接池持有物理连接与该现象相符，但没有取得原 Windows 句柄所有者证据；本机通过不能裁决该平台已修复。

## 2. 本机验证

环境为 macOS `27.0.1` / arm64，.NET SDK `10.0.300`，.NET / ASP.NET Core runtime `10.0.8`，Node `24.16.0`。复用已有 assets、NuGet 缓存和 native 产物，没有执行 restore、依赖安装或工具链更新；本机工具版本不替代托管方案冻结的工具输入。

执行命令：

```bash
dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj -c Debug --no-restore --warnaserror
dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj -c Release --no-restore --warnaserror
```

双配置均成功，**0 warning / 0 error**。项目引用覆盖 Api、Auth、DbMigrate、Gateway 及关联库；构建内已有 SQLite 原始 assets、五个输出所有者检查与 native 图片产物校验均通过。这是 Tests 项目及引用构建，不是本轮全解决方案或全量后端测试结论。

测试使用[托管方案](../features/sqlite-hosted-native-validation-plan.md#41-三个平台-job)原有五组 filter，显式排除 PostgreSQL：

```bash
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -c Debug --no-build --no-restore --filter '(FullyQualifiedName~SqliteNativeContractTests|FullyQualifiedName~AuthOpenIddictMigrationTest|FullyQualifiedName~DbMigrateBootstrapTests|FullyQualifiedName~ImageProcessorTest|FullyQualifiedName~AttachmentServiceUploadContractTest)&Database!=PostgreSQL' --logger 'trx;LogFileName=Debug.trx' --results-directory artifacts/sqlite-windows-fixture-local/2026-11-04
dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -c Release --no-build --no-restore --filter '(FullyQualifiedName~SqliteNativeContractTests|FullyQualifiedName~AuthOpenIddictMigrationTest|FullyQualifiedName~DbMigrateBootstrapTests|FullyQualifiedName~ImageProcessorTest|FullyQualifiedName~AttachmentServiceUploadContractTest)&Database!=PostgreSQL' --logger 'trx;LogFileName=Release.trx' --results-directory artifacts/sqlite-windows-fixture-local/2026-11-04
```

| 分组 | Debug | Release |
| --- | ---: | ---: |
| AuthOpenIddictMigrationTest（SQLite） | 3 passed | 3 passed |
| SqliteNativeContractTests | 2 passed | 2 passed |
| DbMigrateBootstrapTests | 1 passed | 1 passed |
| ImageProcessorTest | 20 passed | 20 passed |
| AttachmentServiceUploadContractTest | 34 passed | 34 passed |
| 总计 | **60 passed / 0 failed / 0 skipped** | **60 passed / 0 failed / 0 skipped** |

另行解析完整 TRX，核对发现 / 执行数及全部 outcome，并确认三个迁移用例均实际执行且通过：

- `ApplyMigrationsAsync_ShouldInitializeEmptySqliteAndBeIdempotent`
- `ApplyMigrationsAsync_ShouldAdoptCompleteEnsureCreatedSchema`
- `ApplyMigrationsAsync_ShouldRejectIncompleteEnsureCreatedSchema`

原始 TRX 与解析摘要保留在本机忽略目录 `artifacts/sqlite-windows-fixture-local/2026-11-04/`，没有上传：

| 文件 | SHA256 |
| --- | --- |
| `Debug.trx` | `aec2634ac6373867d4ce971451ec8f9b508168c3c315c6f66860ade9540d1216` |
| `Release.trx` | `fcca3cfe44bbd58cd7ccb8d12b5c02bf95e79cf795ad0663cf557e1c203447ec` |

`npm run check:repo-quality:changed` 通过：8 个变更文件的文本卫生、953 个文档 / 根治理文件的卫生与根入口同步、878 个 Markdown 文件的 1,390 个本地链接均通过。仅提示既有 `Docs/records/index.md` 篇幅超过建议值，本批不扩展索引重构。`git diff --check` 通过。

## 3. Hangfire 上游状态同步

本批只读读取 [actions/runner-images#15201](https://github.com/actions/runner-images/issues/15201) 的五条现有评论。维护者[解释](https://github.com/actions/runner-images/issues/15201#issuecomment-6575351447)，差异可能源于 image 重新特化后环境变量保留基础镜像版本，预计例行部署修复；[最新回复](https://github.com/actions/runner-images/issues/15201#issuecomment-6580306089)承诺部署后跟进，尚未确认部署完成或提供修复版本。已有项目所有者追问要求确认修复版本及各身份来源应否一致，本批未重复发送。

同步 `current.md`、依赖治理入口与 Windows Razor 方案，状态由“等待回复”改为“等待修复部署与版本确认，再确认下一次 inventory”。保留旧身份冲突与一致性门禁，不裁决旧 VM 身份、不冻结任一冲突值；没有运行 inventory / verify。

## 4. 完成结论与下一步

本批夹具修正与本机双配置定向验证已完成；**Windows 平台与整体托管验收仍未关闭**。未 push、dispatch、发布或部署，未安装依赖 / 工具或启动项目服务 / 容器，未触达生产。测试自有临时资源按现有 finally 清理，TRX 保留用于复核。

下一步按[方案第 8 节](../features/sqlite-hosted-validation-repair-plan.md#windows-fixture-repair)明确新完整候选 SHA、Windows 双配置 / publish / 冷进程及关联回归、实际 job 选择、工具输入、证据和清理，再单独确认远端操作。现有入口仍为七 job，本批没有新增仅 Windows 的入口，也没有取得下一次七 job 执行授权。
