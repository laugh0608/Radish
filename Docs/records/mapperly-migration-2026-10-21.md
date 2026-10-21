# Mapperly 映射迁移记录（2026-10-21）

> 基于 `4a8589b6`，在 `dev` 实施。项目所有者批准安装 `Riok.Mapperly 4.3.1`、完整映射迁移与移除 AutoMapper；ORM / Redis 仅评估，不发布。设计与迁移前盘点见[依赖评估](../features/post-image-dependency-assessment.md)。

## 1. 交付范围

- `Radish.Service/Mapping` 按业务域维护编译期映射、字段忽略和显式业务转换；`PrivateAssets="all"` 限定生成器引用传播，未引入泛用 `Map<T>` 或运行时反射分派。
- BaseService 接收具体实体的 `Func<TEntity,TVo>`；API / Auth 调用 `AddServiceMappings`，为实际泛型服务实体对注册委托。附件映射使用 scoped URL resolver，派生服务与基类查询都生成原图和缩略图地址。
- 迁移前实际注册 104 个方向：保留 101 个；删除 `ProductCategoryVo → ProductCategory`、`UserBenefitVo → UserBenefit`、`UserInventoryVo → UserInventory` 三个没有调用且不解析任何目标字段的空映射。
- 旧配置的 `AuditLogProfile` 没有注册；旧 mapper 实测抛出 `AutoMapperMappingException`。补齐 AuditLog 查询输出（最终共 102 个方向），不启用从未使用的 `CreateAuditLogDto → AuditLog` 规则。
- 商品原地更新保留上架、版本、租户、销量及审计字段；更新 ID 仍沿用 DTO（服务按该 ID 读取实体）。未顺带改变支付口令时间 / 版本规则、数据库 schema、IService 业务接口或 API 响应契约。
- 为遵守单文件 1500 行上限，将本次涉及的 Wiki 治理方法与帖子发布 / 投票查询测试按职责移至 partial 文件；方法体和测试全名保持不变。
- 删除 Extension / Service 两处 AutoMapper 包引用、全部旧 Profile / 注册代码和共享许可配置；前端依赖展示改为 Mapperly，更新架构 / 配置 / 功能文档。历史许可盘点保留其当时事实。

## 2. 映射行为证据

迁移切换前临时对照测试同时运行原 AutoMapper 配置与新具体方法，对 **101 个方向 × 3 类输入 = 303 个用例**逐属性比较：默认对象、填充字段 / 集合、nullable / null 集合。覆盖 null 对象与反向构造校验；无显式映射且构造时产生的时间字段只比较契约，不要求两个实例时间戳相同。全部通过后删除临时旧库测试，最终测试不再依赖 AutoMapper。

对照期间确认并保留了集合复制 / null 转空、反向构造函数校验、nullable 文本、OIDC null payload、权限归一化与服务补全字段默认值。对照不是所有合法 / 非法输入的穷举。

永久回归入口为 `Radish.Api.Tests/Mapping/`，覆盖身份与密码排除、列表 / null、撤回消息、Wiki 权限、OIDC secret / payload、商品写入保护、支付状态、构造校验、BaseService 分页、AuditLog 实际服务读取及 Microsoft DI / Autofac 附件作用域。原服务 / 控制器测试改为真实映射；日志测试保留 Category 映射阶段故障注入，将附件旧 mapper 返回 null 的模拟改为真实 URL resolver 失败，继续验证计费回滚与脱敏日志。

## 3. 依赖与产物

- 新包：`Riok.Mapperly 4.3.1`；包内 `.nuspec` 声明 Apache-2.0，无 NuGet 包依赖，附带 LICENSE；仓库来源 commit 为 `036698914bd48be888b25322938b565db72ff7ff`。
- 已刷新现有依赖图，并在 `/private/tmp/radish-mapperly-clean` 使用独立 obj / bin 重新 restore；复用本机包缓存，不删除共享缓存，不以过滤 DLL 隐藏依赖。
- 现有与独立还原目录各 19 份 `project.assets.json` 均无 AutoMapper。API / Auth / Gateway / DbMigrate 四个本机 Release publish 目录的 `.deps.json` 与文件清单均无 AutoMapper 或 Mapperly DLL；未修改输出文件来制造该结果。publish 仅生成本地验证产物，不代表部署或发布。

## 4. 验证

| 检查 | 最终结果 |
| --- | --- |
| 迁移前后行为对照 | 101 个方向、303 个用例通过 |
| 最终 Debug / Release 严格构建 | `dotnet build Radish.slnx --no-restore --warnaserror --artifacts-path /private/tmp/radish-mapperly-clean` 分别追加 `-c Debug`、`-c Release` 均通过，0 warning / error |
| 映射与附件 / 轻回应定向回归 | 24 个映射契约用例 + 68 个日志回归用例，共 92 通过 |
| 全量后端 | 4031 个用例：3989 通过、0 失败、42 跳过；约 2.73 分钟 |
| 身份专项 | `npm run validate:identity` 通过，35 个用例，无跳过；身份 / LongId 扫描通过 |
| 前端基线 | `npm run validate:baseline:quick`、Client 显式 `tsc -p tsconfig.app.json --noEmit`、`npm run lint:changed` 通过 |
| 卫生与文档 | `check:docs`、`check:repo-hygiene:changed`、`check:time-semantics`、`git diff --check` 通过；存量长文提醒不作为错误 |
| 依赖 / 本地产物 | 两套 19 份依赖图及四宿主 Release publish 清单通过，无 AutoMapper；宿主产物不包含 Mapperly DLL |

最终全量命令（标准仓库 bin；测试本身不启动开发宿主）：

```sh
DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore --logger 'trx;LogFileName=mapperly-final.trx' --results-directory /private/tmp/radish-mapperly-test-results
```

42 个跳过项均为缺少 `RADISH_TEST_POSTGRES_CONNECTION_STRING` 的 PostgreSQL 集成测试；SQLite、服务和映射用例正常执行，不将跳过计为通过。未新增数据库或启动数据库容器。

本机验证过程有三项环境差异：

- 沙盒中的证书夹具无法创建 macOS 临时钥匙串；按仓库规则最小提权后执行全量测试。
- 本机配置文件监听器出现变更回调反复注册；线程采样与 JIT 符号定位到 `ChangeTokenRegistration / FileSystemWatcher`，停在测试的 `WebApplication.CreateBuilder`。仅为测试命令设置 `DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false` 后定向 / 全量通过；没有修改应用配置或关闭产品热重载。
- 从仓库外独立产物目录运行全量测试时，部分源码契约夹具无法向上定位仓库根；最终全量改用标准仓库内 bin。独立目录仍用于 clean restore、严格构建、映射定向验证和 publish 审计。

初次失败和中断未计入最终通过结果；最终 TRX 位于上述临时结果目录。

未启动开发宿主、数据库容器或浏览器 Smoke；未执行生产访问、tag、部署或发布。本记录仅证明本地代码与产物检查，不替代后续成组运行验收。
