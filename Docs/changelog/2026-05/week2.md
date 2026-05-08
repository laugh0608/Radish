# 2026 年 5 月第 2 周开发日志

> 范围：2026-05-05 至 2026-05-10（Asia/Shanghai）
>
> 本周从多端路线验证回到产品功能补全，优先处理 WebOS / PC 工作台、后端与 Console 治理中用户能点到但不能真正完成的功能缺口。

## 2026-05-06

### 主线判断

- 当前阶段继续保持 `第二开发阶段：社区深化与多端化`，但默认执行重心已从端侧验证、分发材料和低收益微体验，切回产品功能补全。
- WebOS / PC 工作台继续承载已登录用户完整工作流，Console 与后端治理优先补模拟 API、统计、权限、安全、经验治理和商城管理缺口。
- Flutter 移动端保持高价值移动路径，不复刻桌面工作台；公开 Web 壳层只做必要稳定维护。

### 已完成提交

- `8076302d docs: 更新代码行数约束`
  - 同步通用协作规则中的单文件行数建议与硬上限口径。
- `31153430 docs: 精简规划入口文档`
  - 收束 `current.md` 与路线图入口，减少历史流水和长背景堆叠。
- `6dcd2356 feat(client): 优化工作台复访续接`
  - 补强 WebOS / PC 工作台复访续接体验。
- `76ab7501 feat(client): 增加桌面应用恢复入口`
  - 增加桌面应用恢复入口，让复访能回到更具体的应用上下文。
- `c2c64a2f docs(planning): 调整第二阶段产品补全规划`
  - 明确第二阶段回到产品功能补全后的端侧分工、近期批次和当前不做范围。
- `d71d6887 feat(radish-pit): 补齐萝卜坑工作流`
  - 接通支付密码设置 / 修改真实能力，移除模拟等待和 `alert`。
  - 快捷操作可切换到转移、记录、安全、统计；最近交易“查看全部”切到交易记录；复制流水号改为统一 toast。
- `ace78ccf feat(console): 接入用户详情治理数据`
  - 用户详情、萝卜币流水、订单列表从模拟 API 切到真实治理数据。
- `46b03a5a feat(console): 接入个人资料与设置真实能力`
  - Console 个人资料与设置保存动作从占位推进到真实后端能力。
- `e3187c37 feat(console): 接入仪表盘真实统计`
  - 新增统计仓储与服务，`StatisticsController` 从固定 / 随机占位数据切到真实聚合。
  - 补充 Console 统计接口权限种子和统计服务定向测试。
- `3eb72ba4 chore(build): 降低既有构建警告噪音`
  - 移除 `Radish.Extension` 旧 `Microsoft.AspNetCore.Mvc 2.3.0` 包引用，改用 `Microsoft.AspNetCore.App` framework reference。
  - 测试项目启用 nullable，`ImageProcessorTest` 传递 xUnit 测试取消令牌。
  - `AttachmentService` 改用 `File.ReadAllBytesAsync`，避免手写流读取 analyzer 警告。

### 验证记录

- `dotnet build Radish.slnx -c Debug`
  - 通过，`0` 错误。
  - 构建警告从约 `186` 条降至 `120` 条。
  - `NU1903 System.Security.Cryptography.Xml`、`CA2022`、`xUnit1051` 和测试项目大量 `CS8632` 噪音已消失。
- `dotnet test Radish.Api.Tests\Radish.Api.Tests.csproj -c Debug --filter FullyQualifiedName~StatisticsServiceTest`
  - 通过，`4/4`。
- `npm run build --workspace=radish.console`
  - 通过。
  - 仍保留既有 chunk size warning。
- `dotnet test Radish.Api.Tests\Radish.Api.Tests.csproj -c Debug --filter FullyQualifiedName~ImageProcessorTest`
  - 通过，`6/6`。

### 剩余风险与下一顺位

- 构建输出仍有历史 nullable、obsolete、XML 注释和 ASP.NET analyzer 警告，后续宜按模块继续治理，避免用全局抑制掩盖真实问题。
- P2-C1 与 P2-C2 已完成首轮；下一顺位建议从 P2-C3 经验 / 等级治理、P2-C4 商城权益效果收口、P2-C5 移动端高价值已登录链路中选择。
- Tauri 正式签名、自动更新、生产 Auth、SmartScreen、托盘 / 菜单和公开分发方式继续后置到真实对外分发前评估。

## 2026-05-07

### 主线判断

- 当日主线仍是“产品功能补全与多端任务重排”，但执行面新增一条横向治理线：`ID Phase A`。
- 本轮 `ID Phase A` 明确不做数据库主键迁移，也不做全量 `PublicId` 切换；先收口外部仍保留 `LongId` 的边界，避免继续在壳层、导航层、DTO、路由参数和回跳参数里提前 `Number(...)`。
- 经验 / 等级治理继续与 Console 治理并行推进，优先补管理员可真实操作的冻结、查询和护栏能力。

### 已完成提交

- `8c104ea7 chore(repo): 收口协作入口与仓库治理`
  - 对齐仓库协作入口与治理规则，减少入口文件口径漂移。
- `44766a5e chore(config): 收口 appsettings 配置约束`
  - 明确配置文件来源与允许提交的变体范围。
- `8f9642bc fix(repo): 修复 PR 质量门禁失败`
  - 修补仓库质量门禁与提交流水中的既有失败点。
- `73974c7e feat(experience): add admin freeze controls and id guardrails`
  - 补齐经验治理中的管理员冻结 / 解冻入口与 ID 护栏。
- `6be84434 fix(console): harden user api response parsing`
  - 加强 Console 用户接口响应解析，降低异常数据形态导致的前端误判。
- `882553f0 refactor(client): preserve string-safe window ids`
  - 先把桌面窗口 ID 与相关运行参数从 number 假设中剥离，避免大整数在壳层丢精度。
- `30f11283 refactor(id): keep external ids string-safe at app boundaries`
  - 收口通知 `extData`、公开 DTO / 路由参数 / 回跳参数，以及 `Profile / Shop / Wiki` 等应用边界的外部 ID。
- `2bd67411 refactor(forum): keep forum ids string-safe across client views`
  - 继续把 `Forum / Public Forum` 的 DTO、回调、评论锚点、Reaction 与公开阅读链路切到 `LongId` / 字符串安全口径。

### 验证记录

- `dotnet build D:\Code\Radish\Radish.Api\Radish.Api.csproj -c Debug -v minimal`
  - 通过。
  - 该结果承接当日批次中的后端 / Console 治理提交，没有发现本轮新增编译错误。
- `npx tsc -b`
  - `Frontend/radish.console` 通过。
  - `Frontend/radish.client` 通过。
- `npm run build --workspace=radish.client`
  - 在提权环境通过。
  - 说明沙盒中的 `vite [plugin externalize-deps] spawn EPERM` 仍属环境限制，不是本轮代码问题。
- `npm run build --workspace=radish.console`
  - 在提权环境通过。
  - 仍保留既有 chunk size warning，但不是新增失败。

### 文档与对齐结论

- 当日提交已触达仓库治理、配置约束、经验治理与 `ID Phase A`，需要同步设计 / 规划 / 日志文档，而不应只留代码提交。
- 规划入口已补一条当前关键事实：`ID Phase A` 当前只做外部 `LongId` / 字符串安全收口，不启动数据库主键迁移或全量 `PublicId` 切换。
- 长期 ID 设计文档已补当前执行策略，明确通知 `extData`、window `appParams` / deep link、公开 DTO / 路由参数 / 回跳参数，以及 `Profile / Shop / Wiki / Forum / Public Forum` 与 Console 用户 ID 入口属于当前首批治理面。

### 剩余风险与下一顺位

- 当前已把外围暴露面大幅收口，但登录态 / 当前用户 ID 仍保留旧 `number` 口径，后续若继续推进 `ID Phase A`，应优先治理共享 `UserInfo / userStore` 与其直接消费者。
- Console 构建的 chunk size warning 仍属既有问题，和本轮 ID 收口无直接关系，后续单独排序处理。
- `PublicId Rollout`、数据库主键迁移与 Snowflake 退出策略继续保留在后置池，不应借当前 Phase A 护栏批次被提前拉成主线。

## 2026-05-08

### 主线判断

- 当日主线继续保持“产品功能补全与多端任务重排”，执行面集中在 `P2-C4 商城权益效果收口`。
- 本轮优先把用户能买到但不能完整消费 / 回补 / 治理的商城断点收成统一口径，覆盖商品种子、存量上架态、公开购买判定、订单取消回补与实际扣款链路。
- 收口后暴露出的下一层真实缺口，已收束为“购买后资产闭环”：背包展示契约、支付密码校验与通知中心稳定性。

### 已完成提交

- `117cbc13 docs(db): 收口数据库结构变更协作口径`
  - 明确结构变更、`DbMigrate apply/init/seed` 与协作边界，减少本地库状态与代码口径漂移。
- `3d55bdc4 feat(frontend): 收口资产与商城治理工作流`
  - 补强资产治理与商城工作流的前端入口与批次承接。
- `91558b86 fix(console): 提升经验治理页状态可信度`
  - 修正 Console 经验治理页面的状态判断和展示可信度。
- `d0a42c0a fix(shop): 收口未开放道具与改名卡使用链路`
  - 封禁未开放道具的公开购买 / 使用入口，并补齐改名卡闭环。
- `a11876ba fix(shop): 收口经验卡链路与抽奖券入口`
  - 补齐经验卡数量闭环，收回抽奖券入口。
- `ad411831 fix(shop): 收口未开放权益商品展示与激活入口`
  - 收回未真实消费的权益商品展示、购买、激活与上架入口。
- `ff09d4a1 fix(shop): 对齐权益商品种子与上架治理口径`
  - 对齐默认商品种子、存量上架态与 Console 管理口径。
- `73cec1e4 fix(shop): 补齐订单库存快照与取消回补口径`
  - 为订单补 `StockType` 快照，并统一取消订单的库存回补链路。
- `71ee3ae1 fix(dbmigrate): 自动补齐商城订单快照列`
  - 让 `DbMigrate` 自动发现缺失列，避免 `ShopOrder.StockType` 这类新增快照字段漏补。
- `205b1140 fix(shop): 收口商品配置与商城购买链路`
  - 自动约束商品分类与消耗品 / 权益类型匹配，阻止商品弹窗遮罩误关。
  - 修复前台购买判定字段解析、拦截坏商品配置，并补独立的商城消费扣款链路与正确的订单交易关联。

### 验证记录

- `npm run type-check --workspace=radish.console`
  - 通过。
- `npm run type-check --workspace=radish.client`
  - 通过。
- `dotnet build D:\Code\Radish\Radish.Api.Tests\Radish.Api.Tests.csproj -v minimal`
  - 通过。
- `Radish.Api.Tests.exe -noLogo -reporter quiet -class "Radish.Api.Tests.Services.CoinServiceTest" -class "Radish.Api.Tests.Services.OrderServiceTest" -class "Radish.Api.Tests.Services.ProductServiceTest"`
  - 通过。

### 文档与对齐结论

- 当日提交已同时影响商城商品配置、库存 / 订单快照、`DbMigrate`、自定义购买判定与消费扣款链路，需要同步规划与开发日志，而不应只保留代码提交。
- 本次回顾后已补齐 `current.md`、`phase-two-product-completion.md` 与 `2026-05` 开发日志入口，明确 `P2-C4` 当前已收口范围和“明天第一事项”。
- 当前没有新的架构分层、端侧路线或视觉设计变更，因此不额外改动路线图总览、壳层策略或设计规范文档口径。

### 剩余风险与下一顺位

- 用户实测已确认下一层缺口在购买后闭环：背包 `UserInventoryVo` 契约与前端渲染未对齐，导致商品名称 / 数量 / 图标与 React `key` 同时失真。
- 商城购买当前仍未接支付密码，前后端 `CreateOrderDto` / 购买弹窗 / 订单服务都缺少密码参数与校验，不宜让“资产消费”继续以无密码口径上线。
- 通知中心 `NotificationApp` 当前在 Store 与本地 state 之间存在循环同步风险，点击购买成功通知会触发 `Maximum update depth exceeded` 并白屏；这应作为明天的第一事项与商城购买后闭环一起收掉。
