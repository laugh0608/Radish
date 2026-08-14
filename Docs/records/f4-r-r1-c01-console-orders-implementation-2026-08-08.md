# F4-R R1-C01 Console 订单表格—明细成组实现

> 日期：2026-08-08（Asia/Shanghai）
>
> 状态：正式代码、静态验证与 Gateway PC / mobile 运行态验收已完成
>
> 范围：`/console/orders`、订单详情 inspector / Mobile 全屏任务、Console Mobile 导航、订单展示与定向契约测试；不修改 Pencil、服务端 API、权限、事务或数据模型

## 1. 结论

- 正式 `/console/orders` 已按确认设计从卡片堆叠、常驻摘要和横向 mobile 表格，收敛为连续订单工作面。
- PC 使用约 `48px` 的单行薄表格；只有显式选择订单后才打开右侧 inspector，未选中时列表独占主轴。
- Mobile 使用连续三列订单行，中列固定承载商品类型 / 单价与支付证据；筛选进入共享 `BottomSheet`，详情成为隐藏 Console 全局导航的全屏任务。
- 只读 Operator 不渲染备注或履约重试；用户、商品和扣款流水入口继续分别受原权限控制。
- `Failed` 筛选与本页失败统计已统一为全部失败订单，本页可重试数量仅按服务端 `VoCanRetryFulfillment` 统计；LongId 筛选改为保留字符串的文本输入；产品文案统一为“重试发放 / Retry fulfillment”。
- 后续授权批已通过 Gateway 完成 PC `1440 × 900` 与 Mobile `390 × 844` CSS viewport 运行态验收；活动 `.pen`、服务端契约与运行时代码均未追加修改。

## 2. 页面与状态实现

### 2.1 列表与详情

- 列表头、四项紧凑数据带、筛选和结果表进入同一连续表面；移除四步任务流、常驻摘要和自动选中失败首单的伪当前项。
- PC 表格保留订单号、时间、用户、商品 / 数量、金额、精确状态与详情入口；单价、支付证据、履约结果、有效期和备注进入 inspector。
- Mobile 订单行分为订单对象、商品 / 支付证据、金额 / 状态三列；提供保持 URL 分页语境的上一页 / 下一页入口。
- 详情顺序固定为状态与动作、订单快照、支付证据、履约结果、管理员备注；Mobile 详情打开时 `AdminLayout` 隐藏 Header、Breadcrumb 和五项全局导航，关闭后恢复原 URL 筛选、分页与底层滚动语境。

### 2.2 关键状态

- 列表刷新失败时不清空 last-good 数据，显示 stale 状态并保留显式刷新入口。
- 详情 `404 / 410 / NotFound` 使用统一“当前不可用”状态，不区分不存在与失权；列表快照可继续阅读，但备注与重试失败关闭。
- 重试确认明确展示订单号、当前失败状态和服务端支付 / 幂等复核边界；确认层提交后立即关闭，避免同一确认按钮重复触发请求。
- 重试 `409` 与备注冲突使用详情内 inline 结果带；不自动重复请求，备注冲突时保留当前编辑草稿。
- 筛选无结果时提供重置入口，不增加创建订单动作。

## 3. 保持不变的契约

- API 继续使用 `AdminGetOrders`、`AdminGetOrder/{orderId}`、`RetryGrantBenefit/{orderId}` 与 `AdminRemarkOrder/{orderId}`。
- 权限继续使用 `console.orders.view / retry / remark` 以及用户、商品、胡萝卜流水各自的查看权限。
- `orderId / userId / productId` 继续全链保持 LongId 字符串；URL 仍拒绝非十进制正整数，不经过 JavaScript `Number`。
- 重试资格仍以服务端详情和写接口复核为准，前端 `VoCanRetryFulfillment` 只决定候选入口；幂等、结构化错误和事务边界未改变。
- 未增加日期范围、排序、批量、导出、退款、删除、手工状态、物流、完整审计时间线或实时刷新。

## 4. 自动化与静态验证

- `npm test --workspace=radish.console`：`66 / 66` 通过。
- `npm run lint --workspace=radish.console`：通过，`0 warning`。
- `npm run type-check --workspace=radish.console`：普通与 strict TypeScript 检查通过。
- `npm run build --workspace=radish.console`：production build 通过。
- `npm run check:repo-hygiene:changed`：通过。
- `git diff --check`：通过。

新增契约测试覆盖 PC 薄表格、Mobile 三列订单行、按需筛选层、全屏任务导航停止线、详情权威读取失败关闭、`Failed` / 可重试统计分离、LongId 文本输入、既有重试 API 与通用履约文案。

## 5. Gateway 运行态验收

- PC `1440 × 900` 下复核连续单行薄表格、显式选择后的 `372px` inspector、LongId 字符串 URL、第二页、`Failed` 筛选与统计、筛选空结果和受权资源回跳；页面宽度保持 `1440px`，无横向溢出。
- Mobile `390 × 844` CSS viewport 下复核连续三列订单行、按需 `BottomSheet` 筛选、Console 五项真实入口与全屏详情任务；详情为 `390 × 844`，打开时全局导航数量为 `0`，页面宽度保持 `390px`。浏览器技能不提供设备像素比控制，因此本次不把结果表述为 DPR `3` 实跑。
- 管理员路径真实覆盖重试确认与服务端 `409`、备注成功与冲突草稿保留、详情 `404`、列表 last-good stale、完整 LongId 空结果；列表 stale 使用浏览器网络阻断进行受控模拟，结束前已恢复网络规则。
- 临时只读 Operator 真实登录后页面显示“只读”，可查看列表与详情，不渲染“重试发放”、管理员备注或保存入口；稳定态新标签的浏览器控制台为 `0 error / 0 warning`。
- `./start.sh` 启动阶段构建为 `0 warning / 0 error`。验收结束后五个服务端口均已停止，六个 SQLite 数据库按验收前 SHA-256 快照原样还原且 `PRAGMA quick_check` 全部为 `ok`；临时订单、Operator 与冲突触发器计数均为 `0`。

## 6. 停止线

- R1-C01 已关闭；本次运行态复核未发现需要回写 Pencil 或修正正式代码的结构偏差。
- R1-C02 未开始；下一任务应先执行 `/console/moderation` 的设计前代码事实与能力覆盖门禁，再决定代表设计与实现范围。
