# F4-R R1-C01 Console 订单表格—明细设计前代码事实与能力覆盖门禁

> 日期：2026-08-08（Asia/Shanghai）
>
> 状态：静态审计与 PC / Mobile / 必要关键状态正式代表设计已确认；正式代码与静态验证已完成，详见[成组实现记录](/records/f4-r-r1-c01-console-orders-implementation-2026-08-08)
>
> 范围：正式 Console `/console/orders`、订单 HTTP / Service、权限、跨资源回跳、PC / mobile 结构，以及 `R1-C01` 对普通 Console 资源页的代表边界；本记录保留设计前事实，后续实现未修改活动 `.pen` 或启动服务

## 1. 结论

- `R1-C01` 继续保持 R1，正式锚点为 `/console/orders`。高密度表格、按需明细和 mobile 从表格转为单任务列表 / 详情的规则会影响 Applications、Products、Users、Categories、Tags、Stickers、Coins 等多个资源页，不能降为 R2 / R3。
- 正式 Console 已承接订单列表、独立详情、URL 深链、筛选 / 分页、管理员备注、履约失败重试，以及用户、商品、扣款流水的受权回跳；WebOS 只有 Console 应用入口，没有独立订单治理实现需要迁移。
- 后端现有 API、`VoCanRetryFulfillment`、结构化错误、权限和事务边界足以支持代表设计，不需要在进入 Pencil 前新增日期范围、排序、批量动作、退款、导出或状态修改能力。
- 当前页面的核心问题是表面分割过多、重复摘要与动作、主表被常驻右栏挤窄、mobile 仍依赖横向表格；这些属于 R1-C01 应解决的结构问题，不应继续在旧卡片堆叠上做局部美化。
- 现有“履约失败”筛选 / 指标实际覆盖全部 `Failed`（含支付失败），LongId 筛选使用 `type="number"`，部分重试文案仍写成“发放权益”；三项均应在后续实现按既有契约校正，但不构成新增服务端能力。

## 2. 正式代表身份

建议固定为：

```text
登录受权 Console Operator
+ console.access
+ console.orders.view / console.orders.retry / console.orders.remark
+ console.users.view / console.products.view / console.coins.view
+ 最近订单第一页包含 Completed、Pending、Payment Failed、Fulfillment Failed 与 Cancelled
+ 当前显式选中一条已支付但履约失败的消耗品订单
+ PaidTime / CoinTransactionId 存在且 VoCanRetryFulfillment=true
+ 订单已有安全失败原因和一条管理员追查备注
```

选择消耗品失败订单用于验证“订单履约”而不是“仅持续权益”的长期语义；重试仍只由服务端支付证据复核决定。默认代表页不展示退款、删除、手工改状态、批量动作或成功趋势。

## 3. 能力覆盖矩阵

| 能力 | 正式代码事实 | 门禁结论 |
| --- | --- | --- |
| 路由与深链 | `/console/orders` 内部路由为 `/orders`；`orderId / orderNo / openDetail / pageIndex / pageSize / returnTo` 可恢复列表和详情 | 已承接 |
| 列表查询 | `AdminGetOrders` 支持 `userId / status / productId / orderNo / pageIndex / pageSize`，按创建时间倒序 | 已承接；不得设计未落地筛选或排序 |
| 独立详情 | `AdminGetOrder/{orderId}` 返回订单快照、支付 / 失败 / 履约证据、有效期与备注 | 已承接 |
| 状态与失败阶段 | `OrderStatus` 与 `OrderFailureStage` 分离；展示可区分支付失败和履约失败 | 已承接；筛选文案需校正 |
| 履约重试 | `console.orders.retry` + `VoCanRetryFulfillment` 控制入口；服务端再次校验真实扣款流水并按来源订单幂等发放 | 已承接；不能把前端资格写成保证成功 |
| 管理员备注 | `console.orders.remark` 独立授权，最多 `500` 字；保存不改变支付、履约或资产事实 | 已承接 |
| 资源回跳 | 受权时可进入用户、商品和胡萝卜流水，并保留合法 `returnTo` | 已承接 |
| 错误契约 | 不存在为 `404 / Order.NotFound`；重试拒绝与备注冲突为稳定 `409` Code / MessageKey | 已承接 |
| LongId | JSON 与前端类型保持字符串，URL 解析拒绝非十进制正整数 | 主链已承接；筛选控件形态需校正 |
| 权限 | 页面读、重试、备注、用户 / 商品 / 流水回跳分别受独立权限约束，服务端仍是安全边界 | 已承接 |
| WebOS / Flutter | WebOS 只按权限显示 Console 应用；Flutter 仅承接用户订单只读，不承担 Console 治理 | 无能力迁移 |

## 4. 不改变的 API、权限与写入边界

### 4.1 保留接口

```text
GET  /api/v1/Shop/AdminGetOrders
GET  /api/v1/Shop/AdminGetOrder/{orderId}
POST /api/v1/Shop/RetryGrantBenefit/{orderId}
POST /api/v1/Shop/AdminRemarkOrder/{orderId}
```

接口名 `RetryGrantBenefit` 是现有兼容契约；产品文案统一表达“重试发放 / 履约重试”，不据此改路由或把消耗品排除在外。

### 4.2 保留权限

- 页面与详情：`console.orders.view`
- 履约重试：`console.orders.retry`
- 管理员备注：`console.orders.remark`
- 用户、商品、流水回跳：分别依赖 `console.users.view / console.products.view / console.coins.view`

无权限的动作不显示为可执行按钮；代表页不通过禁用假按钮暗示用户可以申请额外权限。

### 4.3 保留写入边界

- 重试只接受 `Failed + Fulfillment + PaidTime + CoinTransactionId` 的候选订单，最终仍校验同租户、同用户、同业务 ID、同金额的成功扣款流水。
- 重试只消费不可变订单快照，并依赖来源订单唯一事实防止重复发放；不重新读取当前 Product 作为历史履约真相。
- 管理员备注只更新备注和修改审计字段；不能改订单状态、资产、背包、权益或扣款流水。
- 不新增退款、取消、删除、直接改状态、人工补余额、批量重试或批量备注。

## 5. 设计前正确性输入

### 5.1 `Failed` 筛选与指标不能继续冒充“履约失败”

服务端列表只有 `status=Failed`，结果同时可能包含 `FailureStage=Payment` 与 `FailureStage=Fulfillment`。当前筛选项和值 `5` 被标成“履约失败”，顶部指标也用全部 `Failed` 数量显示“履约失败”，会让运营者误以为结果都可进入发放重试。

正式设计采用以下口径：

- 状态筛选值 `5` 显示为“失败”，不虚构 `failureStage` 查询参数。
- 行级状态继续精确显示“支付失败 / 履约失败”。
- 摘要区若使用当前页统计，分别表达“失败订单”和“可重试”，后者只按 `VoCanRetryFulfillment=true` 计数。
- 不把前端可重试候选表述为支付证据已经复核通过。

### 5.2 LongId 筛选必须保持字符串输入

`userId / productId / orderId` 是字符串 LongId。当前 `type="number"` 虽然读取 `event.target.value`，但浏览器数字控件会引入科学计数、步进和数值输入暗示。正式设计使用普通文本框 + 数字键盘提示 / 明确 ID 标签，禁止数值格式化、千分位和 JavaScript Number 转换。

### 5.3 重试文案必须覆盖全部履约类型

服务端 `GrantOrderFulfillmentAsync` 同时处理持续权益与消耗品。正式界面使用“重试发放”或“履约重试”，不使用“重新发放权益”作为通用按钮和确认文案；确认层仍需写明订单号、当前失败阶段和操作结果。

## 6. 当前结构债

- 页面按 `20px` 纵向间距依次放置卡片化页头、四张指标卡、四张任务流卡、筛选卡、表格卡和常驻摘要卡；同一订单事实被指标、任务流和右栏重复解释，卡片之间的大缝隙削弱连续扫描。
- `admin-table-layout` 在主表旁常驻 `260–320px` 摘要栏，而表格自身固定 `1600px` 横向宽度；PC 主任务被迫横向滚动，右栏又会自动选择首条失败或第一条订单形成“伪当前项”。
- 行内、右栏和详情 Modal 重复提供查看用户、商品、流水、详情和重试；详情 footer 最多同时出现六个按钮，动作层级不清。
- 详情使用大 Modal + 平铺双列 Descriptions，订单身份、状态、支付证据、履约结果和备注没有形成明确分组；错误只以 toast / 警告块表达，旧列表数据没有显式 stale 状态。
- `1200px` 以下常驻摘要掉到表格后，`768px` 以下筛选全部纵向铺开、指标变为四张单列卡、任务流变横向卡片轨道，表格本身仍横向滚动；这不是 family-ui 要求的 mobile 卡片列表 / 单任务详情模型。
- 当前页头仍有装饰性眉题和可由标题替代的描述，违反已经确认的直接标题层级。

## 7. 正式 PC 结构建议

PC `1440 × 900` 使用正式 Console Workbench 壳层，保持高密度连续工作面：

1. 紧凑页头只保留“订单管理”、真实权限状态和来源返回；去掉装饰性眉题与重复副标题。
2. 总量、本页失败、本页可重试与本页金额收口为同一条紧凑数据带，不画趋势，不拆成大间距独立卡片。
3. 筛选、结果数量、刷新和分页与主表进入一个连续表面，垂直间距以 `8–12px` 为主；不恢复四步任务流和常驻摘要看板。
4. 表格保留订单号 / 时间、用户、商品 / 数量、金额、精确状态和紧凑动作；单位价、支付证据、有效期和长备注进入详情。
5. 显式选择行后按需打开右侧详情 inspector；未选择时表格占满主轴，不自动把失败首项当成当前订单。
6. inspector 使用发丝分隔的字段组表达订单身份、状态时间线、用户 / 商品、支付证据、履约结果和管理员备注；资源回跳与履约重试按权限和资格出现。
7. 行内只保留详情入口与必要的条件动作，其余进入 inspector；不复制当前行、右栏和 Modal 三套动作。

该结构吸收 family-ui `ui-ref-01 / 02` 的连续 KPI—筛选—表格节奏、软状态 chip 与紧凑行操作，`ui-ref-06` 的单行薄表格密度，以及 `ui-ref-13` 的订单字段分组和金额对齐；不复制外部配色、可编辑状态下拉、删除动作、物流步骤、ETA 或 SKU 体系。

## 8. PC → mobile 响应式输入

- `390 × 844` 不缩放 PC 表格：订单改为连续卡片行，优先显示订单号 / 时间、商品、用户、总价和精确状态。
- 筛选进入按需 Bottom Sheet；顶部只保留结果数、筛选计数和刷新，不把四个输入框纵向铺满首屏。
- 点击订单进入路由状态承接的全屏详情任务，系统返回 / 明确返回恢复原筛选、分页和滚动语境。
- 详情按状态与动作、订单快照、支付证据、履约结果、备注顺序单列；重试确认使用共享确认层，备注保存保持明确成功 / 冲突反馈。
- Console mobile 壳层与底部功能入口继续复用现有产品契约；订单详情任务打开时不得让浮动功能栏遮挡保存或确认动作。

Pencil 阶段已按顺序完成 PC、Mobile 列表、Mobile 全屏详情任务和必要小型关键状态；未复制等价整页。整组设计确认后，下一步进入正式 Console 代码实现。

### 8.1 PC 代表画板确认结果

- 唯一活动设计源已新增并确认 `R1-C01 / Console 订单管理 / PC 1440` 顶层画板，保持正式 Console Workbench 壳层、紧凑数据带和连续筛选—表格—详情工作面。
- 主表使用约 `48.5px` 单行密度；订单号、时间、用户、商品 / 数量、金额、精确状态和操作独立成列，不使用双行单元格或卡片式行。
- 显式选择已支付但履约失败订单后打开右侧 inspector；详情继续保留身份、支付证据、履约结果、管理员备注、受权资源回跳和服务端复核后的“重试发放”。
- PC 画板结构与布局检查零告警；随后继续完成 Mobile / 关键状态，整个设计批次未进入代码、服务启动或浏览器复核。

### 8.2 Mobile 与必要关键状态确认结果

- 唯一活动设计源已新增并确认 `R1-C01 / Console 订单列表 / Mobile 390`、`R1-C01 / Console 订单详情任务 / Mobile 390` 与 `R1-C01 / Console 订单必要关键状态` 三张顶层画板，并与 PC 画板横向排列。
- Mobile 列表不缩放 PC 表格，使用连续三列订单行：左列为订单对象，中列为商品类型 / 单价与支付证据，右列为总额与详情入口；顶部只保留结果数、筛选和刷新，筛选进入按需层。
- 底部导航复用活动设计源的 Client `Mobile Tab Bar` 胶囊视觉，同时保持 Console 的“总览 / 治理 / 交易 / 权限 / 更多”五项真实入口，订单页激活“交易”。
- Mobile 详情使用隐藏全局导航的单列全屏任务，固定状态与动作、订单快照、支付 / 履约证据、失败结果和管理员备注顺序；只读 Operator、重试确认 / `409`、备注冲突、详情 `404` / 列表 stale、筛选空结果均只表达局部差异。
- 三张新增画板均关闭 placeholder，布局检查零告警；未修改 Console 代码、启动服务或执行浏览器复核。

## 9. 必要关键状态与停止线

必要状态只表达结构变化或高风险动作，不复制完整页面：

| 状态 | 表达方式 | 停止线 |
| --- | --- | --- |
| 只读 Operator | 权限状态与动作区局部变体 | 不显示备注 / 重试；资源回跳仍按各自权限 |
| 重试确认 / `409` | 确认层 + inline 结果带 | 不承诺一定成功，不自动重复请求 |
| 详情 `404` / 列表 stale | inspector / 状态带 | 不清空 last good 列表，不泄露不存在与失权差异 |
| 筛选空结果 | 表格 / 卡片列表空态 | 提供重置筛选，不新增创建订单入口 |
| Mobile 详情 | 独立全屏任务画板 | 不把 PC inspector 压缩成窄栏 |

本批明确不设计：日期快捷范围、保存筛选、排序器、批量选择、批量重试、导出、退款、删除、人工改状态、物流流程、完整订单审计时间线或实时自动刷新。

## 10. R2 / R3 继承边界

- Applications、Products、Users、Categories、Tags、Stickers、Coins 等普通资源页继承 R1-C01 的连续表面、表格密度、筛选带、行选择、按需详情和 mobile 卡片 / 单任务转换，但不继承订单字段或履约动作。
- Documents、Experience、Moderation / Appeals 具有证据、案件、审计或多步骤决定，继续由 `R1-C02` 或对应局部代表承接，不强行套入普通资源表格。
- Roles / Permissions、System Config、Settings / Profile 继续由 `R2-C03` 表达设置、权限矩阵和危险确认差异。
- 派生页若新增批量动作、复杂选择、跨行比较或新的 mobile 交互模型，必须重新评分，不因 R1-C01 已存在而自动降级。

## 11. 主要证据

- `Docs/planning/current.md`
- `Docs/frontend/f4-r-representative-page-audit.md`
- `Docs/frontend/pencil-representative-page-workflow.md`
- `Docs/frontend/ui-addendum.md`
- `Docs/guide/shop-order.md`
- `Docs/guide/shop-backend.md`
- `Docs/guide/console-modules.md`
- `Docs/guide/console-permission-coverage-matrix.md`
- `Docs/frontend/client-console-navigation-contract.md`
- `/Users/luobo/Code/RadishX/docs/design/family-ui/README.md`
- `/Users/luobo/Code/RadishX/docs/design/family-ui/references.md`
- `/Users/luobo/Code/RadishX/docs/design/family-ui/06-components.md`
- `/Users/luobo/Code/RadishX/docs/design/family-ui/07-layout-platforms.md`
- `Frontend/radish.console/src/pages/Orders/OrderList.tsx`
- `Frontend/radish.console/src/pages/Orders/OrderDetail.tsx`
- `Frontend/radish.console/src/pages/Orders/orderListUrlState.ts`
- `Frontend/radish.console/src/pages/Orders/orderPresentation.ts`
- `Frontend/radish.console/src/api/shopApi.ts`
- `Radish.Api/Controllers/ShopController.cs`
- `Radish.Service/OrderService.cs`
- `Radish.Model/ViewModels/OrderVo.cs`
