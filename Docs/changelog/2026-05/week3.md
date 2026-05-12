# 2026 年 5 月第 3 周开发日志

> 范围：2026-05-11 至 2026-05-17（Asia/Shanghai）
>
> 本周继续沿“产品功能补全与多端任务重排”推进，执行面集中在后端 + Console 治理，优先收口经验治理、内容治理审核效率与商城管理前端缺口。

## 2026-05-11

### 主线判断

- 当日执行重点继续落在后端 + Console 治理，不再新增大范围产品线，而是把已进入当前主线的经验治理、内容治理和商城管理缺口补成可用工作流。
- `P2-C3` 经验 / 等级治理保持“最小可解释规则 + 人工复核”边界；内容治理继续聚焦人工审核与手动处置；商城治理则优先服务资产 / 订单人工追查。
- 当前这条治理线完成后，下一步不宜继续扩新规则或新平台，而应先做商城管理人工验收，并按结果整理剩余尾项。

### 已完成提交

- `e01901b6 fix(client): resolve desktop external app urls`
  - 修复桌面外部应用 URL 处理中的既有问题，避免继续干扰桌面工作台稳定性。
- `3eb1788e feat(console): improve moderation queue filtering`
  - 提前补了一轮内容治理审核队列筛选能力。
- `2e618ef2 docs(planning): record flutter ui library trigger`
  - 记录 Flutter UI 组件库触发点，保持规划口径连续。
- `494e36e8 feat(console): add experience transaction review`
  - 为经验治理补齐经验流水回看入口。
- `11600f92 feat(console): 收口经验异常规则治理`
  - 统一经验治理页的异常观察口径与管理端展示。
- `a2f624f6 feat(experience): 串起异常复核闭环`
  - 把异常规则摘要、异常日期与经验流水筛选联动起来，支持一键带入复核。
- `a8d99259 feat(experience): 沉淀治理复核留痕`
  - 补齐人工复核记录接口、冻结 / 解冻自动留痕，以及治理快照回看。
- `7cac6909 feat(moderation): 收口审核台效率`
  - 打通举报队列、治理日志、来源举报和目标动作的查询闭环。
- `ae631cb9 feat(moderation): 接入手动治理处置入口`
  - 审核台接入手动禁言 / 封禁 / 解封 / 解除禁言，并和治理日志联动。
- `6841d29a feat(moderation): 前置展示当前治理状态`
  - 在手动治理区直接展示目标用户当前生效中的禁言 / 封禁状态。
- `9edf138b feat(shop): 收口商城后台订单商品治理`
  - 补齐管理端订单详情、商品详情、商品删除拦截，以及订单 / 商品 / 用户三向治理回跳。
  - 收口 `shopApi` 契约漂移，并补商品 / 订单相关测试与 HTTP 示例。

### 验证记录

- `npm run build --workspace=radish.console`
  - 通过。
  - 当日经验治理、内容治理和商城治理批次均已在该构建入口下确认无新增前端失败。
- `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --filter Experience`
  - 先后通过 `13/13`、`15/15`、`20/20`，覆盖经验异常规则、复核联动与治理留痕批次。
- `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --filter ContentModeration`
  - 先后通过 `13/13`、`14/14`，覆盖审核效率、手动处置与当前治理状态批次。
- `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --filter "FullyQualifiedName~ShopControllerTest|FullyQualifiedName~OrderServiceTest|FullyQualifiedName~ProductServiceTest"`
  - 提权环境通过，`18/18`。
  - 首次在沙盒中失败原因仅为无法读取用户级 `NuGet.Config`，不是本轮代码问题。

### 文档与对齐结论

- 今日提交横跨经验治理、内容治理和商城管理前端缺口，不能只保留代码提交；本次回顾后已同步更新 [current.md](/planning/current)、[development-plan.md](/development-plan)、[phase-two-product-completion.md](/planning/phase-two-product-completion)、[Console 系统设计方案](/guide/console-system) 与 [商城系统设计方案](/guide/shop-system)。
- 月度开发日志入口已新增本周条目，并把“明日第一事项”从已完成的内容治理审核效率，切换为商城管理人工验收收口。
- 今日没有新的多端路线、壳层归属或视觉规范变化，因此不额外改动多壳层策略与视觉设计规范文档。

### 剩余风险与下一顺位

- 经验治理和内容治理已完成首轮收口，但应继续保持人工复核和手动处置边界，不扩自动处罚或大而全治理平台。
- 商城管理前端缺口已基本补齐，但仍需要一轮手工验收，重点确认商品上下架、删除拦截、订单详情 / 备注和三向回跳在真实 Console 账号下的可用性。
- 若人工验收稳定，后端 + Console 治理的下一顺位应转向剩余历史构建 warning 与少量安全治理尾项，而不是继续在已收口模块上做低收益微调。

## 2026-05-12

### 商城管理人工验收收口

- 新增 [商城管理人工验收记录（2026-05-12）](/records/shop-console-management-acceptance-record-2026-05-12)，确认商品相关订单、详情长 ID、删除确认、订单跳用户 / 商品返回链路已修复。
- 后端商品删除保护已纳入验收结论：已有任意订单记录的商品不允许删除，避免破坏历史订单追溯。

### 历史构建 warning 与安全治理尾项

- `radish.console` 已从单入口静态页面导入改为路由级懒加载，并按 `React / Router / Ant Design / @radish/ui / @radish/http` 等边界拆分产物，Console 构建不再输出 chunk size warning。
- 后端同步清理一批低风险 warning：
  - `Radish.Gateway` 使用 .NET 10 推荐的 `KnownIPNetworks` 替代过时 `KnownNetworks`。
  - `PermissionRequirementHandler` 过滤空权限 URL / 角色，并使用大小写不敏感的整段正则匹配，减少无效权限数据靠异常吞掉的安全边界噪音。
  - `AttachmentController` 补齐 `GetMyAttachments` XML 参数注释。
  - `ConsoleAuthorizationController` 未找到角色时不再显式写入泛型 `default` 响应体。
- 继续清理后端历史构建 warning：
  - `Radish.Extension` AOP / Redis / SqlSugar / Scalar 的 nullable warning 已清理，补齐 Redis 缓存未命中、Scalar 文档组件和 SqlSugar 操作名解析的空值边界。
  - `Radish.Repository` 事务 / 多租户基础仓储 warning 已清理，`ISqlSugarClient` 类型不符合事务要求时改为明确失败，分库 `TenantAttribute.configId` 缺失时给出显式异常。
  - `Radish.Common` 和 `Radish.Model` 的历史 nullable warning 已按既有语义收口，保留缓存未命中返回默认值、启动期服务后置注入、无数据响应可为空和历史背包道具展示兼容。
  - `AttachmentService` 去重发现物理文件缺失时改为软删除旧附件记录，避免维护任务继续引入物理删除口径。
- `Radish.Gateway` 已将配置源注册从 `ConfigureAppConfiguration` 等价迁移到 `WebApplicationBuilder.Configuration`，保持共享配置、宿主配置、本地覆盖和环境变量的加载顺序不变，清除最后一条 `ASP0013` analyzer warning。

### 验证记录

- `npm run build --workspace=radish.console`
  - 通过。
  - 构建产物最大 chunk 为 `vendor-antd-_util`，约 `397.53 kB`，未再触发 `500 kB` chunk size warning。
- `dotnet build Radish.slnx -c Debug -v minimal`
  - 提权环境通过。
  - 本次增量构建当前可见 warning 剩余 `24` 条，集中在 `Radish.Extension` 的 AOP、Redis、SqlSugar nullable 存量；Gateway `ASPDEPR005`、权限处理器本轮触达 warning、Attachment XML 注释 warning 与 Console 授权 `default` warning 已消失。
- `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --filter AttachmentControllerTest -v minimal`
  - 提权环境通过，`3/3`。
- `dotnet build Radish.slnx -c Debug -t:Rebuild -v minimal`
  - 提权环境通过。
  - 完整重建已达成 `0` warning / `0` error。
- `dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -v minimal`
  - 提权环境通过，`348/348`。
- `npm run check:repo-hygiene:changed`
  - 通过，未发现文本卫生问题。

### 下一顺位

- 安全治理尾项继续按小批次推进，优先看权限 / 文件访问 / 缓存默认值等边界是否还存在可测试的风险点。
