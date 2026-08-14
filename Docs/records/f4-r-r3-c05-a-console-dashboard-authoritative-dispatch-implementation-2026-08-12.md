# F4-R R3-C05-A Console Dashboard 权威调度面实现

> 日期：2026-08-12（Asia/Shanghai）
>
> 状态：代码实现与静态门禁完成；按当轮约束未启动服务、浏览器或运行态验收
>
> 范围：Dashboard 统计 / 最近订单权威读取、高频任务路径、PC / Mobile 最近订单承载与结构化错误反馈

## 本批结论

R3-C05-A 已关闭 Dashboard 首次失败伪零值、刷新失败清空快照、静态入口冒充实时队列、重复命令入口和 Mobile 订单横向表格等问题。统计与最近订单继续消费既有独立资源，不新增跨模块聚合队列、趋势或 BI API；页面只负责调度入口、紧凑规模摘要和最近交易回看。既有 Console 权限、路由、数据模型与接口保持不变，未修改 Pencil。

## 权威读取与错误边界

- 统计和最近订单分别维护请求代际、权威快照与 `loading / ready / unavailable / stale`；过期响应不能覆盖当前结果。
- 首次读取失败保持 `null`，指标显示明确无快照状态，订单区只显示 unavailable 诊断，不再用零值或空列表伪装真实结果。
- 已有快照刷新失败进入 `stale` 并保留原数据；统计与订单可分别重试，页头只保留一个统一刷新入口。
- `statisticsApi` 与既有订单列表 API 使用 `createApiResponseError` 保留结构化响应字段，并允许调用方传入当前语言的回退文案；Dashboard 反馈不再丢失服务端结构化错误。

## 调度与响应式页面

- 静态“优先处理队列”改名为“高频任务路径”，文案明确它由长期治理职责和当前账号权限裁剪，不代表实时待办数量或排序。
- 删除页头重复治理 / 订单按钮、独立命令组和不能直接创建商品的伪“新建商品”命令；完整路由仍由“全部功能”面板和正式侧栏承载。
- 页面顺序固定为高频任务路径、独立统计快照、最近订单证据和全部功能；主调度任务先于低频完整能力地图。
- PC 最近订单使用紧凑表格，Mobile 使用同一 `recentOrders` 快照的订单摘要卡；两端复用同一详情深链、状态展示与金额口径。
- 中英文补齐读取状态、stale / unavailable 诊断、重试、空态与 Mobile 字段；首次失败、刷新失败和成功空结果可明确区分。

## 静态验证

| 门禁 | 结果 |
| --- | --- |
| `StatisticsServiceTest` | `4 / 4` 通过，既有 Dashboard 聚合口径未回归 |
| R3-C05-A Dashboard 定向合同 | `4 / 4` 通过，覆盖独立代际、无伪零值、单一刷新、无伪命令、PC / Mobile 同快照和结构化错误 |
| `npm run test --workspace=radish.console` | `110 / 110` 通过 |
| `npm run type-check --workspace=radish.console` | 普通与 strict type-check 通过 |
| `npm run lint --workspace=radish.console` | 通过，`0 warning` |
| `npm run build --workspace=radish.console` | production build 通过 |
| `npm run check:console-permissions` | 通过；只保留既有 `console.hangfire.replay` 未引用警告 |
| `git diff --check`、changed hygiene | 通过 |

## 停止线与下一步

- 未新增数据库、migration、权限键、统计字段、跨模块推荐 / BI 系统、实时待办队列或万能 Dashboard 状态层。
- 未启动 API、Gateway、Auth、Console dev server 或浏览器；PC / Mobile、双语、代表主题和角色权限运行态复核留到 R3-C05 四批代码与静态门禁成组关闭后申请授权执行。
- 未修改 Pencil；既有 R3 继承足以表达本批主任务和 Mobile 顺序。
- 下一顺位进入 `R3-C05-B Channel Discoverability`，关闭 URL 权威查询、列表 / 历史独立竞态、历史真实分页、CAS 草稿保留与 Mobile 频道 / 事件卡；不提前实施 Documents 或 Experience 的数据模型改造。
